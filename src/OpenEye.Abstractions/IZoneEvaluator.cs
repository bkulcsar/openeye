using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IZoneEvaluator
{
    ZoneEvaluationResult Evaluate(
        IReadOnlyList<TrackedObject> tracks,
        IReadOnlyList<Zone> zones,
        IReadOnlyList<Tripwire> tripwires);
}
