# OpenEye Framework Design

## Overview

OpenEye is a C# / .NET in-process library that converts raw camera detections (YOLO-style bounding boxes) into higher-level semantic events using configurable YAML rules. It includes built-in object tracking, spatial zone evaluation, a primitives abstraction layer, and a temporal rule engine.

**Target domains:** retail stores, restaurants, factories, warehouses.

## Architecture: Pipeline

Detections flow through a staged pipeline:

```
Raw Detections → Object Tracker → Zone Evaluator → Primitive Extractor → Rule Engine → Event Stream
```

Each stage is an independent, testable component. Events are published to a global event bus, enabling future cross-camera reasoning.

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

## Pipeline Stages

### Stage 1: Object Tracker

Takes raw `Detection[]` per frame, outputs `TrackedObject[]`.

- IoU (Intersection over Union) matching to associate detections across frames
- Hungarian algorithm for optimal assignment
- Track lifecycle: new → active → lost (missed N frames) → expired
- Sliding window of recent positions per track (configurable depth)
- Pure algorithmic tracker (SORT-style), no external dependencies

### Stage 2: Zone Evaluator

Takes `TrackedObject[]` + `Zone[]` config, enriches tracked objects with zone context.

- Point-in-polygon test (centroid of bounding box vs zone polygon)
- Zone enter/exit transitions per tracked object
- Per-track zone history: `{TrackId, ZoneId, EnteredAt, ExitedAt?}`
- Tripwire support: line segment crossing detection based on trajectory

### Stage 3: Primitive Extractor

Takes zone-enriched `TrackedObject[]` + `Primitive[]` config, outputs `Primitive[]` values per frame.

Primitives translate low-level tracker + zone state into semantic boolean/numeric signals that rules reference by name. This decouples rules from raw detection logic.

**Primitive types:**

- `presence` — bool, true if any object of class is in zone
- `absence` — bool, true if no object of class is in zone
- `count` — int, number of objects of a class in zone
- `zone_duration` — float, seconds an object has been in zone
- `speed` — float, object speed derived from tracker trajectory
- `line_crossed` — bool, object crossed a tripwire

All primitives are YAML-configured. No code changes needed per deployment site.

### Stage 4: Rule Engine

Evaluates YAML rules against primitives and enriched tracked objects.

**Condition types:**

- `zone_enter` / `zone_exit` — object enters/leaves a zone
- `duration > Xs` — object in zone longer than X seconds
- `count > N` / `count < N` — object count thresholds in a zone
- `line_crossed` — object trajectory crosses a tripwire
- `absent > Xs` — no objects of class detected for X seconds
- `speed > V` — tracked object moving faster than threshold
- `value == X` / `value > X` / `value < X` — primitive value comparisons

**Temporal aggregation:**

Rules can require conditions to hold over a time window, not just a single frame. Two temporal modes:

- `sustained: Xs` — condition must be continuously true for X seconds. Resets if condition becomes false.
- `within: Xs` + optional `min_occurrences: N` — sliding window. Condition must be true at least N times (default 1) within the last X seconds.

Rules without temporal fields fire immediately on a single frame (default behavior).

Implementation: the rule engine maintains a per-rule evaluation history — a ring buffer of `(timestamp, bool)` pairs. Each frame, it appends the current evaluation result and checks the temporal constraint against the buffer.

**Stateful rule memory:**

Rules with temporal conditions (`duration`, `sustained`, `within`) automatically get per-object state tracking. The engine inspects conditions at config load time and allocates state for rules that need it. No explicit opt-in flag required.

State store: `InMemoryRuleStateStore` backed by `ConcurrentDictionary<(ruleId, trackId), RuleState>`.

State lifecycle:
1. Object enters zone → engine creates `RuleState(ruleId, trackId, now)`
2. Each frame → engine checks elapsed time against condition
3. Condition met → fire event, remove state (respects cooldown before re-tracking)
4. Object leaves zone → remove state, timer resets
5. Track expires → state auto-cleaned

**Evidence requests:**

When a rule fires and has `evidence` config, the engine creates an `EvidenceRequest` and passes it to the host via `IEvidenceProvider`. The framework owns none of the video/frame buffer logic — it specifies what footage is needed (timestamps, camera ID) and the host fulfills it.

### Stage 5: Event Stream

Deduplicated, throttled event output published to `IGlobalEventBus` and exposed as `IAsyncEnumerable<Event>`.

- Deduplication: same rule + same track won't re-fire within cooldown
- Throttling: max N events per rule per time window
- Host app consumes the stream

## Global Event Bus

```csharp
interface IGlobalEventBus
{
    void Publish(Event evt);
    IAsyncEnumerable<Event> Subscribe(string? sourceFilter = null);
}
```

Default implementation: `LocalEventBus` — in-process, single-camera events. Events from each camera's pipeline are published here. The existing `Event` model carries `SourceId`, so events are camera-tagged from day one.

The rule engine publishes to the bus instead of directly to the output stream.

## Multi-Camera Reasoning (Phase 2)

Extension points defined now, implementation deferred.

```csharp
interface ICrossCameraRule
{
    string RuleId { get; }
    IReadOnlyList<string> RequiredSources { get; }  // camera IDs
    Event? Evaluate(IReadOnlyList<Event> recentEvents);
}
```

What this enables later:
- Rules that correlate events across cameras (e.g., tray appears on camera A, tray empty on camera B)
- A `CrossCameraRuleEvaluator` that subscribes to the global bus and evaluates cross-camera rules against a sliding window of events from all sources

What we build now:
- The `IGlobalEventBus` interface and `LocalEventBus` implementation
- Events always carry `SourceId`
- Rule engine publishes to the bus

## Evidence Capture

The host app is responsible for frame buffers and video extraction. The framework emits evidence requests.

```csharp
interface IEvidenceProvider
{
    Task CaptureAsync(EvidenceRequest request, CancellationToken ct);
}
```

Flow:
1. Rule engine fires an `Event`
2. Engine creates an `EvidenceRequest` based on rule's `evidence` config (or `evidence_defaults`)
3. Engine calls `IEvidenceProvider.CaptureAsync` — host app fulfills it
4. `Event.Metadata` gets an `"evidenceRequestId"` key linking event to evidence

## Host API

```csharp
var config = OpenEyeConfig.FromYaml("config.yaml");
var engine = new OpenEyeEngine(config);

// Optional: register evidence provider
engine.SetEvidenceProvider(myEvidenceProvider);

// Feed detections per frame
engine.ProcessFrame("camera-01", detections, timestamp);

// Consume events
await foreach (var evt in engine.Events)
{
    Console.WriteLine($"[{evt.EventType}] {evt.Description}");
}

// Or subscribe via event bus
await foreach (var evt in engine.EventBus.Subscribe("camera-01"))
{
    // camera-filtered events
}
```

## Configuration (YAML)

```yaml
cameras:
  - id: camera-01
    zones:
      - id: entrance
        polygon: [[0.1, 0.2], [0.4, 0.2], [0.4, 0.8], [0.1, 0.8]]
      - id: checkout-queue
        polygon: [[0.5, 0.3], [0.9, 0.3], [0.9, 0.9], [0.5, 0.9]]
      - id: tray-zone
        polygon: [[0.2, 0.4], [0.5, 0.4], [0.5, 0.7], [0.2, 0.7]]
    tripwires:
      - id: door-line
        points: [[0.25, 0.1], [0.25, 0.9]]
        direction: left-to-right

tracker:
  max_lost_frames: 30
  iou_threshold: 0.3
  trajectory_window: 50

primitives:
  - id: tray_empty
    type: absence
    object: plate
    zone: tray-zone

  - id: tray_full
    type: presence
    object: plate
    zone: tray-zone

  - id: queue_length
    type: count
    object: person
    zone: checkout-queue

rules:
  - id: loitering-at-entrance
    trigger:
      object: person
      zone: entrance
      condition: duration > 60s
    event:
      type: loitering
      severity: warning
      cooldown: 120s
    evidence:
      type: screenshot
      before: 10s
      after: 0s

  - id: forklift-in-pedestrian-zone
    trigger:
      object: forklift
      zone: pedestrian-area
      condition: zone_enter
    event:
      type: safety-violation
      severity: critical
    evidence:
      type: both
      before: 10s
      after: 5s

  - id: checkout-queue-sustained
    trigger:
      primitive: queue_length
      condition: value > 5
      sustained: 30s
    event:
      type: queue-alert
      severity: warning

  - id: meal-consumed
    trigger:
      primitive: tray_empty
      zone: end-zone
      condition: value == true
    event:
      type: meal-consumed
      severity: info

  - id: line-crossing-count
    trigger:
      object: person
      tripwire: door-line
      condition: line_crossed
    event:
      type: person-entered
      severity: info
      cooldown: 0s

  - id: cake-left-out
    trigger:
      object: cake
      zone: display-area
      condition: zone_duration > 60s
    event:
      type: food-safety-violation
      severity: critical
    evidence:
      type: both
      before: 10s
      after: 5s

evidence_defaults:
  type: screenshot
  before: 5s
  after: 3s

event_defaults:
  cooldown: 30s
  max_per_minute: 10
```

Key config decisions:

- Coordinates normalized 0..1 (resolution-independent)
- Primitives are YAML-configured semantic abstractions over detections + zones
- Rules can reference primitives or raw objects/zones directly
- Temporal conditions (`sustained`, `within`) are inline per-rule
- Evidence capture config is per-rule with global defaults
- Per-rule cooldown with global defaults
- Multi-camera support with per-camera zones

## Project Structure

```
OpenEye/
├── src/
│   ├── OpenEye/                      # Core library (NuGet package)
│   │   ├── Models/                   # Detection, TrackedObject, Zone, Event, Primitive, EvidenceRequest, RuleState
│   │   ├── Tracking/                 # SORT-style tracker
│   │   ├── Zones/                    # Zone evaluator, tripwire, point-in-polygon
│   │   ├── Primitives/              # Primitive extractor, primitive types
│   │   ├── Rules/                    # YAML rule parser, condition evaluator, temporal aggregation, rule state store
│   │   ├── Pipeline/                # Pipeline orchestrator, event stream, global event bus
│   │   ├── Configuration/            # YAML config loader, validation
│   │   └── OpenEyeEngine.cs          # Main entry point
│   └── OpenEye.Abstractions/         # Interfaces: IGlobalEventBus, ICrossCameraRule, IEvidenceProvider, IRuleStateStore
├── tests/
│   ├── OpenEye.Tests/                # Unit tests
│   └── OpenEye.IntegrationTests/     # Full pipeline tests
├── samples/
│   ├── RetailDemo/
│   └── WarehouseDemo/
├── docs/
│   └── plans/
└── OpenEye.sln
```

## Dependencies

- `YamlDotNet` — YAML parsing
- No other runtime dependencies

## Testing Strategy

- Unit tests for each pipeline stage independently
- Primitive extractor tests: verify correct semantic signals from tracker + zone state
- Temporal aggregation tests: verify sustained/within windows with synthetic time sequences
- Rule state tests: verify state lifecycle (create, check, fire, cleanup)
- Evidence request tests: verify correct timestamp windows emitted
- Integration tests with complete domain scenarios (loitering, safety violations, queue alerts, meal tracking)
- Synthetic detection trajectories as test fixtures
- No external dependencies to mock — pure in-process, fast tests
