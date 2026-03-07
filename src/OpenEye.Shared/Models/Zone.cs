namespace OpenEye.Shared.Models;

public record Point2D(double X, double Y);

public record Zone(
    string ZoneId,
    string SourceId,
    IReadOnlyList<Point2D> Polygon);
