using OpenEye.Shared.Models;

namespace OpenEye.PipelineCore.Zones;

public static class Geometry
{
    /// <summary>Ray-casting point-in-polygon test.</summary>
    public static bool PointInPolygon(IReadOnlyList<Point2D> polygon, Point2D point)
    {
        if (polygon.Count < 3) return false;

        bool inside = false;
        int n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];

            if ((pi.Y > point.Y) != (pj.Y > point.Y) &&
                point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>Tests if two line segments intersect (proper crossing only).</summary>
    public static bool SegmentsIntersect(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        double d1 = CrossProduct(b1, b2, a1);
        double d2 = CrossProduct(b1, b2, a2);
        double d3 = CrossProduct(a1, a2, b1);
        double d4 = CrossProduct(a1, a2, b2);

        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    /// <summary>
    /// Cross product of vectors (o→a) and (o→b).
    /// Positive = b is right of o→a direction, Negative = left.
    /// </summary>
    public static double CrossProduct(Point2D o, Point2D a, Point2D b)
    {
        return (a.Y - o.Y) * (b.X - o.X) - (a.X - o.X) * (b.Y - o.Y);
    }

    /// <summary>Computes centroid of a bounding box.</summary>
    public static Point2D Centroid(BoundingBox box) =>
        new(box.X + box.Width / 2, box.Y + box.Height / 2);
}
