// tests/OpenEye.Tests/Models/ModelTests.cs
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Models;

public class ModelTests
{
    [Fact]
    public void BoundingBox_Properties_SetCorrectly()
    {
        var box = new BoundingBox(0.1, 0.2, 0.3, 0.4);
        Assert.Equal(0.1, box.X);
        Assert.Equal(0.2, box.Y);
        Assert.Equal(0.3, box.Width);
        Assert.Equal(0.4, box.Height);
    }

    [Fact]
    public void Detection_RequiredProperties_SetCorrectly()
    {
        var det = new Detection(
            ClassLabel: "person",
            BoundingBox: new BoundingBox(0.1, 0.2, 0.3, 0.4),
            Confidence: 0.95,
            Timestamp: DateTimeOffset.UtcNow,
            SourceId: "camera-01");

        Assert.Equal("person", det.ClassLabel);
        Assert.Equal(0.95, det.Confidence);
        Assert.Null(det.FrameIndex);
    }

    [Fact]
    public void TrackedObject_InitialState_IsActive()
    {
        var obj = new TrackedObject
        {
            TrackId = "track-0",
            ClassLabel = "person",
            CurrentBox = new BoundingBox(0.1, 0.2, 0.3, 0.4),
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow
        };

        Assert.Equal(TrackState.Active, obj.State);
        Assert.Empty(obj.Trajectory);
        Assert.Empty(obj.Metadata);
    }

    [Fact]
    public void Event_RecordEquality_WorksCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var tracks = Array.Empty<TrackedObject>();
        var e1 = new Event("evt-1", "loitering", now, "cam-1", "zone-1", tracks, "rule-1");
        var e2 = new Event("evt-1", "loitering", now, "cam-1", "zone-1", tracks, "rule-1");
        Assert.Equal(e1, e2);
    }

    [Fact]
    public void Zone_Polygon_StoresNormalizedPoints()
    {
        var polygon = new List<Point2D>
        {
            new(0.0, 0.0), new(1.0, 0.0), new(1.0, 1.0), new(0.0, 1.0)
        };
        var zone = new Zone("checkout", "cam-1", polygon);
        Assert.Equal(4, zone.Polygon.Count);
        Assert.Equal(0.0, zone.Polygon[0].X);
    }

    [Fact]
    public void ZonePresence_MutableProperties_CanBeSet()
    {
        var now = DateTimeOffset.UtcNow;
        var presence = new ZonePresence
        {
            TrackId = "t1",
            ZoneId = "zone-1",
            EnteredAt = now
        };
        Assert.Null(presence.ExitedAt);
        presence.ExitedAt = now.AddSeconds(10);
        Assert.NotNull(presence.ExitedAt);
    }

    [Fact]
    public void ZoneEvaluationResult_HoldsAllThreeCollections()
    {
        var result = new ZoneEvaluationResult(
            Transitions: [],
            TripwireCrossings: [],
            ActivePresences: []);
        Assert.Empty(result.Transitions);
        Assert.Empty(result.TripwireCrossings);
        Assert.Empty(result.ActivePresences);
    }
}
