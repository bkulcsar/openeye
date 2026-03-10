using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

namespace OpenEye.DetectionBridge;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration config,
    IConnectionMultiplexer redis,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private volatile IReadOnlySet<string> _classFilter = new HashSet<string>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cameraIds = config.GetSection("CameraIds").Get<string[]>() ?? [];
        if (cameraIds.Length == 0)
        {
            logger.LogWarning("No camera IDs configured. Waiting...");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        var notifier = new RedisConfigNotifier(redis);
        _classFilter = await notifier.GetClassFilterAsync(stoppingToken);
        logger.LogInformation("Loaded class filter: {Classes}", string.Join(", ", _classFilter));

        var configTask = WatchConfigChanges(notifier, stoppingToken);
        var consumeTasks = cameraIds.Select(id => ConsumeLoop(id, stoppingToken));
        await Task.WhenAll(consumeTasks.Append(configTask));
    }

    private async Task WatchConfigChanges(RedisConfigNotifier notifier, CancellationToken ct)
    {
        await foreach (var section in notifier.SubscribeAsync(ct))
        {
            if (section == "class-filter")
            {
                _classFilter = await notifier.GetClassFilterAsync(ct);
                logger.LogInformation("Reloaded class filter: {Classes}", string.Join(", ", _classFilter));
            }
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
