using OpenEye.Shared.Models;

namespace OpenEye.Shared;

public interface IConfigProvider
{
    Task<IReadOnlyList<CameraConfig>> GetCamerasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Zone>> GetZonesAsync(string? sourceId = null, CancellationToken ct = default);
    Task<IReadOnlyList<Tripwire>> GetTripwiresAsync(string? sourceId = null, CancellationToken ct = default);
    Task<IReadOnlyList<PrimitiveConfig>> GetPrimitivesAsync(string? sourceId = null, CancellationToken ct = default);
    Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NotificationConfig>> GetNotificationsAsync(string? ruleId = null, CancellationToken ct = default);
}
