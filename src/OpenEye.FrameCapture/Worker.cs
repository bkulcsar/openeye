using System.Collections.Concurrent;
using OpenCvSharp;
using OpenEye.Shared;
using OpenEye.Shared.Redis;
using OpenEye.Shared.Models;
using StackExchange.Redis;

namespace OpenEye.FrameCapture;

public class Worker(
    ILogger<Worker> logger,
    IConnectionMultiplexer redis,
    IConfigProvider configProvider,
    RedisConfigNotifier configNotifier) : BackgroundService
{
    private const int InitialBackoffMs = 5_000;
    private const int MaxBackoffMs = 60_000;
    private const int JpegQuality = 80;
    private readonly ConcurrentDictionary<string, CameraLoop> _loops = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private sealed record CameraEntry(string Id, string Url, int TargetFps);
    private sealed record CameraLoop(CameraEntry Camera, CancellationTokenSource Cts, Task Task);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SyncCameraLoopsAsync(stoppingToken);

        try
        {
            await foreach (var section in configNotifier.SubscribeAsync(stoppingToken))
            {
                if (section == "cameras")
                {
                    logger.LogInformation("Camera config changed; syncing frame-capture loops");
                    await SyncCameraLoopsAsync(stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopAllLoopsAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task SyncCameraLoopsAsync(CancellationToken ct)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            var desired = (await configProvider.GetCamerasAsync(ct))
                .Where(c => c.Enabled)
                .Select(MapCamera)
                .ToDictionary(c => c.Id, StringComparer.Ordinal);

            // Stop removed/disabled cameras.
            foreach (var existing in _loops.Keys.ToList())
            {
                if (!desired.ContainsKey(existing))
                    await StopLoopAsync(existing);
            }

            // Start new cameras or restart changed ones.
            foreach (var (cameraId, desiredCamera) in desired)
            {
                if (_loops.TryGetValue(cameraId, out var running))
                {
                    if (running.Camera == desiredCamera) continue;
                    await StopLoopAsync(cameraId);
                }

                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var task = Task.Run(() => CaptureLoop(desiredCamera, cts.Token), cts.Token);
                _loops[cameraId] = new CameraLoop(desiredCamera, cts, task);
                logger.LogInformation("Started frame capture loop for {CameraId}", cameraId);
            }
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task StopAllLoopsAsync()
    {
        var ids = _loops.Keys.ToList();
        foreach (var id in ids)
            await StopLoopAsync(id);
    }

    private async Task StopLoopAsync(string cameraId)
    {
        if (!_loops.TryRemove(cameraId, out var loop))
            return;

        try
        {
            loop.Cts.Cancel();
            await loop.Task;
        }
        catch (OperationCanceledException) { }
        finally
        {
            loop.Cts.Dispose();
            logger.LogInformation("Stopped frame capture loop for {CameraId}", cameraId);
        }
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

    private static CameraEntry MapCamera(CameraConfig camera)
        => new(camera.Id, camera.StreamUrl, camera.TargetFps);
}
