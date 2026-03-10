namespace OpenEye.Tests.Services;

public class DetectionBridgeWorkerTests
{
    [Fact]
    public void Worker_IsBackgroundService()
    {
        var workerType = typeof(OpenEye.DetectionBridge.Worker);
        Assert.True(typeof(Microsoft.Extensions.Hosting.BackgroundService).IsAssignableFrom(workerType));
    }
}
