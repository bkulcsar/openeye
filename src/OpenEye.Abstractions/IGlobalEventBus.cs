using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IGlobalEventBus
{
    void Publish(Event evt);
    IAsyncEnumerable<Event> Subscribe(string? sourceFilter = null);
}
