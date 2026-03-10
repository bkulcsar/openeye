using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Features;

public class ZoneFeatureExtractor : IFeatureExtractor
{
    public void Update(FrameContext context)
    {
        if (context.ZoneResult is null) return;

        var occupancy = context.ZoneResult.ActivePresences
            .GroupBy(p => p.ZoneId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (zoneId, count) in occupancy)
            context.Features.Set("zone_occupancy", count, zoneId);

        foreach (var presence in context.ZoneResult.ActivePresences)
        {
            double dwell = (context.Timestamp - presence.EnteredAt).TotalSeconds;
            context.Features.Set("dwell_time", dwell, $"{presence.TrackId}:{presence.ZoneId}");
        }
    }
}
