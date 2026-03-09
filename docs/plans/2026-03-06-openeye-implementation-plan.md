# OpenEye Platform Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a multi-service video analytics platform that converts RTSP/MJPEG camera streams into actionable events using configurable rules with semantic primitives, a Feature Store for reusable computed metrics, plugin-based rule conditions, and a visual Rule Builder dashboard.

**Architecture:** Microservices communicating via Redis Streams — Frame Capture → Detection Bridge → Pipeline Core → Event Router, with a Next.js dashboard for configuration, monitoring, and visual rule building. Pipeline Core runs a staged pipeline: Object Tracker → Zone Evaluator → Feature Extraction → Feature Store → Primitive Extractor → Rule Engine (plugin-based conditions) → Event Publisher.

**Tech Stack:**
- Backend: .NET 10, C#, .NET Aspire, StackExchange.Redis, Npgsql/Dapper, OpenCvSharp4
- Frontend: Next.js (App Router), TypeScript, Tailwind CSS, Prisma
- Infrastructure: .NET Aspire (orchestration, service discovery, health checks, telemetry), Redis (Streams + pub/sub), PostgreSQL, Docker

**Design doc:** `docs/plans/2026-03-06-openeye-framework-design.md`

---

## Updated Architecture

### Pipeline Diagram

```
┌────────────────┐    ┌──────────────────┐    ┌───────────────────────────────────────────────────────────┐
│ Frame Capture   │───▶│ Detection Bridge  │───▶│                    Pipeline Core                         │
│ (C# Worker)     │    │ (C# Worker)       │    │                                                           │
└────────────────┘    └──────┬───────────┘    │  Detections                                               │
                              │                │      ↓                                                     │
                     ┌────────▼────────┐      │  ObjectTracker ──▶ ZoneEvaluator ──▶ FeatureExtractors    │
                     │   Roboflow      │      │                                           ↓                │
                     │   Inference     │      │                                      FeatureStore          │
                     └─────────────────┘      │                                           ↓                │
                                               │                                   PrimitiveExtractor      │
                                               │                                           ↓                │
                                               │                              RuleEngine (plugin conds)    │
                                               │                                           ↓                │
                                               │                                    EventPublisher         │
                                               └────────────────────────┬──────────────────────────────────┘
                                                                         │
                                                                         ▼
                                                                  ┌──────────────┐    ┌─────────────────┐
                                                                  │ Event Router  │───▶│   Dashboard     │
                                                                  │ (C# Worker)   │    │ (Next.js)       │
                                                                  └──────────────┘    │ + Rule Builder  │
                                                                                       └─────────────────┘
```

### Key Architectural Additions

**1. Feature Store Layer** — Computed metrics stored per-frame in a `FrameContext`. Feature extractors populate the store; primitives and rules consume from it. Avoids recomputing values across rules. Categories: object features (speed, direction, path_length, time_in_scene), zone features (occupancy, density, entry_rate, exit_rate), spatial features (distance_between, proximity_to_boundary), temporal features (dwell_time, time_since_last_seen).

**2. Plugin-Based Rule Conditions** — Each condition type (duration, count_above, line_cross, speed, presence, absence) implements `IRuleCondition`. A `ConditionRegistry` resolves by type string. New conditions can be added via DI without modifying the engine.

**3. Visual Rule Builder UI** — Drag-and-drop rule composition in the dashboard. Rules stored as structured JSON. Backend rule engine executes rules created by either the builder or direct JSON.

### Updated Project Structure

```
openeye/
├── src/
│   ├── OpenEye.slnx
│   ├── OpenEye.AppHost/
│   ├── OpenEye.ServiceDefaults/
│   ├── OpenEye.Shared/
│   │   ├── Models/              # Domain models (existing)
│   │   ├── Features/            # FeatureStore, FeatureKey (NEW)
│   │   ├── Redis/               # Redis stream helpers
│   │   └── Postgres/            # PostgreSQL data access
│   ├── OpenEye.Abstractions/    # Interfaces
│   ├── OpenEye.PipelineCore/
│   │   ├── Tracking/            # HungarianAlgorithm, SortTracker
│   │   ├── Zones/               # Geometry, DefaultZoneEvaluator
│   │   ├── Features/            # Feature extractors (NEW)
│   │   ├── Primitives/          # DefaultPrimitiveExtractor
│   │   ├── Rules/               # RuleEngine, ConditionRegistry, conditions (REFACTORED)
│   │   └── Pipeline/            # Orchestrator, FrameContext, worker
│   ├── OpenEye.FrameCapture/
│   ├── OpenEye.DetectionBridge/
│   ├── OpenEye.EventRouter/
│   ├── OpenEye.Tests/
│   └── OpenEye.IntegrationTests/
├── dashboard/
│   ├── src/app/                 # App Router pages
│   ├── src/components/          # React components
│   │   └── rule-builder/        # Visual Rule Builder (NEW)
│   └── prisma/
├── docker/
└── docs/plans/
```

### Redis Streams Topology (unchanged)

```
frames:{cameraId}        → frame-capture publishes, detection-bridge consumes
detections:{cameraId}    → detection-bridge publishes, pipeline-core consumes
events                   → pipeline-core publishes, event-router consumes
config:changed           → dashboard publishes, services subscribe (pub/sub)
config:class-filter      → pipeline-core publishes, detection-bridge reads (key-value)
```

---

## Phase 1: Foundation — New Models, Abstractions & Infrastructure Helpers

> Models, zone-related types, and basic tests already exist. This phase adds configuration models, Feature Store types, the plugin condition schema, and all abstractions.

### Task 1: Configuration & Feature Store Models

**Files:**
- Create: `src/OpenEye.Shared/Models/CameraConfig.cs`
- Create: `src/OpenEye.Shared/Models/PrimitiveConfig.cs`
- Create: `src/OpenEye.Shared/Models/RuleDefinition.cs`
- Create: `src/OpenEye.Shared/Models/ConditionConfig.cs`
- Create: `src/OpenEye.Shared/Models/NotificationConfig.cs`
- Create: `src/OpenEye.Shared/Features/FeatureKey.cs`
- Create: `src/OpenEye.Shared/Features/IFeatureStore.cs`
- Create: `src/OpenEye.Shared/Features/FeatureStore.cs`
- Create: `src/OpenEye.Shared/Features/FrameContext.cs`
- Test: `src/OpenEye.Tests/Features/FeatureStoreTests.cs`
- Test: `src/OpenEye.Tests/Features/FrameContextTests.cs`

**Step 1: Write FeatureStore tests**

```csharp
// src/OpenEye.Tests/Features/FeatureStoreTests.cs
using OpenEye.Shared.Features;

namespace OpenEye.Tests.Features;

public class FeatureStoreTests
{
    [Fact]
    public void Set_And_Get_GlobalFeature()
    {
        var store = new FeatureStore();
        store.Set("zone_occupancy", 5);
        Assert.Equal(5, store.Get<int>("zone_occupancy"));
    }

    [Fact]
    public void Set_And_Get_ObjectFeature()
    {
        var store = new FeatureStore();
        store.Set("object_speed", 2.5, "track-0");
        Assert.Equal(2.5, store.Get<double>("object_speed", "track-0"));
    }

    [Fact]
    public void Get_MissingFeature_ReturnsDefault()
    {
        var store = new FeatureStore();
        Assert.Equal(0.0, store.Get<double>("missing"));
    }

    [Fact]
    public void TryGet_ExistingFeature_ReturnsTrue()
    {
        var store = new FeatureStore();
        store.Set("speed", 3.0, "track-1");
        Assert.True(store.TryGet<double>("speed", "track-1", out var val));
        Assert.Equal(3.0, val);
    }

    [Fact]
    public void TryGet_MissingFeature_ReturnsFalse()
    {
        var store = new FeatureStore();
        Assert.False(store.TryGet<double>("missing", null, out _));
    }

    [Fact]
    public void ObjectFeatures_Are_Isolated()
    {
        var store = new FeatureStore();
        store.Set("speed", 1.0, "track-0");
        store.Set("speed", 2.0, "track-1");
        Assert.Equal(1.0, store.Get<double>("speed", "track-0"));
        Assert.Equal(2.0, store.Get<double>("speed", "track-1"));
    }

    [Fact]
    public void Clear_RemovesAllFeatures()
    {
        var store = new FeatureStore();
        store.Set("speed", 1.0, "track-0");
        store.Set("zone_occupancy", 5);
        store.Clear();
        Assert.Equal(0.0, store.Get<double>("speed", "track-0"));
        Assert.Equal(0, store.Get<int>("zone_occupancy"));
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~FeatureStoreTests" -v n
```

Expected: FAIL — `The type or namespace name 'Features' could not be found`

**Step 3: Create FeatureKey, IFeatureStore, and FeatureStore**

```csharp
// src/OpenEye.Shared/Features/FeatureKey.cs
namespace OpenEye.Shared.Features;

public readonly record struct FeatureKey(string Name, string? ObjectId);
```

```csharp
// src/OpenEye.Shared/Features/IFeatureStore.cs
namespace OpenEye.Shared.Features;

public interface IFeatureStore
{
    void Set<T>(string name, T value, string? objectId = null);
    T Get<T>(string name, string? objectId = null);
    bool TryGet<T>(string name, string? objectId, out T value);
    void Clear();
}
```

```csharp
// src/OpenEye.Shared/Features/FeatureStore.cs
namespace OpenEye.Shared.Features;

public class FeatureStore : IFeatureStore
{
    private readonly Dictionary<FeatureKey, object> _values = [];

    public void Set<T>(string name, T value, string? objectId = null)
    {
        _values[new FeatureKey(name, objectId)] = value!;
    }

    public T Get<T>(string name, string? objectId = null)
    {
        return _values.TryGetValue(new FeatureKey(name, objectId), out var val)
            ? (T)val
            : default!;
    }

    public bool TryGet<T>(string name, string? objectId, out T value)
    {
        if (_values.TryGetValue(new FeatureKey(name, objectId), out var val))
        {
            value = (T)val;
            return true;
        }
        value = default!;
        return false;
    }

    public void Clear() => _values.Clear();
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~FeatureStoreTests" -v n
```

Expected: PASS — 7 tests green

**Step 5: Create FrameContext**

```csharp
// src/OpenEye.Shared/Features/FrameContext.cs
using OpenEye.Shared.Models;

namespace OpenEye.Shared.Features;

public class FrameContext
{
    public required string SourceId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required IReadOnlyList<Detection> Detections { get; init; }
    public IReadOnlyList<TrackedObject> Tracks { get; set; } = [];
    public ZoneEvaluationResult? ZoneResult { get; set; }
    public IFeatureStore Features { get; } = new FeatureStore();
    public IReadOnlyList<Primitive> Primitives { get; set; } = [];
    public IReadOnlyList<Event> Events { get; set; } = [];
}
```

**Step 6: Write FrameContext test**

```csharp
// src/OpenEye.Tests/Features/FrameContextTests.cs
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Features;

public class FrameContextTests
{
    [Fact]
    public void FrameContext_HasIsolatedFeatureStore()
    {
        var ctx = new FrameContext
        {
            SourceId = "cam-1",
            Timestamp = DateTimeOffset.UtcNow,
            Detections = []
        };

        ctx.Features.Set("test", 42);
        Assert.Equal(42, ctx.Features.Get<int>("test"));
    }

    [Fact]
    public void FrameContext_DefaultCollections_AreEmpty()
    {
        var ctx = new FrameContext
        {
            SourceId = "cam-1",
            Timestamp = DateTimeOffset.UtcNow,
            Detections = []
        };

        Assert.Empty(ctx.Tracks);
        Assert.Null(ctx.ZoneResult);
        Assert.Empty(ctx.Primitives);
        Assert.Empty(ctx.Events);
    }
}
```

**Step 7: Run all feature tests to verify green**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~Features" -v n
```

Expected: PASS — 9 tests green

**Step 8: Create config and rule definition models**

```csharp
// src/OpenEye.Shared/Models/CameraConfig.cs
namespace OpenEye.Shared.Models;

public record CameraConfig(
    string Id,
    string Name,
    string StreamUrl,
    string Type,
    int TargetFps,
    bool Enabled);
```

```csharp
// src/OpenEye.Shared/Models/PrimitiveConfig.cs
namespace OpenEye.Shared.Models;

public enum PrimitiveType { Presence, Absence, Count, ZoneDuration, Speed, LineCrossed }

public record PrimitiveConfig(
    string Name,
    PrimitiveType Type,
    string ClassLabel,
    string? ZoneId,
    string? TripwireId,
    string SourceId);
```

```csharp
// src/OpenEye.Shared/Models/ConditionConfig.cs
namespace OpenEye.Shared.Models;

public record ConditionConfig(
    string Type,
    string? Operator = null,
    double? Value = null,
    string? FeatureName = null);
```

```csharp
// src/OpenEye.Shared/Models/RuleDefinition.cs
namespace OpenEye.Shared.Models;

public record RuleDefinition(
    string RuleId,
    string Name,
    string ObjectClass,
    string? ZoneId,
    string? TripwireId,
    IReadOnlyList<ConditionConfig> Conditions,
    string Action,
    TimeSpan? Sustained = null,
    TimeSpan? Within = null,
    int? MinOccurrences = null,
    TimeSpan? Cooldown = null,
    EvidenceType? EvidenceType = null);
```

```csharp
// src/OpenEye.Shared/Models/NotificationConfig.cs
namespace OpenEye.Shared.Models;

public record NotificationChannel(
    string Type,
    Dictionary<string, string> Config);

public record NotificationConfig(
    string RuleId,
    IReadOnlyList<NotificationChannel> Channels);
```

**Step 9: Verify full build**

```bash
dotnet build src/OpenEye.slnx
```

Expected: Build succeeded. 0 Warning(s) 0 Error(s)

**Step 10: Commit**

```
feat: add Feature Store, FrameContext, config models, and rule definition schema
```

---

### Task 2: Abstractions — Pipeline, Feature & Plugin Interfaces

**Files:**
- Create: `src/OpenEye.Abstractions/IObjectTracker.cs`
- Create: `src/OpenEye.Abstractions/IZoneEvaluator.cs`
- Create: `src/OpenEye.Abstractions/IFeatureExtractor.cs`
- Create: `src/OpenEye.Abstractions/IPrimitiveExtractor.cs`
- Create: `src/OpenEye.Abstractions/IRuleCondition.cs`
- Create: `src/OpenEye.Abstractions/IConditionRegistry.cs`
- Create: `src/OpenEye.Abstractions/IRuleEngine.cs`
- Create: `src/OpenEye.Abstractions/IRuleStateStore.cs`
- Create: `src/OpenEye.Abstractions/IEventPublisher.cs`
- Create: `src/OpenEye.Abstractions/IGlobalEventBus.cs`
- Create: `src/OpenEye.Abstractions/IConfigProvider.cs`
- Create: `src/OpenEye.Abstractions/IFramePublisher.cs`
- Create: `src/OpenEye.Abstractions/IDetectionClient.cs`
- Create: `src/OpenEye.Abstractions/INotificationDispatcher.cs`
- Create: `src/OpenEye.Abstractions/IEvidenceProvider.cs`

**Step 1: Create pipeline stage interfaces**

```csharp
// src/OpenEye.Abstractions/IObjectTracker.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IObjectTracker
{
    IReadOnlyList<TrackedObject> Update(IReadOnlyList<Detection> detections, DateTimeOffset timestamp);
    IReadOnlyList<TrackedObject> ActiveTracks { get; }
    void Reset();
}
```

```csharp
// src/OpenEye.Abstractions/IZoneEvaluator.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IZoneEvaluator
{
    ZoneEvaluationResult Evaluate(
        IReadOnlyList<TrackedObject> tracks,
        IReadOnlyList<Zone> zones,
        IReadOnlyList<Tripwire> tripwires);
}
```

```csharp
// src/OpenEye.Abstractions/IFeatureExtractor.cs
using OpenEye.Shared.Features;

namespace OpenEye.Abstractions;

public interface IFeatureExtractor
{
    void Update(FrameContext context);
}
```

```csharp
// src/OpenEye.Abstractions/IPrimitiveExtractor.cs
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IPrimitiveExtractor
{
    IReadOnlyList<Primitive> Extract(FrameContext context, IReadOnlyList<PrimitiveConfig> configs);
}
```

**Step 2: Create plugin-based rule condition interfaces**

```csharp
// src/OpenEye.Abstractions/IRuleCondition.cs
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IRuleCondition
{
    string Type { get; }
    bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj);
}
```

```csharp
// src/OpenEye.Abstractions/IConditionRegistry.cs
namespace OpenEye.Abstractions;

public interface IConditionRegistry
{
    IRuleCondition Get(string type);
    void Register(IRuleCondition condition);
}
```

```csharp
// src/OpenEye.Abstractions/IRuleEngine.cs
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IRuleEngine
{
    IReadOnlyList<Event> Evaluate(FrameContext context, IReadOnlyList<RuleDefinition> rules);
}
```

**Step 3: Create infrastructure interfaces**

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
}
```

```csharp
// src/OpenEye.Abstractions/IEventPublisher.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync(Event evt, CancellationToken ct = default);
}
```

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
// src/OpenEye.Abstractions/IConfigProvider.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IConfigProvider
{
    Task<IReadOnlyList<CameraConfig>> GetCamerasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Zone>> GetZonesAsync(string? sourceId = null, CancellationToken ct = default);
    Task<IReadOnlyList<Tripwire>> GetTripwiresAsync(string? sourceId = null, CancellationToken ct = default);
    Task<IReadOnlyList<PrimitiveConfig>> GetPrimitivesAsync(string? sourceId = null, CancellationToken ct = default);
    Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NotificationConfig>> GetNotificationsAsync(string? ruleId = null, CancellationToken ct = default);
}
```

```csharp
// src/OpenEye.Abstractions/IFramePublisher.cs
namespace OpenEye.Abstractions;

public interface IFramePublisher
{
    Task PublishFrameAsync(string cameraId, long frameIndex, string framePath, DateTimeOffset timestamp, CancellationToken ct = default);
}
```

```csharp
// src/OpenEye.Abstractions/IDetectionClient.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IDetectionClient
{
    Task<IReadOnlyList<Detection>> DetectAsync(string framePath, IReadOnlySet<string> classFilter, CancellationToken ct = default);
}
```

```csharp
// src/OpenEye.Abstractions/INotificationDispatcher.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface INotificationDispatcher
{
    Task DispatchAsync(Event evt, NotificationConfig config, CancellationToken ct = default);
}
```

```csharp
// src/OpenEye.Abstractions/IEvidenceProvider.cs
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IEvidenceProvider
{
    Task<string?> CaptureEvidenceAsync(EvidenceRequest request, CancellationToken ct = default);
}
```

**Step 4: Verify build**

```bash
dotnet build src/OpenEye.slnx
```

Expected: Build succeeded. 0 Warning(s) 0 Error(s)

**Step 5: Commit**

```
feat: define abstractions for pipeline stages, Feature Store, plugin conditions, and infrastructure
```

---

### Task 3: Redis & PostgreSQL Helpers

**Files:**
- Create: `src/OpenEye.Shared/Redis/RedisStreamPublisher.cs`
- Create: `src/OpenEye.Shared/Redis/RedisStreamConsumer.cs`
- Create: `src/OpenEye.Shared/Redis/RedisConfigNotifier.cs`
- Create: `src/OpenEye.Shared/Postgres/PostgresConfigProvider.cs`
- Create: `src/OpenEye.Shared/Postgres/PostgresEventStore.cs`

> **Note:** Redis (`IConnectionMultiplexer`) and PostgreSQL (`NpgsqlDataSource`) connections are injected by Aspire. These helpers accept DI-injected instances — no manual connection string handling.

**Step 1: Implement RedisStreamPublisher**

```csharp
// src/OpenEye.Shared/Redis/RedisStreamPublisher.cs
using System.Text.Json;
using StackExchange.Redis;

namespace OpenEye.Shared.Redis;

public class RedisStreamPublisher(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task PublishAsync<T>(string streamKey, T message, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(message);
        await _db.StreamAddAsync(streamKey, [new NameValueEntry("data", json)]);
    }
}
```

**Step 2: Implement RedisStreamConsumer**

```csharp
// src/OpenEye.Shared/Redis/RedisStreamConsumer.cs
using System.Runtime.CompilerServices;
using System.Text.Json;
using StackExchange.Redis;

namespace OpenEye.Shared.Redis;

public class RedisStreamConsumer(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task EnsureGroupAsync(string streamKey, string groupName)
    {
        try
        {
            await _db.StreamCreateConsumerGroupAsync(streamKey, groupName, "0-0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists
        }
    }

    public async IAsyncEnumerable<(string Id, T Message)> ConsumeAsync<T>(
        string streamKey, string groupName, string consumerName,
        int batchSize = 10, [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var entries = await _db.StreamReadGroupAsync(
                streamKey, groupName, consumerName, ">", batchSize);

            if (entries.Length == 0)
            {
                await Task.Delay(100, ct);
                continue;
            }

            foreach (var entry in entries)
            {
                var json = entry.Values.FirstOrDefault(v => v.Name == "data").Value;
                if (json.IsNull) continue;
                var message = JsonSerializer.Deserialize<T>(json.ToString());
                if (message is not null)
                    yield return (entry.Id!, message);
            }
        }
    }

    public async Task AckAsync(string streamKey, string groupName, string messageId)
    {
        await _db.StreamAcknowledgeAsync(streamKey, groupName, messageId);
    }
}
```

**Step 3: Implement RedisConfigNotifier**

```csharp
// src/OpenEye.Shared/Redis/RedisConfigNotifier.cs
using System.Runtime.CompilerServices;
using System.Text.Json;
using StackExchange.Redis;

namespace OpenEye.Shared.Redis;

public class RedisConfigNotifier(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly ISubscriber _sub = redis.GetSubscriber();

    public async Task PublishChangeAsync(string configSection, CancellationToken ct = default)
    {
        await _sub.PublishAsync(RedisChannel.Literal("config:changed"), configSection);
    }

    public async IAsyncEnumerable<string> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var queue = System.Threading.Channels.Channel.CreateUnbounded<string>();
        await _sub.SubscribeAsync(RedisChannel.Literal("config:changed"), (_, value) =>
        {
            queue.Writer.TryWrite(value.ToString());
        });

        await foreach (var section in queue.Reader.ReadAllAsync(ct))
            yield return section;
    }

    public async Task SetClassFilterAsync(IReadOnlySet<string> classes, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(classes);
        await _db.StringSetAsync("config:class-filter", json);
    }

    public async Task<IReadOnlySet<string>> GetClassFilterAsync(CancellationToken ct = default)
    {
        var json = await _db.StringGetAsync("config:class-filter");
        if (json.IsNull) return new HashSet<string>();
        return JsonSerializer.Deserialize<HashSet<string>>(json.ToString()) ?? [];
    }
}
```

**Step 4: Implement PostgresConfigProvider (implements IConfigProvider)**

```csharp
// src/OpenEye.Shared/Postgres/PostgresConfigProvider.cs
using Dapper;
using Npgsql;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.Shared.Postgres;

public class PostgresConfigProvider(NpgsqlDataSource dataSource) : IConfigProvider
{
    public async Task<IReadOnlyList<CameraConfig>> GetCamerasAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var results = await conn.QueryAsync<CameraConfig>(
            "SELECT id, name, stream_url AS StreamUrl, type, target_fps AS TargetFps, enabled FROM cameras");
        return results.ToList();
    }

    public async Task<IReadOnlyList<Zone>> GetZonesAsync(string? sourceId = null, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = sourceId is null
            ? "SELECT zone_id, source_id, polygon FROM zones"
            : "SELECT zone_id, source_id, polygon FROM zones WHERE source_id = @SourceId";
        var rows = await conn.QueryAsync<dynamic>(sql, new { SourceId = sourceId });
        return rows.Select(r => new Zone(
            (string)r.zone_id,
            (string)r.source_id,
            System.Text.Json.JsonSerializer.Deserialize<List<Point2D>>((string)r.polygon) ?? []
        )).ToList();
    }

    public async Task<IReadOnlyList<Tripwire>> GetTripwiresAsync(string? sourceId = null, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = sourceId is null
            ? "SELECT tripwire_id, source_id, start_x, start_y, end_x, end_y FROM tripwires"
            : "SELECT tripwire_id, source_id, start_x, start_y, end_x, end_y FROM tripwires WHERE source_id = @SourceId";
        var rows = await conn.QueryAsync<dynamic>(sql, new { SourceId = sourceId });
        return rows.Select(r => new Tripwire(
            (string)r.tripwire_id,
            (string)r.source_id,
            new Point2D((double)r.start_x, (double)r.start_y),
            new Point2D((double)r.end_x, (double)r.end_y)
        )).ToList();
    }

    public async Task<IReadOnlyList<PrimitiveConfig>> GetPrimitivesAsync(string? sourceId = null, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = sourceId is null
            ? "SELECT name, type, class_label, zone_id, tripwire_id, source_id FROM primitive_configs"
            : "SELECT name, type, class_label, zone_id, tripwire_id, source_id FROM primitive_configs WHERE source_id = @SourceId";
        var rows = await conn.QueryAsync<dynamic>(sql, new { SourceId = sourceId });
        return rows.Select(r => new PrimitiveConfig(
            (string)r.name,
            Enum.Parse<PrimitiveType>((string)r.type),
            (string)r.class_label,
            (string?)r.zone_id,
            (string?)r.tripwire_id,
            (string)r.source_id
        )).ToList();
    }

    public async Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<dynamic>("SELECT * FROM rules");
        return rows.Select(r => new RuleDefinition(
            (string)r.rule_id,
            (string)r.name,
            (string)r.object_class,
            (string?)r.zone_id,
            (string?)r.tripwire_id,
            System.Text.Json.JsonSerializer.Deserialize<List<ConditionConfig>>((string)r.conditions) ?? [],
            (string)r.action,
            r.sustained_seconds is not null ? TimeSpan.FromSeconds((double)r.sustained_seconds) : null,
            r.within_seconds is not null ? TimeSpan.FromSeconds((double)r.within_seconds) : null,
            (int?)r.min_occurrences,
            r.cooldown_seconds is not null ? TimeSpan.FromSeconds((double)r.cooldown_seconds) : null,
            r.evidence_type is not null ? Enum.Parse<EvidenceType>((string)r.evidence_type) : null
        )).ToList();
    }

    public async Task<IReadOnlyList<NotificationConfig>> GetNotificationsAsync(string? ruleId = null, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = ruleId is null
            ? "SELECT rule_id, channels FROM notification_configs"
            : "SELECT rule_id, channels FROM notification_configs WHERE rule_id = @RuleId";
        var rows = await conn.QueryAsync<dynamic>(sql, new { RuleId = ruleId });
        return rows.Select(r => new NotificationConfig(
            (string)r.rule_id,
            System.Text.Json.JsonSerializer.Deserialize<List<NotificationChannel>>((string)r.channels) ?? []
        )).ToList();
    }
}
```

**Step 5: Implement PostgresEventStore**

```csharp
// src/OpenEye.Shared/Postgres/PostgresEventStore.cs
using System.Text.Json;
using Dapper;
using Npgsql;
using OpenEye.Shared.Models;

namespace OpenEye.Shared.Postgres;

public class PostgresEventStore(NpgsqlDataSource dataSource)
{
    public async Task SaveEventAsync(Event evt, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO events (event_id, event_type, timestamp, source_id, zone_id, rule_id, tracked_objects, metadata)
            VALUES (@EventId, @EventType, @Timestamp, @SourceId, @ZoneId, @RuleId, @TrackedObjects::jsonb, @Metadata::jsonb)
            """, new
        {
            evt.EventId,
            evt.EventType,
            evt.Timestamp,
            evt.SourceId,
            evt.ZoneId,
            evt.RuleId,
            TrackedObjects = JsonSerializer.Serialize(evt.TrackedObjects),
            Metadata = JsonSerializer.Serialize(evt.Metadata)
        });
    }

    public async Task<IReadOnlyList<Event>> QueryEventsAsync(
        string? sourceId = null, string? ruleId = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int page = 1, int limit = 20, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = """
            SELECT event_id, event_type, timestamp, source_id, zone_id, rule_id, tracked_objects, metadata
            FROM events WHERE 1=1
            """;
        if (sourceId is not null) sql += " AND source_id = @SourceId";
        if (ruleId is not null) sql += " AND rule_id = @RuleId";
        if (from is not null) sql += " AND timestamp >= @From";
        if (to is not null) sql += " AND timestamp <= @To";
        sql += " ORDER BY timestamp DESC LIMIT @Limit OFFSET @Offset";

        var rows = await conn.QueryAsync<dynamic>(sql, new
        {
            SourceId = sourceId, RuleId = ruleId, From = from, To = to,
            Limit = limit, Offset = (page - 1) * limit
        });

        return rows.Select(r => new Event(
            (string)r.event_id, (string)r.event_type, (DateTimeOffset)r.timestamp,
            (string)r.source_id, (string?)r.zone_id,
            JsonSerializer.Deserialize<List<TrackedObject>>((string)r.tracked_objects) ?? [],
            (string)r.rule_id,
            JsonSerializer.Deserialize<Dictionary<string, object>>((string)r.metadata)
        )).ToList();
    }
}
```

**Step 6: Verify build**

```bash
dotnet build src/OpenEye.slnx
```

Expected: Build succeeded. 0 Warning(s) 0 Error(s)

**Step 7: Commit**

```
feat: add Redis stream helpers and PostgreSQL data access layer
```

---

## Phase 2: Pipeline Core — Tracking & Zones

### Task 4: Hungarian Algorithm

**Files:**
- Create: `src/OpenEye.PipelineCore/Tracking/HungarianAlgorithm.cs`
- Test: `src/OpenEye.Tests/Tracking/HungarianAlgorithmTests.cs`

**Step 1: Write the failing tests**

```csharp
// src/OpenEye.Tests/Tracking/HungarianAlgorithmTests.cs
using OpenEye.PipelineCore.Tracking;

namespace OpenEye.Tests.Tracking;

public class HungarianAlgorithmTests
{
    [Fact]
    public void Solve_1x1_ReturnsZero()
    {
        var cost = new double[,] { { 5.0 } };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Equal([0], result);
    }

    [Fact]
    public void Solve_2x2_ReturnsOptimalAssignment()
    {
        // Optimal: row 0→col 1 (cost 2), row 1→col 0 (cost 3) = total 5
        var cost = new double[,]
        {
            { 10, 2 },
            { 3, 10 }
        };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Equal(1, result[0]);
        Assert.Equal(0, result[1]);
    }

    [Fact]
    public void Solve_3x3_ReturnsOptimalAssignment()
    {
        // Classic 3x3 example
        var cost = new double[,]
        {
            { 1, 2, 3 },
            { 2, 4, 6 },
            { 3, 6, 9 }
        };
        var result = HungarianAlgorithm.Solve(cost);
        // Each row assigned to unique column
        var cols = new HashSet<int>(result);
        Assert.Equal(3, cols.Count);
        // Verify total cost is optimal (1+4+9 or 1+6+6 etc — diagonal is 14)
        double total = 0;
        for (int i = 0; i < 3; i++) total += cost[i, result[i]];
        Assert.True(total <= 14);
    }

    [Fact]
    public void Solve_MoreRowsThanCols_UnassignsExtraRows()
    {
        // 3 rows, 2 cols — one row will be unassigned (-1)
        var cost = new double[,]
        {
            { 1, 5 },
            { 5, 1 },
            { 3, 3 }
        };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Equal(3, result.Length);
        Assert.Contains(-1, result);
    }

    [Fact]
    public void Solve_MoreColsThanRows_AllRowsAssigned()
    {
        // 2 rows, 3 cols — all rows assigned
        var cost = new double[,]
        {
            { 1, 5, 9 },
            { 5, 1, 9 }
        };
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Equal(2, result.Length);
        Assert.DoesNotContain(-1, result);
    }

    [Fact]
    public void Solve_EmptyMatrix_ReturnsEmpty()
    {
        var cost = new double[0, 0];
        var result = HungarianAlgorithm.Solve(cost);
        Assert.Empty(result);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~HungarianAlgorithmTests" -v n
```

Expected: FAIL — `The type or namespace name 'Tracking' could not be found`

**Step 3: Implement HungarianAlgorithm**

```csharp
// src/OpenEye.PipelineCore/Tracking/HungarianAlgorithm.cs
namespace OpenEye.PipelineCore.Tracking;

/// <summary>
/// Solves the linear assignment problem using the Hungarian (Munkres) algorithm.
/// O(n^3) time complexity.
/// </summary>
public static class HungarianAlgorithm
{
    /// <summary>
    /// Returns optimal column assignment for each row. -1 = unassigned.
    /// </summary>
    public static int[] Solve(double[,] costMatrix)
    {
        int rows = costMatrix.GetLength(0);
        int cols = costMatrix.GetLength(1);

        if (rows == 0 || cols == 0)
            return [];

        int n = Math.Max(rows, cols);
        var cost = new double[n, n];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                cost[i, j] = costMatrix[i, j];

        var u = new double[n + 1];
        var v = new double[n + 1];
        var p = new int[n + 1];
        var way = new int[n + 1];

        for (int i = 1; i <= n; i++)
        {
            p[0] = i;
            int j0 = 0;
            var minv = new double[n + 1];
            var used = new bool[n + 1];
            Array.Fill(minv, double.MaxValue);

            do
            {
                used[j0] = true;
                int i0 = p[j0], j1 = 0;
                double delta = double.MaxValue;

                for (int j = 1; j <= n; j++)
                {
                    if (used[j]) continue;
                    double cur = cost[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j])
                    {
                        minv[j] = cur;
                        way[j] = j0;
                    }
                    if (minv[j] < delta)
                    {
                        delta = minv[j];
                        j1 = j;
                    }
                }

                for (int j = 0; j <= n; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            }
            while (p[j0] != 0);

            do
            {
                int j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            }
            while (j0 != 0);
        }

        var result = new int[rows];
        Array.Fill(result, -1);
        for (int j = 1; j <= n; j++)
        {
            if (p[j] <= rows && j <= cols)
                result[p[j] - 1] = j - 1;
        }

        return result;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~HungarianAlgorithmTests" -v n
```

Expected: PASS — 6 tests green

**Step 5: Commit**

```
feat: implement Hungarian algorithm for optimal assignment
```

---

### Task 5: SORT Tracker

**Files:**
- Create: `src/OpenEye.PipelineCore/Tracking/TrackerConfig.cs`
- Create: `src/OpenEye.PipelineCore/Tracking/SortTracker.cs`
- Test: `src/OpenEye.Tests/Tracking/SortTrackerTests.cs`

**Step 1: Write the failing tests**

```csharp
// src/OpenEye.Tests/Tracking/SortTrackerTests.cs
using OpenEye.Abstractions;
using OpenEye.PipelineCore.Tracking;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Tracking;

public class SortTrackerTests
{
    private static Detection MakeDet(double x, double y, double w = 0.1, double h = 0.1,
        string cls = "person", string src = "cam-1") =>
        new(cls, new BoundingBox(x, y, w, h), 0.9, DateTimeOffset.UtcNow, src);

    [Fact]
    public void SingleDetection_CreatesOneTrack()
    {
        var tracker = new SortTracker();
        var result = tracker.Update([MakeDet(0.1, 0.1)], DateTimeOffset.UtcNow);
        Assert.Single(result);
        Assert.Equal(TrackState.Active, result[0].State);
    }

    [Fact]
    public void SamePosition_MaintainsTrackId()
    {
        var tracker = new SortTracker();
        var t0 = DateTimeOffset.UtcNow;
        tracker.Update([MakeDet(0.1, 0.1)], t0);
        var result = tracker.Update([MakeDet(0.11, 0.11)], t0.AddSeconds(1));
        Assert.Single(result);
        Assert.Equal("track-0", result[0].TrackId);
    }

    [Fact]
    public void TwoNonOverlapping_CreateTwoTracks()
    {
        var tracker = new SortTracker();
        var result = tracker.Update([MakeDet(0.0, 0.0), MakeDet(0.9, 0.9)], DateTimeOffset.UtcNow);
        Assert.Equal(2, result.Count);
        Assert.NotEqual(result[0].TrackId, result[1].TrackId);
    }

    [Fact]
    public void ObjectDisappears_TransitionsToLostThenExpired()
    {
        var config = new TrackerConfig { MaxLostFrames = 3 };
        var tracker = new SortTracker(config);
        var t = DateTimeOffset.UtcNow;

        tracker.Update([MakeDet(0.1, 0.1)], t);
        tracker.Update([], t.AddSeconds(1)); // Lost
        var result = tracker.Update([], t.AddSeconds(2)); // Still lost
        Assert.Equal(TrackState.Lost, result.First(r => r.TrackId == "track-0").State);

        tracker.Update([], t.AddSeconds(3));
        result = tracker.Update([], t.AddSeconds(4)); // frame 4 of no detection
        // After MaxLostFrames=3 missed frames, should be expired
        var track = result.FirstOrDefault(r => r.TrackId == "track-0");
        Assert.True(track is null || track.State == TrackState.Expired);
    }

    [Fact]
    public void ObjectReappears_WithinLostWindow_MaintainsTrackId()
    {
        var config = new TrackerConfig { MaxLostFrames = 10 };
        var tracker = new SortTracker(config);
        var t = DateTimeOffset.UtcNow;

        tracker.Update([MakeDet(0.1, 0.1)], t);
        tracker.Update([], t.AddSeconds(1));
        var result = tracker.Update([MakeDet(0.1, 0.1)], t.AddSeconds(2));
        Assert.Contains(result, r => r.TrackId == "track-0" && r.State == TrackState.Active);
    }

    [Fact]
    public void Trajectory_AccumulatesPositions()
    {
        var tracker = new SortTracker();
        var t = DateTimeOffset.UtcNow;
        tracker.Update([MakeDet(0.1, 0.1)], t);
        tracker.Update([MakeDet(0.12, 0.12)], t.AddSeconds(1));
        var result = tracker.Update([MakeDet(0.14, 0.14)], t.AddSeconds(2));
        Assert.Equal(3, result[0].Trajectory.Count);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var tracker = new SortTracker();
        tracker.Update([MakeDet(0.1, 0.1)], DateTimeOffset.UtcNow);
        tracker.Reset();
        Assert.Empty(tracker.ActiveTracks);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~SortTrackerTests" -v n
```

Expected: FAIL — `The type or namespace name 'SortTracker' could not be found`

**Step 3: Implement TrackerConfig and SortTracker**

```csharp
// src/OpenEye.PipelineCore/Tracking/TrackerConfig.cs
namespace OpenEye.PipelineCore.Tracking;

public class TrackerConfig
{
    public int MaxLostFrames { get; set; } = 30;
    public double IouThreshold { get; set; } = 0.3;
    public int TrajectoryDepth { get; set; } = 50;
}
```

```csharp
// src/OpenEye.PipelineCore/Tracking/SortTracker.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Tracking;

public class SortTracker(TrackerConfig? config = null) : IObjectTracker
{
    private readonly TrackerConfig _config = config ?? new TrackerConfig();
    private readonly List<TrackedObject> _tracks = [];
    private int _nextId;

    public IReadOnlyList<TrackedObject> ActiveTracks =>
        _tracks.Where(t => t.State != TrackState.Expired).ToList();

    public IReadOnlyList<TrackedObject> Update(IReadOnlyList<Detection> detections, DateTimeOffset timestamp)
    {
        var active = _tracks.Where(t => t.State != TrackState.Expired).ToList();

        if (active.Count == 0 && detections.Count == 0)
            return [];

        if (active.Count == 0)
        {
            foreach (var det in detections)
                CreateTrack(det, timestamp);
            return ActiveTracks;
        }

        if (detections.Count == 0)
        {
            foreach (var track in active)
                IncrementLost(track);
            return ActiveTracks;
        }

        var costMatrix = new double[active.Count, detections.Count];
        for (int i = 0; i < active.Count; i++)
            for (int j = 0; j < detections.Count; j++)
                costMatrix[i, j] = 1.0 - ComputeIoU(active[i].CurrentBox, detections[j].BoundingBox);

        var assignments = HungarianAlgorithm.Solve(costMatrix);
        var matchedDetections = new HashSet<int>();

        for (int i = 0; i < active.Count; i++)
        {
            int j = assignments[i];
            if (j >= 0 && (1.0 - costMatrix[i, j]) >= _config.IouThreshold)
            {
                UpdateTrack(active[i], detections[j], timestamp);
                matchedDetections.Add(j);
            }
            else
            {
                IncrementLost(active[i]);
            }
        }

        for (int j = 0; j < detections.Count; j++)
        {
            if (!matchedDetections.Contains(j))
                CreateTrack(detections[j], timestamp);
        }

        return ActiveTracks;
    }

    public void Reset()
    {
        _tracks.Clear();
        _nextId = 0;
    }

    private void CreateTrack(Detection det, DateTimeOffset timestamp)
    {
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

    private void UpdateTrack(TrackedObject track, Detection det, DateTimeOffset timestamp)
    {
        track.CurrentBox = det.BoundingBox;
        track.LastSeen = timestamp;
        track.State = TrackState.Active;
        track.Metadata.Remove("lostFrames");
        track.Trajectory.Add(new TrajectoryPoint(det.BoundingBox, timestamp));
        if (track.Trajectory.Count > _config.TrajectoryDepth)
            track.Trajectory.RemoveAt(0);
    }

    private void IncrementLost(TrackedObject track)
    {
        var lostFrames = (int)track.Metadata.GetValueOrDefault("lostFrames", 0) + 1;
        track.Metadata["lostFrames"] = lostFrames;
        track.State = lostFrames >= _config.MaxLostFrames ? TrackState.Expired : TrackState.Lost;
    }

    private static double ComputeIoU(BoundingBox a, BoundingBox b)
    {
        double x1 = Math.Max(a.X, b.X);
        double y1 = Math.Max(a.Y, b.Y);
        double x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        double y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        double intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        double union = a.Width * a.Height + b.Width * b.Height - intersection;
        return union == 0 ? 0 : intersection / union;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~SortTrackerTests" -v n
```

Expected: PASS — 7 tests green

**Step 5: Commit**

```
feat: implement SORT-style object tracker with Hungarian algorithm
```

---

### Task 6: Geometry Utilities — Point-in-Polygon & Line Intersection

**Files:**
- Create: `src/OpenEye.PipelineCore/Zones/Geometry.cs`
- Test: `src/OpenEye.Tests/Zones/GeometryTests.cs`

**Step 1: Write the failing tests**

```csharp
// src/OpenEye.Tests/Zones/GeometryTests.cs
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Zones;

public class GeometryTests
{
    private static readonly IReadOnlyList<Point2D> Square =
        [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

    private static readonly IReadOnlyList<Point2D> Triangle =
        [new(0.5, 0), new(1, 1), new(0, 1)];

    // --- Point-in-Polygon ---

    [Fact]
    public void PointInPolygon_Inside_ReturnsTrue()
    {
        Assert.True(Geometry.PointInPolygon(Square, new Point2D(0.5, 0.5)));
    }

    [Fact]
    public void PointInPolygon_Outside_ReturnsFalse()
    {
        Assert.False(Geometry.PointInPolygon(Square, new Point2D(1.5, 0.5)));
    }

    [Fact]
    public void PointInPolygon_Triangle_Inside()
    {
        Assert.True(Geometry.PointInPolygon(Triangle, new Point2D(0.5, 0.7)));
    }

    [Fact]
    public void PointInPolygon_Triangle_Outside()
    {
        Assert.False(Geometry.PointInPolygon(Triangle, new Point2D(0.1, 0.1)));
    }

    [Fact]
    public void PointInPolygon_EmptyPolygon_ReturnsFalse()
    {
        Assert.False(Geometry.PointInPolygon([], new Point2D(0.5, 0.5)));
    }

    // --- Line Intersection ---

    [Fact]
    public void SegmentsIntersect_Crossing_ReturnsTrue()
    {
        Assert.True(Geometry.SegmentsIntersect(
            new(0, 0), new(1, 1), new(0, 1), new(1, 0)));
    }

    [Fact]
    public void SegmentsIntersect_Parallel_ReturnsFalse()
    {
        Assert.False(Geometry.SegmentsIntersect(
            new(0, 0), new(1, 0), new(0, 1), new(1, 1)));
    }

    [Fact]
    public void SegmentsIntersect_NonTouching_ReturnsFalse()
    {
        Assert.False(Geometry.SegmentsIntersect(
            new(0, 0), new(0.4, 0.4), new(0.6, 0.6), new(1, 1)));
    }

    // --- Cross Direction ---

    [Fact]
    public void CrossDirection_LeftToRight_IsPositive()
    {
        // Tripwire from (0.5,0) to (0.5,1) — vertical line
        // Movement from (0.3,0.5) to (0.7,0.5) — left to right
        double cross = Geometry.CrossProduct(
            new(0.5, 0), new(0.5, 1), new(0.7, 0.5));
        Assert.True(cross > 0); // Left-to-right is positive
    }

    [Fact]
    public void CrossDirection_RightToLeft_IsNegative()
    {
        double cross = Geometry.CrossProduct(
            new(0.5, 0), new(0.5, 1), new(0.3, 0.5));
        Assert.True(cross < 0);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~GeometryTests" -v n
```

Expected: FAIL — `The type or namespace name 'Geometry' could not be found`

**Step 3: Implement Geometry utilities**

```csharp
// src/OpenEye.PipelineCore/Zones/Geometry.cs
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Zones;

public static class Geometry
{
    /// <summary>Ray-casting point-in-polygon test.</summary>
    public static bool PointInPolygon(IReadOnlyList<Point2D> polygon, Point2D point)
    {
        if (polygon.Count < 3) return false;

        bool inside = false;
        int n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            if ((pi.Y > point.Y) != (pj.Y > point.Y) &&
                point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>Tests if two line segments intersect (proper crossing only).</summary>
    public static bool SegmentsIntersect(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        double d1 = CrossProduct(b1, b2, a1);
        double d2 = CrossProduct(b1, b2, a2);
        double d3 = CrossProduct(a1, a2, b1);
        double d4 = CrossProduct(a1, a2, b2);

        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    /// <summary>
    /// Cross product of vectors (o→a) and (o→b).
    /// Positive = b is left of o→a, Negative = right.
    /// </summary>
    public static double CrossProduct(Point2D o, Point2D a, Point2D b)
    {
        return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
    }

    /// <summary>Computes centroid of a bounding box.</summary>
    public static Point2D Centroid(BoundingBox box) =>
        new(box.X + box.Width / 2, box.Y + box.Height / 2);
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~GeometryTests" -v n
```

Expected: PASS — 10 tests green

**Step 5: Commit**

```
feat: implement geometry utilities for point-in-polygon and line intersection
```

---

### Task 7: Zone Evaluator

**Files:**
- Create: `src/OpenEye.PipelineCore/Zones/DefaultZoneEvaluator.cs`
- Test: `src/OpenEye.Tests/Zones/ZoneEvaluatorTests.cs`

**Step 1: Write the failing tests**

```csharp
// src/OpenEye.Tests/Zones/ZoneEvaluatorTests.cs
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Zones;

public class ZoneEvaluatorTests
{
    private static readonly Zone TestZone = new("zone-1", "cam-1",
        [new(0.2, 0.2), new(0.8, 0.2), new(0.8, 0.8), new(0.2, 0.8)]);

    private static readonly Tripwire TestTripwire = new("tw-1", "cam-1",
        new(0.5, 0.0), new(0.5, 1.0));

    private static TrackedObject MakeTrack(string id, double cx, double cy, DateTimeOffset t,
        List<TrajectoryPoint>? trajectory = null)
    {
        var box = new BoundingBox(cx - 0.05, cy - 0.05, 0.1, 0.1);
        var track = new TrackedObject
        {
            TrackId = id, ClassLabel = "person", CurrentBox = box,
            FirstSeen = t, LastSeen = t
        };
        if (trajectory is not null)
            track.Trajectory.AddRange(trajectory);
        else
            track.Trajectory.Add(new TrajectoryPoint(box, t));
        return track;
    }

    [Fact]
    public void ObjectEntersZone_RecordsEntryTransition()
    {
        var eval = new DefaultZoneEvaluator();
        var t = DateTimeOffset.UtcNow;
        var track = MakeTrack("t1", 0.5, 0.5, t);

        var result = eval.Evaluate([track], [TestZone], []);

        Assert.Single(result.Transitions);
        Assert.Equal(ZoneTransitionType.Enter, result.Transitions[0].Type);
        Assert.Single(result.ActivePresences);
    }

    [Fact]
    public void ObjectLeavesZone_RecordsExitTransition()
    {
        var eval = new DefaultZoneEvaluator();
        var t = DateTimeOffset.UtcNow;

        eval.Evaluate([MakeTrack("t1", 0.5, 0.5, t)], [TestZone], []);
        var result = eval.Evaluate([MakeTrack("t1", 0.0, 0.0, t.AddSeconds(1))], [TestZone], []);

        Assert.Single(result.Transitions);
        Assert.Equal(ZoneTransitionType.Exit, result.Transitions[0].Type);
    }

    [Fact]
    public void ObjectStaysInZone_NoSpuriousTransitions()
    {
        var eval = new DefaultZoneEvaluator();
        var t = DateTimeOffset.UtcNow;

        eval.Evaluate([MakeTrack("t1", 0.5, 0.5, t)], [TestZone], []);
        var result = eval.Evaluate([MakeTrack("t1", 0.55, 0.55, t.AddSeconds(1))], [TestZone], []);

        Assert.Empty(result.Transitions);
        Assert.Single(result.ActivePresences);
    }

    [Fact]
    public void TripwireCrossing_Detected()
    {
        var eval = new DefaultZoneEvaluator();
        var t = DateTimeOffset.UtcNow;

        var prevBox = new BoundingBox(0.3, 0.45, 0.1, 0.1);
        var currBox = new BoundingBox(0.6, 0.45, 0.1, 0.1);
        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person", CurrentBox = currBox,
            FirstSeen = t, LastSeen = t.AddSeconds(1)
        };
        track.Trajectory.Add(new TrajectoryPoint(prevBox, t));
        track.Trajectory.Add(new TrajectoryPoint(currBox, t.AddSeconds(1)));

        var result = eval.Evaluate([track], [], [TestTripwire]);

        Assert.Single(result.TripwireCrossings);
        Assert.Equal("tw-1", result.TripwireCrossings[0].TripwireId);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~ZoneEvaluatorTests" -v n
```

Expected: FAIL — `The type or namespace name 'DefaultZoneEvaluator' could not be found`

**Step 3: Implement DefaultZoneEvaluator**

```csharp
// src/OpenEye.PipelineCore/Zones/DefaultZoneEvaluator.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Zones;

public class DefaultZoneEvaluator : IZoneEvaluator
{
    private readonly Dictionary<(string TrackId, string ZoneId), ZonePresence> _activePresences = [];

    public ZoneEvaluationResult Evaluate(
        IReadOnlyList<TrackedObject> tracks,
        IReadOnlyList<Zone> zones,
        IReadOnlyList<Tripwire> tripwires)
    {
        var transitions = new List<ZoneTransition>();
        var crossings = new List<TripwireCrossing>();
        var currentKeys = new HashSet<(string, string)>();

        foreach (var track in tracks.Where(t => t.State == TrackState.Active))
        {
            var centroid = Geometry.Centroid(track.CurrentBox);

            foreach (var zone in zones)
            {
                var key = (track.TrackId, zone.ZoneId);
                bool isInside = Geometry.PointInPolygon(zone.Polygon, centroid);
                bool wasInside = _activePresences.ContainsKey(key);

                if (isInside && !wasInside)
                {
                    var presence = new ZonePresence
                    {
                        TrackId = track.TrackId,
                        ZoneId = zone.ZoneId,
                        EnteredAt = track.LastSeen
                    };
                    _activePresences[key] = presence;
                    transitions.Add(new ZoneTransition(track.TrackId, zone.ZoneId,
                        ZoneTransitionType.Enter, track.LastSeen));
                }
                else if (!isInside && wasInside)
                {
                    _activePresences[key].ExitedAt = track.LastSeen;
                    transitions.Add(new ZoneTransition(track.TrackId, zone.ZoneId,
                        ZoneTransitionType.Exit, track.LastSeen));
                    _activePresences.Remove(key);
                }

                if (isInside)
                    currentKeys.Add(key);
            }

            // Tripwire crossing: check last trajectory segment
            if (track.Trajectory.Count >= 2)
            {
                var prev = Geometry.Centroid(track.Trajectory[^2].Box);
                var curr = Geometry.Centroid(track.Trajectory[^1].Box);

                foreach (var tripwire in tripwires)
                {
                    if (Geometry.SegmentsIntersect(prev, curr, tripwire.Start, tripwire.End))
                    {
                        crossings.Add(new TripwireCrossing(
                            track.TrackId, tripwire.TripwireId, track.LastSeen));
                    }
                }
            }
        }

        // Clean up presences for expired/lost tracks
        var activeTrackIds = tracks.Where(t => t.State == TrackState.Active)
            .Select(t => t.TrackId).ToHashSet();
        var staleKeys = _activePresences.Keys.Where(k => !activeTrackIds.Contains(k.TrackId)).ToList();
        foreach (var key in staleKeys)
            _activePresences.Remove(key);

        return new ZoneEvaluationResult(
            transitions,
            crossings,
            _activePresences.Values.ToList());
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~ZoneEvaluatorTests" -v n
```

Expected: PASS — 4 tests green

**Step 5: Commit**

```
feat: implement zone evaluator with point-in-polygon and tripwire detection
```

---

## Phase 3: Pipeline Core — Feature Store

### Task 8: Feature Extractors

**Files:**
- Create: `src/OpenEye.PipelineCore/Features/ObjectFeatureExtractor.cs`
- Create: `src/OpenEye.PipelineCore/Features/ZoneFeatureExtractor.cs`
- Create: `src/OpenEye.PipelineCore/Features/TemporalFeatureExtractor.cs`
- Test: `src/OpenEye.Tests/Features/ObjectFeatureExtractorTests.cs`
- Test: `src/OpenEye.Tests/Features/ZoneFeatureExtractorTests.cs`

**Step 1: Write ObjectFeatureExtractor tests**

```csharp
// src/OpenEye.Tests/Features/ObjectFeatureExtractorTests.cs
using OpenEye.PipelineCore.Features;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Features;

public class ObjectFeatureExtractorTests
{
    [Fact]
    public void ComputesObjectSpeed()
    {
        var t = DateTimeOffset.UtcNow;
        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.3, 0.3, 0.1, 0.1),
            FirstSeen = t.AddSeconds(-2), LastSeen = t
        };
        track.Trajectory.Add(new TrajectoryPoint(new BoundingBox(0.1, 0.1, 0.1, 0.1), t.AddSeconds(-2)));
        track.Trajectory.Add(new TrajectoryPoint(new BoundingBox(0.3, 0.3, 0.1, 0.1), t));

        var ctx = new FrameContext { SourceId = "cam-1", Timestamp = t, Detections = [], Tracks = [track] };
        new ObjectFeatureExtractor().Update(ctx);

        var speed = ctx.Features.Get<double>("object_speed", "t1");
        Assert.True(speed > 0);
    }

    [Fact]
    public void ComputesTimeInScene()
    {
        var t = DateTimeOffset.UtcNow;
        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.1, 0.1, 0.1, 0.1),
            FirstSeen = t.AddSeconds(-5), LastSeen = t
        };

        var ctx = new FrameContext { SourceId = "cam-1", Timestamp = t, Detections = [], Tracks = [track] };
        new ObjectFeatureExtractor().Update(ctx);

        Assert.Equal(5.0, ctx.Features.Get<double>("object_time_in_scene", "t1"), 0.1);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~ObjectFeatureExtractorTests" -v n
```

Expected: FAIL — `The type or namespace name 'ObjectFeatureExtractor' could not be found`

**Step 3: Implement ObjectFeatureExtractor**

```csharp
// src/OpenEye.PipelineCore/Features/ObjectFeatureExtractor.cs
using OpenEye.Abstractions;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Features;

public class ObjectFeatureExtractor : IFeatureExtractor
{
    public void Update(FrameContext context)
    {
        foreach (var track in context.Tracks.Where(t => t.State == TrackState.Active))
        {
            // Speed: displacement / time between first and last trajectory points
            if (track.Trajectory.Count >= 2)
            {
                var first = track.Trajectory[0];
                var last = track.Trajectory[^1];
                var c1 = Geometry.Centroid(first.Box);
                var c2 = Geometry.Centroid(last.Box);
                double dx = c2.X - c1.X;
                double dy = c2.Y - c1.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                double dt = (last.Timestamp - first.Timestamp).TotalSeconds;
                double speed = dt > 0 ? dist / dt : 0;
                context.Features.Set("object_speed", speed, track.TrackId);

                // Direction in radians
                double direction = Math.Atan2(dy, dx);
                context.Features.Set("object_direction", direction, track.TrackId);

                // Path length: sum of segment distances
                double pathLength = 0;
                for (int i = 1; i < track.Trajectory.Count; i++)
                {
                    var p1 = Geometry.Centroid(track.Trajectory[i - 1].Box);
                    var p2 = Geometry.Centroid(track.Trajectory[i].Box);
                    double sdx = p2.X - p1.X, sdy = p2.Y - p1.Y;
                    pathLength += Math.Sqrt(sdx * sdx + sdy * sdy);
                }
                context.Features.Set("object_path_length", pathLength, track.TrackId);
            }

            // Time in scene
            double timeInScene = (context.Timestamp - track.FirstSeen).TotalSeconds;
            context.Features.Set("object_time_in_scene", timeInScene, track.TrackId);
        }
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~ObjectFeatureExtractorTests" -v n
```

Expected: PASS — 2 tests green

**Step 5: Write ZoneFeatureExtractor tests**

```csharp
// src/OpenEye.Tests/Features/ZoneFeatureExtractorTests.cs
using OpenEye.PipelineCore.Features;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Features;

public class ZoneFeatureExtractorTests
{
    [Fact]
    public void ComputesZoneOccupancy()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = new FrameContext
        {
            SourceId = "cam-1", Timestamp = t, Detections = [],
            Tracks = [],
            ZoneResult = new ZoneEvaluationResult([], [],
            [
                new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t },
                new ZonePresence { TrackId = "t2", ZoneId = "z1", EnteredAt = t }
            ])
        };

        new ZoneFeatureExtractor().Update(ctx);

        Assert.Equal(2, ctx.Features.Get<int>("zone_occupancy", "z1"));
    }

    [Fact]
    public void ComputesDwellTime()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = new FrameContext
        {
            SourceId = "cam-1", Timestamp = t, Detections = [],
            Tracks = [],
            ZoneResult = new ZoneEvaluationResult([], [],
            [
                new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t.AddSeconds(-10) }
            ])
        };

        new ZoneFeatureExtractor().Update(ctx);

        Assert.Equal(10.0, ctx.Features.Get<double>("dwell_time", "t1:z1"), 0.1);
    }
}
```

**Step 6: Implement ZoneFeatureExtractor and TemporalFeatureExtractor**

```csharp
// src/OpenEye.PipelineCore/Features/ZoneFeatureExtractor.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Features;

public class ZoneFeatureExtractor : IFeatureExtractor
{
    public void Update(FrameContext context)
    {
        if (context.ZoneResult is null) return;

        // Zone occupancy: count of active presences per zone
        var occupancy = context.ZoneResult.ActivePresences
            .GroupBy(p => p.ZoneId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (zoneId, count) in occupancy)
            context.Features.Set("zone_occupancy", count, zoneId);

        // Dwell time per track+zone
        foreach (var presence in context.ZoneResult.ActivePresences)
        {
            double dwell = (context.Timestamp - presence.EnteredAt).TotalSeconds;
            context.Features.Set("dwell_time", dwell, $"{presence.TrackId}:{presence.ZoneId}");
        }
    }
}
```

```csharp
// src/OpenEye.PipelineCore/Features/TemporalFeatureExtractor.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Features;

public class TemporalFeatureExtractor : IFeatureExtractor
{
    private readonly Dictionary<string, DateTimeOffset> _lastSeen = [];

    public void Update(FrameContext context)
    {
        var currentTrackIds = new HashSet<string>();

        foreach (var track in context.Tracks.Where(t => t.State == TrackState.Active))
        {
            currentTrackIds.Add(track.TrackId);

            if (_lastSeen.TryGetValue(track.TrackId, out var prev))
            {
                double timeSince = (context.Timestamp - prev).TotalSeconds;
                context.Features.Set("time_since_last_seen", timeSince, track.TrackId);
            }

            _lastSeen[track.TrackId] = context.Timestamp;
        }

        // Clean up expired tracks
        var stale = _lastSeen.Keys.Except(currentTrackIds).ToList();
        foreach (var id in stale) _lastSeen.Remove(id);
    }
}
```

**Step 7: Run all feature tests**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~Features" -v n
```

Expected: PASS — 13 tests green (9 FeatureStore + FrameContext + 2 ObjectFeature + 2 ZoneFeature)

**Step 8: Commit**

```
feat: implement feature extractors for object, zone, and temporal metrics
```

---

## Phase 4: Pipeline Core — Primitives & Rules

### Task 9: Primitive Extractor (consuming Feature Store)

**Files:**
- Create: `src/OpenEye.PipelineCore/Primitives/DefaultPrimitiveExtractor.cs`
- Test: `src/OpenEye.Tests/Primitives/PrimitiveExtractorTests.cs`

**Step 1: Write the failing tests**

```csharp
// src/OpenEye.Tests/Primitives/PrimitiveExtractorTests.cs
using OpenEye.PipelineCore.Primitives;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Primitives;

public class PrimitiveExtractorTests
{
    private static FrameContext MakeContext(
        IReadOnlyList<TrackedObject> tracks,
        ZoneEvaluationResult? zoneResult = null,
        DateTimeOffset? timestamp = null)
    {
        var t = timestamp ?? DateTimeOffset.UtcNow;
        return new FrameContext
        {
            SourceId = "cam-1", Timestamp = t, Detections = [],
            Tracks = tracks,
            ZoneResult = zoneResult ?? new ZoneEvaluationResult([], [], [])
        };
    }

    [Fact]
    public void Presence_PersonInZone_ReturnsTrue()
    {
        var t = DateTimeOffset.UtcNow;
        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
            FirstSeen = t, LastSeen = t
        };
        var zoneResult = new ZoneEvaluationResult([], [],
            [new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t }]);
        var ctx = MakeContext([track], zoneResult, t);

        var configs = new List<PrimitiveConfig>
        {
            new("person_in_checkout", PrimitiveType.Presence, "person", "z1", null, "cam-1")
        };

        var result = new DefaultPrimitiveExtractor().Extract(ctx, configs);

        Assert.Single(result);
        Assert.Equal(true, result[0].Value);
    }

    [Fact]
    public void Count_ThreePeopleInZone_ReturnsThree()
    {
        var t = DateTimeOffset.UtcNow;
        var tracks = Enumerable.Range(0, 3).Select(i => new TrackedObject
        {
            TrackId = $"t{i}", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
            FirstSeen = t, LastSeen = t
        }).ToList();
        var presences = tracks.Select(t => new ZonePresence
            { TrackId = t.TrackId, ZoneId = "z1", EnteredAt = DateTimeOffset.UtcNow }).ToList();
        var ctx = MakeContext(tracks, new ZoneEvaluationResult([], [], presences), t);

        var configs = new List<PrimitiveConfig>
        {
            new("queue_length", PrimitiveType.Count, "person", "z1", null, "cam-1")
        };

        var result = new DefaultPrimitiveExtractor().Extract(ctx, configs);

        Assert.Equal(3, result[0].Value);
    }

    [Fact]
    public void Speed_ReadsFromFeatureStore()
    {
        var t = DateTimeOffset.UtcNow;
        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
            FirstSeen = t, LastSeen = t
        };
        var ctx = MakeContext([track], timestamp: t);
        ctx.Features.Set("object_speed", 2.5, "t1");

        var configs = new List<PrimitiveConfig>
        {
            new("person_speed", PrimitiveType.Speed, "person", null, null, "cam-1")
        };

        var result = new DefaultPrimitiveExtractor().Extract(ctx, configs);

        Assert.Equal(2.5, result[0].Value);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~PrimitiveExtractorTests" -v n
```

Expected: FAIL — `The type or namespace name 'DefaultPrimitiveExtractor' could not be found`

**Step 3: Implement DefaultPrimitiveExtractor**

```csharp
// src/OpenEye.PipelineCore/Primitives/DefaultPrimitiveExtractor.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Primitives;

public class DefaultPrimitiveExtractor : IPrimitiveExtractor
{
    public IReadOnlyList<Primitive> Extract(FrameContext context, IReadOnlyList<PrimitiveConfig> configs)
    {
        var primitives = new List<Primitive>();

        foreach (var config in configs)
        {
            var matchingTracks = context.Tracks
                .Where(t => t.State == TrackState.Active && t.ClassLabel == config.ClassLabel)
                .ToList();

            // Filter by zone if specified
            if (config.ZoneId is not null && context.ZoneResult is not null)
            {
                var inZone = context.ZoneResult.ActivePresences
                    .Where(p => p.ZoneId == config.ZoneId)
                    .Select(p => p.TrackId)
                    .ToHashSet();
                matchingTracks = matchingTracks.Where(t => inZone.Contains(t.TrackId)).ToList();
            }

            object value = config.Type switch
            {
                PrimitiveType.Presence => matchingTracks.Count > 0,
                PrimitiveType.Absence => matchingTracks.Count == 0,
                PrimitiveType.Count => matchingTracks.Count,
                PrimitiveType.ZoneDuration => ComputeZoneDuration(context, matchingTracks, config.ZoneId),
                PrimitiveType.Speed => ComputeSpeed(context, matchingTracks),
                PrimitiveType.LineCrossed => CheckLineCrossed(context, matchingTracks, config.TripwireId),
                _ => false
            };

            primitives.Add(new Primitive(config.Name, value, context.Timestamp, context.SourceId));
        }

        return primitives;
    }

    private static double ComputeZoneDuration(FrameContext context, List<TrackedObject> tracks, string? zoneId)
    {
        if (zoneId is null || context.ZoneResult is null || tracks.Count == 0) return 0.0;
        var presence = context.ZoneResult.ActivePresences
            .Where(p => p.ZoneId == zoneId && tracks.Any(t => t.TrackId == p.TrackId))
            .OrderBy(p => p.EnteredAt)
            .FirstOrDefault();
        return presence is null ? 0.0 : (context.Timestamp - presence.EnteredAt).TotalSeconds;
    }

    private static double ComputeSpeed(FrameContext context, List<TrackedObject> tracks)
    {
        if (tracks.Count == 0) return 0.0;
        var track = tracks[0];
        return context.Features.TryGet<double>("object_speed", track.TrackId, out var speed) ? speed : 0.0;
    }

    private static bool CheckLineCrossed(FrameContext context, List<TrackedObject> tracks, string? tripwireId)
    {
        if (tripwireId is null || context.ZoneResult is null) return false;
        return context.ZoneResult.TripwireCrossings
            .Any(c => c.TripwireId == tripwireId && tracks.Any(t => t.TrackId == c.TrackId));
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~PrimitiveExtractorTests" -v n
```

Expected: PASS — 3 tests green

**Step 5: Commit**

```
feat: implement primitive extractor consuming Feature Store
```

---

### Task 10: Plugin-Based Rule Conditions

**Files:**
- Create: `src/OpenEye.PipelineCore/Rules/ConditionRegistry.cs`
- Create: `src/OpenEye.PipelineCore/Rules/Conditions/PresenceCondition.cs`
- Create: `src/OpenEye.PipelineCore/Rules/Conditions/AbsenceCondition.cs`
- Create: `src/OpenEye.PipelineCore/Rules/Conditions/DurationCondition.cs`
- Create: `src/OpenEye.PipelineCore/Rules/Conditions/CountAboveCondition.cs`
- Create: `src/OpenEye.PipelineCore/Rules/Conditions/LineCrossCondition.cs`
- Create: `src/OpenEye.PipelineCore/Rules/Conditions/SpeedCondition.cs`
- Test: `src/OpenEye.Tests/Rules/ConditionTests.cs`

**Step 1: Write the failing tests**

```csharp
// src/OpenEye.Tests/Rules/ConditionTests.cs
using OpenEye.Abstractions;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Rules.Conditions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Rules;

public class ConditionTests
{
    private static FrameContext MakeContext(DateTimeOffset? t = null) =>
        new()
        {
            SourceId = "cam-1",
            Timestamp = t ?? DateTimeOffset.UtcNow,
            Detections = []
        };

    private static RuleDefinition MakeRule(
        string condType, string? op = null, double? val = null,
        string? zoneId = null, string? tripwireId = null) =>
        new("rule-1", "test", "person", zoneId, tripwireId,
            [new ConditionConfig(condType, op, val)], "emit_event");

    [Fact]
    public void DurationCondition_ExceedsThreshold_ReturnsTrue()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = MakeContext(t);
        ctx.ZoneResult = new ZoneEvaluationResult([], [],
            [new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t.AddSeconds(-15) }]);

        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
            FirstSeen = t, LastSeen = t
        };

        var rule = MakeRule("duration", ">", 10, zoneId: "z1");
        var condition = new DurationCondition();
        Assert.True(condition.Evaluate(ctx, rule, track));
    }

    [Fact]
    public void DurationCondition_BelowThreshold_ReturnsFalse()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = MakeContext(t);
        ctx.ZoneResult = new ZoneEvaluationResult([], [],
            [new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t.AddSeconds(-5) }]);

        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
            FirstSeen = t, LastSeen = t
        };

        var rule = MakeRule("duration", ">", 10, zoneId: "z1");
        Assert.False(new DurationCondition().Evaluate(ctx, rule, track));
    }

    [Fact]
    public void CountAboveCondition_ExceedsThreshold_ReturnsTrue()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = MakeContext(t);
        ctx.Primitives = [new Primitive("queue_length", 6, t, "cam-1")];

        var rule = new RuleDefinition("rule-1", "test", "person", "z1", null,
            [new ConditionConfig("count_above", ">", 5, "queue_length")], "emit_event");

        Assert.True(new CountAboveCondition().Evaluate(ctx, rule, null));
    }

    [Fact]
    public void SpeedCondition_ReadsFromFeatureStore()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = MakeContext(t);
        ctx.Features.Set("object_speed", 3.5, "t1");

        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
            FirstSeen = t, LastSeen = t
        };

        var rule = MakeRule("speed", ">", 2.0);
        Assert.True(new SpeedCondition().Evaluate(ctx, rule, track));
    }

    [Fact]
    public void LineCrossCondition_DetectsCrossing()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = MakeContext(t);
        ctx.ZoneResult = new ZoneEvaluationResult([],
            [new TripwireCrossing("t1", "tw-1", t)], []);

        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
            FirstSeen = t, LastSeen = t
        };

        var rule = MakeRule("line_cross", tripwireId: "tw-1");
        Assert.True(new LineCrossCondition().Evaluate(ctx, rule, track));
    }

    [Fact]
    public void ConditionRegistry_ResolvesByType()
    {
        var registry = new ConditionRegistry([
            new DurationCondition(), new SpeedCondition()
        ]);

        Assert.IsType<DurationCondition>(registry.Get("duration"));
        Assert.IsType<SpeedCondition>(registry.Get("speed"));
    }

    [Fact]
    public void ConditionRegistry_UnknownType_Throws()
    {
        var registry = new ConditionRegistry([]);
        Assert.Throws<InvalidOperationException>(() => registry.Get("unknown"));
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~ConditionTests" -v n
```

Expected: FAIL — compilation errors

**Step 3: Implement ConditionRegistry**

```csharp
// src/OpenEye.PipelineCore/Rules/ConditionRegistry.cs
using OpenEye.Abstractions;

namespace OpenEye.PipelineCore.Rules;

public class ConditionRegistry(IEnumerable<IRuleCondition> conditions) : IConditionRegistry
{
    private readonly Dictionary<string, IRuleCondition> _conditions =
        conditions.ToDictionary(c => c.Type);

    public IRuleCondition Get(string type) =>
        _conditions.TryGetValue(type, out var c) ? c
            : throw new InvalidOperationException($"Unknown condition type: {type}");

    public void Register(IRuleCondition condition) =>
        _conditions[condition.Type] = condition;
}
```

**Step 4: Implement condition helper and all condition classes**

```csharp
// src/OpenEye.PipelineCore/Rules/Conditions/ConditionHelper.cs
namespace OpenEye.PipelineCore.Rules.Conditions;

internal static class ConditionHelper
{
    public static bool Compare(double actual, string? op, double? threshold)
    {
        if (threshold is null) return false;
        return op switch
        {
            ">" => actual > threshold.Value,
            ">=" => actual >= threshold.Value,
            "<" => actual < threshold.Value,
            "<=" => actual <= threshold.Value,
            "==" => Math.Abs(actual - threshold.Value) < 0.001,
            _ => false
        };
    }
}
```

```csharp
// src/OpenEye.PipelineCore/Rules/Conditions/PresenceCondition.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class PresenceCondition : IRuleCondition
{
    public string Type => "presence";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        if (rule.ZoneId is null || context.ZoneResult is null) return false;
        return context.ZoneResult.ActivePresences
            .Any(p => p.ZoneId == rule.ZoneId);
    }
}
```

```csharp
// src/OpenEye.PipelineCore/Rules/Conditions/AbsenceCondition.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class AbsenceCondition : IRuleCondition
{
    public string Type => "absence";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        if (rule.ZoneId is null || context.ZoneResult is null) return false;
        return !context.ZoneResult.ActivePresences
            .Any(p => p.ZoneId == rule.ZoneId);
    }
}
```

```csharp
// src/OpenEye.PipelineCore/Rules/Conditions/DurationCondition.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class DurationCondition : IRuleCondition
{
    public string Type => "duration";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        if (obj is null || rule.ZoneId is null || context.ZoneResult is null) return false;
        var presence = context.ZoneResult.ActivePresences
            .FirstOrDefault(p => p.TrackId == obj.TrackId && p.ZoneId == rule.ZoneId);
        if (presence is null) return false;

        var condition = rule.Conditions.First(c => c.Type == Type);
        var elapsed = (context.Timestamp - presence.EnteredAt).TotalSeconds;
        return ConditionHelper.Compare(elapsed, condition.Operator, condition.Value);
    }
}
```

```csharp
// src/OpenEye.PipelineCore/Rules/Conditions/CountAboveCondition.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class CountAboveCondition : IRuleCondition
{
    public string Type => "count_above";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        var condition = rule.Conditions.First(c => c.Type == Type);
        if (condition.FeatureName is null) return false;

        var primitive = context.Primitives.FirstOrDefault(p => p.Name == condition.FeatureName);
        if (primitive is null) return false;

        double actual = Convert.ToDouble(primitive.Value);
        return ConditionHelper.Compare(actual, condition.Operator, condition.Value);
    }
}
```

```csharp
// src/OpenEye.PipelineCore/Rules/Conditions/LineCrossCondition.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class LineCrossCondition : IRuleCondition
{
    public string Type => "line_cross";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        if (obj is null || rule.TripwireId is null || context.ZoneResult is null) return false;
        return context.ZoneResult.TripwireCrossings
            .Any(c => c.TrackId == obj.TrackId && c.TripwireId == rule.TripwireId);
    }
}
```

```csharp
// src/OpenEye.PipelineCore/Rules/Conditions/SpeedCondition.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class SpeedCondition : IRuleCondition
{
    public string Type => "speed";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        if (obj is null) return false;
        var condition = rule.Conditions.First(c => c.Type == Type);
        if (!context.Features.TryGet<double>("object_speed", obj.TrackId, out var speed))
            return false;
        return ConditionHelper.Compare(speed, condition.Operator, condition.Value);
    }
}
```

**Step 5: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~ConditionTests" -v n
```

Expected: PASS — 7 tests green

**Step 6: Commit**

```
feat: implement plugin-based rule conditions with DI-resolved registry
```

---

### Task 11: Temporal Buffer & Event Deduplicator

**Files:**
- Create: `src/OpenEye.PipelineCore/Rules/TemporalBuffer.cs`
- Create: `src/OpenEye.PipelineCore/Rules/EventDeduplicator.cs`
- Test: `src/OpenEye.Tests/Rules/TemporalBufferTests.cs`
- Test: `src/OpenEye.Tests/Rules/EventDeduplicatorTests.cs`

**Step 1: Write TemporalBuffer tests**

```csharp
// src/OpenEye.Tests/Rules/TemporalBufferTests.cs
using OpenEye.PipelineCore.Rules;

namespace OpenEye.Tests.Rules;

public class TemporalBufferTests
{
    [Fact]
    public void Sustained_TrueFor5Seconds_ReturnsTrue()
    {
        var buf = new TemporalBuffer(capacity: 100);
        var t = DateTimeOffset.UtcNow;
        for (int i = 0; i <= 50; i++)
            buf.Record(t.AddMilliseconds(i * 100), true);

        Assert.True(buf.CheckSustained(TimeSpan.FromSeconds(5), t.AddSeconds(5)));
    }

    [Fact]
    public void Sustained_InterruptedByFalse_ReturnsFalse()
    {
        var buf = new TemporalBuffer(capacity: 100);
        var t = DateTimeOffset.UtcNow;
        for (int i = 0; i < 30; i++)
            buf.Record(t.AddMilliseconds(i * 100), true);
        buf.Record(t.AddSeconds(3), false); // Interruption
        for (int i = 31; i <= 50; i++)
            buf.Record(t.AddMilliseconds(i * 100), true);

        Assert.False(buf.CheckSustained(TimeSpan.FromSeconds(5), t.AddSeconds(5)));
    }

    [Fact]
    public void Within_3OccurrencesIn10Seconds_ReturnsTrue()
    {
        var buf = new TemporalBuffer(capacity: 100);
        var t = DateTimeOffset.UtcNow;
        buf.Record(t, true);
        buf.Record(t.AddSeconds(3), true);
        buf.Record(t.AddSeconds(6), true);

        Assert.True(buf.CheckWithin(TimeSpan.FromSeconds(10), 3, t.AddSeconds(6)));
    }

    [Fact]
    public void Within_TooFewOccurrences_ReturnsFalse()
    {
        var buf = new TemporalBuffer(capacity: 100);
        var t = DateTimeOffset.UtcNow;
        buf.Record(t, true);
        buf.Record(t.AddSeconds(3), true);

        Assert.False(buf.CheckWithin(TimeSpan.FromSeconds(10), 3, t.AddSeconds(6)));
    }

    [Fact]
    public void NoTemporalConfig_ImmediateFire()
    {
        var buf = new TemporalBuffer(capacity: 100);
        buf.Record(DateTimeOffset.UtcNow, true);
        // With no sustained/within, single true is enough
        Assert.True(buf.CheckImmediate());
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~TemporalBufferTests" -v n
```

Expected: FAIL — `The type or namespace name 'TemporalBuffer' could not be found`

**Step 3: Implement TemporalBuffer**

```csharp
// src/OpenEye.PipelineCore/Rules/TemporalBuffer.cs
namespace OpenEye.PipelineCore.Rules;

public class TemporalBuffer(int capacity = 256)
{
    private readonly (DateTimeOffset Timestamp, bool Result)[] _ring = new (DateTimeOffset, bool)[capacity];
    private int _head;
    private int _count;

    public void Record(DateTimeOffset timestamp, bool result)
    {
        _ring[_head] = (timestamp, result);
        _head = (_head + 1) % capacity;
        if (_count < capacity) _count++;
    }

    public bool CheckSustained(TimeSpan duration, DateTimeOffset now)
    {
        var cutoff = now - duration;
        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + capacity) % capacity;
            var entry = _ring[idx];
            if (entry.Timestamp < cutoff) break;
            if (!entry.Result) return false;
        }
        // Must have at least one entry in the window
        if (_count == 0) return false;
        var oldest = _ring[(_head - _count + capacity) % capacity];
        var newest = _ring[(_head - 1 + capacity) % capacity];
        return newest.Timestamp >= cutoff && (newest.Timestamp - oldest.Timestamp) >= duration * 0.9;
    }

    public bool CheckWithin(TimeSpan window, int minOccurrences, DateTimeOffset now)
    {
        var cutoff = now - window;
        int count = 0;
        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + capacity) % capacity;
            var entry = _ring[idx];
            if (entry.Timestamp < cutoff) break;
            if (entry.Result) count++;
        }
        return count >= minOccurrences;
    }

    public bool CheckImmediate()
    {
        if (_count == 0) return false;
        return _ring[(_head - 1 + capacity) % capacity].Result;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~TemporalBufferTests" -v n
```

Expected: PASS — 5 tests green

**Step 5: Write EventDeduplicator tests**

```csharp
// src/OpenEye.Tests/Rules/EventDeduplicatorTests.cs
using OpenEye.PipelineCore.Rules;

namespace OpenEye.Tests.Rules;

public class EventDeduplicatorTests
{
    [Fact]
    public void SameRuleAndTrack_WithinCooldown_Suppressed()
    {
        var dedup = new EventDeduplicator();
        var t = DateTimeOffset.UtcNow;
        dedup.RecordFired("rule-1", "track-0", t);
        Assert.True(dedup.ShouldSuppress("rule-1", "track-0", TimeSpan.FromSeconds(30), t.AddSeconds(10)));
    }

    [Fact]
    public void SameRuleAndTrack_AfterCooldown_Allowed()
    {
        var dedup = new EventDeduplicator();
        var t = DateTimeOffset.UtcNow;
        dedup.RecordFired("rule-1", "track-0", t);
        Assert.False(dedup.ShouldSuppress("rule-1", "track-0", TimeSpan.FromSeconds(30), t.AddSeconds(31)));
    }

    [Fact]
    public void DifferentRule_Allowed()
    {
        var dedup = new EventDeduplicator();
        var t = DateTimeOffset.UtcNow;
        dedup.RecordFired("rule-1", "track-0", t);
        Assert.False(dedup.ShouldSuppress("rule-2", "track-0", TimeSpan.FromSeconds(30), t));
    }
}
```

**Step 6: Implement EventDeduplicator**

```csharp
// src/OpenEye.PipelineCore/Rules/EventDeduplicator.cs
namespace OpenEye.PipelineCore.Rules;

public class EventDeduplicator
{
    private readonly Dictionary<(string RuleId, string TrackId), DateTimeOffset> _lastFired = [];

    public bool ShouldSuppress(string ruleId, string trackId, TimeSpan cooldown, DateTimeOffset now)
    {
        if (_lastFired.TryGetValue((ruleId, trackId), out var lastTime))
            return (now - lastTime) < cooldown;
        return false;
    }

    public void RecordFired(string ruleId, string trackId, DateTimeOffset now)
    {
        _lastFired[(ruleId, trackId)] = now;
    }
}
```

**Step 7: Run all rule tests**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~Rules" -v n
```

Expected: PASS — 15 tests green (7 conditions + 5 temporal + 3 dedup)

**Step 8: Commit**

```
feat: implement temporal buffer and event deduplicator for rule engine
```

---

### Task 12: Rule Engine & InMemoryRuleStateStore

**Files:**
- Create: `src/OpenEye.PipelineCore/Rules/InMemoryRuleStateStore.cs`
- Create: `src/OpenEye.PipelineCore/Rules/DefaultRuleEngine.cs`
- Test: `src/OpenEye.Tests/Rules/RuleEngineTests.cs`

**Step 1: Write the failing tests**

```csharp
// src/OpenEye.Tests/Rules/RuleEngineTests.cs
using OpenEye.Abstractions;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Rules.Conditions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Rules;

public class RuleEngineTests
{
    private static DefaultRuleEngine MakeEngine()
    {
        var conditions = new IRuleCondition[]
        {
            new DurationCondition(), new CountAboveCondition(),
            new LineCrossCondition(), new SpeedCondition(),
            new PresenceCondition(), new AbsenceCondition()
        };
        var registry = new ConditionRegistry(conditions);
        var stateStore = new InMemoryRuleStateStore();
        return new DefaultRuleEngine(registry, stateStore);
    }

    [Fact]
    public void SimpleRule_NoTemporal_FiresImmediately()
    {
        var engine = MakeEngine();
        var t = DateTimeOffset.UtcNow;
        var ctx = new FrameContext
        {
            SourceId = "cam-1", Timestamp = t, Detections = [],
            Tracks = [new TrackedObject
            {
                TrackId = "t1", ClassLabel = "person",
                CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
                FirstSeen = t, LastSeen = t
            }],
            ZoneResult = new ZoneEvaluationResult([], [],
                [new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t.AddSeconds(-15) }])
        };

        var rules = new List<RuleDefinition>
        {
            new("rule-1", "loitering", "person", "z1", null,
                [new ConditionConfig("duration", ">", 10)], "emit_event")
        };

        var events = engine.Evaluate(ctx, rules);
        Assert.Single(events);
        Assert.Equal("rule-1", events[0].RuleId);
    }

    [Fact]
    public void Cooldown_PreventsRefiring()
    {
        var engine = MakeEngine();
        var t = DateTimeOffset.UtcNow;

        var makeCtx = (DateTimeOffset ts) => new FrameContext
        {
            SourceId = "cam-1", Timestamp = ts, Detections = [],
            Tracks = [new TrackedObject
            {
                TrackId = "t1", ClassLabel = "person",
                CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
                FirstSeen = t, LastSeen = ts
            }],
            ZoneResult = new ZoneEvaluationResult([], [],
                [new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t.AddSeconds(-20) }])
        };

        var rules = new List<RuleDefinition>
        {
            new("rule-1", "loitering", "person", "z1", null,
                [new ConditionConfig("duration", ">", 10)], "emit_event",
                Cooldown: TimeSpan.FromSeconds(60))
        };

        var events1 = engine.Evaluate(makeCtx(t), rules);
        Assert.Single(events1);

        var events2 = engine.Evaluate(makeCtx(t.AddSeconds(5)), rules);
        Assert.Empty(events2); // Within cooldown
    }

    [Fact]
    public void MultipleRules_EvaluatedIndependently()
    {
        var engine = MakeEngine();
        var t = DateTimeOffset.UtcNow;
        var ctx = new FrameContext
        {
            SourceId = "cam-1", Timestamp = t, Detections = [],
            Tracks = [new TrackedObject
            {
                TrackId = "t1", ClassLabel = "person",
                CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
                FirstSeen = t, LastSeen = t
            }],
            ZoneResult = new ZoneEvaluationResult([], [],
                [new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t.AddSeconds(-15) }])
        };
        ctx.Features.Set("object_speed", 3.0, "t1");

        var rules = new List<RuleDefinition>
        {
            new("rule-1", "loitering", "person", "z1", null,
                [new ConditionConfig("duration", ">", 10)], "emit_event"),
            new("rule-2", "speeding", "person", null, null,
                [new ConditionConfig("speed", ">", 2.0)], "emit_event")
        };

        var events = engine.Evaluate(ctx, rules);
        Assert.Equal(2, events.Count);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~RuleEngineTests" -v n
```

Expected: FAIL — `The type or namespace name 'DefaultRuleEngine' could not be found`

**Step 3: Implement InMemoryRuleStateStore**

```csharp
// src/OpenEye.PipelineCore/Rules/InMemoryRuleStateStore.cs
using System.Collections.Concurrent;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules;

public class InMemoryRuleStateStore : IRuleStateStore
{
    private readonly ConcurrentDictionary<(string RuleId, string TrackId), RuleState> _store = [];

    public RuleState? Get(string ruleId, string trackId) =>
        _store.GetValueOrDefault((ruleId, trackId));

    public void Set(string ruleId, string trackId, RuleState state) =>
        _store[(ruleId, trackId)] = state;

    public void Remove(string ruleId, string trackId) =>
        _store.TryRemove((ruleId, trackId), out _);

    public void RemoveByTrack(string trackId)
    {
        var keys = _store.Keys.Where(k => k.TrackId == trackId).ToList();
        foreach (var key in keys) _store.TryRemove(key, out _);
    }
}
```

**Step 4: Implement DefaultRuleEngine**

```csharp
// src/OpenEye.PipelineCore/Rules/DefaultRuleEngine.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules;

public class DefaultRuleEngine(IConditionRegistry conditionRegistry, IRuleStateStore stateStore) : IRuleEngine
{
    private readonly Dictionary<string, TemporalBuffer> _temporalBuffers = [];
    private readonly EventDeduplicator _deduplicator = new();

    public IReadOnlyList<Event> Evaluate(FrameContext context, IReadOnlyList<RuleDefinition> rules)
    {
        var events = new List<Event>();

        foreach (var rule in rules)
        {
            var matchingTracks = context.Tracks
                .Where(t => t.State == TrackState.Active && t.ClassLabel == rule.ObjectClass)
                .ToList();

            foreach (var track in matchingTracks)
            {
                bool allConditionsMet = rule.Conditions.All(condConfig =>
                {
                    var condition = conditionRegistry.Get(condConfig.Type);
                    return condition.Evaluate(context, rule, track);
                });

                var bufferKey = $"{rule.RuleId}:{track.TrackId}";
                if (!_temporalBuffers.TryGetValue(bufferKey, out var buffer))
                {
                    buffer = new TemporalBuffer();
                    _temporalBuffers[bufferKey] = buffer;
                }

                buffer.Record(context.Timestamp, allConditionsMet);

                bool shouldFire;
                if (rule.Sustained is not null)
                    shouldFire = buffer.CheckSustained(rule.Sustained.Value, context.Timestamp);
                else if (rule.Within is not null)
                    shouldFire = buffer.CheckWithin(rule.Within.Value, rule.MinOccurrences ?? 1, context.Timestamp);
                else
                    shouldFire = buffer.CheckImmediate();

                if (!shouldFire) continue;

                var cooldown = rule.Cooldown ?? TimeSpan.FromSeconds(30);
                if (_deduplicator.ShouldSuppress(rule.RuleId, track.TrackId, cooldown, context.Timestamp))
                    continue;

                _deduplicator.RecordFired(rule.RuleId, track.TrackId, context.Timestamp);

                events.Add(new Event(
                    EventId: Guid.NewGuid().ToString(),
                    EventType: rule.Name,
                    Timestamp: context.Timestamp,
                    SourceId: context.SourceId,
                    ZoneId: rule.ZoneId,
                    TrackedObjects: [track],
                    RuleId: rule.RuleId,
                    Metadata: new Dictionary<string, object> { ["action"] = rule.Action }
                ));
            }
        }

        // Clean up state for expired tracks
        foreach (var track in context.Tracks.Where(t => t.State == TrackState.Expired))
            stateStore.RemoveByTrack(track.TrackId);

        return events;
    }
}
```

**Step 5: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~RuleEngineTests" -v n
```

Expected: PASS — 3 tests green

**Step 6: Commit**

```
feat: implement rule engine with plugin conditions, temporal aggregation, and deduplication
```

---

## Phase 5: Pipeline Orchestration

### Task 13: Pipeline Orchestrator & LocalEventBus

**Files:**
- Create: `src/OpenEye.PipelineCore/Pipeline/LocalEventBus.cs`
- Create: `src/OpenEye.PipelineCore/Pipeline/PipelineOrchestrator.cs`
- Test: `src/OpenEye.Tests/Pipeline/PipelineOrchestratorTests.cs`

**Step 1: Implement LocalEventBus**

```csharp
// src/OpenEye.PipelineCore/Pipeline/LocalEventBus.cs
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Pipeline;

public class LocalEventBus : IGlobalEventBus
{
    private readonly Channel<Event> _channel = Channel.CreateUnbounded<Event>();

    public void Publish(Event evt) => _channel.Writer.TryWrite(evt);

    public async IAsyncEnumerable<Event> Subscribe(
        string? sourceFilter = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            if (sourceFilter is null || evt.SourceId == sourceFilter)
                yield return evt;
        }
    }
}
```

**Step 2: Write PipelineOrchestrator tests**

```csharp
// src/OpenEye.Tests/Pipeline/PipelineOrchestratorTests.cs
using OpenEye.Abstractions;
using OpenEye.PipelineCore.Features;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Rules.Conditions;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Pipeline;

public class PipelineOrchestratorTests
{
    private static PipelineOrchestrator MakeOrchestrator()
    {
        var tracker = new SortTracker();
        var zoneEval = new DefaultZoneEvaluator();
        var featureExtractors = new IFeatureExtractor[]
        {
            new ObjectFeatureExtractor(), new ZoneFeatureExtractor(), new TemporalFeatureExtractor()
        };
        var primitiveExtractor = new DefaultPrimitiveExtractor();
        var conditions = new IRuleCondition[]
        {
            new DurationCondition(), new CountAboveCondition(),
            new LineCrossCondition(), new SpeedCondition(),
            new PresenceCondition(), new AbsenceCondition()
        };
        var ruleEngine = new DefaultRuleEngine(new ConditionRegistry(conditions), new InMemoryRuleStateStore());
        return new PipelineOrchestrator(tracker, zoneEval, featureExtractors, primitiveExtractor, ruleEngine);
    }

    [Fact]
    public void FullPipeline_PersonEntersZone_EventFires()
    {
        var orch = MakeOrchestrator();
        var zone = new Zone("z1", "cam-1",
            [new(0.2, 0.2), new(0.8, 0.2), new(0.8, 0.8), new(0.2, 0.8)]);
        var rule = new RuleDefinition("rule-1", "zone_entry", "person", "z1", null,
            [new ConditionConfig("presence")], "emit_event");

        orch.ReloadConfig([zone], [], [], [rule]);

        var t = DateTimeOffset.UtcNow;
        var det = new Detection("person", new BoundingBox(0.45, 0.45, 0.1, 0.1), 0.9, t, "cam-1");

        var events = orch.ProcessFrame("cam-1", [det], t);
        Assert.Single(events);
        Assert.Equal("rule-1", events[0].RuleId);
    }

    [Fact]
    public void FullPipeline_NoDetections_NoEvents()
    {
        var orch = MakeOrchestrator();
        orch.ReloadConfig([], [], [], []);

        var events = orch.ProcessFrame("cam-1", [], DateTimeOffset.UtcNow);
        Assert.Empty(events);
    }

    [Fact]
    public void GetRequiredClasses_ExtractsFromRules()
    {
        var orch = MakeOrchestrator();
        orch.ReloadConfig([], [],
            [new PrimitiveConfig("p1", PrimitiveType.Presence, "person", null, null, "cam-1")],
            [new RuleDefinition("r1", "test", "forklift", null, null, [], "emit_event")]);

        var classes = orch.GetRequiredClasses();
        Assert.Contains("person", classes);
        Assert.Contains("forklift", classes);
    }
}
```

**Step 3: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~PipelineOrchestratorTests" -v n
```

Expected: FAIL — `The type or namespace name 'PipelineOrchestrator' could not be found`

**Step 4: Implement PipelineOrchestrator**

```csharp
// src/OpenEye.PipelineCore/Pipeline/PipelineOrchestrator.cs
using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Pipeline;

public class PipelineOrchestrator(
    IObjectTracker tracker,
    IZoneEvaluator zoneEvaluator,
    IEnumerable<IFeatureExtractor> featureExtractors,
    IPrimitiveExtractor primitiveExtractor,
    IRuleEngine ruleEngine)
{
    private IReadOnlyList<Zone> _zones = [];
    private IReadOnlyList<Tripwire> _tripwires = [];
    private IReadOnlyList<PrimitiveConfig> _primitiveConfigs = [];
    private IReadOnlyList<RuleDefinition> _rules = [];

    public void ReloadConfig(
        IReadOnlyList<Zone> zones,
        IReadOnlyList<Tripwire> tripwires,
        IReadOnlyList<PrimitiveConfig> primitiveConfigs,
        IReadOnlyList<RuleDefinition> rules)
    {
        _zones = zones;
        _tripwires = tripwires;
        _primitiveConfigs = primitiveConfigs;
        _rules = rules;
    }

    public IReadOnlyList<Event> ProcessFrame(
        string cameraId, IReadOnlyList<Detection> detections, DateTimeOffset timestamp)
    {
        var context = new FrameContext
        {
            SourceId = cameraId,
            Timestamp = timestamp,
            Detections = detections
        };

        // Stage 1: Object Tracking
        context.Tracks = tracker.Update(detections, timestamp);

        // Stage 2: Zone Evaluation
        context.ZoneResult = zoneEvaluator.Evaluate(context.Tracks, _zones, _tripwires);

        // Stage 3: Feature Extraction → Feature Store
        foreach (var extractor in featureExtractors)
            extractor.Update(context);

        // Stage 4: Primitive Extraction (reads from Feature Store)
        context.Primitives = primitiveExtractor.Extract(context, _primitiveConfigs);

        // Stage 5: Rule Evaluation (plugin conditions read from context)
        context.Events = ruleEngine.Evaluate(context, _rules);

        return context.Events;
    }

    public IReadOnlySet<string> GetRequiredClasses()
    {
        var classes = new HashSet<string>();
        foreach (var p in _primitiveConfigs) classes.Add(p.ClassLabel);
        foreach (var r in _rules) classes.Add(r.ObjectClass);
        return classes;
    }
}
```

**Step 5: Run tests to verify they pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~PipelineOrchestratorTests" -v n
```

Expected: PASS — 3 tests green

**Step 6: Run ALL tests to verify nothing is broken**

```bash
dotnet test src/OpenEye.slnx -v n
```

Expected: PASS — all tests green

**Step 7: Commit**

```
feat: implement pipeline orchestrator with full staged pipeline and local event bus
```

---

## Phase 6: Peripheral Services — Frame Capture, Detection Bridge, Event Router

> Wire up the three peripheral worker services. Each reads from Redis Streams, processes data, and writes downstream.

### Task 14: Frame Capture Worker — RTSP/MJPEG Capture to Redis

**Files:**
- Create: `src/OpenEye.PipelineCore/Pipeline/RedisStreamPublisher.cs`
- Create: `src/OpenEye.PipelineCore/Pipeline/RedisStreamConsumer.cs`
- Modify: `src/OpenEye.FrameCapture/Worker.cs`
- Modify: `src/OpenEye.FrameCapture/Program.cs`
- Modify: `src/OpenEye.FrameCapture/OpenEye.FrameCapture.csproj`
- Test: `src/OpenEye.Tests/Services/FrameCaptureWorkerTests.cs`

**Step 1: Write Redis stream helpers (shared by all services)**

```csharp
// src/OpenEye.PipelineCore/Pipeline/RedisStreamPublisher.cs
using StackExchange.Redis;

namespace OpenEye.PipelineCore.Pipeline;

public class RedisStreamPublisher(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task PublishAsync(string streamKey, Dictionary<string, string> fields)
    {
        var entries = fields.Select(f => new NameValueEntry(f.Key, f.Value)).ToArray();
        await _db.StreamAddAsync(streamKey, entries, maxLength: 1000, useApproximateMaxLength: true);
    }
}
```

```csharp
// src/OpenEye.PipelineCore/Pipeline/RedisStreamConsumer.cs
using StackExchange.Redis;

namespace OpenEye.PipelineCore.Pipeline;

public class RedisStreamConsumer(IConnectionMultiplexer redis, string streamKey, string groupName, string consumerName)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private bool _groupCreated;

    public async Task EnsureGroupAsync()
    {
        if (_groupCreated) return;
        try
        {
            await _db.StreamCreateConsumerGroupAsync(streamKey, groupName, "0-0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists
        }
        _groupCreated = true;
    }

    public async Task<StreamEntry[]> ReadAsync(int count = 10)
    {
        await EnsureGroupAsync();
        var entries = await _db.StreamReadGroupAsync(streamKey, groupName, consumerName, ">", count);
        return entries;
    }

    public async Task AcknowledgeAsync(RedisValue messageId)
    {
        await _db.StreamAcknowledgeAsync(streamKey, groupName, messageId);
    }
}
```

**Step 2: Write FrameCapture Worker tests**

```csharp
// src/OpenEye.Tests/Services/FrameCaptureWorkerTests.cs
namespace OpenEye.Tests.Services;

public class FrameCaptureWorkerTests
{
    [Fact]
    public void Worker_IsBackgroundService()
    {
        // Verify the Worker class inherits from BackgroundService
        var workerType = typeof(OpenEye.FrameCapture.Worker);
        Assert.True(typeof(Microsoft.Extensions.Hosting.BackgroundService).IsAssignableFrom(workerType));
    }
}
```

**Step 3: Run tests to verify they fail**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~FrameCaptureWorkerTests" -v n
```

Expected: FAIL — Worker is a placeholder, test should still pass since class exists but we need to verify compilation.

Actually this will pass since Worker already exists. Let's verify:

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~FrameCaptureWorkerTests" -v n
```

Expected: PASS — 1 test green (Worker already exists as BackgroundService)

**Step 4: Implement Frame Capture Worker**

```csharp
// src/OpenEye.FrameCapture/Worker.cs
using System.Text.Json;
using OpenCvSharp;
using OpenEye.PipelineCore.Pipeline;
using StackExchange.Redis;

namespace OpenEye.FrameCapture;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration config,
    IConnectionMultiplexer redis) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cameras = config.GetSection("Cameras").Get<CameraEntry[]>() ?? [];
        if (cameras.Length == 0)
        {
            logger.LogWarning("No cameras configured. Waiting...");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        var tasks = cameras.Select(c => CaptureLoop(c, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task CaptureLoop(CameraEntry camera, CancellationToken ct)
    {
        var publisher = new RedisStreamPublisher(redis);
        var streamKey = $"frames:{camera.Id}";

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var capture = new VideoCapture(camera.Url);
                if (!capture.IsOpened())
                {
                    logger.LogError("Cannot open camera {CameraId} at {Url}", camera.Id, camera.Url);
                    await Task.Delay(5000, ct);
                    continue;
                }

                logger.LogInformation("Capturing from {CameraId} at {Url}", camera.Id, camera.Url);
                using var frame = new Mat();
                long frameIndex = 0;

                while (!ct.IsCancellationRequested && capture.Read(frame))
                {
                    if (frame.Empty()) continue;

                    var jpegBytes = frame.ImEncode(".jpg", new ImageEncodingParam(ImwriteFlags.JpegQuality, 80));
                    var base64 = Convert.ToBase64String(jpegBytes);

                    await publisher.PublishAsync(streamKey, new Dictionary<string, string>
                    {
                        ["camera_id"] = camera.Id,
                        ["frame_index"] = (frameIndex++).ToString(),
                        ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                        ["image"] = base64
                    });

                    // Respect target FPS
                    if (camera.TargetFps > 0)
                        await Task.Delay(1000 / camera.TargetFps, ct);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Error capturing from {CameraId}, reconnecting...", camera.Id);
                await Task.Delay(5000, ct);
            }
        }
    }
}

public record CameraEntry
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public int TargetFps { get; init; } = 5;
}
```

**Step 5: Update FrameCapture Program.cs**

```csharp
// src/OpenEye.FrameCapture/Program.cs
using OpenEye.FrameCapture;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redisConn = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

**Step 6: Add StackExchange.Redis to FrameCapture csproj**

Add `<PackageReference Include="StackExchange.Redis" Version="2.11.8" />` to the `<ItemGroup>` with other packages, and add a project reference to PipelineCore for the Redis helpers.

**Step 7: Build to verify compilation**

```bash
dotnet build src/OpenEye.FrameCapture -v n
```

Expected: Build succeeded

**Step 8: Run tests to verify pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~FrameCaptureWorkerTests" -v n
```

Expected: PASS — 1 test green

**Step 9: Commit**

```
feat: implement frame capture worker with RTSP/MJPEG to Redis stream publishing
```

---

### Task 15: Detection Bridge Worker — Frame to Roboflow Inference

**Files:**
- Modify: `src/OpenEye.DetectionBridge/Worker.cs`
- Modify: `src/OpenEye.DetectionBridge/Program.cs`
- Modify: `src/OpenEye.DetectionBridge/OpenEye.DetectionBridge.csproj`
- Test: `src/OpenEye.Tests/Services/DetectionBridgeWorkerTests.cs`

**Step 1: Write Detection Bridge tests**

```csharp
// src/OpenEye.Tests/Services/DetectionBridgeWorkerTests.cs
namespace OpenEye.Tests.Services;

public class DetectionBridgeWorkerTests
{
    [Fact]
    public void Worker_IsBackgroundService()
    {
        var workerType = typeof(OpenEye.DetectionBridge.Worker);
        Assert.True(typeof(Microsoft.Extensions.Hosting.BackgroundService).IsAssignableFrom(workerType));
    }
}
```

**Step 2: Run tests to verify pass (Worker class exists)**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~DetectionBridgeWorkerTests" -v n
```

Expected: PASS — 1 test green

**Step 3: Implement Detection Bridge Worker**

```csharp
// src/OpenEye.DetectionBridge/Worker.cs
using System.Net.Http.Json;
using System.Text.Json;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.Shared.Models;
using StackExchange.Redis;

namespace OpenEye.DetectionBridge;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration config,
    IConnectionMultiplexer redis,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cameraIds = config.GetSection("CameraIds").Get<string[]>() ?? [];
        if (cameraIds.Length == 0)
        {
            logger.LogWarning("No camera IDs configured. Waiting...");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        var tasks = cameraIds.Select(id => ConsumeLoop(id, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ConsumeLoop(string cameraId, CancellationToken ct)
    {
        var consumer = new RedisStreamConsumer(redis, $"frames:{cameraId}", "detection-bridge", $"worker-{cameraId}");
        var publisher = new RedisStreamPublisher(redis);
        var roboflowUrl = config["Roboflow:Url"] ?? "http://localhost:9001";
        var roboflowApiKey = config["Roboflow:ApiKey"] ?? "";
        var modelId = config["Roboflow:ModelId"] ?? "yolov8n-640";
        var httpClient = httpClientFactory.CreateClient();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var entries = await consumer.ReadAsync(1);
                if (entries.Length == 0)
                {
                    await Task.Delay(50, ct);
                    continue;
                }

                foreach (var entry in entries)
                {
                    var image = entry["image"].ToString();
                    var frameIndex = entry["frame_index"].ToString();
                    var timestamp = entry["timestamp"].ToString();

                    // Call Roboflow inference
                    var response = await httpClient.PostAsJsonAsync(
                        $"{roboflowUrl}/{modelId}?api_key={roboflowApiKey}",
                        new { image },
                        ct);

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync(ct);
                        await publisher.PublishAsync($"detections:{cameraId}", new Dictionary<string, string>
                        {
                            ["camera_id"] = cameraId,
                            ["frame_index"] = frameIndex,
                            ["timestamp"] = timestamp,
                            ["detections"] = result
                        });
                    }
                    else
                    {
                        logger.LogWarning("Roboflow returned {Status} for camera {Camera}",
                            response.StatusCode, cameraId);
                    }

                    await consumer.AcknowledgeAsync(entry.Id);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Error processing frames for {CameraId}", cameraId);
                await Task.Delay(1000, ct);
            }
        }
    }
}
```

**Step 4: Update DetectionBridge Program.cs**

```csharp
// src/OpenEye.DetectionBridge/Program.cs
using OpenEye.DetectionBridge;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redisConn = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddHttpClient();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

**Step 5: Add packages to DetectionBridge csproj**

Add `StackExchange.Redis 2.11.8` and project reference to `OpenEye.PipelineCore`.

**Step 6: Build to verify compilation**

```bash
dotnet build src/OpenEye.DetectionBridge -v n
```

Expected: Build succeeded

**Step 7: Run tests**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~DetectionBridgeWorkerTests" -v n
```

Expected: PASS — 1 test green

**Step 8: Commit**

```
feat: implement detection bridge worker with Roboflow inference integration
```

---

### Task 16: Event Router Worker — Event Persistence & Forwarding

**Files:**
- Modify: `src/OpenEye.EventRouter/Worker.cs`
- Modify: `src/OpenEye.EventRouter/Program.cs`
- Modify: `src/OpenEye.EventRouter/OpenEye.EventRouter.csproj`
- Test: `src/OpenEye.Tests/Services/EventRouterWorkerTests.cs`

**Step 1: Write Event Router tests**

```csharp
// src/OpenEye.Tests/Services/EventRouterWorkerTests.cs
namespace OpenEye.Tests.Services;

public class EventRouterWorkerTests
{
    [Fact]
    public void Worker_IsBackgroundService()
    {
        var workerType = typeof(OpenEye.EventRouter.Worker);
        Assert.True(typeof(Microsoft.Extensions.Hosting.BackgroundService).IsAssignableFrom(workerType));
    }
}
```

**Step 2: Run tests to verify pass**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~EventRouterWorkerTests" -v n
```

Expected: PASS — 1 test green

**Step 3: Implement Event Router Worker**

```csharp
// src/OpenEye.EventRouter/Worker.cs
using System.Text.Json;
using Dapper;
using Npgsql;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.Shared.Models;
using StackExchange.Redis;

namespace OpenEye.EventRouter;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration config,
    IConnectionMultiplexer redis) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new RedisStreamConsumer(redis, "events", "event-router", "worker-0");
        var connString = config.GetConnectionString("openeye") ?? "";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await consumer.ReadAsync(10);
                if (entries.Length == 0)
                {
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                {
                    var eventJson = entry["event"].ToString();
                    var evt = JsonSerializer.Deserialize<Event>(eventJson);
                    if (evt is null) continue;

                    // Persist to PostgreSQL
                    if (!string.IsNullOrEmpty(connString))
                    {
                        await using var conn = new NpgsqlConnection(connString);
                        await conn.ExecuteAsync(
                            """
                            INSERT INTO events (event_id, event_type, timestamp, source_id, zone_id, rule_id, tracked_objects, metadata)
                            VALUES (@EventId, @EventType, @Timestamp, @SourceId, @ZoneId, @RuleId, @TrackedObjects::jsonb, @Metadata::jsonb)
                            ON CONFLICT (event_id) DO NOTHING
                            """,
                            new
                            {
                                evt.EventId,
                                evt.EventType,
                                Timestamp = evt.Timestamp.UtcDateTime,
                                evt.SourceId,
                                evt.ZoneId,
                                evt.RuleId,
                                TrackedObjects = JsonSerializer.Serialize(evt.TrackedObjects),
                                Metadata = evt.Metadata is not null ? JsonSerializer.Serialize(evt.Metadata) : null
                            });
                    }

                    logger.LogInformation("Event {EventId} ({EventType}) from {Source} persisted",
                        evt.EventId, evt.EventType, evt.SourceId);

                    await consumer.AcknowledgeAsync(entry.Id);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error routing events");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
```

**Step 4: Update EventRouter Program.cs**

```csharp
// src/OpenEye.EventRouter/Program.cs
using OpenEye.EventRouter;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redisConn = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

**Step 5: Add packages to EventRouter csproj**

Add `StackExchange.Redis 2.11.8`, `Npgsql 10.0.1`, `Dapper 2.1.72`, and project reference to `OpenEye.PipelineCore`.

**Step 6: Build to verify compilation**

```bash
dotnet build src/OpenEye.EventRouter -v n
```

Expected: Build succeeded

**Step 7: Run tests**

```bash
dotnet test src/OpenEye.Tests --filter "FullyQualifiedName~EventRouterWorkerTests" -v n
```

Expected: PASS — 1 test green

**Step 8: Commit**

```
feat: implement event router worker with PostgreSQL persistence
```

---

### Task 17: Pipeline Core Worker — Detection Consumer to Pipeline

**Files:**
- Modify: `src/OpenEye.PipelineCore/Worker.cs`
- Modify: `src/OpenEye.PipelineCore/Program.cs`
- Modify: `src/OpenEye.PipelineCore/OpenEye.PipelineCore.csproj`

**Step 1: Implement Pipeline Core Worker**

```csharp
// src/OpenEye.PipelineCore/Worker.cs
using System.Text.Json;
using OpenEye.Abstractions;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.Shared.Models;
using StackExchange.Redis;

namespace OpenEye.PipelineCore;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration config,
    IConnectionMultiplexer redis,
    PipelineOrchestrator orchestrator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cameraIds = config.GetSection("CameraIds").Get<string[]>() ?? [];
        if (cameraIds.Length == 0)
        {
            logger.LogWarning("No camera IDs configured. Waiting...");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        // Publish required class filter to Redis for detection-bridge
        var classFilter = orchestrator.GetRequiredClasses();
        var db = redis.GetDatabase();
        await db.StringSetAsync("config:class-filter", JsonSerializer.Serialize(classFilter));

        var tasks = cameraIds.Select(id => ConsumeLoop(id, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ConsumeLoop(string cameraId, CancellationToken ct)
    {
        var consumer = new RedisStreamConsumer(redis, $"detections:{cameraId}", "pipeline-core", $"worker-{cameraId}");
        var publisher = new RedisStreamPublisher(redis);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var entries = await consumer.ReadAsync(1);
                if (entries.Length == 0)
                {
                    await Task.Delay(50, ct);
                    continue;
                }

                foreach (var entry in entries)
                {
                    var detectionsJson = entry["detections"].ToString();
                    var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(entry["timestamp"].ToString()));
                    var frameIndex = long.Parse(entry["frame_index"].ToString());

                    var detections = ParseDetections(detectionsJson, cameraId, timestamp, frameIndex);
                    var events = orchestrator.Process(cameraId, detections, timestamp);

                    // Publish events to Redis stream
                    foreach (var evt in events)
                    {
                        await publisher.PublishAsync("events", new Dictionary<string, string>
                        {
                            ["event"] = JsonSerializer.Serialize(evt)
                        });
                        logger.LogInformation("Published event {EventId} ({EventType})", evt.EventId, evt.EventType);
                    }

                    await consumer.AcknowledgeAsync(entry.Id);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Error processing detections for {CameraId}", cameraId);
                await Task.Delay(1000, ct);
            }
        }
    }

    private static List<Detection> ParseDetections(string json, string sourceId, DateTimeOffset timestamp, long frameIndex)
    {
        // Parse Roboflow inference response format
        var detections = new List<Detection>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("predictions", out var predictions))
            {
                foreach (var pred in predictions.EnumerateArray())
                {
                    var x = pred.GetProperty("x").GetDouble();
                    var y = pred.GetProperty("y").GetDouble();
                    var w = pred.GetProperty("width").GetDouble();
                    var h = pred.GetProperty("height").GetDouble();
                    var label = pred.GetProperty("class").GetString() ?? "unknown";
                    var confidence = pred.GetProperty("confidence").GetDouble();

                    detections.Add(new Detection(
                        label,
                        new BoundingBox(x - w / 2, y - h / 2, w, h),
                        confidence,
                        timestamp,
                        sourceId,
                        frameIndex));
                }
            }
        }
        catch
        {
            // If parsing fails, return empty list
        }
        return detections;
    }
}
```

**Step 2: Update PipelineCore Program.cs with full DI wiring**

```csharp
// src/OpenEye.PipelineCore/Program.cs
using OpenEye.Abstractions;
using OpenEye.PipelineCore;
using OpenEye.PipelineCore.Features;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

// Redis
var redisConn = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));

// Tracking
builder.Services.AddSingleton<IObjectTracker, SortTracker>();

// Zone evaluation
builder.Services.AddSingleton<IZoneEvaluator, DefaultZoneEvaluator>();

// Feature extractors
builder.Services.AddSingleton<IFeatureExtractor, ObjectFeatureExtractor>();
builder.Services.AddSingleton<IFeatureExtractor, ZoneFeatureExtractor>();
builder.Services.AddSingleton<IFeatureExtractor, TemporalFeatureExtractor>();

// Primitive extraction
builder.Services.AddSingleton<IPrimitiveExtractor, DefaultPrimitiveExtractor>();

// Rule conditions (plugin-based)
builder.Services.AddSingleton<IRuleCondition, DurationCondition>();
builder.Services.AddSingleton<IRuleCondition, CountAboveCondition>();
builder.Services.AddSingleton<IRuleCondition, LineCrossCondition>();
builder.Services.AddSingleton<IRuleCondition, SpeedCondition>();
builder.Services.AddSingleton<IRuleCondition, PresenceCondition>();
builder.Services.AddSingleton<IRuleCondition, AbsenceCondition>();
builder.Services.AddSingleton<IConditionRegistry, ConditionRegistry>();

// Rule engine
builder.Services.AddSingleton<IRuleEngine, RuleEngine>();

// Pipeline
builder.Services.AddSingleton<IEventBus, LocalEventBus>();
builder.Services.AddSingleton<PipelineOrchestrator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

**Step 3: Add StackExchange.Redis to PipelineCore csproj**

Add `<PackageReference Include="StackExchange.Redis" Version="2.11.8" />` to the PipelineCore csproj.

**Step 4: Build to verify compilation**

```bash
dotnet build src/OpenEye.PipelineCore -v n
```

Expected: Build succeeded

**Step 5: Run ALL tests**

```bash
dotnet test src/OpenEye.slnx -v n
```

Expected: PASS — all tests green

**Step 6: Commit**

```
feat: implement pipeline core worker with full DI wiring and detection parsing
```

---

## Phase 7: Dashboard Backend — Next.js Scaffolding, Prisma, API Routes

> Create the Next.js dashboard with Prisma ORM, REST API routes for cameras, zones, rules, and events.

### Task 18: Dashboard Scaffolding & Prisma Schema

**Files:**
- Create: `dashboard/package.json`
- Create: `dashboard/tsconfig.json`
- Create: `dashboard/next.config.ts`
- Create: `dashboard/tailwind.config.ts`
- Create: `dashboard/postcss.config.mjs`
- Create: `dashboard/prisma/schema.prisma`
- Create: `dashboard/src/app/layout.tsx`
- Create: `dashboard/src/app/page.tsx`
- Create: `dashboard/src/app/globals.css`
- Create: `dashboard/src/lib/prisma.ts`

**Step 1: Create `dashboard/package.json`**

```json
{
  "name": "openeye-dashboard",
  "version": "0.1.0",
  "private": true,
  "scripts": {
    "dev": "next dev --port 3000",
    "build": "next build",
    "start": "next start",
    "lint": "next lint",
    "db:push": "prisma db push",
    "db:generate": "prisma generate",
    "db:studio": "prisma studio"
  },
  "dependencies": {
    "next": "^15.3.0",
    "react": "^19.1.0",
    "react-dom": "^19.1.0",
    "@prisma/client": "^6.6.0"
  },
  "devDependencies": {
    "prisma": "^6.6.0",
    "typescript": "^5.8.0",
    "@types/node": "^22.0.0",
    "@types/react": "^19.1.0",
    "@types/react-dom": "^19.1.0",
    "tailwindcss": "^4.1.0",
    "@tailwindcss/postcss": "^4.1.0",
    "postcss": "^8.5.0"
  }
}
```

**Step 2: Create `dashboard/prisma/schema.prisma`**

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
  id        String   @id @default(cuid())
  name      String
  url       String
  targetFps Int      @default(5) @map("target_fps")
  enabled   Boolean  @default(true)
  createdAt DateTime @default(now()) @map("created_at")
  updatedAt DateTime @updatedAt @map("updated_at")
  zones     Zone[]
  rules     Rule[]

  @@map("cameras")
}

model Zone {
  id       String @id @default(cuid())
  name     String
  cameraId String @map("camera_id")
  camera   Camera @relation(fields: [cameraId], references: [id], onDelete: Cascade)
  polygon  Json   // Array of {x, y} points
  type     String @default("zone") // "zone" or "tripwire"

  createdAt DateTime @default(now()) @map("created_at")
  updatedAt DateTime @updatedAt @map("updated_at")

  @@map("zones")
}

model Rule {
  id          String  @id @default(cuid())
  name        String
  cameraId    String  @map("camera_id")
  camera      Camera  @relation(fields: [cameraId], references: [id], onDelete: Cascade)
  objectClass String  @map("object_class")
  zoneId      String? @map("zone_id")
  enabled     Boolean @default(true)

  conditions Json // Array of ConditionConfig: [{type, params}]
  logic      String @default("all") // "all" or "any"
  cooldown   Int    @default(30) // seconds

  createdAt DateTime @default(now()) @map("created_at")
  updatedAt DateTime @updatedAt @map("updated_at")

  @@map("rules")
}

model Event {
  id             String   @id @map("event_id")
  eventType      String   @map("event_type")
  timestamp      DateTime
  sourceId       String   @map("source_id")
  zoneId         String?  @map("zone_id")
  ruleId         String   @map("rule_id")
  trackedObjects Json     @map("tracked_objects")
  metadata       Json?

  createdAt DateTime @default(now()) @map("created_at")

  @@map("events")
}
```

**Step 3: Create `dashboard/src/lib/prisma.ts`**

```typescript
// dashboard/src/lib/prisma.ts
import { PrismaClient } from "@prisma/client";

const globalForPrisma = globalThis as unknown as { prisma: PrismaClient };

export const prisma = globalForPrisma.prisma ?? new PrismaClient();

if (process.env.NODE_ENV !== "production") globalForPrisma.prisma = prisma;
```

**Step 4: Create `dashboard/next.config.ts`**

```typescript
// dashboard/next.config.ts
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
};

export default nextConfig;
```

**Step 5: Create `dashboard/tsconfig.json`**

```json
{
  "compilerOptions": {
    "target": "ES2017",
    "lib": ["dom", "dom.iterable", "esnext"],
    "allowJs": true,
    "skipLibCheck": true,
    "strict": true,
    "noEmit": true,
    "esModuleInterop": true,
    "module": "esnext",
    "moduleResolution": "bundler",
    "resolveJsonModule": true,
    "isolatedModules": true,
    "jsx": "preserve",
    "incremental": true,
    "plugins": [{ "name": "next" }],
    "paths": { "@/*": ["./src/*"] }
  },
  "include": ["next-env.d.ts", "**/*.ts", "**/*.tsx", ".next/types/**/*.ts"],
  "exclude": ["node_modules"]
}
```

**Step 6: Create `dashboard/postcss.config.mjs`**

```javascript
// dashboard/postcss.config.mjs
const config = {
  plugins: {
    "@tailwindcss/postcss": {},
  },
};

export default config;
```

**Step 7: Create `dashboard/tailwind.config.ts`**

```typescript
// dashboard/tailwind.config.ts
import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{js,ts,jsx,tsx,mdx}"],
  theme: {
    extend: {},
  },
  plugins: [],
};

export default config;
```

**Step 8: Create `dashboard/src/app/globals.css`**

```css
/* dashboard/src/app/globals.css */
@import "tailwindcss";
```

**Step 9: Create `dashboard/src/app/layout.tsx`**

```tsx
// dashboard/src/app/layout.tsx
import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "OpenEye Dashboard",
  description: "Video analytics monitoring and rule configuration",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="bg-gray-50 text-gray-900 min-h-screen">{children}</body>
    </html>
  );
}
```

**Step 10: Create `dashboard/src/app/page.tsx`**

```tsx
// dashboard/src/app/page.tsx
export default function Home() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center p-24">
      <h1 className="text-4xl font-bold">OpenEye Dashboard</h1>
      <p className="mt-4 text-gray-600">Video analytics monitoring and configuration</p>
    </main>
  );
}
```

**Step 11: Install dependencies and verify**

```bash
cd dashboard && npm install && npx prisma generate && npm run build
```

Expected: Build succeeds, Prisma client generated

**Step 12: Commit**

```
feat: scaffold Next.js dashboard with Prisma schema for cameras, zones, rules, events
```

---

### Task 19: Dashboard API Routes

**Files:**
- Create: `dashboard/src/app/api/cameras/route.ts`
- Create: `dashboard/src/app/api/cameras/[id]/route.ts`
- Create: `dashboard/src/app/api/zones/route.ts`
- Create: `dashboard/src/app/api/zones/[id]/route.ts`
- Create: `dashboard/src/app/api/rules/route.ts`
- Create: `dashboard/src/app/api/rules/[id]/route.ts`
- Create: `dashboard/src/app/api/events/route.ts`

**Step 1: Create cameras API routes**

```typescript
// dashboard/src/app/api/cameras/route.ts
import { prisma } from "@/lib/prisma";
import { NextResponse } from "next/server";

export async function GET() {
  const cameras = await prisma.camera.findMany({
    include: { zones: true },
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(cameras);
}

export async function POST(request: Request) {
  const body = await request.json();
  const camera = await prisma.camera.create({
    data: {
      name: body.name,
      url: body.url,
      targetFps: body.targetFps ?? 5,
      enabled: body.enabled ?? true,
    },
  });
  return NextResponse.json(camera, { status: 201 });
}
```

```typescript
// dashboard/src/app/api/cameras/[id]/route.ts
import { prisma } from "@/lib/prisma";
import { NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const camera = await prisma.camera.findUnique({
    where: { id },
    include: { zones: true, rules: true },
  });
  if (!camera) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(camera);
}

export async function PUT(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const body = await request.json();
  const camera = await prisma.camera.update({
    where: { id },
    data: body,
  });
  return NextResponse.json(camera);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await prisma.camera.delete({ where: { id } });
  return NextResponse.json({ deleted: true });
}
```

**Step 2: Create zones API routes**

```typescript
// dashboard/src/app/api/zones/route.ts
import { prisma } from "@/lib/prisma";
import { NextResponse } from "next/server";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const cameraId = searchParams.get("cameraId");
  const zones = await prisma.zone.findMany({
    where: cameraId ? { cameraId } : undefined,
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(zones);
}

export async function POST(request: Request) {
  const body = await request.json();
  const zone = await prisma.zone.create({
    data: {
      name: body.name,
      cameraId: body.cameraId,
      polygon: body.polygon,
      type: body.type ?? "zone",
    },
  });
  return NextResponse.json(zone, { status: 201 });
}
```

```typescript
// dashboard/src/app/api/zones/[id]/route.ts
import { prisma } from "@/lib/prisma";
import { NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const zone = await prisma.zone.findUnique({ where: { id } });
  if (!zone) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(zone);
}

export async function PUT(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const body = await request.json();
  const zone = await prisma.zone.update({ where: { id }, data: body });
  return NextResponse.json(zone);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await prisma.zone.delete({ where: { id } });
  return NextResponse.json({ deleted: true });
}
```

**Step 3: Create rules API routes**

```typescript
// dashboard/src/app/api/rules/route.ts
import { prisma } from "@/lib/prisma";
import { NextResponse } from "next/server";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const cameraId = searchParams.get("cameraId");
  const rules = await prisma.rule.findMany({
    where: cameraId ? { cameraId } : undefined,
    orderBy: { createdAt: "desc" },
  });
  return NextResponse.json(rules);
}

export async function POST(request: Request) {
  const body = await request.json();
  const rule = await prisma.rule.create({
    data: {
      name: body.name,
      cameraId: body.cameraId,
      objectClass: body.objectClass,
      zoneId: body.zoneId ?? null,
      enabled: body.enabled ?? true,
      conditions: body.conditions,
      logic: body.logic ?? "all",
      cooldown: body.cooldown ?? 30,
    },
  });
  return NextResponse.json(rule, { status: 201 });
}
```

```typescript
// dashboard/src/app/api/rules/[id]/route.ts
import { prisma } from "@/lib/prisma";
import { NextResponse } from "next/server";

export async function GET(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const rule = await prisma.rule.findUnique({ where: { id } });
  if (!rule) return NextResponse.json({ error: "Not found" }, { status: 404 });
  return NextResponse.json(rule);
}

export async function PUT(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const body = await request.json();
  const rule = await prisma.rule.update({ where: { id }, data: body });
  return NextResponse.json(rule);
}

export async function DELETE(_: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  await prisma.rule.delete({ where: { id } });
  return NextResponse.json({ deleted: true });
}
```

**Step 4: Create events API route (read-only)**

```typescript
// dashboard/src/app/api/events/route.ts
import { prisma } from "@/lib/prisma";
import { NextResponse } from "next/server";

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const sourceId = searchParams.get("sourceId");
  const limit = parseInt(searchParams.get("limit") ?? "50", 10);
  const offset = parseInt(searchParams.get("offset") ?? "0", 10);

  const events = await prisma.event.findMany({
    where: sourceId ? { sourceId } : undefined,
    orderBy: { timestamp: "desc" },
    take: limit,
    skip: offset,
  });

  const total = await prisma.event.count({
    where: sourceId ? { sourceId } : undefined,
  });

  return NextResponse.json({ events, total, limit, offset });
}
```

**Step 5: Build to verify**

```bash
cd dashboard && npm run build
```

Expected: Build succeeds

**Step 6: Commit**

```
feat: add dashboard REST API routes for cameras, zones, rules, and events
```

---

## Phase 8: Dashboard Rule Builder UI

> Implement the visual drag-and-drop rule builder in the Next.js dashboard.

### Task 20: Rule Builder Components

**Files:**
- Create: `dashboard/src/components/rule-builder/types.ts`
- Create: `dashboard/src/components/rule-builder/ConditionCard.tsx`
- Create: `dashboard/src/components/rule-builder/ConditionPalette.tsx`
- Create: `dashboard/src/components/rule-builder/RuleCanvas.tsx`
- Create: `dashboard/src/components/rule-builder/RuleBuilderDialog.tsx`

**Step 1: Create shared types**

```typescript
// dashboard/src/components/rule-builder/types.ts
export interface ConditionConfig {
  type: string;
  params: Record<string, unknown>;
}

export interface RuleFormData {
  name: string;
  cameraId: string;
  objectClass: string;
  zoneId?: string;
  conditions: ConditionConfig[];
  logic: "all" | "any";
  cooldown: number;
  enabled: boolean;
}

export const CONDITION_TYPES = [
  {
    type: "duration",
    label: "Duration",
    description: "Object stays in zone for N seconds",
    params: { minSeconds: { type: "number", default: 5, label: "Min Seconds" } },
  },
  {
    type: "count_above",
    label: "Count Above",
    description: "More than N objects in zone",
    params: { threshold: { type: "number", default: 3, label: "Threshold" } },
  },
  {
    type: "line_cross",
    label: "Line Cross",
    description: "Object crosses a tripwire",
    params: { tripwireId: { type: "string", default: "", label: "Tripwire ID" } },
  },
  {
    type: "speed",
    label: "Speed",
    description: "Object speed exceeds threshold",
    params: {
      minSpeed: { type: "number", default: 0, label: "Min Speed" },
      maxSpeed: { type: "number", default: 100, label: "Max Speed" },
    },
  },
  {
    type: "presence",
    label: "Presence",
    description: "Object is present in zone",
    params: {},
  },
  {
    type: "absence",
    label: "Absence",
    description: "No objects in zone for N seconds",
    params: { timeoutSeconds: { type: "number", default: 30, label: "Timeout (s)" } },
  },
] as const;
```

**Step 2: Create ConditionCard component**

```tsx
// dashboard/src/components/rule-builder/ConditionCard.tsx
"use client";

import { ConditionConfig, CONDITION_TYPES } from "./types";

interface ConditionCardProps {
  condition: ConditionConfig;
  index: number;
  onUpdate: (index: number, condition: ConditionConfig) => void;
  onRemove: (index: number) => void;
}

export function ConditionCard({ condition, index, onUpdate, onRemove }: ConditionCardProps) {
  const typeDef = CONDITION_TYPES.find((t) => t.type === condition.type);
  if (!typeDef) return null;

  return (
    <div className="border rounded-lg p-4 bg-white shadow-sm" draggable>
      <div className="flex items-center justify-between mb-2">
        <h4 className="font-medium text-sm">{typeDef.label}</h4>
        <button
          onClick={() => onRemove(index)}
          className="text-red-500 hover:text-red-700 text-sm"
        >
          Remove
        </button>
      </div>
      <p className="text-xs text-gray-500 mb-3">{typeDef.description}</p>
      <div className="space-y-2">
        {Object.entries(typeDef.params).map(([key, paramDef]) => (
          <div key={key} className="flex items-center gap-2">
            <label className="text-xs text-gray-600 w-24">{paramDef.label}</label>
            <input
              type={paramDef.type}
              value={(condition.params[key] as string | number) ?? paramDef.default}
              onChange={(e) => {
                const value = paramDef.type === "number" ? Number(e.target.value) : e.target.value;
                onUpdate(index, {
                  ...condition,
                  params: { ...condition.params, [key]: value },
                });
              }}
              className="border rounded px-2 py-1 text-sm flex-1"
            />
          </div>
        ))}
      </div>
    </div>
  );
}
```

**Step 3: Create ConditionPalette component**

```tsx
// dashboard/src/components/rule-builder/ConditionPalette.tsx
"use client";

import { CONDITION_TYPES, ConditionConfig } from "./types";

interface ConditionPaletteProps {
  onAdd: (condition: ConditionConfig) => void;
}

export function ConditionPalette({ onAdd }: ConditionPaletteProps) {
  return (
    <div className="space-y-2">
      <h3 className="text-sm font-semibold text-gray-700">Conditions</h3>
      {CONDITION_TYPES.map((typeDef) => (
        <button
          key={typeDef.type}
          onClick={() => {
            const defaultParams: Record<string, unknown> = {};
            for (const [key, paramDef] of Object.entries(typeDef.params)) {
              defaultParams[key] = paramDef.default;
            }
            onAdd({ type: typeDef.type, params: defaultParams });
          }}
          className="w-full text-left border rounded p-2 hover:bg-blue-50 transition-colors"
        >
          <div className="text-sm font-medium">{typeDef.label}</div>
          <div className="text-xs text-gray-500">{typeDef.description}</div>
        </button>
      ))}
    </div>
  );
}
```

**Step 4: Create RuleCanvas component**

```tsx
// dashboard/src/components/rule-builder/RuleCanvas.tsx
"use client";

import { useState } from "react";
import { ConditionConfig } from "./types";
import { ConditionCard } from "./ConditionCard";
import { ConditionPalette } from "./ConditionPalette";

interface RuleCanvasProps {
  conditions: ConditionConfig[];
  logic: "all" | "any";
  onConditionsChange: (conditions: ConditionConfig[]) => void;
  onLogicChange: (logic: "all" | "any") => void;
}

export function RuleCanvas({ conditions, logic, onConditionsChange, onLogicChange }: RuleCanvasProps) {
  const handleAdd = (condition: ConditionConfig) => {
    onConditionsChange([...conditions, condition]);
  };

  const handleUpdate = (index: number, condition: ConditionConfig) => {
    const updated = [...conditions];
    updated[index] = condition;
    onConditionsChange(updated);
  };

  const handleRemove = (index: number) => {
    onConditionsChange(conditions.filter((_, i) => i !== index));
  };

  return (
    <div className="flex gap-6">
      {/* Palette (left sidebar) */}
      <div className="w-48 flex-shrink-0">
        <ConditionPalette onAdd={handleAdd} />
      </div>

      {/* Canvas (center) */}
      <div className="flex-1 min-h-[300px] border-2 border-dashed border-gray-200 rounded-lg p-4">
        <div className="flex items-center gap-2 mb-4">
          <span className="text-sm text-gray-600">Match</span>
          <select
            value={logic}
            onChange={(e) => onLogicChange(e.target.value as "all" | "any")}
            className="border rounded px-2 py-1 text-sm"
          >
            <option value="all">ALL conditions</option>
            <option value="any">ANY condition</option>
          </select>
        </div>

        {conditions.length === 0 ? (
          <div className="flex items-center justify-center h-48 text-gray-400">
            Click a condition from the palette to add it
          </div>
        ) : (
          <div className="space-y-3">
            {conditions.map((condition, index) => (
              <div key={index}>
                {index > 0 && (
                  <div className="text-center text-xs text-gray-400 py-1">
                    {logic === "all" ? "AND" : "OR"}
                  </div>
                )}
                <ConditionCard
                  condition={condition}
                  index={index}
                  onUpdate={handleUpdate}
                  onRemove={handleRemove}
                />
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
```

**Step 5: Create RuleBuilderDialog component**

```tsx
// dashboard/src/components/rule-builder/RuleBuilderDialog.tsx
"use client";

import { useState } from "react";
import { RuleFormData, ConditionConfig } from "./types";
import { RuleCanvas } from "./RuleCanvas";

interface RuleBuilderDialogProps {
  cameraId: string;
  initialData?: Partial<RuleFormData>;
  onSave: (data: RuleFormData) => void;
  onCancel: () => void;
}

export function RuleBuilderDialog({ cameraId, initialData, onSave, onCancel }: RuleBuilderDialogProps) {
  const [name, setName] = useState(initialData?.name ?? "");
  const [objectClass, setObjectClass] = useState(initialData?.objectClass ?? "person");
  const [zoneId, setZoneId] = useState(initialData?.zoneId ?? "");
  const [conditions, setConditions] = useState<ConditionConfig[]>(initialData?.conditions ?? []);
  const [logic, setLogic] = useState<"all" | "any">(initialData?.logic ?? "all");
  const [cooldown, setCooldown] = useState(initialData?.cooldown ?? 30);

  const handleSave = () => {
    onSave({
      name,
      cameraId,
      objectClass,
      zoneId: zoneId || undefined,
      conditions,
      logic,
      cooldown,
      enabled: true,
    });
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-3xl max-h-[90vh] overflow-y-auto p-6">
        <h2 className="text-xl font-bold mb-4">
          {initialData ? "Edit Rule" : "Create Rule"}
        </h2>

        {/* Basic fields */}
        <div className="grid grid-cols-2 gap-4 mb-6">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Rule Name</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full border rounded px-3 py-2"
              placeholder="e.g., Loitering Alert"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Object Class</label>
            <input
              type="text"
              value={objectClass}
              onChange={(e) => setObjectClass(e.target.value)}
              className="w-full border rounded px-3 py-2"
              placeholder="e.g., person"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Zone ID (optional)</label>
            <input
              type="text"
              value={zoneId}
              onChange={(e) => setZoneId(e.target.value)}
              className="w-full border rounded px-3 py-2"
              placeholder="Zone ID"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Cooldown (seconds)</label>
            <input
              type="number"
              value={cooldown}
              onChange={(e) => setCooldown(Number(e.target.value))}
              className="w-full border rounded px-3 py-2"
              min={0}
            />
          </div>
        </div>

        {/* Rule Builder Canvas */}
        <div className="mb-6">
          <h3 className="text-sm font-semibold text-gray-700 mb-2">Conditions</h3>
          <RuleCanvas
            conditions={conditions}
            logic={logic}
            onConditionsChange={setConditions}
            onLogicChange={setLogic}
          />
        </div>

        {/* Actions */}
        <div className="flex justify-end gap-3">
          <button
            onClick={onCancel}
            className="px-4 py-2 border rounded-lg text-gray-600 hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            disabled={!name || conditions.length === 0}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
          >
            Save Rule
          </button>
        </div>
      </div>
    </div>
  );
}
```

**Step 6: Build to verify**

```bash
cd dashboard && npm run build
```

Expected: Build succeeds

**Step 7: Commit**

```
feat: implement visual rule builder UI with condition palette and canvas
```

---

### Task 21: Rules Page Integration

**Files:**
- Create: `dashboard/src/app/rules/page.tsx`

**Step 1: Create rules page with builder integration**

```tsx
// dashboard/src/app/rules/page.tsx
"use client";

import { useEffect, useState } from "react";
import { RuleBuilderDialog } from "@/components/rule-builder/RuleBuilderDialog";
import { RuleFormData } from "@/components/rule-builder/types";

interface Rule {
  id: string;
  name: string;
  cameraId: string;
  objectClass: string;
  zoneId: string | null;
  enabled: boolean;
  conditions: Array<{ type: string; params: Record<string, unknown> }>;
  logic: string;
  cooldown: number;
}

export default function RulesPage() {
  const [rules, setRules] = useState<Rule[]>([]);
  const [showBuilder, setShowBuilder] = useState(false);
  const [editingRule, setEditingRule] = useState<Rule | null>(null);

  useEffect(() => {
    fetch("/api/rules")
      .then((r) => r.json())
      .then(setRules);
  }, []);

  const handleSave = async (data: RuleFormData) => {
    const method = editingRule ? "PUT" : "POST";
    const url = editingRule ? `/api/rules/${editingRule.id}` : "/api/rules";

    const res = await fetch(url, {
      method,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });

    if (res.ok) {
      const savedRule = await res.json();
      if (editingRule) {
        setRules(rules.map((r) => (r.id === savedRule.id ? savedRule : r)));
      } else {
        setRules([savedRule, ...rules]);
      }
    }

    setShowBuilder(false);
    setEditingRule(null);
  };

  const handleDelete = async (id: string) => {
    await fetch(`/api/rules/${id}`, { method: "DELETE" });
    setRules(rules.filter((r) => r.id !== id));
  };

  return (
    <div className="max-w-4xl mx-auto p-8">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Rules</h1>
        <button
          onClick={() => setShowBuilder(true)}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
        >
          Create Rule
        </button>
      </div>

      <div className="space-y-3">
        {rules.map((rule) => (
          <div key={rule.id} className="border rounded-lg p-4 bg-white shadow-sm">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-medium">{rule.name}</h3>
                <p className="text-sm text-gray-500">
                  {rule.objectClass} · {rule.conditions.length} condition(s) · {rule.logic}
                </p>
              </div>
              <div className="flex gap-2">
                <button
                  onClick={() => { setEditingRule(rule); setShowBuilder(true); }}
                  className="text-sm text-blue-600 hover:text-blue-800"
                >
                  Edit
                </button>
                <button
                  onClick={() => handleDelete(rule.id)}
                  className="text-sm text-red-600 hover:text-red-800"
                >
                  Delete
                </button>
              </div>
            </div>
          </div>
        ))}
        {rules.length === 0 && (
          <p className="text-gray-500 text-center py-8">No rules yet. Create one to get started.</p>
        )}
      </div>

      {showBuilder && (
        <RuleBuilderDialog
          cameraId={editingRule?.cameraId ?? ""}
          initialData={editingRule ? {
            name: editingRule.name,
            objectClass: editingRule.objectClass,
            zoneId: editingRule.zoneId ?? undefined,
            conditions: editingRule.conditions,
            logic: editingRule.logic as "all" | "any",
            cooldown: editingRule.cooldown,
          } : undefined}
          onSave={handleSave}
          onCancel={() => { setShowBuilder(false); setEditingRule(null); }}
        />
      )}
    </div>
  );
}
```

**Step 2: Build to verify**

```bash
cd dashboard && npm run build
```

Expected: Build succeeds

**Step 3: Commit**

```
feat: add rules page with visual rule builder integration
```

---

## Phase 9: Infrastructure & Testing

> Aspire orchestration wiring, PostgreSQL schema initialization, Docker configuration, and integration tests.

### Task 22: Aspire AppHost — Wire Dashboard & Finalize Orchestration

**Files:**
- Modify: `src/OpenEye.AppHost/AppHost.cs`
- Modify: `src/OpenEye.AppHost/OpenEye.AppHost.csproj`

**Step 1: Update AppHost to include dashboard**

```csharp
// src/OpenEye.AppHost/AppHost.cs
var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");
var postgres = builder.AddPostgres("postgres")
    .AddDatabase("openeye");

builder.AddProject<Projects.OpenEye_FrameCapture>("frame-capture")
    .WithReference(redis)
    .WaitFor(redis);

builder.AddProject<Projects.OpenEye_DetectionBridge>("detection-bridge")
    .WithReference(redis)
    .WaitFor(redis);

builder.AddProject<Projects.OpenEye_PipelineCore>("pipeline-core")
    .WithReference(redis)
    .WithReference(postgres)
    .WaitFor(redis)
    .WaitFor(postgres);

builder.AddProject<Projects.OpenEye_EventRouter>("event-router")
    .WithReference(redis)
    .WithReference(postgres)
    .WaitFor(redis)
    .WaitFor(postgres);

builder.AddNpmApp("dashboard", "../../dashboard", "dev")
    .WithReference(postgres)
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithExternalHttpEndpoints()
    .WaitFor(postgres);

builder.Build().Run();
```

**Step 2: Build to verify**

```bash
dotnet build src/OpenEye.AppHost -v n
```

Expected: Build succeeded

**Step 3: Commit**

```
feat: wire dashboard and add WaitFor dependencies to Aspire AppHost
```

---

### Task 23: PostgreSQL Schema Initialization

**Files:**
- Create: `docker/init.sql`

**Step 1: Create SQL init script**

```sql
-- docker/init.sql
-- PostgreSQL schema for OpenEye events (used by Event Router)
-- Prisma manages camera/zone/rule tables; this handles the event-router's table.

CREATE TABLE IF NOT EXISTS events (
    event_id     TEXT PRIMARY KEY,
    event_type   TEXT NOT NULL,
    timestamp    TIMESTAMPTZ NOT NULL,
    source_id    TEXT NOT NULL,
    zone_id      TEXT,
    rule_id      TEXT NOT NULL,
    tracked_objects JSONB NOT NULL DEFAULT '[]',
    metadata     JSONB,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_events_source_id ON events (source_id);
CREATE INDEX IF NOT EXISTS idx_events_timestamp ON events (timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_events_event_type ON events (event_type);
```

**Step 2: Commit**

```
feat: add PostgreSQL schema init script for events table
```

---

### Task 24: Integration Tests — Redis Stream Round-Trip

**Files:**
- Modify: `src/OpenEye.IntegrationTests/OpenEye.IntegrationTests.csproj`
- Create: `src/OpenEye.IntegrationTests/RedisStreamTests.cs`

**Step 1: Add packages to IntegrationTests csproj**

Add `StackExchange.Redis 2.11.8` and `Aspire.Hosting.Testing` to the IntegrationTests project. Also add project reference to `OpenEye.PipelineCore`.

**Step 2: Write Redis Stream integration tests**

```csharp
// src/OpenEye.IntegrationTests/RedisStreamTests.cs
using OpenEye.PipelineCore.Pipeline;
using StackExchange.Redis;

namespace OpenEye.IntegrationTests;

/// <summary>
/// These tests require a running Redis instance.
/// Run with: docker run -d -p 6379:6379 redis:7
/// Or skip if Redis is unavailable.
/// </summary>
public class RedisStreamTests : IAsyncLifetime
{
    private IConnectionMultiplexer? _redis;
    private const string TestStream = "test:integration";

    public async Task InitializeAsync()
    {
        try
        {
            _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379,abortConnect=false,connectTimeout=2000");
        }
        catch
        {
            // Redis not available, tests will be skipped
        }
    }

    public async Task DisposeAsync()
    {
        if (_redis is not null)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(TestStream);
            _redis.Dispose();
        }
    }

    [Fact]
    public async Task PublishAndConsume_RoundTrip()
    {
        if (_redis is null)
        {
            // Skip if Redis not available
            return;
        }

        var publisher = new RedisStreamPublisher(_redis);
        var consumer = new RedisStreamConsumer(_redis, TestStream, "test-group", "test-consumer");

        // Publish
        await publisher.PublishAsync(TestStream, new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        });

        // Consume
        var entries = await consumer.ReadAsync(10);
        Assert.Single(entries);
        Assert.Equal("value1", entries[0]["key1"].ToString());
        Assert.Equal("value2", entries[0]["key2"].ToString());

        // Acknowledge
        await consumer.AcknowledgeAsync(entries[0].Id);
    }

    [Fact]
    public async Task Consumer_GroupAlreadyExists_DoesNotThrow()
    {
        if (_redis is null) return;

        var consumer1 = new RedisStreamConsumer(_redis, TestStream, "test-group-2", "consumer-a");
        var consumer2 = new RedisStreamConsumer(_redis, TestStream, "test-group-2", "consumer-b");

        // Both should be able to ensure group without error
        await consumer1.EnsureGroupAsync();
        await consumer2.EnsureGroupAsync();
    }
}
```

**Step 3: Build to verify compilation**

```bash
dotnet build src/OpenEye.IntegrationTests -v n
```

Expected: Build succeeded

**Step 4: Run integration tests (if Redis available)**

```bash
dotnet test src/OpenEye.IntegrationTests -v n
```

Expected: PASS (or skip if Redis is not running)

**Step 5: Commit**

```
feat: add Redis stream round-trip integration tests
```

---

### Task 25: Integration Tests — Pipeline End-to-End

**Files:**
- Create: `src/OpenEye.IntegrationTests/PipelineEndToEndTests.cs`

**Step 1: Write Pipeline end-to-end integration test**

```csharp
// src/OpenEye.IntegrationTests/PipelineEndToEndTests.cs
using OpenEye.PipelineCore.Features;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.IntegrationTests;

public class PipelineEndToEndTests
{
    [Fact]
    public void FullPipeline_DetectionToPrimitiveToEvent()
    {
        // Arrange: zone, primitive config, rule with plugin condition
        var zone = new Zone("zone-1", "cam-1", [
            new Point2D(0, 0), new Point2D(100, 0),
            new Point2D(100, 100), new Point2D(0, 100)
        ]);

        var primitiveConfigs = new List<PrimitiveConfig>
        {
            new("zone_occupancy", "person", "zone-1")
        };

        var ruleDefinitions = new List<RuleDefinition>
        {
            new(
                RuleId: "rule-1",
                Name: "Crowding Alert",
                ObjectClass: "person",
                ZoneId: "zone-1",
                Conditions: [new ConditionConfig("count_above", new Dictionary<string, object> { ["threshold"] = 2 })],
                Logic: "all",
                CooldownSeconds: 0)
        };

        // Create pipeline components
        var tracker = new SortTracker();
        var zoneEvaluator = new DefaultZoneEvaluator();
        var featureExtractors = new IFeatureExtractor[]
        {
            new ObjectFeatureExtractor(),
            new ZoneFeatureExtractor(),
            new TemporalFeatureExtractor()
        };
        var primitiveExtractor = new DefaultPrimitiveExtractor();

        // Rule conditions
        var conditions = new IRuleCondition[]
        {
            new DurationCondition(),
            new CountAboveCondition(),
            new LineCrossCondition(),
            new SpeedCondition(),
            new PresenceCondition(),
            new AbsenceCondition()
        };
        var conditionRegistry = new ConditionRegistry(conditions);
        var ruleEngine = new RuleEngine(conditionRegistry);
        var eventBus = new LocalEventBus();

        var orchestrator = new PipelineOrchestrator(
            tracker, zoneEvaluator, featureExtractors,
            primitiveExtractor, ruleEngine, eventBus,
            [zone], [], primitiveConfigs, ruleDefinitions);

        // Act: send 3 detections (all in zone) — should trigger count_above(2)
        var now = DateTimeOffset.UtcNow;
        var detections = new List<Detection>
        {
            new("person", new BoundingBox(10, 10, 20, 20), 0.9, now, "cam-1"),
            new("person", new BoundingBox(30, 30, 20, 20), 0.9, now, "cam-1"),
            new("person", new BoundingBox(50, 50, 20, 20), 0.9, now, "cam-1"),
        };

        var events = orchestrator.Process("cam-1", detections, now);

        // Assert: at least one event fired
        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.RuleId == "rule-1");
    }

    [Fact]
    public void FullPipeline_NoDetections_NoEvents()
    {
        var zone = new Zone("zone-1", "cam-1", [
            new Point2D(0, 0), new Point2D(100, 0),
            new Point2D(100, 100), new Point2D(0, 100)
        ]);

        var tracker = new SortTracker();
        var zoneEvaluator = new DefaultZoneEvaluator();
        var featureExtractors = new IFeatureExtractor[]
        {
            new ObjectFeatureExtractor(),
            new ZoneFeatureExtractor(),
            new TemporalFeatureExtractor()
        };
        var primitiveExtractor = new DefaultPrimitiveExtractor();
        var conditions = new IRuleCondition[]
        {
            new DurationCondition(),
            new CountAboveCondition(),
            new LineCrossCondition(),
            new SpeedCondition(),
            new PresenceCondition(),
            new AbsenceCondition()
        };
        var conditionRegistry = new ConditionRegistry(conditions);
        var ruleEngine = new RuleEngine(conditionRegistry);
        var eventBus = new LocalEventBus();

        var orchestrator = new PipelineOrchestrator(
            tracker, zoneEvaluator, featureExtractors,
            primitiveExtractor, ruleEngine, eventBus,
            [zone], [], [], []);

        var events = orchestrator.Process("cam-1", [], DateTimeOffset.UtcNow);

        Assert.Empty(events);
    }
}
```

**Step 2: Build to verify compilation**

```bash
dotnet build src/OpenEye.IntegrationTests -v n
```

Expected: Build succeeded

**Step 3: Run integration tests**

```bash
dotnet test src/OpenEye.IntegrationTests --filter "FullyQualifiedName~PipelineEndToEndTests" -v n
```

Expected: PASS — 2 tests green

**Step 4: Run ALL tests**

```bash
dotnet test src/OpenEye.slnx -v n
```

Expected: PASS — all tests green

**Step 5: Commit**

```
feat: add pipeline end-to-end integration tests with full orchestrator wiring
```

---

## Phase 10: Polish — Configuration Examples & Docker

> Configuration examples, Docker Compose fallback, and final verification.

### Task 26: Configuration Examples

**Files:**
- Create: `src/OpenEye.FrameCapture/appsettings.json`
- Create: `src/OpenEye.DetectionBridge/appsettings.json`
- Create: `src/OpenEye.PipelineCore/appsettings.json`
- Create: `src/OpenEye.EventRouter/appsettings.json`
- Create: `dashboard/.env.example`

**Step 1: Create FrameCapture appsettings.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "redis": "localhost:6379"
  },
  "Cameras": [
    {
      "Id": "cam-1",
      "Url": "rtsp://192.168.1.100:554/stream",
      "TargetFps": 5
    }
  ]
}
```

**Step 2: Create DetectionBridge appsettings.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "redis": "localhost:6379"
  },
  "CameraIds": ["cam-1"],
  "Roboflow": {
    "Url": "http://localhost:9001",
    "ApiKey": "",
    "ModelId": "yolov8n-640"
  }
}
```

**Step 3: Create PipelineCore appsettings.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "redis": "localhost:6379",
    "openeye": "Host=localhost;Database=openeye;Username=postgres;Password=postgres"
  },
  "CameraIds": ["cam-1"]
}
```

**Step 4: Create EventRouter appsettings.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "redis": "localhost:6379",
    "openeye": "Host=localhost;Database=openeye;Username=postgres;Password=postgres"
  }
}
```

**Step 5: Create dashboard/.env.example**

```
DATABASE_URL="postgresql://postgres:postgres@localhost:5432/openeye?schema=public"
```

**Step 6: Commit**

```
feat: add configuration examples for all services and dashboard
```

---

### Task 27: Docker Compose Fallback (Non-Aspire)

**Files:**
- Create: `docker/docker-compose.yml`

**Step 1: Create Docker Compose file**

```yaml
# docker/docker-compose.yml
# Fallback for running without .NET Aspire (e.g., CI, production)
version: "3.9"

services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

  postgres:
    image: postgres:16-alpine
    ports:
      - "5432:5432"
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: openeye
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql

  roboflow-inference:
    image: roboflow/roboflow-inference-server-cpu:latest
    ports:
      - "9001:9001"
    environment:
      ROBOFLOW_API_KEY: ${ROBOFLOW_API_KEY:-}

volumes:
  redis-data:
  postgres-data:
```

**Step 2: Commit**

```
feat: add Docker Compose fallback for Redis, PostgreSQL, and Roboflow inference
```

---

### Task 28: Final Verification — Full Build & Test

**Step 1: Build entire solution**

```bash
dotnet build src/OpenEye.slnx -v n
```

Expected: Build succeeded, 0 warnings (or acceptable warnings only)

**Step 2: Run all unit tests**

```bash
dotnet test src/OpenEye.Tests -v n
```

Expected: PASS — all unit tests green

**Step 3: Run all integration tests**

```bash
dotnet test src/OpenEye.IntegrationTests -v n
```

Expected: PASS (or skip if Redis not running)

**Step 4: Build dashboard**

```bash
cd dashboard && npm run build
```

Expected: Build succeeds

**Step 5: Final commit**

```
chore: final verification — all builds and tests pass
```

---

## Summary

| Phase | Tasks | Description |
|-------|-------|-------------|
| 1 | 1–3 | Foundation — models, abstractions, Redis/Postgres helpers |
| 2 | 4–7 | Tracking & Zones — Hungarian algorithm, SORT tracker, geometry, zone evaluator |
| 3 | 8 | Feature Store — object, zone, temporal feature extractors |
| 4 | 9–12 | Primitives & Rules — extractor, plugin conditions, temporal buffer, dedup, engine |
| 5 | 13 | Pipeline Orchestration — orchestrator, local event bus |
| 6 | 14–17 | Services — frame capture, detection bridge, event router, pipeline core worker |
| 7 | 18–19 | Dashboard Backend — Next.js scaffolding, Prisma, API routes |
| 8 | 20–21 | Dashboard Rule Builder — condition components, canvas, integration |
| 9 | 22–25 | Infrastructure & Testing — Aspire wiring, PostgreSQL init, integration tests |
| 10 | 26–28 | Polish — configuration examples, Docker Compose, final verification |

**Total: 28 tasks across 10 phases**
