using System.Text.Json;
using OpenEye.Abstractions;
using OpenEye.Shared;
using OpenEye.Shared.Models;
using OpenEye.Shared.Postgres;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

namespace OpenEye.EventRouter;

public class Worker(
    ILogger<Worker> logger,
    IConnectionMultiplexer redis,
    PostgresEventStore eventStore,
    IConfigProvider configProvider,
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

                    await eventStore.SaveEventAsync(evt, stoppingToken);

                    logger.LogInformation("Event {EventId} ({EventType}) persisted", evt.EventId, evt.EventType);

                    try
                    {
                        var notifications = await configProvider.GetNotificationsAsync(evt.RuleId, stoppingToken);
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
}
