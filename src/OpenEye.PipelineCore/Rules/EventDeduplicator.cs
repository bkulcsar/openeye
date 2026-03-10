namespace OpenEye.PipelineCore.Rules;

public class EventDeduplicator
{
    private readonly Dictionary<(string RuleId, string TrackId), DateTimeOffset> _lastFired = [];

    public bool ShouldSuppress(string ruleId, string trackId, TimeSpan cooldown, DateTimeOffset now)
    {
        if (_lastFired.TryGetValue((ruleId, trackId), out var lastTime))
            return (now - lastTime) < cooldown;
        return false;
    }

    public void RecordFired(string ruleId, string trackId, DateTimeOffset now)
    {
        _lastFired[(ruleId, trackId)] = now;
    }
}
