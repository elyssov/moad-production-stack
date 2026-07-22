using Moad.Engine;

var levelPath = Path.Combine(AppContext.BaseDirectory, "Content", "levels", "papyrus_chamber.json");
var world = WorldLoader.Load(levelPath);
var narrativePath = Path.Combine(AppContext.BaseDirectory, "Content", "narrative", "papyrus_decision_proof.json");
var narrative = NarrativeContentLoader.Load(narrativePath);

Assert(world.Size == new Vec2(3840f, 720f), "room size");
AssertClose(world.PixelsPerMeter, 80f, 0.001f, "world physical scale");
Assert(world.Tracks.Count == 3, "three depth tracks");
Assert(world.Supports.Count >= 8, "support surface catalog");
var unstableBridge = world.Supports.Single(surface => surface.Id == "middle_unstable_bridge");
Assert(unstableBridge is { TraversalMode: "unstable_bridge", SpeedMultiplier: 0.5f }, "visible bridge has explicit unstable traversal semantics");
Assert(world.Enemies.Count == 2, "two contract enemies");
Assert(world.Enemies.Single(enemy => enemy.Id == "Zombie1") is { MaxHp: 15, ArmourClass: 10, MotionClass: "hobble" }, "mummy combat contract");
Assert(world.Enemies.Single(enemy => enemy.Id == "Cultist1") is { MaxHp: 10, ArmourClass: 5, CoverLevel: 0 }, "cult adept has no invisible authored cover");
Assert(world.Enemies.Single(enemy => enemy.Id == "Zombie1").RangedAttack is { Kind: EnemyAttackKind.ThrownStone, Damage: 1 }, "mummy throws a physical stone");
Assert(world.Enemies.Single(enemy => enemy.Id == "Cultist1").RangedAttack is { Kind: EnemyAttackKind.Fireball, Damage: 2 }, "cult adept casts a visible fireball");
Assert(world.CombatObstacles.Count == 4, "room loads four art-backed combat obstacles");
Assert(world.AuthoredObstacles.Count == 0, "legacy room has no editor-authored obstacles");
Assert(world.AuthoredOccluders.Count == 0, "legacy room has no editor-authored occluders");
Assert(world.CoverZones.Count == 4, "room loads four art-backed cover zones");
Assert(world.Objective is { Id: "papyrus", Lane: 2 }, "papyrus objective contract");
Assert(narrative.Decisions["papyrus_silence"].Choices.Count == 4, "decision contract accepts four authored choices");
Assert(narrative.Decisions["papyrus_silence"].SlideAsset == "backgrounds/papyrus_archive.png", "decision contract carries a presentation-owned slide asset ID");
Assert(narrative.Decisions["papyrus_silence"].SlideCrop is { Width: 1050f, Height: 240f }, "decision contract carries optional slide crop geometry");
Assert(narrative.DirectedSequences["alice_warning_shots"].Beats.Count == 5, "directed sequence is data-authored");

var combat = new RurkCombatResolver(1994);
var crossTrackMath = combat.CalculateLugerTarget(new RurkShotContext
{
    DistanceMeters = 14.9,
    TargetMovementSvd = 5,
    TrackDistance = 1,
});
Assert(crossTrackMath.DistanceSvd == 10, "distance adds one SVD per metre beyond five");
Assert(crossTrackMath.TrackSvd == 10, "adjacent track adds ten SVD");
Assert(crossTrackMath.TotalSvd == 25, "SVD components add without multiplication");
Assert(crossTrackMath.Target == 46, "ZPN 71 minus SVD 25 gives target 46");
var uncappedMath = combat.CalculateLugerTarget(new RurkShotContext { Zpn = 121 });
Assert(uncappedMath.Target == 121, "ZPN above one hundred remains uncapped");
var impossibleMath = combat.CalculateLugerTarget(new RurkShotContext { Zpn = 5, ExtraSvd = 10 });
Assert(impossibleMath.Target == -5, "final target below one remains below one");
Assert(RurkCombatResolver.OutcomeFor(4, impossibleMath.Target) == RurkShotOutcome.Failure, "target below one cannot become a critical-success hit");
Assert(RurkCombatResolver.OutcomeFor(94, uncappedMath.Target) == RurkShotOutcome.Success, "uncapped expertise survives ordinary high rolls");
Assert(RurkCombatResolver.OutcomeFor(96, uncappedMath.Target) == RurkShotOutcome.CriticalFailure, "explicit critical-failure rule still overrides uncapped ZPN");

var combatGeometry = new CombatGeometry(world);
var centralRubbleHit = combatGeometry.Trace(
    new Vec2(2200f, 540f), 0f,
    new Vec2(2600f, 520f), 0f,
    AttackCollisionKind.InstantBallistic);
Assert(centralRubbleHit?.ObstacleId == "near_central_rubble_core", "same-lane Luger ray hits the painted central rubble core");
Assert(combatGeometry.Trace(
    new Vec2(2200f, 450f), 0f,
    new Vec2(2600f, 450f), 0f,
    AttackCollisionKind.InstantBallistic) is null, "ray above the rubble remains clear");
Assert(combatGeometry.Trace(
    new Vec2(2200f, 540f), 1f,
    new Vec2(2600f, 520f), 1f,
    AttackCollisionKind.InstantBallistic) is null, "near-depth rubble does not block a middle-depth ray");
Assert(combatGeometry.Trace(
    new Vec2(2200f, 540f), 0f,
    new Vec2(2600f, 520f), 0f,
    AttackCollisionKind.TravellingMagic)?.ObstacleId == "near_central_rubble_core", "the same obstacle stops travelling magic");
Assert(combatGeometry.Trace(
    new Vec2(2200f, 540f), 0f,
    new Vec2(2600f, 520f), 0f,
    AttackCollisionKind.ThrownPhysical)?.ObstacleId == "near_central_rubble_core", "the same obstacle stops thrown physical attacks");
Assert(combatGeometry.CoverLevel(
    new Vec2(2200f, 540f), 0f,
    new Vec2(2440f, 540f), new Vec2(2440f, 580f), 0) == 3,
    "central painted rubble provides head-only cover when it actually crosses the directed line of fire");
Assert(combatGeometry.CoverLevel(
    new Vec2(2200f, 540f), 0f,
    new Vec2(2385f, 540f), new Vec2(2385f, 580f), 0) == 0,
    "standing inside a broad cover zone does not grant cover before the linked obstacle");

var movementObstacle = new AuthoredObstacle(
    "test_column",
    1,
    [new Vec2(95f, 20f), new Vec2(115f, 20f), new Vec2(115f, 100f), new Vec2(95f, 100f)],
    0f,
    2.5f,
    true,
    "stone");
Assert(AuthoredObstacleGeometry.BlocksMovement(
    [movementObstacle], 1, new Vec2(75f, 100f), new Vec2(100f, 100f), 80f, 8f),
    "authored movement obstacle blocks an actor entering its polygon on the same depth lane");
Assert(!AuthoredObstacleGeometry.BlocksMovement(
    [movementObstacle], 0, new Vec2(75f, 100f), new Vec2(100f, 100f), 80f, 8f),
    "authored movement obstacle does not block a different depth lane");
Assert(!AuthoredObstacleGeometry.BlocksMovement(
    [movementObstacle], 1, new Vec2(125f, 100f), new Vec2(140f, 100f), 80f, 8f),
    "authored movement obstacle allows motion outside its polygon");

var stopped = combat.ResolveLugerShot(
    new RurkShotContext { ArmourClass = 10 },
    [20, 25, 19]);
Assert(stopped.Hit && !stopped.Penetrated && stopped.Damage == 0, "mummy armour stops penetration roll 19");
var penetrated = combat.ResolveLugerShot(
    new RurkShotContext { ArmourClass = 10 },
    [20, 25, 20, 65]);
Assert(penetrated.Penetrated && penetrated.Damage == 8, "mummy armour accepts penetration roll 20 and damage math");

var solver = new SupportSolver(world);
var farTop = solver.FindNear(2, 2250f, 180f, 2f);
Assert(farTop is { Y: 180f }, "far gallery is bound directly to painted y=180");

var simulation = new WorldSimulation(world, new RurkCombatResolver(2));
simulation.Reset(new Vec2(1740f, 430f), 1);
var middleScale = simulation.Hero.DisplayScale;
Advance(simulation, 2f);
AssertClose(simulation.Hero.Position.Y, 580f, 0.25f, "middle-depth lower catch");
Assert(simulation.Hero.Lane == 1, "middle fall keeps depth");
AssertClose(simulation.Hero.DisplayScale, middleScale, 0.0001f, "middle fall keeps scale");

simulation.Reset(new Vec2(1000f, 250f), 2);
var farScale = simulation.Hero.DisplayScale;
Advance(simulation, 2f);
AssertClose(simulation.Hero.Position.Y, 390f, 0.25f, "far fall catches first intermediate support");
Assert(simulation.Hero.Lane == 2, "far fall keeps depth");
AssertClose(simulation.Hero.DisplayScale, farScale, 0.0001f, "far fall keeps scale");

simulation.Reset(new Vec2(2880f, 390f), 1);
Assert(simulation.BeginTransition("stairs_right_middle_to_far"), "right stair starts");
Advance(simulation, 2f);
Assert(simulation.Hero.Lane == 2, "right stair changes depth");
AssertClose(simulation.Hero.Position.X, 3200f, 0.25f, "right stair target x");
AssertClose(simulation.Hero.Position.Y, 270f, 0.25f, "right stair target y");
Assert(simulation.Hero.Facing == 1, "right stair faces travel direction");

Assert(simulation.BeginTransition("stairs_right_far_to_middle"), "right stair reverse starts");
Advance(simulation, 2f);
Assert(simulation.Hero.Lane == 1, "right stair reverse restores middle depth");
AssertClose(simulation.Hero.Position.X, 2880f, 0.25f, "right stair reverse target x");
AssertClose(simulation.Hero.Position.Y, 390f, 0.25f, "right stair reverse target y");
Assert(simulation.Hero.Facing == -1, "right stair reverse faces travel direction");

simulation.Reset(new Vec2(800f, 580f), 0);
simulation.Update(new InputFrame(0f, false, false, true, false), 1f / 60f);
Assert(simulation.ActiveTransitionId == "stairs_left_near_to_middle", "public W/Up starts the left staircase");
Advance(simulation, 2.1f);
Assert(simulation.Hero.Lane == 1, "public W/Up reaches the middle track");
AssertClose(simulation.Hero.Position.X, 250f, 0.25f, "public stair ascent target x");
AssertClose(simulation.Hero.Position.Y, 390f, 0.25f, "public stair ascent target y");

simulation.Update(new InputFrame(0f, false, false, false, true), 1f / 60f);
Assert(simulation.ActiveTransitionId == "stairs_left_middle_to_near", "public S/Down starts the reverse staircase");
Advance(simulation, 2.1f);
Assert(simulation.Hero.Lane == 0, "public S/Down returns to the near track");
AssertClose(simulation.Hero.Position.X, 800f, 0.25f, "public stair descent target x");
AssertClose(simulation.Hero.Position.Y, 580f, 0.25f, "public stair descent target y");

simulation.Reset(new Vec2(1600f, 390f), 1);
Assert(simulation.Hero.TraversalMode == "unstable_bridge", "bridge binds to unstable traversal mode");
Assert(simulation.Hero.BalanceRequired, "bridge requests the dedicated balance gait");
Advance(simulation, 4f, new InputFrame(1f, false, false, false, false));
Assert(simulation.Hero.Grounded, "visible bridge supports a complete crossing");
Assert(simulation.Hero.Position.X > 1840f && simulation.Hero.Position.X < 1870f, "bridge halves walking speed");
Advance(simulation, 1f, new InputFrame(1f, false, false, false, false));
Assert(simulation.Hero.Grounded && simulation.Hero.Position.X > 1900f, "bridge hands off to the right platform without a fall");

var stanceSimulation = new WorldSimulation(world, new RurkCombatResolver(2));
stanceSimulation.Reset(world.Spawn, world.SpawnLane);
stanceSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Advance(stanceSimulation, 0.6f);
stanceSimulation.Update(new InputFrame(1f, false, false, false, false), 1f / 60f);
Assert(stanceSimulation.Hero.PistolReady, "ordinary walking preserves the drawn Guardian Luger");
Assert(stanceSimulation.Hero.State == HeroState.Move, "armed walking remains a locomotion state");
stanceSimulation.Update(new InputFrame(1f, true, false, false, false), 1f / 60f);
Assert(!stanceSimulation.Hero.PistolReady, "beginning an actual sprint holsters the Guardian Luger");
stanceSimulation.Update(new InputFrame(0f, false, false, false, false, CrouchPressed: true), 1f / 60f);
Assert(stanceSimulation.Hero is { IsCrouched: true, State: HeroState.Crouch }, "crouch input enters an authoritative crouch state");
var crouchStart = stanceSimulation.Hero.Position.X;
Advance(stanceSimulation, 1f, new InputFrame(1f, false, false, false, false));
Assert(stanceSimulation.Hero.Position.X > crouchStart + 55f && stanceSimulation.Hero.Position.X < crouchStart + 70f, "crouch walk uses its own half-speed gait");
stanceSimulation.Update(new InputFrame(0f, false, false, false, false, CrouchPressed: true), 1f / 60f);
Assert(!stanceSimulation.Hero.IsCrouched, "second crouch input returns Alice to standing");

simulation.Reset(world.Spawn, world.SpawnLane);
simulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Assert(simulation.Hero.PistolReady, "first shoot input draws pistol");
Assert(simulation.Hero.AmmoInMagazine == 8, "drawing pistol does not spend ammunition");
Assert(simulation.SelectedTarget is not null, "drawing pistol selects nearest living target");
var initiallySelected = simulation.SelectedTargetId;
simulation.CycleTarget();
Assert(simulation.SelectedTargetId != initiallySelected, "Q-style cycle selects the other living target");
simulation.CycleTarget();
Assert(simulation.SelectedTargetId == initiallySelected, "target cycle wraps left-to-right");
Advance(simulation, 0.6f);
simulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Assert(simulation.Hero.AmmoInMagazine == 7, "second shoot input fires one round");
Assert(simulation.LastShot?.Hit == true, "seeded combat shot hits for presentation test");
Assert(simulation.ActivePlayerShotEffect is { ImpactKind: FirearmImpactKind.MummyDust }, "Luger hit exposes a short authoritative mummy-dust effect");
var shotEffectProgress = simulation.ActivePlayerShotEffect!.Progress;
simulation.Update(default, 1f / 60f);
Assert(simulation.ActivePlayerShotEffect?.Progress > shotEffectProgress, "Luger presentation effect advances across frames without a beam");
Assert(simulation.Enemies.Single(enemy => enemy.Definition.Id == "Zombie1").PresentationState == EnemyPresentationState.HitLight, "Luger hit starts light mummy reaction");

var armourStopSimulation = new WorldSimulation(WithEnemyArmour(world, 1000), new RurkCombatResolver(2));
armourStopSimulation.Reset(new Vec2(2200f, 580f), 0);
armourStopSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Advance(armourStopSimulation, 0.6f);
armourStopSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Assert(armourStopSimulation.LastShot is { Hit: true, Penetrated: false }, "authored extreme armour stops a seeded Luger hit");
Assert(armourStopSimulation.ActivePlayerShotEffect is { ImpactKind: FirearmImpactKind.MummyDust }, "armour-stopped mummy hit still produces dust instead of silent nothing");

var blockedShotSimulation = new WorldSimulation(world, new RurkCombatResolver(2));
blockedShotSimulation.Reset(new Vec2(2200f, 580f), 0);
Advance(blockedShotSimulation, 3f);
blockedShotSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Advance(blockedShotSimulation, 0.6f);
var blockedTarget = blockedShotSimulation.SelectedTarget ?? throw new InvalidOperationException("blocked-shot target missing");
var blockedTargetHp = blockedTarget.Hp;
blockedShotSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Assert(blockedShotSimulation.LastShot is { AimHit: true, Hit: false }, "blocked-shot seed wins the aim test but the bullet does not reach the target");
Assert(blockedShotSimulation.LastShotBlockedByObstacleId == "near_central_rubble_core", "painted rubble authoritatively blocks the successful shot");
Assert(blockedShotSimulation.LastShot is { PenetrationRoll: 0, DamageRoll: 0, Damage: 0 }, "blocked bullet does not consume phantom armour or damage rolls");
Assert(blockedTarget.Hp == blockedTargetHp, "blocked bullet cannot damage the target behind rubble");
Assert(blockedShotSimulation.ActivePlayerShotEffect is { ImpactKind: FirearmImpactKind.StoneObstacle }, "blocked bullet exposes a stone-impact presentation event");
Assert(blockedShotSimulation.CoverLevelFor(blockedTarget) == 3, "the blocked target receives head-only cover from the same geometry");

var missedIntoCoverSimulation = new WorldSimulation(world, new RurkCombatResolver(13));
missedIntoCoverSimulation.Reset(new Vec2(2200f, 580f), 0);
Advance(missedIntoCoverSimulation, 3f);
missedIntoCoverSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Advance(missedIntoCoverSimulation, 0.6f);
missedIntoCoverSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Assert(missedIntoCoverSimulation.LastShot is { AimHit: false, Hit: false }, "critical-failure aim does not become a target hit");
Assert(missedIntoCoverSimulation.LastShotBlockedByObstacleId == "near_central_rubble_core", "failed aim still resolves its physical trajectory against the wall");
Assert(missedIntoCoverSimulation.ActivePlayerShotEffect is { ImpactKind: FirearmImpactKind.StoneObstacle }, "failed aim into cover still produces a visible stone impact");
Advance(simulation, 0.5f);
simulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Assert(simulation.Hero.AmmoInMagazine == 6, "subsequent shot spends one round");
Advance(simulation, 0.5f);
simulation.Update(new InputFrame(0f, false, false, false, false, ReloadPressed: true), 1f / 60f);
Assert(simulation.Hero.ReloadPhase == 0, "reload starts at phase zero");
Advance(simulation, 2.8f);
Assert(simulation.Hero.AmmoInMagazine == 6 && simulation.Hero.ReserveAmmo == 16, "reload does not transfer ammunition early");
Advance(simulation, 0.3f);
Assert(simulation.Hero.AmmoInMagazine == 8 && simulation.Hero.ReserveAmmo == 14, "reload transfers ammunition after three seconds");

var attackSimulation = new WorldSimulation(world, new RurkCombatResolver(2));
attackSimulation.Reset(new Vec2(2200f, 580f), 0);
attackSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
AdvanceUntil(attackSimulation, () => attackSimulation.EnemyProjectiles.Count > 0, 3f, "enemy launches a projectile after combat begins");
var launchedProjectile = attackSimulation.EnemyProjectiles[0];
var launchPosition = launchedProjectile.Position;
attackSimulation.Update(default, 1f / 60f);
Assert(attackSimulation.EnemyProjectiles[0].Position != launchPosition, "enemy projectile persists and advances across frames");
var hpBeforeAttack = attackSimulation.Hero.Hp;
AdvanceUntil(attackSimulation, () => attackSimulation.Hero.Hp < hpBeforeAttack, 3f, "physical projectile reaches Alice and applies damage");
Assert(attackSimulation.Hero.Hp == hpBeforeAttack - 1, "mummy stone applies its authored one point of damage");
Assert(attackSimulation.Hero.HitFlashRemaining > 0f, "projectile impact exposes visible hit feedback state");

var blockedStoneSimulation = new WorldSimulation(
    WithSoleEnemyPlacement(world, "Zombie1", new Vec2(2520f, 580f)),
    new RurkCombatResolver(2));
blockedStoneSimulation.Reset(new Vec2(2200f, 580f), 0);
blockedStoneSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
AdvanceUntil(blockedStoneSimulation, () => blockedStoneSimulation.EnemyProjectiles.Count > 0, 1f, "blocked-stone enemy launches");
Assert(blockedStoneSimulation.EnemyProjectiles.Single().BlockedByObstacleId == "near_central_rubble_core", "central rubble fixes the stone's collision endpoint at launch");
var hpBeforeBlockedStone = blockedStoneSimulation.Hero.Hp;
AdvanceUntil(blockedStoneSimulation, () => blockedStoneSimulation.WorldImpactEffects.Count > 0, 2f, "blocked stone reaches room geometry");
Assert(blockedStoneSimulation.Hero.Hp == hpBeforeBlockedStone, "stone stopped by rubble cannot damage Alice");
Assert(blockedStoneSimulation.WorldImpactEffects.Single().Kind == WorldImpactKind.StoneDust, "blocked stone emits a typed stone-dust impact");

var preObstacleInterceptionSimulation = new WorldSimulation(
    WithSoleEnemyPlacement(world, "Zombie1", new Vec2(3000f, 580f)),
    new RurkCombatResolver(2));
preObstacleInterceptionSimulation.Reset(new Vec2(2380f, 580f), 0);
preObstacleInterceptionSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
AdvanceUntil(preObstacleInterceptionSimulation, () => preObstacleInterceptionSimulation.EnemyProjectiles.Count > 0, 1f, "pre-obstacle interception launches");
Assert(preObstacleInterceptionSimulation.EnemyProjectiles.Single().BlockedByObstacleId == "near_central_rubble_core", "interception projectile still has an authoritative later obstacle impact");
var hpBeforePreObstacleInterception = preObstacleInterceptionSimulation.Hero.Hp;
for (var frame = 0; frame < 120 && preObstacleInterceptionSimulation.Hero.Hp == hpBeforePreObstacleInterception; frame++)
{
    preObstacleInterceptionSimulation.Update(new InputFrame(1f, true, false, false, false), 1f / 60f);
}
Assert(preObstacleInterceptionSimulation.Hero.Hp == hpBeforePreObstacleInterception - 1, "Alice crossing the trajectory before its obstacle is hit by the blocked stone");

var fireballSimulation = new WorldSimulation(world, new RurkCombatResolver(2));
fireballSimulation.Reset(new Vec2(3200f, 270f), 2);
fireballSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
AdvanceUntil(fireballSimulation, () => fireballSimulation.EnemyProjectiles.Any(projectile => projectile.Kind == EnemyAttackKind.Fireball), 4f, "cult adept launches a fireball");
Assert(fireballSimulation.EnemyProjectiles.Single(projectile => projectile.Kind == EnemyAttackKind.Fireball).Damage == 2, "fireball carries its authored damage");
var hpBeforeFireball = fireballSimulation.Hero.Hp;
AdvanceUntil(fireballSimulation, () => fireballSimulation.Hero.Hp < hpBeforeFireball, 7f, "fireball reaches Alice and applies damage");
Assert(fireballSimulation.Hero.Hp == hpBeforeFireball - 2, "cult fireball applies two points of damage");

var crossingFireballSimulation = new WorldSimulation(
    WithSoleEnemyPlacement(world, "Cultist1", new Vec2(2200f, 180f)),
    new RurkCombatResolver(2));
crossingFireballSimulation.Reset(new Vec2(2800f, 180f), 2);
crossingFireballSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
AdvanceUntil(crossingFireballSimulation, () => crossingFireballSimulation.EnemyProjectiles.Count > 0, 1f, "crossing-fireball enemy launches");
var hpBeforeCrossingFireball = crossingFireballSimulation.Hero.Hp;
for (var frame = 0; frame < 120 && crossingFireballSimulation.Hero.Hp == hpBeforeCrossingFireball; frame++)
{
    crossingFireballSimulation.Update(new InputFrame(-1f, false, false, false, false), 1f / 60f);
}
Assert(crossingFireballSimulation.Hero.Hp == hpBeforeCrossingFireball - 2, "moving Alice collides with a fireball swept segment before its old endpoint");

var blockedFireballSimulation = new WorldSimulation(
    WithSoleEnemyPlacement(world, "Cultist1", new Vec2(2200f, 180f)),
    new RurkCombatResolver(2));
blockedFireballSimulation.Reset(new Vec2(1600f, 180f), 2);
blockedFireballSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
AdvanceUntil(blockedFireballSimulation, () => blockedFireballSimulation.EnemyProjectiles.Count > 0, 1f, "blocked-fireball enemy launches");
Assert(blockedFireballSimulation.EnemyProjectiles.Single().BlockedByObstacleId == "far_low_plinth", "far plinth fixes the fireball collision endpoint at launch");
var hpBeforeBlockedFireball = blockedFireballSimulation.Hero.Hp;
AdvanceUntil(blockedFireballSimulation, () => blockedFireballSimulation.WorldImpactEffects.Count > 0, 3f, "blocked fireball reaches room geometry");
Assert(blockedFireballSimulation.Hero.Hp == hpBeforeBlockedFireball, "fireball stopped by the plinth cannot damage Alice");
Assert(blockedFireballSimulation.WorldImpactEffects.Single().Kind == WorldImpactKind.FireBurst, "blocked fireball emits a typed fire burst");

var finalFlightFractionSimulation = new WorldSimulation(
    WithThinBlockedFireballScenario(world),
    new RurkCombatResolver(2));
finalFlightFractionSimulation.Reset(new Vec2(2446f, 580f), 0);
finalFlightFractionSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
AdvanceUntil(finalFlightFractionSimulation, () => finalFlightFractionSimulation.EnemyProjectiles.Count > 0, 1f, "short-final-flight fireball launches");
var finalFlightProjectile = finalFlightFractionSimulation.EnemyProjectiles.Single();
Assert(finalFlightProjectile.BlockedByObstacleId == "timing_wall", "short-final-flight fireball has a later obstacle impact");
const float finalFlightRemainder = 0.005f;
while (finalFlightProjectile.Duration - finalFlightProjectile.Elapsed > finalFlightRemainder + 0.0001f)
{
    var remaining = finalFlightProjectile.Duration - finalFlightProjectile.Elapsed;
    finalFlightFractionSimulation.Update(default, MathF.Min(0.05f, remaining - finalFlightRemainder));
    finalFlightProjectile = finalFlightFractionSimulation.EnemyProjectiles.Single();
}
var hpBeforeFinalFlight = finalFlightFractionSimulation.Hero.Hp;
finalFlightFractionSimulation.Update(new InputFrame(1f, true, false, false, false), 0.05f);
Assert(finalFlightFractionSimulation.Hero.Hp == hpBeforeFinalFlight, "Alice movement after a projectile's final five milliseconds is not compressed before wall impact");
Assert(finalFlightFractionSimulation.WorldImpactEffects.Single().Kind == WorldImpactKind.FireBurst, "wall impact wins after the projectile lifetime ends inside a longer simulation tick");

var pausedAttackSimulation = new WorldSimulation(world, new RurkCombatResolver(2), narrative);
pausedAttackSimulation.Reset(new Vec2(2200f, 580f), 0);
pausedAttackSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
AdvanceUntil(pausedAttackSimulation, () => pausedAttackSimulation.EnemyProjectiles.Count > 0, 3f, "pause proof launches an authoritative projectile");
var pausedProjectile = pausedAttackSimulation.EnemyProjectiles[0].Position;
Assert(pausedAttackSimulation.OpenDecision("papyrus_silence"), "decision opens during enemy projectile flight");
Advance(pausedAttackSimulation, 0.5f);
Assert(pausedAttackSimulation.EnemyProjectiles[0].Position == pausedProjectile, "narrative decision pauses enemy projectiles with the world");

var durableWorld = WithEnemyHp(world, 1000);
WorldSimulation? autoReloadSimulation = null;
for (ulong seed = 1; seed <= 100 && autoReloadSimulation is null; seed++)
{
    var candidate = new WorldSimulation(durableWorld, new RurkCombatResolver(seed));
    candidate.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
    Advance(candidate, 0.6f);
    for (var shot = 0; shot < 8; shot++)
    {
        candidate.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
        Advance(candidate, 0.5f);
    }
    if (candidate.Hero.State == HeroState.Reload && candidate.Hero.WeaponCondition == WeaponState.Reloading)
    {
        autoReloadSimulation = candidate;
    }
}
Assert(autoReloadSimulation is not null, "normal eight-shot sequence reaches automatic reload without a jam");
var reloadSimulation = autoReloadSimulation!;
Assert(reloadSimulation.Hero.State == HeroState.Reload && reloadSimulation.Hero.AmmoInMagazine == 0, "empty magazine starts reload automatically");
Assert(reloadSimulation.Hero.ReloadProgress > 0f && reloadSimulation.Hero.ReloadProgress < 0.1f, "automatic reload exposes normalized progress");
Advance(reloadSimulation, 3.1f);
Assert(reloadSimulation.Hero.AmmoInMagazine == 8 && reloadSimulation.Hero.ReserveAmmo == 8, "automatic reload completes after three seconds");

var jamSimulation = new WorldSimulation(world, new RurkCombatResolver(13));
jamSimulation.Reset(new Vec2(2200f, 580f), 0);
jamSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Advance(jamSimulation, 0.6f);
jamSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Assert(jamSimulation.LastShot?.Outcome == RurkShotOutcome.CriticalFailure, "seeded roll 96 is a critical failure");
Advance(jamSimulation, 0.5f);
Assert(jamSimulation.Hero.WeaponCondition == WeaponState.ClearingJam, "critical failure jams the Guardian Luger");
AssertClose(jamSimulation.Hero.WeaponActionDuration, 6f, 0.001f, "jam clearance doubles reload duration");
Advance(jamSimulation, 6.1f);
Assert(jamSimulation.Hero.WeaponCondition == WeaponState.Ready, "jam clearance returns the weapon to ready");

var deathSimulation = new WorldSimulation(WithEnemyHp(world, 1), new RurkCombatResolver(2));
deathSimulation.Reset(new Vec2(2200f, 580f), 0);
deathSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
Advance(deathSimulation, 0.6f);
deathSimulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
var dyingMummy = deathSimulation.Enemies.Single(enemy => enemy.Definition.Id == "Zombie1");
Assert(dyingMummy.PresentationState == EnemyPresentationState.Dying, "lethal hit starts death playback instead of disappearing");
Advance(deathSimulation, 0.8f);
Assert(dyingMummy.PresentationState == EnemyPresentationState.Corpse, "death playback ends in persistent corpse state");

simulation.Reset(world.Objective.Position, world.Objective.Lane);
simulation.Update(default, 1f / 60f);
Assert(simulation.ObjectiveCollected, "papyrus collects automatically inside its authored area");

var narrativeSimulation = new WorldSimulation(world, new RurkCombatResolver(2), narrative);
narrativeSimulation.Reset(new Vec2(2200f, 580f), 0);
Assert(narrativeSimulation.OpenDecision("papyrus_silence"), "Core opens an authored decision");
var pausedHero = narrativeSimulation.Hero.Position;
var pausedEnemy = narrativeSimulation.Enemies[0].Position;
var timerBefore = narrativeSimulation.DecisionSecondsRemaining;
Advance(narrativeSimulation, 0.5f, new InputFrame(1f, true, true, true, true, ShootPressed: true));
Assert(narrativeSimulation.IsWorldPaused, "decision keeps the world paused");
Assert(narrativeSimulation.Hero.Position == pausedHero, "decision blocks hero simulation");
Assert(narrativeSimulation.Enemies[0].Position == pausedEnemy, "decision blocks enemy simulation");
Assert(narrativeSimulation.DecisionSecondsRemaining < timerBefore, "decision timer advances independently of the world");

narrativeSimulation.Update(new InputFrame(0f, false, false, false, false, DecisionChoiceNumber: 2), 1f / 60f);
Assert(narrativeSimulation.ActiveDecision is null, "numbered input resolves the active decision");
Assert(narrativeSimulation.Narrative.Flags["papyrus_warning_shots"], "choice applies typed flag consequence");
Assert(narrativeSimulation.Narrative.Relationships["alice_eugene"] == -1, "choice applies typed relationship consequence");
Assert(narrativeSimulation.ActiveDirectedSequenceId == "alice_warning_shots", "choice starts its authored directed sequence");
var directorStart = narrativeSimulation.Hero.Position.X;
var sawDirectedShotEffect = false;
var sawAdvancedDirectedShotEffect = false;
for (var frame = 0; frame < 600 && narrativeSimulation.ActiveDirectedSequenceId is not null; frame++)
{
    narrativeSimulation.Update(new InputFrame(-1f, true, true, true, true), 1f / 60f);
    if (narrativeSimulation.ActivePlayerShotEffect is { } directedShotEffect)
    {
        sawDirectedShotEffect = true;
        sawAdvancedDirectedShotEffect |= directedShotEffect.Progress > 0f;
    }
}
Assert(narrativeSimulation.ActiveDirectedSequenceId is null, "directed sequence returns control after its final beat");
Assert(narrativeSimulation.Hero.Position.X > directorStart + 100f, $"director moves Alice while player input is locked; start {directorStart}, end {narrativeSimulation.Hero.Position.X}");
Assert(narrativeSimulation.Hero.AmmoInMagazine == 6, "director fires two shots through the normal weapon path");
Assert(sawDirectedShotEffect && sawAdvancedDirectedShotEffect, "directed firearm presentation advances instead of freezing on its first frame");
Assert(narrativeSimulation.Hero.ActiveSupportId is not null, "directed movement stays bound to authored support geometry");
narrativeSimulation.Reset(new Vec2(2200f, 580f), 0);
Assert(narrativeSimulation.Narrative.Flags["papyrus_warning_shots"], "level reset preserves campaign narrative consequences");

Assert(narrativeSimulation.OpenDecision("timed_engine_proof"), "Core opens a second decision");
Advance(narrativeSimulation, 0.4f);
Assert(narrativeSimulation.ActiveDecision is null, "timed decision resolves automatically");
Assert(narrativeSimulation.Narrative.Flags["proof_timed_out"], "timeout applies its authored choice consequences");
Assert(narrativeSimulation.OpenDecision("intermission_engine_proof"), "Core opens a full-scene visual-novel decision");
Assert(narrativeSimulation.ActiveDecision?.Presentation == DecisionPresentation.FullScene, "full-scene presentation survives content loading");
narrativeSimulation.Update(new InputFrame(0f, false, false, false, false, DecisionChoiceNumber: 1), 1f / 60f);
Assert(narrativeSimulation.Narrative.Flags["proof_full_scene_resolved"], "full-scene choice uses the same typed consequence path");

Console.WriteLine("[Moad.Engine.Tests] PASS 2.5D, player/enemy combat, projectiles, narrative pause, timed choice, consequences, and directed sequence");
return;

static void Advance(WorldSimulation simulation, float seconds, InputFrame input = default)
{
    const float step = 1f / 60f;
    for (var elapsed = 0f; elapsed < seconds; elapsed += step)
    {
        simulation.Update(input, step);
    }
}

static void AdvanceUntil(WorldSimulation simulation, Func<bool> condition, float timeoutSeconds, string label)
{
    const float step = 1f / 60f;
    for (var elapsed = 0f; elapsed < timeoutSeconds; elapsed += step)
    {
        if (condition())
        {
            return;
        }
        simulation.Update(default, step);
    }
    Assert(condition(), label);
}

static void Assert(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"FAIL: {label}");
    }
}

static void AssertClose(float actual, float expected, float tolerance, string label) =>
    Assert(MathF.Abs(actual - expected) <= tolerance, $"{label}; expected {expected}, got {actual}");

static WorldDefinition WithEnemyHp(WorldDefinition world, int hp) => new()
{
    Id = world.Id,
    Size = world.Size,
    PixelsPerMeter = world.PixelsPerMeter,
    Spawn = world.Spawn,
    SpawnLane = world.SpawnLane,
    Tracks = world.Tracks,
    Supports = world.Supports,
    Transitions = world.Transitions,
    Enemies = world.Enemies.Select(enemy => enemy with { MaxHp = hp }).ToList(),
    CombatObstacles = world.CombatObstacles,
    CoverZones = world.CoverZones,
    Objective = world.Objective,
};

static WorldDefinition WithEnemyArmour(WorldDefinition world, int armourClass) => new()
{
    Id = world.Id,
    Size = world.Size,
    PixelsPerMeter = world.PixelsPerMeter,
    Spawn = world.Spawn,
    SpawnLane = world.SpawnLane,
    Tracks = world.Tracks,
    Supports = world.Supports,
    Transitions = world.Transitions,
    Enemies = world.Enemies.Select(enemy => enemy with { ArmourClass = armourClass }).ToList(),
    CombatObstacles = world.CombatObstacles,
    CoverZones = world.CoverZones,
    Objective = world.Objective,
};

static WorldDefinition WithSoleEnemyPlacement(WorldDefinition world, string enemyId, Vec2 position) => new()
{
    Id = world.Id,
    Size = world.Size,
    PixelsPerMeter = world.PixelsPerMeter,
    Spawn = world.Spawn,
    SpawnLane = world.SpawnLane,
    Tracks = world.Tracks,
    Supports = world.Supports,
    Transitions = world.Transitions,
    Enemies = world.Enemies
        .Where(enemy => enemy.Id == enemyId)
        .Select(enemy => enemy with
        {
            Position = position,
            PatrolLeft = position.X,
            PatrolRight = position.X,
            PatrolSpeed = 0f,
            RangedAttack = enemy.RangedAttack is null
                ? null
                : enemy.RangedAttack with { InitialDelaySeconds = 0.1f },
        })
        .ToList(),
    CombatObstacles = world.CombatObstacles,
    CoverZones = world.CoverZones,
    Objective = world.Objective,
};

static WorldDefinition WithThinBlockedFireballScenario(WorldDefinition world)
{
    var source = world.Enemies.Single(enemy => enemy.Id == "Cultist1");
    return new WorldDefinition
    {
        Id = world.Id,
        Size = world.Size,
        PixelsPerMeter = world.PixelsPerMeter,
        Spawn = world.Spawn,
        SpawnLane = world.SpawnLane,
        Tracks = world.Tracks,
        Supports = world.Supports,
        Transitions = world.Transitions,
        Enemies =
        [
            source with
            {
                Lane = 0,
                Position = new Vec2(3000f, 580f),
                PatrolLeft = 3000f,
                PatrolRight = 3000f,
                PatrolSpeed = 0f,
                RangedAttack = source.RangedAttack! with
                {
                    Kind = EnemyAttackKind.Fireball,
                    RangeMeters = 50f,
                    InitialDelaySeconds = 0.1f,
                    ProjectileSpeed = 360f,
                },
            },
        ],
        CombatObstacles =
        [
            new CombatObstacle(
                "timing_wall",
                new Rect2(2500f, 0f, 1f, 720f),
                0,
                0,
                new HashSet<AttackCollisionKind> { AttackCollisionKind.TravellingMagic },
                "stone",
                "synthetic one-pixel test wall"),
        ],
        CoverZones = [],
        Objective = world.Objective,
    };
}
