using OpenEye.Abstractions;
using OpenEye.PipelineCore.Features;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Rules.Conditions;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;

namespace OpenEye.IntegrationTests;

public class PipelineEndToEndTests
{
    private static PipelineOrchestrator BuildOrchestrator(
        IReadOnlyList<Zone> zones,
        IReadOnlyList<PrimitiveConfig> primitiveConfigs,
        IReadOnlyList<RuleDefinition> rules)
    {
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
        var stateStore = new InMemoryRuleStateStore();
        var ruleEngine = new DefaultRuleEngine(conditionRegistry, stateStore);

        var orchestrator = new PipelineOrchestrator(
            tracker, zoneEvaluator, featureExtractors, primitiveExtractor, ruleEngine);
        orchestrator.ReloadConfig(zones, [], primitiveConfigs, rules);
        return orchestrator;
    }

    [Fact]
    public void FullPipeline_DetectionToPrimitiveToEvent()
    {
        var zone = new Zone("zone-1", "cam-1", [
            new Point2D(0, 0), new Point2D(1, 0),
            new Point2D(1, 1), new Point2D(0, 1)
        ]);

        var primitiveConfigs = new List<PrimitiveConfig>
        {
            new("zone_occupancy", PrimitiveType.Count, "person", "zone-1")
        };

        var ruleDefinitions = new List<RuleDefinition>
        {
            new(
                RuleId: "rule-1",
                Name: "Crowding Alert",
                ObjectClass: "person",
                ZoneId: "zone-1",
                TripwireId: null,
                Conditions: [new ConditionConfig("presence")],
                Logic: "all",
                Cooldown: null)
        };

        var orchestrator = BuildOrchestrator(zone is var z ? [z] : [], primitiveConfigs, ruleDefinitions);

        var now = DateTimeOffset.UtcNow;
        var detections = new List<Detection>
        {
            new("person", new BoundingBox(0.1, 0.1, 0.1, 0.1), 0.9, now, "cam-1"),
        };

        var events = orchestrator.ProcessFrame("cam-1", detections, now);

        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.RuleId == "rule-1");
    }

    [Fact]
    public void FullPipeline_NoDetections_NoEvents()
    {
        var zone = new Zone("zone-1", "cam-1", [
            new Point2D(0, 0), new Point2D(1, 0),
            new Point2D(1, 1), new Point2D(0, 1)
        ]);

        var orchestrator = BuildOrchestrator([zone], [], []);

        var events = orchestrator.ProcessFrame("cam-1", [], DateTimeOffset.UtcNow);

        Assert.Empty(events);
    }
}
