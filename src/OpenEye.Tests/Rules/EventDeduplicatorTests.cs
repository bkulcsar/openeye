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
