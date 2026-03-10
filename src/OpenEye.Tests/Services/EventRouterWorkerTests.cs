namespace OpenEye.Tests.Services;

public class EventRouterWorkerTests
{
    [Fact]
    public void Worker_IsBackgroundService()
    {
        var workerType = typeof(OpenEye.EventRouter.Worker);
        Assert.True(typeof(Microsoft.Extensions.Hosting.BackgroundService).IsAssignableFrom(workerType));
    }
}
