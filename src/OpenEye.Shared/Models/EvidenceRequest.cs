namespace OpenEye.Shared.Models;

public enum EvidenceType { Screenshot, VideoClip, Both }

public record EvidenceRequest(
    string EventId,
    string SourceId,
    DateTimeOffset From,
    DateTimeOffset To,
    EvidenceType Type);
