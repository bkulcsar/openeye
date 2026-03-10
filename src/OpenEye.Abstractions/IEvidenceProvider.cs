using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IEvidenceProvider
{
    Task<string?> CaptureEvidenceAsync(EvidenceRequest request, CancellationToken ct = default);
}
