using OpenEye.PipelineCore.Zones;
using OpenEye.Shared.Models;

namespace OpenEye.Tests.Zones;

public class GeometryTests
{
    private static readonly IReadOnlyList<Point2D> Square =
        [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

    private static readonly IReadOnlyList<Point2D> Triangle =
        [new(0.5, 0), new(1, 1), new(0, 1)];

    [Fact]
    public void PointInPolygon_Inside_ReturnsTrue()
    {
        Assert.True(Geometry.PointInPolygon(Square, new Point2D(0.5, 0.5)));
    }

    [Fact]
    public void PointInPolygon_Outside_ReturnsFalse()
    {
        Assert.False(Geometry.PointInPolygon(Square, new Point2D(1.5, 0.5)));
    }

    [Fact]
    public void PointInPolygon_Triangle_Inside()
    {
        Assert.True(Geometry.PointInPolygon(Triangle, new Point2D(0.5, 0.7)));
    }

    [Fact]
    public void PointInPolygon_Triangle_Outside()
    {
        Assert.False(Geometry.PointInPolygon(Triangle, new Point2D(0.1, 0.1)));
    }

    [Fact]
    public void PointInPolygon_EmptyPolygon_ReturnsFalse()
    {
        Assert.False(Geometry.PointInPolygon([], new Point2D(0.5, 0.5)));
    }

    [Fact]
    public void SegmentsIntersect_Crossing_ReturnsTrue()
    {
        Assert.True(Geometry.SegmentsIntersect(
            new(0, 0), new(1, 1), new(0, 1), new(1, 0)));
    }

    [Fact]
    public void SegmentsIntersect_Parallel_ReturnsFalse()
    {
        Assert.False(Geometry.SegmentsIntersect(
            new(0, 0), new(1, 0), new(0, 1), new(1, 1)));
    }

    [Fact]
    public void SegmentsIntersect_NonTouching_ReturnsFalse()
    {
        Assert.False(Geometry.SegmentsIntersect(
            new(0, 0), new(0.4, 0.4), new(0.6, 0.6), new(1, 1)));
    }

    [Fact]
    public void CrossDirection_LeftToRight_IsPositive()
    {
        double cross = Geometry.CrossProduct(
            new(0.5, 0), new(0.5, 1), new(0.7, 0.5));
        Assert.True(cross > 0);
    }

    [Fact]
    public void CrossDirection_RightToLeft_IsNegative()
    {
        double cross = Geometry.CrossProduct(
            new(0.5, 0), new(0.5, 1), new(0.3, 0.5));
        Assert.True(cross < 0);
    }
}
