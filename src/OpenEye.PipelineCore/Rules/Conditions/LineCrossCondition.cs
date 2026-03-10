using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules.Conditions;

public class LineCrossCondition : IRuleCondition
{
    public string Type => "line_cross";

    public bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj)
    {
        if (obj is null || rule.TripwireId is null || context.ZoneResult is null) return false;
        return context.ZoneResult.TripwireCrossings
            .Any(c => c.TrackId == obj.TrackId && c.TripwireId == rule.TripwireId);
    }
}
