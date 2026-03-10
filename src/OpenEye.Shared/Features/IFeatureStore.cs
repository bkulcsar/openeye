namespace OpenEye.Shared.Features;

public interface IFeatureStore
{
    void Set<T>(string name, T value, string? objectId = null);
    T Get<T>(string name, string? objectId = null);
    bool TryGet<T>(string name, string? objectId, out T value);
    void Clear();
}
