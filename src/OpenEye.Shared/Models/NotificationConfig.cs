namespace OpenEye.Shared.Models;

public record NotificationChannel(
    string Type,
    Dictionary<string, string> Config);

public record NotificationConfig(
    string RuleId,
    IReadOnlyList<NotificationChannel> Channels);
