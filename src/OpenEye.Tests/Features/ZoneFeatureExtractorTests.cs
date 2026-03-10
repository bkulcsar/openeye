using OpenEye.PipelineCore.Features;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Features;

public class ZoneFeatureExtractorTests
{
    [Fact]
    public void ComputesZoneOccupancy()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = new FrameContext
        {
            SourceId = "cam-1", Timestamp = t, Detections = [],
            Tracks = [],
            ZoneResult = new ZoneEvaluationResult([], [],
            [
                new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t },
                new ZonePresence { TrackId = "t2", ZoneId = "z1", EnteredAt = t }
            ])
        };

        new ZoneFeatureExtractor().Update(ctx);

        Assert.Equal(2, ctx.Features.Get<int>("zone_occupancy", "z1"));
    }

    [Fact]
    public void ComputesDwellTime()
    {
        var t = DateTimeOffset.UtcNow;
        var ctx = new FrameContext
        {
            SourceId = "cam-1", Timestamp = t, Detections = [],
            Tracks = [],
            ZoneResult = new ZoneEvaluationResult([], [],
            [
                new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t.AddSeconds(-10) }
            ])
        };

        new ZoneFeatureExtractor().Update(ctx);

        Assert.Equal(10.0, ctx.Features.Get<double>("dwell_time", "t1:z1"), 0.1);
    }
}
