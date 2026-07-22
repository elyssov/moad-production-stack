namespace Moad.Engine;

public static class AuthoredObstacleGeometry
{
    public static bool BlocksMovement(
        IReadOnlyList<AuthoredObstacle> obstacles,
        int lane,
        Vec2 previousFoot,
        Vec2 nextFoot,
        float actorHeight,
        float actorHalfWidth)
    {
        foreach (var obstacle in obstacles.Where(item => item.Lane == lane && item.BlocksMovement))
        {
            var previousIntersects = IntersectsActor(obstacle.Polygon, previousFoot, actorHeight, actorHalfWidth);
            var nextIntersects = IntersectsActor(obstacle.Polygon, nextFoot, actorHeight, actorHalfWidth);
            if (nextIntersects && !previousIntersects)
            {
                return true;
            }
        }
        return false;
    }

    public static bool IntersectsActor(
        IReadOnlyList<Vec2> polygon,
        Vec2 foot,
        float actorHeight,
        float actorHalfWidth)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        var left = foot.X - actorHalfWidth;
        var right = foot.X + actorHalfWidth;
        var top = foot.Y - actorHeight;
        var bottom = foot.Y - 2f;
        var corners = new[]
        {
            new Vec2(left, top),
            new Vec2(right, top),
            new Vec2(right, bottom),
            new Vec2(left, bottom),
        };

        if (corners.Any(point => PointInPolygon(point, polygon))
            || polygon.Any(point => point.X >= left && point.X <= right && point.Y >= top && point.Y <= bottom))
        {
            return true;
        }

        for (var polygonIndex = 0; polygonIndex < polygon.Count; polygonIndex++)
        {
            var polygonStart = polygon[polygonIndex];
            var polygonEnd = polygon[(polygonIndex + 1) % polygon.Count];
            for (var edgeIndex = 0; edgeIndex < corners.Length; edgeIndex++)
            {
                if (SegmentsIntersect(
                        polygonStart,
                        polygonEnd,
                        corners[edgeIndex],
                        corners[(edgeIndex + 1) % corners.Length]))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool PointInPolygon(Vec2 point, IReadOnlyList<Vec2> polygon)
    {
        var inside = false;
        for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
        {
            var a = polygon[current];
            var b = polygon[previous];
            if ((a.Y > point.Y) == (b.Y > point.Y))
            {
                continue;
            }
            var crossingX = (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (point.X < crossingX)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static bool SegmentsIntersect(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        var abC = Cross(a, b, c);
        var abD = Cross(a, b, d);
        var cdA = Cross(c, d, a);
        var cdB = Cross(c, d, b);
        if (((abC > 0.001f && abD < -0.001f) || (abC < -0.001f && abD > 0.001f))
            && ((cdA > 0.001f && cdB < -0.001f) || (cdA < -0.001f && cdB > 0.001f)))
        {
            return true;
        }
        return MathF.Abs(abC) <= 0.001f && OnSegment(a, b, c)
            || MathF.Abs(abD) <= 0.001f && OnSegment(a, b, d)
            || MathF.Abs(cdA) <= 0.001f && OnSegment(c, d, a)
            || MathF.Abs(cdB) <= 0.001f && OnSegment(c, d, b);
    }

    private static bool OnSegment(Vec2 start, Vec2 end, Vec2 point) =>
        point.X >= MathF.Min(start.X, end.X) - 0.001f
        && point.X <= MathF.Max(start.X, end.X) + 0.001f
        && point.Y >= MathF.Min(start.Y, end.Y) - 0.001f
        && point.Y <= MathF.Max(start.Y, end.Y) + 0.001f;

    private static float Cross(Vec2 a, Vec2 b, Vec2 point) =>
        (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
}
