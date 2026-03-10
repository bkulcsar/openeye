using System.Text;
using System.Text.Json;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.EventRouter;

public class WebhookNotificationDispatcher(
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookNotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task DispatchAsync(Event evt, NotificationConfig config, CancellationToken ct = default)
    {
        foreach (var channel in config.Channels)
        {
            if (!string.Equals(channel.Type, "webhook", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Unsupported notification channel type '{Type}' for rule {RuleId}", channel.Type, config.RuleId);
                continue;
            }

            if (!channel.Config.TryGetValue("url", out var url) || string.IsNullOrWhiteSpace(url))
            {
                logger.LogWarning("Webhook channel for rule {RuleId} has no url configured", config.RuleId);
                continue;
            }

            try
            {
                using var client = httpClientFactory.CreateClient("webhooks");
                var json = JsonSerializer.Serialize(evt);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content, ct);
                logger.LogInformation("Webhook dispatched for event {EventId} to {Url} — status {StatusCode}",
                    evt.EventId, url, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch webhook for event {EventId} to {Url}", evt.EventId, url);
            }
        }
    }
}
