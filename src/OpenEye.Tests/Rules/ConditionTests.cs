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
