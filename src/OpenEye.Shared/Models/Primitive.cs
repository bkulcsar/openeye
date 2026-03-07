namespace OpenEye.Shared.Models;

public record Primitive(
    string Name,
    object Value,
    DateTimeOffset Timestamp,
    string SourceId);
