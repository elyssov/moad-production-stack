namespace Moad.Engine;

public sealed record LineOfFireHit(
    string ObstacleId,
    string Material,
    Vec2 Point,
    float Depth,
    float SegmentProgress);

public sealed class CombatGeometry
{
    private const float LaneHalfThickness = 0.18f;
    private readonly WorldDefinition world;

    public CombatGeometry(WorldDefinition world)
    {
        this.world = world;
    }

    public int CoverLevel(
        Vec2 origin,
        float originDepth,
        Vec2 targetPoint,
        Vec2 targetFoot,
        int targetLane)
    {
        var blocker = Trace(
            origin,
            originDepth,
            targetPoint,
            targetLane,
            AttackCollisionKind.InstantBallistic);
        if (blocker is null)
        {
            return 0;
        }
        return world.CoverZones
            .Where(zone => zone.Lane == targetLane
                && zone.Bounds.Contains(targetFoot)
                && zone.ObstacleId == blocker.ObstacleId)
            .Select(zone => zone.CoverLevel)
            .DefaultIfEmpty(0)
            .Max();
    }

    public LineOfFireHit? Trace(
        Vec2 origin,
        float originDepth,
        Vec2 target,
        float targetDepth,
        AttackCollisionKind attackKind)
    {
        LineOfFireHit? nearest = null;
        foreach (var obstacle in world.CombatObstacles.Where(item => item.Blocks.Contains(attackKind)))
        {
            var minDepth = MathF.Min(obstacle.FrontLane, obstacle.BackLane) - LaneHalfThickness;
            var maxDepth = MathF.Max(obstacle.FrontLane, obstacle.BackLane) + LaneHalfThickness;
            if (!TryClipAxisAlignedBox(
                    origin,
                    originDepth,
                    target,
                    targetDepth,
                    obstacle.Bounds,
                    minDepth,
                    maxDepth,
                    out var progress))
            {
                continue;
            }
            if (nearest is not null && nearest.SegmentProgress <= progress)
            {
                continue;
            }
            nearest = new LineOfFireHit(
                obstacle.Id,
                obstacle.Material,
                Vec2.Lerp(origin, target, progress),
                originDepth + (targetDepth - originDepth) * progress,
                progress);
        }
        return nearest;
    }

    private static bool TryClipAxisAlignedBox(
        Vec2 origin,
        float originDepth,
        Vec2 target,
        float targetDepth,
        Rect2 bounds,
        float minDepth,
        float maxDepth,
        out float entry)
    {
        var minimum = 0f;
        var maximum = 1f;
        if (!ClipAxis(origin.X, target.X - origin.X, bounds.Left, bounds.Right, ref minimum, ref maximum)
            || !ClipAxis(origin.Y, target.Y - origin.Y, bounds.Top, bounds.Bottom, ref minimum, ref maximum)
            || !ClipAxis(originDepth, targetDepth - originDepth, minDepth, maxDepth, ref minimum, ref maximum)
            || maximum < 0.001f || minimum > 0.999f)
        {
            entry = 0f;
            return false;
        }
        entry = Math.Clamp(minimum, 0f, 1f);
        return true;
    }

    private static bool ClipAxis(float origin, float delta, float minimum, float maximum, ref float entry, ref float exit)
    {
        if (MathF.Abs(delta) < 0.0001f)
        {
            return origin >= minimum && origin <= maximum;
        }
        var first = (minimum - origin) / delta;
        var second = (maximum - origin) / delta;
        if (first > second)
        {
            (first, second) = (second, first);
        }
        entry = MathF.Max(entry, first);
        exit = MathF.Min(exit, second);
        return entry <= exit;
    }
}
