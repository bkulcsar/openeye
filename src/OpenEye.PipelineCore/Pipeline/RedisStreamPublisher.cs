using StackExchange.Redis;

namespace OpenEye.PipelineCore.Pipeline;

public class RedisStreamPublisher(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task PublishAsync(string streamKey, Dictionary<string, string> fields)
    {
        var entries = fields.Select(f => new NameValueEntry(f.Key, f.Value)).ToArray();
        await _db.StreamAddAsync(streamKey, entries, maxLength: 1000, useApproximateMaxLength: true);
    }
}
