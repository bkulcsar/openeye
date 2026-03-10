using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IPrimitiveExtractor
{
    IReadOnlyList<Primitive> Extract(FrameContext context, IReadOnlyList<PrimitiveConfig> configs);
}
