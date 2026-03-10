using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IObjectTracker
{
    IReadOnlyList<TrackedObject> Update(IReadOnlyList<Detection> detections, DateTimeOffset timestamp);
    IReadOnlyList<TrackedObject> ActiveTracks { get; }
    void Reset();
}
