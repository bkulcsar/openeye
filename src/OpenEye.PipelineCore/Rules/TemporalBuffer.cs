namespace OpenEye.PipelineCore.Rules;

public class TemporalBuffer(int capacity = 256)
{
    private readonly (DateTimeOffset Timestamp, bool Result)[] _ring = new (DateTimeOffset, bool)[capacity];
    private int _head;
    private int _count;

    public void Record(DateTimeOffset timestamp, bool result)
    {
        _ring[_head] = (timestamp, result);
        _head = (_head + 1) % capacity;
        if (_count < capacity) _count++;
    }

    public bool CheckSustained(TimeSpan duration, DateTimeOffset now)
    {
        var cutoff = now - duration;
        DateTimeOffset? newestInWindow = null;
        DateTimeOffset oldestInWindow = default;

        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + capacity) % capacity;
            var entry = _ring[idx];
            if (entry.Timestamp < cutoff) break;
            if (!entry.Result) return false;

            newestInWindow ??= entry.Timestamp;  // set on first iteration (newest)
            oldestInWindow = entry.Timestamp;    // updated each iteration (ends at oldest in-window)
        }

        if (newestInWindow is null) return false; // no entries in the window
        return (newestInWindow.Value - oldestInWindow) >= duration * 0.9;
    }

    public bool CheckWithin(TimeSpan window, int minOccurrences, DateTimeOffset now)
    {
        var cutoff = now - window;
        int count = 0;
        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + capacity) % capacity;
            var entry = _ring[idx];
            if (entry.Timestamp < cutoff) break;
            if (entry.Result) count++;
        }
        return count >= minOccurrences;
    }

    public bool CheckImmediate()
    {
        if (_count == 0) return false;
        return _ring[(_head - 1 + capacity) % capacity].Result;
    }
}
