namespace OpenEye.Abstractions;

public interface IFramePublisher
{
    Task PublishFrameAsync(string cameraId, long frameIndex, string framePath, DateTimeOffset timestamp, CancellationToken ct = default);
}
