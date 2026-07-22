using System.Text.Json;

namespace Moad.Engine;

public readonly record struct Vec2(float X, float Y)
{
    public static Vec2 operator +(Vec2 value, Vec2 other) => new(value.X + other.X, value.Y + other.Y);
    public static Vec2 operator -(Vec2 value, Vec2 other) => new(value.X - other.X, value.Y - other.Y);

    public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t);

    public float DistanceSquared(Vec2 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return dx * dx + dy * dy;
    }
}

public readonly record struct Rect2(float X, float Y, float Width, float Height)
{
    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public bool Contains(Vec2 point) =>
        point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
}

public sealed record DepthTrack(string Id, int Lane, float Scale, int ZIndex);

public sealed class SupportSurface
{
    public required string Id { get; init; }
    public required int Lane { get; init; }
    public required int ElevationRank { get; init; }
    public required string TraversalMode { get; init; }
    public required float SpeedMultiplier { get; init; }
    public required List<Vec2> Points { get; init; }
}

public sealed class DepthTransition
{
    public required string Id { get; init; }
    public required int FromLane { get; init; }
    public required int ToLane { get; init; }
    public required float Duration { get; init; }
    public required float LaneHandoff { get; init; }
    public required List<Vec2> TriggerPolygon { get; init; }
    public required List<Vec2> Path { get; init; }
}

public sealed record EnemyDefinition(
    string Id,
    string DisplayName,
    int Lane,
    Vec2 Position,
    float PatrolLeft,
    float PatrolRight,
    int CoverLevel,
    string MotionClass,
    float PatrolSpeed,
    int MaxHp,
    int ArmourClass,
    EnemyRangedAttackDefinition? RangedAttack = null,
    float VisualHeightMeters = 1.75f);

public sealed record EnemyRangedAttackDefinition(
    string RulesProfile,
    EnemyAttackKind Kind,
    int Damage,
    float RangeMeters,
    float CooldownSeconds,
    float InitialDelaySeconds,
    float ProjectileSpeed);

public enum EnemyAttackKind
{
    None,
    ThrownStone,
    Fireball,
}

public sealed record ObjectiveDefinition(string Id, int Lane, Vec2 Position, Vec2 PickupHalfExtents);

public enum AttackCollisionKind
{
    InstantBallistic,
    TravellingMagic,
    ThrownPhysical,
}

public sealed record CombatObstacle(
    string Id,
    Rect2 Bounds,
    int FrontLane,
    int BackLane,
    IReadOnlySet<AttackCollisionKind> Blocks,
    string Material,
    string VisibleArtContract);

public sealed record AuthoredObstacle(
    string Id,
    int Lane,
    IReadOnlyList<Vec2> Polygon,
    float HeightMinMeters,
    float HeightMaxMeters,
    bool BlocksMovement,
    string Material);

public sealed record AuthoredOccluder(
    string Id,
    int Lane,
    IReadOnlyList<Vec2> Polygon,
    float HeightMinMeters,
    float HeightMaxMeters,
    float Opacity);

public sealed record CoverZone(
    string Id,
    Rect2 Bounds,
    int Lane,
    int CoverLevel,
    string ObstacleId,
    string VisibleArtContract);

public sealed class WorldDefinition
{
    public required string Id { get; init; }
    public required Vec2 Size { get; init; }
    public required float PixelsPerMeter { get; init; }
    public required Vec2 Spawn { get; init; }
    public required int SpawnLane { get; init; }
    public required List<DepthTrack> Tracks { get; init; }
    public required List<SupportSurface> Supports { get; init; }
    public required List<DepthTransition> Transitions { get; init; }
    public required List<EnemyDefinition> Enemies { get; init; }
    public required List<CombatObstacle> CombatObstacles { get; init; }
    public List<AuthoredObstacle> AuthoredObstacles { get; init; } = [];
    public List<AuthoredOccluder> AuthoredOccluders { get; init; } = [];
    public required List<CoverZone> CoverZones { get; init; }
    public required ObjectiveDefinition Objective { get; init; }

    public float ScaleFor(int lane) => Tracks.First(track => track.Lane == lane).Scale;
}

public static class WorldLoader
{
    public static WorldDefinition Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var pixelsPerMeter = root.GetProperty("world_metrics").GetProperty("pixels_per_meter").GetSingle();
        if (pixelsPerMeter <= 0f)
        {
            throw new InvalidDataException("world_metrics.pixels_per_meter must be positive");
        }
        var laneIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var tracks = new List<DepthTrack>();
        foreach (var item in root.GetProperty("lanes").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString() ?? throw new InvalidDataException("Lane has no id");
            var lane = item.GetProperty("lane").GetInt32();
            laneIds.Add(id, lane);
            tracks.Add(new DepthTrack(
                id,
                lane,
                item.GetProperty("scale").GetSingle(),
                item.GetProperty("z_index").GetInt32()));
        }

        var offset = ReadVec(root.GetProperty("world_metrics").GetProperty("route_to_art_offset"));
        var spawn = root.GetProperty("spawn");
        var supports = new List<SupportSurface>();
        foreach (var item in root.GetProperty("collision_surfaces").EnumerateArray())
        {
            supports.Add(new SupportSurface
            {
                Id = item.GetProperty("id").GetString() ?? throw new InvalidDataException("Support has no id"),
                Lane = laneIds[item.GetProperty("lane").GetString()!],
                ElevationRank = item.GetProperty("elevation_rank").GetInt32(),
                TraversalMode = item.TryGetProperty("traversal_mode", out var traversalMode)
                    ? traversalMode.GetString() ?? "stable"
                    : "stable",
                SpeedMultiplier = item.TryGetProperty("speed_multiplier", out var speedMultiplier)
                    ? speedMultiplier.GetSingle()
                    : 1f,
                Points = item.GetProperty("points").EnumerateArray().Select(point => ReadVec(point) + offset).ToList(),
            });
        }

        var transitions = new List<DepthTransition>();
        foreach (var item in root.GetProperty("transitions").EnumerateArray())
        {
            transitions.Add(new DepthTransition
            {
                Id = item.GetProperty("id").GetString() ?? throw new InvalidDataException("Transition has no id"),
                FromLane = laneIds[item.GetProperty("from_lane").GetString()!],
                ToLane = laneIds[item.GetProperty("to_lane").GetString()!],
                Duration = item.GetProperty("duration").GetSingle(),
                LaneHandoff = item.GetProperty("lane_handoff_t").GetSingle(),
                TriggerPolygon = item.GetProperty("trigger_polygon").EnumerateArray().Select(point => ReadVec(point) + offset).ToList(),
                Path = item.GetProperty("path_points").EnumerateArray().Select(point => ReadVec(point) + offset).ToList(),
            });
        }

        var enemies = new List<EnemyDefinition>();
        foreach (var item in root.GetProperty("enemy_spawns").EnumerateArray())
        {
            var id = item.GetProperty("node").GetString() ?? throw new InvalidDataException("Enemy has no node id");
            enemies.Add(new EnemyDefinition(
                id,
                item.GetProperty("display_name").GetString() ?? id,
                laneIds[item.GetProperty("lane").GetString()!],
                ReadVec(item.GetProperty("position")) + offset,
                item.GetProperty("patrol_left").GetSingle() + offset.X,
                item.GetProperty("patrol_right").GetSingle() + offset.X,
                item.GetProperty("cover_level").GetInt32(),
                item.GetProperty("motion_class").GetString() ?? "walk",
                item.GetProperty("patrol_speed").GetSingle(),
                item.GetProperty("max_hp").GetInt32(),
                item.GetProperty("armour_class").GetInt32(),
                ReadRangedAttack(item),
                ReadVisualHeight(item)));
        }

        var combatObstacles = new List<CombatObstacle>();
        if (root.TryGetProperty("combat_obstacles", out var obstacleItems))
        {
            foreach (var item in obstacleItems.EnumerateArray())
            {
                var depthSpan = item.GetProperty("depth_span").EnumerateArray().ToArray();
                if (depthSpan.Length != 2)
                {
                    throw new InvalidDataException("Combat obstacle depth_span must contain two lanes");
                }
                var bounds = ReadRect(item.GetProperty("rect"), offset);
                var blocks = item.GetProperty("blocks").EnumerateArray()
                    .Select(ReadAttackCollisionKind)
                    .ToHashSet();
                if (bounds.Width <= 0f || bounds.Height <= 0f || blocks.Count == 0)
                {
                    throw new InvalidDataException("Combat obstacle must have positive geometry and at least one blocked attack family");
                }
                var id = item.GetProperty("id").GetString() ?? throw new InvalidDataException("Combat obstacle has no id");
                if (combatObstacles.Any(obstacle => obstacle.Id == id))
                {
                    throw new InvalidDataException($"Duplicate combat obstacle '{id}'");
                }
                combatObstacles.Add(new CombatObstacle(
                    id,
                    bounds,
                    laneIds[depthSpan[0].GetString()!],
                    laneIds[depthSpan[1].GetString()!],
                    blocks,
                    item.GetProperty("material").GetString() ?? "stone",
                    item.GetProperty("visible_art_contract").GetString()
                        ?? throw new InvalidDataException("Combat obstacle has no visible-art contract")));
            }
        }

        var authoredObstacles = new List<AuthoredObstacle>();
        if (root.TryGetProperty("editor_obstacles", out var authoredObstacleItems))
        {
            foreach (var item in authoredObstacleItems.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString()
                    ?? throw new InvalidDataException("Authored obstacle has no id");
                if (combatObstacles.Any(obstacle => obstacle.Id == id))
                {
                    throw new InvalidDataException($"Duplicate combat obstacle '{id}'");
                }

                var lane = laneIds[item.GetProperty("lane").GetString()!];
                var points = item.GetProperty("polygon").EnumerateArray()
                    .Select(point => ReadVec(point) + offset)
                    .ToList();
                if (points.Count < 3)
                {
                    throw new InvalidDataException($"Authored obstacle '{id}' needs at least three points");
                }

                var heightMin = item.TryGetProperty("height_min_m", out var heightMinValue) ? heightMinValue.GetSingle() : 0f;
                var heightMax = item.TryGetProperty("height_max_m", out var heightMaxValue) ? heightMaxValue.GetSingle() : heightMin;
                if (heightMin > heightMax)
                {
                    throw new InvalidDataException($"Authored obstacle '{id}' has an inverted height range");
                }
                var obstacleMaterial = item.TryGetProperty("material", out var material)
                    ? material.GetString() ?? "stone"
                    : "stone";
                authoredObstacles.Add(new AuthoredObstacle(
                    id,
                    lane,
                    points,
                    heightMin,
                    heightMax,
                    ReadOptionalBoolean(item, "blocks_movement"),
                    obstacleMaterial));

                var blocks = new HashSet<AttackCollisionKind>();
                if (ReadOptionalBoolean(item, "blocks_ballistics")) blocks.Add(AttackCollisionKind.InstantBallistic);
                if (ReadOptionalBoolean(item, "blocks_magic")) blocks.Add(AttackCollisionKind.TravellingMagic);
                if (ReadOptionalBoolean(item, "blocks_thrown")) blocks.Add(AttackCollisionKind.ThrownPhysical);
                if (blocks.Count == 0)
                {
                    continue;
                }

                combatObstacles.Add(new CombatObstacle(
                    id,
                    BoundsOf(points),
                    lane,
                    lane,
                    blocks,
                    obstacleMaterial,
                    $"MoAD Level Editor polygon assigned to depth lane {lane}"));
            }
        }

        var authoredOccluders = new List<AuthoredOccluder>();
        if (root.TryGetProperty("editor_occluders", out var authoredOccluderItems))
        {
            foreach (var item in authoredOccluderItems.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString()
                    ?? throw new InvalidDataException("Authored occluder has no id");
                var lane = laneIds[item.GetProperty("lane").GetString()!];
                var points = item.GetProperty("polygon").EnumerateArray()
                    .Select(point => ReadVec(point) + offset)
                    .ToList();
                if (points.Count < 3)
                {
                    throw new InvalidDataException($"Authored occluder '{id}' needs at least three points");
                }
                var heightMin = item.TryGetProperty("height_min_m", out var heightMinValue) ? heightMinValue.GetSingle() : 0f;
                var heightMax = item.TryGetProperty("height_max_m", out var heightMaxValue) ? heightMaxValue.GetSingle() : heightMin;
                if (heightMin > heightMax)
                {
                    throw new InvalidDataException($"Authored occluder '{id}' has an inverted height range");
                }
                var opacity = item.TryGetProperty("opacity", out var opacityValue) ? opacityValue.GetSingle() : 1f;
                authoredOccluders.Add(new AuthoredOccluder(
                    id,
                    lane,
                    points,
                    heightMin,
                    heightMax,
                    Math.Clamp(opacity, 0f, 1f)));
            }
        }

        var coverZones = new List<CoverZone>();
        if (root.TryGetProperty("cover_zones", out var coverItems))
        {
            foreach (var item in coverItems.EnumerateArray())
            {
                var level = item.GetProperty("cover_level").GetInt32();
                if (level is < 1 or > 3)
                {
                    throw new InvalidDataException("Cover level must be between one and three");
                }
                var obstacleId = item.GetProperty("obstacle_id").GetString()
                    ?? throw new InvalidDataException("Cover zone has no obstacle id");
                var obstacle = combatObstacles.FirstOrDefault(candidate => candidate.Id == obstacleId);
                if (obstacle is null)
                {
                    throw new InvalidDataException($"Cover zone references unknown obstacle '{obstacleId}'");
                }
                var lane = laneIds[item.GetProperty("lane").GetString()!];
                if (lane < Math.Min(obstacle.FrontLane, obstacle.BackLane)
                    || lane > Math.Max(obstacle.FrontLane, obstacle.BackLane))
                {
                    throw new InvalidDataException($"Cover zone lane is outside obstacle '{obstacleId}' depth span");
                }
                var bounds = ReadRect(item.GetProperty("rect"), offset);
                if (bounds.Width <= 0f || bounds.Height <= 0f)
                {
                    throw new InvalidDataException("Cover zone must have positive geometry");
                }
                var id = item.GetProperty("id").GetString() ?? throw new InvalidDataException("Cover zone has no id");
                if (coverZones.Any(zone => zone.Id == id))
                {
                    throw new InvalidDataException($"Duplicate cover zone '{id}'");
                }
                coverZones.Add(new CoverZone(
                    id,
                    bounds,
                    lane,
                    level,
                    obstacleId,
                    item.GetProperty("visible_art_contract").GetString()
                        ?? throw new InvalidDataException("Cover zone has no visible-art contract")));
            }
        }

        var objectiveValue = root.GetProperty("objective");
        var objective = new ObjectiveDefinition(
            objectiveValue.GetProperty("id").GetString() ?? "objective",
            laneIds[objectiveValue.GetProperty("lane").GetString()!],
            ReadVec(objectiveValue.GetProperty("position")) + offset,
            ReadVec(objectiveValue.GetProperty("pickup_half_extents")));

        return new WorldDefinition
        {
            Id = root.GetProperty("id").GetString() ?? Path.GetFileNameWithoutExtension(path),
            Size = ReadVec(root.GetProperty("runtime_size")),
            PixelsPerMeter = pixelsPerMeter,
            Spawn = ReadVec(spawn.GetProperty("position")) + offset,
            SpawnLane = laneIds[spawn.GetProperty("lane").GetString()!],
            Tracks = tracks,
            Supports = supports,
            Transitions = transitions,
            Enemies = enemies,
            CombatObstacles = combatObstacles,
            AuthoredObstacles = authoredObstacles,
            AuthoredOccluders = authoredOccluders,
            CoverZones = coverZones,
            Objective = objective,
        };
    }

    private static Vec2 ReadVec(JsonElement value)
    {
        var values = value.EnumerateArray().ToArray();
        if (values.Length != 2)
        {
            throw new InvalidDataException("Expected a two-component coordinate");
        }
        return new Vec2(values[0].GetSingle(), values[1].GetSingle());
    }

    private static Rect2 ReadRect(JsonElement value, Vec2 offset)
    {
        var values = value.EnumerateArray().ToArray();
        if (values.Length != 4)
        {
            throw new InvalidDataException("Expected a four-component rectangle");
        }
        return new Rect2(
            values[0].GetSingle() + offset.X,
            values[1].GetSingle() + offset.Y,
            values[2].GetSingle(),
            values[3].GetSingle());
    }

    private static Rect2 BoundsOf(IReadOnlyList<Vec2> points)
    {
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var right = points.Max(point => point.X);
        var bottom = points.Max(point => point.Y);
        return new Rect2(left, top, right - left, bottom - top);
    }

    private static bool ReadOptionalBoolean(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static AttackCollisionKind ReadAttackCollisionKind(JsonElement value) => value.GetString() switch
    {
        "instant_ballistic" => AttackCollisionKind.InstantBallistic,
        "travelling_magic" => AttackCollisionKind.TravellingMagic,
        "thrown_physical" => AttackCollisionKind.ThrownPhysical,
        var kind => throw new InvalidDataException($"Unknown attack collision kind '{kind}'"),
    };

    private static EnemyRangedAttackDefinition? ReadRangedAttack(JsonElement item)
    {
        if (!item.TryGetProperty("ranged_attack", out var value))
        {
            return null;
        }
        var kind = value.GetProperty("kind").GetString() switch
        {
            "thrown_stone" => EnemyAttackKind.ThrownStone,
            "fireball" => EnemyAttackKind.Fireball,
            "none" or null => EnemyAttackKind.None,
            var unknownKind => throw new InvalidDataException($"Unknown enemy attack kind '{unknownKind}'"),
        };
        var attack = new EnemyRangedAttackDefinition(
            value.GetProperty("rules_profile").GetString()
                ?? throw new InvalidDataException("Enemy ranged attack has no rules profile"),
            kind,
            value.GetProperty("damage").GetInt32(),
            value.GetProperty("range_meters").GetSingle(),
            value.GetProperty("cooldown_seconds").GetSingle(),
            value.GetProperty("initial_delay_seconds").GetSingle(),
            value.GetProperty("projectile_speed").GetSingle());
        if (attack.Damage <= 0 || attack.RangeMeters <= 0f || attack.CooldownSeconds <= 0f
            || attack.InitialDelaySeconds < 0f || attack.ProjectileSpeed <= 0f)
        {
            throw new InvalidDataException($"Enemy ranged attack '{attack.RulesProfile}' has invalid numeric values");
        }
        return attack;
    }

    private static float ReadVisualHeight(JsonElement item)
    {
        var height = item.TryGetProperty("visual_height_meters", out var visualHeight)
            ? visualHeight.GetSingle()
            : 1.75f;
        if (height <= 0f)
        {
            throw new InvalidDataException("enemy.visual_height_meters must be positive");
        }
        return height;
    }
}
