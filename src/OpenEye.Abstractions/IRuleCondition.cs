using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IRuleCondition
{
    string Type { get; }
    bool Evaluate(FrameContext context, RuleDefinition rule, TrackedObject? obj);
}
