using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class PresenceCondition : IRuleCondition
{
    public string Type => "presence";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        if (rule.ZoneId is null || context.ZoneResult is null) return false;
        return context.ZoneResult.ActivePresences
            .Any(p => p.ZoneId == rule.ZoneId);
    }
}
