using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IRuleEngine
{
    IReadOnlyList<Event> Evaluate(FrameContext context, IReadOnlyList<RuleDefinition> rules);
}
