namespace OpenEye.Tests.Services;

public class FrameCaptureWorkerTests
{
    [Fact]
    public void Worker_IsBackgroundService()
    {
        var workerType = typeof(OpenEye.FrameCapture.Worker);
        Assert.True(typeof(Microsoft.Extensions.Hosting.BackgroundService).IsAssignableFrom(workerType));
    }
}
