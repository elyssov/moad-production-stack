namespace Moad.Engine;

public enum RurkShotOutcome
{
    CriticalSuccess,
    Success,
    Partial,
    Failure,
    CriticalFailure,
}

public enum RurkHitZone
{
    None,
    Head,
    Chest,
    Belly,
    RightArm,
    LeftArm,
    Groin,
    RightLeg,
    LeftLeg,
}

public sealed record RurkShotContext
{
    public int Zpn { get; init; } = 71;
    public double DistanceMeters { get; init; }
    public int ShooterMovementSvd { get; init; }
    public int TargetMovementSvd { get; init; }
    public int TrackDistance { get; init; }
    public int CoverLevel { get; init; }
    public int ExtraSvd { get; init; }
    public int ArmourPenetration { get; init; } = 40;
    public int ArmourClass { get; init; }
    public int DamageDivisor { get; init; } = 13;
    public int DamageFlat { get; init; } = 3;
}

public sealed record RurkShotMath(
    int Zpn,
    double DistanceMeters,
    double FreeRangeMeters,
    int DistanceSvd,
    int ShooterMovementSvd,
    int TargetMovementSvd,
    int TrackDistance,
    int TrackSvd,
    int CoverLevel,
    int CoverSvd,
    int ExtraSvd,
    int TotalSvd,
    int Target)
{
    public int RangeSvd => DistanceSvd;
    public int MovementSvd => ShooterMovementSvd;
    public int CrossTrackSvd => TrackSvd;
}

public sealed record RurkShotResult(
    string Weapon,
    RurkShotMath Math,
    int HitRoll,
    RurkShotOutcome Outcome,
    bool AimHit,
    bool Hit,
    RurkHitZone Zone,
    int ZoneRoll,
    int PenetrationRoll,
    int ArmourPenetration,
    int ArmourClass,
    bool Penetrated,
    int DamageRoll,
    int Damage)
{
    public int Target => Math.Target;
    public int TotalSvd => Math.TotalSvd;
}

public sealed record RurkAimResult(
    RurkShotMath Math,
    int HitRoll,
    RurkShotOutcome Outcome,
    bool Hit,
    RurkHitZone Zone,
    int ZoneRoll);

public sealed class RurkCombatResolver
{
    public const string LugerWeaponName = "Guardian Luger P08";
    public const double FreeHandgunRangeMeters = 5.0;
    public const int DistanceSvdPerMeter = 1;
    public const int CoverSvdPerLevel = 5;
    public const ulong DefaultSeed = 1994;

    private static readonly int[] TrackSvdByDistance = [0, 10, 20];
    private readonly object _randomLock = new();
    private ulong _randomState;

    public RurkCombatResolver(ulong seed = DefaultSeed)
    {
        _randomState = seed;
    }

    public RurkShotMath CalculateLugerTarget(RurkShotContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var distanceMeters = Math.Max(0.0, context.DistanceMeters);
        var distanceSvd = Math.Max(
            0,
            (int)Math.Ceiling(distanceMeters - FreeHandgunRangeMeters)) * DistanceSvdPerMeter;
        var shooterMovementSvd = Math.Max(0, context.ShooterMovementSvd);
        var targetMovementSvd = Math.Max(0, context.TargetMovementSvd);
        var trackDistance = Math.Clamp(Math.Abs((long)context.TrackDistance), 0L, 2L);
        var trackSvd = TrackSvdByDistance[(int)trackDistance];
        var coverLevel = Math.Clamp(context.CoverLevel, 0, 3);
        var coverSvd = coverLevel * CoverSvdPerLevel;
        var totalSvd = distanceSvd
            + shooterMovementSvd
            + targetMovementSvd
            + trackSvd
            + coverSvd
            + context.ExtraSvd;

        return new RurkShotMath(
            context.Zpn,
            distanceMeters,
            FreeHandgunRangeMeters,
            distanceSvd,
            shooterMovementSvd,
            targetMovementSvd,
            (int)trackDistance,
            trackSvd,
            coverLevel,
            coverSvd,
            context.ExtraSvd,
            totalSvd,
            context.Zpn - totalSvd);
    }

    public RurkShotResult ResolveLugerShot(
        RurkShotContext context,
        IEnumerable<int>? forcedRolls = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rolls = forcedRolls is null ? null : new Queue<int>(forcedRolls);
        var aim = ResolveLugerAim(context, rolls);
        return ResolveLugerImpact(context, aim, rolls);
    }

    public RurkAimResult ResolveLugerAim(
        RurkShotContext context,
        IEnumerable<int>? forcedRolls = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ResolveLugerAim(context, forcedRolls is null ? null : new Queue<int>(forcedRolls));
    }

    public RurkShotResult ResolveLugerImpact(
        RurkShotContext context,
        RurkAimResult aim,
        IEnumerable<int>? forcedRolls = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(aim);
        return ResolveLugerImpact(context, aim, forcedRolls is null ? null : new Queue<int>(forcedRolls));
    }

    public RurkShotResult CreateBlockedLugerResult(RurkShotContext context, RurkAimResult aim)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(aim);
        return new RurkShotResult(
            LugerWeaponName,
            aim.Math,
            aim.HitRoll,
            aim.Outcome,
            aim.Hit,
            false,
            aim.Zone,
            aim.ZoneRoll,
            0,
            context.ArmourPenetration,
            context.ArmourClass,
            false,
            0,
            0);
    }

    private RurkAimResult ResolveLugerAim(RurkShotContext context, Queue<int>? rolls)
    {
        var shotMath = CalculateLugerTarget(context);
        var hitRoll = NextRoll(rolls);
        var outcome = OutcomeFor(hitRoll, shotMath.Target);
        var hit = outcome is RurkShotOutcome.CriticalSuccess
            or RurkShotOutcome.Success
            or RurkShotOutcome.Partial;

        if (!hit)
        {
            return new RurkAimResult(shotMath, hitRoll, outcome, false, RurkHitZone.None, 0);
        }

        var zoneRoll = NextRoll(rolls);
        var zone = ZoneFor(zoneRoll);
        return new RurkAimResult(shotMath, hitRoll, outcome, true, zone, zoneRoll);
    }

    private RurkShotResult ResolveLugerImpact(RurkShotContext context, RurkAimResult aim, Queue<int>? rolls)
    {
        if (!aim.Hit)
        {
            return new RurkShotResult(
                LugerWeaponName,
                aim.Math,
                aim.HitRoll,
                aim.Outcome,
                false,
                false,
                RurkHitZone.None,
                0,
                0,
                context.ArmourPenetration,
                context.ArmourClass,
                false,
                0,
                0);
        }

        var penetrationRoll = NextRoll(rolls);
        var penetrated = penetrationRoll + context.ArmourPenetration >= context.ArmourClass + 50;

        if (!penetrated)
        {
            return new RurkShotResult(
                LugerWeaponName,
                aim.Math,
                aim.HitRoll,
                aim.Outcome,
                true,
                true,
                aim.Zone,
                aim.ZoneRoll,
                penetrationRoll,
                context.ArmourPenetration,
                context.ArmourClass,
                false,
                0,
                0);
        }

        var damageRoll = NextRoll(rolls);
        var damageDivisor = Math.Max(1, context.DamageDivisor);
        var damage = Math.Max(1, damageRoll / damageDivisor + context.DamageFlat);
        if (aim.Outcome == RurkShotOutcome.Partial)
        {
            damage = Math.Max(1, (int)Math.Ceiling(damage * 0.5));
        }

        return new RurkShotResult(
            LugerWeaponName,
            aim.Math,
            aim.HitRoll,
            aim.Outcome,
            true,
            true,
            aim.Zone,
            aim.ZoneRoll,
            penetrationRoll,
            context.ArmourPenetration,
            context.ArmourClass,
            true,
            damageRoll,
            damage);
    }

    public static RurkShotOutcome OutcomeFor(int roll, int target)
    {
        var clampedRoll = Math.Clamp(roll, 1, 100);
        if (target < 1)
        {
            return clampedRoll >= 95
                ? RurkShotOutcome.CriticalFailure
                : RurkShotOutcome.Failure;
        }

        if (clampedRoll <= 5)
        {
            return RurkShotOutcome.CriticalSuccess;
        }

        if (clampedRoll >= 95)
        {
            return RurkShotOutcome.CriticalFailure;
        }

        if (clampedRoll == target)
        {
            return RurkShotOutcome.Partial;
        }

        return clampedRoll < target ? RurkShotOutcome.Success : RurkShotOutcome.Failure;
    }

    public static RurkHitZone ZoneFor(int roll) => Math.Clamp(roll, 1, 100) switch
    {
        <= 10 => RurkHitZone.Head,
        <= 30 => RurkHitZone.Chest,
        <= 50 => RurkHitZone.Belly,
        <= 60 => RurkHitZone.RightArm,
        <= 70 => RurkHitZone.LeftArm,
        <= 80 => RurkHitZone.Groin,
        <= 90 => RurkHitZone.RightLeg,
        _ => RurkHitZone.LeftLeg,
    };

    public static string OutcomeName(RurkShotOutcome outcome) => outcome switch
    {
        RurkShotOutcome.CriticalSuccess => "critical_success",
        RurkShotOutcome.Success => "success",
        RurkShotOutcome.Partial => "partial",
        RurkShotOutcome.Failure => "failure",
        RurkShotOutcome.CriticalFailure => "critical_failure",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };

    public static string ZoneName(RurkHitZone zone) => zone switch
    {
        RurkHitZone.None => "none",
        RurkHitZone.Head => "head",
        RurkHitZone.Chest => "chest",
        RurkHitZone.Belly => "belly",
        RurkHitZone.RightArm => "right_arm",
        RurkHitZone.LeftArm => "left_arm",
        RurkHitZone.Groin => "groin",
        RurkHitZone.RightLeg => "right_leg",
        RurkHitZone.LeftLeg => "left_leg",
        _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, null),
    };

    private int NextRoll(Queue<int>? forcedRolls)
    {
        if (forcedRolls is { Count: > 0 })
        {
            return Math.Clamp(forcedRolls.Dequeue(), 1, 100);
        }

        lock (_randomLock)
        {
            // SplitMix64 gives a small, stable generator whose sequence does not depend on .NET versions.
            _randomState += 0x9E3779B97F4A7C15UL;
            var value = _randomState;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return (int)(value % 100UL) + 1;
        }
    }
}
