# OpenEye Framework Design

## Overview

OpenEye is a C# / .NET in-process library that converts raw camera detections (YOLO-style bounding boxes) into higher-level semantic events using configurable YAML rules. It includes built-in object tracking, spatial zone evaluation, and a temporal rule engine.

**Target domains:** retail stores, restaurants, factories, warehouses.

## Architecture: Pipeline

Detections flow through a staged pipeline:

```
Raw Detections → Object Tracker → Zone Evaluator → Rule Engine → Event Stream
```

Each stage is an independent, testable component.

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

### Event (output)

- `EventType` (string) — e.g., "loitering", "safety-violation"
- `Timestamp` (DateTimeOffset)
- `SourceId` (string)
- `ZoneId` (string, nullable)
- `TrackedObjects` — list of involved tracked objects
- `RuleId` (string) — which rule triggered this
- `Metadata` (Dictionary<string, object>)

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

### Stage 3: Rule Engine

Evaluates YAML rules against enriched tracked objects.

Supported conditions:

- `zone_enter` / `zone_exit` — object enters/leaves a zone
- `duration > Xs` — object in zone longer than X seconds
- `count > N` — more than N objects of a class in a zone
- `count < N` — fewer than N objects (e.g., unstaffed station)
- `line_crossed` — object trajectory crosses a tripwire
- `absent > Xs` — no objects of class detected for X seconds
- `speed > V` — tracked object moving faster than threshold

### Stage 4: Event Stream

Deduplicated, throttled event output exposed as `IAsyncEnumerable<Event>`.

- Deduplication: same rule + same track won't re-fire within cooldown
- Throttling: max N events per rule per time window
- Host app consumes the stream

## Host API

```csharp
var config = OpenEyeConfig.FromYaml("config.yaml");
var engine = new OpenEyeEngine(config);

// Feed detections per frame
engine.ProcessFrame("camera-01", detections, timestamp);

// Consume events
await foreach (var evt in engine.Events)
{
    Console.WriteLine($"[{evt.EventType}] {evt.Description}");
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
    tripwires:
      - id: door-line
        points: [[0.25, 0.1], [0.25, 0.9]]
        direction: left-to-right

tracker:
  max_lost_frames: 30
  iou_threshold: 0.3
  trajectory_window: 50

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

  - id: forklift-in-pedestrian-zone
    trigger:
      object: forklift
      zone: pedestrian-area
      condition: zone_enter
    event:
      type: safety-violation
      severity: critical

  - id: checkout-queue-long
    trigger:
      object: person
      zone: checkout-queue
      condition: count > 5
    event:
      type: queue-alert
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

event_defaults:
  cooldown: 30s
  max_per_minute: 10
```

Key config decisions:

- Coordinates normalized 0..1 (resolution-independent)
- Rules reference zones/tripwires by ID
- Per-rule cooldown with global defaults
- Multi-camera support with per-camera zones

## Project Structure

```
OpenEye/
├── src/
│   ├── OpenEye/                      # Core library (NuGet package)
│   │   ├── Models/                   # Detection, TrackedObject, Zone, Event
│   │   ├── Tracking/                 # SORT-style tracker
│   │   ├── Zones/                    # Zone evaluator, tripwire, point-in-polygon
│   │   ├── Rules/                    # YAML rule parser, condition evaluator
│   │   ├── Pipeline/                 # Pipeline orchestrator, event stream
│   │   ├── Configuration/            # YAML config loader, validation
│   │   └── OpenEyeEngine.cs          # Main entry point
│   └── OpenEye.Abstractions/         # Interfaces only (extensibility)
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
- Integration tests with complete domain scenarios (loitering, safety violations, queue alerts)
- Synthetic detection trajectories as test fixtures
- No external dependencies to mock — pure in-process, fast tests
