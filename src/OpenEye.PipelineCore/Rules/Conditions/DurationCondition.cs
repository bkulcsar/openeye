using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class DurationCondition : IRuleCondition
{
    public string Type => "duration";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        if (obj is null || rule.ZoneId is null || context.ZoneResult is null) return false;
        var presence = context.ZoneResult.ActivePresences
            .FirstOrDefault(p => p.TrackId == obj.TrackId && p.ZoneId == rule.ZoneId);
        if (presence is null) return false;

        var condition = rule.Conditions.FirstOrDefault(c => c.Type == Type);
        if (condition is null) return false;
        var elapsed = (context.Timestamp - presence.EnteredAt).TotalSeconds;
        return ConditionHelper.Compare(elapsed, condition.Operator, condition.Value);
    }
}
