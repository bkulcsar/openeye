using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class CountAboveCondition : IRuleCondition
{
    public string Type => "count_above";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        var condition = rule.Conditions.FirstOrDefault(c => c.Type == Type);
        if (condition is null) return false;
        if (condition.FeatureName is null) return false;

        var primitive = context.Primitives.FirstOrDefault(p => p.Name == condition.FeatureName);
        if (primitive is null) return false;

        double actual;
        try { actual = Convert.ToDouble(primitive.Value); }
        catch { return false; }
        return ConditionHelper.Compare(actual, condition.Operator, condition.Value);
    }
}
