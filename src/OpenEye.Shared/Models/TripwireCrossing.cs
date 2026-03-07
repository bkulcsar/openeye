namespace OpenEye.Shared.Models;

public record TripwireCrossing(
    string TrackId,
    string TripwireId,
    DateTimeOffset Timestamp);
