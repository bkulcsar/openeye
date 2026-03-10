using OpenEye.Shared.Models;

namespace OpenEye.Abstractions;

public interface IDetectionClient
{
    Task<IReadOnlyList<Detection>> DetectAsync(string framePath, IReadOnlySet<string> classFilter, CancellationToken ct = default);
}
