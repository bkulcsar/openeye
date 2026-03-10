using System.Text.Json;
using Dapper;
using Npgsql;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.Shared.Models;
using StackExchange.Redis;

namespace OpenEye.EventRouter;

public class Worker(
    ILogger<Worker> logger,
    IConfiguration config,
    IConnectionMultiplexer redis) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new RedisStreamConsumer(redis, "events", "event-router", "worker-0");
        var connString = config.GetConnectionString("openeye") ?? "";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await consumer.ReadAsync(10);
                if (entries.Length == 0)
                {
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                {
                    var eventJson = entry["event"].ToString();
                    var evt = JsonSerializer.Deserialize<Event>(eventJson);
                    if (evt is null) continue;

                    if (!string.IsNullOrEmpty(connString))
                    {
                        await using var conn = new NpgsqlConnection(connString);
                        await conn.ExecuteAsync(
                            """
                            INSERT INTO events (event_id, event_type, timestamp, source_id, zone_id, rule_id, tracked_objects, metadata)
                            VALUES (@EventId, @EventType, @Timestamp, @SourceId, @ZoneId, @RuleId, @TrackedObjects::jsonb, @Metadata::jsonb)
                            ON CONFLICT (event_id) DO NOTHING
                            """,
                            new
                            {
                                evt.EventId,
                                evt.EventType,
                                Timestamp = evt.Timestamp.UtcDateTime,
                                evt.SourceId,
                                evt.ZoneId,
                                evt.RuleId,
                                TrackedObjects = JsonSerializer.Serialize(evt.TrackedObjects),
                                Metadata = evt.Metadata is not null ? JsonSerializer.Serialize(evt.Metadata) : null
                            });
                    }

                    logger.LogInformation("Event {EventId} ({EventType}) persisted", evt.EventId, evt.EventType);
                    await consumer.AcknowledgeAsync(entry.Id);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error routing events");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
