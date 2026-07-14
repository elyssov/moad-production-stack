namespace Moad.Engine;

public readonly record struct InputFrame(
    float Horizontal,
    bool Run,
    bool JumpPressed,
    bool DepthDeeperPressed,
    bool DepthCloserPressed,
    bool ShootPressed = false,
    bool CycleTargetPressed = false,
    bool ReloadPressed = false,
    int DecisionChoiceNumber = 0,
    bool CrouchPressed = false);

public enum HeroState
{
    Move,
    Airborne,
    DepthTransition,
    DrawWeapon,
    Shoot,
    Reload,
    Crouch,
}

public enum WeaponState
{
    Holstered,
    Ready,
    Reloading,
    ClearingJam,
}

public sealed class HeroModel
{
    public const int MaximumHp = 9;
    public const float StandingHeightMeters = 1.75f;

    public Vec2 Position { get; internal set; }
    public Vec2 Velocity { get; internal set; }
    public int Lane { get; internal set; }
    public int Facing { get; internal set; } = 1;
    public bool Grounded { get; internal set; }
    public string? ActiveSupportId { get; internal set; }
    public HeroState State { get; internal set; }
    public float DisplayScale { get; internal set; } = 1f;
    public string TraversalMode { get; internal set; } = "stable";
    public bool IsCrouched { get; internal set; }
    public bool BalanceRequired => TraversalMode == "unstable_bridge";
    public WeaponState WeaponCondition { get; internal set; }
    public bool PistolReady => WeaponCondition != WeaponState.Holstered;
    public int AmmoInMagazine { get; internal set; } = 8;
    public int ReserveAmmo { get; internal set; } = 16;
    public float WeaponActionRemaining { get; internal set; }
    public float WeaponActionDuration { get; internal set; }
    public int ReloadPhase { get; internal set; } = -1;
    public float ReloadProgress => State == HeroState.Reload && WeaponActionDuration > 0f
        ? Math.Clamp(1f - WeaponActionRemaining / WeaponActionDuration, 0f, 1f)
        : 0f;
    public int Hp { get; internal set; } = MaximumHp;
    public bool IsAlive => Hp > 0;
    public float HitFlashRemaining { get; internal set; }
}

public sealed class EnemyModel
{
    internal EnemyModel(EnemyDefinition definition)
    {
        Definition = definition;
        Position = definition.Position;
        Hp = definition.MaxHp;
    }

    public EnemyDefinition Definition { get; }
    public Vec2 Position { get; internal set; }
    public int Hp { get; internal set; }
    public int Facing { get; internal set; } = 1;
    public bool IsAlive => Hp > 0;
    public EnemyPresentationState PresentationState { get; internal set; } = EnemyPresentationState.Patrol;
    public float PresentationRemaining { get; internal set; }
    public float PresentationDuration { get; internal set; }
    public float PresentationProgress => PresentationDuration <= 0f
        ? 1f
        : Math.Clamp(1f - PresentationRemaining / PresentationDuration, 0f, 1f);
    public int MovementSvd => PresentationState != EnemyPresentationState.Patrol ? 0 : Definition.MotionClass switch
    {
        "hobble" => 5,
        "run" => 20,
        _ => 10,
    };
}

public enum EnemyPresentationState
{
    Patrol,
    HitLight,
    HitHeavy,
    Dying,
    Corpse,
}

public sealed class EnemyProjectileModel
{
    internal EnemyProjectileModel(
        string sourceEnemyId,
        EnemyAttackKind kind,
        Vec2 origin,
        Vec2 target,
        int sourceLane,
        int targetLane,
        float fullDuration,
        int damage,
        LineOfFireHit? obstacleHit)
    {
        SourceEnemyId = sourceEnemyId;
        Kind = kind;
        Origin = origin;
        Target = target;
        SourceLane = sourceLane;
        TargetLane = targetLane;
        PathEndProgress = obstacleHit?.SegmentProgress ?? 1f;
        ImpactPoint = obstacleHit?.Point ?? target;
        ImpactDepth = obstacleHit?.Depth ?? targetLane;
        Duration = MathF.Max(0.18f, fullDuration * MathF.Max(0.05f, PathEndProgress));
        Damage = damage;
        BlockedByObstacleId = obstacleHit?.ObstacleId;
        ImpactMaterial = obstacleHit?.Material;
        Position = origin;
    }

    public string SourceEnemyId { get; }
    public EnemyAttackKind Kind { get; }
    public Vec2 Origin { get; }
    public Vec2 Target { get; }
    public int SourceLane { get; }
    public int TargetLane { get; }
    public Vec2 ImpactPoint { get; }
    public float ImpactDepth { get; }
    public int ImpactLane => (int)MathF.Round(ImpactDepth);
    public float PathEndProgress { get; }
    public float Duration { get; }
    public int Damage { get; }
    public string? BlockedByObstacleId { get; }
    public string? ImpactMaterial { get; }
    public float Elapsed { get; internal set; }
    public float Progress => Math.Clamp(Elapsed / Duration, 0f, 1f);
    public float PathProgress => Progress * PathEndProgress;
    public float CurrentDepth => SourceLane + (TargetLane - SourceLane) * PathProgress;
    public Vec2 Position { get; internal set; }

    public Vec2 PositionAtPathProgress(float pathProgress) => PositionAlong(Kind, Origin, Target, pathProgress);

    internal static Vec2 PositionAlong(EnemyAttackKind kind, Vec2 origin, Vec2 target, float pathProgress)
    {
        var progress = Math.Clamp(pathProgress, 0f, 1f);
        var position = Vec2.Lerp(origin, target, progress);
        return kind == EnemyAttackKind.ThrownStone
            ? new Vec2(position.X, position.Y - MathF.Sin(progress * MathF.PI) * 58f)
            : position;
    }
}

public enum FirearmImpactKind
{
    None,
    MummyDust,
    LivingTarget,
    StoneObstacle,
}

public enum WorldImpactKind
{
    StoneDust,
    FireBurst,
}

public sealed class WorldImpactEffectModel
{
    internal WorldImpactEffectModel(Vec2 position, int lane, WorldImpactKind kind)
    {
        Position = position;
        Lane = lane;
        Kind = kind;
    }

    public const float TotalDuration = 0.42f;
    public Vec2 Position { get; }
    public int Lane { get; }
    public WorldImpactKind Kind { get; }
    public float Elapsed { get; internal set; }
    public float Progress => Math.Clamp(Elapsed / TotalDuration, 0f, 1f);
}

public sealed class PlayerShotEffectModel
{
    internal PlayerShotEffectModel(Vec2 origin, Vec2 target, int originLane, int targetLane, FirearmImpactKind impactKind)
    {
        Origin = origin;
        Target = target;
        OriginLane = originLane;
        TargetLane = targetLane;
        ImpactKind = impactKind;
    }

    public const float TotalDuration = 0.18f;
    public Vec2 Origin { get; }
    public Vec2 Target { get; }
    public int OriginLane { get; }
    public int TargetLane { get; }
    public FirearmImpactKind ImpactKind { get; }
    public float Elapsed { get; internal set; }
    public float Progress => Math.Clamp(Elapsed / TotalDuration, 0f, 1f);
}

public sealed class WorldSimulation
{
    private const float WalkSpeed = 125f;
    private const float CrouchSpeed = 62.5f;
    private const float RunSpeed = 245f;
    private const float Gravity = 1250f;
    private const float JumpVelocity = -500f;
    private const float TerminalFall = 760f;
    private const float DrawDuration = 0.52f;
    private const float ShotDuration = 0.46f;
    private const float ReloadDuration = 3f;
    private const int MagazineSize = 8;
    private const float HeroHitFlashDuration = 0.35f;
    private const float ProjectileHitRadius = 52f;

    private readonly WorldDefinition world;
    private readonly SupportSolver supports;
    private readonly CombatGeometry combatGeometry;
    private readonly RurkCombatResolver combat;
    private readonly NarrativeContent? narrativeContent;
    private ActiveTransition? transition;
    private ActiveDirectedSequence? directedSequence;
    private float decisionSecondsRemaining;
    private readonly Dictionary<string, int> enemyDirections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> enemyAttackCooldowns = new(StringComparer.Ordinal);

    public WorldSimulation(WorldDefinition world, RurkCombatResolver? combat = null, NarrativeContent? narrativeContent = null)
    {
        this.world = world;
        supports = new SupportSolver(world);
        combatGeometry = new CombatGeometry(world);
        this.combat = combat ?? new RurkCombatResolver();
        this.narrativeContent = narrativeContent;
        Hero = new HeroModel();
        Narrative = new NarrativeState();
        Enemies = [];
        Reset(world.Spawn, world.SpawnLane);
    }

    public HeroModel Hero { get; }
    public NarrativeState Narrative { get; }
    public List<EnemyModel> Enemies { get; }
    public List<EnemyProjectileModel> EnemyProjectiles { get; } = [];
    public List<WorldImpactEffectModel> WorldImpactEffects { get; } = [];
    public PlayerShotEffectModel? ActivePlayerShotEffect { get; private set; }
    public string? SelectedTargetId { get; private set; }
    public EnemyModel? SelectedTarget => Enemies.FirstOrDefault(enemy => enemy.Definition.Id == SelectedTargetId && enemy.IsAlive);
    public RurkShotResult? LastShot { get; private set; }
    public string? LastShotTargetId { get; private set; }
    public string? LastShotBlockedByObstacleId { get; private set; }
    public string LastCombatMessage { get; private set; } = "The chamber is listening.";
    public bool ObjectiveCollected { get; private set; }
    public string? ActiveTransitionId => transition?.Definition.Id;
    public float TransitionProgress => transition?.Progress ?? 0f;
    public DecisionDefinition? ActiveDecision { get; private set; }
    public float? DecisionSecondsRemaining => ActiveDecision?.TimerSeconds is null ? null : decisionSecondsRemaining;
    public string? ActiveDirectedSequenceId => directedSequence?.Definition.Id;
    public bool IsWorldPaused => ActiveDecision is not null;
    public bool IsPlayerControlLocked => ActiveDecision is not null || directedSequence is not null;
    public string? LastNarrativeMessage { get; private set; }

    public int CoverLevelFor(EnemyModel enemy)
    {
        var shotFacing = enemy.Position.X >= Hero.Position.X ? 1f : -1f;
        var origin = new Vec2(Hero.Position.X + shotFacing * 30f, Hero.Position.Y - 39.2f * Hero.DisplayScale);
        return combatGeometry.CoverLevel(
            origin,
            Hero.Lane,
            TargetPointFor(enemy, RurkHitZone.Groin),
            enemy.Position,
            enemy.Definition.Lane);
    }

    public void Reset(Vec2 position, int lane)
    {
        transition = null;
        directedSequence = null;
        ActiveDecision = null;
        decisionSecondsRemaining = 0f;
        LastNarrativeMessage = null;
        Hero.Position = position;
        Hero.Velocity = new Vec2(0f, 0f);
        Hero.Lane = lane;
        Hero.DisplayScale = world.ScaleFor(lane);
        var hit = supports.FindNear(lane, position.X, position.Y, 12f);
        Hero.Grounded = hit is not null;
        Hero.ActiveSupportId = hit?.SurfaceId;
        if (hit is not null)
        {
            Hero.Position = new Vec2(position.X, hit.Value.Y);
        }
        Hero.State = Hero.Grounded ? HeroState.Move : HeroState.Airborne;
        Hero.TraversalMode = hit is null ? "air" : world.Supports.Single(surface => surface.Id == hit.Value.SurfaceId).TraversalMode;
        Hero.IsCrouched = false;
        Hero.WeaponCondition = WeaponState.Holstered;
        Hero.AmmoInMagazine = MagazineSize;
        Hero.ReserveAmmo = 16;
        Hero.WeaponActionRemaining = 0f;
        Hero.WeaponActionDuration = 0f;
        Hero.ReloadPhase = -1;
        Hero.Hp = HeroModel.MaximumHp;
        Hero.HitFlashRemaining = 0f;
        Enemies.Clear();
        EnemyProjectiles.Clear();
        WorldImpactEffects.Clear();
        ActivePlayerShotEffect = null;
        enemyDirections.Clear();
        enemyAttackCooldowns.Clear();
        foreach (var definition in world.Enemies)
        {
            Enemies.Add(new EnemyModel(definition));
            enemyDirections[definition.Id] = 1;
            enemyAttackCooldowns[definition.Id] = definition.RangedAttack?.InitialDelaySeconds ?? 0f;
        }
        SelectedTargetId = null;
        LastShot = null;
        LastShotTargetId = null;
        LastShotBlockedByObstacleId = null;
        LastCombatMessage = "The chamber is listening.";
        ObjectiveCollected = false;
    }

    public bool BeginTransition(string id)
    {
        var definition = world.Transitions.FirstOrDefault(item => item.Id == id);
        if (definition is null || definition.FromLane != Hero.Lane || definition.Path.Count < 2)
        {
            return false;
        }
        transition = new ActiveTransition(definition, Hero.Position, world.ScaleFor(Hero.Lane), world.ScaleFor(definition.ToLane));
        Hero.IsCrouched = false;
        Hero.State = HeroState.DepthTransition;
        Hero.Grounded = false;
        Hero.ActiveSupportId = null;
        var target = definition.Path[^1];
        if (MathF.Abs(target.X - Hero.Position.X) > 1f)
        {
            Hero.Facing = target.X > Hero.Position.X ? 1 : -1;
        }
        return true;
    }

    public void Update(InputFrame input, float delta)
    {
        delta = Math.Clamp(delta, 0f, 1f / 20f);
        if (ActiveDecision is not null)
        {
            UpdateDecision(input, delta);
            return;
        }
        if (directedSequence is not null)
        {
            UpdateDirectedSequence(delta);
            return;
        }
        var heroStartCenter = HeroCollisionCenter();
        var heroStartDepth = HeroCollisionDepth();
        UpdateEnemies(delta);
        UpdateHero(input, delta);
        UpdateEnemyProjectiles(delta, heroStartCenter, heroStartDepth, HeroCollisionCenter(), HeroCollisionDepth());
    }

    private void UpdateHero(InputFrame input, float delta)
    {
        UpdatePlayerShotEffect(delta);
        UpdateWorldImpactEffects(delta);
        Hero.HitFlashRemaining = MathF.Max(0f, Hero.HitFlashRemaining - delta);
        if (!Hero.IsAlive)
        {
            Hero.Velocity = new Vec2(0f, 0f);
            TryCollectObjective();
            return;
        }
        if (UpdateWeaponAction(delta))
        {
            TryCollectObjective();
            return;
        }
        if (input.CycleTargetPressed && Hero.PistolReady)
        {
            CycleTarget();
        }
        if (input.ReloadPressed && TryBeginReload())
        {
            return;
        }
        if (input.ShootPressed && HandleShootPressed())
        {
            return;
        }
        if (transition is not null)
        {
            UpdateTransition(delta);
            TryCollectObjective();
            return;
        }

        if (input.CrouchPressed && Hero.Grounded)
        {
            Hero.IsCrouched = !Hero.IsCrouched;
            Hero.Velocity = new Vec2(0f, 0f);
        }

        var horizontal = Math.Clamp(input.Horizontal, -1f, 1f);
        var running = input.Run && MathF.Abs(horizontal) > 0.01f;
        if (running)
        {
            Hero.IsCrouched = false;
            if (Hero.PistolReady)
            {
                Hero.WeaponCondition = WeaponState.Holstered;
                SelectedTargetId = null;
                LastCombatMessage = "Guardian Luger holstered for the sprint.";
            }
        }

        if (input.DepthDeeperPressed && TryBeginNearestTransition(1))
        {
            return;
        }
        if (input.DepthCloserPressed && TryBeginNearestTransition(-1))
        {
            return;
        }

        var baseSpeed = Hero.IsCrouched ? CrouchSpeed : running ? RunSpeed : WalkSpeed;
        var activeSurface = world.Supports.FirstOrDefault(surface => surface.Id == Hero.ActiveSupportId);
        var speed = baseSpeed * (activeSurface?.SpeedMultiplier ?? 1f);
        if (MathF.Abs(horizontal) > 0.01f)
        {
            Hero.Facing = horizontal > 0f ? 1 : -1;
        }
        Hero.Velocity = new Vec2(horizontal * speed, Hero.Velocity.Y);

        if (Hero.Grounded && input.JumpPressed)
        {
            Hero.IsCrouched = false;
            Hero.Grounded = false;
            Hero.ActiveSupportId = null;
            Hero.Velocity = new Vec2(Hero.Velocity.X, JumpVelocity);
        }

        var previous = Hero.Position;
        var next = new Vec2(
            Math.Clamp(previous.X + Hero.Velocity.X * delta, 0f, world.Size.X),
            previous.Y);

        if (Hero.Grounded)
        {
            var ground = supports.FindNear(Hero.Lane, next.X, previous.Y, 36f);
            if (ground is not null)
            {
                next = new Vec2(next.X, ground.Value.Y);
                Hero.ActiveSupportId = ground.Value.SurfaceId;
                Hero.TraversalMode = world.Supports.Single(surface => surface.Id == ground.Value.SurfaceId).TraversalMode;
                Hero.Velocity = new Vec2(Hero.Velocity.X, 0f);
            }
            else
            {
                Hero.Grounded = false;
                Hero.ActiveSupportId = null;
                Hero.TraversalMode = "air";
            }
        }

        if (!Hero.Grounded)
        {
            var velocityY = MathF.Min(Hero.Velocity.Y + Gravity * delta, TerminalFall);
            Hero.Velocity = new Vec2(Hero.Velocity.X, velocityY);
            next = new Vec2(next.X, previous.Y + velocityY * delta);
            if (velocityY >= 0f)
            {
                var landing = supports.FindFirstBelow(Hero.Lane, next.X, previous.Y, next.Y);
                if (landing is not null)
                {
                    next = new Vec2(next.X, landing.Value.Y);
                    Hero.Velocity = new Vec2(Hero.Velocity.X, 0f);
                    Hero.Grounded = true;
                    Hero.ActiveSupportId = landing.Value.SurfaceId;
                    Hero.TraversalMode = world.Supports.Single(surface => surface.Id == landing.Value.SurfaceId).TraversalMode;
                }
            }
        }

        Hero.Position = next;
        Hero.State = Hero.Grounded
            ? Hero.IsCrouched ? HeroState.Crouch : HeroState.Move
            : HeroState.Airborne;
        Hero.DisplayScale = world.ScaleFor(Hero.Lane);
        TryCollectObjective();
    }

    public bool OpenDecision(string id)
    {
        if (ActiveDecision is not null || directedSequence is not null || narrativeContent is null
            || !narrativeContent.Decisions.TryGetValue(id, out var decision)
            || Narrative.ResolvedDecisions.Contains(id))
        {
            return false;
        }
        ActiveDecision = decision;
        decisionSecondsRemaining = decision.TimerSeconds ?? 0f;
        Hero.Velocity = new Vec2(0f, 0f);
        LastNarrativeMessage = decision.Body;
        return true;
    }

    public bool ChooseDecision(int choiceIndex)
    {
        var decision = ActiveDecision;
        if (decision is null || choiceIndex < 0 || choiceIndex >= decision.Choices.Count)
        {
            return false;
        }
        var choice = decision.Choices[choiceIndex];
        Narrative.Apply(decision.Id, choice.Effect);
        ActiveDecision = null;
        decisionSecondsRemaining = 0f;
        LastNarrativeMessage = choice.Text;
        if (choice.Effect.DirectedSequenceId is not null)
        {
            BeginDirectedSequence(choice.Effect.DirectedSequenceId);
        }
        return true;
    }

    public void CycleTarget()
    {
        var living = Enemies.Where(enemy => enemy.IsAlive)
            .OrderBy(enemy => enemy.Position.X)
            .ThenBy(enemy => enemy.Definition.Id, StringComparer.Ordinal)
            .ToList();
        if (living.Count == 0)
        {
            SelectedTargetId = null;
            LastCombatMessage = "No living target remains in the papyrus chamber.";
            return;
        }
        var current = living.FindIndex(enemy => enemy.Definition.Id == SelectedTargetId);
        var target = living[(current + 1) % living.Count];
        SelectTarget(target);
    }

    private bool HandleShootPressed()
    {
        if (!Hero.PistolReady)
        {
            Hero.WeaponCondition = WeaponState.Ready;
            Hero.WeaponActionRemaining = DrawDuration;
            Hero.WeaponActionDuration = DrawDuration;
            Hero.State = HeroState.DrawWeapon;
            Hero.Velocity = new Vec2(0f, 0f);
            SelectNearestTarget();
            return true;
        }
        if (Hero.AmmoInMagazine <= 0)
        {
            LastCombatMessage = "The Guardian Luger clicks on an empty chamber.";
            return true;
        }
        var target = SelectedTarget;
        if (target is null)
        {
            SelectNearestTarget();
            target = SelectedTarget;
        }
        if (target is null)
        {
            LastCombatMessage = "The Guardian Luger finds no living target.";
            return true;
        }

        ResolveShotAtTarget(target);
        return true;
    }

    private void ResolveShotAtTarget(EnemyModel target)
    {
        Hero.AmmoInMagazine--;
        Hero.WeaponActionRemaining = ShotDuration;
        Hero.WeaponActionDuration = ShotDuration;
        Hero.State = HeroState.Shoot;
        Hero.Velocity = new Vec2(0f, 0f);
        Hero.Facing = target.Position.X >= Hero.Position.X ? 1 : -1;
        var origin = new Vec2(Hero.Position.X + Hero.Facing * 30f, Hero.Position.Y - 39.2f * Hero.DisplayScale);
        var aimPoint = TargetPointFor(target, RurkHitZone.Chest);
        var distanceMeters = MathF.Sqrt(origin.DistanceSquared(aimPoint)) / world.PixelsPerMeter;
        var shotContext = new RurkShotContext
        {
            DistanceMeters = distanceMeters,
            TargetMovementSvd = target.MovementSvd,
            TrackDistance = Math.Abs(target.Definition.Lane - Hero.Lane),
            CoverLevel = CoverLevelFor(target),
            ArmourClass = target.Definition.ArmourClass,
        };
        var aim = combat.ResolveLugerAim(shotContext);
        LastShotTargetId = target.Definition.Id;
        LastShotBlockedByObstacleId = null;
        var targetPoint = aim.Hit ? TargetPointFor(target, aim.Zone) : aimPoint;
        var obstacleHit = combatGeometry.Trace(
            origin,
            Hero.Lane,
            targetPoint,
            target.Definition.Lane,
            AttackCollisionKind.InstantBallistic);
        if (obstacleHit is not null)
        {
            LastShotBlockedByObstacleId = obstacleHit.ObstacleId;
            LastShot = combat.CreateBlockedLugerResult(shotContext, aim);
            targetPoint = obstacleHit.Point;
        }
        else
        {
            LastShot = combat.ResolveLugerImpact(shotContext, aim);
        }
        ActivePlayerShotEffect = new PlayerShotEffectModel(
            origin,
            targetPoint,
            Hero.Lane,
            obstacleHit is null ? target.Definition.Lane : (int)MathF.Round(obstacleHit.Depth),
            obstacleHit is not null
                ? FirearmImpactKind.StoneObstacle
                : LastShot.Hit
                ? target.Definition.Id == "Zombie1" ? FirearmImpactKind.MummyDust : FirearmImpactKind.LivingTarget
                : FirearmImpactKind.None);
        if (obstacleHit is null && LastShot.Damage > 0)
        {
            target.Hp = Math.Max(0, target.Hp - LastShot.Damage);
            SetEnemyPresentation(
                target,
                target.Hp == 0 ? EnemyPresentationState.Dying : EnemyPresentationState.HitLight,
                target.Hp == 0 ? 0.75f : 0.28f);
        }
        LastCombatMessage = obstacleHit is null
            ? DescribeShot(target, LastShot)
            : $"LINE OF FIRE BLOCKED / Luger strikes {obstacleHit.Material}.";
        if (!target.IsAlive)
        {
            SelectNearestTarget();
        }
    }

    private bool TryBeginReload()
    {
        if (Hero.AmmoInMagazine >= MagazineSize || Hero.ReserveAmmo <= 0)
        {
            return false;
        }
        BeginReload(ReloadDuration, false);
        return true;
    }

    private void BeginReload(float duration, bool clearingJam)
    {
        Hero.WeaponActionRemaining = duration;
        Hero.WeaponActionDuration = duration;
        Hero.ReloadPhase = 0;
        Hero.State = HeroState.Reload;
        Hero.WeaponCondition = clearingJam ? WeaponState.ClearingJam : WeaponState.Reloading;
        Hero.Velocity = new Vec2(0f, 0f);
        LastCombatMessage = clearingJam ? "WEAPON JAMMED / CLEARING STOPPAGE" : "Reloading: magazine out.";
    }

    private bool UpdateWeaponAction(float delta)
    {
        if (Hero.WeaponActionRemaining <= 0f)
        {
            return false;
        }
        Hero.WeaponActionRemaining = MathF.Max(0f, Hero.WeaponActionRemaining - delta);
        if (Hero.State == HeroState.Reload)
        {
            var phaseDuration = Hero.WeaponActionDuration / 3f;
            var elapsed = Hero.WeaponActionDuration - Hero.WeaponActionRemaining;
            Hero.ReloadPhase = Math.Min(2, (int)MathF.Floor(elapsed / phaseDuration));
            LastCombatMessage = Hero.WeaponCondition == WeaponState.ClearingJam
                ? $"WEAPON JAMMED / CLEARING {Hero.ReloadProgress:P0}"
                : Hero.ReloadPhase switch
                {
                    0 => "Reloading: magazine out.",
                    1 => "Reloading: fresh magazine.",
                    _ => "Reloading: magazine seated.",
                };
        }
        if (Hero.WeaponActionRemaining > 0f)
        {
            return true;
        }
        if (Hero.State == HeroState.Reload)
        {
            var loaded = Math.Min(MagazineSize - Hero.AmmoInMagazine, Hero.ReserveAmmo);
            Hero.AmmoInMagazine += loaded;
            Hero.ReserveAmmo -= loaded;
            Hero.ReloadPhase = -1;
            Hero.WeaponCondition = WeaponState.Ready;
            LastCombatMessage = "Guardian Luger ready.";
        }
        else if (Hero.State == HeroState.Shoot && LastShot?.Outcome == RurkShotOutcome.CriticalFailure)
        {
            BeginReload(ReloadDuration * 2f, true);
            return true;
        }
        else if (Hero.State == HeroState.Shoot && Hero.AmmoInMagazine == 0 && Hero.ReserveAmmo > 0)
        {
            BeginReload(ReloadDuration, false);
            return true;
        }
        Hero.State = Hero.Grounded
            ? Hero.IsCrouched ? HeroState.Crouch : HeroState.Move
            : HeroState.Airborne;
        return false;
    }

    private void SelectNearestTarget()
    {
        var target = Enemies.Where(enemy => enemy.IsAlive)
            .OrderBy(enemy => enemy.Position.DistanceSquared(Hero.Position))
            .FirstOrDefault();
        if (target is null)
        {
            SelectedTargetId = null;
            return;
        }
        SelectTarget(target);
    }

    private void SelectTarget(EnemyModel target)
    {
        SelectedTargetId = target.Definition.Id;
        Hero.Facing = target.Position.X >= Hero.Position.X ? 1 : -1;
        var trackDistance = Math.Abs(target.Definition.Lane - Hero.Lane);
        LastCombatMessage = trackDistance == 0
            ? $"TARGET: {target.Definition.DisplayName} / SAME TRACK"
            : $"TARGET: {target.Definition.DisplayName} / TRACK DISTANCE {trackDistance} / SVD +{trackDistance * 10}";
    }

    private void UpdateEnemies(float delta)
    {
        UpdateEnemyPresentation(delta);
        foreach (var enemy in Enemies)
        {
            if (!enemy.IsAlive || enemy.PresentationState != EnemyPresentationState.Patrol)
            {
                continue;
            }
            var direction = enemyDirections[enemy.Definition.Id];
            var x = enemy.Position.X + direction * enemy.Definition.PatrolSpeed * delta;
            if (x <= enemy.Definition.PatrolLeft)
            {
                x = enemy.Definition.PatrolLeft;
                direction = 1;
            }
            else if (x >= enemy.Definition.PatrolRight)
            {
                x = enemy.Definition.PatrolRight;
                direction = -1;
            }
            enemy.Position = new Vec2(x, enemy.Position.Y);
            enemy.Facing = direction;
            enemyDirections[enemy.Definition.Id] = direction;

            UpdateEnemyAttack(enemy, delta);
        }
    }

    private void UpdateEnemyPresentation(float delta)
    {
        foreach (var enemy in Enemies)
        {
            if (enemy.PresentationRemaining <= 0f)
            {
                continue;
            }
            enemy.PresentationRemaining = MathF.Max(0f, enemy.PresentationRemaining - delta);
            if (enemy.PresentationRemaining <= 0f)
            {
                enemy.PresentationState = enemy.PresentationState == EnemyPresentationState.Dying
                    ? EnemyPresentationState.Corpse
                    : EnemyPresentationState.Patrol;
            }
        }
    }

    private void UpdateEnemyAttack(EnemyModel enemy, float delta)
    {
        var definition = enemy.Definition;
        var attack = definition.RangedAttack;
        if (attack is null || attack.Kind == EnemyAttackKind.None)
        {
            return;
        }

        var cooldown = MathF.Max(0f, enemyAttackCooldowns[definition.Id] - delta);
        enemyAttackCooldowns[definition.Id] = cooldown;
        if (!Hero.PistolReady || !Hero.IsAlive || transition is not null || cooldown > 0f
            || EnemyProjectiles.Any(projectile => projectile.SourceEnemyId == definition.Id))
        {
            return;
        }

        var scale = world.ScaleFor(definition.Lane);
        var origin = new Vec2(enemy.Position.X, enemy.Position.Y - 48f * scale);
        var target = new Vec2(Hero.Position.X, Hero.Position.Y - 42f * Hero.DisplayScale);
        var distance = MathF.Sqrt(origin.DistanceSquared(target));
        if (distance / 80f > attack.RangeMeters)
        {
            return;
        }

        enemy.Facing = Hero.Position.X >= enemy.Position.X ? 1 : -1;
        var obstacleHit = TraceEnemyTrajectory(
            attack.Kind,
            origin,
            definition.Lane,
            target,
            Hero.Lane);
        var fullDuration = MathF.Max(0.35f, distance / attack.ProjectileSpeed);
        EnemyProjectiles.Add(new EnemyProjectileModel(
            definition.Id,
            attack.Kind,
            origin,
            target,
            definition.Lane,
            Hero.Lane,
            fullDuration,
            attack.Damage,
            obstacleHit));
        enemyAttackCooldowns[definition.Id] = attack.CooldownSeconds;
        LastCombatMessage = attack.Kind == EnemyAttackKind.Fireball
            ? $"{definition.DisplayName} casts a fireball."
            : $"{definition.DisplayName} hurls a stone.";
    }

    private void UpdateEnemyProjectiles(
        float delta,
        Vec2 heroStart,
        float heroStartDepth,
        Vec2 heroEnd,
        float heroEndDepth)
    {
        for (var index = EnemyProjectiles.Count - 1; index >= 0; index--)
        {
            var projectile = EnemyProjectiles[index];
            var previousPosition = projectile.Position;
            var previousDepth = projectile.CurrentDepth;
            var previousElapsed = projectile.Elapsed;
            projectile.Elapsed = MathF.Min(projectile.Duration, projectile.Elapsed + delta);
            projectile.Position = projectile.PositionAtPathProgress(projectile.PathProgress);
            var activeFraction = delta <= 0f
                ? 0f
                : Math.Clamp((projectile.Elapsed - previousElapsed) / delta, 0f, 1f);
            var collisionHeroEnd = Vec2.Lerp(heroStart, heroEnd, activeFraction);
            var collisionHeroEndDepth = heroStartDepth + (heroEndDepth - heroStartDepth) * activeFraction;

            if (ProjectileHitsHero(
                    previousPosition,
                    previousDepth,
                    projectile.Position,
                    projectile.CurrentDepth,
                    heroStart,
                    heroStartDepth,
                    collisionHeroEnd,
                    collisionHeroEndDepth))
            {
                ApplyProjectileDamage(projectile);
                EnemyProjectiles.RemoveAt(index);
                continue;
            }

            if (projectile.Progress < 1f)
            {
                continue;
            }

            if (projectile.BlockedByObstacleId is not null)
            {
                WorldImpactEffects.Add(new WorldImpactEffectModel(
                    projectile.ImpactPoint,
                    projectile.ImpactLane,
                    projectile.Kind == EnemyAttackKind.Fireball ? WorldImpactKind.FireBurst : WorldImpactKind.StoneDust));
                LastCombatMessage = projectile.Kind == EnemyAttackKind.Fireball
                    ? $"The fireball bursts on {projectile.ImpactMaterial}."
                    : $"The thrown stone breaks on {projectile.ImpactMaterial}.";
                EnemyProjectiles.RemoveAt(index);
                continue;
            }

            LastCombatMessage = projectile.Kind == EnemyAttackKind.Fireball
                ? "The fireball bursts against the stones."
                : "The thrown stone misses Alice.";
            EnemyProjectiles.RemoveAt(index);
        }
    }

    private LineOfFireHit? TraceEnemyTrajectory(
        EnemyAttackKind kind,
        Vec2 origin,
        int sourceLane,
        Vec2 target,
        int targetLane)
    {
        var collisionKind = kind == EnemyAttackKind.Fireball
            ? AttackCollisionKind.TravellingMagic
            : AttackCollisionKind.ThrownPhysical;
        if (kind != EnemyAttackKind.ThrownStone)
        {
            return combatGeometry.Trace(origin, sourceLane, target, targetLane, collisionKind);
        }

        const int segments = 32;
        var previous = origin;
        var previousDepth = (float)sourceLane;
        for (var index = 1; index <= segments; index++)
        {
            var progress = index / (float)segments;
            var current = EnemyProjectileModel.PositionAlong(kind, origin, target, progress);
            var currentDepth = sourceLane + (targetLane - sourceLane) * progress;
            var hit = combatGeometry.Trace(previous, previousDepth, current, currentDepth, collisionKind);
            if (hit is not null)
            {
                return hit with
                {
                    SegmentProgress = (index - 1 + hit.SegmentProgress) / segments,
                };
            }
            previous = current;
            previousDepth = currentDepth;
        }
        return null;
    }

    private bool ProjectileHitsHero(
        Vec2 start,
        float startDepth,
        Vec2 end,
        float endDepth,
        Vec2 heroStart,
        float heroStartDepth,
        Vec2 heroEnd,
        float heroEndDepth)
    {
        if (!Hero.IsAlive)
        {
            return false;
        }
        var relativeStart = start - heroStart;
        var relativeDelta = (end - start) - (heroEnd - heroStart);
        if (!TryRadiusInterval(relativeStart, relativeDelta, ProjectileHitRadius, out var xyEntry, out var xyExit))
        {
            return false;
        }
        var depthStart = startDepth - heroStartDepth;
        var depthDelta = (endDepth - startDepth) - (heroEndDepth - heroStartDepth);
        return TryAbsoluteInterval(depthStart, depthDelta, 0.25f, out var depthEntry, out var depthExit)
            && MathF.Max(xyEntry, depthEntry) <= MathF.Min(xyExit, depthExit);
    }

    private void ApplyProjectileDamage(EnemyProjectileModel projectile)
    {
        Hero.Hp = Math.Max(0, Hero.Hp - projectile.Damage);
        Hero.HitFlashRemaining = HeroHitFlashDuration;
        LastCombatMessage = Hero.IsAlive
            ? $"Alice is hit for {projectile.Damage}."
            : "ALICE FALLS / THE CHAMBER CLAIMS HER";
    }

    private Vec2 HeroCollisionCenter() => new(Hero.Position.X, Hero.Position.Y - 42f * Hero.DisplayScale);

    private float HeroCollisionDepth() => transition is null
        ? Hero.Lane
        : transition.Definition.FromLane
            + (transition.Definition.ToLane - transition.Definition.FromLane) * transition.Progress;

    private static bool TryRadiusInterval(
        Vec2 relativeStart,
        Vec2 relativeDelta,
        float radius,
        out float entry,
        out float exit)
    {
        var a = relativeDelta.X * relativeDelta.X + relativeDelta.Y * relativeDelta.Y;
        var c = relativeStart.X * relativeStart.X + relativeStart.Y * relativeStart.Y - radius * radius;
        if (a <= 0.0001f)
        {
            entry = 0f;
            exit = 1f;
            return c <= 0f;
        }
        var b = 2f * (relativeStart.X * relativeDelta.X + relativeStart.Y * relativeDelta.Y);
        var discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
        {
            entry = 0f;
            exit = 0f;
            return false;
        }
        var root = MathF.Sqrt(discriminant);
        entry = MathF.Max(0f, (-b - root) / (2f * a));
        exit = MathF.Min(1f, (-b + root) / (2f * a));
        return entry <= exit;
    }

    private static bool TryAbsoluteInterval(
        float relativeStart,
        float relativeDelta,
        float limit,
        out float entry,
        out float exit)
    {
        if (MathF.Abs(relativeDelta) <= 0.0001f)
        {
            entry = 0f;
            exit = 1f;
            return MathF.Abs(relativeStart) <= limit;
        }
        var first = (-limit - relativeStart) / relativeDelta;
        var second = (limit - relativeStart) / relativeDelta;
        if (first > second)
        {
            (first, second) = (second, first);
        }
        entry = MathF.Max(0f, first);
        exit = MathF.Min(1f, second);
        return entry <= exit;
    }

    private void UpdatePlayerShotEffect(float delta)
    {
        if (ActivePlayerShotEffect is null)
        {
            return;
        }
        ActivePlayerShotEffect.Elapsed += delta;
        if (ActivePlayerShotEffect.Elapsed >= PlayerShotEffectModel.TotalDuration)
        {
            ActivePlayerShotEffect = null;
        }
    }

    private void UpdateWorldImpactEffects(float delta)
    {
        for (var index = WorldImpactEffects.Count - 1; index >= 0; index--)
        {
            var effect = WorldImpactEffects[index];
            effect.Elapsed += delta;
            if (effect.Elapsed >= WorldImpactEffectModel.TotalDuration)
            {
                WorldImpactEffects.RemoveAt(index);
            }
        }
    }

    private static void SetEnemyPresentation(EnemyModel enemy, EnemyPresentationState state, float duration)
    {
        enemy.PresentationState = state;
        enemy.PresentationDuration = duration;
        enemy.PresentationRemaining = duration;
    }

    private void TryCollectObjective()
    {
        if (ObjectiveCollected || Hero.Lane != world.Objective.Lane)
        {
            return;
        }
        if (MathF.Abs(Hero.Position.X - world.Objective.Position.X) <= world.Objective.PickupHalfExtents.X
            && MathF.Abs(Hero.Position.Y - world.Objective.Position.Y) <= world.Objective.PickupHalfExtents.Y)
        {
            ObjectiveCollected = true;
            LastCombatMessage = "PAPYRUS RECOVERED / CHAMBER SECURED";
            TryOpenTriggeredDecision("objective_collected", world.Objective.Id);
        }
    }

    private void TryOpenTriggeredDecision(string triggerEvent, string triggerId)
    {
        var decision = narrativeContent?.Decisions.Values.FirstOrDefault(item =>
            item.TriggerEvent == triggerEvent
            && item.TriggerId == triggerId
            && !Narrative.ResolvedDecisions.Contains(item.Id));
        if (decision is not null)
        {
            OpenDecision(decision.Id);
        }
    }

    private void UpdateDecision(InputFrame input, float delta)
    {
        if (input.DecisionChoiceNumber > 0)
        {
            ChooseDecision(input.DecisionChoiceNumber - 1);
            return;
        }
        if (ActiveDecision?.TimerSeconds is null)
        {
            return;
        }
        decisionSecondsRemaining = MathF.Max(0f, decisionSecondsRemaining - delta);
        if (decisionSecondsRemaining <= 0f)
        {
            ChooseDecision(ActiveDecision.TimeoutChoiceIndex);
        }
    }

    private bool BeginDirectedSequence(string id)
    {
        if (narrativeContent is null || !narrativeContent.DirectedSequences.TryGetValue(id, out var definition))
        {
            throw new InvalidDataException($"Decision refers to missing directed sequence '{id}'");
        }
        directedSequence = new ActiveDirectedSequence(definition, Hero.Position);
        transition = null;
        Hero.Velocity = new Vec2(0f, 0f);
        return true;
    }

    private void UpdateDirectedSequence(float delta)
    {
        var active = directedSequence ?? throw new InvalidOperationException("Directed sequence state disappeared");
        UpdateEnemyPresentation(delta);
        UpdatePlayerShotEffect(delta);
        UpdateWorldImpactEffects(delta);
        Hero.HitFlashRemaining = MathF.Max(0f, Hero.HitFlashRemaining - delta);
        if (active.BeatIndex >= active.Definition.Beats.Count)
        {
            EndDirectedSequence();
            return;
        }
        if (UpdateWeaponAction(delta))
        {
            return;
        }

        var beat = active.Definition.Beats[active.BeatIndex];
        active.Elapsed += delta;
        switch (beat.Kind)
        {
            case DirectedBeatKind.MoveTo:
                {
                    var targetX = beat.X ?? Hero.Position.X;
                    var duration = MathF.Max(beat.Duration, 0.001f);
                    var progress = Math.Clamp(active.Elapsed / duration, 0f, 1f);
                    var authored = Vec2.Lerp(active.BeatStart, new Vec2(targetX, active.BeatStart.Y), progress);
                    var support = supports.FindNear(Hero.Lane, authored.X, Hero.Position.Y, 72f)
                        ?? throw new InvalidDataException($"Directed move '{active.Definition.Id}' left authored support at x={authored.X:0.##}, lane={Hero.Lane}");
                    Hero.Position = new Vec2(authored.X, support.Y);
                    Hero.ActiveSupportId = support.SurfaceId;
                    Hero.TraversalMode = world.Supports.Single(surface => surface.Id == support.SurfaceId).TraversalMode;
                    Hero.Grounded = true;
                    Hero.Facing = targetX >= active.BeatStart.X ? 1 : -1;
                    Hero.State = HeroState.Move;
                    if (progress >= 1f)
                    {
                        AdvanceDirectedBeat(active);
                    }
                    break;
                }
            case DirectedBeatKind.FireAtTarget:
                {
                    var target = Enemies.FirstOrDefault(enemy => enemy.Definition.Id == beat.TargetId && enemy.IsAlive);
                    if (target is null || active.ShotsFired >= beat.Count || Hero.AmmoInMagazine <= 0)
                    {
                        AdvanceDirectedBeat(active);
                        break;
                    }
                    Hero.WeaponCondition = WeaponState.Ready;
                    SelectTarget(target);
                    ResolveShotAtTarget(target);
                    active.ShotsFired++;
                    break;
                }
            case DirectedBeatKind.Message:
                LastNarrativeMessage = beat.Text;
                if (active.Elapsed >= beat.Duration)
                {
                    AdvanceDirectedBeat(active);
                }
                break;
            case DirectedBeatKind.Wait:
                if (active.Elapsed >= beat.Duration)
                {
                    AdvanceDirectedBeat(active);
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported directed beat '{beat.Kind}'");
        }
    }

    private void AdvanceDirectedBeat(ActiveDirectedSequence active)
    {
        active.BeatIndex++;
        active.Elapsed = 0f;
        active.ShotsFired = 0;
        active.BeatStart = Hero.Position;
        if (active.BeatIndex >= active.Definition.Beats.Count)
        {
            EndDirectedSequence();
        }
    }

    private void EndDirectedSequence()
    {
        directedSequence = null;
        Hero.State = Hero.Grounded ? HeroState.Move : HeroState.Airborne;
        Hero.Velocity = new Vec2(0f, 0f);
    }

    private static string DescribeShot(EnemyModel target, RurkShotResult shot)
    {
        if (!shot.Hit)
        {
            return $"{target.Definition.DisplayName}: d100 {shot.HitRoll} > {shot.Target}, MISS / ZPN {shot.Math.Zpn} - SVD {shot.TotalSvd}";
        }
        if (!shot.Penetrated)
        {
            return $"{target.Definition.DisplayName}: d100 {shot.HitRoll} <= {shot.Target}, {RurkCombatResolver.ZoneName(shot.Zone)}, armour stopped it";
        }
        return $"{target.Definition.DisplayName}: d100 {shot.HitRoll} <= {shot.Target}, {RurkCombatResolver.ZoneName(shot.Zone)}, {shot.Damage} damage";
    }

    private Vec2 TargetPointFor(EnemyModel target, RurkHitZone zone)
    {
        var scale = world.ScaleFor(target.Definition.Lane);
        var vertical = zone switch
        {
            RurkHitZone.Head => 80f,
            RurkHitZone.Chest => 60f,
            RurkHitZone.Belly => 45f,
            RurkHitZone.RightArm or RurkHitZone.LeftArm => 57f,
            RurkHitZone.Groin => 31f,
            RurkHitZone.RightLeg or RurkHitZone.LeftLeg => 17f,
            _ => 60f,
        };
        var facing = target.Facing >= 0 ? 1f : -1f;
        var horizontal = zone switch
        {
            RurkHitZone.RightArm or RurkHitZone.RightLeg => 10f * scale * facing,
            RurkHitZone.LeftArm or RurkHitZone.LeftLeg => -10f * scale * facing,
            _ => 0f,
        };
        return new Vec2(target.Position.X + horizontal, target.Position.Y - vertical * scale);
    }

    private bool TryBeginNearestTransition(int laneDirection)
    {
        var candidate = world.Transitions
            .Where(item => item.FromLane == Hero.Lane && Math.Sign(item.ToLane - item.FromLane) == laneDirection)
            .Where(item => Contains(item.TriggerPolygon, Hero.Position))
            .Select(item => (Definition: item, Distance: item.Path[0].DistanceSquared(Hero.Position)))
            .OrderBy(item => item.Distance)
            .FirstOrDefault();
        return candidate.Definition is not null && BeginTransition(candidate.Definition.Id);
    }

    private static bool Contains(IReadOnlyList<Vec2> polygon, Vec2 point)
    {
        if (polygon.Count < 3)
        {
            return false;
        }
        var inside = false;
        for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
        {
            var a = polygon[current];
            var b = polygon[previous];
            var crosses = (a.Y > point.Y) != (b.Y > point.Y)
                && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (crosses)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private void UpdateTransition(float delta)
    {
        var active = transition!;
        active.Elapsed = MathF.Min(active.Elapsed + delta, active.Definition.Duration);
        var linear = active.Elapsed / active.Definition.Duration;
        var smooth = linear * linear * (3f - 2f * linear);
        Hero.Position = PointOnPath(active, smooth);
        Hero.DisplayScale = active.SourceScale + (active.TargetScale - active.SourceScale) * linear;
        if (linear >= active.Definition.LaneHandoff)
        {
            Hero.Lane = active.Definition.ToLane;
        }
        if (linear < 1f)
        {
            return;
        }

        Hero.Lane = active.Definition.ToLane;
        Hero.DisplayScale = active.TargetScale;
        Hero.Velocity = new Vec2(0f, 0f);
        var ground = supports.FindNear(Hero.Lane, Hero.Position.X, Hero.Position.Y, 48f);
        if (ground is not null)
        {
            Hero.Position = new Vec2(Hero.Position.X, ground.Value.Y);
            Hero.ActiveSupportId = ground.Value.SurfaceId;
            Hero.TraversalMode = world.Supports.Single(surface => surface.Id == ground.Value.SurfaceId).TraversalMode;
            Hero.Grounded = true;
        }
        Hero.State = Hero.Grounded ? HeroState.Move : HeroState.Airborne;
        transition = null;
    }

    private static Vec2 PointOnPath(ActiveTransition active, float t)
    {
        var points = active.Path;
        var lengths = new float[points.Count - 1];
        var total = 0f;
        for (var index = 0; index < lengths.Length; index++)
        {
            lengths[index] = MathF.Sqrt(points[index].DistanceSquared(points[index + 1]));
            total += lengths[index];
        }
        var remaining = total * Math.Clamp(t, 0f, 1f);
        for (var index = 0; index < lengths.Length; index++)
        {
            if (remaining <= lengths[index] || index == lengths.Length - 1)
            {
                var local = lengths[index] <= 0.001f ? 0f : remaining / lengths[index];
                return Vec2.Lerp(points[index], points[index + 1], local);
            }
            remaining -= lengths[index];
        }
        return points[^1];
    }

    private sealed class ActiveTransition
    {
        public ActiveTransition(DepthTransition definition, Vec2 heroStart, float sourceScale, float targetScale)
        {
            Definition = definition;
            SourceScale = sourceScale;
            TargetScale = targetScale;
            Path = new List<Vec2> { heroStart };
            var nearest = definition.Path
                .Select((point, index) => (Index: index, Distance: point.DistanceSquared(heroStart)))
                .OrderBy(item => item.Distance)
                .First().Index;
            Path.AddRange(definition.Path.Skip(nearest).Where(point => point.DistanceSquared(Path[^1]) > 1f));
        }

        public DepthTransition Definition { get; }
        public List<Vec2> Path { get; }
        public float SourceScale { get; }
        public float TargetScale { get; }
        public float Elapsed { get; set; }
        public float Progress => Elapsed / Definition.Duration;
    }

    private sealed class ActiveDirectedSequence(DirectedSequenceDefinition definition, Vec2 beatStart)
    {
        public DirectedSequenceDefinition Definition { get; } = definition;
        public int BeatIndex { get; set; }
        public float Elapsed { get; set; }
        public int ShotsFired { get; set; }
        public Vec2 BeatStart { get; set; } = beatStart;
    }
}
