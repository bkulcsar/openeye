using OpenEye.PipelineCore.Rules;

namespace OpenEye.Tests.Rules;

public class TemporalBufferTests
{
    [Fact]
    public void Sustained_TrueFor5Seconds_ReturnsTrue()
    {
        var buf = new TemporalBuffer(capacity: 100);
        var t = DateTimeOffset.UtcNow;
        for (int i = 0; i <= 50; i++)
            buf.Record(t.AddMilliseconds(i * 100), true);

        Assert.True(buf.CheckSustained(TimeSpan.FromSeconds(5), t.AddSeconds(5)));
    }

    [Fact]
    public void Sustained_InterruptedByFalse_ReturnsFalse()
    {
        var buf = new TemporalBuffer(capacity: 100);
        var t = DateTimeOffset.UtcNow;
        for (int i = 0; i < 30; i++)
            buf.Record(t.AddMilliseconds(i * 100), true);
        buf.Record(t.AddSeconds(3), false); // Interruption
        for (int i = 31; i <= 50; i++)
            buf.Record(t.AddMilliseconds(i * 100), true);

        Assert.False(buf.CheckSustained(TimeSpan.FromSeconds(5), t.AddSeconds(5)));
    }

    [Fact]
    public void Within_3OccurrencesIn10Seconds_ReturnsTrue()
    {
        var buf = new TemporalBuffer(capacity: 100);
        var t = DateTimeOffset.UtcNow;
        buf.Record(t, true);
        buf.Record(t.AddSeconds(3), true);
        buf.Record(t.AddSeconds(6), true);

        Assert.True(buf.CheckWithin(TimeSpan.FromSeconds(10), 3, t.AddSeconds(6)));
    }

    [Fact]
    public void Within_TooFewOccurrences_ReturnsFalse()
    {
        var buf = new TemporalBuffer(capacity: 100);
        var t = DateTimeOffset.UtcNow;
        buf.Record(t, true);
        buf.Record(t.AddSeconds(3), true);

        Assert.False(buf.CheckWithin(TimeSpan.FromSeconds(10), 3, t.AddSeconds(6)));
    }

    [Fact]
    public void NoTemporalConfig_ImmediateFire()
    {
        var buf = new TemporalBuffer(capacity: 100);
        buf.Record(DateTimeOffset.UtcNow, true);
        // With no sustained/within, single true is enough
        Assert.True(buf.CheckImmediate());
    }
}
