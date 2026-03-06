# OpenEye Platform Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a multi-service video analytics platform that converts RTSP/MJPEG camera streams into actionable events using configurable rules with semantic primitives, bring-your-own detection models via Roboflow Inference, and a dashboard for configuration and monitoring.

**Architecture:** Microservices communicating via Redis Streams — Frame Capture → Detection Bridge → Pipeline Core → Event Router, with a Next.js dashboard for configuration and real-time monitoring.

**Tech Stack:**
- Backend: .NET 8, C#, StackExchange.Redis, Npgsql/Dapper, OpenCvSharp4
- Frontend: Next.js (App Router), TypeScript, Tailwind CSS, Prisma
- Infrastructure: Redis (Streams + pub/sub), PostgreSQL, Docker Compose

**Design doc:** `docs/plans/2026-03-06-openeye-framework-design.md`

---

## Phase 1: Foundation — Solution Structure, Shared Models & Infrastructure Helpers

### Task 1: Solution Scaffolding

**Files:**
- Create: `OpenEye.sln`
- Create: `src/OpenEye.Shared/OpenEye.Shared.csproj`
- Create: `src/OpenEye.Abstractions/OpenEye.Abstractions.csproj`
- Create: `src/OpenEye.PipelineCore/OpenEye.PipelineCore.csproj`
- Create: `src/OpenEye.FrameCapture/OpenEye.FrameCapture.csproj`
- Create: `src/OpenEye.DetectionBridge/OpenEye.DetectionBridge.csproj`
- Create: `src/OpenEye.EventRouter/OpenEye.EventRouter.csproj`
- Create: `tests/OpenEye.Tests/OpenEye.Tests.csproj`
- Create: `tests/OpenEye.IntegrationTests/OpenEye.IntegrationTests.csproj`
- Update: `.gitignore`

**Step 1: Update .gitignore for .NET**

Append to existing `.gitignore`:

```
# .NET
bin/
obj/
*.user
*.suo
.vs/
*.DotSettings.user

# Node / Dashboard
dashboard/node_modules/
dashboard/.next/
dashboard/.env.local

# IDE
.idea/
*.swp

# OS
.DS_Store
Thumbs.db
```

**Step 2: Create solution and all projects**

```bash
cd /home/user/openeye

dotnet new sln -n OpenEye

# Shared models library
dotnet new classlib -n OpenEye.Shared -o src/OpenEye.Shared -f net8.0

# Abstractions (interfaces)
dotnet new classlib -n OpenEye.Abstractions -o src/OpenEye.Abstractions -f net8.0

# Services (worker services)
dotnet new worker -n OpenEye.PipelineCore -o src/OpenEye.PipelineCore -f net8.0
dotnet new worker -n OpenEye.FrameCapture -o src/OpenEye.FrameCapture -f net8.0
dotnet new worker -n OpenEye.DetectionBridge -o src/OpenEye.DetectionBridge -f net8.0
dotnet new worker -n OpenEye.EventRouter -o src/OpenEye.EventRouter -f net8.0

# Test projects
dotnet new xunit -n OpenEye.Tests -o tests/OpenEye.Tests -f net8.0
dotnet new xunit -n OpenEye.IntegrationTests -o tests/OpenEye.IntegrationTests -f net8.0
```

**Step 3: Add all projects to solution**

```bash
dotnet sln add src/OpenEye.Shared/OpenEye.Shared.csproj
dotnet sln add src/OpenEye.Abstractions/OpenEye.Abstractions.csproj
dotnet sln add src/OpenEye.PipelineCore/OpenEye.PipelineCore.csproj
dotnet sln add src/OpenEye.FrameCapture/OpenEye.FrameCapture.csproj
dotnet sln add src/OpenEye.DetectionBridge/OpenEye.DetectionBridge.csproj
dotnet sln add src/OpenEye.EventRouter/OpenEye.EventRouter.csproj
dotnet sln add tests/OpenEye.Tests/OpenEye.Tests.csproj
dotnet sln add tests/OpenEye.IntegrationTests/OpenEye.IntegrationTests.csproj
```

**Step 4: Wire up project references**

```bash
# Abstractions depends on Shared (for model types)
dotnet add src/OpenEye.Abstractions reference src/OpenEye.Shared/OpenEye.Shared.csproj

# All services depend on Shared + Abstractions
dotnet add src/OpenEye.PipelineCore reference src/OpenEye.Shared/OpenEye.Shared.csproj
dotnet add src/OpenEye.PipelineCore reference src/OpenEye.Abstractions/OpenEye.Abstractions.csproj
dotnet add src/OpenEye.FrameCapture reference src/OpenEye.Shared/OpenEye.Shared.csproj
dotnet add src/OpenEye.FrameCapture reference src/OpenEye.Abstractions/OpenEye.Abstractions.csproj
dotnet add src/OpenEye.DetectionBridge reference src/OpenEye.Shared/OpenEye.Shared.csproj
dotnet add src/OpenEye.DetectionBridge reference src/OpenEye.Abstractions/OpenEye.Abstractions.csproj
dotnet add src/OpenEye.EventRouter reference src/OpenEye.Shared/OpenEye.Shared.csproj
dotnet add src/OpenEye.EventRouter reference src/OpenEye.Abstractions/OpenEye.Abstractions.csproj

# Test projects reference everything they need
dotnet add tests/OpenEye.Tests reference src/OpenEye.Shared/OpenEye.Shared.csproj
dotnet add tests/OpenEye.Tests reference src/OpenEye.Abstractions/OpenEye.Abstractions.csproj
dotnet add tests/OpenEye.Tests reference src/OpenEye.PipelineCore/OpenEye.PipelineCore.csproj
dotnet add tests/OpenEye.IntegrationTests reference src/OpenEye.Shared/OpenEye.Shared.csproj
dotnet add tests/OpenEye.IntegrationTests reference src/OpenEye.Abstractions/OpenEye.Abstractions.csproj
dotnet add tests/OpenEye.IntegrationTests reference src/OpenEye.PipelineCore/OpenEye.PipelineCore.csproj
```

**Step 5: Add NuGet dependencies**

```bash
# Redis for all services
dotnet add src/OpenEye.Shared package StackExchange.Redis
dotnet add src/OpenEye.Shared package Npgsql
dotnet add src/OpenEye.Shared package Dapper

# OpenCvSharp for frame capture only
dotnet add src/OpenEye.FrameCapture package OpenCvSharp4
dotnet add src/OpenEye.FrameCapture package OpenCvSharp4.runtime.linux-x64
```

**Step 6: Delete placeholder files, verify build**

Delete all auto-generated `Class1.cs`, `Worker.cs`, `UnitTest1.cs` files.

```bash
dotnet build
dotnet test
```

Expected: Build succeeds, 0 tests run.

**Step 7: Commit**

```
chore: scaffold multi-service solution with shared libraries and test projects
```

---

### Task 2: Core Data Models (OpenEye.Shared)

**Files:**
- Create: `src/OpenEye.Shared/Models/PointF.cs`
- Create: `src/OpenEye.Shared/Models/BoundingBox.cs`
- Create: `src/OpenEye.Shared/Models/Detection.cs`
- Create: `src/OpenEye.Shared/Models/TrackState.cs`
- Create: `src/OpenEye.Shared/Models/TrackedObject.cs`
- Create: `src/OpenEye.Shared/Models/TrajectoryPoint.cs`
- Create: `src/OpenEye.Shared/Models/Zone.cs`
- Create: `src/OpenEye.Shared/Models/Tripwire.cs`
- Create: `src/OpenEye.Shared/Models/TripwireDirection.cs`
- Create: `src/OpenEye.Shared/Models/Primitive.cs`
- Create: `src/OpenEye.Shared/Models/PrimitiveType.cs`
- Create: `src/OpenEye.Shared/Models/OpenEyeEvent.cs`
- Create: `src/OpenEye.Shared/Models/EvidenceRequest.cs`
- Create: `src/OpenEye.Shared/Models/EvidenceType.cs`
- Create: `src/OpenEye.Shared/Models/RuleState.cs`
- Create: `src/OpenEye.Shared/Models/CameraConfig.cs`
- Create: `src/OpenEye.Shared/Models/RuleConfig.cs`
- Create: `src/OpenEye.Shared/Models/NotificationConfig.cs`
- Test: `tests/OpenEye.Tests/Models/BoundingBoxTests.cs`
- Test: `tests/OpenEye.Tests/Models/DetectionTests.cs`

**Step 1: Write BoundingBox tests first (TDD)**

Test `Centroid`, `Area`, `IoU` (identical, non-overlapping, partial overlap, one box inside another).

**Step 2: Implement `PointF` as `readonly record struct`**

Fields: `X` (float), `Y` (float).

**Step 3: Implement `BoundingBox` as `readonly record struct`**

Fields: `X`, `Y`, `Width`, `Height` (all float, normalized 0..1).
Computed: `Centroid` → `PointF`, `Area` → float.
Static: `IoU(BoundingBox a, BoundingBox b)` → float.

**Step 4: Implement remaining models**

- `Detection` — sealed record: `ClassLabel`, `BoundingBox`, `Confidence`, `Timestamp`, `SourceId`, `FrameIndex?`
- `TrackState` — enum: `Active`, `Lost`, `Expired`
- `TrajectoryPoint` — readonly record struct: `Position` (PointF), `Timestamp`
- `TrackedObject` — sealed class (mutable state): `TrackId`, `ClassLabel`, `CurrentBox`, `Trajectory` (List<TrajectoryPoint>), `FirstSeen`, `LastSeen`, `State`, `Metadata`
- `Zone` — sealed record: `ZoneId`, `Polygon` (IReadOnlyList<PointF>), `SourceId`
- `Tripwire` — sealed record: `TripwireId`, `Start` (PointF), `End` (PointF), `SourceId`, `Direction` (TripwireDirection)
- `TripwireDirection` — enum: `Either`, `LeftToRight`, `RightToLeft`
- `PrimitiveType` — enum: `Presence`, `Absence`, `Count`, `ZoneDuration`, `Speed`, `LineCrossed`
- `Primitive` — sealed record: `Name`, `Type` (PrimitiveType), `Value` (object), `Timestamp`, `SourceId`
- `OpenEyeEvent` — sealed record: `EventId`, `EventType`, `Timestamp`, `SourceId`, `ZoneId?`, `TrackedObjects`, `RuleId`, `Metadata`, `EvidenceRequest?`
- `EvidenceType` — enum: `Screenshot`, `VideoClip`, `Both`
- `EvidenceRequest` — sealed record: `EventId`, `SourceId`, `From`, `To`, `Type` (EvidenceType)
- `RuleState` — sealed class: `RuleId`, `TrackId`, `StartedAt`, `Data`
- `CameraConfig` — sealed record: `Id`, `Name`, `StreamUrl`, `Type` (string: "rtsp"/"mjpeg"/"usb"), `TargetFps`, `Enabled`
- `RuleConfig` — sealed record: `RuleId`, `Condition`, `ClassLabel`, `ZoneId?`, `TripwireId?`, `Sustained?`, `Within?`, `MinOccurrences?`, `Cooldown`, `EvidenceConfig?`
- `NotificationConfig` — sealed record: `RuleId`, `Channels` (list of channel configs)

**Step 5: Run tests, verify green**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~Models" -v n
```

**Step 6: Commit**

```
feat: add core data models for all services
```

---

### Task 3: Abstractions / Interfaces (OpenEye.Abstractions)

**Files:**
- Create: `src/OpenEye.Abstractions/IObjectTracker.cs`
- Create: `src/OpenEye.Abstractions/IZoneEvaluator.cs`
- Create: `src/OpenEye.Abstractions/IPrimitiveExtractor.cs`
- Create: `src/OpenEye.Abstractions/IRuleEngine.cs`
- Create: `src/OpenEye.Abstractions/IRuleStateStore.cs`
- Create: `src/OpenEye.Abstractions/IGlobalEventBus.cs`
- Create: `src/OpenEye.Abstractions/IEventPublisher.cs`
- Create: `src/OpenEye.Abstractions/IConfigProvider.cs`
- Create: `src/OpenEye.Abstractions/IFramePublisher.cs`
- Create: `src/OpenEye.Abstractions/IDetectionClient.cs`
- Create: `src/OpenEye.Abstractions/INotificationDispatcher.cs`
- Create: `src/OpenEye.Abstractions/IEvidenceProvider.cs`

**Step 1: Define pipeline stage interfaces**

```csharp
public interface IObjectTracker
{
    IReadOnlyList<TrackedObject> Update(IReadOnlyList<Detection> detections, DateTimeOffset timestamp);
    IReadOnlyList<TrackedObject> ActiveTracks { get; }
    void Reset();
}

public interface IZoneEvaluator
{
    void Evaluate(IReadOnlyList<TrackedObject> tracks, IReadOnlyList<Zone> zones, IReadOnlyList<Tripwire> tripwires);
    bool IsInZone(string trackId, string zoneId);
    IReadOnlyList<(string TrackId, string ZoneId, DateTimeOffset EnteredAt, DateTimeOffset? ExitedAt)> GetZoneHistory();
    IReadOnlyList<(string TrackId, string TripwireId, DateTimeOffset CrossedAt)> GetTripwireCrossings();
}

public interface IPrimitiveExtractor
{
    IReadOnlyList<Primitive> Extract(
        IReadOnlyList<TrackedObject> tracks,
        IZoneEvaluator zoneEvaluator,
        IReadOnlyList<PrimitiveConfig> configs,
        DateTimeOffset timestamp);
}

public interface IRuleEngine
{
    IReadOnlyList<OpenEyeEvent> Evaluate(
        IReadOnlyList<Primitive> primitives,
        IReadOnlyList<TrackedObject> tracks,
        IZoneEvaluator zoneEvaluator,
        IReadOnlyList<RuleConfig> rules,
        DateTimeOffset timestamp);
}
```

**Step 2: Define infrastructure interfaces**

```csharp
public interface IRuleStateStore
{
    RuleState? Get(string ruleId, string trackId);
    void Set(string ruleId, string trackId, RuleState state);
    void Remove(string ruleId, string trackId);
    void RemoveByTrack(string trackId);
}

public interface IGlobalEventBus
{
    void Publish(OpenEyeEvent evt);
    IAsyncEnumerable<OpenEyeEvent> Subscribe(string? sourceFilter = null);
}

public interface IEventPublisher
{
    Task PublishAsync(OpenEyeEvent evt, CancellationToken ct = default);
}

public interface IConfigProvider
{
    Task<IReadOnlyList<CameraConfig>> GetCamerasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Zone>> GetZonesAsync(string? sourceId = null, CancellationToken ct = default);
    Task<IReadOnlyList<Tripwire>> GetTripwiresAsync(string? sourceId = null, CancellationToken ct = default);
    Task<IReadOnlyList<PrimitiveConfig>> GetPrimitivesAsync(string? sourceId = null, CancellationToken ct = default);
    Task<IReadOnlyList<RuleConfig>> GetRulesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NotificationConfig>> GetNotificationsAsync(string? ruleId = null, CancellationToken ct = default);
}

public interface IFramePublisher
{
    Task PublishFrameAsync(string cameraId, long frameIndex, string framePath, DateTimeOffset timestamp, CancellationToken ct = default);
}

public interface IDetectionClient
{
    Task<IReadOnlyList<Detection>> DetectAsync(string framePath, IReadOnlySet<string> classFilter, CancellationToken ct = default);
}

public interface INotificationDispatcher
{
    Task DispatchAsync(OpenEyeEvent evt, NotificationConfig config, CancellationToken ct = default);
}

public interface IEvidenceProvider
{
    Task<string?> CaptureEvidenceAsync(EvidenceRequest request, CancellationToken ct = default);
}
```

**Step 3: Commit**

```
feat: define abstractions and interfaces for all services and pipeline stages
```

---

### Task 4: Redis & PostgreSQL Helpers (OpenEye.Shared)

**Files:**
- Create: `src/OpenEye.Shared/Redis/RedisStreamPublisher.cs`
- Create: `src/OpenEye.Shared/Redis/RedisStreamConsumer.cs`
- Create: `src/OpenEye.Shared/Redis/RedisConfigNotifier.cs`
- Create: `src/OpenEye.Shared/Postgres/PostgresConfigProvider.cs`
- Create: `src/OpenEye.Shared/Postgres/PostgresEventStore.cs`

**Step 1: Implement `RedisStreamPublisher`**

Generic helper that publishes JSON-serialized messages to a Redis stream.

```csharp
public class RedisStreamPublisher
{
    Task PublishAsync<T>(string streamKey, T message, CancellationToken ct = default);
}
```

Uses `StackExchange.Redis` `IDatabase.StreamAddAsync`.

**Step 2: Implement `RedisStreamConsumer`**

Generic helper that reads from a Redis stream using consumer groups.

```csharp
public class RedisStreamConsumer
{
    // Creates consumer group if it doesn't exist
    Task EnsureGroupAsync(string streamKey, string groupName);

    // Reads next batch of messages, deserializes from JSON
    IAsyncEnumerable<(string Id, T Message)> ConsumeAsync<T>(
        string streamKey, string groupName, string consumerName,
        int batchSize = 10, CancellationToken ct = default);

    // Acknowledges processed messages
    Task AckAsync(string streamKey, string groupName, string messageId);
}
```

**Step 3: Implement `RedisConfigNotifier`**

Publishes and subscribes to the `config:changed` Redis channel.

```csharp
public class RedisConfigNotifier
{
    Task PublishChangeAsync(string configSection, CancellationToken ct = default);
    IAsyncEnumerable<string> SubscribeAsync(CancellationToken ct = default);
}
```

Also manages the `config:class-filter` Redis key (get/set the active class filter set).

**Step 4: Implement `PostgresConfigProvider` (implements `IConfigProvider`)**

Uses Dapper to query cameras, zones, tripwires, primitives, rules, and notification configs from PostgreSQL.

**Step 5: Implement `PostgresEventStore`**

```csharp
public class PostgresEventStore
{
    Task SaveEventAsync(OpenEyeEvent evt, CancellationToken ct = default);
    Task<IReadOnlyList<OpenEyeEvent>> QueryEventsAsync(EventQuery query, CancellationToken ct = default);
}
```

**Step 6: Commit**

```
feat: add Redis stream helpers and PostgreSQL data access layer
```

---

## Phase 2: Pipeline Core — The Brain

### Task 5: Object Tracker (SORT-style)

**Files:**
- Create: `src/OpenEye.PipelineCore/Tracking/HungarianAlgorithm.cs`
- Create: `src/OpenEye.PipelineCore/Tracking/SortTracker.cs`
- Create: `src/OpenEye.PipelineCore/Tracking/TrackerConfig.cs`
- Test: `tests/OpenEye.Tests/Tracking/HungarianAlgorithmTests.cs`
- Test: `tests/OpenEye.Tests/Tracking/SortTrackerTests.cs`

**Step 1: Write Hungarian Algorithm tests (TDD)**

Test cases:
- 1×1, 2×2, 3×3 cost matrices with known optimal assignments
- Non-square matrices (more detections than tracks and vice versa)
- Edge case: empty matrix

**Step 2: Implement `HungarianAlgorithm`**

Pure C# implementation of the Hungarian (Munkres) algorithm.

```csharp
public static class HungarianAlgorithm
{
    /// Returns optimal column assignment for each row. -1 = unassigned.
    public static int[] Solve(float[,] costMatrix);
}
```

**Step 3: Run tests, verify green**

**Step 4: Write SortTracker tests (TDD)**

Test cases:
- Single detection across multiple frames → stable track ID
- Two objects that don't overlap → two separate tracks
- Object disappears → track transitions to Lost then Expired
- Object reappears within lost window → same track ID maintained
- Track trajectory accumulates positions
- Multiple detections with IoU matching
- `Reset()` clears all state

**Step 5: Implement `SortTracker` (implements `IObjectTracker`)**

```csharp
public class TrackerConfig
{
    public int MaxLostFrames { get; set; } = 30;
    public float IouThreshold { get; set; } = 0.3f;
    public int TrajectoryDepth { get; set; } = 50;
}
```

`SortTracker` logic:
1. Compute IoU cost matrix between active tracks and new detections
2. Run Hungarian algorithm for optimal assignment
3. Update matched tracks (new box, append trajectory point, update LastSeen)
4. Create new tracks for unmatched detections
5. Increment lost counter for unmatched tracks; transition Lost → Expired
6. Return all active + newly created tracks

**Step 6: Run tests, verify green**

**Step 7: Commit**

```
feat: implement SORT-style object tracker with Hungarian algorithm
```

---

### Task 6: Zone Evaluator

**Files:**
- Create: `src/OpenEye.PipelineCore/Zones/PointInPolygon.cs`
- Create: `src/OpenEye.PipelineCore/Zones/LineIntersection.cs`
- Create: `src/OpenEye.PipelineCore/Zones/DefaultZoneEvaluator.cs`
- Test: `tests/OpenEye.Tests/Zones/PointInPolygonTests.cs`
- Test: `tests/OpenEye.Tests/Zones/LineIntersectionTests.cs`
- Test: `tests/OpenEye.Tests/Zones/ZoneEvaluatorTests.cs`

**Step 1: Write point-in-polygon tests (TDD)**

Test cases:
- Point inside a square zone → true
- Point outside → false
- Point on edge → true (boundary convention)
- Complex polygon (L-shape, triangle)
- Degenerate: empty polygon → false

**Step 2: Implement `PointInPolygon` using ray casting algorithm**

```csharp
public static class PointInPolygon
{
    public static bool Contains(IReadOnlyList<PointF> polygon, PointF point);
}
```

**Step 3: Write line intersection tests (TDD) for tripwire crossing**

Test cases:
- Two segments that cross → true, with intersection point
- Parallel segments → false
- Collinear overlapping → false (no crossing)
- T-intersection
- Direction detection (left-to-right vs right-to-left relative to tripwire)

**Step 4: Implement `LineIntersection`**

```csharp
public static class LineIntersection
{
    public static bool Intersects(PointF a1, PointF a2, PointF b1, PointF b2);
    public static CrossDirection GetCrossDirection(PointF a1, PointF a2, PointF tripStart, PointF tripEnd);
}
```

**Step 5: Write ZoneEvaluator tests (TDD)**

Test cases:
- Object enters zone → zone history records entry
- Object leaves zone → ExitedAt is set
- Object moves between zones → tracked correctly
- Tripwire crossing detected from trajectory
- Tripwire direction filtering works (LeftToRight only, etc.)
- No spurious enter/exit when object stays in zone across frames

**Step 6: Implement `DefaultZoneEvaluator` (implements `IZoneEvaluator`)**

Internal state:
- `Dictionary<(trackId, zoneId), ZoneOccupancy>` — current zone membership
- `List<ZoneHistoryEntry>` — historical enter/exit records
- `List<TripwireCrossing>` — detected crossings

Each `Evaluate()` call:
1. For each track, test centroid against all zone polygons
2. Compare with previous frame to detect enter/exit transitions
3. For tracks with 2+ trajectory points, test last segment against all tripwires
4. Record crossings with direction check

**Step 7: Run all zone tests, verify green**

**Step 8: Commit**

```
feat: implement zone evaluator with point-in-polygon and tripwire crossing
```

---

### Task 7: Primitive Extractor

**Files:**
- Create: `src/OpenEye.PipelineCore/Primitives/DefaultPrimitiveExtractor.cs`
- Create: `src/OpenEye.Shared/Models/PrimitiveConfig.cs`
- Test: `tests/OpenEye.Tests/Primitives/PrimitiveExtractorTests.cs`

**Step 1: Define `PrimitiveConfig` model**

```csharp
public sealed record PrimitiveConfig(
    string Name,
    PrimitiveType Type,
    string ClassLabel,
    string? ZoneId,
    string? TripwireId,
    string SourceId);
```

**Step 2: Write Primitive Extractor tests (TDD)**

Test cases for each primitive type:
- `presence`: person in zone → true; no person → false
- `absence`: no person in zone → true; person present → false
- `count`: 3 people in zone → value is 3
- `zone_duration`: person in zone for 5.2s → value is 5.2
- `speed`: person with trajectory → speed computed from displacement over time
- `line_crossed`: person crossed tripwire → true

**Step 3: Implement `DefaultPrimitiveExtractor` (implements `IPrimitiveExtractor`)**

For each `PrimitiveConfig`:
1. Filter tracked objects by `ClassLabel`
2. Filter by zone (if `ZoneId` set) using `IZoneEvaluator.IsInZone()`
3. Compute value based on `PrimitiveType`:
   - `Presence` → any matching object in zone
   - `Absence` → no matching objects in zone
   - `Count` → count of matching objects in zone
   - `ZoneDuration` → seconds since earliest zone entry for matching objects (from zone history)
   - `Speed` → compute from trajectory: displacement / time between first and last trajectory points
   - `LineCrossed` → check tripwire crossings for matching objects

**Step 4: Run tests, verify green**

**Step 5: Commit**

```
feat: implement primitive extractor for semantic signal computation
```

---

### Task 8: Rule Engine with Temporal Aggregation

**Files:**
- Create: `src/OpenEye.PipelineCore/Rules/DefaultRuleEngine.cs`
- Create: `src/OpenEye.PipelineCore/Rules/InMemoryRuleStateStore.cs`
- Create: `src/OpenEye.PipelineCore/Rules/TemporalBuffer.cs`
- Create: `src/OpenEye.PipelineCore/Rules/ConditionEvaluator.cs`
- Create: `src/OpenEye.PipelineCore/Rules/EventDeduplicator.cs`
- Test: `tests/OpenEye.Tests/Rules/ConditionEvaluatorTests.cs`
- Test: `tests/OpenEye.Tests/Rules/TemporalBufferTests.cs`
- Test: `tests/OpenEye.Tests/Rules/RuleEngineTests.cs`
- Test: `tests/OpenEye.Tests/Rules/EventDeduplicatorTests.cs`

**Step 1: Write ConditionEvaluator tests (TDD)**

Test cases:
- `zone_enter` → true when track just entered zone
- `zone_exit` → true when track just exited zone
- `duration > 10s` → true when zone duration exceeds threshold
- `count > 5` → true when primitive count exceeds threshold
- `count < 2` → true when primitive count below threshold
- `line_crossed` → true when tripwire crossed
- `absent > 30s` → true when absence primitive has been true for 30+ seconds
- `speed > 2.0` → true when speed exceeds threshold
- `value == X`, `value > X`, `value < X` → primitive value comparisons

**Step 2: Implement `ConditionEvaluator`**

Evaluates a single rule condition against current primitives, tracks, and zone state. Returns bool.

**Step 3: Write TemporalBuffer tests (TDD)**

Test cases:
- `sustained: 5s` — condition true for 5 consecutive seconds → fires; condition drops briefly → resets
- `within: 10s, min_occurrences: 3` — condition true 3 times in 10s window → fires
- Sliding window correctly drops old entries
- Ring buffer wraps correctly
- No temporal config → fires immediately when condition is true

**Step 4: Implement `TemporalBuffer`**

Ring buffer of `(DateTimeOffset timestamp, bool conditionResult)` pairs per rule.

```csharp
public class TemporalBuffer
{
    void Record(DateTimeOffset timestamp, bool result);
    bool CheckSustained(TimeSpan duration, DateTimeOffset now);
    bool CheckWithin(TimeSpan window, int minOccurrences, DateTimeOffset now);
}
```

**Step 5: Implement `InMemoryRuleStateStore` (implements `IRuleStateStore`)**

Backed by `ConcurrentDictionary<(string ruleId, string trackId), RuleState>`.

**Step 6: Write RuleEngine tests (TDD)**

Test cases:
- Simple rule (no temporal) fires on single matching frame
- Rule with `sustained: 5s` fires only after 5s of continuous condition
- Rule with `within: 10s, min_occurrences: 3` fires after 3 occurrences
- Cooldown prevents re-firing within cooldown window
- Rule state auto-cleans when track expires
- Evidence request included when rule has evidence config
- Multiple rules evaluated independently

**Step 7: Implement `DefaultRuleEngine` (implements `IRuleEngine`)**

For each rule:
1. Evaluate condition via `ConditionEvaluator`
2. Record result in `TemporalBuffer`
3. Check temporal aggregation (sustained / within / immediate)
4. If triggered, check deduplication/cooldown via `EventDeduplicator`
5. If not suppressed, create `OpenEyeEvent` (with optional `EvidenceRequest`)
6. Return collected events

**Step 8: Write EventDeduplicator tests**

- Same rule + same track within cooldown → suppressed
- Same rule + same track after cooldown → allowed
- Different rule or different track → allowed
- Max events per rule per time window throttling

**Step 9: Implement `EventDeduplicator`**

```csharp
public class EventDeduplicator
{
    bool ShouldSuppress(string ruleId, string trackId, DateTimeOffset now);
    void RecordFired(string ruleId, string trackId, DateTimeOffset now);
}
```

**Step 10: Run all rule engine tests, verify green**

**Step 11: Commit**

```
feat: implement rule engine with temporal aggregation, deduplication, and state management
```

---

### Task 9: Pipeline Orchestrator

**Files:**
- Create: `src/OpenEye.PipelineCore/Pipeline/PipelineOrchestrator.cs`
- Create: `src/OpenEye.PipelineCore/Pipeline/LocalEventBus.cs`
- Create: `src/OpenEye.PipelineCore/Pipeline/PipelineCoreWorker.cs`
- Test: `tests/OpenEye.Tests/Pipeline/PipelineOrchestratorTests.cs`
- Test: `tests/OpenEye.Tests/Pipeline/LocalEventBusTests.cs`

**Step 1: Implement `LocalEventBus` (implements `IGlobalEventBus`)**

In-memory event bus using `Channel<OpenEyeEvent>`. Supports multiple subscribers with optional source filter.

**Step 2: Write PipelineOrchestrator tests (TDD)**

Test cases:
- Feed detections → tracker produces tracks → zones evaluated → primitives extracted → rules evaluated → events emitted
- Full pipeline scenario: person enters zone, stays 10s, rule fires
- Config reload updates zones/rules without losing tracker state
- Class filter computation from active primitives and rules
- Events published to event bus

**Step 3: Implement `PipelineOrchestrator`**

Wires together all pipeline stages for a single camera:

```csharp
public class PipelineOrchestrator
{
    // Dependencies injected: IObjectTracker, IZoneEvaluator, IPrimitiveExtractor, IRuleEngine, IEventPublisher

    Task ProcessFrameAsync(string cameraId, IReadOnlyList<Detection> detections, DateTimeOffset timestamp);
    void ReloadConfig(IReadOnlyList<Zone> zones, IReadOnlyList<Tripwire> tripwires,
                      IReadOnlyList<PrimitiveConfig> primitives, IReadOnlyList<RuleConfig> rules);
    IReadOnlySet<string> GetRequiredClasses();
}
```

**Step 4: Implement `PipelineCoreWorker` (BackgroundService)**

The service entry point:
1. On startup: load config from PostgreSQL via `IConfigProvider`
2. Compute class filter → publish to `config:class-filter` Redis key
3. Subscribe to `detections:{cameraId}` Redis streams for all enabled cameras
4. For each detection message: deserialize, feed to appropriate `PipelineOrchestrator`
5. Published events → `IEventPublisher` → Redis `events` stream
6. Subscribe to `config:changed` Redis channel → reload affected config

**Step 5: Run tests, verify green**

**Step 6: Commit**

```
feat: implement pipeline orchestrator and core worker service
```

---

## Phase 3: Peripheral Services

### Task 10: Frame Capture Service

**Files:**
- Create: `src/OpenEye.FrameCapture/CaptureLoop.cs`
- Create: `src/OpenEye.FrameCapture/FrameCaptureWorker.cs`
- Create: `src/OpenEye.FrameCapture/FrameWriter.cs`
- Create: `src/OpenEye.FrameCapture/StreamReconnector.cs`
- Test: `tests/OpenEye.Tests/FrameCapture/CaptureLoopTests.cs`

**Step 1: Implement `FrameWriter`**

Writes decoded frames as JPEG to the shared volume. File naming: `{cameraId}/{frameIndex}.jpg`.

**Step 2: Implement `CaptureLoop`**

One loop per camera, running as a background task:
1. Open stream via OpenCvSharp `VideoCapture` (supports RTSP, MJPEG, USB)
2. FPS throttling: skip frames to match `CameraConfig.TargetFps`
3. Decode frame → write JPEG via `FrameWriter`
4. Publish frame reference to `frames:{cameraId}` Redis stream via `IFramePublisher`

**Step 3: Implement `StreamReconnector`**

Health monitoring wrapper:
- Detects stream disconnect (read failure)
- Auto-reconnects with exponential backoff (1s, 2s, 4s, 8s, max 30s)
- Logs reconnection attempts

**Step 4: Implement `FrameCaptureWorker` (BackgroundService)**

1. Load camera configs from PostgreSQL
2. Start a `CaptureLoop` for each enabled camera
3. Watch `config:changed` → add/remove/update capture loops dynamically

**Step 5: Write unit tests for CaptureLoop**

Test FPS throttling logic, frame index sequencing, and reconnection behavior (using mocked VideoCapture).

**Step 6: Commit**

```
feat: implement frame capture service with RTSP/MJPEG/USB support
```

---

### Task 11: Detection Bridge Service

**Files:**
- Create: `src/OpenEye.DetectionBridge/RoboflowInferenceClient.cs`
- Create: `src/OpenEye.DetectionBridge/DetectionBridgeWorker.cs`
- Test: `tests/OpenEye.Tests/DetectionBridge/RoboflowInferenceClientTests.cs`

**Step 1: Implement `RoboflowInferenceClient` (implements `IDetectionClient`)**

HTTP client that:
1. Reads frame from shared volume path
2. Sends to Roboflow Inference API: `POST http://{inferenceUrl}/infer`
3. Applies class filter (only request detections for needed classes)
4. Parses response into `List<Detection>`
5. Handles errors gracefully (timeout, 5xx → retry with backoff)

**Step 2: Implement `DetectionBridgeWorker` (BackgroundService)**

1. Load detection config from PostgreSQL (inference URL, model ID, confidence threshold)
2. Read class filter from `config:class-filter` Redis key
3. Subscribe to `config:changed` → refresh class filter when it changes
4. Consume from `frames:{cameraId}` Redis streams
5. For each frame: call `RoboflowInferenceClient.DetectAsync()`
6. Publish detections to `detections:{cameraId}` Redis stream

**Step 3: Write tests with mocked HTTP responses**

Test correct request formation, response parsing, class filtering, and error handling.

**Step 4: Commit**

```
feat: implement detection bridge service for Roboflow Inference
```

---

### Task 12: Event Router Service

**Files:**
- Create: `src/OpenEye.EventRouter/EventRouterWorker.cs`
- Create: `src/OpenEye.EventRouter/Notifications/WebhookNotifier.cs`
- Create: `src/OpenEye.EventRouter/Notifications/WhatsAppNotifier.cs`
- Create: `src/OpenEye.EventRouter/Notifications/EmailNotifier.cs`
- Create: `src/OpenEye.EventRouter/Notifications/DashboardPushNotifier.cs`
- Create: `src/OpenEye.EventRouter/Notifications/NotificationDispatcher.cs`
- Create: `src/OpenEye.EventRouter/Evidence/EvidenceHandler.cs`
- Test: `tests/OpenEye.Tests/EventRouter/NotificationDispatcherTests.cs`
- Test: `tests/OpenEye.Tests/EventRouter/EventRouterWorkerTests.cs`

**Step 1: Implement notification channel classes**

Each implements a common `INotificationChannel` interface:
- `WebhookNotifier` — HTTP POST with event JSON payload
- `WhatsAppNotifier` — Twilio API call with event summary + evidence URL
- `EmailNotifier` — SMTP/SendGrid with event summary + evidence attachment
- `DashboardPushNotifier` — Redis pub/sub to `dashboard:events` channel

**Step 2: Implement `NotificationDispatcher` (implements `INotificationDispatcher`)**

Routes events to the correct notification channels based on `NotificationConfig`.
Retry logic: exponential backoff (1s, 2s, 4s), dead-letter after 3 failures.

**Step 3: Implement `EvidenceHandler`**

When event has `EvidenceRequest`:
1. Retrieve frames from shared volume based on time range
2. Store evidence files to evidence volume
3. Return evidence URL/path for attachment to notifications

**Step 4: Implement `EventRouterWorker` (BackgroundService)**

1. Consume from `events` Redis stream
2. For each event, in parallel:
   a. Persist to PostgreSQL via `PostgresEventStore`
   b. If `EvidenceRequest` present → `EvidenceHandler.CaptureEvidenceAsync()`
   c. Load notification config for the rule → `NotificationDispatcher.DispatchAsync()`
3. Acknowledge message after both persist + notify complete

**Step 5: Write tests**

- Dispatcher routes to correct channels
- Retry logic works on transient failures
- Dead-letter after max retries
- Evidence handler constructs correct paths
- Worker processes events end-to-end (with mocked dependencies)

**Step 6: Commit**

```
feat: implement event router with notification dispatch and evidence handling
```

---

## Phase 4: Dashboard (Next.js)

### Task 13: Dashboard Scaffolding & Database Schema

**Files:**
- Create: `dashboard/package.json`
- Create: `dashboard/tsconfig.json`
- Create: `dashboard/tailwind.config.ts`
- Create: `dashboard/prisma/schema.prisma`
- Create: `dashboard/src/app/layout.tsx`
- Create: `dashboard/src/app/page.tsx`
- Create: `dashboard/src/lib/prisma.ts`

**Step 1: Initialize Next.js project**

```bash
cd /home/user/openeye
npx create-next-app@latest dashboard --typescript --tailwind --app --src-dir --no-import-alias
```

**Step 2: Install dependencies**

```bash
cd dashboard
npm install prisma @prisma/client
npx prisma init
```

**Step 3: Define Prisma schema**

Models matching the design:
- `Camera` — id, name, streamUrl, type, targetFps, enabled, createdAt, updatedAt
- `Zone` — id, polygon (JSON), sourceId (→ Camera), createdAt
- `Tripwire` — id, startX, startY, endX, endY, direction, sourceId (→ Camera)
- `PrimitiveConfig` — id, name, type, classLabel, zoneId (→ Zone?), tripwireId (→ Tripwire?), sourceId
- `Rule` — id, ruleId, condition, classLabel, zoneId?, tripwireId?, sustained?, within?, minOccurrences?, cooldown, evidenceType?, createdAt
- `NotificationChannel` — id, ruleId (→ Rule), type, config (JSON)
- `Event` — id, eventId, eventType, timestamp, sourceId, zoneId?, ruleId, trackedObjects (JSON), metadata (JSON), evidencePath?
- `User` — id, username, passwordHash

**Step 4: Run initial migration**

```bash
npx prisma migrate dev --name init
```

**Step 5: Create Prisma client singleton** (`src/lib/prisma.ts`)

**Step 6: Create root layout with Tailwind** and placeholder home page.

**Step 7: Commit**

```
feat: scaffold Next.js dashboard with Prisma schema and database migration
```

---

### Task 14: Dashboard API Routes (CRUD)

**Files:**
- Create: `dashboard/src/app/api/cameras/route.ts` — GET (list), POST (create)
- Create: `dashboard/src/app/api/cameras/[id]/route.ts` — GET, PUT, DELETE
- Create: `dashboard/src/app/api/zones/route.ts`
- Create: `dashboard/src/app/api/zones/[id]/route.ts`
- Create: `dashboard/src/app/api/tripwires/route.ts`
- Create: `dashboard/src/app/api/tripwires/[id]/route.ts`
- Create: `dashboard/src/app/api/primitives/route.ts`
- Create: `dashboard/src/app/api/primitives/[id]/route.ts`
- Create: `dashboard/src/app/api/rules/route.ts`
- Create: `dashboard/src/app/api/rules/[id]/route.ts`
- Create: `dashboard/src/app/api/notifications/route.ts`
- Create: `dashboard/src/app/api/notifications/[id]/route.ts`
- Create: `dashboard/src/app/api/events/route.ts` — GET with pagination, filters
- Create: `dashboard/src/app/api/auth/login/route.ts`
- Create: `dashboard/src/lib/redis.ts` — Redis client for config change notifications
- Test: API route tests

**Step 1: Create Redis client helper**

Connects to Redis, exposes `publishConfigChange(section: string)` that publishes to `config:changed`.

**Step 2: Implement CRUD API routes**

Each resource follows the same pattern:
- `GET /api/{resource}` — list all (with optional sourceId filter)
- `POST /api/{resource}` — create, then publish `config:changed`
- `GET /api/{resource}/[id]` — get by ID
- `PUT /api/{resource}/[id]` — update, then publish `config:changed`
- `DELETE /api/{resource}/[id]` — delete, then publish `config:changed`

Events API is read-only:
- `GET /api/events?camera=X&rule=Y&from=T1&to=T2&page=1&limit=20` — paginated, filtered

Auth:
- `POST /api/auth/login` — username/password → JWT token
- Simple middleware for protected routes

**Step 3: Commit**

```
feat: implement dashboard CRUD API routes with config change notifications
```

---

### Task 15: Dashboard UI Pages

**Files:**
- Create: `dashboard/src/app/cameras/page.tsx`
- Create: `dashboard/src/app/zones/page.tsx`
- Create: `dashboard/src/app/rules/page.tsx`
- Create: `dashboard/src/app/notifications/page.tsx`
- Create: `dashboard/src/app/events/page.tsx`
- Create: `dashboard/src/app/events/history/page.tsx`
- Create: `dashboard/src/app/settings/page.tsx`
- Create: `dashboard/src/app/login/page.tsx`
- Create: `dashboard/src/components/Sidebar.tsx`
- Create: `dashboard/src/components/CameraCard.tsx`
- Create: `dashboard/src/components/ZoneEditor.tsx`
- Create: `dashboard/src/components/RuleForm.tsx`
- Create: `dashboard/src/components/EventFeed.tsx`
- Create: `dashboard/src/components/EventTable.tsx`

**Step 1: Create layout with sidebar navigation**

Sidebar with links: Cameras, Zones & Primitives, Rules, Notifications, Events (Live), Events (History), Settings.

**Step 2: Cameras page**

- Table of configured cameras
- Add/edit modal: name, stream URL, type dropdown, target FPS slider, enabled toggle
- Live preview thumbnail (fetched from frame capture via API)

**Step 3: Zones & Primitives page**

- Camera selector dropdown
- Canvas showing camera snapshot
- Zone editor: draw polygons by clicking points, save zone
- For each zone: list of primitives with add/edit/delete
- Primitive form: name, type dropdown, class label, optional zone/tripwire link

**Step 4: Rules page**

- Table of configured rules
- Rule form: select primitive or condition type, set parameters, temporal config (sustained/within), cooldown, evidence toggle

**Step 5: Notifications page**

- Per-rule notification channel list
- Add channel: type dropdown (webhook, WhatsApp, email, dashboard), config fields
- Test button: sends a test notification

**Step 6: Events (Live) page**

- Real-time event feed via WebSocket (connected to Redis pub/sub via server)
- Filters: camera, rule, severity
- Each event card shows: type, camera, zone, timestamp, evidence thumbnail if available

**Step 7: Events (History) page**

- Paginated table with date range picker, camera filter, rule filter
- Click row to expand: full event details + evidence viewer

**Step 8: Settings page**

- Detection model config: inference URL, model ID, confidence threshold
- System health overview: service status, Redis connectivity, PostgreSQL connectivity

**Step 9: Login page**

Simple username/password form → JWT → stored in cookie.

**Step 10: Commit**

```
feat: implement dashboard UI pages for cameras, zones, rules, events, and settings
```

---

## Phase 5: Infrastructure & Integration

### Task 16: Docker Compose Setup

**Files:**
- Create: `docker/docker-compose.yml`
- Create: `docker/Dockerfile.dotnet`
- Create: `docker/Dockerfile.dashboard`
- Create: `docker/.env.example`

**Step 1: Create `Dockerfile.dotnet` (multi-stage)**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# Restore, build, publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# Copy published output, set entrypoint
# ARG SERVICE_NAME to select which service to run
```

Single Dockerfile for all C# services, parameterized by `SERVICE_NAME` build arg.

**Step 2: Create `Dockerfile.dashboard`**

Standard Next.js multi-stage build.

**Step 3: Create `docker-compose.yml`**

Services:
- `redis` — `redis:7-alpine`, port 6379
- `postgres` — `postgres:16-alpine`, port 5432, with init SQL for schema
- `roboflow-inference` — `roboflow/inference-server:latest`, port 9001
- `frame-capture` — builds from Dockerfile.dotnet with `SERVICE_NAME=OpenEye.FrameCapture`
- `detection-bridge` — builds from Dockerfile.dotnet with `SERVICE_NAME=OpenEye.DetectionBridge`
- `pipeline-core` — builds from Dockerfile.dotnet with `SERVICE_NAME=OpenEye.PipelineCore`
- `event-router` — builds from Dockerfile.dotnet with `SERVICE_NAME=OpenEye.EventRouter`
- `dashboard` — builds from Dockerfile.dashboard, port 3000

Volumes: `frames`, `evidence`, `pgdata`

Dependencies / startup order via `depends_on` with healthchecks:
`redis, postgres` → `roboflow-inference` → `frame-capture, pipeline-core` → `detection-bridge` → `event-router` → `dashboard`

Environment variables via `.env` file.

**Step 4: Create `.env.example`**

```
POSTGRES_USER=openeye
POSTGRES_PASSWORD=openeye
POSTGRES_DB=openeye
REDIS_URL=redis://redis:6379
INFERENCE_URL=http://roboflow-inference:9001
ROBOFLOW_MODEL_ID=yolov8n-640
```

**Step 5: Commit**

```
feat: add Docker Compose setup with all services and infrastructure
```

---

### Task 17: PostgreSQL Schema Init Script

**Files:**
- Create: `docker/init.sql`

**Step 1: Create SQL schema**

Tables matching the Prisma schema but for direct PostgreSQL init (used by C# services via Dapper):
- `cameras`, `zones`, `tripwires`, `primitive_configs`, `rules`, `notification_channels`, `events`, `users`
- Indexes on `events(timestamp)`, `events(source_id)`, `events(rule_id)`
- Default admin user

**Step 2: Commit**

```
feat: add PostgreSQL schema initialization script
```

---

## Phase 6: Integration Testing & End-to-End

### Task 18: Integration Tests

**Files:**
- Create: `tests/OpenEye.IntegrationTests/FullPipelineTests.cs`
- Create: `tests/OpenEye.IntegrationTests/Scenarios/LoiteringScenario.cs`
- Create: `tests/OpenEye.IntegrationTests/Scenarios/SafetyViolationScenario.cs`
- Create: `tests/OpenEye.IntegrationTests/Scenarios/QueueAlertScenario.cs`
- Create: `tests/OpenEye.IntegrationTests/Fixtures/SyntheticDetections.cs`

**Step 1: Create synthetic detection trajectory fixtures**

Helper that generates sequences of `Detection` objects simulating:
- Person walking through a zone (enters, loiters, exits)
- Forklift crossing a tripwire
- Queue building up in a zone (1 → 5 → 10 people)
- Person absent from zone for extended period

**Step 2: Write FullPipelineTests**

End-to-end test using in-memory implementations (no Redis/Postgres):
1. Configure zones, primitives, rules
2. Feed synthetic detections through `PipelineOrchestrator`
3. Assert correct events are emitted with correct timing

**Step 3: Write domain scenario tests**

- **Loitering:** Person enters zone, stays > 30s → loitering event fires. Person leaves at 25s → no event.
- **Safety violation:** Person enters restricted zone → immediate event. With PPE class absent → fires.
- **Queue alert:** Count in zone exceeds threshold for sustained period → queue alert fires.

**Step 4: Run all tests**

```bash
dotnet test -v n
```

**Step 5: Commit**

```
feat: add integration tests with full pipeline scenarios
```

---

## Phase 7: Polish & Documentation

### Task 19: Configuration Examples & Getting Started

**Files:**
- Create: `examples/retail-store.md` — example config for a retail store scenario
- Create: `examples/warehouse-safety.md` — example config for warehouse safety
- Update: top-level README or similar developer onboarding if requested

**Step 1: Write example configurations**

Show complete camera → zone → primitive → rule → notification config for real-world scenarios.

**Step 2: Commit**

```
docs: add configuration examples for retail and warehouse scenarios
```

---

## Summary

| Phase | Tasks | Focus |
|---|---|---|
| **1: Foundation** | Tasks 1–4 | Solution structure, models, interfaces, infrastructure helpers |
| **2: Pipeline Core** | Tasks 5–9 | Tracker, zones, primitives, rules, orchestrator |
| **3: Peripheral Services** | Tasks 10–12 | Frame capture, detection bridge, event router |
| **4: Dashboard** | Tasks 13–15 | Next.js scaffolding, API routes, UI pages |
| **5: Infrastructure** | Tasks 16–17 | Docker Compose, database schema |
| **6: Integration Testing** | Task 18 | Full pipeline scenarios, synthetic fixtures |
| **7: Polish** | Task 19 | Examples, documentation |

**Execution order:** Phases 1–2 are strictly sequential (each task builds on the last). Phase 3 tasks are independent of each other but depend on Phase 2. Phase 4 can begin in parallel with Phase 3. Phases 5–7 depend on all prior phases.

**Testing approach:** TDD throughout — write tests first, then implement. Each task includes its own test step. Integration tests in Phase 6 validate the complete pipeline with realistic scenarios.
