namespace OpenEye.PipelineCore.Tracking;

public class TrackerConfig
{
    public int MaxLostFrames { get; set; } = 30;
    public double IouThreshold { get; set; } = 0.3;
    public int TrajectoryDepth { get; set; } = 50;
}
