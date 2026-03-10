using System.Collections.Concurrent;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules;

public class InMemoryRuleStateStore : IRuleStateStore
{
    private readonly ConcurrentDictionary<(string RuleId, string TrackId), RuleState> _store = new();

    public RuleState? Get(string ruleId, string trackId) =>
        _store.TryGetValue((ruleId, trackId), out var state) ? state : null;

    public void Set(string ruleId, string trackId, RuleState state) =>
        _store[(ruleId, trackId)] = state;

    public void Remove(string ruleId, string trackId) =>
        _store.TryRemove((ruleId, trackId), out _);

    public void RemoveByTrack(string trackId)
    {
        var keysToRemove = _store.Keys
            .Where(k => k.TrackId == trackId)
            .ToList();

        foreach (var key in keysToRemove)
            _store.TryRemove(key, out _);
    }

    public IReadOnlyList<RuleState> GetByRule(string ruleId)
    {
        return _store.Where(kv => kv.Key.RuleId == ruleId)
            .Select(kv => kv.Value)
            .ToList();
    }
}
