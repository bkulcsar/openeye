using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Primitives;

public class DefaultPrimitiveExtractor : IPrimitiveExtractor
{
    public IReadOnlyList<Primitive> Extract(FrameContext context, IReadOnlyList<PrimitiveConfig> configs)
    {
        var primitives = new List<Primitive>();

        foreach (var config in configs)
        {
            var matchingTracks = context.Tracks
                .Where(t => t.State == TrackState.Active && t.ClassLabel == config.ClassLabel)
                .ToList();

            if (config.ZoneId is not null && context.ZoneResult is not null)
            {
                var inZone = context.ZoneResult.ActivePresences
                    .Where(p => p.ZoneId == config.ZoneId)
                    .Select(p => p.TrackId)
                    .ToHashSet();
                matchingTracks = matchingTracks.Where(t => inZone.Contains(t.TrackId)).ToList();
            }

            object value = config.Type switch
            {
                PrimitiveType.Presence  => matchingTracks.Count > 0,
                PrimitiveType.Absence   => matchingTracks.Count == 0,
                PrimitiveType.Count     => matchingTracks.Count,
                PrimitiveType.ZoneDuration => ComputeZoneDuration(context, matchingTracks, config.ZoneId),
                PrimitiveType.Speed     => ComputeSpeed(context, matchingTracks),
                PrimitiveType.LineCrossed => CheckLineCrossed(context, matchingTracks, config.TripwireId),
                _ => false
            };

            primitives.Add(new Primitive(config.Name, value, context.Timestamp, context.SourceId));
        }

        return primitives;
    }

    private static double ComputeZoneDuration(FrameContext context, List<TrackedObject> tracks, string? zoneId)
    {
        if (zoneId is null || context.ZoneResult is null || tracks.Count == 0) return 0.0;
        var presence = context.ZoneResult.ActivePresences
            .Where(p => p.ZoneId == zoneId && tracks.Any(t => t.TrackId == p.TrackId))
            .OrderBy(p => p.EnteredAt)
            .FirstOrDefault();
        return presence is null ? 0.0 : (context.Timestamp - presence.EnteredAt).TotalSeconds;
    }

    private static double ComputeSpeed(FrameContext context, List<TrackedObject> tracks)
    {
        if (tracks.Count == 0) return 0.0;
        var track = tracks[0];
        return context.Features.TryGet<double>("object_speed", track.TrackId, out var speed) ? speed : 0.0;
    }

    private static bool CheckLineCrossed(FrameContext context, List<TrackedObject> tracks, string? tripwireId)
    {
        if (tripwireId is null || context.ZoneResult is null) return false;
        return context.ZoneResult.TripwireCrossings
            .Any(c => c.TripwireId == tripwireId && tracks.Any(t => t.TrackId == c.TrackId));
    }
}
