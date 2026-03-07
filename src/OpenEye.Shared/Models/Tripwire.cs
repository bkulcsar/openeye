namespace OpenEye.Shared.Models;

public record Tripwire(
    string TripwireId,
    string SourceId,
    Point2D Start,
    Point2D End);
