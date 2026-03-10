// src/OpenEye.Tests/Features/FeatureStoreTests.cs
using OpenEye.Shared.Features;

namespace OpenEye.Tests.Features;

public class FeatureStoreTests
{
    [Fact]
    public void Set_And_Get_GlobalFeature()
    {
        var store = new FeatureStore();
        store.Set("zone_occupancy", 5);
        Assert.Equal(5, store.Get<int>("zone_occupancy"));
    }

    [Fact]
    public void Set_And_Get_ObjectFeature()
    {
        var store = new FeatureStore();
        store.Set("object_speed", 2.5, "track-0");
        Assert.Equal(2.5, store.Get<double>("object_speed", "track-0"));
    }

    [Fact]
    public void Get_MissingFeature_ReturnsDefault()
    {
        var store = new FeatureStore();
        Assert.Equal(0.0, store.Get<double>("missing"));
    }

    [Fact]
    public void TryGet_ExistingFeature_ReturnsTrue()
    {
        var store = new FeatureStore();
        store.Set("speed", 3.0, "track-1");
        Assert.True(store.TryGet<double>("speed", "track-1", out var val));
        Assert.Equal(3.0, val);
    }

    [Fact]
    public void TryGet_MissingFeature_ReturnsFalse()
    {
        var store = new FeatureStore();
        Assert.False(store.TryGet<double>("missing", null, out _));
    }

    [Fact]
    public void ObjectFeatures_Are_Isolated()
    {
        var store = new FeatureStore();
        store.Set("speed", 1.0, "track-0");
        store.Set("speed", 2.0, "track-1");
        Assert.Equal(1.0, store.Get<double>("speed", "track-0"));
        Assert.Equal(2.0, store.Get<double>("speed", "track-1"));
    }

    [Fact]
    public void Clear_RemovesAllFeatures()
    {
        var store = new FeatureStore();
        store.Set("speed", 1.0, "track-0");
        store.Set("zone_occupancy", 5);
        store.Clear();
        Assert.Equal(0.0, store.Get<double>("speed", "track-0"));
        Assert.Equal(0, store.Get<int>("zone_occupancy"));
    }
}
