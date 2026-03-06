# OpenEye Platform Design

## Overview

OpenEye is a multi-service video analytics platform that converts camera streams into actionable events using configurable rules. It supports RTSP/MJPEG camera streams, bring-your-own detection models (via Roboflow Inference), a YAML/DB-configured rule engine with primitives abstraction, and a dashboard for configuration and event monitoring.

**Target domains:** retail stores, restaurants, factories, warehouses.

## Architecture

```
┌───────────────┐    ┌───────────────┐    ┌─────────────────┐    ┌──────────────┐    ┌─────────────┐
│ Frame Capture  │───▶│  Detection    │───▶│  Pipeline Core  │───▶│ Event Router │───▶│  Dashboard  │
│     (C#)       │    │ Bridge (C#)   │    │      (C#)       │    │    (C#)      │    │  (Next.js)  │
└───────────────┘    └───────┬───────┘    └─────────────────┘    └──────────────┘    └─────────────┘
                             │
                    ┌────────▼────────┐
                    │    Roboflow     │
                    │   Inference     │
                    │  (prebuilt)     │
                    └─────────────────┘

Communication: Redis Streams between services
Persistence: PostgreSQL for events, config, rules
```

### Services

| Service | Language | Role |
|---|---|---|
| **frame-capture** | C# | Connects to RTSP/MJPEG streams, decodes frames, publishes to Redis |
| **detection-bridge** | C# | Consumes frames, calls Roboflow Inference HTTP API, publishes detections to Redis |
| **pipeline-core** | C# | Tracker → Zones → Primitives → Rules. Consumes detections, publishes events to Redis |
| **event-router** | C# | Consumes events, saves to PostgreSQL, dispatches notifications (webhook, WhatsApp, email) |
| **dashboard** | Next.js / TypeScript | Config UI, real-time event feed, event history, notification setup |
| **roboflow-inference** | Prebuilt container | Model server. Users deploy their chosen detection model here |
| **redis** | Infrastructure | Message bus (Streams) + config notifications (pub/sub) |
| **postgres** | Infrastructure | Persistent storage for events, cameras, zones, primitives, rules, notifications |

### Why Two Stores

- **Redis:** Real-time message passing between services (streams), config change notifications (pub/sub), ephemeral class filter (key-value). High throughput, low latency.
- **PostgreSQL:** Event history (queryable, filterable, paginated), all configuration (cameras, zones, primitives, rules, notifications), evidence metadata. Relational queries, indexes, disk-based storage for long-term accumulation.

## Service Communication

### Redis Streams Topology

```
frames:{cameraId}        → frame-capture publishes, detection-bridge consumes
detections:{cameraId}    → detection-bridge publishes, pipeline-core consumes
events                   → pipeline-core publishes, event-router consumes
```

Each stream is keyed by camera where applicable. The `events` stream is global.

### Message Formats (JSON)

- `frames:{cameraId}` — `{ frameIndex, timestamp, framePath }` (reference to JPEG on shared volume)
- `detections:{cameraId}` — `{ frameIndex, timestamp, detections: [{ classLabel, bbox, confidence }] }`
- `events` — `{ eventId, eventType, ruleId, sourceId, zoneId, timestamp, trackedObjects, metadata, evidenceRequest? }`

### Consumer Groups

Each service uses a Redis consumer group for its input stream. Enables horizontal scaling — multiple instances of a service share the workload without duplicate processing.

### Config Change Notification

When the dashboard saves config changes to PostgreSQL, it publishes to a `config:changed` Redis channel. Pipeline-core and detection-bridge subscribe — they reload affected config without restart.

### Frame Passing Optimization

Frame-capture writes decoded frames as JPEG files to a shared Docker volume. The Redis stream message carries only the file path, not the image data. Detection-bridge reads frames by path.

## Core Data Model

### Detection (input)

- `ClassLabel` (string) — e.g., "person", "forklift", "plate"
- `BoundingBox` (x, y, width, height) — normalized 0..1
- `Confidence` (float 0..1)
- `Timestamp` (DateTimeOffset)
- `SourceId` (string) — camera identifier
- `FrameIndex` (long, optional) — for ordering

### TrackedObject

- `TrackId` (string) — stable ID assigned by tracker
- `ClassLabel` (string)
- `CurrentBox` — current bounding box
- `Trajectory` — sliding window of recent positions with timestamps
- `FirstSeen` / `LastSeen` (DateTimeOffset)
- `State` — Active, Lost, Expired
- `Metadata` (Dictionary<string, object>) — extensible

### Zone

- `ZoneId` (string) — e.g., "checkout-area", "loading-dock"
- `Polygon` — list of normalized points
- `SourceId` — which camera this zone belongs to

### Primitive

- `Name` (string) — e.g., "tray_empty", "queue_length"
- `Value` (object) — bool, int, float, or string
- `Timestamp` (DateTimeOffset)
- `SourceId` (string) — which camera produced it

### Event (output)

- `EventId` (string) — unique identifier
- `EventType` (string) — e.g., "loitering", "safety-violation"
- `Timestamp` (DateTimeOffset)
- `SourceId` (string)
- `ZoneId` (string, nullable)
- `TrackedObjects` — list of involved tracked objects
- `RuleId` (string) — which rule triggered this
- `Metadata` (Dictionary<string, object>) — includes `evidenceRequestId` when evidence is configured

### EvidenceRequest

- `EventId` (string) — links back to the triggering Event
- `SourceId` (string) — which camera
- `From` (DateTimeOffset) — capture start (e.g., 10s before event)
- `To` (DateTimeOffset) — capture end (e.g., 5s after event)
- `Type` (EvidenceType) — Screenshot, VideoClip, or Both

### RuleState

- `RuleId` (string) — which rule owns this state
- `TrackId` (string) — which tracked object
- `StartedAt` (DateTimeOffset) — when state tracking began
- `Data` (Dictionary<string, object>) — rule-specific state

## Service Details

### Frame Capture Service (C#)

Connects to camera streams, decodes frames, publishes frame references to Redis.

**Supported stream types:**
- RTSP (IP cameras, NVRs)
- MJPEG over HTTP (webcams, cheap IP cameras)
- USB/local cameras (via OpenCvSharp VideoCapture)

**Key behaviors:**
- One capture loop per camera, running as a background task
- Configurable FPS throttling — cameras produce 30fps but detection may only need 5-10fps. Capture service decimates frames before publishing.
- Health monitoring — detects stream disconnects, auto-reconnects with exponential backoff
- Writes decoded frames as JPEG to shared volume, publishes file path to Redis

**Camera config (stored in PostgreSQL, managed via dashboard):**

```json
{
  "id": "camera-01",
  "name": "Front Entrance",
  "streamUrl": "rtsp://192.168.1.100:554/stream",
  "type": "rtsp",
  "targetFps": 5,
  "enabled": true
}
```

**Libraries:** OpenCvSharp4, StackExchange.Redis

### Detection Bridge Service (C#)

Thin bridge between Redis frame stream and Roboflow Inference API.

**Flow:**
1. Consumes frame references from `frames:{cameraId}` Redis stream
2. Reads frame from shared volume
3. Sends frame to Roboflow Inference HTTP API with class filter
4. Publishes detections to `detections:{cameraId}` Redis stream

**Class filter — derived automatically:**
- Pipeline-core reads all primitives and rules from PostgreSQL
- Extracts the unique set of required object classes (e.g., "person", "plate", "forklift")
- Publishes the class filter to `config:class-filter` Redis key
- Detection-bridge watches this key and updates the filter sent to Roboflow Inference
- When a user adds a new primitive or rule via the dashboard referencing a new class, the filter updates automatically — no restart needed

**Detection bridge config (stored in PostgreSQL):**

```json
{
  "inferenceUrl": "http://roboflow-inference:9001",
  "modelId": "yolov8n-640",
  "confidenceThreshold": 0.5
}
```

**Libraries:** StackExchange.Redis, System.Net.Http

### Pipeline Core Service (C#)

The brain. Consumes detections, runs the full analysis pipeline, publishes events.

**Pipeline stages:**

```
Detections → Object Tracker → Zone Evaluator → Primitive Extractor → Rule Engine → Event Bus
```

#### Stage 1: Object Tracker

Takes raw detections per frame, outputs tracked objects.

- IoU (Intersection over Union) matching to associate detections across frames
- Hungarian algorithm for optimal assignment
- Track lifecycle: new → active → lost (missed N frames) → expired
- Sliding window of recent positions per track (configurable depth)
- Pure algorithmic tracker (SORT-style), no external dependencies

#### Stage 2: Zone Evaluator

Takes tracked objects + zone config, enriches with zone context.

- Point-in-polygon test (centroid of bounding box vs zone polygon)
- Zone enter/exit transitions per tracked object
- Per-track zone history: `{TrackId, ZoneId, EnteredAt, ExitedAt?}`
- Tripwire support: line segment crossing detection based on trajectory

#### Stage 3: Primitive Extractor

Takes zone-enriched tracked objects + primitive config, outputs primitive values per frame.

Primitives translate low-level tracker + zone state into semantic boolean/numeric signals that rules reference by name. Decouples rules from raw detection logic.

**Primitive types:**
- `presence` — bool, true if any object of class is in zone
- `absence` — bool, true if no object of class is in zone
- `count` — int, number of objects of a class in zone
- `zone_duration` — float, seconds an object has been in zone
- `speed` — float, object speed derived from tracker trajectory
- `line_crossed` — bool, object crossed a tripwire

All primitives configured via dashboard UI, stored in PostgreSQL.

#### Stage 4: Rule Engine

Evaluates rules against primitives and enriched tracked objects.

**Condition types:**
- `zone_enter` / `zone_exit` — object enters/leaves a zone
- `duration > Xs` — object in zone longer than X seconds
- `count > N` / `count < N` — object count thresholds in a zone
- `line_crossed` — object trajectory crosses a tripwire
- `absent > Xs` — no objects of class detected for X seconds
- `speed > V` — tracked object moving faster than threshold
- `value == X` / `value > X` / `value < X` — primitive value comparisons

**Temporal aggregation:**

Rules can require conditions to hold over a time window. Two modes:
- `sustained: Xs` — condition must be continuously true for X seconds. Resets if condition becomes false.
- `within: Xs` + optional `min_occurrences: N` — sliding window. Condition must be true at least N times (default 1) within the last X seconds.

Rules without temporal fields fire immediately on a single frame.

Implementation: per-rule evaluation history as a ring buffer of `(timestamp, bool)` pairs.

**Stateful rule memory:**

Rules with temporal conditions automatically get per-object state tracking. The engine inspects conditions at config load time and allocates state for rules that need it. No explicit flag required.

State store: `InMemoryRuleStateStore` backed by `ConcurrentDictionary<(ruleId, trackId), RuleState>`.

State lifecycle:
1. Object enters zone → engine creates RuleState
2. Each frame → engine checks elapsed time against condition
3. Condition met → fire event, remove state (respects cooldown before re-tracking)
4. Object leaves zone → remove state, timer resets
5. Track expires → state auto-cleaned

**Evidence requests:**

When a rule fires and has evidence config, the engine includes an `EvidenceRequest` in the event. Event-router handles fulfillment.

#### Stage 5: Event Publishing

Deduplicated, throttled events published to `events` Redis stream and to `IGlobalEventBus`.

- Deduplication: same rule + same track won't re-fire within cooldown
- Throttling: max N events per rule per time window

**Startup:**
1. Reads cameras, zones, primitives, and rules from PostgreSQL
2. Computes class filter, publishes to `config:class-filter` in Redis
3. Subscribes to `detections:{cameraId}` streams for all enabled cameras
4. Starts pipeline loop per camera

**Config reload:**
- Watches `config:changed` Redis channel
- Reloads affected config from PostgreSQL, recomputes class filter, rebuilds pipeline state
- No restart needed

**State:** All in-memory (tracker, zones, rule state). Lost on restart — tracker re-establishes tracks within seconds, temporal buffers rebuild quickly.

**Libraries:** StackExchange.Redis, Npgsql/Dapper, pure C# for tracker math

### Event Router Service (C#)

Consumes events from Redis, persists to PostgreSQL, dispatches notifications.

**For each event, two things happen in parallel:**
1. **Persist** — save event to PostgreSQL `events` table
2. **Notify** — dispatch via configured notification channels

**Notification channels (pluggable):**

| Channel | Implementation |
|---|---|
| Webhook | HTTP POST to configured URL with event JSON payload |
| WhatsApp | Twilio API — sends message with event summary + evidence image |
| Email | SMTP or SendGrid — event summary with evidence attachment |
| Dashboard push | WebSocket via Redis pub/sub — dashboard receives real-time events |

**Notification config (per-rule, stored in PostgreSQL, managed via dashboard):**

```json
{
  "ruleId": "safety-violation",
  "channels": [
    { "type": "webhook", "url": "https://example.com/hook" },
    { "type": "whatsapp", "phone": "+1234567890" },
    { "type": "dashboard" }
  ]
}
```

**Evidence handling:**
- When an event includes an `EvidenceRequest`, event-router calls frame-capture to retrieve the frame/clip from its rolling buffer
- Evidence files stored on shared volume, reference saved in PostgreSQL
- Evidence URL attached to notifications

**Retry logic:** Failed notifications retried with exponential backoff. Dead-letter after N failures, visible in dashboard.

**Libraries:** StackExchange.Redis, Npgsql/Dapper, System.Net.Http

### Dashboard (Next.js / TypeScript)

Configuration UI, real-time monitoring, event history, notification setup.

**Pages:**

| Page | Purpose |
|---|---|
| **Cameras** | Add/edit/remove cameras. Configure stream URL, type, target FPS. Live preview thumbnail. |
| **Zones & Primitives** | Visual zone editor — draw polygons on camera snapshot. Configure primitives per zone. |
| **Rules** | Create/edit rules. Select primitive or object, condition, temporal params, evidence config. |
| **Notifications** | Per-rule notification channel setup (webhook, WhatsApp, email). Test button. |
| **Events (live)** | Real-time event feed via WebSocket. Filters by camera, rule, severity. Evidence thumbnails inline. |
| **Events (history)** | Paginated event table with date range, camera, rule, severity filters. Click to expand details + evidence. |
| **Settings** | Detection model config (inference URL, model ID, confidence). System health overview. |

**Tech stack:**
- Next.js App Router
- TypeScript
- Tailwind CSS
- Prisma ORM (PostgreSQL)
- WebSocket for real-time events (via Redis pub/sub)
- REST API routes for all CRUD

**Zone editor:** User selects camera → dashboard shows snapshot → user draws polygons → saves as normalized coordinates to PostgreSQL → publishes `config:changed` to Redis.

**Auth:** Simple username/password login. Single-tenant.

## Multi-Camera Reasoning (Phase 2)

Extension points defined now, implementation deferred.

```csharp
interface IGlobalEventBus
{
    void Publish(Event evt);
    IAsyncEnumerable<Event> Subscribe(string? sourceFilter = null);
}

interface ICrossCameraRule
{
    string RuleId { get; }
    IReadOnlyList<string> RequiredSources { get; }
    Event? Evaluate(IReadOnlyList<Event> recentEvents);
}
```

Default `LocalEventBus` implementation in pipeline-core. Events carry `SourceId` from day one. Future `CrossCameraRuleEvaluator` subscribes to the global bus and evaluates cross-camera rules.

## Docker Compose Topology

```yaml
services:
  redis:              # Message bus + pub/sub
  postgres:           # Persistent storage
  roboflow-inference: # Prebuilt model server
  frame-capture:      # C# — camera streams
  detection-bridge:   # C# — Roboflow Inference bridge
  pipeline-core:      # C# — tracker, zones, primitives, rules
  event-router:       # C# — persist + notify
  dashboard:          # Next.js — UI

volumes:
  frames:             # Shared: capture → detection, capture → evidence
  evidence:           # Shared: evidence clips/screenshots
  pgdata:             # PostgreSQL data
```

**Startup order:** redis, postgres → roboflow-inference → frame-capture, pipeline-core → detection-bridge → event-router → dashboard

**Development:** `docker compose up` starts everything. Dashboard at `localhost:3000`.

## Project Structure

```
openeye/
├── src/
│   ├── OpenEye.FrameCapture/        # C# — frame capture service
│   ├── OpenEye.DetectionBridge/      # C# — detection bridge service
│   ├── OpenEye.PipelineCore/         # C# — pipeline core service
│   │   ├── Tracking/                 # SORT-style tracker
│   │   ├── Zones/                    # Zone evaluator, tripwire, point-in-polygon
│   │   ├── Primitives/               # Primitive extractor
│   │   ├── Rules/                    # Rule engine, temporal aggregation, rule state
│   │   └── Pipeline/                 # Pipeline orchestrator, event bus
│   ├── OpenEye.EventRouter/          # C# — event router service
│   ├── OpenEye.Shared/               # C# — shared models, Redis/Postgres helpers
│   └── OpenEye.Abstractions/         # C# — interfaces (IGlobalEventBus, IEvidenceProvider, IRuleStateStore)
├── dashboard/                        # Next.js / TypeScript
│   ├── src/
│   │   ├── app/                      # App Router pages
│   │   ├── components/               # React components
│   │   └── lib/                      # API clients, utilities
│   ├── prisma/                       # Prisma schema + migrations
│   └── package.json
├── docker/
│   ├── docker-compose.yml
│   ├── Dockerfile.dotnet             # Multi-stage build for C# services
│   └── Dockerfile.dashboard          # Next.js build
├── docs/
│   └── plans/
├── tests/
│   ├── OpenEye.Tests/                # Unit tests for all C# services
│   └── OpenEye.IntegrationTests/     # Full pipeline integration tests
└── OpenEye.sln
```

## Dependencies

**C# services:**
- OpenCvSharp4 — frame decoding (frame-capture only)
- StackExchange.Redis — Redis Streams + pub/sub
- Npgsql / Dapper — PostgreSQL access
- No other runtime dependencies. Pure C# for tracker math.

**Dashboard:**
- Next.js, React, TypeScript
- Prisma — PostgreSQL ORM
- Tailwind CSS — styling

## Testing Strategy

- Unit tests for each pipeline stage independently (tracker, zones, primitives, rules)
- Temporal aggregation tests with synthetic time sequences
- Rule state lifecycle tests
- Evidence request tests
- Integration tests with complete domain scenarios (loitering, safety violations, queue alerts, meal tracking)
- Synthetic detection trajectories as test fixtures
- Dashboard: component tests + API route tests
- Docker Compose-based end-to-end tests
