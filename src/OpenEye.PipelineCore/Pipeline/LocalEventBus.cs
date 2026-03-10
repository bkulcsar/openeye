using System.Runtime.CompilerServices;
using System.Threading.Channels;
using OpenEye.Abstractions;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Pipeline;

public class LocalEventBus : IGlobalEventBus
{
    private readonly Channel<Event> _channel = Channel.CreateUnbounded<Event>();

    public void Publish(Event evt) => _channel.Writer.TryWrite(evt);

    public async IAsyncEnumerable<Event> Subscribe(
        string? sourceFilter = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            if (sourceFilter is null || evt.SourceId == sourceFilter)
                yield return evt;
        }
    }

    // Explicit interface implementation to satisfy IGlobalEventBus.Subscribe(string?)
    IAsyncEnumerable<Event> IGlobalEventBus.Subscribe(string? sourceFilter) =>
        Subscribe(sourceFilter, CancellationToken.None);
}
