using OpenEye.PipelineCore.Rules;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Rules;

public class InMemoryRuleStateStoreTests
{
    [Fact]
    public void Set_And_Get_RoundTrips()
    {
        var store = new InMemoryRuleStateStore();
        var state = new RuleState { RuleId = "rule-1", TrackId = "track-1", StartedAt = DateTimeOffset.UtcNow };
        store.Set("rule-1", "track-1", state);
        var result = store.Get("rule-1", "track-1");
        Assert.NotNull(result);
        Assert.Equal(state.StartedAt, result.StartedAt);
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        var store = new InMemoryRuleStateStore();
        var state = new RuleState { RuleId = "rule-1", TrackId = "track-1", StartedAt = DateTimeOffset.UtcNow };
        store.Set("rule-1", "track-1", state);
        store.Remove("rule-1", "track-1");
        Assert.Null(store.Get("rule-1", "track-1"));
    }

    [Fact]
    public void RemoveByTrack_ClearsAllRulesForTrack()
    {
        var store = new InMemoryRuleStateStore();
        var now = DateTimeOffset.UtcNow;
        store.Set("rule-1", "track-1", new RuleState { RuleId = "rule-1", TrackId = "track-1", StartedAt = now });
        store.Set("rule-2", "track-1", new RuleState { RuleId = "rule-2", TrackId = "track-1", StartedAt = now });
        store.Set("rule-1", "track-2", new RuleState { RuleId = "rule-1", TrackId = "track-2", StartedAt = now }); // different track, should remain
        store.RemoveByTrack("track-1");
        Assert.Null(store.Get("rule-1", "track-1"));
        Assert.Null(store.Get("rule-2", "track-1"));
        Assert.NotNull(store.Get("rule-1", "track-2")); // untouched
    }

    [Fact]
    public void GetByRule_ReturnsAllStatesForRule()
    {
        var store = new InMemoryRuleStateStore();
        var now = DateTimeOffset.UtcNow;
        store.Set("rule-1", "track-1", new RuleState { RuleId = "rule-1", TrackId = "track-1", StartedAt = now });
        store.Set("rule-1", "track-2", new RuleState { RuleId = "rule-1", TrackId = "track-2", StartedAt = now });
        store.Set("rule-2", "track-1", new RuleState { RuleId = "rule-2", TrackId = "track-1", StartedAt = now }); // different rule, should not appear
        var results = store.GetByRule("rule-1");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        var store = new InMemoryRuleStateStore();
        Assert.Null(store.Get("rule-1", "track-1"));
    }
}
