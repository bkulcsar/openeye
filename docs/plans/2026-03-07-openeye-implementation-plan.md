# OpenEye Platform Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a multi-service video analytics platform that converts camera streams into actionable events using configurable rules, with a Next.js dashboard for configuration and monitoring.

**Architecture:** Six services communicate via Redis Streams: frame-capture decodes camera streams, detection-bridge calls Roboflow Inference, pipeline-core runs tracking/zones/primitives/rules, event-router persists and notifies. A Next.js dashboard provides configuration UI and real-time event monitoring. PostgreSQL stores all config and event history. .NET Aspire orchestrates the backend services.

**Tech Stack:** .NET 10 (C#), .NET Aspire, Redis Streams, PostgreSQL, Dapper, OpenCvSharp4, Next.js (App Router), TypeScript, Prisma, Tailwind CSS

**Design Reference:** `docs/plans/2026-03-06-openeye-framework-design.md`

---

## Phase 1: Project Scaffolding

### Task 1: Create .NET Solution and Projects

**Files:**
- Create: `src/OpenEye.slnx`
- Create: `src/OpenEye.Shared/OpenEye.Shared.csproj`
- Create: `src/OpenEye.Abstractions/OpenEye.Abstractions.csproj`
- Create: `src/OpenEye.PipelineCore/OpenEye.PipelineCore.csproj`
- Create: `src/OpenEye.FrameCapture/OpenEye.FrameCapture.csproj`
- Create: `src/OpenEye.DetectionBridge/OpenEye.DetectionBridge.csproj`
- Create: `src/OpenEye.EventRouter/OpenEye.EventRouter.csproj`
- Create: `tests/OpenEye.Tests/OpenEye.Tests.csproj`
- Create: `tests/OpenEye.IntegrationTests/OpenEye.IntegrationTests.csproj`

**Step 1: Create class library projects**

```bash
dotnet new classlib -n OpenEye.Shared -o src/OpenEye.Shared -f net10.0
dotnet new classlib -n OpenEye.Abstractions -o src/OpenEye.Abstractions -f net10.0
```

**Step 2: Create worker service projects**

```bash
dotnet new worker -n OpenEye.PipelineCore -o src/OpenEye.PipelineCore -f net10.0
dotnet new worker -n OpenEye.FrameCapture -o src/OpenEye.FrameCapture -f net10.0
dotnet new worker -n OpenEye.DetectionBridge -o src/OpenEye.DetectionBridge -f net10.0
dotnet new worker -n OpenEye.EventRouter -o src/OpenEye.EventRouter -f net10.0
```

**Step 3: Create test projects**

```bash
dotnet new xunit -n OpenEye.Tests -o tests/OpenEye.Tests -f net10.0
dotnet new xunit -n OpenEye.IntegrationTests -o tests/OpenEye.IntegrationTests -f net10.0
```

**Step 4: Create solution and add all projects**

```bash
dotnet new sln -n OpenEye -o src --type slnx
dotnet sln src/OpenEye.slnx add src/OpenEye.Shared/OpenEye.Shared.csproj src/OpenEye.Abstractions/OpenEye.Abstractions.csproj src/OpenEye.PipelineCore/OpenEye.PipelineCore.csproj src/OpenEye.FrameCapture/OpenEye.FrameCapture.csproj src/OpenEye.DetectionBridge/OpenEye.DetectionBridge.csproj src/OpenEye.EventRouter/OpenEye.EventRouter.csproj tests/OpenEye.Tests/OpenEye.Tests.csproj tests/OpenEye.IntegrationTests/OpenEye.IntegrationTests.csproj
```

**Step 5: Add project references**

```bash
dotnet add src/OpenEye.Abstractions reference src/OpenEye.Shared/OpenEye.Shared.csproj
dotnet add src/OpenEye.PipelineCore reference src/OpenEye.Shared/OpenEye.Shared.csproj src/OpenEye.Abstractions/OpenEye.Abstractions.csproj
dotnet add src/OpenEye.FrameCapture reference src/OpenEye.Shared/OpenEye.Shared.csproj src/OpenEye.Abstractions/OpenEye.Abstractions.csproj
dotnet add src/OpenEye.DetectionBridge reference src/OpenEye.Shared/OpenEye.Shared.csproj src/OpenEye.Abstractions/OpenEye.Abstractions.csproj
dotnet add src/OpenEye.EventRouter reference src/OpenEye.Shared/OpenEye.Shared.csproj src/OpenEye.Abstractions/OpenEye.Abstractions.csproj
dotnet add tests/OpenEye.Tests reference src/OpenEye.Shared/OpenEye.Shared.csproj src/OpenEye.Abstractions/OpenEye.Abstractions.csproj src/OpenEye.PipelineCore/OpenEye.PipelineCore.csproj
dotnet add tests/OpenEye.IntegrationTests reference src/OpenEye.Shared/OpenEye.Shared.csproj src/OpenEye.Abstractions/OpenEye.Abstractions.csproj src/OpenEye.PipelineCore/OpenEye.PipelineCore.csproj
```

**Step 6: Install NuGet packages**

```bash
dotnet add src/OpenEye.Shared package StackExchange.Redis
dotnet add src/OpenEye.Shared package Npgsql
dotnet add src/OpenEye.Shared package Dapper
dotnet add src/OpenEye.FrameCapture package OpenCvSharp4
dotnet add src/OpenEye.FrameCapture package OpenCvSharp4.runtime.win
```

**Step 7: Build to verify**

```bash
dotnet build src/OpenEye.slnx
```
Expected: Build succeeded with 0 errors.

**Step 8: Commit**

```bash
git add -A
git commit -m "feat: scaffold .NET solution with all service projects, tests, and references"
```

---

### Task 2: Aspire AppHost and ServiceDefaults

**Files:**
- Create: `src/OpenEye.AppHost/OpenEye.AppHost.csproj`
- Create: `src/OpenEye.AppHost/Program.cs`
- Create: `src/OpenEye.ServiceDefaults/OpenEye.ServiceDefaults.csproj`
- Create: `src/OpenEye.ServiceDefaults/Extensions.cs`
- Modify: `src/OpenEye.slnx`

**Step 1: Create Aspire projects**

```bash
dotnet new aspire-apphost -n OpenEye.AppHost -o src/OpenEye.AppHost
dotnet new aspire-servicedefaults -n OpenEye.ServiceDefaults -o src/OpenEye.ServiceDefaults
dotnet sln src/OpenEye.slnx add src/OpenEye.AppHost/OpenEye.AppHost.csproj src/OpenEye.ServiceDefaults/OpenEye.ServiceDefaults.csproj
```

**Step 2: Add AppHost references to all service projects**

```bash
dotnet add src/OpenEye.AppHost reference src/OpenEye.PipelineCore/OpenEye.PipelineCore.csproj src/OpenEye.FrameCapture/OpenEye.FrameCapture.csproj src/OpenEye.DetectionBridge/OpenEye.DetectionBridge.csproj src/OpenEye.EventRouter/OpenEye.EventRouter.csproj
```

**Step 3: Add ServiceDefaults reference to all services**

```bash
dotnet add src/OpenEye.PipelineCore reference src/OpenEye.ServiceDefaults/OpenEye.ServiceDefaults.csproj
dotnet add src/OpenEye.FrameCapture reference src/OpenEye.ServiceDefaults/OpenEye.ServiceDefaults.csproj
dotnet add src/OpenEye.DetectionBridge reference src/OpenEye.ServiceDefaults/OpenEye.ServiceDefaults.csproj
dotnet add src/OpenEye.EventRouter reference src/OpenEye.ServiceDefaults/OpenEye.ServiceDefaults.csproj
```

**Step 4: Install Aspire hosting packages**

```bash
dotnet add src/OpenEye.AppHost package Aspire.Hosting.Redis
dotnet add src/OpenEye.AppHost package Aspire.Hosting.PostgreSQL
dotnet add src/OpenEye.AppHost package Aspire.Hosting.NodeJs
```

**Step 5: Write AppHost Program.cs**

```csharp
// src/OpenEye.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");
var postgres = builder.AddPostgres("postgres")
    .AddDatabase("openeye");

var frameCapture = builder.AddProject<Projects.OpenEye_FrameCapture>("frame-capture")
    .WithReference(redis);

var detectionBridge = builder.AddProject<Projects.OpenEye_DetectionBridge>("detection-bridge")
    .WithReference(redis);

var pipelineCore = builder.AddProject<Projects.OpenEye_PipelineCore>("pipeline-core")
    .WithReference(redis)
    .WithReference(postgres);

var eventRouter = builder.AddProject<Projects.OpenEye_EventRouter>("event-router")
    .WithReference(redis)
    .WithReference(postgres);

builder.Build().Run();
```

**Step 6: Build to verify**

```bash
dotnet build src/OpenEye.slnx
```
Expected: Build succeeded with 0 errors.

**Step 7: Commit**

```bash
git add -A
git commit -m "feat: add Aspire AppHost and ServiceDefaults for service orchestration"
```

---

## Phase 2: Shared Foundation

### Task 3: Shared Domain Models

**Files:**
- Create: `src/OpenEye.Shared/Models/BoundingBox.cs`
- Create: `src/OpenEye.Shared/Models/Detection.cs`
- Create: `src/OpenEye.Shared/Models/TrackedObject.cs`
- Create: `src/OpenEye.Shared/Models/Zone.cs`
- Create: `src/OpenEye.Shared/Models/Tripwire.cs`
- Create: `src/OpenEye.Shared/Models/Primitive.cs`
- Create: `src/OpenEye.Shared/Models/Event.cs`
- Create: `src/OpenEye.Shared/Models/EvidenceRequest.cs`
- Create: `src/OpenEye.Shared/Models/RuleState.cs`
- Create: `src/OpenEye.Shared/Models/ZonePresence.cs`
- Create: `src/OpenEye.Shared/Models/ZoneTransition.cs`
- Create: `src/OpenEye.Shared/Models/TripwireCrossing.cs`
- Create: `src/OpenEye.Shared/Models/ZoneEvaluationResult.cs`
- Test: `tests/OpenEye.Tests/Models/ModelTests.cs`

**Step 1: Write model tests**

```csharp
// tests/OpenEye.Tests/Models/ModelTests.cs
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Models;

public class ModelTests
{
    [Fact]
    public void BoundingBox_Properties_SetCorrectly()
    {
        var box = new BoundingBox(0.1, 0.2, 0.3, 0.4);
        Assert.Equal(0.1, box.X);
        Assert.Equal(0.2, box.Y);
        Assert.Equal(0.3, box.Width);
        Assert.Equal(0.4, box.Height);
    }

    [Fact]
    public void Detection_RequiredProperties_SetCorrectly()
    {
        var det = new Detection(
            ClassLabel: "person",
            BoundingBox: new BoundingBox(0.1, 0.2, 0.3, 0.4),
            Confidence: 0.95,
            Timestamp: DateTimeOffset.UtcNow,
            SourceId: "camera-01");

        Assert.Equal("person", det.ClassLabel);
        Assert.Equal(0.95, det.Confidence);
        Assert.Null(det.FrameIndex);
    }

    [Fact]
    public void TrackedObject_InitialState_IsActive()
    {
        var obj = new TrackedObject
        {
            TrackId = "track-0",
            ClassLabel = "person",
            CurrentBox = new BoundingBox(0.1, 0.2, 0.3, 0.4),
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow
        };

        Assert.Equal(TrackState.Active, obj.State);
        Assert.Empty(obj.Trajectory);
        Assert.Empty(obj.Metadata);
    }

    [Fact]
    public void Event_RecordEquality_WorksCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var tracks = Array.Empty<TrackedObject>();
        var e1 = new Event("evt-1", "loitering", now, "cam-1", "zone-1", tracks, "rule-1");
        var e2 = new Event("evt-1", "loitering", now, "cam-1", "zone-1", tracks, "rule-1");
        Assert.Equal(e1, e2);
    }

    [Fact]
    public void Zone_Polygon_StoresNormalizedPoints()
    {
        var polygon = new List<Point2D>
        {
            new(0.0, 0.0), new(1.0, 0.0), new(1.0, 1.0), new(0.0, 1.0)
        };
        var zone = new Zone("checkout", "cam-1", polygon);
        Assert.Equal(4, zone.Polygon.Count);
        Assert.Equal(0.0, zone.Polygon[0].X);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ModelTests" -v minimal
```
Expected: FAIL — types do not exist yet.

**Step 3: Create all shared models**

```csharp
// src/OpenEye.Shared/Models/BoundingBox.cs
namespace OpenEye.Shared.Models;

public record BoundingBox(double X, double Y, double Width, double Height);
```

```csharp
// src/OpenEye.Shared/Models/Detection.cs
namespace OpenEye.Shared.Models;

public record Detection(
    string ClassLabel,
    BoundingBox BoundingBox,
    double Confidence,
    DateTimeOffset Timestamp,
    string SourceId,
    long? FrameIndex = null);
```

```csharp
// src/OpenEye.Shared/Models/TrackedObject.cs
namespace OpenEye.Shared.Models;

public enum TrackState { Active, Lost, Expired }

public record TrajectoryPoint(BoundingBox Box, DateTimeOffset Timestamp);

public class TrackedObject
{
    public required string TrackId { get; init; }
    public required string ClassLabel { get; init; }
    public required BoundingBox CurrentBox { get; set; }
    public List<TrajectoryPoint> Trajectory { get; } = [];
    public required DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; set; }
    public TrackState State { get; set; } = TrackState.Active;
    public Dictionary<string, object> Metadata { get; } = [];
}
```

```csharp
// src/OpenEye.Shared/Models/Zone.cs
namespace OpenEye.Shared.Models;

public record Point2D(double X, double Y);

public record Zone(
    string ZoneId,
    string SourceId,
    IReadOnlyList<Point2D> Polygon);
```

```csharp
// src/OpenEye.Shared/Models/Tripwire.cs
namespace OpenEye.Shared.Models;

public record Tripwire(
    string TripwireId,
    string SourceId,
    Point2D Start,
    Point2D End);
```

```csharp
// src/OpenEye.Shared/Models/Primitive.cs
namespace OpenEye.Shared.Models;

public record Primitive(
    string Name,
    object Value,
    DateTimeOffset Timestamp,
    string SourceId);
```

```csharp
// src/OpenEye.Shared/Models/Event.cs
namespace OpenEye.Shared.Models;

public record Event(
    string EventId,
    string EventType,
    DateTimeOffset Timestamp,
    string SourceId,
    string? ZoneId,
    IReadOnlyList<TrackedObject> TrackedObjects,
    string RuleId,
    Dictionary<string, object>? Metadata = null);
```

```csharp
// src/OpenEye.Shared/Models/EvidenceRequest.cs
namespace OpenEye.Shared.Models;

public enum EvidenceType { Screenshot, VideoClip, Both }

public record EvidenceRequest(
    string EventId,
    string SourceId,
    DateTimeOffset From,
    DateTimeOffset To,
    EvidenceType Type);
```

```csharp
// src/OpenEye.Shared/Models/RuleState.cs
namespace OpenEye.Shared.Models;

public class RuleState
{
    public required string RuleId { get; init; }
    public required string TrackId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public Dictionary<string, object> Data { get; } = [];
}
```

```csharp
// src/OpenEye.Shared/Models/ZonePresence.cs
namespace OpenEye.Shared.Models;

public class ZonePresence
{
    public required string TrackId { get; set; }
    public required string ZoneId { get; set; }
    public required DateTimeOffset EnteredAt { get; set; }
    public DateTimeOffset? ExitedAt { get; set; }
}
```

```csharp
// src/OpenEye.Shared/Models/ZoneTransition.cs
namespace OpenEye.Shared.Models;

public enum ZoneTransitionType { Enter, Exit }

public record ZoneTransition(
    string TrackId,
    string ZoneId,
    ZoneTransitionType Type,
    DateTimeOffset Timestamp);
```

```csharp
// src/OpenEye.Shared/Models/TripwireCrossing.cs
namespace OpenEye.Shared.Models;

public record TripwireCrossing(
    string TrackId,
    string TripwireId,
    DateTimeOffset Timestamp);
```

```csharp
// src/OpenEye.Shared/Models/ZoneEvaluationResult.cs
namespace OpenEye.Shared.Models;

public record ZoneEvaluationResult(
    IReadOnlyList<ZoneTransition> Transitions,
    IReadOnlyList<TripwireCrossing> TripwireCrossings,
    IReadOnlyList<ZonePresence> ActivePresences);
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ModelTests" -v minimal
```
Expected: All tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add shared domain models for detection, tracking, zones, primitives, events"
```

---

### Task 4: Configuration Models

**Files:**
- Create: `src/OpenEye.Shared/Models/Config/CameraConfig.cs`
- Create: `src/OpenEye.Shared/Models/Config/PrimitiveConfig.cs`
- Create: `src/OpenEye.Shared/Models/Config/RuleConfig.cs`
- Create: `src/OpenEye.Shared/Models/Config/NotificationConfig.cs`
- Create: `src/OpenEye.Shared/Models/Config/DetectionBridgeConfig.cs`

**Step 1: Create configuration models**

```csharp
// src/OpenEye.Shared/Models/Config/CameraConfig.cs
namespace OpenEye.Shared.Models.Config;

public class CameraConfig
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string StreamUrl { get; set; }
    public string Type { get; set; } = "rtsp"; // rtsp, mjpeg, usb
    public int TargetFps { get; set; } = 5;
    public bool Enabled { get; set; } = true;
}
```

```csharp
// src/OpenEye.Shared/Models/Config/PrimitiveConfig.cs
namespace OpenEye.Shared.Models.Config;

public class PrimitiveConfig
{
    public required string Name { get; set; }
    public required string Type { get; set; } // presence, absence, count, zone_duration, speed, line_crossed
    public required string SourceId { get; set; }
    public required string ZoneId { get; set; }
    public required string ClassLabel { get; set; }
    public string? TripwireId { get; set; }
}
```

```csharp
// src/OpenEye.Shared/Models/Config/RuleConfig.cs
namespace OpenEye.Shared.Models.Config;

public class RuleConfig
{
    public required string RuleId { get; set; }
    public required string Name { get; set; }
    public required string SourceId { get; set; }
    public string? ZoneId { get; set; }
    public required RuleCondition Condition { get; set; }
    public TemporalConfig? Temporal { get; set; }
    public EvidenceConfig? Evidence { get; set; }
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(60);
    public bool Enabled { get; set; } = true;
}

public class RuleCondition
{
    /// <summary>
    /// Condition types: zone_enter, zone_exit, duration, count_above, count_below,
    /// line_crossed, absent, speed, value_eq, value_gt, value_lt
    /// </summary>
    public required string Type { get; set; }
    public string? ClassLabel { get; set; }
    public string? PrimitiveName { get; set; }
    public double? Threshold { get; set; }
}

public class TemporalConfig
{
    public required string Mode { get; set; } // "sustained" or "within"
    public required double Seconds { get; set; }
    public int MinOccurrences { get; set; } = 1;
}

public class EvidenceConfig
{
    public required EvidenceType Type { get; set; }
    public double PreEventSeconds { get; set; } = 10;
    public double PostEventSeconds { get; set; } = 5;
}
```

```csharp
// src/OpenEye.Shared/Models/Config/NotificationConfig.cs
namespace OpenEye.Shared.Models.Config;

public class NotificationConfig
{
    public required string RuleId { get; set; }
    public List<NotificationChannel> Channels { get; set; } = [];
}

public class NotificationChannel
{
    public required string Type { get; set; } // webhook, whatsapp, email, dashboard
    public string? Url { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
```

```csharp
// src/OpenEye.Shared/Models/Config/DetectionBridgeConfig.cs
namespace OpenEye.Shared.Models.Config;

public class DetectionBridgeConfig
{
    public required string InferenceUrl { get; set; }
    public string ModelId { get; set; } = "yolov8n-640";
    public double ConfidenceThreshold { get; set; } = 0.5;
}
```

**Step 2: Build to verify**

```bash
dotnet build src/OpenEye.slnx
```
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add configuration models for cameras, rules, primitives, notifications"
```

---

### Task 5: Abstractions (Interfaces)

**Files:**
- Create: `src/OpenEye.Abstractions/IGlobalEventBus.cs`
- Create: `src/OpenEye.Abstractions/IRuleStateStore.cs`
- Create: `src/OpenEye.Abstractions/IEvidenceProvider.cs`
- Create: `src/OpenEye.Abstractions/IConfigRepository.cs`
- Create: `src/OpenEye.Abstractions/IEventRepository.cs`
- Create: `src/OpenEye.Abstractions/INotificationDispatcher.cs`
- Create: `src/OpenEye.Abstractions/IRedisStreamPublisher.cs`
- Create: `src/OpenEye.Abstractions/IRedisStreamConsumer.cs`

**Step 1: Create all interfaces**

```csharp
// src/OpenEye.Abstractions/IGlobalEventBus.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IGlobalEventBus
{
    void Publish(Event evt);
    IAsyncEnumerable<Event> Subscribe(string? sourceFilter = null);
}
```

```csharp
// src/OpenEye.Abstractions/IRuleStateStore.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IRuleStateStore
{
    RuleState? Get(string ruleId, string trackId);
    void Set(string ruleId, string trackId, RuleState state);
    void Remove(string ruleId, string trackId);
    void RemoveByTrack(string trackId);
    IReadOnlyList<RuleState> GetByRule(string ruleId);
}
```

```csharp
// src/OpenEye.Abstractions/IEvidenceProvider.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IEvidenceProvider
{
    Task<string?> CaptureEvidence(EvidenceRequest request);
}
```

```csharp
// src/OpenEye.Abstractions/IConfigRepository.cs
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.Abstractions;

public interface IConfigRepository
{
    Task<IReadOnlyList<CameraConfig>> GetCamerasAsync();
    Task<IReadOnlyList<Zone>> GetZonesAsync(string sourceId);
    Task<IReadOnlyList<Tripwire>> GetTripwiresAsync(string sourceId);
    Task<IReadOnlyList<PrimitiveConfig>> GetPrimitivesAsync(string sourceId);
    Task<IReadOnlyList<RuleConfig>> GetRulesAsync(string? sourceId = null);
    Task<IReadOnlyList<NotificationConfig>> GetNotificationsAsync(string ruleId);
}
```

```csharp
// src/OpenEye.Abstractions/IEventRepository.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public class EventQuery
{
    public string? SourceId { get; set; }
    public string? RuleId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}

public interface IEventRepository
{
    Task SaveEventAsync(Event evt);
    Task<IReadOnlyList<Event>> GetEventsAsync(EventQuery query);
}
```

```csharp
// src/OpenEye.Abstractions/INotificationDispatcher.cs
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.Abstractions;

public interface INotificationDispatcher
{
    string ChannelType { get; }
    Task DispatchAsync(Event evt, NotificationChannel channel, string? evidenceUrl = null);
}
```

```csharp
// src/OpenEye.Abstractions/IRedisStreamPublisher.cs
namespace OpenEye.Abstractions;

public interface IRedisStreamPublisher
{
    Task PublishAsync(string streamKey, object message);
}
```

```csharp
// src/OpenEye.Abstractions/IRedisStreamConsumer.cs
namespace OpenEye.Abstractions;

public interface IRedisStreamConsumer
{
    IAsyncEnumerable<T> ConsumeAsync<T>(string streamKey, string groupName, string consumerName, CancellationToken ct = default);
}
```

**Step 2: Build to verify**

```bash
dotnet build src/OpenEye.slnx
```
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add abstraction interfaces for event bus, repositories, Redis streams, notifications"
```

---

## Phase 3: Pipeline Core — Object Tracking

### Task 6: IoU Calculator

**Files:**
- Create: `src/OpenEye.PipelineCore/Tracking/IouCalculator.cs`
- Test: `tests/OpenEye.Tests/Tracking/IouCalculatorTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Tracking/IouCalculatorTests.cs
using OpenEye.PipelineCore.Tracking;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Tracking;

public class IouCalculatorTests
{
    [Fact]
    public void ComputeIou_IdenticalBoxes_ReturnsOne()
    {
        var box = new BoundingBox(0.1, 0.1, 0.2, 0.2);
        Assert.Equal(1.0, IouCalculator.ComputeIou(box, box), precision: 5);
    }

    [Fact]
    public void ComputeIou_NoOverlap_ReturnsZero()
    {
        var a = new BoundingBox(0.0, 0.0, 0.1, 0.1);
        var b = new BoundingBox(0.5, 0.5, 0.1, 0.1);
        Assert.Equal(0.0, IouCalculator.ComputeIou(a, b));
    }

    [Fact]
    public void ComputeIou_PartialOverlap_ReturnsCorrectValue()
    {
        // A: (0,0) to (0.2,0.2) — area 0.04
        // B: (0.1,0.1) to (0.3,0.3) — area 0.04
        // Intersection: (0.1,0.1) to (0.2,0.2) — area 0.01
        // Union: 0.04 + 0.04 - 0.01 = 0.07
        var a = new BoundingBox(0.0, 0.0, 0.2, 0.2);
        var b = new BoundingBox(0.1, 0.1, 0.2, 0.2);
        Assert.Equal(0.01 / 0.07, IouCalculator.ComputeIou(a, b), precision: 5);
    }

    [Fact]
    public void ComputeIou_ContainedBox_ReturnsCorrectValue()
    {
        // A: (0,0) to (1,1) — area 1.0
        // B: (0.25,0.25) to (0.75,0.75) — area 0.25
        // Intersection: 0.25, Union: 1.0
        var a = new BoundingBox(0.0, 0.0, 1.0, 1.0);
        var b = new BoundingBox(0.25, 0.25, 0.5, 0.5);
        Assert.Equal(0.25, IouCalculator.ComputeIou(a, b), precision: 5);
    }

    [Fact]
    public void ComputeIou_ZeroAreaBox_ReturnsZero()
    {
        var a = new BoundingBox(0.1, 0.1, 0.0, 0.0);
        var b = new BoundingBox(0.1, 0.1, 0.2, 0.2);
        Assert.Equal(0.0, IouCalculator.ComputeIou(a, b));
    }

    [Fact]
    public void ComputeIou_TouchingEdges_ReturnsZero()
    {
        var a = new BoundingBox(0.0, 0.0, 0.1, 0.1);
        var b = new BoundingBox(0.1, 0.0, 0.1, 0.1);
        Assert.Equal(0.0, IouCalculator.ComputeIou(a, b));
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~IouCalculatorTests" -v minimal
```
Expected: FAIL — `IouCalculator` does not exist.

**Step 3: Implement IouCalculator**

```csharp
// src/OpenEye.PipelineCore/Tracking/IouCalculator.cs
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Tracking;

public static class IouCalculator
{
    public static double ComputeIou(BoundingBox a, BoundingBox b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        if (x2 <= x1 || y2 <= y1)
            return 0.0;

        var intersectionArea = (x2 - x1) * (y2 - y1);
        var areaA = a.Width * a.Height;
        var areaB = b.Width * b.Height;
        var unionArea = areaA + areaB - intersectionArea;

        return unionArea > 0 ? intersectionArea / unionArea : 0.0;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~IouCalculatorTests" -v minimal
```
Expected: All 6 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add IoU calculator for bounding box overlap computation"
```

---

### Task 7: Greedy Matcher

**Files:**
- Create: `src/OpenEye.PipelineCore/Tracking/GreedyMatcher.cs`
- Test: `tests/OpenEye.Tests/Tracking/GreedyMatcherTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Tracking/GreedyMatcherTests.cs
using OpenEye.PipelineCore.Tracking;

namespace OpenEye.Tests.Tracking;

public class GreedyMatcherTests
{
    [Fact]
    public void Match_EmptyMatrix_ReturnsNoMatches()
    {
        var matrix = new double[0, 0];
        var (matches, unmatchedTracks, unmatchedDets) = GreedyMatcher.Match(matrix, 0, 0);
        Assert.Empty(matches);
        Assert.Empty(unmatchedTracks);
        Assert.Empty(unmatchedDets);
    }

    [Fact]
    public void Match_PerfectDiagonal_MatchesAll()
    {
        var matrix = new double[,]
        {
            { 0.9, 0.1 },
            { 0.1, 0.8 }
        };
        var (matches, unmatchedTracks, unmatchedDets) = GreedyMatcher.Match(matrix, 2, 2);
        Assert.Equal(2, matches.Count);
        Assert.Empty(unmatchedTracks);
        Assert.Empty(unmatchedDets);
    }

    [Fact]
    public void Match_AllBelowThreshold_NoMatches()
    {
        var matrix = new double[,]
        {
            { 0.1, 0.05 },
            { 0.02, 0.1 }
        };
        var (matches, unmatchedTracks, unmatchedDets) = GreedyMatcher.Match(matrix, 2, 2, minIou: 0.3);
        Assert.Empty(matches);
        Assert.Equal(2, unmatchedTracks.Count);
        Assert.Equal(2, unmatchedDets.Count);
    }

    [Fact]
    public void Match_MoreDetectionsThanTracks_ReturnsUnmatchedDetections()
    {
        var matrix = new double[,]
        {
            { 0.9, 0.1, 0.0 }
        };
        var (matches, unmatchedTracks, unmatchedDets) = GreedyMatcher.Match(matrix, 1, 3);
        Assert.Single(matches);
        Assert.Equal(0, matches[0].TrackIdx);
        Assert.Equal(0, matches[0].DetIdx);
        Assert.Empty(unmatchedTracks);
        Assert.Equal(2, unmatchedDets.Count);
    }

    [Fact]
    public void Match_MoreTracksThanDetections_ReturnsUnmatchedTracks()
    {
        var matrix = new double[,]
        {
            { 0.9 },
            { 0.1 },
            { 0.0 }
        };
        var (matches, unmatchedTracks, unmatchedDets) = GreedyMatcher.Match(matrix, 3, 1);
        Assert.Single(matches);
        Assert.Equal(2, unmatchedTracks.Count);
        Assert.Empty(unmatchedDets);
    }

    [Fact]
    public void Match_CompetingTracks_HighestIouWins()
    {
        // Both tracks want detection 0, track 1 has higher IoU
        var matrix = new double[,]
        {
            { 0.5, 0.0 },
            { 0.8, 0.0 }
        };
        var (matches, _, _) = GreedyMatcher.Match(matrix, 2, 2);
        // Track 1 should get detection 0 (IoU 0.8 > 0.5)
        Assert.Contains(matches, m => m.TrackIdx == 1 && m.DetIdx == 0);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~GreedyMatcherTests" -v minimal
```
Expected: FAIL — `GreedyMatcher` does not exist.

**Step 3: Implement GreedyMatcher**

```csharp
// src/OpenEye.PipelineCore/Tracking/GreedyMatcher.cs
namespace OpenEye.PipelineCore.Tracking;

public static class GreedyMatcher
{
    public record MatchResult(
        IReadOnlyList<(int TrackIdx, int DetIdx)> Matches,
        IReadOnlyList<int> UnmatchedTracks,
        IReadOnlyList<int> UnmatchedDetections);

    public static MatchResult Match(double[,] iouMatrix, int numTracks, int numDetections, double minIou = 0.3)
    {
        var matches = new List<(int TrackIdx, int DetIdx)>();
        var matchedTracks = new HashSet<int>();
        var matchedDetections = new HashSet<int>();

        // Collect all candidate pairs above threshold
        var candidates = new List<(int t, int d, double iou)>();
        for (int t = 0; t < numTracks; t++)
            for (int d = 0; d < numDetections; d++)
                if (iouMatrix[t, d] >= minIou)
                    candidates.Add((t, d, iouMatrix[t, d]));

        // Greedily assign by descending IoU
        foreach (var (t, d, _) in candidates.OrderByDescending(c => c.iou))
        {
            if (matchedTracks.Contains(t) || matchedDetections.Contains(d))
                continue;
            matches.Add((t, d));
            matchedTracks.Add(t);
            matchedDetections.Add(d);
        }

        var unmatchedTracks = Enumerable.Range(0, numTracks)
            .Where(t => !matchedTracks.Contains(t)).ToList();
        var unmatchedDets = Enumerable.Range(0, numDetections)
            .Where(d => !matchedDetections.Contains(d)).ToList();

        return new MatchResult(matches, unmatchedTracks, unmatchedDets);
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~GreedyMatcherTests" -v minimal
```
Expected: All 6 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add greedy IoU-based matcher for detection-to-track assignment"
```

---

### Task 8: Object Tracker

**Files:**
- Create: `src/OpenEye.PipelineCore/Tracking/ObjectTracker.cs`
- Test: `tests/OpenEye.Tests/Tracking/ObjectTrackerTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Tracking/ObjectTrackerTests.cs
using OpenEye.PipelineCore.Tracking;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Tracking;

public class ObjectTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Detection MakeDetection(string cls, double x, double y, double w = 0.1, double h = 0.1)
        => new(cls, new BoundingBox(x, y, w, h), 0.9, T0, "cam-1");

    [Fact]
    public void Update_FirstFrame_CreatesNewTracks()
    {
        var tracker = new ObjectTracker();
        var detections = new[] { MakeDetection("person", 0.1, 0.1), MakeDetection("person", 0.5, 0.5) };

        var tracks = tracker.Update(detections, T0);

        Assert.Equal(2, tracks.Count);
        Assert.All(tracks, t => Assert.Equal(TrackState.Active, t.State));
        Assert.All(tracks, t => Assert.Equal("person", t.ClassLabel));
    }

    [Fact]
    public void Update_SamePosition_MaintainsTrackId()
    {
        var tracker = new ObjectTracker();
        var det1 = new[] { MakeDetection("person", 0.5, 0.5) };
        var det2 = new[] { MakeDetection("person", 0.52, 0.52) }; // slight movement, high IoU

        var tracks1 = tracker.Update(det1, T0);
        var tracks2 = tracker.Update(det2, T0.AddSeconds(1));

        var activeTracks = tracks2.Where(t => t.State == TrackState.Active).ToList();
        Assert.Single(activeTracks);
        Assert.Equal(tracks1[0].TrackId, activeTracks[0].TrackId);
    }

    [Fact]
    public void Update_NoDetections_TracksBecomeLost()
    {
        var tracker = new ObjectTracker();
        tracker.Update([MakeDetection("person", 0.5, 0.5)], T0);

        var tracks = tracker.Update([], T0.AddSeconds(1));

        Assert.Contains(tracks, t => t.State == TrackState.Lost);
    }

    [Fact]
    public void Update_LostTooLong_TracksExpire()
    {
        var tracker = new ObjectTracker(maxLostAge: TimeSpan.FromSeconds(2));
        tracker.Update([MakeDetection("person", 0.5, 0.5)], T0);

        // No detections for 3 seconds
        tracker.Update([], T0.AddSeconds(1));
        tracker.Update([], T0.AddSeconds(2));
        var tracks = tracker.Update([], T0.AddSeconds(3));

        Assert.Contains(tracks, t => t.State == TrackState.Expired);
    }

    [Fact]
    public void Update_TrajectoryBuilds_OverMultipleFrames()
    {
        var tracker = new ObjectTracker();
        tracker.Update([MakeDetection("person", 0.1, 0.1)], T0);
        tracker.Update([MakeDetection("person", 0.12, 0.12)], T0.AddSeconds(1));
        var tracks = tracker.Update([MakeDetection("person", 0.14, 0.14)], T0.AddSeconds(2));

        var active = tracks.First(t => t.State == TrackState.Active);
        Assert.Equal(3, active.Trajectory.Count);
    }

    [Fact]
    public void Update_TrajectoryDepth_IsRespected()
    {
        var tracker = new ObjectTracker(trajectoryDepth: 3);
        for (int i = 0; i < 5; i++)
        {
            tracker.Update([MakeDetection("person", 0.1 + i * 0.01, 0.1)], T0.AddSeconds(i));
        }

        var tracks = tracker.Update([MakeDetection("person", 0.16, 0.1)], T0.AddSeconds(5));
        var active = tracks.First(t => t.State == TrackState.Active);
        Assert.Equal(3, active.Trajectory.Count);
    }

    [Fact]
    public void Update_TwoDistantObjects_GetSeparateTracks()
    {
        var tracker = new ObjectTracker();
        var detections = new[]
        {
            MakeDetection("person", 0.1, 0.1),
            MakeDetection("person", 0.9, 0.9)
        };

        var tracks = tracker.Update(detections, T0);
        Assert.Equal(2, tracks.Count);
        Assert.NotEqual(tracks[0].TrackId, tracks[1].TrackId);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ObjectTrackerTests" -v minimal
```
Expected: FAIL — `ObjectTracker` does not exist.

**Step 3: Implement ObjectTracker**

```csharp
// src/OpenEye.PipelineCore/Tracking/ObjectTracker.cs
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Tracking;

public class ObjectTracker
{
    private readonly List<TrackedObject> _tracks = [];
    private int _nextId;
    private readonly TimeSpan _maxLostAge;
    private readonly int _trajectoryDepth;
    private readonly double _minIou;

    public ObjectTracker(
        TimeSpan? maxLostAge = null,
        int trajectoryDepth = 50,
        double minIou = 0.3)
    {
        _maxLostAge = maxLostAge ?? TimeSpan.FromSeconds(5);
        _trajectoryDepth = trajectoryDepth;
        _minIou = minIou;
    }

    public IReadOnlyList<TrackedObject> Update(IReadOnlyList<Detection> detections, DateTimeOffset timestamp)
    {
        var activeTracks = _tracks.Where(t => t.State != TrackState.Expired).ToList();

        if (activeTracks.Count == 0 && detections.Count == 0)
            return _tracks.AsReadOnly();

        // Build IoU matrix
        var iouMatrix = new double[activeTracks.Count, detections.Count];
        for (int t = 0; t < activeTracks.Count; t++)
            for (int d = 0; d < detections.Count; d++)
                iouMatrix[t, d] = IouCalculator.ComputeIou(activeTracks[t].CurrentBox, detections[d].BoundingBox);

        var result = GreedyMatcher.Match(iouMatrix, activeTracks.Count, detections.Count, _minIou);

        // Update matched tracks
        foreach (var (trackIdx, detIdx) in result.Matches)
        {
            var track = activeTracks[trackIdx];
            var det = detections[detIdx];
            track.CurrentBox = det.BoundingBox;
            track.LastSeen = timestamp;
            track.State = TrackState.Active;
            track.Trajectory.Add(new TrajectoryPoint(det.BoundingBox, timestamp));
            if (track.Trajectory.Count > _trajectoryDepth)
                track.Trajectory.RemoveAt(0);
        }

        // Mark unmatched tracks as lost or expired
        foreach (var trackIdx in result.UnmatchedTracks)
        {
            var track = activeTracks[trackIdx];
            if (track.State == TrackState.Active)
                track.State = TrackState.Lost;

            if ((timestamp - track.LastSeen) > _maxLostAge)
                track.State = TrackState.Expired;
        }

        // Create new tracks for unmatched detections
        foreach (var detIdx in result.UnmatchedDetections)
        {
            var det = detections[detIdx];
            var track = new TrackedObject
            {
                TrackId = $"track-{_nextId++}",
                ClassLabel = det.ClassLabel,
                CurrentBox = det.BoundingBox,
                FirstSeen = timestamp,
                LastSeen = timestamp
            };
            track.Trajectory.Add(new TrajectoryPoint(det.BoundingBox, timestamp));
            _tracks.Add(track);
        }

        return _tracks.AsReadOnly();
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ObjectTrackerTests" -v minimal
```
Expected: All 7 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add SORT-style object tracker with IoU matching and track lifecycle"
```

---

## Phase 4: Pipeline Core — Zone Evaluation

### Task 9: Geometry Helpers

**Files:**
- Create: `src/OpenEye.PipelineCore/Zones/GeometryHelper.cs`
- Test: `tests/OpenEye.Tests/Zones/GeometryHelperTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Zones/GeometryHelperTests.cs
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Zones;

public class GeometryHelperTests
{
    private static readonly IReadOnlyList<Point2D> Square = new[]
    {
        new Point2D(0.0, 0.0), new Point2D(1.0, 0.0),
        new Point2D(1.0, 1.0), new Point2D(0.0, 1.0)
    };

    private static readonly IReadOnlyList<Point2D> Triangle = new[]
    {
        new Point2D(0.5, 0.0), new Point2D(1.0, 1.0), new Point2D(0.0, 1.0)
    };

    [Fact]
    public void PointInPolygon_InsideSquare_ReturnsTrue()
    {
        Assert.True(GeometryHelper.PointInPolygon(new Point2D(0.5, 0.5), Square));
    }

    [Fact]
    public void PointInPolygon_OutsideSquare_ReturnsFalse()
    {
        Assert.False(GeometryHelper.PointInPolygon(new Point2D(1.5, 0.5), Square));
    }

    [Fact]
    public void PointInPolygon_InsideTriangle_ReturnsTrue()
    {
        Assert.True(GeometryHelper.PointInPolygon(new Point2D(0.5, 0.8), Triangle));
    }

    [Fact]
    public void PointInPolygon_OutsideTriangle_ReturnsFalse()
    {
        Assert.False(GeometryHelper.PointInPolygon(new Point2D(0.1, 0.1), Triangle));
    }

    [Fact]
    public void Centroid_ReturnsCenter()
    {
        var box = new BoundingBox(0.2, 0.3, 0.4, 0.6);
        var c = GeometryHelper.Centroid(box);
        Assert.Equal(0.4, c.X, precision: 5);
        Assert.Equal(0.6, c.Y, precision: 5);
    }

    [Fact]
    public void LineSegmentsIntersect_Crossing_ReturnsTrue()
    {
        // X shape
        var a1 = new Point2D(0.0, 0.0);
        var a2 = new Point2D(1.0, 1.0);
        var b1 = new Point2D(0.0, 1.0);
        var b2 = new Point2D(1.0, 0.0);
        Assert.True(GeometryHelper.LineSegmentsIntersect(a1, a2, b1, b2));
    }

    [Fact]
    public void LineSegmentsIntersect_Parallel_ReturnsFalse()
    {
        var a1 = new Point2D(0.0, 0.0);
        var a2 = new Point2D(1.0, 0.0);
        var b1 = new Point2D(0.0, 1.0);
        var b2 = new Point2D(1.0, 1.0);
        Assert.False(GeometryHelper.LineSegmentsIntersect(a1, a2, b1, b2));
    }

    [Fact]
    public void LineSegmentsIntersect_NonIntersecting_ReturnsFalse()
    {
        var a1 = new Point2D(0.0, 0.0);
        var a2 = new Point2D(0.5, 0.5);
        var b1 = new Point2D(0.6, 0.0);
        var b2 = new Point2D(1.0, 0.0);
        Assert.False(GeometryHelper.LineSegmentsIntersect(a1, a2, b1, b2));
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~GeometryHelperTests" -v minimal
```
Expected: FAIL — `GeometryHelper` does not exist.

**Step 3: Implement GeometryHelper**

```csharp
// src/OpenEye.PipelineCore/Zones/GeometryHelper.cs
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Zones;

public static class GeometryHelper
{
    public static bool PointInPolygon(Point2D point, IReadOnlyList<Point2D> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y) &&
                point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) /
                           (polygon[j].Y - polygon[i].Y) + polygon[i].X)
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }

    public static Point2D Centroid(BoundingBox box) =>
        new(box.X + box.Width / 2.0, box.Y + box.Height / 2.0);

    public static bool LineSegmentsIntersect(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        double d1 = CrossProduct(b1, b2, a1);
        double d2 = CrossProduct(b1, b2, a2);
        double d3 = CrossProduct(a1, a2, b1);
        double d4 = CrossProduct(a1, a2, b2);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        return false;
    }

    private static double CrossProduct(Point2D o, Point2D a, Point2D b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~GeometryHelperTests" -v minimal
```
Expected: All 8 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add geometry helpers for point-in-polygon, centroid, line segment intersection"
```

---

### Task 10: Zone Evaluator

**Files:**
- Create: `src/OpenEye.PipelineCore/Zones/ZoneEvaluator.cs`
- Test: `tests/OpenEye.Tests/Zones/ZoneEvaluatorTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Zones/ZoneEvaluatorTests.cs
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Zones;

public class ZoneEvaluatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Zone TestZone = new("zone-1", "cam-1", new[]
    {
        new Point2D(0.2, 0.2), new Point2D(0.8, 0.2),
        new Point2D(0.8, 0.8), new Point2D(0.2, 0.8)
    });

    private static readonly Tripwire TestTripwire = new("trip-1", "cam-1",
        new Point2D(0.5, 0.0), new Point2D(0.5, 1.0));

    private static TrackedObject MakeTrack(string id, double x, double y,
        List<TrajectoryPoint>? trajectory = null)
    {
        var track = new TrackedObject
        {
            TrackId = id,
            ClassLabel = "person",
            CurrentBox = new BoundingBox(x - 0.05, y - 0.05, 0.1, 0.1),
            FirstSeen = T0,
            LastSeen = T0
        };
        if (trajectory != null)
            track.Trajectory.AddRange(trajectory);
        else
            track.Trajectory.Add(new TrajectoryPoint(track.CurrentBox, T0));
        return track;
    }

    [Fact]
    public void Evaluate_ObjectEntersZone_ProducesEnterTransition()
    {
        var evaluator = new ZoneEvaluator([TestZone], []);

        // Frame 1: outside zone
        evaluator.Evaluate([MakeTrack("t1", 0.1, 0.1)], T0);

        // Frame 2: inside zone
        var result = evaluator.Evaluate([MakeTrack("t1", 0.5, 0.5)], T0.AddSeconds(1));

        Assert.Single(result.Transitions);
        Assert.Equal("t1", result.Transitions[0].TrackId);
        Assert.Equal("zone-1", result.Transitions[0].ZoneId);
        Assert.Equal(ZoneTransitionType.Enter, result.Transitions[0].Type);
    }

    [Fact]
    public void Evaluate_ObjectExitsZone_ProducesExitTransition()
    {
        var evaluator = new ZoneEvaluator([TestZone], []);

        // Frame 1: inside zone
        evaluator.Evaluate([MakeTrack("t1", 0.5, 0.5)], T0);

        // Frame 2: outside zone
        var result = evaluator.Evaluate([MakeTrack("t1", 0.1, 0.1)], T0.AddSeconds(1));

        Assert.Single(result.Transitions);
        Assert.Equal(ZoneTransitionType.Exit, result.Transitions[0].Type);
    }

    [Fact]
    public void Evaluate_ObjectStaysInZone_NoTransition()
    {
        var evaluator = new ZoneEvaluator([TestZone], []);
        evaluator.Evaluate([MakeTrack("t1", 0.5, 0.5)], T0);

        var result = evaluator.Evaluate([MakeTrack("t1", 0.52, 0.52)], T0.AddSeconds(1));

        Assert.Empty(result.Transitions);
        Assert.Single(result.ActivePresences);
    }

    [Fact]
    public void Evaluate_ObjectCrossesTripwire_ProducesCrossing()
    {
        var evaluator = new ZoneEvaluator([], [TestTripwire]);

        var trajectory = new List<TrajectoryPoint>
        {
            new(new BoundingBox(0.35, 0.45, 0.1, 0.1), T0),
            new(new BoundingBox(0.55, 0.45, 0.1, 0.1), T0.AddSeconds(1))
        };
        var track = MakeTrack("t1", 0.6, 0.5, trajectory);

        var result = evaluator.Evaluate([track], T0.AddSeconds(1));

        Assert.Single(result.TripwireCrossings);
        Assert.Equal("t1", result.TripwireCrossings[0].TrackId);
        Assert.Equal("trip-1", result.TripwireCrossings[0].TripwireId);
    }

    [Fact]
    public void Evaluate_ObjectDoesNotCrossTripwire_NoCrossing()
    {
        var evaluator = new ZoneEvaluator([], [TestTripwire]);

        var trajectory = new List<TrajectoryPoint>
        {
            new(new BoundingBox(0.1, 0.45, 0.1, 0.1), T0),
            new(new BoundingBox(0.2, 0.45, 0.1, 0.1), T0.AddSeconds(1))
        };
        var track = MakeTrack("t1", 0.25, 0.5, trajectory);

        var result = evaluator.Evaluate([track], T0.AddSeconds(1));

        Assert.Empty(result.TripwireCrossings);
    }

    [Fact]
    public void Evaluate_ActivePresences_TracksDuration()
    {
        var evaluator = new ZoneEvaluator([TestZone], []);
        evaluator.Evaluate([MakeTrack("t1", 0.5, 0.5)], T0);
        var result = evaluator.Evaluate([MakeTrack("t1", 0.5, 0.5)], T0.AddSeconds(5));

        Assert.Single(result.ActivePresences);
        Assert.Equal(T0, result.ActivePresences[0].EnteredAt);
    }

    [Fact]
    public void Evaluate_ExpiredTrack_CleansUpPresence()
    {
        var evaluator = new ZoneEvaluator([TestZone], []);

        // Object enters zone
        evaluator.Evaluate([MakeTrack("t1", 0.5, 0.5)], T0);

        // Object gone (expired track not in list)
        var result = evaluator.Evaluate([], T0.AddSeconds(5));

        Assert.Empty(result.ActivePresences);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ZoneEvaluatorTests" -v minimal
```
Expected: FAIL — `ZoneEvaluator` does not exist.

**Step 3: Implement ZoneEvaluator**

```csharp
// src/OpenEye.PipelineCore/Zones/ZoneEvaluator.cs
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Zones;

public class ZoneEvaluator
{
    private readonly IReadOnlyList<Zone> _zones;
    private readonly IReadOnlyList<Tripwire> _tripwires;
    private readonly Dictionary<(string TrackId, string ZoneId), ZonePresence> _presences = [];
    private readonly HashSet<(string TrackId, string TripwireId)> _recentCrossings = [];

    public ZoneEvaluator(IReadOnlyList<Zone> zones, IReadOnlyList<Tripwire> tripwires)
    {
        _zones = zones;
        _tripwires = tripwires;
    }

    public ZoneEvaluationResult Evaluate(IReadOnlyList<TrackedObject> tracks, DateTimeOffset timestamp)
    {
        var transitions = new List<ZoneTransition>();
        var crossings = new List<TripwireCrossing>();
        var activeTrackIds = new HashSet<string>();

        foreach (var track in tracks.Where(t => t.State == TrackState.Active))
        {
            activeTrackIds.Add(track.TrackId);
            var centroid = GeometryHelper.Centroid(track.CurrentBox);

            // Zone enter/exit detection
            foreach (var zone in _zones)
            {
                var inside = GeometryHelper.PointInPolygon(centroid, zone.Polygon);
                var key = (track.TrackId, zone.ZoneId);

                if (inside && !_presences.ContainsKey(key))
                {
                    _presences[key] = new ZonePresence
                    {
                        TrackId = track.TrackId,
                        ZoneId = zone.ZoneId,
                        EnteredAt = timestamp
                    };
                    transitions.Add(new ZoneTransition(track.TrackId, zone.ZoneId,
                        ZoneTransitionType.Enter, timestamp));
                }
                else if (!inside && _presences.ContainsKey(key))
                {
                    _presences[key].ExitedAt = timestamp;
                    transitions.Add(new ZoneTransition(track.TrackId, zone.ZoneId,
                        ZoneTransitionType.Exit, timestamp));
                    _presences.Remove(key);
                }
            }

            // Tripwire crossing detection
            if (track.Trajectory.Count >= 2)
            {
                var prevBox = track.Trajectory[^2].Box;
                var prev = GeometryHelper.Centroid(prevBox);
                var curr = centroid;

                foreach (var tripwire in _tripwires)
                {
                    var crossed = GeometryHelper.LineSegmentsIntersect(
                        prev, curr, tripwire.Start, tripwire.End);
                    var crossKey = (track.TrackId, tripwire.TripwireId);

                    if (crossed && !_recentCrossings.Contains(crossKey))
                    {
                        _recentCrossings.Add(crossKey);
                        crossings.Add(new TripwireCrossing(track.TrackId, tripwire.TripwireId, timestamp));
                    }
                    else if (!crossed)
                    {
                        _recentCrossings.Remove(crossKey);
                    }
                }
            }
        }

        // Clean up presences for tracks that are no longer active
        var staleKeys = _presences.Keys
            .Where(k => !activeTrackIds.Contains(k.TrackId))
            .ToList();
        foreach (var key in staleKeys)
            _presences.Remove(key);

        return new ZoneEvaluationResult(transitions, crossings, _presences.Values.ToList());
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~ZoneEvaluatorTests" -v minimal
```
Expected: All 7 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add zone evaluator with enter/exit transitions and tripwire crossing detection"
```

---

## Phase 5: Pipeline Core — Primitive Extraction

### Task 11: Primitive Extractor

**Files:**
- Create: `src/OpenEye.PipelineCore/Primitives/PrimitiveExtractor.cs`
- Test: `tests/OpenEye.Tests/Primitives/PrimitiveExtractorTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Primitives/PrimitiveExtractorTests.cs
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.Tests.Primitives;

public class PrimitiveExtractorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TrackedObject MakeTrack(string id, string cls, double x, double y)
    {
        var track = new TrackedObject
        {
            TrackId = id, ClassLabel = cls,
            CurrentBox = new BoundingBox(x - 0.05, y - 0.05, 0.1, 0.1),
            FirstSeen = T0, LastSeen = T0
        };
        track.Trajectory.Add(new TrajectoryPoint(track.CurrentBox, T0));
        return track;
    }

    [Fact]
    public void Extract_Presence_TrueWhenObjectInZone()
    {
        var config = new PrimitiveConfig
        {
            Name = "person_at_checkout", Type = "presence",
            SourceId = "cam-1", ZoneId = "zone-1", ClassLabel = "person"
        };
        var extractor = new PrimitiveExtractor([config]);

        var presences = new List<ZonePresence>
        {
            new() { TrackId = "t1", ZoneId = "zone-1", EnteredAt = T0 }
        };
        var tracks = new[] { MakeTrack("t1", "person", 0.5, 0.5) };
        var zoneResult = new ZoneEvaluationResult([], [], presences);

        var primitives = extractor.Extract(tracks, zoneResult, T0, "cam-1");

        Assert.Single(primitives);
        Assert.Equal("person_at_checkout", primitives[0].Name);
        Assert.Equal(true, primitives[0].Value);
    }

    [Fact]
    public void Extract_Presence_FalseWhenNoObjectInZone()
    {
        var config = new PrimitiveConfig
        {
            Name = "person_at_checkout", Type = "presence",
            SourceId = "cam-1", ZoneId = "zone-1", ClassLabel = "person"
        };
        var extractor = new PrimitiveExtractor([config]);
        var zoneResult = new ZoneEvaluationResult([], [], []);

        var primitives = extractor.Extract([], zoneResult, T0, "cam-1");

        Assert.Single(primitives);
        Assert.Equal(false, primitives[0].Value);
    }

    [Fact]
    public void Extract_Absence_TrueWhenNoObjectInZone()
    {
        var config = new PrimitiveConfig
        {
            Name = "no_tray", Type = "absence",
            SourceId = "cam-1", ZoneId = "zone-1", ClassLabel = "tray"
        };
        var extractor = new PrimitiveExtractor([config]);
        var zoneResult = new ZoneEvaluationResult([], [], []);

        var primitives = extractor.Extract([], zoneResult, T0, "cam-1");

        Assert.Equal(true, primitives[0].Value);
    }

    [Fact]
    public void Extract_Count_ReturnsCorrectCount()
    {
        var config = new PrimitiveConfig
        {
            Name = "queue_length", Type = "count",
            SourceId = "cam-1", ZoneId = "zone-1", ClassLabel = "person"
        };
        var extractor = new PrimitiveExtractor([config]);

        var presences = new List<ZonePresence>
        {
            new() { TrackId = "t1", ZoneId = "zone-1", EnteredAt = T0 },
            new() { TrackId = "t2", ZoneId = "zone-1", EnteredAt = T0 },
            new() { TrackId = "t3", ZoneId = "zone-1", EnteredAt = T0 }
        };
        var tracks = new[]
        {
            MakeTrack("t1", "person", 0.3, 0.5),
            MakeTrack("t2", "person", 0.5, 0.5),
            MakeTrack("t3", "person", 0.7, 0.5)
        };
        var zoneResult = new ZoneEvaluationResult([], [], presences);

        var primitives = extractor.Extract(tracks, zoneResult, T0, "cam-1");

        Assert.Equal(3, primitives[0].Value);
    }

    [Fact]
    public void Extract_ZoneDuration_ReturnsSecondsInZone()
    {
        var config = new PrimitiveConfig
        {
            Name = "loiter_time", Type = "zone_duration",
            SourceId = "cam-1", ZoneId = "zone-1", ClassLabel = "person"
        };
        var extractor = new PrimitiveExtractor([config]);

        var presences = new List<ZonePresence>
        {
            new() { TrackId = "t1", ZoneId = "zone-1", EnteredAt = T0 }
        };
        var tracks = new[] { MakeTrack("t1", "person", 0.5, 0.5) };
        var zoneResult = new ZoneEvaluationResult([], [], presences);
        var now = T0.AddSeconds(30);

        var primitives = extractor.Extract(tracks, zoneResult, now, "cam-1");

        Assert.Equal(30.0, (double)primitives[0].Value, precision: 1);
    }

    [Fact]
    public void Extract_Speed_ReturnsMaxSpeedOfClass()
    {
        var config = new PrimitiveConfig
        {
            Name = "forklift_speed", Type = "speed",
            SourceId = "cam-1", ZoneId = "zone-1", ClassLabel = "forklift"
        };
        var extractor = new PrimitiveExtractor([config]);

        var track = MakeTrack("t1", "forklift", 0.5, 0.5);
        track.Trajectory.Clear();
        track.Trajectory.Add(new TrajectoryPoint(new BoundingBox(0.1, 0.45, 0.1, 0.1), T0));
        track.Trajectory.Add(new TrajectoryPoint(new BoundingBox(0.3, 0.45, 0.1, 0.1), T0.AddSeconds(1)));

        var zoneResult = new ZoneEvaluationResult([], [], []);
        var primitives = extractor.Extract([track], zoneResult, T0.AddSeconds(1), "cam-1");

        Assert.True((double)primitives[0].Value > 0);
    }

    [Fact]
    public void Extract_LineCrossed_TrueWhenCrossingDetected()
    {
        var config = new PrimitiveConfig
        {
            Name = "entered_area", Type = "line_crossed",
            SourceId = "cam-1", ZoneId = "zone-1", ClassLabel = "person",
            TripwireId = "trip-1"
        };
        var extractor = new PrimitiveExtractor([config]);

        var crossings = new List<TripwireCrossing>
        {
            new("t1", "trip-1", T0)
        };
        var zoneResult = new ZoneEvaluationResult([], crossings, []);

        var primitives = extractor.Extract([], zoneResult, T0, "cam-1");

        Assert.Equal(true, primitives[0].Value);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~PrimitiveExtractorTests" -v minimal
```
Expected: FAIL — `PrimitiveExtractor` does not exist.

**Step 3: Implement PrimitiveExtractor**

```csharp
// src/OpenEye.PipelineCore/Primitives/PrimitiveExtractor.cs
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.PipelineCore.Primitives;

public class PrimitiveExtractor
{
    private readonly IReadOnlyList<PrimitiveConfig> _configs;

    public PrimitiveExtractor(IReadOnlyList<PrimitiveConfig> configs)
    {
        _configs = configs;
    }

    public IReadOnlyList<Primitive> Extract(
        IReadOnlyList<TrackedObject> tracks,
        ZoneEvaluationResult zoneResult,
        DateTimeOffset timestamp,
        string sourceId)
    {
        var primitives = new List<Primitive>();

        foreach (var config in _configs)
        {
            object value = config.Type switch
            {
                "presence" => ExtractPresence(config, zoneResult, tracks),
                "absence" => !ExtractPresence(config, zoneResult, tracks),
                "count" => ExtractCount(config, zoneResult, tracks),
                "zone_duration" => ExtractZoneDuration(config, zoneResult, timestamp),
                "speed" => ExtractSpeed(config, tracks),
                "line_crossed" => ExtractLineCrossed(config, zoneResult),
                _ => false
            };

            primitives.Add(new Primitive(config.Name, value, timestamp, sourceId));
        }

        return primitives;
    }

    private static bool ExtractPresence(PrimitiveConfig config, ZoneEvaluationResult zoneResult,
        IReadOnlyList<TrackedObject> tracks)
    {
        return zoneResult.ActivePresences.Any(p =>
            p.ZoneId == config.ZoneId &&
            tracks.Any(t => t.TrackId == p.TrackId && t.ClassLabel == config.ClassLabel));
    }

    private static int ExtractCount(PrimitiveConfig config, ZoneEvaluationResult zoneResult,
        IReadOnlyList<TrackedObject> tracks)
    {
        return zoneResult.ActivePresences.Count(p =>
            p.ZoneId == config.ZoneId &&
            tracks.Any(t => t.TrackId == p.TrackId && t.ClassLabel == config.ClassLabel));
    }

    private static double ExtractZoneDuration(PrimitiveConfig config, ZoneEvaluationResult zoneResult,
        DateTimeOffset now)
    {
        var maxDuration = zoneResult.ActivePresences
            .Where(p => p.ZoneId == config.ZoneId)
            .Select(p => (now - p.EnteredAt).TotalSeconds)
            .DefaultIfEmpty(0.0)
            .Max();
        return maxDuration;
    }

    private static double ExtractSpeed(PrimitiveConfig config, IReadOnlyList<TrackedObject> tracks)
    {
        var maxSpeed = 0.0;
        foreach (var track in tracks.Where(t => t.ClassLabel == config.ClassLabel && t.Trajectory.Count >= 2))
        {
            var p1 = GeometryHelper.Centroid(track.Trajectory[^2].Box);
            var p2 = GeometryHelper.Centroid(track.Trajectory[^1].Box);
            var dt = (track.Trajectory[^1].Timestamp - track.Trajectory[^2].Timestamp).TotalSeconds;
            if (dt > 0)
            {
                var dist = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
                maxSpeed = Math.Max(maxSpeed, dist / dt);
            }
        }
        return maxSpeed;
    }

    private static bool ExtractLineCrossed(PrimitiveConfig config, ZoneEvaluationResult zoneResult)
    {
        return zoneResult.TripwireCrossings.Any(c => c.TripwireId == config.TripwireId);
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~PrimitiveExtractorTests" -v minimal
```
Expected: All 7 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add primitive extractor for presence, absence, count, duration, speed, line_crossed"
```

---

## Phase 6: Pipeline Core — Rule Engine

### Task 12: RingBuffer Utility

**Files:**
- Create: `src/OpenEye.Shared/Utilities/RingBuffer.cs`
- Test: `tests/OpenEye.Tests/Utilities/RingBufferTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Utilities/RingBufferTests.cs
using OpenEye.Shared.Utilities;

namespace OpenEye.Tests.Utilities;

public class RingBufferTests
{
    [Fact]
    public void Add_WithinCapacity_AllItemsAccessible()
    {
        var buffer = new RingBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        Assert.Equal(3, buffer.Count);
        Assert.Equal([1, 2, 3], buffer.ToList());
    }

    [Fact]
    public void Add_ExceedsCapacity_OldestDropped()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4);

        Assert.Equal(3, buffer.Count);
        Assert.Equal([2, 3, 4], buffer.ToList());
    }

    [Fact]
    public void Add_DoubleWrap_MaintainsOrder()
    {
        var buffer = new RingBuffer<int>(3);
        for (int i = 1; i <= 7; i++)
            buffer.Add(i);

        Assert.Equal([5, 6, 7], buffer.ToList());
    }

    [Fact]
    public void Empty_Buffer_HasZeroCount()
    {
        var buffer = new RingBuffer<int>(5);
        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~RingBufferTests" -v minimal
```
Expected: FAIL — `RingBuffer` does not exist.

**Step 3: Implement RingBuffer**

```csharp
// src/OpenEye.Shared/Utilities/RingBuffer.cs
using System.Collections;

namespace OpenEye.Shared.Utilities;

public class RingBuffer<T> : IEnumerable<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;

    public RingBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    public int Count => _count;

    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length) _count++;
    }

    public IEnumerator<T> GetEnumerator()
    {
        var start = _count < _buffer.Length ? 0 : _head;
        for (int i = 0; i < _count; i++)
            yield return _buffer[(start + i) % _buffer.Length];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~RingBufferTests" -v minimal
```
Expected: All 4 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add RingBuffer utility for temporal aggregation history"
```

---

### Task 13: InMemoryRuleStateStore

**Files:**
- Create: `src/OpenEye.PipelineCore/Rules/InMemoryRuleStateStore.cs`
- Test: `tests/OpenEye.Tests/Rules/InMemoryRuleStateStoreTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Rules/InMemoryRuleStateStoreTests.cs
using OpenEye.PipelineCore.Rules;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Rules;

public class InMemoryRuleStateStoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Get_NonExistent_ReturnsNull()
    {
        var store = new InMemoryRuleStateStore();
        Assert.Null(store.Get("rule-1", "track-1"));
    }

    [Fact]
    public void SetAndGet_ReturnsState()
    {
        var store = new InMemoryRuleStateStore();
        var state = new RuleState { RuleId = "rule-1", TrackId = "track-1", StartedAt = T0 };
        store.Set("rule-1", "track-1", state);

        var result = store.Get("rule-1", "track-1");
        Assert.NotNull(result);
        Assert.Equal("rule-1", result.RuleId);
    }

    [Fact]
    public void Remove_RemovesState()
    {
        var store = new InMemoryRuleStateStore();
        store.Set("rule-1", "track-1", new RuleState { RuleId = "rule-1", TrackId = "track-1", StartedAt = T0 });
        store.Remove("rule-1", "track-1");

        Assert.Null(store.Get("rule-1", "track-1"));
    }

    [Fact]
    public void RemoveByTrack_RemovesAllStatesForTrack()
    {
        var store = new InMemoryRuleStateStore();
        store.Set("rule-1", "track-1", new RuleState { RuleId = "rule-1", TrackId = "track-1", StartedAt = T0 });
        store.Set("rule-2", "track-1", new RuleState { RuleId = "rule-2", TrackId = "track-1", StartedAt = T0 });
        store.Set("rule-1", "track-2", new RuleState { RuleId = "rule-1", TrackId = "track-2", StartedAt = T0 });

        store.RemoveByTrack("track-1");

        Assert.Null(store.Get("rule-1", "track-1"));
        Assert.Null(store.Get("rule-2", "track-1"));
        Assert.NotNull(store.Get("rule-1", "track-2"));
    }

    [Fact]
    public void GetByRule_ReturnsAllStatesForRule()
    {
        var store = new InMemoryRuleStateStore();
        store.Set("rule-1", "track-1", new RuleState { RuleId = "rule-1", TrackId = "track-1", StartedAt = T0 });
        store.Set("rule-1", "track-2", new RuleState { RuleId = "rule-1", TrackId = "track-2", StartedAt = T0 });
        store.Set("rule-2", "track-1", new RuleState { RuleId = "rule-2", TrackId = "track-1", StartedAt = T0 });

        var results = store.GetByRule("rule-1");
        Assert.Equal(2, results.Count);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~InMemoryRuleStateStoreTests" -v minimal
```
Expected: FAIL — `InMemoryRuleStateStore` does not exist.

**Step 3: Implement InMemoryRuleStateStore**

```csharp
// src/OpenEye.PipelineCore/Rules/InMemoryRuleStateStore.cs
using System.Collections.Concurrent;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules;

public class InMemoryRuleStateStore : IRuleStateStore
{
    private readonly ConcurrentDictionary<(string RuleId, string TrackId), RuleState> _store = new();

    public RuleState? Get(string ruleId, string trackId)
    {
        _store.TryGetValue((ruleId, trackId), out var state);
        return state;
    }

    public void Set(string ruleId, string trackId, RuleState state)
    {
        _store[(ruleId, trackId)] = state;
    }

    public void Remove(string ruleId, string trackId)
    {
        _store.TryRemove((ruleId, trackId), out _);
    }

    public void RemoveByTrack(string trackId)
    {
        var keys = _store.Keys.Where(k => k.TrackId == trackId).ToList();
        foreach (var key in keys)
            _store.TryRemove(key, out _);
    }

    public IReadOnlyList<RuleState> GetByRule(string ruleId)
    {
        return _store.Where(kv => kv.Key.RuleId == ruleId)
            .Select(kv => kv.Value)
            .ToList();
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~InMemoryRuleStateStoreTests" -v minimal
```
Expected: All 5 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add in-memory rule state store backed by ConcurrentDictionary"
```

---

### Task 14: Rule Engine — Condition Evaluation

**Files:**
- Create: `src/OpenEye.PipelineCore/Rules/RuleEngine.cs`
- Test: `tests/OpenEye.Tests/Rules/RuleEngineTests.cs`

**Step 1: Write failing tests for basic conditions**

```csharp
// tests/OpenEye.Tests/Rules/RuleEngineTests.cs
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.Tests.Rules;

public class RuleEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TrackedObject MakeTrack(string id, string cls = "person")
    {
        return new TrackedObject
        {
            TrackId = id, ClassLabel = cls,
            CurrentBox = new BoundingBox(0.45, 0.45, 0.1, 0.1),
            FirstSeen = T0, LastSeen = T0
        };
    }

    [Fact]
    public void Evaluate_ZoneEnter_FiresEvent()
    {
        var rule = new RuleConfig
        {
            RuleId = "r1", Name = "person-entered", SourceId = "cam-1", ZoneId = "zone-1",
            Condition = new RuleCondition { Type = "zone_enter", ClassLabel = "person" },
            Cooldown = TimeSpan.Zero
        };
        var engine = new RuleEngine([rule]);

        var transitions = new List<ZoneTransition>
        {
            new("t1", "zone-1", ZoneTransitionType.Enter, T0)
        };
        var zoneResult = new ZoneEvaluationResult(transitions, [], []);

        var events = engine.Evaluate([MakeTrack("t1")], zoneResult, [], T0, "cam-1");

        Assert.Single(events);
        Assert.Equal("r1", events[0].RuleId);
        Assert.Equal("person-entered", events[0].EventType);
    }

    [Fact]
    public void Evaluate_ZoneExit_FiresEvent()
    {
        var rule = new RuleConfig
        {
            RuleId = "r1", Name = "person-left", SourceId = "cam-1", ZoneId = "zone-1",
            Condition = new RuleCondition { Type = "zone_exit", ClassLabel = "person" },
            Cooldown = TimeSpan.Zero
        };
        var engine = new RuleEngine([rule]);

        var transitions = new List<ZoneTransition>
        {
            new("t1", "zone-1", ZoneTransitionType.Exit, T0)
        };
        var zoneResult = new ZoneEvaluationResult(transitions, [], []);

        var events = engine.Evaluate([MakeTrack("t1")], zoneResult, [], T0, "cam-1");

        Assert.Single(events);
    }

    [Fact]
    public void Evaluate_CountAbove_FiresWhenExceeded()
    {
        var rule = new RuleConfig
        {
            RuleId = "r1", Name = "crowded", SourceId = "cam-1", ZoneId = "zone-1",
            Condition = new RuleCondition { Type = "count_above", ClassLabel = "person", Threshold = 2 },
            Cooldown = TimeSpan.Zero
        };
        var engine = new RuleEngine([rule]);

        var presences = new List<ZonePresence>
        {
            new() { TrackId = "t1", ZoneId = "zone-1", EnteredAt = T0 },
            new() { TrackId = "t2", ZoneId = "zone-1", EnteredAt = T0 },
            new() { TrackId = "t3", ZoneId = "zone-1", EnteredAt = T0 }
        };
        var tracks = new[] { MakeTrack("t1"), MakeTrack("t2"), MakeTrack("t3") };
        var zoneResult = new ZoneEvaluationResult([], [], presences);

        var events = engine.Evaluate(tracks, zoneResult, [], T0, "cam-1");

        Assert.Single(events);
    }

    [Fact]
    public void Evaluate_CountAbove_DoesNotFireWhenBelow()
    {
        var rule = new RuleConfig
        {
            RuleId = "r1", Name = "crowded", SourceId = "cam-1", ZoneId = "zone-1",
            Condition = new RuleCondition { Type = "count_above", ClassLabel = "person", Threshold = 5 },
            Cooldown = TimeSpan.Zero
        };
        var engine = new RuleEngine([rule]);

        var presences = new List<ZonePresence>
        {
            new() { TrackId = "t1", ZoneId = "zone-1", EnteredAt = T0 }
        };
        var zoneResult = new ZoneEvaluationResult([], [], presences);

        var events = engine.Evaluate([MakeTrack("t1")], zoneResult, [], T0, "cam-1");

        Assert.Empty(events);
    }

    [Fact]
    public void Evaluate_Duration_FiresWhenExceeded()
    {
        var rule = new RuleConfig
        {
            RuleId = "r1", Name = "loitering", SourceId = "cam-1", ZoneId = "zone-1",
            Condition = new RuleCondition { Type = "duration", ClassLabel = "person", Threshold = 30 },
            Cooldown = TimeSpan.Zero
        };
        var engine = new RuleEngine([rule]);

        var presences = new List<ZonePresence>
        {
            new() { TrackId = "t1", ZoneId = "zone-1", EnteredAt = T0 }
        };
        var zoneResult = new ZoneEvaluationResult([], [], presences);

        // 31 seconds later
        var events = engine.Evaluate([MakeTrack("t1")], zoneResult, [], T0.AddSeconds(31), "cam-1");

        Assert.Single(events);
    }

    [Fact]
    public void Evaluate_Cooldown_PreventsRefire()
    {
        var rule = new RuleConfig
        {
            RuleId = "r1", Name = "person-entered", SourceId = "cam-1", ZoneId = "zone-1",
            Condition = new RuleCondition { Type = "zone_enter", ClassLabel = "person" },
            Cooldown = TimeSpan.FromSeconds(60)
        };
        var engine = new RuleEngine([rule]);

        var transitions = new List<ZoneTransition> { new("t1", "zone-1", ZoneTransitionType.Enter, T0) };
        var zoneResult = new ZoneEvaluationResult(transitions, [], []);

        // First fire
        var events1 = engine.Evaluate([MakeTrack("t1")], zoneResult, [], T0, "cam-1");
        Assert.Single(events1);

        // Try again within cooldown
        var events2 = engine.Evaluate([MakeTrack("t1")], zoneResult, [], T0.AddSeconds(30), "cam-1");
        Assert.Empty(events2);

        // After cooldown
        var events3 = engine.Evaluate([MakeTrack("t1")], zoneResult, [], T0.AddSeconds(61), "cam-1");
        Assert.Single(events3);
    }

    [Fact]
    public void Evaluate_DisabledRule_DoesNotFire()
    {
        var rule = new RuleConfig
        {
            RuleId = "r1", Name = "person-entered", SourceId = "cam-1", ZoneId = "zone-1",
            Condition = new RuleCondition { Type = "zone_enter", ClassLabel = "person" },
            Enabled = false
        };
        var engine = new RuleEngine([rule]);

        var transitions = new List<ZoneTransition> { new("t1", "zone-1", ZoneTransitionType.Enter, T0) };
        var zoneResult = new ZoneEvaluationResult(transitions, [], []);

        var events = engine.Evaluate([MakeTrack("t1")], zoneResult, [], T0, "cam-1");

        Assert.Empty(events);
    }

    [Fact]
    public void Evaluate_ValueGt_FiresOnPrimitive()
    {
        var rule = new RuleConfig
        {
            RuleId = "r1", Name = "queue-long", SourceId = "cam-1",
            Condition = new RuleCondition { Type = "value_gt", PrimitiveName = "queue_length", Threshold = 5 },
            Cooldown = TimeSpan.Zero
        };
        var engine = new RuleEngine([rule]);

        var primitives = new List<Primitive> { new("queue_length", 8, T0, "cam-1") };
        var zoneResult = new ZoneEvaluationResult([], [], []);

        var events = engine.Evaluate([], zoneResult, primitives, T0, "cam-1");

        Assert.Single(events);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~RuleEngineTests" -v minimal
```
Expected: FAIL — `RuleEngine` does not exist.

**Step 3: Implement RuleEngine**

```csharp
// src/OpenEye.PipelineCore/Rules/RuleEngine.cs
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;
using OpenEye.Shared.Utilities;

namespace OpenEye.PipelineCore.Rules;

public class RuleEngine
{
    private readonly IReadOnlyList<RuleConfig> _rules;
    private readonly Dictionary<string, DateTimeOffset> _lastFired = [];
    private readonly Dictionary<string, RingBuffer<(DateTimeOffset Timestamp, bool Result)>> _evalHistory = [];

    public RuleEngine(IReadOnlyList<RuleConfig> rules)
    {
        _rules = rules;
    }

    public IReadOnlyList<Event> Evaluate(
        IReadOnlyList<TrackedObject> tracks,
        ZoneEvaluationResult zoneResult,
        IReadOnlyList<Primitive> primitives,
        DateTimeOffset timestamp,
        string sourceId)
    {
        var events = new List<Event>();

        foreach (var rule in _rules.Where(r => r.Enabled))
        {
            // Check cooldown
            if (_lastFired.TryGetValue(rule.RuleId, out var lastFired) &&
                (timestamp - lastFired) < rule.Cooldown)
                continue;

            var conditionMet = EvaluateCondition(rule, tracks, zoneResult, primitives, timestamp);

            if (rule.Temporal != null)
            {
                if (!_evalHistory.ContainsKey(rule.RuleId))
                    _evalHistory[rule.RuleId] = new RingBuffer<(DateTimeOffset, bool)>(1000);

                _evalHistory[rule.RuleId].Add((timestamp, conditionMet));

                var shouldFire = rule.Temporal.Mode switch
                {
                    "sustained" => EvaluateSustained(rule.RuleId, rule.Temporal, timestamp),
                    "within" => EvaluateWithin(rule.RuleId, rule.Temporal, timestamp),
                    _ => false
                };

                if (!shouldFire) continue;
            }
            else if (!conditionMet)
            {
                continue;
            }

            // Build involved tracks list
            var involvedTracks = GetInvolvedTracks(rule, tracks, zoneResult);

            var evt = new Event(
                EventId: Guid.NewGuid().ToString(),
                EventType: rule.Name,
                Timestamp: timestamp,
                SourceId: sourceId,
                ZoneId: rule.ZoneId,
                TrackedObjects: involvedTracks,
                RuleId: rule.RuleId);

            events.Add(evt);
            _lastFired[rule.RuleId] = timestamp;
        }

        return events;
    }

    private static bool EvaluateCondition(
        RuleConfig rule,
        IReadOnlyList<TrackedObject> tracks,
        ZoneEvaluationResult zoneResult,
        IReadOnlyList<Primitive> primitives,
        DateTimeOffset timestamp)
    {
        return rule.Condition.Type switch
        {
            "zone_enter" => zoneResult.Transitions.Any(t =>
                t.ZoneId == rule.ZoneId &&
                t.Type == ZoneTransitionType.Enter &&
                MatchesClass(t.TrackId, rule.Condition.ClassLabel, tracks)),

            "zone_exit" => zoneResult.Transitions.Any(t =>
                t.ZoneId == rule.ZoneId &&
                t.Type == ZoneTransitionType.Exit &&
                MatchesClass(t.TrackId, rule.Condition.ClassLabel, tracks)),

            "duration" => zoneResult.ActivePresences.Any(p =>
                p.ZoneId == rule.ZoneId &&
                (timestamp - p.EnteredAt).TotalSeconds > (rule.Condition.Threshold ?? 0) &&
                MatchesClass(p.TrackId, rule.Condition.ClassLabel, tracks)),

            "count_above" => CountInZone(rule, zoneResult, tracks) > (int)(rule.Condition.Threshold ?? 0),

            "count_below" => CountInZone(rule, zoneResult, tracks) < (int)(rule.Condition.Threshold ?? 0),

            "line_crossed" => zoneResult.TripwireCrossings.Any(c =>
                MatchesClass(c.TrackId, rule.Condition.ClassLabel, tracks)),

            "absent" => !zoneResult.ActivePresences.Any(p =>
                p.ZoneId == rule.ZoneId &&
                MatchesClass(p.TrackId, rule.Condition.ClassLabel, tracks)),

            "speed" => tracks.Any(t =>
                t.ClassLabel == rule.Condition.ClassLabel &&
                GetSpeed(t) > (rule.Condition.Threshold ?? 0)),

            "value_eq" => primitives.Any(p =>
                p.Name == rule.Condition.PrimitiveName &&
                Convert.ToDouble(p.Value) == (rule.Condition.Threshold ?? 0)),

            "value_gt" => primitives.Any(p =>
                p.Name == rule.Condition.PrimitiveName &&
                Convert.ToDouble(p.Value) > (rule.Condition.Threshold ?? 0)),

            "value_lt" => primitives.Any(p =>
                p.Name == rule.Condition.PrimitiveName &&
                Convert.ToDouble(p.Value) < (rule.Condition.Threshold ?? 0)),

            _ => false
        };
    }

    private bool EvaluateSustained(string ruleId, TemporalConfig temporal, DateTimeOffset now)
    {
        var cutoff = now - TimeSpan.FromSeconds(temporal.Seconds);
        var entries = _evalHistory[ruleId].Where(e => e.Timestamp >= cutoff).ToList();
        if (entries.Count == 0) return false;
        return entries.All(e => e.Result);
    }

    private bool EvaluateWithin(string ruleId, TemporalConfig temporal, DateTimeOffset now)
    {
        var cutoff = now - TimeSpan.FromSeconds(temporal.Seconds);
        var trueCount = _evalHistory[ruleId].Count(e => e.Timestamp >= cutoff && e.Result);
        return trueCount >= temporal.MinOccurrences;
    }

    private static bool MatchesClass(string trackId, string? classLabel, IReadOnlyList<TrackedObject> tracks)
    {
        if (classLabel == null) return true;
        return tracks.Any(t => t.TrackId == trackId && t.ClassLabel == classLabel);
    }

    private static int CountInZone(RuleConfig rule, ZoneEvaluationResult zoneResult,
        IReadOnlyList<TrackedObject> tracks)
    {
        return zoneResult.ActivePresences.Count(p =>
            p.ZoneId == rule.ZoneId &&
            MatchesClass(p.TrackId, rule.Condition.ClassLabel, tracks));
    }

    private static double GetSpeed(TrackedObject track)
    {
        if (track.Trajectory.Count < 2) return 0;
        var p1 = GeometryHelper.Centroid(track.Trajectory[^2].Box);
        var p2 = GeometryHelper.Centroid(track.Trajectory[^1].Box);
        var dt = (track.Trajectory[^1].Timestamp - track.Trajectory[^2].Timestamp).TotalSeconds;
        if (dt <= 0) return 0;
        return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2)) / dt;
    }

    private static IReadOnlyList<TrackedObject> GetInvolvedTracks(
        RuleConfig rule, IReadOnlyList<TrackedObject> tracks, ZoneEvaluationResult zoneResult)
    {
        if (rule.ZoneId != null)
        {
            var trackIdsInZone = zoneResult.ActivePresences
                .Where(p => p.ZoneId == rule.ZoneId)
                .Select(p => p.TrackId)
                .ToHashSet();

            // Also include tracks from transitions
            foreach (var t in zoneResult.Transitions.Where(t => t.ZoneId == rule.ZoneId))
                trackIdsInZone.Add(t.TrackId);

            return tracks.Where(t => trackIdsInZone.Contains(t.TrackId)).ToList();
        }

        if (rule.Condition.ClassLabel != null)
            return tracks.Where(t => t.ClassLabel == rule.Condition.ClassLabel).ToList();

        return [];
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~RuleEngineTests" -v minimal
```
Expected: All 8 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add rule engine with condition evaluation, temporal aggregation, and cooldown"
```

---

### Task 15: Rule Engine — Temporal Aggregation Tests

**Files:**
- Modify: `tests/OpenEye.Tests/Rules/RuleEngineTests.cs`

**Step 1: Add temporal aggregation tests**

Append the following test methods to the existing `RuleEngineTests` class:

```csharp
[Fact]
public void Evaluate_Sustained_FiresAfterContinuousCondition()
{
    var rule = new RuleConfig
    {
        RuleId = "r1", Name = "loitering", SourceId = "cam-1", ZoneId = "zone-1",
        Condition = new RuleCondition { Type = "duration", ClassLabel = "person", Threshold = 0 },
        Temporal = new TemporalConfig { Mode = "sustained", Seconds = 3 },
        Cooldown = TimeSpan.Zero
    };
    var engine = new RuleEngine([rule]);

    var presences = new List<ZonePresence>
    {
        new() { TrackId = "t1", ZoneId = "zone-1", EnteredAt = T0 }
    };
    var tracks = new[] { MakeTrack("t1") };
    var zoneResult = new ZoneEvaluationResult([], [], presences);

    // Condition true at T0, T0+1, T0+2 — not enough yet (sustained=3s)
    Assert.Empty(engine.Evaluate(tracks, zoneResult, [], T0, "cam-1"));
    Assert.Empty(engine.Evaluate(tracks, zoneResult, [], T0.AddSeconds(1), "cam-1"));
    Assert.Empty(engine.Evaluate(tracks, zoneResult, [], T0.AddSeconds(2), "cam-1"));

    // At T0+3 — sustained for 3 seconds, should fire
    var events = engine.Evaluate(tracks, zoneResult, [], T0.AddSeconds(3), "cam-1");
    Assert.Single(events);
}

[Fact]
public void Evaluate_Sustained_ResetsWhenConditionFalse()
{
    var rule = new RuleConfig
    {
        RuleId = "r1", Name = "loitering", SourceId = "cam-1", ZoneId = "zone-1",
        Condition = new RuleCondition { Type = "duration", ClassLabel = "person", Threshold = 0 },
        Temporal = new TemporalConfig { Mode = "sustained", Seconds = 3 },
        Cooldown = TimeSpan.Zero
    };
    var engine = new RuleEngine([rule]);

    var presences = new List<ZonePresence>
    {
        new() { TrackId = "t1", ZoneId = "zone-1", EnteredAt = T0 }
    };
    var tracks = new[] { MakeTrack("t1") };

    // True for 2 seconds
    engine.Evaluate(tracks, zoneResult: new ZoneEvaluationResult([], [], presences), [], T0, "cam-1");
    engine.Evaluate(tracks, zoneResult: new ZoneEvaluationResult([], [], presences), [], T0.AddSeconds(1), "cam-1");

    // False at T0+2 (empty presences)
    engine.Evaluate(tracks, zoneResult: new ZoneEvaluationResult([], [], []), [], T0.AddSeconds(2), "cam-1");

    // True again at T0+3, T0+4, T0+5 — should NOT fire at T0+5 because reset happened
    engine.Evaluate(tracks, zoneResult: new ZoneEvaluationResult([], [], presences), [], T0.AddSeconds(3), "cam-1");
    engine.Evaluate(tracks, zoneResult: new ZoneEvaluationResult([], [], presences), [], T0.AddSeconds(4), "cam-1");
    var events = engine.Evaluate(tracks, zoneResult: new ZoneEvaluationResult([], [], presences), [], T0.AddSeconds(5), "cam-1");

    // Should not fire — the false at T0+2 broke the sustained window
    // But at T0+6 (3 consecutive seconds from T0+3), it should fire
    var events2 = engine.Evaluate(tracks, zoneResult: new ZoneEvaluationResult([], [], presences), [], T0.AddSeconds(6), "cam-1");
    Assert.Single(events2);
}

[Fact]
public void Evaluate_Within_FiresOnMinOccurrences()
{
    var rule = new RuleConfig
    {
        RuleId = "r1", Name = "frequent-entry", SourceId = "cam-1", ZoneId = "zone-1",
        Condition = new RuleCondition { Type = "zone_enter", ClassLabel = "person" },
        Temporal = new TemporalConfig { Mode = "within", Seconds = 10, MinOccurrences = 3 },
        Cooldown = TimeSpan.Zero
    };
    var engine = new RuleEngine([rule]);

    var tracks = new[] { MakeTrack("t1") };

    // 3 enter transitions within 10 seconds
    for (int i = 0; i < 3; i++)
    {
        var transitions = new List<ZoneTransition> { new("t1", "zone-1", ZoneTransitionType.Enter, T0.AddSeconds(i * 2)) };
        var zoneResult = new ZoneEvaluationResult(transitions, [], []);
        var events = engine.Evaluate(tracks, zoneResult, [], T0.AddSeconds(i * 2), "cam-1");

        if (i < 2)
            Assert.Empty(events);
        else
            Assert.Single(events);
    }
}
```

**Step 2: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~RuleEngineTests" -v minimal
```
Expected: All 11 tests PASS. If any temporal test fails, debug and fix the `EvaluateSustained`/`EvaluateWithin` logic.

**Step 3: Commit**

```bash
git add -A
git commit -m "test: add temporal aggregation tests for sustained and within modes"
```

---

## Phase 7: Pipeline Core — Orchestration

### Task 16: Local Event Bus

**Files:**
- Create: `src/OpenEye.PipelineCore/Pipeline/LocalEventBus.cs`
- Test: `tests/OpenEye.Tests/Pipeline/LocalEventBusTests.cs`

**Step 1: Write failing tests**

```csharp
// tests/OpenEye.Tests/Pipeline/LocalEventBusTests.cs
using OpenEye.PipelineCore.Pipeline;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Pipeline;

public class LocalEventBusTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Event MakeEvent(string sourceId = "cam-1") =>
        new("evt-1", "test", T0, sourceId, null, [], "rule-1");

    [Fact]
    public void Publish_SubscriberReceivesEvent()
    {
        var bus = new LocalEventBus();
        var received = new List<Event>();

        var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            await foreach (var evt in bus.Subscribe(ct: cts.Token))
            {
                received.Add(evt);
                break;
            }
        });

        bus.Publish(MakeEvent());
        task.Wait(TimeSpan.FromSeconds(2));

        Assert.Single(received);
    }

    [Fact]
    public void Subscribe_WithSourceFilter_OnlyReceivesMatchingEvents()
    {
        var bus = new LocalEventBus();
        var received = new List<Event>();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = Task.Run(async () =>
        {
            await foreach (var evt in bus.Subscribe(sourceFilter: "cam-2", ct: cts.Token))
            {
                received.Add(evt);
                break;
            }
        });

        bus.Publish(MakeEvent("cam-1")); // should be filtered out
        bus.Publish(MakeEvent("cam-2")); // should be received
        task.Wait(TimeSpan.FromSeconds(3));

        Assert.Single(received);
        Assert.Equal("cam-2", received[0].SourceId);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~LocalEventBusTests" -v minimal
```
Expected: FAIL — `LocalEventBus` does not exist.

**Step 3: Implement LocalEventBus**

```csharp
// src/OpenEye.PipelineCore/Pipeline/LocalEventBus.cs
using System.Threading.Channels;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Pipeline;

public class LocalEventBus : IGlobalEventBus
{
    private readonly Channel<Event> _channel = Channel.CreateUnbounded<Event>();

    public void Publish(Event evt)
    {
        _channel.Writer.TryWrite(evt);
    }

    public async IAsyncEnumerable<Event> Subscribe(
        string? sourceFilter = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            if (sourceFilter == null || evt.SourceId == sourceFilter)
                yield return evt;
        }
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~LocalEventBusTests" -v minimal
```
Expected: All 2 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add local event bus backed by System.Threading.Channels"
```

---

### Task 17: Camera Pipeline Orchestrator

**Files:**
- Create: `src/OpenEye.PipelineCore/Pipeline/CameraPipeline.cs`
- Test: `tests/OpenEye.Tests/Pipeline/CameraPipelineTests.cs`

**Step 1: Write failing test**

```csharp
// tests/OpenEye.Tests/Pipeline/CameraPipelineTests.cs
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.Tests.Pipeline;

public class CameraPipelineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProcessFrame_FullPipeline_ProducesEventForLoitering()
    {
        // Zone covering center of frame
        var zone = new Zone("zone-1", "cam-1", new[]
        {
            new Point2D(0.2, 0.2), new Point2D(0.8, 0.2),
            new Point2D(0.8, 0.8), new Point2D(0.2, 0.8)
        });

        // Rule: loitering = person in zone > 5 seconds
        var rule = new RuleConfig
        {
            RuleId = "loiter", Name = "loitering", SourceId = "cam-1", ZoneId = "zone-1",
            Condition = new RuleCondition { Type = "duration", ClassLabel = "person", Threshold = 5 },
            Cooldown = TimeSpan.Zero
        };

        var eventBus = new LocalEventBus();
        var pipeline = new CameraPipeline(
            sourceId: "cam-1",
            tracker: new ObjectTracker(),
            zoneEvaluator: new ZoneEvaluator([zone], []),
            primitiveExtractor: new PrimitiveExtractor([]),
            ruleEngine: new RuleEngine([rule]),
            eventBus: eventBus);

        // Simulate person appearing in zone for 6 seconds
        var detection = new Detection("person", new BoundingBox(0.45, 0.45, 0.1, 0.1), 0.9, T0, "cam-1");

        IReadOnlyList<Event> events = [];
        for (int i = 0; i <= 6; i++)
        {
            var det = detection with { Timestamp = T0.AddSeconds(i) };
            events = pipeline.ProcessFrame([det], T0.AddSeconds(i));
        }

        // At 6 seconds, duration > 5 should have fired
        Assert.Single(events);
        Assert.Equal("loitering", events[0].EventType);
    }

    [Fact]
    public void ProcessFrame_NoDetections_NoEvents()
    {
        var pipeline = new CameraPipeline(
            sourceId: "cam-1",
            tracker: new ObjectTracker(),
            zoneEvaluator: new ZoneEvaluator([], []),
            primitiveExtractor: new PrimitiveExtractor([]),
            ruleEngine: new RuleEngine([]),
            eventBus: new LocalEventBus());

        var events = pipeline.ProcessFrame([], T0);

        Assert.Empty(events);
    }

    [Fact]
    public void ProcessFrame_QueueCountRule_FiresWhenExceeded()
    {
        var zone = new Zone("queue-zone", "cam-1", new[]
        {
            new Point2D(0.0, 0.0), new Point2D(1.0, 0.0),
            new Point2D(1.0, 1.0), new Point2D(0.0, 1.0)
        });

        var primitiveConfig = new PrimitiveConfig
        {
            Name = "queue_count", Type = "count",
            SourceId = "cam-1", ZoneId = "queue-zone", ClassLabel = "person"
        };

        var rule = new RuleConfig
        {
            RuleId = "queue-alert", Name = "queue-too-long", SourceId = "cam-1",
            Condition = new RuleCondition { Type = "value_gt", PrimitiveName = "queue_count", Threshold = 2 },
            Cooldown = TimeSpan.Zero
        };

        var pipeline = new CameraPipeline(
            sourceId: "cam-1",
            tracker: new ObjectTracker(),
            zoneEvaluator: new ZoneEvaluator([zone], []),
            primitiveExtractor: new PrimitiveExtractor([primitiveConfig]),
            ruleEngine: new RuleEngine([rule]),
            eventBus: new LocalEventBus());

        // 3 people in the zone
        var detections = new[]
        {
            new Detection("person", new BoundingBox(0.1, 0.1, 0.1, 0.1), 0.9, T0, "cam-1"),
            new Detection("person", new BoundingBox(0.4, 0.4, 0.1, 0.1), 0.9, T0, "cam-1"),
            new Detection("person", new BoundingBox(0.7, 0.7, 0.1, 0.1), 0.9, T0, "cam-1")
        };

        // First frame establishes tracks and zone presences
        pipeline.ProcessFrame(detections, T0);

        // Second frame — now zone presences are established
        var events = pipeline.ProcessFrame(
            detections.Select(d => d with { Timestamp = T0.AddSeconds(1) }).ToArray(),
            T0.AddSeconds(1));

        Assert.Single(events);
        Assert.Equal("queue-too-long", events[0].EventType);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~CameraPipelineTests" -v minimal
```
Expected: FAIL — `CameraPipeline` does not exist.

**Step 3: Implement CameraPipeline**

```csharp
// src/OpenEye.PipelineCore/Pipeline/CameraPipeline.cs
using OpenEye.Abstractions;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Pipeline;

public class CameraPipeline
{
    private readonly string _sourceId;
    private readonly ObjectTracker _tracker;
    private readonly ZoneEvaluator _zoneEvaluator;
    private readonly PrimitiveExtractor _primitiveExtractor;
    private readonly RuleEngine _ruleEngine;
    private readonly IGlobalEventBus _eventBus;

    public CameraPipeline(
        string sourceId,
        ObjectTracker tracker,
        ZoneEvaluator zoneEvaluator,
        PrimitiveExtractor primitiveExtractor,
        RuleEngine ruleEngine,
        IGlobalEventBus eventBus)
    {
        _sourceId = sourceId;
        _tracker = tracker;
        _zoneEvaluator = zoneEvaluator;
        _primitiveExtractor = primitiveExtractor;
        _ruleEngine = ruleEngine;
        _eventBus = eventBus;
    }

    public IReadOnlyList<Event> ProcessFrame(IReadOnlyList<Detection> detections, DateTimeOffset timestamp)
    {
        // Stage 1: Track objects
        var tracks = _tracker.Update(detections, timestamp);
        var activeTracks = tracks.Where(t => t.State == TrackState.Active).ToList();

        // Stage 2: Evaluate zones
        var zoneResult = _zoneEvaluator.Evaluate(activeTracks, timestamp);

        // Stage 3: Extract primitives
        var primitives = _primitiveExtractor.Extract(activeTracks, zoneResult, timestamp, _sourceId);

        // Stage 4: Evaluate rules
        var events = _ruleEngine.Evaluate(activeTracks, zoneResult, primitives, timestamp, _sourceId);

        // Stage 5: Publish events
        foreach (var evt in events)
            _eventBus.Publish(evt);

        return events;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/OpenEye.Tests --filter "FullyQualifiedName~CameraPipelineTests" -v minimal
```
Expected: All 3 tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add CameraPipeline orchestrator wiring tracker, zones, primitives, and rules"
```

---

## Phase 8: Infrastructure — Redis & PostgreSQL

### Task 18: Redis Stream Helpers

**Files:**
- Create: `src/OpenEye.Shared/Redis/RedisStreamPublisher.cs`
- Create: `src/OpenEye.Shared/Redis/RedisStreamConsumer.cs`

**Step 1: Implement RedisStreamPublisher**

```csharp
// src/OpenEye.Shared/Redis/RedisStreamPublisher.cs
using System.Text.Json;
using OpenEye.Abstractions;
using StackExchange.Redis;

namespace OpenEye.Shared.Redis;

public class RedisStreamPublisher : IRedisStreamPublisher
{
    private readonly IConnectionMultiplexer _redis;

    public RedisStreamPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishAsync(string streamKey, object message)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(message);
        await db.StreamAddAsync(streamKey, [new NameValueEntry("data", json)], maxLength: 10000);
    }
}
```

**Step 2: Implement RedisStreamConsumer**

```csharp
// src/OpenEye.Shared/Redis/RedisStreamConsumer.cs
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenEye.Abstractions;
using StackExchange.Redis;

namespace OpenEye.Shared.Redis;

public class RedisStreamConsumer : IRedisStreamConsumer
{
    private readonly IConnectionMultiplexer _redis;

    public RedisStreamConsumer(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async IAsyncEnumerable<T> ConsumeAsync<T>(
        string streamKey,
        string groupName,
        string consumerName,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        // Create consumer group if it doesn't exist
        try
        {
            await db.StreamCreateConsumerGroupAsync(streamKey, groupName, "0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists
        }

        while (!ct.IsCancellationRequested)
        {
            var entries = await db.StreamReadGroupAsync(
                streamKey, groupName, consumerName,
                count: 10, noAck: false);

            if (entries.Length == 0)
            {
                await Task.Delay(100, ct);
                continue;
            }

            foreach (var entry in entries)
            {
                var data = entry["data"];
                if (data.IsNullOrEmpty) continue;

                var item = JsonSerializer.Deserialize<T>(data.ToString());
                if (item != null)
                {
                    yield return item;
                    await db.StreamAcknowledgeAsync(streamKey, groupName, entry.Id);
                }
            }
        }
    }
}
```

**Step 3: Build to verify**

```bash
dotnet build src/OpenEye.slnx
```
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add Redis Stream publisher and consumer with consumer group support"
```

---

### Task 19: PostgreSQL Schema and Data Access

**Files:**
- Create: `src/OpenEye.Shared/Data/schema.sql`
- Create: `src/OpenEye.Shared/Data/ConfigRepository.cs`
- Create: `src/OpenEye.Shared/Data/EventRepository.cs`

**Step 1: Create SQL schema**

```sql
-- src/OpenEye.Shared/Data/schema.sql

CREATE TABLE IF NOT EXISTS cameras (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    stream_url TEXT NOT NULL,
    type TEXT NOT NULL DEFAULT 'rtsp',
    target_fps INT NOT NULL DEFAULT 5,
    enabled BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS zones (
    zone_id TEXT PRIMARY KEY,
    source_id TEXT NOT NULL REFERENCES cameras(id) ON DELETE CASCADE,
    polygon JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS tripwires (
    tripwire_id TEXT PRIMARY KEY,
    source_id TEXT NOT NULL REFERENCES cameras(id) ON DELETE CASCADE,
    start_point JSONB NOT NULL,
    end_point JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS primitive_configs (
    name TEXT PRIMARY KEY,
    type TEXT NOT NULL,
    source_id TEXT NOT NULL REFERENCES cameras(id) ON DELETE CASCADE,
    zone_id TEXT REFERENCES zones(zone_id) ON DELETE CASCADE,
    class_label TEXT NOT NULL,
    tripwire_id TEXT REFERENCES tripwires(tripwire_id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS rule_configs (
    rule_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    source_id TEXT NOT NULL REFERENCES cameras(id) ON DELETE CASCADE,
    zone_id TEXT REFERENCES zones(zone_id) ON DELETE SET NULL,
    condition_type TEXT NOT NULL,
    condition_class_label TEXT,
    condition_primitive_name TEXT,
    condition_threshold DOUBLE PRECISION,
    temporal_mode TEXT,
    temporal_seconds DOUBLE PRECISION,
    temporal_min_occurrences INT DEFAULT 1,
    evidence_type TEXT,
    evidence_pre_seconds DOUBLE PRECISION DEFAULT 10,
    evidence_post_seconds DOUBLE PRECISION DEFAULT 5,
    cooldown_seconds DOUBLE PRECISION NOT NULL DEFAULT 60,
    enabled BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS notification_configs (
    id SERIAL PRIMARY KEY,
    rule_id TEXT NOT NULL REFERENCES rule_configs(rule_id) ON DELETE CASCADE,
    channel_type TEXT NOT NULL,
    url TEXT,
    phone TEXT,
    email TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS events (
    event_id TEXT PRIMARY KEY,
    event_type TEXT NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    source_id TEXT NOT NULL,
    zone_id TEXT,
    rule_id TEXT NOT NULL,
    tracked_objects JSONB NOT NULL,
    metadata JSONB,
    evidence_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_events_timestamp ON events(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_events_source_id ON events(source_id);
CREATE INDEX IF NOT EXISTS idx_events_rule_id ON events(rule_id);
```

**Step 2: Implement ConfigRepository**

```csharp
// src/OpenEye.Shared/Data/ConfigRepository.cs
using System.Text.Json;
using Dapper;
using Npgsql;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.Shared.Data;

public class ConfigRepository : IConfigRepository
{
    private readonly string _connectionString;

    public ConfigRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<IReadOnlyList<CameraConfig>> GetCamerasAsync()
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<CameraRow>("SELECT * FROM cameras WHERE enabled = true");
        return rows.Select(r => new CameraConfig
        {
            Id = r.id, Name = r.name, StreamUrl = r.stream_url,
            Type = r.type, TargetFps = r.target_fps, Enabled = r.enabled
        }).ToList();
    }

    public async Task<IReadOnlyList<Zone>> GetZonesAsync(string sourceId)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<ZoneRow>(
            "SELECT * FROM zones WHERE source_id = @sourceId", new { sourceId });
        return rows.Select(r => new Zone(
            r.zone_id, r.source_id,
            JsonSerializer.Deserialize<List<Point2D>>(r.polygon)!
        )).ToList();
    }

    public async Task<IReadOnlyList<Tripwire>> GetTripwiresAsync(string sourceId)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<TripwireRow>(
            "SELECT * FROM tripwires WHERE source_id = @sourceId", new { sourceId });
        return rows.Select(r => new Tripwire(
            r.tripwire_id, r.source_id,
            JsonSerializer.Deserialize<Point2D>(r.start_point)!,
            JsonSerializer.Deserialize<Point2D>(r.end_point)!
        )).ToList();
    }

    public async Task<IReadOnlyList<PrimitiveConfig>> GetPrimitivesAsync(string sourceId)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<PrimitiveConfigRow>(
            "SELECT * FROM primitive_configs WHERE source_id = @sourceId", new { sourceId });
        return rows.Select(r => new PrimitiveConfig
        {
            Name = r.name, Type = r.type, SourceId = r.source_id,
            ZoneId = r.zone_id, ClassLabel = r.class_label, TripwireId = r.tripwire_id
        }).ToList();
    }

    public async Task<IReadOnlyList<RuleConfig>> GetRulesAsync(string? sourceId = null)
    {
        using var conn = CreateConnection();
        var sql = sourceId != null
            ? "SELECT * FROM rule_configs WHERE source_id = @sourceId AND enabled = true"
            : "SELECT * FROM rule_configs WHERE enabled = true";
        var rows = await conn.QueryAsync<RuleConfigRow>(sql, new { sourceId });
        return rows.Select(MapRule).ToList();
    }

    public async Task<IReadOnlyList<NotificationConfig>> GetNotificationsAsync(string ruleId)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<NotificationConfigRow>(
            "SELECT * FROM notification_configs WHERE rule_id = @ruleId", new { ruleId });

        return rows.GroupBy(r => r.rule_id).Select(g => new NotificationConfig
        {
            RuleId = g.Key,
            Channels = g.Select(r => new NotificationChannel
            {
                Type = r.channel_type, Url = r.url, Phone = r.phone, Email = r.email
            }).ToList()
        }).ToList();
    }

    private static RuleConfig MapRule(RuleConfigRow r)
    {
        var rule = new RuleConfig
        {
            RuleId = r.rule_id, Name = r.name, SourceId = r.source_id, ZoneId = r.zone_id,
            Condition = new RuleCondition
            {
                Type = r.condition_type,
                ClassLabel = r.condition_class_label,
                PrimitiveName = r.condition_primitive_name,
                Threshold = r.condition_threshold
            },
            Cooldown = TimeSpan.FromSeconds(r.cooldown_seconds),
            Enabled = r.enabled
        };

        if (r.temporal_mode != null)
        {
            rule.Temporal = new TemporalConfig
            {
                Mode = r.temporal_mode,
                Seconds = r.temporal_seconds ?? 0,
                MinOccurrences = r.temporal_min_occurrences ?? 1
            };
        }

        if (r.evidence_type != null)
        {
            rule.Evidence = new EvidenceConfig
            {
                Type = Enum.Parse<EvidenceType>(r.evidence_type, ignoreCase: true),
                PreEventSeconds = r.evidence_pre_seconds ?? 10,
                PostEventSeconds = r.evidence_post_seconds ?? 5
            };
        }

        return rule;
    }

    // Row types for Dapper mapping
    private record CameraRow(string id, string name, string stream_url, string type, int target_fps, bool enabled);
    private record ZoneRow(string zone_id, string source_id, string polygon);
    private record TripwireRow(string tripwire_id, string source_id, string start_point, string end_point);
    private record PrimitiveConfigRow(string name, string type, string source_id, string zone_id, string class_label, string? tripwire_id);
    private record RuleConfigRow(string rule_id, string name, string source_id, string? zone_id,
        string condition_type, string? condition_class_label, string? condition_primitive_name, double? condition_threshold,
        string? temporal_mode, double? temporal_seconds, int? temporal_min_occurrences,
        string? evidence_type, double? evidence_pre_seconds, double? evidence_post_seconds,
        double cooldown_seconds, bool enabled);
    private record NotificationConfigRow(string rule_id, string channel_type, string? url, string? phone, string? email);
}
```

**Step 3: Implement EventRepository**

```csharp
// src/OpenEye.Shared/Data/EventRepository.cs
using System.Text.Json;
using Dapper;
using Npgsql;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.Shared.Data;

public class EventRepository : IEventRepository
{
    private readonly string _connectionString;

    public EventRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task SaveEventAsync(Event evt)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(
            """
            INSERT INTO events (event_id, event_type, timestamp, source_id, zone_id, rule_id, tracked_objects, metadata)
            VALUES (@EventId, @EventType, @Timestamp, @SourceId, @ZoneId, @RuleId, @TrackedObjects::jsonb, @Metadata::jsonb)
            """,
            new
            {
                evt.EventId, evt.EventType, evt.Timestamp, evt.SourceId, evt.ZoneId, evt.RuleId,
                TrackedObjects = JsonSerializer.Serialize(evt.TrackedObjects),
                Metadata = evt.Metadata != null ? JsonSerializer.Serialize(evt.Metadata) : null
            });
    }

    public async Task<IReadOnlyList<Event>> GetEventsAsync(EventQuery query)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = "SELECT * FROM events WHERE 1=1";
        var parameters = new DynamicParameters();

        if (query.SourceId != null)
        {
            sql += " AND source_id = @SourceId";
            parameters.Add("SourceId", query.SourceId);
        }
        if (query.RuleId != null)
        {
            sql += " AND rule_id = @RuleId";
            parameters.Add("RuleId", query.RuleId);
        }
        if (query.From != null)
        {
            sql += " AND timestamp >= @From";
            parameters.Add("From", query.From);
        }
        if (query.To != null)
        {
            sql += " AND timestamp <= @To";
            parameters.Add("To", query.To);
        }

        sql += " ORDER BY timestamp DESC LIMIT @Limit OFFSET @Offset";
        parameters.Add("Limit", query.Limit);
        parameters.Add("Offset", query.Offset);

        var rows = await conn.QueryAsync<EventRow>(sql, parameters);
        return rows.Select(r => new Event(
            r.event_id, r.event_type, r.timestamp, r.source_id, r.zone_id,
            JsonSerializer.Deserialize<List<TrackedObject>>(r.tracked_objects) ?? [],
            r.rule_id,
            r.metadata != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(r.metadata) : null
        )).ToList();
    }

    private record EventRow(string event_id, string event_type, DateTimeOffset timestamp,
        string source_id, string? zone_id, string rule_id, string tracked_objects, string? metadata);
}
```

**Step 4: Build to verify**

```bash
dotnet build src/OpenEye.slnx
```
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add PostgreSQL schema and Dapper-based config/event repositories"
```

---

## Phase 9: Services

### Task 20: Frame Capture Service

**Files:**
- Create: `src/OpenEye.FrameCapture/CameraCapture.cs`
- Modify: `src/OpenEye.FrameCapture/Program.cs`

**Step 1: Implement CameraCapture background service**

```csharp
// src/OpenEye.FrameCapture/CameraCapture.cs
using OpenCvSharp;
using OpenEye.Abstractions;
using OpenEye.Shared.Models.Config;

namespace OpenEye.FrameCapture;

public class CameraCapture : BackgroundService
{
    private readonly CameraConfig _config;
    private readonly IRedisStreamPublisher _publisher;
    private readonly string _frameOutputDir;
    private readonly ILogger<CameraCapture> _logger;

    public CameraCapture(
        CameraConfig config,
        IRedisStreamPublisher publisher,
        string frameOutputDir,
        ILogger<CameraCapture> logger)
    {
        _config = config;
        _publisher = publisher;
        _frameOutputDir = frameOutputDir;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_frameOutputDir);
        var frameInterval = TimeSpan.FromSeconds(1.0 / _config.TargetFps);
        long frameIndex = 0;
        int retryDelay = 1;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var capture = new VideoCapture(_config.StreamUrl);
                if (!capture.IsOpened())
                {
                    _logger.LogWarning("Failed to open stream for camera {CameraId}, retrying in {Delay}s",
                        _config.Id, retryDelay);
                    await Task.Delay(TimeSpan.FromSeconds(retryDelay), stoppingToken);
                    retryDelay = Math.Min(retryDelay * 2, 60);
                    continue;
                }

                retryDelay = 1;
                _logger.LogInformation("Connected to camera {CameraId}", _config.Id);

                using var frame = new Mat();
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (!capture.Read(frame) || frame.Empty())
                    {
                        _logger.LogWarning("Lost stream for camera {CameraId}", _config.Id);
                        break;
                    }

                    var path = Path.Combine(_frameOutputDir, $"{_config.Id}_{frameIndex}.jpg");
                    frame.SaveImage(path);

                    await _publisher.PublishAsync($"frames:{_config.Id}", new
                    {
                        frameIndex,
                        timestamp = DateTimeOffset.UtcNow,
                        framePath = path
                    });

                    frameIndex++;
                    await Task.Delay(frameInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing from camera {CameraId}", _config.Id);
                await Task.Delay(TimeSpan.FromSeconds(retryDelay), stoppingToken);
                retryDelay = Math.Min(retryDelay * 2, 60);
            }
        }
    }
}
```

**Step 2: Wire up Program.cs**

```csharp
// src/OpenEye.FrameCapture/Program.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Data;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redis = ConnectionMultiplexer.Connect(
    builder.Configuration.GetConnectionString("redis") ?? "localhost");
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<IRedisStreamPublisher, RedisStreamPublisher>();

var host = builder.Build();

// Load cameras from config and start a capture task per camera
var configRepo = new ConfigRepository(
    builder.Configuration.GetConnectionString("openeye") ?? "Host=localhost;Database=openeye");
var cameras = await configRepo.GetCamerasAsync();
var publisher = host.Services.GetRequiredService<IRedisStreamPublisher>();
var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var frameDir = builder.Configuration["FrameOutputDir"] ?? "/frames";

foreach (var camera in cameras.Where(c => c.Enabled))
{
    var capture = new OpenEye.FrameCapture.CameraCapture(
        camera, publisher, frameDir,
        loggerFactory.CreateLogger<OpenEye.FrameCapture.CameraCapture>());
    _ = capture.StartAsync(CancellationToken.None);
}

await host.RunAsync();
```

**Step 3: Build to verify**

```bash
dotnet build src/OpenEye.FrameCapture
```
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add frame capture service with RTSP/MJPEG stream decoding and auto-reconnect"
```

---

### Task 21: Detection Bridge Service

**Files:**
- Create: `src/OpenEye.DetectionBridge/DetectionBridgeWorker.cs`
- Create: `src/OpenEye.DetectionBridge/InferenceClient.cs`
- Modify: `src/OpenEye.DetectionBridge/Program.cs`

**Step 1: Implement InferenceClient**

```csharp
// src/OpenEye.DetectionBridge/InferenceClient.cs
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OpenEye.DetectionBridge;

public class InferenceClient
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly double _confidenceThreshold;

    public InferenceClient(HttpClient httpClient, string modelId, double confidenceThreshold)
    {
        _httpClient = httpClient;
        _modelId = modelId;
        _confidenceThreshold = confidenceThreshold;
    }

    public async Task<IReadOnlyList<InferencePrediction>> DetectAsync(byte[] imageBytes)
    {
        var base64 = Convert.ToBase64String(imageBytes);
        var response = await _httpClient.PostAsJsonAsync("/infer", new
        {
            image = base64,
            model_id = _modelId,
            confidence = _confidenceThreshold
        });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<InferenceResponse>();
        return result?.Predictions ?? [];
    }
}

public class InferenceResponse
{
    [JsonPropertyName("predictions")]
    public List<InferencePrediction> Predictions { get; set; } = [];
}

public class InferencePrediction
{
    [JsonPropertyName("class")]
    public string Class { get; set; } = "";
    [JsonPropertyName("x")]
    public double X { get; set; }
    [JsonPropertyName("y")]
    public double Y { get; set; }
    [JsonPropertyName("width")]
    public double Width { get; set; }
    [JsonPropertyName("height")]
    public double Height { get; set; }
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}
```

**Step 2: Implement DetectionBridgeWorker**

```csharp
// src/OpenEye.DetectionBridge/DetectionBridgeWorker.cs
using System.Text.Json;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.DetectionBridge;

public class DetectionBridgeWorker : BackgroundService
{
    private readonly IRedisStreamConsumer _consumer;
    private readonly IRedisStreamPublisher _publisher;
    private readonly InferenceClient _inferenceClient;
    private readonly ILogger<DetectionBridgeWorker> _logger;

    public DetectionBridgeWorker(
        IRedisStreamConsumer consumer,
        IRedisStreamPublisher publisher,
        InferenceClient inferenceClient,
        ILogger<DetectionBridgeWorker> logger)
    {
        _consumer = consumer;
        _publisher = publisher;
        _inferenceClient = inferenceClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: consume from multiple camera streams based on config
        // For now, consume from a known pattern
        await foreach (var msg in _consumer.ConsumeAsync<FrameMessage>(
            "frames:*", "detection-bridge", "worker-1", stoppingToken))
        {
            try
            {
                var frameBytes = await File.ReadAllBytesAsync(msg.FramePath, stoppingToken);
                var predictions = await _inferenceClient.DetectAsync(frameBytes);

                var detections = predictions.Select(p => new Detection(
                    ClassLabel: p.Class,
                    BoundingBox: new BoundingBox(p.X, p.Y, p.Width, p.Height),
                    Confidence: p.Confidence,
                    Timestamp: msg.Timestamp,
                    SourceId: msg.SourceId,
                    FrameIndex: msg.FrameIndex)).ToList();

                await _publisher.PublishAsync($"detections:{msg.SourceId}", new
                {
                    frameIndex = msg.FrameIndex,
                    timestamp = msg.Timestamp,
                    detections
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing frame {FrameIndex}", msg.FrameIndex);
            }
        }
    }
}

public record FrameMessage(long FrameIndex, DateTimeOffset Timestamp, string FramePath, string SourceId);
```

**Step 3: Wire up Program.cs**

```csharp
// src/OpenEye.DetectionBridge/Program.cs
using OpenEye.Abstractions;
using OpenEye.DetectionBridge;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redis = ConnectionMultiplexer.Connect(
    builder.Configuration.GetConnectionString("redis") ?? "localhost");
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<IRedisStreamPublisher, RedisStreamPublisher>();
builder.Services.AddSingleton<IRedisStreamConsumer, RedisStreamConsumer>();

var inferenceUrl = builder.Configuration["InferenceUrl"] ?? "http://localhost:9001";
var modelId = builder.Configuration["ModelId"] ?? "yolov8n-640";
var confidence = double.Parse(builder.Configuration["ConfidenceThreshold"] ?? "0.5");
builder.Services.AddSingleton(new InferenceClient(
    new HttpClient { BaseAddress = new Uri(inferenceUrl) }, modelId, confidence));

builder.Services.AddHostedService<DetectionBridgeWorker>();

var host = builder.Build();
await host.RunAsync();
```

**Step 4: Build to verify**

```bash
dotnet build src/OpenEye.DetectionBridge
```
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add detection bridge service calling Roboflow Inference and publishing detections"
```

---

### Task 22: Event Router Service

**Files:**
- Create: `src/OpenEye.EventRouter/EventRouterWorker.cs`
- Create: `src/OpenEye.EventRouter/Dispatchers/WebhookDispatcher.cs`
- Create: `src/OpenEye.EventRouter/Dispatchers/DashboardDispatcher.cs`
- Modify: `src/OpenEye.EventRouter/Program.cs`

**Step 1: Implement EventRouterWorker**

```csharp
// src/OpenEye.EventRouter/EventRouterWorker.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.EventRouter;

public class EventRouterWorker : BackgroundService
{
    private readonly IRedisStreamConsumer _consumer;
    private readonly IEventRepository _eventRepo;
    private readonly IConfigRepository _configRepo;
    private readonly IEnumerable<INotificationDispatcher> _dispatchers;
    private readonly ILogger<EventRouterWorker> _logger;

    public EventRouterWorker(
        IRedisStreamConsumer consumer,
        IEventRepository eventRepo,
        IConfigRepository configRepo,
        IEnumerable<INotificationDispatcher> dispatchers,
        ILogger<EventRouterWorker> logger)
    {
        _consumer = consumer;
        _eventRepo = eventRepo;
        _configRepo = configRepo;
        _dispatchers = dispatchers;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _consumer.ConsumeAsync<Event>(
            "events", "event-router", "worker-1", stoppingToken))
        {
            try
            {
                // Persist and notify in parallel
                var persistTask = _eventRepo.SaveEventAsync(evt);
                var notifyTask = DispatchNotificationsAsync(evt);
                await Task.WhenAll(persistTask, notifyTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {EventId}", evt.EventId);
            }
        }
    }

    private async Task DispatchNotificationsAsync(Event evt)
    {
        var notifications = await _configRepo.GetNotificationsAsync(evt.RuleId);
        foreach (var config in notifications)
        {
            foreach (var channel in config.Channels)
            {
                var dispatcher = _dispatchers.FirstOrDefault(d => d.ChannelType == channel.Type);
                if (dispatcher != null)
                {
                    try
                    {
                        await dispatcher.DispatchAsync(evt, channel);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to dispatch {ChannelType} for event {EventId}",
                            channel.Type, evt.EventId);
                        // TODO: retry with exponential backoff
                    }
                }
            }
        }
    }
}
```

**Step 2: Implement WebhookDispatcher**

```csharp
// src/OpenEye.EventRouter/Dispatchers/WebhookDispatcher.cs
using System.Net.Http.Json;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.EventRouter.Dispatchers;

public class WebhookDispatcher : INotificationDispatcher
{
    private readonly HttpClient _httpClient;

    public WebhookDispatcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string ChannelType => "webhook";

    public async Task DispatchAsync(Event evt, NotificationChannel channel, string? evidenceUrl = null)
    {
        if (channel.Url == null) return;
        await _httpClient.PostAsJsonAsync(channel.Url, new
        {
            evt.EventId, evt.EventType, evt.Timestamp, evt.SourceId,
            evt.ZoneId, evt.RuleId, evidenceUrl
        });
    }
}
```

**Step 3: Implement DashboardDispatcher**

```csharp
// src/OpenEye.EventRouter/Dispatchers/DashboardDispatcher.cs
using System.Text.Json;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;
using StackExchange.Redis;

namespace OpenEye.EventRouter.Dispatchers;

public class DashboardDispatcher : INotificationDispatcher
{
    private readonly IConnectionMultiplexer _redis;

    public DashboardDispatcher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public string ChannelType => "dashboard";

    public async Task DispatchAsync(Event evt, NotificationChannel channel, string? evidenceUrl = null)
    {
        var subscriber = _redis.GetSubscriber();
        var json = JsonSerializer.Serialize(evt);
        await subscriber.PublishAsync(RedisChannel.Literal("events:live"), json);
    }
}
```

**Step 4: Wire up Program.cs**

```csharp
// src/OpenEye.EventRouter/Program.cs
using OpenEye.Abstractions;
using OpenEye.EventRouter;
using OpenEye.EventRouter.Dispatchers;
using OpenEye.Shared.Data;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redisConn = builder.Configuration.GetConnectionString("redis") ?? "localhost";
var redis = ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<IRedisStreamConsumer, RedisStreamConsumer>();

var dbConn = builder.Configuration.GetConnectionString("openeye") ?? "Host=localhost;Database=openeye";
builder.Services.AddSingleton<IEventRepository>(new EventRepository(dbConn));
builder.Services.AddSingleton<IConfigRepository>(new ConfigRepository(dbConn));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<INotificationDispatcher, WebhookDispatcher>();
builder.Services.AddSingleton<INotificationDispatcher, DashboardDispatcher>();
builder.Services.AddHostedService<EventRouterWorker>();

var host = builder.Build();
await host.RunAsync();
```

**Step 5: Build to verify**

```bash
dotnet build src/OpenEye.EventRouter
```
Expected: Build succeeded.

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add event router service with webhook and dashboard notification dispatchers"
```

---

### Task 23: Pipeline Core Service Worker

**Files:**
- Create: `src/OpenEye.PipelineCore/PipelineCoreWorker.cs`
- Modify: `src/OpenEye.PipelineCore/Program.cs`

**Step 1: Implement PipelineCoreWorker**

```csharp
// src/OpenEye.PipelineCore/PipelineCoreWorker.cs
using System.Text.Json;
using OpenEye.Abstractions;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;
using StackExchange.Redis;

namespace OpenEye.PipelineCore;

public class PipelineCoreWorker : BackgroundService
{
    private readonly IConfigRepository _configRepo;
    private readonly IRedisStreamPublisher _streamPublisher;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<PipelineCoreWorker> _logger;
    private readonly Dictionary<string, CameraPipeline> _pipelines = [];

    public PipelineCoreWorker(
        IConfigRepository configRepo,
        IRedisStreamPublisher streamPublisher,
        IConnectionMultiplexer redis,
        ILogger<PipelineCoreWorker> logger)
    {
        _configRepo = configRepo;
        _streamPublisher = streamPublisher;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializePipelinesAsync();
        await SubscribeToConfigChangesAsync(stoppingToken);
        await ConsumeDetectionsAsync(stoppingToken);
    }

    private async Task InitializePipelinesAsync()
    {
        var cameras = await _configRepo.GetCamerasAsync();
        var allRules = await _configRepo.GetRulesAsync();

        // Compute class filter
        var classLabels = allRules
            .Where(r => r.Condition.ClassLabel != null)
            .Select(r => r.Condition.ClassLabel!)
            .Distinct()
            .ToList();

        var db = _redis.GetDatabase();
        await db.StringSetAsync("config:class-filter", JsonSerializer.Serialize(classLabels));

        foreach (var camera in cameras.Where(c => c.Enabled))
        {
            var zones = await _configRepo.GetZonesAsync(camera.Id);
            var tripwires = await _configRepo.GetTripwiresAsync(camera.Id);
            var primitiveConfigs = await _configRepo.GetPrimitivesAsync(camera.Id);
            var cameraRules = allRules.Where(r => r.SourceId == camera.Id).ToList();

            var eventBus = new LocalEventBus();
            var pipeline = new CameraPipeline(
                sourceId: camera.Id,
                tracker: new ObjectTracker(),
                zoneEvaluator: new ZoneEvaluator(zones, tripwires),
                primitiveExtractor: new PrimitiveExtractor(primitiveConfigs),
                ruleEngine: new RuleEngine(cameraRules),
                eventBus: eventBus);

            _pipelines[camera.Id] = pipeline;

            // Forward events from local bus to Redis events stream
            _ = ForwardEventsAsync(eventBus, camera.Id);

            _logger.LogInformation("Initialized pipeline for camera {CameraId}", camera.Id);
        }
    }

    private async Task ForwardEventsAsync(LocalEventBus eventBus, string cameraId)
    {
        await foreach (var evt in eventBus.Subscribe())
        {
            await _streamPublisher.PublishAsync("events", evt);
        }
    }

    private async Task SubscribeToConfigChangesAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();
        await subscriber.SubscribeAsync(RedisChannel.Literal("config:changed"), async (_, _) =>
        {
            _logger.LogInformation("Config change detected, reinitializing pipelines");
            _pipelines.Clear();
            await InitializePipelinesAsync();
        });
    }

    private async Task ConsumeDetectionsAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var (cameraId, pipeline) in _pipelines)
            {
                try
                {
                    var streamKey = $"detections:{cameraId}";

                    // Ensure consumer group exists
                    try { await db.StreamCreateConsumerGroupAsync(streamKey, "pipeline-core", "0", true); }
                    catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP")) { }

                    var entries = await db.StreamReadGroupAsync(
                        streamKey, "pipeline-core", "worker-1", count: 1);

                    foreach (var entry in entries)
                    {
                        var data = entry["data"];
                        if (data.IsNullOrEmpty) continue;

                        var msg = JsonSerializer.Deserialize<DetectionMessage>(data.ToString());
                        if (msg?.Detections != null)
                        {
                            pipeline.ProcessFrame(msg.Detections, msg.Timestamp);
                        }

                        await db.StreamAcknowledgeAsync(streamKey, "pipeline-core", entry.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing detections for camera {CameraId}", cameraId);
                }
            }

            await Task.Delay(10, stoppingToken);
        }
    }
}

public record DetectionMessage(long FrameIndex, DateTimeOffset Timestamp, List<Detection> Detections);
```

**Step 2: Wire up Program.cs**

```csharp
// src/OpenEye.PipelineCore/Program.cs
using OpenEye.Abstractions;
using OpenEye.PipelineCore;
using OpenEye.Shared.Data;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redisConn = builder.Configuration.GetConnectionString("redis") ?? "localhost";
var redis = ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<IRedisStreamPublisher, RedisStreamPublisher>();

var dbConn = builder.Configuration.GetConnectionString("openeye") ?? "Host=localhost;Database=openeye";
builder.Services.AddSingleton<IConfigRepository>(new ConfigRepository(dbConn));

builder.Services.AddHostedService<PipelineCoreWorker>();

var host = builder.Build();
await host.RunAsync();
```

**Step 3: Build to verify**

```bash
dotnet build src/OpenEye.PipelineCore
```
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add pipeline core service worker with config loading, detection consumption, and event forwarding"
```

---

## Phase 10: Dashboard

### Task 24: Next.js Dashboard Scaffolding

**Files:**
- Create: `dashboard/` (Next.js project)
- Create: `dashboard/prisma/schema.prisma`

**Step 1: Create Next.js project**

```bash
cd /c/Repos/openeye && npx create-next-app@latest dashboard --typescript --tailwind --eslint --app --src-dir --no-import-alias
```

**Step 2: Install dependencies**

```bash
cd /c/Repos/openeye/dashboard && npm install prisma @prisma/client
```

**Step 3: Create Prisma schema**

```prisma
// dashboard/prisma/schema.prisma
generator client {
  provider = "prisma-client-js"
}

datasource db {
  provider = "postgresql"
  url      = env("DATABASE_URL")
}

model Camera {
  id        String   @id
  name      String
  streamUrl String   @map("stream_url")
  type      String   @default("rtsp")
  targetFps Int      @default(5) @map("target_fps")
  enabled   Boolean  @default(true)
  createdAt DateTime @default(now()) @map("created_at")
  updatedAt DateTime @updatedAt @map("updated_at")

  zones      Zone[]
  tripwires  Tripwire[]
  primitives PrimitiveConfig[]
  rules      RuleConfig[]

  @@map("cameras")
}

model Zone {
  zoneId    String   @id @map("zone_id")
  sourceId  String   @map("source_id")
  polygon   Json
  createdAt DateTime @default(now()) @map("created_at")

  camera Camera @relation(fields: [sourceId], references: [id], onDelete: Cascade)

  @@map("zones")
}

model Tripwire {
  tripwireId String   @id @map("tripwire_id")
  sourceId   String   @map("source_id")
  startPoint Json     @map("start_point")
  endPoint   Json     @map("end_point")
  createdAt  DateTime @default(now()) @map("created_at")

  camera Camera @relation(fields: [sourceId], references: [id], onDelete: Cascade)

  @@map("tripwires")
}

model PrimitiveConfig {
  name       String  @id
  type       String
  sourceId   String  @map("source_id")
  zoneId     String? @map("zone_id")
  classLabel String  @map("class_label")
  tripwireId String? @map("tripwire_id")
  createdAt  DateTime @default(now()) @map("created_at")

  camera Camera @relation(fields: [sourceId], references: [id], onDelete: Cascade)

  @@map("primitive_configs")
}

model RuleConfig {
  ruleId                 String  @id @map("rule_id")
  name                   String
  sourceId               String  @map("source_id")
  zoneId                 String? @map("zone_id")
  conditionType          String  @map("condition_type")
  conditionClassLabel    String? @map("condition_class_label")
  conditionPrimitiveName String? @map("condition_primitive_name")
  conditionThreshold     Float?  @map("condition_threshold")
  temporalMode           String? @map("temporal_mode")
  temporalSeconds        Float?  @map("temporal_seconds")
  temporalMinOccurrences Int?    @default(1) @map("temporal_min_occurrences")
  evidenceType           String? @map("evidence_type")
  evidencePreSeconds     Float?  @default(10) @map("evidence_pre_seconds")
  evidencePostSeconds    Float?  @default(5) @map("evidence_post_seconds")
  cooldownSeconds        Float   @default(60) @map("cooldown_seconds")
  enabled                Boolean @default(true)
  createdAt              DateTime @default(now()) @map("created_at")
  updatedAt              DateTime @updatedAt @map("updated_at")

  camera        Camera               @relation(fields: [sourceId], references: [id], onDelete: Cascade)
  notifications NotificationConfig[]

  @@map("rule_configs")
}

model NotificationConfig {
  id          Int      @id @default(autoincrement())
  ruleId      String   @map("rule_id")
  channelType String   @map("channel_type")
  url         String?
  phone       String?
  email       String?
  createdAt   DateTime @default(now()) @map("created_at")

  rule RuleConfig @relation(fields: [ruleId], references: [ruleId], onDelete: Cascade)

  @@map("notification_configs")
}

model Event {
  eventId        String   @id @map("event_id")
  eventType      String   @map("event_type")
  timestamp      DateTime
  sourceId       String   @map("source_id")
  zoneId         String?  @map("zone_id")
  ruleId         String   @map("rule_id")
  trackedObjects Json     @map("tracked_objects")
  metadata       Json?
  evidenceUrl    String?  @map("evidence_url")
  createdAt      DateTime @default(now()) @map("created_at")

  @@map("events")
}
```

**Step 4: Generate Prisma client and create DB lib**

```bash
cd /c/Repos/openeye/dashboard && npx prisma generate
```

**Step 5: Create Prisma client singleton**

```typescript
// dashboard/src/lib/db.ts
import { PrismaClient } from '@prisma/client'

const globalForPrisma = globalThis as unknown as { prisma: PrismaClient }

export const prisma = globalForPrisma.prisma || new PrismaClient()

if (process.env.NODE_ENV !== 'production') globalForPrisma.prisma = prisma
```

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: scaffold Next.js dashboard with Prisma schema matching PostgreSQL tables"
```

---

### Task 25: Dashboard API Routes

**Files:**
- Create: `dashboard/src/app/api/cameras/route.ts`
- Create: `dashboard/src/app/api/zones/route.ts`
- Create: `dashboard/src/app/api/rules/route.ts`
- Create: `dashboard/src/app/api/events/route.ts`
- Create: `dashboard/src/lib/redis.ts`

**Step 1: Create Redis client for config notifications**

```typescript
// dashboard/src/lib/redis.ts
import { createClient } from 'redis'

export async function publishConfigChanged() {
  const client = createClient({ url: process.env.REDIS_URL || 'redis://localhost:6379' })
  await client.connect()
  await client.publish('config:changed', 'reload')
  await client.disconnect()
}
```

Install redis client:
```bash
cd /c/Repos/openeye/dashboard && npm install redis
```

**Step 2: Create cameras API route**

```typescript
// dashboard/src/app/api/cameras/route.ts
import { prisma } from '@/lib/db'
import { NextRequest, NextResponse } from 'next/server'
import { publishConfigChanged } from '@/lib/redis'

export async function GET() {
  const cameras = await prisma.camera.findMany({ orderBy: { createdAt: 'desc' } })
  return NextResponse.json(cameras)
}

export async function POST(req: NextRequest) {
  const body = await req.json()
  const camera = await prisma.camera.create({ data: body })
  await publishConfigChanged()
  return NextResponse.json(camera, { status: 201 })
}
```

**Step 3: Create zones API route**

```typescript
// dashboard/src/app/api/zones/route.ts
import { prisma } from '@/lib/db'
import { NextRequest, NextResponse } from 'next/server'
import { publishConfigChanged } from '@/lib/redis'

export async function GET(req: NextRequest) {
  const sourceId = req.nextUrl.searchParams.get('sourceId')
  const zones = await prisma.zone.findMany({
    where: sourceId ? { sourceId } : undefined,
    orderBy: { createdAt: 'desc' }
  })
  return NextResponse.json(zones)
}

export async function POST(req: NextRequest) {
  const body = await req.json()
  const zone = await prisma.zone.create({ data: body })
  await publishConfigChanged()
  return NextResponse.json(zone, { status: 201 })
}
```

**Step 4: Create rules and events API routes**

```typescript
// dashboard/src/app/api/rules/route.ts
import { prisma } from '@/lib/db'
import { NextRequest, NextResponse } from 'next/server'
import { publishConfigChanged } from '@/lib/redis'

export async function GET() {
  const rules = await prisma.ruleConfig.findMany({
    include: { notifications: true },
    orderBy: { createdAt: 'desc' }
  })
  return NextResponse.json(rules)
}

export async function POST(req: NextRequest) {
  const body = await req.json()
  const rule = await prisma.ruleConfig.create({ data: body })
  await publishConfigChanged()
  return NextResponse.json(rule, { status: 201 })
}
```

```typescript
// dashboard/src/app/api/events/route.ts
import { prisma } from '@/lib/db'
import { NextRequest, NextResponse } from 'next/server'

export async function GET(req: NextRequest) {
  const params = req.nextUrl.searchParams
  const where: Record<string, unknown> = {}

  if (params.get('sourceId')) where.sourceId = params.get('sourceId')
  if (params.get('ruleId')) where.ruleId = params.get('ruleId')
  if (params.get('from') || params.get('to')) {
    where.timestamp = {}
    if (params.get('from')) (where.timestamp as Record<string, unknown>).gte = new Date(params.get('from')!)
    if (params.get('to')) (where.timestamp as Record<string, unknown>).lte = new Date(params.get('to')!)
  }

  const limit = parseInt(params.get('limit') || '50')
  const offset = parseInt(params.get('offset') || '0')

  const events = await prisma.event.findMany({
    where,
    orderBy: { timestamp: 'desc' },
    take: limit,
    skip: offset
  })
  return NextResponse.json(events)
}
```

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add dashboard API routes for cameras, zones, rules, and events"
```

---

### Task 26: Dashboard Pages

**Files:**
- Modify: `dashboard/src/app/page.tsx`
- Create: `dashboard/src/app/cameras/page.tsx`
- Create: `dashboard/src/app/events/page.tsx`
- Create: `dashboard/src/app/rules/page.tsx`
- Create: `dashboard/src/components/Sidebar.tsx`

**Step 1: Create sidebar navigation component**

```tsx
// dashboard/src/components/Sidebar.tsx
'use client'
import Link from 'next/link'
import { usePathname } from 'next/navigation'

const navItems = [
  { href: '/', label: 'Dashboard' },
  { href: '/cameras', label: 'Cameras' },
  { href: '/zones', label: 'Zones & Primitives' },
  { href: '/rules', label: 'Rules' },
  { href: '/notifications', label: 'Notifications' },
  { href: '/events', label: 'Events (Live)' },
  { href: '/events/history', label: 'Events (History)' },
  { href: '/settings', label: 'Settings' },
]

export default function Sidebar() {
  const pathname = usePathname()

  return (
    <aside className="w-64 bg-gray-900 text-white min-h-screen p-4">
      <h1 className="text-xl font-bold mb-8">OpenEye</h1>
      <nav className="space-y-1">
        {navItems.map(item => (
          <Link
            key={item.href}
            href={item.href}
            className={`block px-3 py-2 rounded text-sm ${
              pathname === item.href ? 'bg-gray-700 text-white' : 'text-gray-300 hover:bg-gray-800'
            }`}
          >
            {item.label}
          </Link>
        ))}
      </nav>
    </aside>
  )
}
```

**Step 2: Update root layout to include sidebar**

```tsx
// dashboard/src/app/layout.tsx
import type { Metadata } from 'next'
import './globals.css'
import Sidebar from '@/components/Sidebar'

export const metadata: Metadata = {
  title: 'OpenEye Dashboard',
  description: 'Video analytics configuration and monitoring',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="flex">
        <Sidebar />
        <main className="flex-1 p-8 bg-gray-50 min-h-screen">{children}</main>
      </body>
    </html>
  )
}
```

**Step 3: Create cameras page**

```tsx
// dashboard/src/app/cameras/page.tsx
import { prisma } from '@/lib/db'

export default async function CamerasPage() {
  const cameras = await prisma.camera.findMany({ orderBy: { createdAt: 'desc' } })

  return (
    <div>
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-bold">Cameras</h1>
        <button className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">
          Add Camera
        </button>
      </div>

      <div className="grid gap-4">
        {cameras.map(camera => (
          <div key={camera.id} className="bg-white p-4 rounded shadow">
            <div className="flex justify-between items-center">
              <div>
                <h2 className="font-semibold">{camera.name}</h2>
                <p className="text-sm text-gray-500">{camera.streamUrl}</p>
                <p className="text-xs text-gray-400">{camera.type} · {camera.targetFps} FPS</p>
              </div>
              <span className={`px-2 py-1 rounded text-xs ${camera.enabled ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}`}>
                {camera.enabled ? 'Active' : 'Disabled'}
              </span>
            </div>
          </div>
        ))}
        {cameras.length === 0 && (
          <p className="text-gray-500 text-center py-8">No cameras configured. Add one to get started.</p>
        )}
      </div>
    </div>
  )
}
```

**Step 4: Create events page**

```tsx
// dashboard/src/app/events/page.tsx
import { prisma } from '@/lib/db'

export default async function EventsPage() {
  const events = await prisma.event.findMany({
    orderBy: { timestamp: 'desc' },
    take: 50,
  })

  return (
    <div>
      <h1 className="text-2xl font-bold mb-6">Events (Live)</h1>
      <div className="bg-white rounded shadow overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-3 text-left">Time</th>
              <th className="px-4 py-3 text-left">Type</th>
              <th className="px-4 py-3 text-left">Camera</th>
              <th className="px-4 py-3 text-left">Zone</th>
              <th className="px-4 py-3 text-left">Rule</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {events.map(event => (
              <tr key={event.eventId} className="hover:bg-gray-50">
                <td className="px-4 py-3">{new Date(event.timestamp).toLocaleString()}</td>
                <td className="px-4 py-3 font-medium">{event.eventType}</td>
                <td className="px-4 py-3">{event.sourceId}</td>
                <td className="px-4 py-3">{event.zoneId || '—'}</td>
                <td className="px-4 py-3">{event.ruleId}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {events.length === 0 && (
          <p className="text-gray-500 text-center py-8">No events yet.</p>
        )}
      </div>
    </div>
  )
}
```

**Step 5: Create rules page**

```tsx
// dashboard/src/app/rules/page.tsx
import { prisma } from '@/lib/db'

export default async function RulesPage() {
  const rules = await prisma.ruleConfig.findMany({
    include: { notifications: true },
    orderBy: { createdAt: 'desc' },
  })

  return (
    <div>
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-bold">Rules</h1>
        <button className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">
          Add Rule
        </button>
      </div>

      <div className="grid gap-4">
        {rules.map(rule => (
          <div key={rule.ruleId} className="bg-white p-4 rounded shadow">
            <div className="flex justify-between items-center">
              <div>
                <h2 className="font-semibold">{rule.name}</h2>
                <p className="text-sm text-gray-500">
                  {rule.conditionType}
                  {rule.conditionClassLabel ? ` · ${rule.conditionClassLabel}` : ''}
                  {rule.conditionThreshold != null ? ` > ${rule.conditionThreshold}` : ''}
                </p>
                {rule.temporalMode && (
                  <p className="text-xs text-gray-400">{rule.temporalMode}: {rule.temporalSeconds}s</p>
                )}
              </div>
              <span className={`px-2 py-1 rounded text-xs ${rule.enabled ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}`}>
                {rule.enabled ? 'Active' : 'Disabled'}
              </span>
            </div>
          </div>
        ))}
        {rules.length === 0 && (
          <p className="text-gray-500 text-center py-8">No rules configured.</p>
        )}
      </div>
    </div>
  )
}
```

**Step 6: Update home page**

```tsx
// dashboard/src/app/page.tsx
export default function Home() {
  return (
    <div>
      <h1 className="text-2xl font-bold mb-6">Dashboard</h1>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="bg-white p-6 rounded shadow">
          <h2 className="text-sm font-medium text-gray-500 mb-1">Active Cameras</h2>
          <p className="text-3xl font-bold">—</p>
        </div>
        <div className="bg-white p-6 rounded shadow">
          <h2 className="text-sm font-medium text-gray-500 mb-1">Active Rules</h2>
          <p className="text-3xl font-bold">—</p>
        </div>
        <div className="bg-white p-6 rounded shadow">
          <h2 className="text-sm font-medium text-gray-500 mb-1">Events Today</h2>
          <p className="text-3xl font-bold">—</p>
        </div>
      </div>
    </div>
  )
}
```

**Step 7: Commit**

```bash
git add -A
git commit -m "feat: add dashboard pages for cameras, rules, events with sidebar navigation"
```

---

## Phase 11: Docker & Integration

### Task 27: Docker Compose and Dockerfiles

**Files:**
- Create: `docker/docker-compose.yml`
- Create: `docker/Dockerfile.dotnet`
- Create: `docker/Dockerfile.dashboard`

**Step 1: Create .NET multi-stage Dockerfile**

```dockerfile
# docker/Dockerfile.dotnet
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ .
RUN dotnet restore OpenEye.slnx
RUN dotnet publish OpenEye.slnx -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
```

**Step 2: Create dashboard Dockerfile**

```dockerfile
# docker/Dockerfile.dashboard
FROM node:22-alpine AS build
WORKDIR /app
COPY dashboard/package*.json ./
RUN npm ci
COPY dashboard/ .
RUN npx prisma generate
RUN npm run build

FROM node:22-alpine
WORKDIR /app
COPY --from=build /app/.next ./.next
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/package.json ./
COPY --from=build /app/prisma ./prisma
EXPOSE 3000
CMD ["npm", "start"]
```

**Step 3: Create Docker Compose file**

```yaml
# docker/docker-compose.yml
services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: openeye
      POSTGRES_USER: openeye
      POSTGRES_PASSWORD: openeye
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ../src/OpenEye.Shared/Data/schema.sql:/docker-entrypoint-initdb.d/01-schema.sql

  roboflow-inference:
    image: roboflow/roboflow-inference-server-cpu:latest
    ports:
      - "9001:9001"
    environment:
      ROBOFLOW_API_KEY: ${ROBOFLOW_API_KEY:-}

  frame-capture:
    build:
      context: ..
      dockerfile: docker/Dockerfile.dotnet
    entrypoint: ["dotnet", "OpenEye.FrameCapture.dll"]
    depends_on: [redis, postgres]
    environment:
      ConnectionStrings__redis: redis:6379
      ConnectionStrings__openeye: Host=postgres;Database=openeye;Username=openeye;Password=openeye
      FrameOutputDir: /frames
    volumes:
      - frames:/frames

  detection-bridge:
    build:
      context: ..
      dockerfile: docker/Dockerfile.dotnet
    entrypoint: ["dotnet", "OpenEye.DetectionBridge.dll"]
    depends_on: [redis, roboflow-inference]
    environment:
      ConnectionStrings__redis: redis:6379
      InferenceUrl: http://roboflow-inference:9001
      ModelId: yolov8n-640
      ConfidenceThreshold: "0.5"
    volumes:
      - frames:/frames

  pipeline-core:
    build:
      context: ..
      dockerfile: docker/Dockerfile.dotnet
    entrypoint: ["dotnet", "OpenEye.PipelineCore.dll"]
    depends_on: [redis, postgres]
    environment:
      ConnectionStrings__redis: redis:6379
      ConnectionStrings__openeye: Host=postgres;Database=openeye;Username=openeye;Password=openeye

  event-router:
    build:
      context: ..
      dockerfile: docker/Dockerfile.dotnet
    entrypoint: ["dotnet", "OpenEye.EventRouter.dll"]
    depends_on: [redis, postgres]
    environment:
      ConnectionStrings__redis: redis:6379
      ConnectionStrings__openeye: Host=postgres;Database=openeye;Username=openeye;Password=openeye

  dashboard:
    build:
      context: ..
      dockerfile: docker/Dockerfile.dashboard
    ports:
      - "3000:3000"
    depends_on: [postgres, redis]
    environment:
      DATABASE_URL: postgresql://openeye:openeye@postgres:5432/openeye
      REDIS_URL: redis://redis:6379

volumes:
  redis-data:
  pgdata:
  frames:
  evidence:
```

**Step 4: Build to verify**

```bash
dotnet build src/OpenEye.slnx
```
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add Docker Compose with all services, PostgreSQL, Redis, and Roboflow Inference"
```

---

### Task 28: Integration Tests — Full Pipeline Scenarios

**Files:**
- Create: `tests/OpenEye.IntegrationTests/LoiteringScenarioTests.cs`
- Create: `tests/OpenEye.IntegrationTests/QueueAlertScenarioTests.cs`
- Create: `tests/OpenEye.IntegrationTests/TripwireScenarioTests.cs`

**Step 1: Write loitering scenario test**

```csharp
// tests/OpenEye.IntegrationTests/LoiteringScenarioTests.cs
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.IntegrationTests;

/// <summary>
/// Scenario: A person enters a restricted zone and stays for more than 30 seconds.
/// Expected: A "loitering" event fires after 30 seconds.
/// </summary>
public class LoiteringScenarioTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Person_LoiteringInZone_FiresAfter30Seconds()
    {
        // Setup: zone, rule with 30s duration threshold
        var zone = new Zone("restricted", "cam-1", new[]
        {
            new Point2D(0.2, 0.2), new Point2D(0.8, 0.2),
            new Point2D(0.8, 0.8), new Point2D(0.2, 0.8)
        });

        var rule = new RuleConfig
        {
            RuleId = "loiter-rule", Name = "loitering",
            SourceId = "cam-1", ZoneId = "restricted",
            Condition = new RuleCondition { Type = "duration", ClassLabel = "person", Threshold = 30 },
            Cooldown = TimeSpan.FromSeconds(120)
        };

        var eventBus = new LocalEventBus();
        var pipeline = new CameraPipeline("cam-1",
            new ObjectTracker(), new ZoneEvaluator([zone], []),
            new PrimitiveExtractor([]), new RuleEngine([rule]), eventBus);

        var allEvents = new List<Event>();

        // Simulate person detection at (0.5, 0.5) for 35 seconds at 5 FPS
        for (int sec = 0; sec <= 35; sec++)
        {
            var det = new Detection("person", new BoundingBox(0.45, 0.45, 0.1, 0.1), 0.9,
                T0.AddSeconds(sec), "cam-1");
            var events = pipeline.ProcessFrame([det], T0.AddSeconds(sec));
            allEvents.AddRange(events);
        }

        // Should have fired exactly once (at ~31 seconds, cooldown prevents refire)
        Assert.Single(allEvents);
        Assert.Equal("loitering", allEvents[0].EventType);
        Assert.Equal("restricted", allEvents[0].ZoneId);
    }

    [Fact]
    public void Person_LeavesBeforeThreshold_NoEvent()
    {
        var zone = new Zone("restricted", "cam-1", new[]
        {
            new Point2D(0.2, 0.2), new Point2D(0.8, 0.2),
            new Point2D(0.8, 0.8), new Point2D(0.2, 0.8)
        });

        var rule = new RuleConfig
        {
            RuleId = "loiter-rule", Name = "loitering",
            SourceId = "cam-1", ZoneId = "restricted",
            Condition = new RuleCondition { Type = "duration", ClassLabel = "person", Threshold = 30 },
            Cooldown = TimeSpan.Zero
        };

        var pipeline = new CameraPipeline("cam-1",
            new ObjectTracker(), new ZoneEvaluator([zone], []),
            new PrimitiveExtractor([]), new RuleEngine([rule]), new LocalEventBus());

        var allEvents = new List<Event>();

        // Person in zone for 20 seconds
        for (int sec = 0; sec <= 20; sec++)
        {
            var det = new Detection("person", new BoundingBox(0.45, 0.45, 0.1, 0.1), 0.9,
                T0.AddSeconds(sec), "cam-1");
            allEvents.AddRange(pipeline.ProcessFrame([det], T0.AddSeconds(sec)));
        }

        // Person leaves zone
        for (int sec = 21; sec <= 40; sec++)
        {
            var det = new Detection("person", new BoundingBox(0.05, 0.05, 0.1, 0.1), 0.9,
                T0.AddSeconds(sec), "cam-1");
            allEvents.AddRange(pipeline.ProcessFrame([det], T0.AddSeconds(sec)));
        }

        Assert.Empty(allEvents);
    }
}
```

**Step 2: Write queue alert scenario test**

```csharp
// tests/OpenEye.IntegrationTests/QueueAlertScenarioTests.cs
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.IntegrationTests;

/// <summary>
/// Scenario: Queue at checkout grows beyond 5 people.
/// Expected: A "queue-too-long" event fires.
/// </summary>
public class QueueAlertScenarioTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void QueueExceedsThreshold_FiresAlert()
    {
        var zone = new Zone("checkout-queue", "cam-1", new[]
        {
            new Point2D(0.0, 0.0), new Point2D(1.0, 0.0),
            new Point2D(1.0, 1.0), new Point2D(0.0, 1.0)
        });

        var primitiveConfig = new PrimitiveConfig
        {
            Name = "queue_count", Type = "count",
            SourceId = "cam-1", ZoneId = "checkout-queue", ClassLabel = "person"
        };

        var rule = new RuleConfig
        {
            RuleId = "queue-alert", Name = "queue-too-long",
            SourceId = "cam-1",
            Condition = new RuleCondition { Type = "value_gt", PrimitiveName = "queue_count", Threshold = 5 },
            Cooldown = TimeSpan.FromSeconds(300)
        };

        var pipeline = new CameraPipeline("cam-1",
            new ObjectTracker(), new ZoneEvaluator([zone], []),
            new PrimitiveExtractor([primitiveConfig]), new RuleEngine([rule]), new LocalEventBus());

        // Generate 6 people spread across the zone
        var detections = Enumerable.Range(0, 6).Select(i =>
            new Detection("person",
                new BoundingBox(0.1 + i * 0.13, 0.4, 0.1, 0.2), 0.9, T0, "cam-1")
        ).ToList();

        // Frame 1: establish tracks
        pipeline.ProcessFrame(detections, T0);

        // Frame 2: zone presences established, count should trigger
        var events = pipeline.ProcessFrame(
            detections.Select(d => d with { Timestamp = T0.AddSeconds(1) }).ToList(),
            T0.AddSeconds(1));

        Assert.Single(events);
        Assert.Equal("queue-too-long", events[0].EventType);
    }
}
```

**Step 3: Write tripwire scenario test**

```csharp
// tests/OpenEye.IntegrationTests/TripwireScenarioTests.cs
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;
using OpenEye.Shared.Models.Config;

namespace OpenEye.IntegrationTests;

/// <summary>
/// Scenario: A person crosses a tripwire line.
/// Expected: A "boundary-crossed" event fires.
/// </summary>
public class TripwireScenarioTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Person_CrossesTripwire_FiresEvent()
    {
        var tripwire = new Tripwire("entry-line", "cam-1",
            new Point2D(0.5, 0.0), new Point2D(0.5, 1.0));

        var primitiveConfig = new PrimitiveConfig
        {
            Name = "entry_crossed", Type = "line_crossed",
            SourceId = "cam-1", ZoneId = "", ClassLabel = "person",
            TripwireId = "entry-line"
        };

        var rule = new RuleConfig
        {
            RuleId = "crossing-rule", Name = "boundary-crossed",
            SourceId = "cam-1",
            Condition = new RuleCondition { Type = "value_eq", PrimitiveName = "entry_crossed", Threshold = 1 },
            Cooldown = TimeSpan.FromSeconds(10)
        };

        var pipeline = new CameraPipeline("cam-1",
            new ObjectTracker(), new ZoneEvaluator([], [tripwire]),
            new PrimitiveExtractor([primitiveConfig]), new RuleEngine([rule]), new LocalEventBus());

        var allEvents = new List<Event>();

        // Person moves from left (x=0.3) to right (x=0.7) crossing x=0.5 tripwire
        for (int i = 0; i < 5; i++)
        {
            double x = 0.3 + i * 0.1; // 0.3, 0.4, 0.5, 0.6, 0.7
            var det = new Detection("person", new BoundingBox(x - 0.05, 0.45, 0.1, 0.1), 0.9,
                T0.AddSeconds(i), "cam-1");
            allEvents.AddRange(pipeline.ProcessFrame([det], T0.AddSeconds(i)));
        }

        Assert.Single(allEvents);
        Assert.Equal("boundary-crossed", allEvents[0].EventType);
    }
}
```

**Step 4: Run all tests**

```bash
dotnet test tests/ -v minimal
```
Expected: All unit tests and integration tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "test: add integration tests for loitering, queue alert, and tripwire scenarios"
```

---

### Task 29: Full Solution Build Verification and .gitignore

**Files:**
- Modify: `.gitignore`

**Step 1: Update .gitignore for full project**

```
# .gitignore
.claude/

# .NET
bin/
obj/
*.user
*.suo
*.userprefs
.vs/

# Node
node_modules/
.next/
dashboard/.env
dashboard/.env.local

# Docker
.env

# Frames and evidence (runtime data)
frames/
evidence/
```

**Step 2: Run full build**

```bash
dotnet build src/OpenEye.slnx
```
Expected: Build succeeded with 0 errors.

**Step 3: Run all tests**

```bash
dotnet test tests/ -v minimal
```
Expected: All tests PASS.

**Step 4: Commit**

```bash
git add -A
git commit -m "chore: update .gitignore and verify full solution builds cleanly"
```

---

## Summary

| Phase | Tasks | Description |
|-------|-------|-------------|
| 1 | 1–2 | Project scaffolding, Aspire setup |
| 2 | 3–5 | Shared models, config models, interfaces |
| 3 | 6–8 | Object tracker (IoU, matcher, SORT) |
| 4 | 9–10 | Zone evaluator (geometry, transitions, tripwire) |
| 5 | 11 | Primitive extractor |
| 6 | 12–15 | Rule engine (ring buffer, state store, conditions, temporal) |
| 7 | 16–17 | Event bus, camera pipeline orchestrator |
| 8 | 18–19 | Redis streams, PostgreSQL schema & repositories |
| 9 | 20–23 | Frame capture, detection bridge, event router, pipeline core worker |
| 10 | 24–26 | Dashboard scaffolding, API routes, pages |
| 11 | 27–29 | Docker Compose, integration tests, final verification |

**Total: 29 tasks, ~145 steps**

All pipeline core algorithms (Tasks 6–17) are fully TDD with tests written before implementation. Service wiring (Tasks 18–23) and dashboard (Tasks 24–26) follow a build-and-verify pattern since they require external infrastructure for meaningful testing.
