namespace OpenEye.Shared.Models;

public class RuleState
{
    public required string RuleId { get; init; }
    public required string TrackId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public Dictionary<string, object> Data { get; } = [];
}
