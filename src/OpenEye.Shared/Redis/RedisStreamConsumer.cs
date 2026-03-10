using System.Runtime.CompilerServices;
using System.Text.Json;
using StackExchange.Redis;

namespace OpenEye.Shared.Redis;

public class RedisStreamConsumer(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task EnsureGroupAsync(string streamKey, string groupName)
    {
        try
        {
            await _db.StreamCreateConsumerGroupAsync(streamKey, groupName, "0-0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists
        }
    }

    public async IAsyncEnumerable<(string Id, T Message)> ConsumeAsync<T>(
        string streamKey, string groupName, string consumerName,
        int batchSize = 10, [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var entries = await _db.StreamReadGroupAsync(
                streamKey, groupName, consumerName, ">", batchSize);

            if (entries.Length == 0)
            {
                await Task.Delay(100, ct);
                continue;
            }

            foreach (var entry in entries)
            {
                var json = entry.Values.FirstOrDefault(v => v.Name == "data").Value;
                if (json.IsNull) continue;
                var message = JsonSerializer.Deserialize<T>(json.ToString());
                if (message is not null)
                    yield return (entry.Id!, message);
            }
        }
    }

    public async Task AckAsync(string streamKey, string groupName, string messageId)
    {
        await _db.StreamAcknowledgeAsync(streamKey, groupName, messageId);
    }
}
