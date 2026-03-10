using StackExchange.Redis;

namespace OpenEye.Shared.Redis;

public class RedisStreamConsumer(IConnectionMultiplexer redis, string streamKey, string groupName, string consumerName)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private bool _groupCreated;

    public async Task EnsureGroupAsync()
    {
        if (_groupCreated) return;
        try
        {
            await _db.StreamCreateConsumerGroupAsync(streamKey, groupName, "0-0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists
        }
        _groupCreated = true;
    }

    public async Task<StreamEntry[]> ReadAsync(int count = 10)
    {
        await EnsureGroupAsync();
        var entries = await _db.StreamReadGroupAsync(streamKey, groupName, consumerName, ">", count);
        return entries;
    }

    public async Task AcknowledgeAsync(RedisValue messageId)
    {
        await _db.StreamAcknowledgeAsync(streamKey, groupName, messageId);
    }
}
