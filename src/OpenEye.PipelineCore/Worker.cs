using System.Collections.Concurrent;
using System.Text.Json;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.Shared;
using OpenEye.Shared.Models;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

namespace OpenEye.PipelineCore;

public class Worker(
    ILogger<Worker> logger,
    IConnectionMultiplexer redis,
    IConfigProvider configProvider,
    RedisConfigNotifier configNotifier,
    PipelineOrchestrator orchestrator) : BackgroundService
{
    private readonly ConcurrentDictionary<string, CameraLoop> _loops = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private sealed record CameraLoop(CancellationTokenSource Cts, Task Task);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Load initial config from database
        await ReloadPipelineConfigAsync(stoppingToken);

        // Publish required class filter to Redis
        await PublishClassFilterAsync();

        // Start per-camera loops from DB camera configuration.
        await SyncCameraLoopsAsync(stoppingToken);

        await ListenForConfigChangesAsync(stoppingToken);
    }

    private async Task ReloadPipelineConfigAsync(CancellationToken ct)
    {
        var zones = await configProvider.GetZonesAsync(ct: ct);
        var tripwires = await configProvider.GetTripwiresAsync(ct: ct);
        var primitives = await configProvider.GetPrimitivesAsync(ct: ct);
        var rules = await configProvider.GetRulesAsync(ct);
        orchestrator.ReloadConfig(zones, tripwires, primitives, rules);
    }

    private async Task PublishClassFilterAsync()
    {
        var classFilter = orchestrator.GetRequiredClasses();
        var db = redis.GetDatabase();
        await db.StringSetAsync("config:class-filter", JsonSerializer.Serialize(classFilter));
    }

    private async Task ListenForConfigChangesAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var section in configNotifier.SubscribeAsync(ct))
            {
                logger.LogInformation("Config change received ({Section}), reloading pipeline config", section);
                try
                {
                    if (section == "cameras")
                        await SyncCameraLoopsAsync(ct);

                    await ReloadPipelineConfigAsync(ct);
                    await PublishClassFilterAsync();
                    logger.LogInformation("Pipeline config reloaded successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to reload pipeline config");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
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
                logger.LogInformation("Started pipeline loop for {CameraId}", cameraId);
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
            logger.LogInformation("Stopped pipeline loop for {CameraId}", cameraId);
        }
    }

    private async Task ConsumeLoop(string cameraId, CancellationToken ct)
    {
        var consumer = new RedisStreamConsumer(redis, $"detections:{cameraId}", "pipeline-core", $"worker-{cameraId}");
        var publisher = new RedisStreamPublisher(redis);

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
                    var detectionsJson = entry["detections"].ToString();
                    var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(entry["timestamp"].ToString()));
                    var frameIndex = long.Parse(entry["frame_index"].ToString());

                    var detections = ParseDetections(detectionsJson, cameraId, timestamp, frameIndex);
                    var events = orchestrator.ProcessFrame(cameraId, detections, timestamp);

                    foreach (var evt in events)
                    {
                        await publisher.PublishAsync("events", new Dictionary<string, string>
                        {
                            ["event"] = JsonSerializer.Serialize(evt)
                        });
                        logger.LogInformation("Published event {EventId} ({EventType})", evt.EventId, evt.EventType);
                    }

                    await consumer.AcknowledgeAsync(entry.Id);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Error processing detections for {CameraId}", cameraId);
                await Task.Delay(1000, ct);
            }
        }
    }

    private List<Detection> ParseDetections(string json, string sourceId, DateTimeOffset timestamp, long frameIndex)
    {
        var detections = new List<Detection>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("predictions", out var predictions))
            {
                foreach (var pred in predictions.EnumerateArray())
                {
                    var x = pred.GetProperty("x").GetDouble();
                    var y = pred.GetProperty("y").GetDouble();
                    var w = pred.GetProperty("width").GetDouble();
                    var h = pred.GetProperty("height").GetDouble();
                    var label = pred.GetProperty("class").GetString() ?? "unknown";
                    var confidence = pred.GetProperty("confidence").GetDouble();

                    detections.Add(new Detection(label, new BoundingBox(x - w / 2, y - h / 2, w, h), confidence, timestamp, sourceId, frameIndex));
                }
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to parse detections JSON for {SourceId}", sourceId); }
        return detections;
    }
}
