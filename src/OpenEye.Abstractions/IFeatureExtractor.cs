using OpenEye.Shared.Features;

namespace OpenEye.Abstractions;

public interface IFeatureExtractor
{
    void Update(FrameContext context);
}
