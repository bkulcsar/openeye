using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Zones;

public class ZoneEvaluatorTests
{
    private static readonly Zone TestZone = new("zone-1", "cam-1",
        [new(0.2, 0.2), new(0.8, 0.2), new(0.8, 0.8), new(0.2, 0.8)]);

    private static readonly Tripwire TestTripwire = new("tw-1", "cam-1",
        new(0.5, 0.0), new(0.5, 1.0));

    private static TrackedObject MakeTrack(string id, double cx, double cy, DateTimeOffset t,
        List<TrajectoryPoint>? trajectory = null)
    {
        var box = new BoundingBox(cx - 0.05, cy - 0.05, 0.1, 0.1);
        var track = new TrackedObject
        {
            TrackId = id, ClassLabel = "person", CurrentBox = box,
            FirstSeen = t, LastSeen = t
        };
        if (trajectory is not null)
            track.Trajectory.AddRange(trajectory);
        else
            track.Trajectory.Add(new TrajectoryPoint(box, t));
        return track;
    }

    [Fact]
    public void ObjectEntersZone_RecordsEntryTransition()
    {
        var eval = new DefaultZoneEvaluator();
        var t = DateTimeOffset.UtcNow;
        var track = MakeTrack("t1", 0.5, 0.5, t);

        var result = eval.Evaluate([track], [TestZone], []);

        Assert.Single(result.Transitions);
        Assert.Equal(ZoneTransitionType.Enter, result.Transitions[0].Type);
        Assert.Single(result.ActivePresences);
    }

    [Fact]
    public void ObjectLeavesZone_RecordsExitTransition()
    {
        var eval = new DefaultZoneEvaluator();
        var t = DateTimeOffset.UtcNow;

        eval.Evaluate([MakeTrack("t1", 0.5, 0.5, t)], [TestZone], []);
        var result = eval.Evaluate([MakeTrack("t1", 0.0, 0.0, t.AddSeconds(1))], [TestZone], []);

        Assert.Single(result.Transitions);
        Assert.Equal(ZoneTransitionType.Exit, result.Transitions[0].Type);
    }

    [Fact]
    public void ObjectStaysInZone_NoSpuriousTransitions()
    {
        var eval = new DefaultZoneEvaluator();
        var t = DateTimeOffset.UtcNow;

        eval.Evaluate([MakeTrack("t1", 0.5, 0.5, t)], [TestZone], []);
        var result = eval.Evaluate([MakeTrack("t1", 0.55, 0.55, t.AddSeconds(1))], [TestZone], []);

        Assert.Empty(result.Transitions);
        Assert.Single(result.ActivePresences);
    }

    [Fact]
    public void TripwireCrossing_Detected()
    {
        var eval = new DefaultZoneEvaluator();
        var t = DateTimeOffset.UtcNow;

        var prevBox = new BoundingBox(0.3, 0.45, 0.1, 0.1);
        var currBox = new BoundingBox(0.6, 0.45, 0.1, 0.1);
        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person", CurrentBox = currBox,
            FirstSeen = t, LastSeen = t.AddSeconds(1)
        };
        track.Trajectory.Add(new TrajectoryPoint(prevBox, t));
        track.Trajectory.Add(new TrajectoryPoint(currBox, t.AddSeconds(1)));

        var result = eval.Evaluate([track], [], [TestTripwire]);

        Assert.Single(result.TripwireCrossings);
        Assert.Equal("tw-1", result.TripwireCrossings[0].TripwireId);
    }
}
