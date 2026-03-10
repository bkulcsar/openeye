using OpenEye.Abstractions;
using OpenEye.Shared.Features;
using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Rules;

public class DefaultRuleEngine(IConditionRegistry conditionRegistry, IRuleStateStore stateStore) : IRuleEngine
{
    private readonly Dictionary<string, TemporalBuffer> _temporalBuffers = [];
    private readonly EventDeduplicator _deduplicator = new();

    public IReadOnlyList<Event> Evaluate(FrameContext context, IReadOnlyList<RuleDefinition> rules)
    {
        var events = new List<Event>();

        foreach (var rule in rules)
        {
            var matchingTracks = context.Tracks
                .Where(t => t.State == TrackState.Active && t.ClassLabel == rule.ObjectClass)
                .ToList();

            foreach (var track in matchingTracks)
            {
                bool allConditionsMet = rule.Logic == "any"
                    ? rule.Conditions.Any(condConfig =>
                    {
                        var condition = conditionRegistry.Get(condConfig.Type);
                        return condition.Evaluate(context, rule, track);
                    })
                    : rule.Conditions.All(condConfig =>
                    {
                        var condition = conditionRegistry.Get(condConfig.Type);
                        return condition.Evaluate(context, rule, track);
                    });

                var bufferKey = $"{rule.RuleId}:{track.TrackId}";
                if (!_temporalBuffers.TryGetValue(bufferKey, out var buffer))
                {
                    buffer = new TemporalBuffer();
                    _temporalBuffers[bufferKey] = buffer;
                }

                buffer.Record(context.Timestamp, allConditionsMet);

                bool shouldFire;
                if (rule.Sustained is not null)
                    shouldFire = buffer.CheckSustained(rule.Sustained.Value, context.Timestamp);
                else if (rule.Within is not null)
                    shouldFire = buffer.CheckWithin(rule.Within.Value, rule.MinOccurrences ?? 1, context.Timestamp);
                else
                    shouldFire = buffer.CheckImmediate();

                if (!shouldFire) continue;

                var cooldown = rule.Cooldown ?? TimeSpan.FromSeconds(30);
                if (_deduplicator.ShouldSuppress(rule.RuleId, track.TrackId, cooldown, context.Timestamp))
                    continue;

                _deduplicator.RecordFired(rule.RuleId, track.TrackId, context.Timestamp);

                var eventId = Guid.NewGuid().ToString();
                var metadata = new Dictionary<string, object> { ["action"] = rule.Action };

                if (rule.EvidenceType is not null)
                {
                    metadata["evidenceRequestId"] = Guid.NewGuid().ToString();
                    metadata["evidenceFrom"] = context.Timestamp.AddSeconds(-10);
                    metadata["evidenceTo"] = context.Timestamp.AddSeconds(5);
                    metadata["evidenceType"] = rule.EvidenceType.Value.ToString();
                }

                events.Add(new Event(
                    EventId: eventId,
                    EventType: rule.Name,
                    Timestamp: context.Timestamp,
                    SourceId: context.SourceId,
                    ZoneId: rule.ZoneId,
                    TrackedObjects: [track],
                    RuleId: rule.RuleId,
                    Metadata: metadata
                ));
            }
        }

        // Clean up state for expired tracks
        foreach (var track in context.Tracks.Where(t => t.State == TrackState.Expired))
        {
            stateStore.RemoveByTrack(track.TrackId);
            // Remove temporal buffers for this track
            var keysToRemove = _temporalBuffers.Keys
                .Where(k => k.EndsWith($":{track.TrackId}"))
                .ToList();
            foreach (var key in keysToRemove)
                _temporalBuffers.Remove(key);
        }

        return events;
    }
}
