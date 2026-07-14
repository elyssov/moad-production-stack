namespace Moad.Engine;

public readonly record struct SupportHit(string SurfaceId, float Y, int ElevationRank);

public sealed class SupportSolver(WorldDefinition world)
{
    public bool TrySample(SupportSurface surface, float x, out float y)
    {
        for (var index = 0; index < surface.Points.Count - 1; index++)
        {
            var a = surface.Points[index];
            var b = surface.Points[index + 1];
            var minX = MathF.Min(a.X, b.X);
            var maxX = MathF.Max(a.X, b.X);
            if (x < minX - 0.01f || x > maxX + 0.01f)
            {
                continue;
            }

            var span = b.X - a.X;
            var t = MathF.Abs(span) < 0.001f ? 0f : (x - a.X) / span;
            y = a.Y + (b.Y - a.Y) * Math.Clamp(t, 0f, 1f);
            return true;
        }

        y = 0f;
        return false;
    }

    public SupportHit? FindNear(int lane, float x, float footY, float tolerance)
    {
        SupportHit? best = null;
        var bestDistance = float.MaxValue;
        foreach (var support in world.Supports.Where(item => item.Lane == lane))
        {
            if (!TrySample(support, x, out var supportY))
            {
                continue;
            }
            var distance = MathF.Abs(supportY - footY);
            if (distance <= tolerance && distance < bestDistance)
            {
                best = new SupportHit(support.Id, supportY, support.ElevationRank);
                bestDistance = distance;
            }
        }
        return best;
    }

    public SupportHit? FindFirstBelow(int lane, float x, float previousY, float nextY)
    {
        SupportHit? first = null;
        foreach (var support in world.Supports.Where(item => item.Lane == lane))
        {
            if (!TrySample(support, x, out var supportY))
            {
                continue;
            }
            if (supportY < previousY - 0.01f || supportY > nextY + 0.01f)
            {
                continue;
            }
            if (first is null || supportY < first.Value.Y)
            {
                first = new SupportHit(support.Id, supportY, support.ElevationRank);
            }
        }
        return first;
    }
}
