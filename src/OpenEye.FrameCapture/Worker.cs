using System.Text.Json;
using OpenEye.PipelineCore.Pipeline;
using StackExchange.Redis;

namespace OpenEye.FrameCapture;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration config,
    IConnectionMultiplexer redis) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cameras = config.GetSection("Cameras").Get<CameraEntry[]>() ?? [];
        if (cameras.Length == 0)
        {
            logger.LogWarning("No cameras configured. Waiting...");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        var tasks = cameras.Select(c => CaptureLoop(c, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task CaptureLoop(CameraEntry camera, CancellationToken ct)
    {
        var publisher = new RedisStreamPublisher(redis);
        var streamKey = $"frames:{camera.Id}";
        logger.LogInformation("Starting capture for {CameraId} at {Url}", camera.Id, camera.Url);

        // Note: OpenCvSharp capture would go here. Currently publishing heartbeat frames.
        long frameIndex = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await publisher.PublishAsync(streamKey, new Dictionary<string, string>
                {
                    ["camera_id"] = camera.Id,
                    ["frame_index"] = (frameIndex++).ToString(),
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                    ["image"] = string.Empty
                });

                if (camera.TargetFps > 0)
                    await Task.Delay(1000 / camera.TargetFps, ct);
                else
                    await Task.Delay(200, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Error capturing from {CameraId}", camera.Id);
                await Task.Delay(5000, ct);
            }
        }
    }
}

public record CameraEntry
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public int TargetFps { get; init; } = 5;
}
