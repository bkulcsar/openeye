namespace OpenEye.Shared.Models;

public record RuleDefinition(
    string RuleId,
    string Name,
    string ObjectClass,
    string? ZoneId,
    string? TripwireId,
    IReadOnlyList<ConditionConfig> Conditions,
    string Action = "emit_event",
    TimeSpan? Cooldown = null,
    string Logic = "all",
    TimeSpan? Sustained = null,
    TimeSpan? Within = null,
    int? MinOccurrences = null,
    EvidenceType? EvidenceType = null);
