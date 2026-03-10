using System.Text.Json;
using StackExchange.Redis;

namespace OpenEye.Shared.Redis;

public class RedisStreamPublisher(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task PublishAsync<T>(string streamKey, T message, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(message);
        await _db.StreamAddAsync(streamKey, [new NameValueEntry("data", json)]);
    }
}
