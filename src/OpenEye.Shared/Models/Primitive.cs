namespace OpenEye.Shared.Models;

/// <summary>
/// A named semantic signal computed by the primitive extractor each frame.
/// Value is one of: bool (presence/absence/line_crossed), int (count),
/// or double (zone_duration, speed). Record equality on Value uses reference
/// semantics for boxed value types — compare with Convert.ToDouble() for numerics.
/// </summary>
public record Primitive(
    string Name,
    object Value,
    DateTimeOffset Timestamp,
    string SourceId);
