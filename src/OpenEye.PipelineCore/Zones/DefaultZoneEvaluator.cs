using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Zones;

public class DefaultZoneEvaluator : IZoneEvaluator
{
    private readonly Dictionary<(string TrackId, string ZoneId), ZonePresence> _activePresences = [];

    public ZoneEvaluationResult Evaluate(
        IReadOnlyList<TrackedObject> tracks,
        IReadOnlyList<Zone> zones,
        IReadOnlyList<Tripwire> tripwires)
    {
        var transitions = new List<ZoneTransition>();
        var crossings = new List<TripwireCrossing>();

        foreach (var track in tracks.Where(t => t.State == TrackState.Active))
        {
            var centroid = Geometry.Centroid(track.CurrentBox);

            foreach (var zone in zones)
            {
                var key = (track.TrackId, zone.ZoneId);
                bool isInside = Geometry.PointInPolygon(zone.Polygon, centroid);
                bool wasInside = _activePresences.ContainsKey(key);

                if (isInside && !wasInside)
                {
                    var presence = new ZonePresence
                    {
                        TrackId = track.TrackId,
                        ZoneId = zone.ZoneId,
                        EnteredAt = track.LastSeen
                    };
                    _activePresences[key] = presence;
                    transitions.Add(new ZoneTransition(track.TrackId, zone.ZoneId,
                        ZoneTransitionType.Enter, track.LastSeen));
                }
                else if (!isInside && wasInside)
                {
                    _activePresences[key].ExitedAt = track.LastSeen;
                    transitions.Add(new ZoneTransition(track.TrackId, zone.ZoneId,
                        ZoneTransitionType.Exit, track.LastSeen));
                    _activePresences.Remove(key);
                }
            }

            // Tripwire crossing: check last trajectory segment
            if (track.Trajectory.Count >= 2)
            {
                var prev = Geometry.Centroid(track.Trajectory[^2].Box);
                var curr = Geometry.Centroid(track.Trajectory[^1].Box);

                foreach (var tripwire in tripwires)
                {
                    if (Geometry.SegmentsIntersect(prev, curr, tripwire.Start, tripwire.End))
                    {
                        crossings.Add(new TripwireCrossing(
                            track.TrackId, tripwire.TripwireId, track.LastSeen));
                    }
                }
            }
        }

        // Clean up presences for expired/lost tracks
        var activeTrackIds = tracks.Where(t => t.State == TrackState.Active)
            .Select(t => t.TrackId).ToHashSet();
        var staleKeys = _activePresences.Keys.Where(k => !activeTrackIds.Contains(k.TrackId)).ToList();
        foreach (var key in staleKeys)
            _activePresences.Remove(key);

        return new ZoneEvaluationResult(
            transitions,
            crossings,
            _activePresences.Values.ToList());
    }
}
