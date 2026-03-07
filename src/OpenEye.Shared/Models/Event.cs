namespace OpenEye.Shared.Models;

/// <remarks>
/// Metadata uses Dictionary for extensibility. Note: record structural equality
/// on Metadata compares by reference — two Events with identical but separately-
/// constructed Metadata dictionaries will NOT compare equal via ==.
/// </remarks>
public record Event(
    string EventId,
    string EventType,
    DateTimeOffset Timestamp,
    string SourceId,
    string? ZoneId,
    IReadOnlyList<TrackedObject> TrackedObjects,
    string RuleId,
    Dictionary<string, object>? Metadata = null);
