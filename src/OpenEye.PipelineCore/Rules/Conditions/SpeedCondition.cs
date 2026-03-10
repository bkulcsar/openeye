using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class SpeedCondition : IRuleCondition
{
    public string Type => "speed";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        if (obj is null) return false;
        var condition = rule.Conditions.FirstOrDefault(c => c.Type == Type);
        if (condition is null) return false;
        if (!context.Features.TryGet<double>("object_speed", obj.TrackId, out var speed))
            return false;
        return ConditionHelper.Compare(speed, condition.Operator, condition.Value);
    }
}
