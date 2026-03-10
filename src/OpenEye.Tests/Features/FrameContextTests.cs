using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Features;

public class FrameContextTests
{
    [Fact]
    public void FrameContext_HasIsolatedFeatureStore()
    {
        var ctx = new FrameContext
        {
            SourceId = "cam-1",
            Timestamp = DateTimeOffset.UtcNow,
            Detections = []
        };

        ctx.Features.Set("test", 42);
        Assert.Equal(42, ctx.Features.Get<int>("test"));
    }

    [Fact]
    public void FrameContext_DefaultCollections_AreEmpty()
    {
        var ctx = new FrameContext
        {
            SourceId = "cam-1",
            Timestamp = DateTimeOffset.UtcNow,
            Detections = []
        };

        Assert.Empty(ctx.Tracks);
        Assert.Null(ctx.ZoneResult);
        Assert.Empty(ctx.Primitives);
        Assert.Empty(ctx.Events);
    }
}
