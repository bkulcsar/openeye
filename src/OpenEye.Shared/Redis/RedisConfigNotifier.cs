using System.Runtime.CompilerServices;
using System.Text.Json;
using StackExchange.Redis;

namespace OpenEye.Shared.Redis;

public class RedisConfigNotifier(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly ISubscriber _sub = redis.GetSubscriber();

    public async Task PublishChangeAsync(string configSection, CancellationToken ct = default)
    {
        await _sub.PublishAsync(RedisChannel.Literal("config:changed"), configSection);
    }

    public async IAsyncEnumerable<string> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var queue = System.Threading.Channels.Channel.CreateUnbounded<string>();
        await _sub.SubscribeAsync(RedisChannel.Literal("config:changed"), (_, value) =>
        {
            queue.Writer.TryWrite(value.ToString());
        });

        await foreach (var section in queue.Reader.ReadAllAsync(ct))
            yield return section;
    }

    public async Task SetClassFilterAsync(IReadOnlySet<string> classes, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(classes);
        await _db.StringSetAsync("config:class-filter", json);
    }

    public async Task<IReadOnlySet<string>> GetClassFilterAsync(CancellationToken ct = default)
    {
        var json = await _db.StringGetAsync("config:class-filter");
        if (json.IsNull) return new HashSet<string>();
        return JsonSerializer.Deserialize<HashSet<string>>(json.ToString()) ?? [];
    }
}
