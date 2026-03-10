using OpenEye.Abstractions;

namespace OpenEye.PipelineCore.Rules;

public class ConditionRegistry(IEnumerable<IRuleCondition> conditions) : IConditionRegistry
{
    private readonly Dictionary<string, IRuleCondition> _conditions =
        conditions.ToDictionary(c => c.Type);

    public IRuleCondition Get(string type) =>
        _conditions.TryGetValue(type, out var c) ? c
            : throw new InvalidOperationException($"Unknown condition type: {type}");

    public void Register(IRuleCondition condition) =>
        _conditions[condition.Type] = condition;
}
