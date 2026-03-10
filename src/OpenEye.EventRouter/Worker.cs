using System.Text.Json;
using Dapper;
using Npgsql;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

namespace OpenEye.EventRouter;

public class Worker(
    ILogger<Worker> logger,
    IConnectionMultiplexer redis,
    NpgsqlDataSource dataSource,
    INotificationDispatcher notificationDispatcher) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new RedisStreamConsumer(redis, "events", "event-router", "worker-0");

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

                    await using var conn = await dataSource.OpenConnectionAsync(stoppingToken);
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

                    logger.LogInformation("Event {EventId} ({EventType}) persisted", evt.EventId, evt.EventType);

                    try
                    {
                        var notifications = await GetNotificationsAsync(conn, evt.RuleId, stoppingToken);
                        foreach (var notif in notifications)
                        {
                            await notificationDispatcher.DispatchAsync(evt, notif, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to dispatch notifications for event {EventId}", evt.EventId);
                    }

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

    private static async Task<IReadOnlyList<NotificationConfig>> GetNotificationsAsync(
        NpgsqlConnection conn, string ruleId, CancellationToken ct)
    {
        var rows = await conn.QueryAsync<dynamic>(
            "SELECT rule_id, channels FROM notification_configs WHERE rule_id = @RuleId",
            new { RuleId = ruleId });
        return rows.Select(r => new NotificationConfig(
            (string)r.rule_id,
            JsonSerializer.Deserialize<List<NotificationChannel>>((string)r.channels) ?? []
        )).ToList();
    }
}
