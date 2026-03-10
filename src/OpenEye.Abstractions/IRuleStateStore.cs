using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IRuleStateStore
{
    RuleState? Get(string ruleId, string trackId);
    void Set(string ruleId, string trackId, RuleState state);
    void Remove(string ruleId, string trackId);
    void RemoveByTrack(string trackId);
    IReadOnlyList<RuleState> GetByRule(string ruleId);
}
