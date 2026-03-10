using OpenEye.PipelineCore.Features;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Features;

public class ObjectFeatureExtractorTests
{
    [Fact]
    public void ComputesObjectSpeed()
    {
        var t = DateTimeOffset.UtcNow;
        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.3, 0.3, 0.1, 0.1),
            FirstSeen = t.AddSeconds(-2), LastSeen = t
        };
        track.Trajectory.Add(new TrajectoryPoint(new BoundingBox(0.1, 0.1, 0.1, 0.1), t.AddSeconds(-2)));
        track.Trajectory.Add(new TrajectoryPoint(new BoundingBox(0.3, 0.3, 0.1, 0.1), t));

        var ctx = new FrameContext { SourceId = "cam-1", Timestamp = t, Detections = [], Tracks = [track] };
        new ObjectFeatureExtractor().Update(ctx);

        var speed = ctx.Features.Get<double>("object_speed", "t1");
        Assert.True(speed > 0);
    }

    [Fact]
    public void ComputesTimeInScene()
    {
        var t = DateTimeOffset.UtcNow;
        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.1, 0.1, 0.1, 0.1),
            FirstSeen = t.AddSeconds(-5), LastSeen = t
        };

        var ctx = new FrameContext { SourceId = "cam-1", Timestamp = t, Detections = [], Tracks = [track] };
        new ObjectFeatureExtractor().Update(ctx);

        Assert.Equal(5.0, ctx.Features.Get<double>("object_time_in_scene", "t1"), 0.1);
    }
}
