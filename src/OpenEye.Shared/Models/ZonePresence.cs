namespace OpenEye.Shared.Models;

public class ZonePresence
{
    public required string TrackId { get; set; }
    public required string ZoneId { get; set; }
    public required DateTimeOffset EnteredAt { get; set; }
    public DateTimeOffset? ExitedAt { get; set; }
}
