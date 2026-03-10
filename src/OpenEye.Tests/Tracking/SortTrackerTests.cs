using OpenEye.PipelineCore.Tracking;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Tracking;

public class SortTrackerTests
{
    private static Detection MakeDet(double x, double y, double w = 0.1, double h = 0.1,
        string cls = "person", string src = "cam-1") =>
        new(cls, new BoundingBox(x, y, w, h), 0.9, DateTimeOffset.UtcNow, src);

    [Fact]
    public void SingleDetection_CreatesOneTrack()
    {
        var tracker = new SortTracker();
        var result = tracker.Update([MakeDet(0.1, 0.1)], DateTimeOffset.UtcNow);
        Assert.Single(result);
        Assert.Equal(TrackState.Active, result[0].State);
    }

    [Fact]
    public void SamePosition_MaintainsTrackId()
    {
        var tracker = new SortTracker();
        var t0 = DateTimeOffset.UtcNow;
        tracker.Update([MakeDet(0.1, 0.1)], t0);
        var result = tracker.Update([MakeDet(0.11, 0.11)], t0.AddSeconds(1));
        Assert.Single(result);
        Assert.Equal("track-0", result[0].TrackId);
    }

    [Fact]
    public void TwoNonOverlapping_CreateTwoTracks()
    {
        var tracker = new SortTracker();
        var result = tracker.Update([MakeDet(0.0, 0.0), MakeDet(0.9, 0.9)], DateTimeOffset.UtcNow);
        Assert.Equal(2, result.Count);
        Assert.NotEqual(result[0].TrackId, result[1].TrackId);
    }

    [Fact]
    public void ObjectDisappears_TransitionsToLostThenExpired()
    {
        var config = new TrackerConfig { MaxLostFrames = 3 };
        var tracker = new SortTracker(config);
        var t = DateTimeOffset.UtcNow;

        tracker.Update([MakeDet(0.1, 0.1)], t);
        tracker.Update([], t.AddSeconds(1)); // Lost
        var result = tracker.Update([], t.AddSeconds(2)); // Still lost
        Assert.Equal(TrackState.Lost, result.First(r => r.TrackId == "track-0").State);

        tracker.Update([], t.AddSeconds(3));
        result = tracker.Update([], t.AddSeconds(4));
        var track = result.FirstOrDefault(r => r.TrackId == "track-0");
        Assert.True(track is null || track.State == TrackState.Expired);
    }

    [Fact]
    public void ObjectReappears_WithinLostWindow_MaintainsTrackId()
    {
        var config = new TrackerConfig { MaxLostFrames = 10 };
        var tracker = new SortTracker(config);
        var t = DateTimeOffset.UtcNow;

        tracker.Update([MakeDet(0.1, 0.1)], t);
        tracker.Update([], t.AddSeconds(1));
        var result = tracker.Update([MakeDet(0.1, 0.1)], t.AddSeconds(2));
        Assert.Contains(result, r => r.TrackId == "track-0" && r.State == TrackState.Active);
    }

    [Fact]
    public void Trajectory_AccumulatesPositions()
    {
        var tracker = new SortTracker();
        var t = DateTimeOffset.UtcNow;
        tracker.Update([MakeDet(0.1, 0.1)], t);
        tracker.Update([MakeDet(0.12, 0.12)], t.AddSeconds(1));
        var result = tracker.Update([MakeDet(0.14, 0.14)], t.AddSeconds(2));
        Assert.Equal(3, result[0].Trajectory.Count);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var tracker = new SortTracker();
        tracker.Update([MakeDet(0.1, 0.1)], DateTimeOffset.UtcNow);
        tracker.Reset();
        Assert.Empty(tracker.ActiveTracks);
    }
}
