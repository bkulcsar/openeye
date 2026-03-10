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
            [new Point2D(0.2, 0.2), new Point2D(0.8, 0.2), new Point2D(0.8, 0.8), new Point2D(0.2, 0.8)]);
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
