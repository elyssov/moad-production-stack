using System.Numerics;
using System.Globalization;
using System.Text.Json;
using Moad.Engine;
using Raylib_cs;

const int ScreenWidth = 1280;
const int ScreenHeight = 720;
const float HeroPresentationScale = 2f / 3f;

var proofMode = args.Contains("--proof", StringComparer.OrdinalIgnoreCase);
var devMode = proofMode || args.Contains("--dev", StringComparer.OrdinalIgnoreCase);
var contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
var levelPath = Path.Combine(contentRoot, "levels", "papyrus_chamber.json");
var world = WorldLoader.Load(levelPath);
var narrativePath = Path.Combine(contentRoot, "narrative", "papyrus_decision_proof.json");
var narrative = NarrativeContentLoader.Load(narrativePath);
var simulation = new WorldSimulation(world, new RurkCombatResolver(proofMode ? 2UL : RurkCombatResolver.DefaultSeed), narrative);

if (proofMode)
{
    Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
}
else
{
    Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
}
Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
Raylib.InitWindow(ScreenWidth, ScreenHeight, "Mystery of Ancient Darkness - MoAD Runtime");
Raylib.SetTargetFPS(60);
Typography.Load(Path.Combine(contentRoot, "fonts"));
NarrativeSlides.Load(contentRoot, narrative);

var backgroundPath = Path.Combine(contentRoot, "backgrounds", "papyrus_archive.png");
var background = Raylib.LoadTexture(backgroundPath);
Raylib.SetTextureFilter(background, TextureFilter.Bilinear);
var occlusionLayers = LoadOcclusionLayers(backgroundPath, world);
var idle = LoadSequence(Path.Combine(contentRoot, "alice", "idle"), 365f);
var walk = LoadSequence(Path.Combine(contentRoot, "alice", "walk"), 365f, removeGreenFringe: true);
var run = LoadSequence(Path.Combine(contentRoot, "alice", "run"), 365f, removeGreenFringe: true);
var jump = LoadSequence(Path.Combine(contentRoot, "alice", "long_jump"), 365f, removeGreenFringe: true);
var shoot = LoadSequence(Path.Combine(contentRoot, "alice", "shoot_luger"), 477f);
var crouch = LoadSequence(Path.Combine(contentRoot, "alice", "crouch"), 199f);
var crouchArmed = LoadSequence(Path.Combine(contentRoot, "alice", "crouch_armed"), 223f);
var balance = LoadSequence(Path.Combine(contentRoot, "alice", "balance"), 255f);
var mummy = LoadSequence(Path.Combine(contentRoot, "enemies", "mummy"), 1f);
var cultist = LoadSequence(Path.Combine(contentRoot, "enemies", "cultist"), 1f);
if (walk.Frames.Count == 0)
{
    throw new InvalidDataException("Alice walk sequence is empty");
}

var debugGeometry = proofMode;
var animationClock = 0f;
var locomotionDistance = 0f;
var previousHeroX = simulation.Hero.Position.X;
var cameraX = ScreenWidth * 0.5f;
var pendingCapture = false;
var captureStem = "manual";
var proofStage = 0;
var selectedPoint = (Surface: (SupportSurface?)null, Index: -1);

if (proofMode)
{
    var inspectionDirectory = Path.Combine(AppContext.BaseDirectory, "inspections");
    if (Directory.Exists(inspectionDirectory))
    {
        foreach (var file in Directory.GetFiles(inspectionDirectory).Where(path =>
                     path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                     || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(file);
        }
    }
    simulation.Reset(new Vec2(2250f, 180f), 2);
}

while (!Raylib.WindowShouldClose())
{
    var delta = proofMode ? 1f / 60f : Raylib.GetFrameTime();
    var horizontal = Axis(KeyboardKey.A, KeyboardKey.D, KeyboardKey.Left, KeyboardKey.Right);
    InputFrame input = new(
        horizontal,
        Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift),
        Raylib.IsKeyPressed(KeyboardKey.Space),
        Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressed(KeyboardKey.Up),
        Raylib.IsKeyPressed(KeyboardKey.S) || Raylib.IsKeyPressed(KeyboardKey.Down),
        Raylib.IsKeyPressed(KeyboardKey.J) || Raylib.IsKeyPressed(KeyboardKey.Enter),
        Raylib.IsKeyPressed(KeyboardKey.Q),
        Raylib.IsKeyPressed(KeyboardKey.R),
        ReadDecisionChoice(simulation),
        Raylib.IsKeyPressed(KeyboardKey.C) || Raylib.IsKeyPressed(KeyboardKey.LeftControl));
    if (proofMode && proofStage is >= 67 and <= 107)
    {
        input = new InputFrame(1f, false, false, false, false);
    }
    simulation.Update(input, delta);
    animationClock += delta;
    var travelled = MathF.Abs(simulation.Hero.Position.X - previousHeroX);
    if (travelled < 120f && simulation.Hero.State is HeroState.Move or HeroState.Crouch)
    {
        locomotionDistance += travelled;
    }
    previousHeroX = simulation.Hero.Position.X;

    if (devMode && Raylib.IsKeyPressed(KeyboardKey.F1))
    {
        debugGeometry = !debugGeometry;
    }
    if (devMode && Raylib.IsKeyPressed(KeyboardKey.F8))
    {
        pendingCapture = true;
        captureStem = $"inspection_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
    }
    if (Raylib.IsKeyPressed(KeyboardKey.F2))
    {
        simulation.OpenDecision("papyrus_silence");
    }
    if (devMode && Raylib.IsKeyPressed(KeyboardKey.F3))
    {
        simulation.OpenDecision("intermission_engine_proof");
    }

    cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
    if (debugGeometry)
    {
        UpdateGeometryEditor(world, cameraX, ref selectedPoint);
        if (Raylib.IsKeyPressed(KeyboardKey.F5))
        {
            SaveGeometryOverride(world);
        }
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    DrawBackground(background, cameraX);
    if (debugGeometry)
    {
        DrawGeometry(world, simulation, cameraX, selectedPoint);
    }
    DrawObjective(world, simulation, cameraX);
    foreach (var track in world.Tracks.OrderBy(track => track.ZIndex))
    {
        DrawEnemies(world, simulation, mummy, cultist, animationClock, cameraX, track.Lane);
        DrawEnemyProjectiles(world, simulation, cameraX, track.Lane);
        if (simulation.Hero.Lane == track.Lane)
        {
            DrawHero(world, simulation, idle, walk, run, jump, shoot, crouch, crouchArmed, balance, animationClock, locomotionDistance, cameraX);
        }
        DrawOcclusionLayer(occlusionLayers, cameraX, track.Lane);
        DrawCombatOccluders(background, world, cameraX, track.Lane);
        DrawPlayerShotEffect(world, simulation, cameraX, track.Lane);
        DrawWorldImpactEffects(world, simulation, cameraX, track.Lane);
    }
    DrawReloadProgress(simulation, cameraX);
    DrawHeroHitFeedback(simulation);
    DrawHud(simulation, debugGeometry);
    DrawDirectedSequence(simulation);
    DrawDecisionOverlay(simulation);
    Raylib.EndDrawing();

    if (pendingCapture)
    {
        WriteInspection(captureStem, world, simulation, cameraX, debugGeometry);
        pendingCapture = false;
    }

    if (proofMode)
    {
        proofStage++;
        if (proofStage == 3)
        {
            WriteInspection("01_support_binding", world, simulation, cameraX, debugGeometry);
            simulation.Reset(new Vec2(2880f, 390f), 1);
            simulation.BeginTransition("stairs_right_middle_to_far");
            for (var index = 0; index < 45; index++)
            {
                simulation.Update(default, 1f / 60f);
            }
        }
        else if (proofStage == 6)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("02_depth_transition", world, simulation, cameraX, debugGeometry);
            debugGeometry = false;
        }
        else if (proofStage == 9)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("03_clean_runtime", world, simulation, cameraX, debugGeometry);
            simulation.Reset(new Vec2(2200f, 580f), 0);
            simulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
            for (var index = 0; index < 40; index++)
            {
                simulation.Update(default, 1f / 60f);
            }
            simulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
        }
        else if (proofStage == 12)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("04_combat_runtime", world, simulation, cameraX, debugGeometry);
            simulation.Reset(new Vec2(2200f, 580f), 0);
            for (var index = 0; index < 180; index++)
            {
                simulation.Update(default, 1f / 60f);
            }
            simulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
            for (var index = 0; index < 40; index++)
            {
                simulation.Update(default, 1f / 60f);
            }
            simulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
        }
        else if (proofStage == 15)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("05_blocked_shot", world, simulation, cameraX, debugGeometry);
            simulation.Reset(new Vec2(2200f, 580f), 0);
            simulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
            for (var index = 0; index < 95; index++)
            {
                simulation.Update(default, 1f / 60f);
            }
        }
        else if (proofStage == 18)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("06_enemy_projectile", world, simulation, cameraX, debugGeometry);
            for (var index = 0; index < 8; index++)
            {
                simulation.Update(default, 1f / 60f);
            }
        }
        else if (proofStage == 21)
        {
            WriteInspection("07_alice_stone_hit", world, simulation, cameraX, debugGeometry);
            simulation.Reset(new Vec2(3700f, 270f), 2);
            simulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
            for (var index = 0; index < 210; index++)
            {
                simulation.Update(default, 1f / 60f);
            }
        }
        else if (proofStage == 24)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("08_cult_fireball", world, simulation, cameraX, debugGeometry);
            for (var index = 0; index < 105; index++)
            {
                simulation.Update(default, 1f / 60f);
            }
        }
        else if (proofStage == 27)
        {
            WriteInspection("09_alice_fire_hit", world, simulation, cameraX, debugGeometry);
            simulation.OpenDecision("papyrus_silence");
        }
        else if (proofStage == 30)
        {
            WriteInspection("10_decision_overlay", world, simulation, cameraX, debugGeometry);
            simulation.Update(new InputFrame(0f, false, false, false, false, DecisionChoiceNumber: 2), 1f / 60f);
            for (var index = 0; index < 80; index++)
            {
                simulation.Update(default, 1f / 60f);
            }
        }
        else if (proofStage == 33)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("11_directed_sequence", world, simulation, cameraX, debugGeometry);
            for (var index = 0; index < 600 && simulation.ActiveDirectedSequenceId is not null; index++)
            {
                simulation.Update(default, 1f / 60f);
            }
        }
        else if (proofStage == 35)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("12_directed_complete", world, simulation, cameraX, debugGeometry);
            simulation.Reset(new Vec2(1200f, 390f), 1);
            simulation.Update(new InputFrame(0f, false, false, false, false, ShootPressed: true), 1f / 60f);
        }
        else if (proofStage == 70)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("13_armed_walk", world, simulation, cameraX, debugGeometry);
            simulation.Update(new InputFrame(0f, false, false, false, false, CrouchPressed: true), 1f / 60f);
        }
        else if (proofStage == 72)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("14_armed_crouch", world, simulation, cameraX, debugGeometry);
            simulation.Reset(new Vec2(1600f, 390f), 1);
            locomotionDistance = 0f;
            previousHeroX = simulation.Hero.Position.X;
        }
        else if (proofStage == 108)
        {
            cameraX = Math.Clamp(simulation.Hero.Position.X, ScreenWidth * 0.5f, world.Size.X - ScreenWidth * 0.5f);
            WriteInspection("15_bridge_balance", world, simulation, cameraX, debugGeometry);
            break;
        }
    }
}

foreach (var texture in idle.Frames
             .Concat(walk.Frames)
             .Concat(run.Frames)
             .Concat(jump.Frames)
             .Concat(shoot.Frames)
             .Concat(crouch.Frames)
             .Concat(crouchArmed.Frames)
             .Concat(balance.Frames)
             .Concat(mummy.Frames)
             .Concat(cultist.Frames)
             .Select(frame => frame.Texture))
{
    Raylib.UnloadTexture(texture);
}
Raylib.UnloadTexture(background);
UnloadOcclusionLayers(occlusionLayers);
NarrativeSlides.Unload();
Typography.Unload();
Raylib.CloseWindow();

static unsafe SpriteClip LoadSequence(string directory, float referenceBodyHeight, bool removeGreenFringe = false)
{
    var frames = new List<SpriteFrame>();
    foreach (var path in Directory.GetFiles(directory, "*.png")
                 .OrderBy(path => NumericSuffix(Path.GetFileNameWithoutExtension(path))))
    {
        var image = Raylib.LoadImage(path);
        Color* pixels = null;
        if (removeGreenFringe)
        {
            pixels = Raylib.LoadImageColors(image);
            for (var index = 0; index < image.Width * image.Height; index++)
            {
                var color = pixels[index];
                var strongestOther = Math.Max(color.R, color.B);
                if (color.A > 0 && color.G > 85 && color.G > strongestOther * 1.55f)
                {
                    color.A = 0;
                }
                else if (color.A > 0 && color.G > strongestOther * 1.22f && color.G - strongestOther > 18)
                {
                    color.G = (byte)((color.R + color.B) / 2);
                }
                pixels[index] = color;
            }
        }
        var alphaBounds = Raylib.GetImageAlphaBorder(image, 0.03f);
        if (alphaBounds.Width <= 0f || alphaBounds.Height <= 0f)
        {
            if (pixels != null)
            {
                Raylib.UnloadImageColors(pixels);
            }
            Raylib.UnloadImage(image);
            continue;
        }
        var texture = Raylib.LoadTextureFromImage(image);
        if (pixels != null)
        {
            Raylib.UpdateTexture(texture, pixels);
            Raylib.UnloadImageColors(pixels);
        }
        Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
        Raylib.UnloadImage(image);
        frames.Add(new SpriteFrame(texture, alphaBounds));
    }
    return new SpriteClip(frames, referenceBodyHeight);
}

static int NumericSuffix(string name)
{
    var digits = new string(name.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
    return int.TryParse(digits, out var value) ? value : int.MaxValue;
}

static float Axis(KeyboardKey negativeA, KeyboardKey positiveA, KeyboardKey negativeB, KeyboardKey positiveB)
{
    var negative = Raylib.IsKeyDown(negativeA) || Raylib.IsKeyDown(negativeB);
    var positive = Raylib.IsKeyDown(positiveA) || Raylib.IsKeyDown(positiveB);
    return (positive ? 1f : 0f) - (negative ? 1f : 0f);
}

static void DrawBackground(Texture2D texture, float cameraX)
{
    var source = new Rectangle(cameraX - ScreenWidth * 0.5f, 0f, ScreenWidth, ScreenHeight);
    var destination = new Rectangle(0f, 0f, ScreenWidth, ScreenHeight);
    Raylib.DrawTexturePro(texture, source, destination, Vector2.Zero, 0f, Color.White);
}

static void DrawCombatOccluders(Texture2D background, WorldDefinition world, float cameraX, int lane)
{
    var left = cameraX - ScreenWidth * 0.5f;
    foreach (var obstacle in world.CombatObstacles.Where(item =>
                 lane >= Math.Min(item.FrontLane, item.BackLane)
                 && lane <= Math.Max(item.FrontLane, item.BackLane)))
    {
        var bounds = obstacle.Bounds;
        var source = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var destination = new Rectangle(bounds.X - left, bounds.Y, bounds.Width, bounds.Height);
        Raylib.DrawTexturePro(background, source, destination, Vector2.Zero, 0f, Color.White);
    }
}

static List<DepthOcclusionLayer> LoadOcclusionLayers(string backgroundPath, WorldDefinition world)
{
    if (world.AuthoredOccluders.Count == 0)
    {
        return [];
    }

    var backgroundImage = Raylib.LoadImage(backgroundPath);
    var layers = new List<DepthOcclusionLayer>();
    try
    {
        foreach (var group in world.AuthoredOccluders.GroupBy(item => item.Lane))
        {
            var mask = Raylib.GenImageColor(backgroundImage.Width, backgroundImage.Height, Color.Black);
            try
            {
                foreach (var occluder in group)
                {
                    var intensity = (byte)Math.Clamp(MathF.Round(occluder.Opacity * 255f), 0f, 255f);
                    var color = new Color(intensity, intensity, intensity, (byte)255);
                    var triangles = TriangulatePolygon(occluder.Polygon);
                    for (var index = 0; index + 2 < triangles.Count; index += 3)
                    {
                        Raylib.ImageDrawTriangle(
                            ref mask,
                            ToVector(triangles[index]),
                            ToVector(triangles[index + 1]),
                            ToVector(triangles[index + 2]),
                            color);
                    }
                }

                var layerImage = Raylib.ImageCopy(backgroundImage);
                try
                {
                    Raylib.ImageAlphaMask(ref layerImage, mask);
                    var texture = Raylib.LoadTextureFromImage(layerImage);
                    Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
                    layers.Add(new DepthOcclusionLayer(group.Key, texture));
                }
                finally
                {
                    Raylib.UnloadImage(layerImage);
                }
            }
            finally
            {
                Raylib.UnloadImage(mask);
            }
        }
    }
    finally
    {
        Raylib.UnloadImage(backgroundImage);
    }
    return layers;
}

static void DrawOcclusionLayer(IReadOnlyList<DepthOcclusionLayer> layers, float cameraX, int lane)
{
    var layer = layers.FirstOrDefault(item => item.Lane == lane);
    if (layer is null)
    {
        return;
    }
    DrawBackground(layer.Texture, cameraX);
}

static void UnloadOcclusionLayers(IEnumerable<DepthOcclusionLayer> layers)
{
    foreach (var layer in layers)
    {
        Raylib.UnloadTexture(layer.Texture);
    }
}

static Vector2 ToVector(Vec2 point) => new(point.X, point.Y);

static List<Vec2> TriangulatePolygon(IReadOnlyList<Vec2> polygon)
{
    if (polygon.Count < 3)
    {
        return [];
    }

    var indices = Enumerable.Range(0, polygon.Count).ToList();
    if (SignedArea(polygon) < 0f)
    {
        indices.Reverse();
    }

    var triangles = new List<Vec2>((polygon.Count - 2) * 3);
    var guard = polygon.Count * polygon.Count;
    while (indices.Count > 3 && guard-- > 0)
    {
        var clipped = false;
        for (var index = 0; index < indices.Count; index++)
        {
            var previous = indices[(index - 1 + indices.Count) % indices.Count];
            var current = indices[index];
            var next = indices[(index + 1) % indices.Count];
            var a = polygon[previous];
            var b = polygon[current];
            var c = polygon[next];
            if (Cross(a, b, c) <= 0.001f)
            {
                continue;
            }
            if (indices.Any(candidate => candidate != previous && candidate != current && candidate != next
                    && PointInTriangle(polygon[candidate], a, b, c)))
            {
                continue;
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            indices.RemoveAt(index);
            clipped = true;
            break;
        }
        if (!clipped)
        {
            return [];
        }
    }

    if (indices.Count == 3)
    {
        triangles.Add(polygon[indices[0]]);
        triangles.Add(polygon[indices[1]]);
        triangles.Add(polygon[indices[2]]);
    }
    return triangles;
}

static float SignedArea(IReadOnlyList<Vec2> polygon)
{
    var area = 0f;
    for (var index = 0; index < polygon.Count; index++)
    {
        var next = polygon[(index + 1) % polygon.Count];
        area += polygon[index].X * next.Y - next.X * polygon[index].Y;
    }
    return area * 0.5f;
}

static float Cross(Vec2 a, Vec2 b, Vec2 c) =>
    (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

static bool PointInTriangle(Vec2 point, Vec2 a, Vec2 b, Vec2 c)
{
    var ab = Cross(a, b, point);
    var bc = Cross(b, c, point);
    var ca = Cross(c, a, point);
    return ab >= -0.001f && bc >= -0.001f && ca >= -0.001f;
}

static void DrawHero(
    WorldDefinition world,
    WorldSimulation simulation,
    SpriteClip idle,
    SpriteClip walk,
    SpriteClip run,
    SpriteClip jump,
    SpriteClip shoot,
    SpriteClip crouch,
    SpriteClip crouchArmed,
    SpriteClip balance,
    float clock,
    float locomotionDistance,
    float cameraX)
{
    SpriteClip sequence;
    float fps;
    var strideDistance = 0f;
    float? actionProgress = null;
    var moving = MathF.Abs(simulation.Hero.Velocity.X) > 1f;
    var balancing = simulation.Hero.BalanceRequired && simulation.Hero.Grounded && moving;
    if (simulation.Hero.State is HeroState.DrawWeapon or HeroState.Shoot && shoot.Frames.Count > 0)
    {
        sequence = shoot;
        fps = 0f;
        actionProgress = simulation.Hero.WeaponActionDuration <= 0f
            ? 1f
            : 1f - simulation.Hero.WeaponActionRemaining / simulation.Hero.WeaponActionDuration;
    }
    else if (simulation.Hero.State == HeroState.Crouch)
    {
        sequence = simulation.Hero.PistolReady && crouchArmed.Frames.Count > 0 ? crouchArmed : crouch;
        fps = 0f;
    }
    else if (simulation.Hero.State == HeroState.Airborne && jump.Frames.Count > 0)
    {
        sequence = jump;
        fps = 10f;
    }
    else if (balancing && balance.Frames.Count > 0)
    {
        sequence = balance;
        fps = 0f;
        strideDistance = 72f;
    }
    else if (simulation.Hero.State == HeroState.DepthTransition || MathF.Abs(simulation.Hero.Velocity.X) > 160f)
    {
        sequence = run.Frames.Count > 0 ? run : walk;
        fps = 12f;
        strideDistance = 180f;
    }
    else if (simulation.Hero.PistolReady && !moving && shoot.Frames.Count > 0)
    {
        sequence = shoot;
        fps = 0f;
        actionProgress = 1f;
    }
    else if (!moving && idle.Frames.Count > 0)
    {
        sequence = idle;
        fps = 0f;
    }
    else
    {
        sequence = walk;
        fps = 0f;
        strideDistance = balancing ? 105f : 145f;
    }

    var frameIndex = actionProgress is not null
        ? Math.Clamp((int)(actionProgress.Value * (sequence.Frames.Count - 1)), 0, sequence.Frames.Count - 1)
        : strideDistance > 0f && moving
            ? (int)(locomotionDistance / strideDistance * sequence.Frames.Count) % sequence.Frames.Count
            : fps <= 0f ? 0 : (int)(clock * fps) % sequence.Frames.Count;
    if (simulation.Hero.State == HeroState.Airborne)
    {
        var vertical = simulation.Hero.Velocity.Y;
        var phase = vertical < 0f
            ? Math.Clamp(1f - MathF.Abs(vertical) / 500f, 0f, 1f) * 0.5f
            : 0.5f + Math.Clamp(vertical / 760f, 0f, 1f) * 0.5f;
        frameIndex = Math.Clamp((int)(phase * sequence.Frames.Count), 0, sequence.Frames.Count - 1);
    }

    var frame = sequence.Frames[frameIndex];
    var physicalHeight = world.PixelsPerMeter * HeroModel.StandingHeightMeters * HeroPresentationScale;
    if (balancing)
    {
        physicalHeight *= 0.96f;
    }
    var scale = physicalHeight / sequence.ReferenceBodyHeight * simulation.Hero.DisplayScale;
    var width = frame.Texture.Width * scale;
    var height = frame.Texture.Height * scale;
    var screenX = simulation.Hero.Position.X - (cameraX - ScreenWidth * 0.5f);
    var footY = simulation.Hero.Position.Y + 1f;
    var source = simulation.Hero.Facing > 0
        ? new Rectangle(0f, 0f, frame.Texture.Width, frame.Texture.Height)
        : new Rectangle(frame.Texture.Width, 0f, -frame.Texture.Width, frame.Texture.Height);
    var destination = new Rectangle(screenX, footY, width, height);
    var origin = new Vector2(width * 0.5f, (frame.AlphaBounds.Y + frame.AlphaBounds.Height) * scale);
    var rotation = balancing ? MathF.Sin(locomotionDistance / 17f) * 2.5f : 0f;
    Raylib.DrawTexturePro(frame.Texture, source, destination, origin, rotation, Color.White);

    if (simulation.Hero.PistolReady
        && simulation.Hero.State == HeroState.Move
        && moving
        && shoot.Frames.Count >= 4)
    {
        DrawWalkingPistol(simulation, shoot, screenX, footY, physicalHeight, HeroPresentationScale);
    }
}

static void DrawWalkingPistol(
    WorldSimulation simulation,
    SpriteClip shoot,
    float screenX,
    float footY,
    float physicalHeight,
    float presentationScale)
{
    var frame = shoot.Frames[3];
    var source = simulation.Hero.Facing > 0
        ? new Rectangle(314f, 82f, 86f, 52f)
        : new Rectangle(400f, 82f, -86f, 52f);
    var scale = physicalHeight / shoot.ReferenceBodyHeight * simulation.Hero.DisplayScale;
    var width = 86f * scale;
    var height = 52f * scale;
    var center = new Vector2(
        screenX + simulation.Hero.Facing * 36f * simulation.Hero.DisplayScale * presentationScale,
        footY - 73f * simulation.Hero.DisplayScale * presentationScale);
    var destination = new Rectangle(center.X, center.Y, width, height);
    Raylib.DrawTexturePro(
        frame.Texture,
        source,
        destination,
        new Vector2(width * 0.5f, height * 0.5f),
        simulation.Hero.Facing * 12f,
        Color.White);
}

static void DrawObjective(WorldDefinition world, WorldSimulation simulation, float cameraX)
{
    if (simulation.ObjectiveCollected)
    {
        return;
    }
    var x = world.Objective.Position.X - (cameraX - ScreenWidth * 0.5f);
    var y = world.Objective.Position.Y - 26f;
    Raylib.DrawRectangleRounded(new Rectangle(x - 16f, y - 9f, 32f, 18f), 0.18f, 3, new Color(207, 166, 70, 255));
    Raylib.DrawLineEx(new Vector2(x - 12f, y - 4f), new Vector2(x + 10f, y - 4f), 2f, new Color(84, 51, 24, 255));
    Raylib.DrawLineEx(new Vector2(x - 10f, y + 2f), new Vector2(x + 8f, y + 2f), 2f, new Color(84, 51, 24, 255));
    Raylib.DrawCircleV(new Vector2(x - 16f, y), 4f, new Color(242, 206, 105, 255));
    Raylib.DrawCircleV(new Vector2(x + 16f, y), 4f, new Color(242, 206, 105, 255));
}

static void DrawEnemies(
    WorldDefinition world,
    WorldSimulation simulation,
    SpriteClip mummy,
    SpriteClip cultist,
    float clock,
    float cameraX,
    int lane)
{
    foreach (var enemy in simulation.Enemies.Where(enemy => enemy.Definition.Lane == lane))
    {
        var sequence = enemy.Definition.Id == "Zombie1" ? mummy : cultist;
        if (sequence.Frames.Count == 0)
        {
            continue;
        }
        var idleCount = Math.Min(enemy.Definition.Id == "Zombie1" ? 3 : 6, sequence.Frames.Count);
        var frameIndex = enemy.PresentationState switch
        {
            EnemyPresentationState.HitLight => Math.Min(5, sequence.Frames.Count - 1),
            EnemyPresentationState.HitHeavy => Math.Min(5, sequence.Frames.Count - 1),
            EnemyPresentationState.Dying => Math.Clamp(
                6 + (int)(enemy.PresentationProgress * Math.Max(1, sequence.Frames.Count - 6)),
                Math.Min(6, sequence.Frames.Count - 1),
                sequence.Frames.Count - 1),
            EnemyPresentationState.Corpse => sequence.Frames.Count - 1,
            _ => (int)(clock * 5f) % idleCount,
        };
        var frame = sequence.Frames[frameIndex];
        var depthScale = world.ScaleFor(enemy.Definition.Lane);
        var height = enemy.Definition.VisualHeightMeters * world.PixelsPerMeter * depthScale;
        var width = frame.AlphaBounds.Width / frame.AlphaBounds.Height * height;
        var screenX = enemy.Position.X - (cameraX - ScreenWidth * 0.5f);
        var source = enemy.Facing > 0
            ? frame.AlphaBounds
            : new Rectangle(
                frame.AlphaBounds.X + frame.AlphaBounds.Width,
                frame.AlphaBounds.Y,
                -frame.AlphaBounds.Width,
                frame.AlphaBounds.Height);
        var destination = new Rectangle(screenX - width * 0.5f, enemy.Position.Y - height, width, height);
        Raylib.DrawTexturePro(frame.Texture, source, destination, Vector2.Zero, 0f, Color.White);
        if (enemy.Definition.Id == simulation.SelectedTargetId && enemy.IsAlive)
        {
            var radius = MathF.Max(25f, height * 0.44f);
            var center = new Vector2(screenX, enemy.Position.Y - height * 0.48f);
            Raylib.DrawCircleLinesV(center, radius, new Color(255, 213, 72, 255));
            Raylib.DrawLineEx(center + new Vector2(-radius - 9f, 0f), center + new Vector2(-radius + 5f, 0f), 2f, new Color(255, 213, 72, 255));
            Raylib.DrawLineEx(center + new Vector2(radius - 5f, 0f), center + new Vector2(radius + 9f, 0f), 2f, new Color(255, 213, 72, 255));
        }
    }
}

static void DrawEnemyProjectiles(WorldDefinition world, WorldSimulation simulation, float cameraX, int lane)
{
    var left = cameraX - ScreenWidth * 0.5f;
    foreach (var projectile in simulation.EnemyProjectiles.Where(item => ProjectileLane(item) == lane))
    {
        var position = ToScreen(projectile.Position, left);
        var scale = world.ScaleFor(projectile.SourceLane)
            + (world.ScaleFor(projectile.TargetLane) - world.ScaleFor(projectile.SourceLane)) * projectile.PathProgress;
        if (projectile.Kind == EnemyAttackKind.Fireball)
        {
            var previous = ToScreen(projectile.PositionAtPathProgress(MathF.Max(0f, projectile.PathProgress - 0.08f)), left);
            var flight = position - previous;
            var direction = flight.LengthSquared() > 0.001f ? Vector2.Normalize(flight) : Vector2.UnitX;
            for (var index = 3; index >= 1; index--)
            {
                var trail = position - direction * index * 9f * scale;
                Raylib.DrawCircleV(trail, (7f - index) * scale, new Color((byte)238, (byte)88, (byte)25, (byte)(65 + index * 35)));
            }
            Raylib.DrawCircleV(position, 13f * scale, new Color(232, 56, 18, 220));
            Raylib.DrawCircleV(position, 8f * scale, new Color(255, 154, 35, 255));
            Raylib.DrawCircleV(position, 3.5f * scale, new Color(255, 242, 174, 255));
        }
        else
        {
            var previous = ToScreen(projectile.PositionAtPathProgress(MathF.Max(0f, projectile.PathProgress - 0.08f)), left);
            var flight = position - previous;
            var direction = flight.LengthSquared() > 0.001f ? Vector2.Normalize(flight) : Vector2.UnitX;
            for (var index = 1; index <= 3; index++)
            {
                Raylib.DrawCircleV(position - direction * index * 8f * scale, (4f - index) * scale, new Color(177, 151, 115, 110));
            }
            Raylib.DrawCircleV(position + new Vector2(2f, 3f) * scale, 11f * scale, new Color(31, 20, 14, 145));
            Raylib.DrawCircleV(position, 9f * scale, new Color(109, 88, 65, 255));
            Raylib.DrawCircleV(position + new Vector2(-2f, -2f) * scale, 2f * scale, new Color(179, 153, 116, 255));
        }
    }
}

static int ProjectileLane(EnemyProjectileModel projectile) =>
    (int)MathF.Round(projectile.CurrentDepth);

static void DrawPlayerShotEffect(WorldDefinition world, WorldSimulation simulation, float cameraX, int lane)
{
    var effect = simulation.ActivePlayerShotEffect;
    if (effect is null)
    {
        return;
    }
    var left = cameraX - ScreenWidth * 0.5f;
    if (effect.OriginLane == lane && effect.Progress <= 0.45f)
    {
        var scale = world.ScaleFor(effect.OriginLane);
        var origin = ToScreen(effect.Origin, left);
        Raylib.DrawCircleV(origin, 7f * scale, new Color(255, 174, 62, 225));
        Raylib.DrawCircleV(origin, 3f * scale, new Color(255, 245, 195, 255));
        Raylib.DrawCircleV(origin + new Vector2(simulation.Hero.Facing * 7f, 0f), 3f * scale, new Color(238, 90, 24, 210));
    }
    if (effect.TargetLane != lane || effect.ImpactKind == FirearmImpactKind.None || effect.Progress < 0.15f)
    {
        return;
    }

    var targetScale = world.ScaleFor(effect.TargetLane);
    var target = ToScreen(effect.Target, left);
    var spread = 7f + effect.Progress * 18f;
    var dustColor = effect.ImpactKind is FirearmImpactKind.MummyDust or FirearmImpactKind.StoneObstacle
        ? new Color((byte)190, (byte)161, (byte)113, (byte)(220 * (1f - effect.Progress)))
        : new Color((byte)136, (byte)42, (byte)29, (byte)(210 * (1f - effect.Progress)));
    var offsets = new[]
    {
        new Vector2(-0.8f, -0.2f),
        new Vector2(-0.3f, -0.9f),
        new Vector2(0.2f, -0.6f),
        new Vector2(0.7f, -0.15f),
        new Vector2(0.45f, 0.35f),
    };
    foreach (var offset in offsets)
    {
        Raylib.DrawCircleV(target + offset * spread * targetScale, (4f + effect.Progress * 3f) * targetScale, dustColor);
    }
}

static void DrawWorldImpactEffects(WorldDefinition world, WorldSimulation simulation, float cameraX, int lane)
{
    var left = cameraX - ScreenWidth * 0.5f;
    foreach (var effect in simulation.WorldImpactEffects.Where(item => item.Lane == lane))
    {
        var center = ToScreen(effect.Position, left);
        var scale = world.ScaleFor(lane);
        var fade = 1f - effect.Progress;
        if (effect.Kind == WorldImpactKind.FireBurst)
        {
            Raylib.DrawCircleV(center, (10f + effect.Progress * 28f) * scale, new Color((byte)232, (byte)64, (byte)18, (byte)(190 * fade)));
            Raylib.DrawCircleLinesV(center, (16f + effect.Progress * 38f) * scale, new Color((byte)255, (byte)190, (byte)65, (byte)(220 * fade)));
            continue;
        }
        var offsets = new[]
        {
            new Vector2(-0.8f, -0.3f),
            new Vector2(-0.2f, -0.9f),
            new Vector2(0.4f, -0.6f),
            new Vector2(0.9f, -0.1f),
        };
        foreach (var offset in offsets)
        {
            Raylib.DrawCircleV(
                center + offset * (8f + effect.Progress * 24f) * scale,
                (5f + effect.Progress * 3f) * scale,
                new Color((byte)178, (byte)151, (byte)112, (byte)(200 * fade)));
        }
    }
}

static void DrawGeometry(
    WorldDefinition world,
    WorldSimulation simulation,
    float cameraX,
    (SupportSurface? Surface, int Index) selected)
{
    var left = cameraX - ScreenWidth * 0.5f;
    foreach (var surface in world.Supports)
    {
        var color = surface.Lane switch
        {
            0 => new Color(53, 220, 110, 230),
            1 => new Color(255, 194, 61, 230),
            _ => new Color(70, 174, 255, 230),
        };
        var isActive = surface.Id == simulation.Hero.ActiveSupportId;
        if (surface.Lane != simulation.Hero.Lane)
        {
            color = new Color(color.R, color.G, color.B, (byte)85);
        }
        if (isActive)
        {
            color = Color.White;
        }
        for (var index = 0; index < surface.Points.Count - 1; index++)
        {
            Raylib.DrawLineEx(ToScreen(surface.Points[index], left), ToScreen(surface.Points[index + 1], left), isActive ? 5f : 3f, color);
        }
        for (var index = 0; index < surface.Points.Count; index++)
        {
            var pointColor = selected.Surface == surface && selected.Index == index ? Color.White : color;
            Raylib.DrawCircleV(ToScreen(surface.Points[index], left), 5f, pointColor);
        }
    }

    foreach (var transition in world.Transitions)
    {
        for (var index = 0; index < transition.Path.Count - 1; index++)
        {
            Raylib.DrawLineEx(ToScreen(transition.Path[index], left), ToScreen(transition.Path[index + 1], left), 2f, new Color(230, 73, 210, 210));
        }
    }

    foreach (var obstacle in world.CombatObstacles)
    {
        var bounds = obstacle.Bounds;
        var rectangle = new Rectangle(bounds.X - left, bounds.Y, bounds.Width, bounds.Height);
        Raylib.DrawRectangleLinesEx(rectangle, 3f, new Color(245, 67, 54, 235));
        DrawBody($"BLOCK {obstacle.Id}", (int)rectangle.X + 4, (int)rectangle.Y + 4, 12f, Color.White);
    }
    foreach (var zone in world.CoverZones)
    {
        var bounds = zone.Bounds;
        var rectangle = new Rectangle(bounds.X - left, bounds.Y, bounds.Width, bounds.Height);
        Raylib.DrawRectangleLinesEx(rectangle, 2f, new Color(72, 232, 111, 220));
        DrawBody($"COVER {zone.CoverLevel}", (int)rectangle.X + 4, (int)rectangle.Y + 20, 12f, new Color(72, 232, 111, 255));
    }

    var foot = ToScreen(simulation.Hero.Position, left);
    Raylib.DrawCircleV(foot, 7f, Color.White);
    Raylib.DrawLineEx(foot + new Vector2(-12f, 0f), foot + new Vector2(12f, 0f), 2f, Color.White);
}

static void DrawReloadProgress(WorldSimulation simulation, float cameraX)
{
    if (simulation.Hero.State != HeroState.Reload)
    {
        return;
    }
    var progress = simulation.Hero.ReloadProgress;
    var screenX = simulation.Hero.Position.X - (cameraX - ScreenWidth * 0.5f);
    var center = new Vector2(screenX, simulation.Hero.Position.Y - 126f * simulation.Hero.DisplayScale);
    var red = new Color(230, 55, 43, 255);
    var green = new Color(72, 232, 111, 255);
    var fill = new Color(
        (byte)(red.R + (green.R - red.R) * progress),
        (byte)(red.G + (green.G - red.G) * progress),
        (byte)(red.B + (green.B - red.B) * progress),
        (byte)255);
    Raylib.DrawRing(center, 15f, 21f, 0f, 360f, 48, new Color(40, 12, 10, 220));
    Raylib.DrawRing(center, 15f, 21f, -90f, -90f + 360f * progress, 48, fill);
    Raylib.DrawCircleV(center, 10f, new Color(0, 0, 0, 205));
}

static void DrawHud(WorldSimulation simulation, bool debug)
{
    Raylib.DrawRectangle(18, 18, 455, 52, new Color(0, 0, 0, 210));
    DrawDisplay(
        simulation.ObjectiveCollected ? "PAPYRUS RECOVERED  /  CHAMBER SECURED" : "PAPYRUS CHAMBER  /  RECOVER THE SCROLL",
        34, 27, 25, new Color(230, 196, 83, 255));
    Raylib.DrawRectangle(18, 622, 310, 78, new Color(0, 0, 0, 205));
    DrawDisplay("LADY ALICE", 32, 631, 25, new Color(218, 226, 213, 255));
    var hpColor = simulation.Hero.Hp <= 3 ? new Color(244, 76, 62, 255) : new Color(84, 255, 144, 255);
    DrawBody($"HP {simulation.Hero.Hp:00} / {HeroModel.MaximumHp:00}", 32, 668, 20, hpColor);
    Raylib.DrawRectangle(940, 622, 322, 78, new Color(0, 0, 0, 205));
    DrawDisplay("GUARDIAN LUGER  /  ZPN 71", 958, 632, 22, new Color(230, 196, 83, 255));
    DrawBody($"{simulation.Hero.AmmoInMagazine} / {simulation.Hero.ReserveAmmo}", 958, 668, 21, Color.White);
    Raylib.DrawRectangle(340, 650, 580, 50, new Color(0, 0, 0, 205));
    var hudMessage = simulation.ActiveDirectedSequenceId is not null
        && !string.IsNullOrWhiteSpace(simulation.LastNarrativeMessage)
            ? simulation.LastNarrativeMessage
            : simulation.LastCombatMessage;
    DrawBodyFit(hudMessage, 358, 663, 544, 17f, 12f, new Color(218, 226, 213, 255));
    if (simulation.Hero.ReloadPhase >= 0)
    {
        DrawReloadPanels(simulation.Hero.ReloadPhase, simulation.Hero.WeaponCondition == WeaponState.ClearingJam);
    }
    if (!debug)
    {
        return;
    }
    Raylib.DrawRectangle(820, 82, 440, 294, new Color(0, 0, 0, 220));
    DrawDisplay("MOAD WORLD / RURK INSPECTOR", 840, 92, 24, new Color(230, 196, 83, 255));
    DrawBody($"DEPTH {simulation.Hero.Lane}   SCALE {simulation.Hero.DisplayScale:0.00}", 840, 130, 16, Color.White);
    DrawBody($"FOOT {simulation.Hero.Position.X:0.0}, {simulation.Hero.Position.Y:0.0}", 840, 156, 16, Color.White);
    DrawBody($"SUPPORT {simulation.Hero.ActiveSupportId ?? "NONE"}", 840, 182, 16, Color.White);
    DrawBody($"TRANSITION {simulation.ActiveTransitionId ?? "NONE"}", 840, 208, 14, Color.White);
    if (simulation.SelectedTarget is { } target)
    {
        var math = simulation.LastShot is not null && simulation.LastShotTargetId == target.Definition.Id
            ? simulation.LastShot.Math
            : new RurkCombatResolver().CalculateLugerTarget(new RurkShotContext
            {
                DistanceMeters = MathF.Sqrt(simulation.Hero.Position.DistanceSquared(target.Position)) / 80f,
                TargetMovementSvd = target.MovementSvd,
                TrackDistance = Math.Abs(target.Definition.Lane - simulation.Hero.Lane),
                CoverLevel = simulation.CoverLevelFor(target),
                ArmourClass = target.Definition.ArmourClass,
            });
        DrawBody($"TARGET {target.Definition.DisplayName.ToUpperInvariant()}  HP {target.Hp}/{target.Definition.MaxHp}", 840, 240, 14, Color.White);
        DrawBody($"DIST {math.DistanceMeters:0.0}m  +{math.DistanceSvd}", 840, 266, 14, Color.White);
        DrawBody($"MOVE +{math.TargetMovementSvd}  TRACK +{math.TrackSvd}  COVER +{math.CoverSvd}", 840, 292, 14, Color.White);
        DrawBody($"TOTAL SVD +{math.TotalSvd}  /  HIT <= {math.Target}", 840, 318, 14, new Color(84, 255, 144, 255));
        DrawBody($"ARMOUR d100 + 40 >= {target.Definition.ArmourClass} + 50", 840, 344, 14, Color.White);
        if (simulation.LastShotBlockedByObstacleId is not null)
        {
            DrawBody($"BLOCKED BY {simulation.LastShotBlockedByObstacleId}", 840, 366, 12, new Color(245, 67, 54, 255));
        }
    }
}

static void DrawHeroHitFeedback(WorldSimulation simulation)
{
    if (simulation.Hero.HitFlashRemaining <= 0f)
    {
        return;
    }
    var alpha = (byte)Math.Clamp((int)(simulation.Hero.HitFlashRemaining / 0.35f * 105f), 0, 105);
    Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color((byte)156, (byte)18, (byte)14, alpha));
}

static void DrawReloadPanels(int phase, bool jammed)
{
    var labels = new[]
    {
        ("I", "MAGAZINE OUT"),
        ("II", "INSERT MAGAZINE"),
        ("III", "PULL THE TOGGLE"),
    };
    var positions = new[] { 40, 480, 920 };
    for (var index = 0; index <= Math.Clamp(phase, 0, labels.Length - 1); index++)
    {
        var item = labels[index];
        var x = positions[index];
        Raylib.DrawRectangle(x, 92, 320, 126, new Color(7, 8, 8, 242));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, 92, 320, 126), 4f, Color.White);
        Raylib.DrawRectangleLinesEx(new Rectangle(x + 6, 98, 308, 114), 2f, new Color(230, 196, 83, 255));
        DrawDisplay(jammed ? $"CLEAR JAM {item.Item1}" : $"RELOAD {item.Item1}", x + 24, 108, 26, new Color(230, 196, 83, 255));
        DrawBody(item.Item2, x + 24, 158, 18, Color.White);
    }
}

static int ReadDecisionChoice(WorldSimulation simulation)
{
    if (simulation.ActiveDecision is null)
    {
        return 0;
    }
    var keys = new[] { KeyboardKey.One, KeyboardKey.Two, KeyboardKey.Three, KeyboardKey.Four };
    for (var index = 0; index < simulation.ActiveDecision.Choices.Count; index++)
    {
        if (Raylib.IsKeyPressed(keys[index]))
        {
            return index + 1;
        }
    }
    if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
    {
        return 0;
    }
    var mouse = Raylib.GetMousePosition();
    var rectangles = DecisionLayout.Create(simulation.ActiveDecision).Choices;
    for (var index = 0; index < rectangles.Count; index++)
    {
        if (Raylib.CheckCollisionPointRec(mouse, rectangles[index]))
        {
            return index + 1;
        }
    }
    return 0;
}

static void DrawDecisionOverlay(WorldSimulation simulation)
{
    var decision = simulation.ActiveDecision;
    if (decision is null)
    {
        return;
    }
    var width = Raylib.GetScreenWidth();
    var height = Raylib.GetScreenHeight();
    var backdropAlpha = decision.Presentation == DecisionPresentation.FullScene ? 255 : 190;
    Raylib.DrawRectangle(0, 0, width, height, new Color(0, 0, 0, backdropAlpha));

    var layout = DecisionLayout.Create(decision);
    Raylib.DrawRectangle(layout.PanelX, layout.PanelY, layout.PanelWidth, layout.PanelHeight, new Color(10, 12, 13, 245));
    Raylib.DrawRectangle(layout.PanelX, layout.PanelY, 5, layout.PanelHeight, new Color(217, 181, 67, 255));
    DrawDisplay(decision.Title, layout.PanelX + 30, layout.PanelY + 18, 36, new Color(235, 205, 105, 255));
    DrawWrappedText(decision.Body, layout.PanelX + 30, layout.PanelY + 76, layout.PanelWidth - 60, 21, 28, new Color(225, 226, 218, 255));

    if (layout.Slide is { } slide && decision.SlideAsset is { } slideAsset)
    {
        DrawDecisionSlide(slideAsset, decision.SlideCrop, slide);
    }

    var rectangles = layout.Choices;
    var mouse = Raylib.GetMousePosition();
    for (var index = 0; index < decision.Choices.Count; index++)
    {
        var rectangle = rectangles[index];
        var hover = Raylib.CheckCollisionPointRec(mouse, rectangle);
        Raylib.DrawRectangleRec(rectangle, hover ? new Color(50, 55, 48, 250) : new Color(25, 29, 29, 250));
        Raylib.DrawRectangleLinesEx(rectangle, 2f, hover ? new Color(235, 205, 105, 255) : new Color(92, 97, 87, 255));
        DrawDisplay($"{index + 1}", (int)rectangle.X + 16, (int)rectangle.Y + 6, 30, new Color(235, 205, 105, 255));
        DrawWrappedText(decision.Choices[index].Text, (int)rectangle.X + 52, (int)rectangle.Y + 12, (int)rectangle.Width - 66, 19, 23, Color.White);
    }

    if (decision.TimerSeconds is { } duration && simulation.DecisionSecondsRemaining is { } remaining)
    {
        var barX = layout.PanelX + 30;
        var barY = layout.TimerBarY;
        var barWidth = layout.PanelWidth - 60;
        var progress = Math.Clamp(remaining / duration, 0f, 1f);
        Raylib.DrawRectangle(barX, barY, barWidth, 5, new Color(53, 55, 51, 255));
        Raylib.DrawRectangle(barX, barY, (int)(barWidth * progress), 5, progress < 0.25f ? Color.Red : new Color(217, 181, 67, 255));
        DrawBody(remaining.ToString("0.0", CultureInfo.InvariantCulture), layout.PanelX + layout.PanelWidth - 72, layout.TimerTextY, 16, new Color(225, 226, 218, 255));
    }
}

static void DrawDecisionSlide(string asset, NarrativeRect? authoredCrop, Rectangle destination)
{
    var texture = NarrativeSlides.Get(asset);
    var source = authoredCrop is { } crop
        ? new Rectangle(crop.X, crop.Y, crop.Width, crop.Height)
        : new Rectangle(0f, 0f, texture.Width, texture.Height);
    var scale = MathF.Min(destination.Width / source.Width, destination.Height / source.Height);
    var fitted = new Rectangle(
        destination.X + (destination.Width - source.Width * scale) * 0.5f,
        destination.Y + (destination.Height - source.Height * scale) * 0.5f,
        source.Width * scale,
        source.Height * scale);
    Raylib.DrawRectangleRec(destination, new Color(2, 3, 3, 255));
    Raylib.DrawTexturePro(texture, source, fitted, Vector2.Zero, 0f, Color.White);
    Raylib.DrawRectangleLinesEx(destination, 2f, new Color(117, 112, 91, 255));
}

static void DrawDirectedSequence(WorldSimulation simulation)
{
    if (simulation.ActiveDirectedSequenceId is null)
    {
        return;
    }
    var width = Raylib.GetScreenWidth();
    var height = Raylib.GetScreenHeight();
    Raylib.DrawRectangle(0, 0, width, 28, Color.Black);
    Raylib.DrawRectangle(0, height - 28, width, 28, Color.Black);
}

static void DrawWrappedText(string text, int x, int y, int maxWidth, int fontSize, int lineHeight, Color color)
{
    var line = string.Empty;
    var lineIndex = 0;
    foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    {
        var candidate = line.Length == 0 ? word : $"{line} {word}";
        if (line.Length > 0 && MeasureBody(candidate, fontSize) > maxWidth)
        {
            DrawBody(line, x, y + lineIndex * lineHeight, fontSize, color);
            line = word;
            lineIndex++;
        }
        else
        {
            line = candidate;
        }
    }
    if (line.Length > 0)
    {
        DrawBody(line, x, y + lineIndex * lineHeight, fontSize, color);
    }
}

static void DrawDisplay(string text, int x, int y, float size, Color color) =>
    Raylib.DrawTextEx(Typography.Display, text, new Vector2(x, y), size, MathF.Max(0.8f, size * 0.055f), color);

static void DrawBody(string text, int x, int y, float size, Color color) =>
    Raylib.DrawTextEx(Typography.Body, text, new Vector2(x, y), size, MathF.Max(0.15f, size * 0.012f), color);

static int MeasureBody(string text, float size) =>
    (int)MathF.Ceiling(Raylib.MeasureTextEx(Typography.Body, text, size, MathF.Max(0.15f, size * 0.012f)).X);

static void DrawBodyFit(string text, int x, int y, int maxWidth, float size, float minimumSize, Color color)
{
    var fittedSize = size;
    while (fittedSize > minimumSize && MeasureBody(text, fittedSize) > maxWidth)
    {
        fittedSize -= 0.5f;
    }
    DrawBody(text, x, y + (int)MathF.Ceiling((size - fittedSize) * 0.35f), fittedSize, color);
}

static Vector2 ToScreen(Vec2 point, float cameraLeft) => new(point.X - cameraLeft, point.Y);

static void UpdateGeometryEditor(
    WorldDefinition world,
    float cameraX,
    ref (SupportSurface? Surface, int Index) selected)
{
    var mouse = Raylib.GetMousePosition();
    var worldMouse = new Vec2(mouse.X + cameraX - ScreenWidth * 0.5f, mouse.Y);
    if (Raylib.IsMouseButtonPressed(MouseButton.Left))
    {
        selected = world.Supports
            .SelectMany(surface => surface.Points.Select((point, index) => (Surface: surface, Index: index, Distance: point.DistanceSquared(worldMouse))))
            .Where(item => item.Distance <= 16f * 16f)
            .OrderBy(item => item.Distance)
            .Select(item => (item.Surface, item.Index))
            .FirstOrDefault();
    }
    if (selected.Surface is not null && Raylib.IsMouseButtonDown(MouseButton.Left))
    {
        selected.Surface.Points[selected.Index] = worldMouse;
    }
    if (Raylib.IsMouseButtonReleased(MouseButton.Left))
    {
        selected = (null, -1);
    }
}

static void SaveGeometryOverride(WorldDefinition world)
{
    var directory = Path.Combine(AppContext.BaseDirectory, "inspections");
    Directory.CreateDirectory(directory);
    var payload = world.Supports.Select(surface => new
    {
        id = surface.Id,
        lane = surface.Lane,
        elevation_rank = surface.ElevationRank,
        points = surface.Points.Select(point => new[] { point.X, point.Y }),
    });
    File.WriteAllText(
        Path.Combine(directory, "support_geometry_override.json"),
        JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
}

static void WriteInspection(string stem, WorldDefinition world, WorldSimulation simulation, float cameraX, bool debugOverlay)
{
    var directory = Path.Combine(AppContext.BaseDirectory, "inspections");
    Directory.CreateDirectory(directory);
    Raylib.TakeScreenshot(Path.Combine("inspections", $"{stem}.png"));
    var snapshot = new
    {
        frame = new { width = ScreenWidth, height = ScreenHeight, camera_x = cameraX, debug_overlay = debugOverlay },
        world = world.Id,
        hero = new
        {
            foot = new[] { simulation.Hero.Position.X, simulation.Hero.Position.Y },
            velocity = new[] { simulation.Hero.Velocity.X, simulation.Hero.Velocity.Y },
            simulation.Hero.Lane,
            simulation.Hero.DisplayScale,
            state = simulation.Hero.State.ToString(),
            simulation.Hero.ActiveSupportId,
            simulation.Hero.TraversalMode,
            simulation.Hero.IsCrouched,
            simulation.Hero.BalanceRequired,
            transition = simulation.ActiveTransitionId,
            transition_progress = simulation.TransitionProgress,
            simulation.Hero.PistolReady,
            simulation.Hero.AmmoInMagazine,
            simulation.Hero.ReserveAmmo,
            simulation.Hero.ReloadPhase,
            simulation.Hero.ReloadProgress,
            simulation.Hero.Hp,
            simulation.Hero.IsAlive,
            simulation.Hero.HitFlashRemaining,
            weapon_condition = simulation.Hero.WeaponCondition.ToString(),
        },
        objective = new { world.Objective.Id, simulation.ObjectiveCollected },
        narrative = new
        {
            active_decision = simulation.ActiveDecision?.Id,
            simulation.DecisionSecondsRemaining,
            active_directed_sequence = simulation.ActiveDirectedSequenceId,
            simulation.IsWorldPaused,
            simulation.IsPlayerControlLocked,
            flags = simulation.Narrative.Flags,
            relationships = simulation.Narrative.Relationships,
        },
        selected_target = simulation.SelectedTargetId,
        enemies = simulation.Enemies.Select(enemy => new
        {
            enemy.Definition.Id,
            enemy.Definition.DisplayName,
            position = new[] { enemy.Position.X, enemy.Position.Y },
            lane = enemy.Definition.Lane,
            cover_level = simulation.CoverLevelFor(enemy),
            enemy.Hp,
            enemy.IsAlive,
            presentation_state = enemy.PresentationState.ToString(),
            presentation_progress = enemy.PresentationProgress,
        }),
        enemy_projectiles = simulation.EnemyProjectiles.Select(projectile => new
        {
            projectile.SourceEnemyId,
            kind = projectile.Kind.ToString(),
            position = new[] { projectile.Position.X, projectile.Position.Y },
            origin = new[] { projectile.Origin.X, projectile.Origin.Y },
            target = new[] { projectile.Target.X, projectile.Target.Y },
            impact = new[] { projectile.ImpactPoint.X, projectile.ImpactPoint.Y },
            projectile.SourceLane,
            projectile.TargetLane,
            projectile.Progress,
            projectile.PathProgress,
            projectile.PathEndProgress,
            projectile.CurrentDepth,
            projectile.Damage,
            projectile.BlockedByObstacleId,
            projectile.ImpactMaterial,
        }),
        world_impact_effects = simulation.WorldImpactEffects.Select(effect => new
        {
            position = new[] { effect.Position.X, effect.Position.Y },
            effect.Lane,
            kind = effect.Kind.ToString(),
            effect.Progress,
        }),
        player_shot_effect = simulation.ActivePlayerShotEffect is null ? null : new
        {
            origin = new[] { simulation.ActivePlayerShotEffect.Origin.X, simulation.ActivePlayerShotEffect.Origin.Y },
            target = new[] { simulation.ActivePlayerShotEffect.Target.X, simulation.ActivePlayerShotEffect.Target.Y },
            simulation.ActivePlayerShotEffect.OriginLane,
            simulation.ActivePlayerShotEffect.TargetLane,
            impact_kind = simulation.ActivePlayerShotEffect.ImpactKind.ToString(),
            simulation.ActivePlayerShotEffect.Progress,
        },
        last_shot = simulation.LastShot is null ? null : new
        {
            simulation.LastShot.HitRoll,
            outcome = simulation.LastShot.Outcome.ToString(),
            simulation.LastShot.AimHit,
            simulation.LastShot.Hit,
            zone = simulation.LastShot.Zone.ToString(),
            simulation.LastShot.PenetrationRoll,
            simulation.LastShot.Penetrated,
            simulation.LastShot.DamageRoll,
            simulation.LastShot.Damage,
            simulation.LastShotBlockedByObstacleId,
            math = simulation.LastShot.Math,
        },
        combat_obstacles = world.CombatObstacles.Select(obstacle => new
        {
            obstacle.Id,
            rect = new[] { obstacle.Bounds.X, obstacle.Bounds.Y, obstacle.Bounds.Width, obstacle.Bounds.Height },
            obstacle.FrontLane,
            obstacle.BackLane,
            blocks = obstacle.Blocks.Select(kind => kind.ToString()),
            obstacle.Material,
            obstacle.VisibleArtContract,
        }),
        cover_zones = world.CoverZones.Select(zone => new
        {
            zone.Id,
            rect = new[] { zone.Bounds.X, zone.Bounds.Y, zone.Bounds.Width, zone.Bounds.Height },
            zone.Lane,
            zone.CoverLevel,
            zone.ObstacleId,
            zone.VisibleArtContract,
        }),
        visible_supports = world.Supports.Select(surface => new
        {
            surface.Id,
            surface.Lane,
            surface.ElevationRank,
            points = surface.Points.Select(point => new[] { point.X, point.Y }),
        }),
    };
    File.WriteAllText(
        Path.Combine(directory, $"{stem}.json"),
        JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
}

sealed record DepthOcclusionLayer(int Lane, Texture2D Texture);

static class Typography
{
    public static Font Display { get; private set; }
    public static Font Body { get; private set; }

    public static void Load(string fontRoot)
    {
        var canonicalFont = Path.Combine(fontRoot, "cinzel", "Cinzel-Variable.ttf");
        if (!File.Exists(canonicalFont))
        {
            throw new FileNotFoundException("Canonical Cinzel font is missing from the runtime package", canonicalFont);
        }

        var fallback = Raylib.GetFontDefault();
        var display = Raylib.LoadFontEx(
            canonicalFont,
            72,
            null!,
            0);
        if (!IsExpectedFont(display, fallback, 72))
        {
            UnloadIfOwned(display, fallback);
            throw new InvalidDataException("Canonical Cinzel display font could not be decoded");
        }

        var body = Raylib.LoadFontEx(
            canonicalFont,
            56,
            null!,
            0);
        if (!IsExpectedFont(body, fallback, 56))
        {
            UnloadIfOwned(body, fallback);
            Raylib.UnloadFont(display);
            throw new InvalidDataException("Canonical Cinzel body font could not be decoded");
        }

        Display = display;
        Body = body;
        Raylib.SetTextureFilter(Display.Texture, TextureFilter.Bilinear);
        Raylib.SetTextureFilter(Body.Texture, TextureFilter.Bilinear);
    }

    public static void Unload()
    {
        Raylib.UnloadFont(Display);
        Raylib.UnloadFont(Body);
    }

    private static bool IsExpectedFont(Font font, Font fallback, int expectedBaseSize) =>
        font.Texture.Id != 0
        && font.Texture.Id != fallback.Texture.Id
        && font.BaseSize == expectedBaseSize
        && font.GlyphCount > 0;

    private static void UnloadIfOwned(Font font, Font fallback)
    {
        if (font.Texture.Id != 0 && font.Texture.Id != fallback.Texture.Id)
        {
            Raylib.UnloadFont(font);
        }
    }
}

sealed record SpriteFrame(Texture2D Texture, Rectangle AlphaBounds);

sealed record SpriteClip(IReadOnlyList<SpriteFrame> Frames, float ReferenceBodyHeight);

sealed record DecisionLayout(
    int PanelX,
    int PanelY,
    int PanelWidth,
    int PanelHeight,
    Rectangle? Slide,
    IReadOnlyList<Rectangle> Choices,
    int TimerTextY,
    int TimerBarY)
{
    public static DecisionLayout Create(DecisionDefinition decision)
    {
        var screenWidth = Raylib.GetScreenWidth();
        var screenHeight = Raylib.GetScreenHeight();
        var hasSlide = decision.SlideAsset is not null;
        var targetHeight = hasSlide ? 650 : 520;
        var panelHeight = Math.Min(targetHeight, screenHeight - 24);
        var panelWidth = Math.Min(hasSlide ? 1000 : 900, screenWidth - 60);
        var panelX = (screenWidth - panelWidth) / 2;
        var panelY = (screenHeight - panelHeight) / 2;
        var timerBarY = panelY + panelHeight - 30;
        var timerTextY = timerBarY - 24;
        var choiceHeight = hasSlide ? 54 : 62;
        var gap = hasSlide ? 7 : 9;
        var choiceBlockHeight = decision.Choices.Count * choiceHeight + (decision.Choices.Count - 1) * gap;
        var choiceStartY = hasSlide
            ? timerTextY - choiceBlockHeight - 12
            : panelY + 178;
        var choices = new List<Rectangle>(decision.Choices.Count);
        for (var index = 0; index < decision.Choices.Count; index++)
        {
            choices.Add(new Rectangle(
                panelX + 30,
                choiceStartY + index * (choiceHeight + gap),
                panelWidth - 60,
                choiceHeight));
        }
        Rectangle? slide = null;
        if (hasSlide)
        {
            var slideY = panelY + 120;
            var slideHeight = Math.Max(80, choiceStartY - slideY - 14);
            slide = new Rectangle(panelX + 30, slideY, panelWidth - 60, slideHeight);
        }
        return new DecisionLayout(panelX, panelY, panelWidth, panelHeight, slide, choices, timerTextY, timerBarY);
    }
}

static class NarrativeSlides
{
    private static readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);

    public static void Load(string contentRoot, NarrativeContent narrative)
    {
        var root = Path.GetFullPath(contentRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var decision in narrative.Decisions.Values.Where(item => item.SlideAsset is not null))
        {
            var asset = decision.SlideAsset!;
            if (!Textures.TryGetValue(asset, out var texture))
            {
                var path = Path.GetFullPath(Path.Combine(root, asset.Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                {
                    throw new InvalidDataException($"Decision slide '{asset}' does not resolve inside Content");
                }
                texture = Raylib.LoadTexture(path);
                Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
                Textures.Add(asset, texture);
            }
            if (decision.SlideCrop is { } crop
                && (crop.X < 0f || crop.Y < 0f || crop.X + crop.Width > texture.Width || crop.Y + crop.Height > texture.Height))
            {
                throw new InvalidDataException($"Decision '{decision.Id}' slide crop exceeds '{asset}' bounds");
            }
        }
    }

    public static Texture2D Get(string asset) => Textures.TryGetValue(asset, out var texture)
        ? texture
        : throw new InvalidOperationException($"Decision slide '{asset}' was not loaded");

    public static void Unload()
    {
        foreach (var texture in Textures.Values)
        {
            Raylib.UnloadTexture(texture);
        }
        Textures.Clear();
    }
}
