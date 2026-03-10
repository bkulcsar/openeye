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

        // Stage 3: Feature Extraction
        foreach (var extractor in featureExtractors)
            extractor.Update(context);

        // Stage 4: Primitive Extraction
        context.Primitives = primitiveExtractor.Extract(context, _primitiveConfigs);

        // Stage 5: Rule Evaluation
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
