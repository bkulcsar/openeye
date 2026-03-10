using OpenEye.Shared.Models;

namespace OpenEye.Shared.Features;

public class FrameContext
{
    public required string SourceId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required IReadOnlyList<Detection> Detections { get; init; }
    public IReadOnlyList<TrackedObject> Tracks { get; set; } = [];
    public ZoneEvaluationResult? ZoneResult { get; set; }
    public IFeatureStore Features { get; } = new FeatureStore();
    public IReadOnlyList<Primitive> Primitives { get; set; } = [];
    public IReadOnlyList<Event> Events { get; set; } = [];
}
