namespace OpenEye.Shared.Models;

public enum PrimitiveType { Presence, Absence, Count, ZoneDuration, Speed, LineCrossed }

public record PrimitiveConfig(
    string Name,
    PrimitiveType Type,
    string ClassLabel,
    string? ZoneId,
    string? TripwireId = null,
    string SourceId = "");
