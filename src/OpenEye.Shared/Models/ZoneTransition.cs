namespace OpenEye.Shared.Models;

public enum ZoneTransitionType { Enter, Exit }

public record ZoneTransition(
    string TrackId,
    string ZoneId,
    ZoneTransitionType Type,
    DateTimeOffset Timestamp);
