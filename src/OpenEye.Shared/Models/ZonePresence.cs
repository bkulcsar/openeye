namespace OpenEye.Shared.Models;

public class ZonePresence
{
    public required string TrackId { get; init; }
    public required string ZoneId { get; init; }
    public required DateTimeOffset EnteredAt { get; init; }
    public DateTimeOffset? ExitedAt { get; set; }
}
