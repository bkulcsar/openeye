using OpenCvSharp;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

namespace OpenEye.FrameCapture;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration config,
    IConnectionMultiplexer redis) : BackgroundService
{
    private const int InitialBackoffMs = 5_000;
    private const int MaxBackoffMs = 60_000;
    private const int JpegQuality = 80;

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
        long frameIndex = 0;
        var encodingParams = new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, JpegQuality) };

        while (!ct.IsCancellationRequested)
        {
            using var capture = new VideoCapture();
            var backoffMs = InitialBackoffMs;

            logger.LogInformation("Opening capture for {CameraId} at {Url}", camera.Id, camera.Url);

            if (!TryOpenCapture(capture, camera.Url))
            {
                while (!ct.IsCancellationRequested)
                {
                    logger.LogError("Failed to open capture for {CameraId} at {Url}. Retrying in {BackoffMs}ms",
                        camera.Id, camera.Url, backoffMs);
                    await Task.Delay(backoffMs, ct);
                    backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);

                    if (TryOpenCapture(capture, camera.Url))
                        break;
                }

                if (ct.IsCancellationRequested) break;
            }

            logger.LogInformation("Capture opened for {CameraId}", camera.Id);

            using var frame = new Mat();
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!capture.Read(frame) || frame.Empty())
                    {
                        logger.LogWarning("Stream disconnected for {CameraId}. Reconnecting...", camera.Id);
                        break; // exit inner loop to reconnect via outer loop
                    }

                    Cv2.ImEncode(".jpg", frame, out var jpegBytes, encodingParams);
                    var base64 = Convert.ToBase64String(jpegBytes);

                    await publisher.PublishAsync(streamKey, new Dictionary<string, string>
                    {
                        ["camera_id"] = camera.Id,
                        ["frame_index"] = (frameIndex++).ToString(),
                        ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                        ["image"] = base64
                    });

                    if (camera.TargetFps > 0)
                        await Task.Delay(1000 / camera.TargetFps, ct);
                    else
                        await Task.Delay(200, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error capturing from {CameraId}", camera.Id);
                    break; // reconnect
                }
            }
        }
    }

    private static bool TryOpenCapture(VideoCapture capture, string url)
    {
        // Support device index (e.g. "0") or stream URL (RTSP/MJPEG)
        if (int.TryParse(url, out var deviceIndex))
            capture.Open(deviceIndex);
        else
            capture.Open(url);

        return capture.IsOpened();
    }
}

public record CameraEntry
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public int TargetFps { get; init; } = 5;
}
