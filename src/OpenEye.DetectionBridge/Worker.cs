using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenEye.Shared;
using OpenEye.Shared.Models;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

namespace OpenEye.DetectionBridge;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration config,
    IConnectionMultiplexer redis,
    IConfigProvider configProvider,
    RedisConfigNotifier configNotifier,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private volatile IReadOnlySet<string> _classFilter = new HashSet<string>();
    private readonly ConcurrentDictionary<string, CameraLoop> _loops = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private sealed record CameraLoop(CancellationTokenSource Cts, Task Task);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _classFilter = await configNotifier.GetClassFilterAsync(stoppingToken);
        logger.LogInformation("Loaded class filter: {Classes}", string.Join(", ", _classFilter));

        await SyncCameraLoopsAsync(stoppingToken);

        try
        {
            await foreach (var section in configNotifier.SubscribeAsync(stoppingToken))
            {
                if (section == "class-filter")
                {
                    _classFilter = await configNotifier.GetClassFilterAsync(stoppingToken);
                    logger.LogInformation("Reloaded class filter: {Classes}", string.Join(", ", _classFilter));
                }
                else if (section == "cameras")
                {
                    logger.LogInformation("Camera config changed; syncing detection-bridge loops");
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
                .Select(c => c.Id)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var existing in _loops.Keys.ToList())
            {
                if (!desired.Contains(existing))
                    await StopLoopAsync(existing);
            }

            foreach (var cameraId in desired)
            {
                if (_loops.ContainsKey(cameraId))
                    continue;
                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var task = Task.Run(() => ConsumeLoop(cameraId, cts.Token), cts.Token);
                _loops[cameraId] = new CameraLoop(cts, task);
                logger.LogInformation("Started detection-bridge loop for {CameraId}", cameraId);
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
            logger.LogInformation("Stopped detection-bridge loop for {CameraId}", cameraId);
        }
    }

    private string ApplyClassFilter(string detectionsJson)
    {
        var filter = _classFilter;
        if (filter.Count == 0)
            return detectionsJson;

        var doc = JsonNode.Parse(detectionsJson);
        if (doc?["predictions"] is JsonArray predictions)
        {
            var filtered = new JsonArray();
            foreach (var pred in predictions)
            {
                var cls = pred?["class"]?.GetValue<string>();
                if (cls != null && filter.Contains(cls))
                    filtered.Add(pred!.DeepClone());
            }
            doc["predictions"] = filtered;
            return doc.ToJsonString();
        }
        return detectionsJson;
    }

    private async Task ConsumeLoop(string cameraId, CancellationToken ct)
    {
        var consumer = new RedisStreamConsumer(redis, $"frames:{cameraId}", "detection-bridge", $"worker-{cameraId}");
        var publisher = new RedisStreamPublisher(redis);
        var roboflowUrl = config["Roboflow:Url"] ?? "http://localhost:9001";
        var roboflowApiKey = config["Roboflow:ApiKey"] ?? "";
        var modelId = config["Roboflow:ModelId"] ?? "yolov8n-640";
        var httpClient = httpClientFactory.CreateClient();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var entries = await consumer.ReadAsync(1);
                if (entries.Length == 0)
                {
                    await Task.Delay(50, ct);
                    continue;
                }

                foreach (var entry in entries)
                {
                    var image = entry["image"].ToString();
                    var frameIndex = entry["frame_index"].ToString();
                    var timestamp = entry["timestamp"].ToString();

                    var request = new HttpRequestMessage(HttpMethod.Post, $"{roboflowUrl}/{modelId}")
                    {
                        Content = JsonContent.Create(new { image })
                    };
                    if (!string.IsNullOrEmpty(roboflowApiKey))
                        request.Headers.Add("x-api-key", roboflowApiKey);
                    var response = await httpClient.SendAsync(request, ct);

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync(ct);
                        var filtered = ApplyClassFilter(result);
                        await publisher.PublishAsync($"detections:{cameraId}", new Dictionary<string, string>
                        {
                            ["camera_id"] = cameraId,
                            ["frame_index"] = frameIndex,
                            ["timestamp"] = timestamp,
                            ["detections"] = filtered
                        });
                    }
                    else
                    {
                        logger.LogWarning("Roboflow returned {Status} for camera {Camera}",
                            response.StatusCode, cameraId);
                    }

                    await consumer.AcknowledgeAsync(entry.Id);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Error processing frames for {CameraId}", cameraId);
                await Task.Delay(1000, ct);
            }
        }
    }
}
