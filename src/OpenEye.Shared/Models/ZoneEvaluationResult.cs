namespace OpenEye.Shared.Models;

public record ZoneEvaluationResult(
    IReadOnlyList<ZoneTransition> Transitions,
    IReadOnlyList<TripwireCrossing> TripwireCrossings,
    IReadOnlyList<ZonePresence> ActivePresences);
