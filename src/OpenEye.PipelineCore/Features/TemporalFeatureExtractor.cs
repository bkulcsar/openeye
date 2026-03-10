using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Features;

public class TemporalFeatureExtractor : IFeatureExtractor
{
    private readonly Dictionary<string, DateTimeOffset> _lastSeen = [];

    public void Update(FrameContext context)
    {
        var currentTrackIds = new HashSet<string>();

        foreach (var track in context.Tracks.Where(t => t.State == TrackState.Active))
        {
            currentTrackIds.Add(track.TrackId);

            if (_lastSeen.TryGetValue(track.TrackId, out var prev))
            {
                double timeSince = (context.Timestamp - prev).TotalSeconds;
                context.Features.Set("time_since_last_seen", timeSince, track.TrackId);
            }

            _lastSeen[track.TrackId] = context.Timestamp;
        }

        var stale = _lastSeen.Keys.Except(currentTrackIds).ToList();
        foreach (var id in stale) _lastSeen.Remove(id);
    }
}
