namespace OpenEye.Shared.Models;

public enum TrackState { Active, Lost, Expired }

public record TrajectoryPoint(BoundingBox Box, DateTimeOffset Timestamp);

public class TrackedObject
{
    public required string TrackId { get; init; }
    public required string ClassLabel { get; init; }
    public required BoundingBox CurrentBox { get; set; }
    public List<TrajectoryPoint> Trajectory { get; } = [];
    public required DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; set; }
    public TrackState State { get; set; } = TrackState.Active;
    public Dictionary<string, object> Metadata { get; } = [];
}
