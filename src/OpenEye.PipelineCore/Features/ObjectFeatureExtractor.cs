using OpenEye.Abstractions;
using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Features;

public class ObjectFeatureExtractor : IFeatureExtractor
{
    public void Update(FrameContext context)
    {
        foreach (var track in context.Tracks.Where(t => t.State == TrackState.Active))
        {
            if (track.Trajectory.Count >= 2)
            {
                var first = track.Trajectory[0];
                var last = track.Trajectory[^1];
                var c1 = Geometry.Centroid(first.Box);
                var c2 = Geometry.Centroid(last.Box);
                double dx = c2.X - c1.X;
                double dy = c2.Y - c1.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                double dt = (last.Timestamp - first.Timestamp).TotalSeconds;
                double speed = dt > 0 ? dist / dt : 0;
                context.Features.Set("object_speed", speed, track.TrackId);
                context.Features.Set("object_direction", Math.Atan2(dy, dx), track.TrackId);

                double pathLength = 0;
                for (int i = 1; i < track.Trajectory.Count; i++)
                {
                    var p1 = Geometry.Centroid(track.Trajectory[i - 1].Box);
                    var p2 = Geometry.Centroid(track.Trajectory[i].Box);
                    double sdx = p2.X - p1.X, sdy = p2.Y - p1.Y;
                    pathLength += Math.Sqrt(sdx * sdx + sdy * sdy);
                }
                context.Features.Set("object_path_length", pathLength, track.TrackId);
            }

            double timeInScene = (context.Timestamp - track.FirstSeen).TotalSeconds;
            context.Features.Set("object_time_in_scene", timeInScene, track.TrackId);
        }
    }
}
