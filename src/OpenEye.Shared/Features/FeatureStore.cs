namespace OpenEye.Shared.Features;

public class FeatureStore : IFeatureStore
{
    private readonly Dictionary<FeatureKey, object> _values = [];

    public void Set<T>(string name, T value, string? objectId = null)
    {
        _values[new FeatureKey(name, objectId)] = value!;
    }

    public T Get<T>(string name, string? objectId = null)
    {
        return _values.TryGetValue(new FeatureKey(name, objectId), out var val)
            ? (T)val
            : default!;
    }

    public bool TryGet<T>(string name, string? objectId, out T value)
    {
        if (_values.TryGetValue(new FeatureKey(name, objectId), out var val))
        {
            value = (T)val;
            return true;
        }
        value = default!;
        return false;
    }

    public void Clear() => _values.Clear();
}
