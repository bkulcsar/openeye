namespace OpenEye.Shared.Models;

public record Event(
    string EventId,
    string EventType,
    DateTimeOffset Timestamp,
    string SourceId,
    string? ZoneId,
    IReadOnlyList<TrackedObject> TrackedObjects,
    string RuleId,
    Dictionary<string, object>? Metadata = null);
