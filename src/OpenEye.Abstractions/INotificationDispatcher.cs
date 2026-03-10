using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface INotificationDispatcher
{
    Task DispatchAsync(Event evt, NotificationConfig config, CancellationToken ct = default);
}
