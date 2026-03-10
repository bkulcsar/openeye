using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Tracking;

public class SortTracker(TrackerConfig? config = null) : IObjectTracker
{
    private readonly TrackerConfig _config = config ?? new TrackerConfig();
    private readonly List<TrackedObject> _tracks = [];
    private int _nextId;

    public IReadOnlyList<TrackedObject> ActiveTracks =>
        _tracks.Where(t => t.State != TrackState.Expired).ToList();

    public IReadOnlyList<TrackedObject> Update(IReadOnlyList<Detection> detections, DateTimeOffset timestamp)
    {
        var active = _tracks.Where(t => t.State != TrackState.Expired).ToList();

        if (active.Count == 0 && detections.Count == 0)
            return [];

        if (active.Count == 0)
        {
            foreach (var det in detections)
                CreateTrack(det, timestamp);
            return ActiveTracks;
        }

        if (detections.Count == 0)
        {
            foreach (var track in active)
                IncrementLost(track);
            return ActiveTracks;
        }

        var costMatrix = new double[active.Count, detections.Count];
        for (int i = 0; i < active.Count; i++)
            for (int j = 0; j < detections.Count; j++)
                costMatrix[i, j] = 1.0 - ComputeIoU(active[i].CurrentBox, detections[j].BoundingBox);

        var assignments = HungarianAlgorithm.Solve(costMatrix);
        var matchedDetections = new HashSet<int>();

        for (int i = 0; i < active.Count; i++)
        {
            int j = assignments[i];
            if (j >= 0 && (1.0 - costMatrix[i, j]) >= _config.IouThreshold)
            {
                UpdateTrack(active[i], detections[j], timestamp);
                matchedDetections.Add(j);
            }
            else
            {
                IncrementLost(active[i]);
            }
        }

        for (int j = 0; j < detections.Count; j++)
        {
            if (!matchedDetections.Contains(j))
                CreateTrack(detections[j], timestamp);
        }

        return ActiveTracks;
    }

    public void Reset()
    {
        _tracks.Clear();
        _nextId = 0;
    }

    private void CreateTrack(Detection det, DateTimeOffset timestamp)
    {
        var track = new TrackedObject
        {
            TrackId = $"track-{_nextId++}",
            ClassLabel = det.ClassLabel,
            CurrentBox = det.BoundingBox,
            FirstSeen = timestamp,
            LastSeen = timestamp
        };
        track.Trajectory.Add(new TrajectoryPoint(det.BoundingBox, timestamp));
        _tracks.Add(track);
    }

    private void UpdateTrack(TrackedObject track, Detection det, DateTimeOffset timestamp)
    {
        track.CurrentBox = det.BoundingBox;
        track.LastSeen = timestamp;
        track.State = TrackState.Active;
        track.Metadata.Remove("lostFrames");
        track.Trajectory.Add(new TrajectoryPoint(det.BoundingBox, timestamp));
        if (track.Trajectory.Count > _config.TrajectoryDepth)
            track.Trajectory.RemoveAt(0);
    }

    private void IncrementLost(TrackedObject track)
    {
        var lostFrames = (int)track.Metadata.GetValueOrDefault("lostFrames", 0) + 1;
        track.Metadata["lostFrames"] = lostFrames;
        track.State = lostFrames >= _config.MaxLostFrames ? TrackState.Expired : TrackState.Lost;
    }

    private static double ComputeIoU(BoundingBox a, BoundingBox b)
    {
        double x1 = Math.Max(a.X, b.X);
        double y1 = Math.Max(a.Y, b.Y);
        double x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        double y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        double intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        double union = a.Width * a.Height + b.Width * b.Height - intersection;
        return union == 0 ? 0 : intersection / union;
    }
}
