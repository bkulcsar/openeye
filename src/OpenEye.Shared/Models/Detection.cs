namespace OpenEye.Shared.Models;

public record Detection(
    string ClassLabel,
    BoundingBox BoundingBox,
    double Confidence,
    DateTimeOffset Timestamp,
    string SourceId,
    long? FrameIndex = null);
