using OpenEye.PipelineCore.Primitives;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Primitives;

public class PrimitiveExtractorTests
{
    private static FrameContext MakeContext(
        IReadOnlyList<TrackedObject> tracks,
        ZoneEvaluationResult? zoneResult = null,
        DateTimeOffset? timestamp = null)
    {
        var t = timestamp ?? DateTimeOffset.UtcNow;
        return new FrameContext
        {
            SourceId = "cam-1", Timestamp = t, Detections = [],
            Tracks = tracks,
            ZoneResult = zoneResult ?? new ZoneEvaluationResult([], [], [])
        };
    }

    [Fact]
    public void Presence_PersonInZone_ReturnsTrue()
    {
        var t = DateTimeOffset.UtcNow;
        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
            FirstSeen = t, LastSeen = t
        };
        var zoneResult = new ZoneEvaluationResult([], [],
            [new ZonePresence { TrackId = "t1", ZoneId = "z1", EnteredAt = t }]);
        var ctx = MakeContext([track], zoneResult, t);

        var configs = new List<PrimitiveConfig>
        {
            new("person_in_checkout", PrimitiveType.Presence, "person", "z1", null, "cam-1")
        };

        var result = new DefaultPrimitiveExtractor().Extract(ctx, configs);

        Assert.Single(result);
        Assert.Equal(true, result[0].Value);
    }

    [Fact]
    public void Count_ThreePeopleInZone_ReturnsThree()
    {
        var t = DateTimeOffset.UtcNow;
        var tracks = Enumerable.Range(0, 3).Select(i => new TrackedObject
        {
            TrackId = $"t{i}", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
            FirstSeen = t, LastSeen = t
        }).ToList();
        var presences = tracks.Select(t => new ZonePresence
            { TrackId = t.TrackId, ZoneId = "z1", EnteredAt = DateTimeOffset.UtcNow }).ToList();
        var ctx = MakeContext(tracks, new ZoneEvaluationResult([], [], presences), t);

        var configs = new List<PrimitiveConfig>
        {
            new("queue_length", PrimitiveType.Count, "person", "z1", null, "cam-1")
        };

        var result = new DefaultPrimitiveExtractor().Extract(ctx, configs);

        Assert.Equal(3, result[0].Value);
    }

    [Fact]
    public void Speed_ReadsFromFeatureStore()
    {
        var t = DateTimeOffset.UtcNow;
        var track = new TrackedObject
        {
            TrackId = "t1", ClassLabel = "person",
            CurrentBox = new BoundingBox(0.5, 0.5, 0.1, 0.1),
            FirstSeen = t, LastSeen = t
        };
        var ctx = MakeContext([track], timestamp: t);
        ctx.Features.Set("object_speed", 2.5, "t1");

        var configs = new List<PrimitiveConfig>
        {
            new("person_speed", PrimitiveType.Speed, "person", null, null, "cam-1")
        };

        var result = new DefaultPrimitiveExtractor().Extract(ctx, configs);

        Assert.Equal(2.5, result[0].Value);
    }
}
