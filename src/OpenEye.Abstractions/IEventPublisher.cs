using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync(Event evt, CancellationToken ct = default);
}
