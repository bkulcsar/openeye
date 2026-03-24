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

    [Fact]
    public void RuleWithEvidenceType_IncludesEvidenceRequestInMetadata()
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
            new("rule-ev", "loitering", "person", "z1", null,
                [new ConditionConfig("duration", ">", 10)], "emit_event",
                EvidenceType: EvidenceType.Screenshot)
        };

        var events = engine.Evaluate(ctx, rules);
        Assert.Single(events);
        Assert.True(events[0].Metadata!.ContainsKey("evidenceRequest"));
        var evidenceRequest = Assert.IsType<EvidenceRequest>(events[0].Metadata!["evidenceRequest"]);
        Assert.Equal(events[0].EventId, evidenceRequest.EventId);
        Assert.Equal("cam-1", evidenceRequest.SourceId);
        Assert.Equal(EvidenceType.Screenshot, evidenceRequest.Type);
    }

    [Fact]
    public void RuleWithoutEvidenceType_DoesNotIncludeEvidenceRequest()
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
            new("rule-no-ev", "loitering", "person", "z1", null,
                [new ConditionConfig("duration", ">", 10)], "emit_event")
        };

        var events = engine.Evaluate(ctx, rules);
        Assert.Single(events);
        Assert.False(events[0].Metadata!.ContainsKey("evidenceRequest"));
    }
}
