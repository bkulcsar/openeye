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

        var context = new FrameContext
        {
            SourceId = cameraId,
            Timestamp = timestamp,
            Detections = detections
        };

        // Stage 1: Object Tracking
        context.Tracks = tracker.Update(detections, timestamp);

        // Stage 2: Zone Evaluation
        context.ZoneResult = zoneEvaluator.Evaluate(context.Tracks, cfg.Zones, cfg.Tripwires);

        // Stage 3: Feature Extraction → Feature Store
        foreach (var extractor in featureExtractors)
            extractor.Update(context);

        // Stage 4: Primitive Extraction (reads from Feature Store)
        context.Primitives = primitiveExtractor.Extract(context, cfg.PrimitiveConfigs);

        // Stage 5: Rule Evaluation (plugin conditions read from context)
        context.Events = ruleEngine.Evaluate(context, cfg.Rules);

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
