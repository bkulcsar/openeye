using System.Collections.Concurrent;
using OpenEye.Abstractions;
using OpenEye.PipelineCore.Features;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Pipeline;

public class PipelineOrchestrator(
    IConditionRegistry conditionRegistry,
    IEnumerable<IFeatureExtractor> sharedFeatureExtractors,
    IPrimitiveExtractor primitiveExtractor)
{
    private readonly ConcurrentDictionary<string, CameraState> _cameraStates = new();

    private sealed class CameraState
    {
        public IObjectTracker Tracker { get; } = new SortTracker();
        public IZoneEvaluator ZoneEvaluator { get; } = new DefaultZoneEvaluator();
        public TemporalFeatureExtractor TemporalFeatures { get; } = new TemporalFeatureExtractor();
        public IRuleEngine RuleEngine { get; }

        public CameraState(IConditionRegistry conditionRegistry)
        {
            var stateStore = new InMemoryRuleStateStore();
            RuleEngine = new DefaultRuleEngine(conditionRegistry, stateStore);
        }
    }
    private sealed record CameraPipelineConfig(
        IReadOnlyList<Zone> Zones,
        IReadOnlyList<Tripwire> Tripwires,
        IReadOnlyList<PrimitiveConfig> PrimitiveConfigs,
        IReadOnlyList<RuleDefinition> Rules);

    private volatile IReadOnlyDictionary<string, CameraPipelineConfig> _configByCamera
        = new Dictionary<string, CameraPipelineConfig>();

    public void ReloadConfig(
        IReadOnlyList<Zone> zones,
        IReadOnlyList<Tripwire> tripwires,
        IReadOnlyList<PrimitiveConfig> primitiveConfigs,
        IReadOnlyList<RuleDefinition> rules)
    {
        var cameraIds = zones.Select(z => z.SourceId)
            .Concat(tripwires.Select(t => t.SourceId))
            .Concat(primitiveConfigs.Select(p => p.SourceId))
            .Concat(rules.Where(r => !string.IsNullOrWhiteSpace(r.CameraId)).Select(r => r.CameraId!))
            .Distinct()
            .ToList();

        var byCamera = new Dictionary<string, CameraPipelineConfig>(StringComparer.Ordinal);
        var globalRules = rules.Where(r => string.IsNullOrWhiteSpace(r.CameraId)).ToList();
        foreach (var cameraId in cameraIds)
        {
            byCamera[cameraId] = new CameraPipelineConfig(
                zones.Where(z => z.SourceId == cameraId).ToList(),
                tripwires.Where(t => t.SourceId == cameraId).ToList(),
                primitiveConfigs.Where(p => p.SourceId == cameraId).ToList(),
                rules.Where(r => r.CameraId == cameraId).Concat(globalRules).ToList()
            );
        }

        _configByCamera = byCamera;
    }

    public IReadOnlyList<Event> ProcessFrame(
        string cameraId, IReadOnlyList<Detection> detections, DateTimeOffset timestamp)
    {
        var cfgByCamera = _configByCamera; // capture once for consistent snapshot
        var state = _cameraStates.GetOrAdd(cameraId, _ => new CameraState(conditionRegistry));

        var cfg = cfgByCamera.TryGetValue(cameraId, out var cameraCfg)
            ? cameraCfg
            : new CameraPipelineConfig([], [], [], []);

        var context = new FrameContext
        {
            SourceId = cameraId,
            Timestamp = timestamp,
            Detections = detections
        };

        // Stage 1: Object Tracking
        context.Tracks = state.Tracker.Update(detections, timestamp);

        // Stage 2: Zone Evaluation
        context.ZoneResult = state.ZoneEvaluator.Evaluate(context.Tracks, cfg.Zones, cfg.Tripwires);

        // Stage 3: Feature Extraction → Feature Store
        foreach (var extractor in sharedFeatureExtractors)
            extractor.Update(context);
        state.TemporalFeatures.Update(context);

        // Stage 4: Primitive Extraction (reads from Feature Store)
        context.Primitives = primitiveExtractor.Extract(context, cfg.PrimitiveConfigs);

        // Stage 5: Rule Evaluation (plugin conditions read from context)
        context.Events = state.RuleEngine.Evaluate(context, cfg.Rules);

        return context.Events;
    }

    public IReadOnlySet<string> GetRequiredClasses()
    {
        var cfgByCamera = _configByCamera; // capture once
        var classes = new HashSet<string>();
        foreach (var cfg in cfgByCamera.Values)
        {
            foreach (var p in cfg.PrimitiveConfigs) classes.Add(p.ClassLabel);
            foreach (var r in cfg.Rules) classes.Add(r.ObjectClass);
        }
        return classes;
    }
}
