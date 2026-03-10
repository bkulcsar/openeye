using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IEventBus
{
    void Publish(Event evt);
}
