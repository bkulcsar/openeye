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
    private sealed record PipelineConfig(
        IReadOnlyList<Zone> Zones,
        IReadOnlyList<Tripwire> Tripwires,
        IReadOnlyList<PrimitiveConfig> PrimitiveConfigs,
        IReadOnlyList<RuleDefinition> Rules);

    private volatile PipelineConfig _config = new([], [], [], []);

    public void ReloadConfig(
        IReadOnlyList<Zone> zones,
        IReadOnlyList<Tripwire> tripwires,
        IReadOnlyList<PrimitiveConfig> primitiveConfigs,
        IReadOnlyList<RuleDefinition> rules)
    {
        _config = new PipelineConfig(zones, tripwires, primitiveConfigs, rules);
    }

    public IReadOnlyList<Event> ProcessFrame(
        string cameraId, IReadOnlyList<Detection> detections, DateTimeOffset timestamp)
    {
        var cfg = _config; // capture once for consistent snapshot
        var state = _cameraStates.GetOrAdd(cameraId, _ => new CameraState(conditionRegistry));

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
        var cfg = _config; // capture once
        var classes = new HashSet<string>();
        foreach (var p in cfg.PrimitiveConfigs) classes.Add(p.ClassLabel);
        foreach (var r in cfg.Rules) classes.Add(r.ObjectClass);
        return classes;
    }
}
