namespace OpenEye.Shared.Models;

public record CameraConfig(
    string Id,
    string Name,
    string StreamUrl,
    string Type,
    int TargetFps,
    bool Enabled);
