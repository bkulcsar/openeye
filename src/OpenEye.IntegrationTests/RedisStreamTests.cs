using OpenEye.PipelineCore.Pipeline;
using StackExchange.Redis;

namespace OpenEye.IntegrationTests;

/// <summary>
/// These tests require a running Redis instance.
/// Run with: docker run -d -p 6379:6379 redis:7
/// Or skip if Redis is unavailable.
/// </summary>
public class RedisStreamTests : IAsyncLifetime
{
    private IConnectionMultiplexer? _redis;
    private const string TestStream = "test:integration";

    public async Task InitializeAsync()
    {
        try
        {
            _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379,abortConnect=false,connectTimeout=2000");
            if (!_redis.IsConnected)
            {
                _redis.Dispose();
                _redis = null;
            }
        }
        catch
        {
            // Redis not available, tests will be skipped
        }
    }

    public async Task DisposeAsync()
    {
        if (_redis is not null)
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(TestStream);
            }
            catch { }
            _redis.Dispose();
        }
    }

    [Fact]
    public async Task PublishAndConsume_RoundTrip()
    {
        if (_redis is null) return;

        var publisher = new RedisStreamPublisher(_redis);
        var consumer = new RedisStreamConsumer(_redis, TestStream, "test-group", "test-consumer");

        // Publish
        await publisher.PublishAsync(TestStream, new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        });

        // Consume
        var entries = await consumer.ReadAsync(10);
        Assert.Single(entries);
        Assert.Equal("value1", entries[0]["key1"].ToString());
        Assert.Equal("value2", entries[0]["key2"].ToString());

        // Acknowledge
        await consumer.AcknowledgeAsync(entries[0].Id);
    }

    [Fact]
    public async Task Consumer_GroupAlreadyExists_DoesNotThrow()
    {
        if (_redis is null) return; // Skip if Redis not available

        var consumer1 = new RedisStreamConsumer(_redis, TestStream, "test-group-2", "consumer-a");
        var consumer2 = new RedisStreamConsumer(_redis, TestStream, "test-group-2", "consumer-b");

        // Both should be able to ensure group without error
        await consumer1.EnsureGroupAsync();
        await consumer2.EnsureGroupAsync();
    }
}
