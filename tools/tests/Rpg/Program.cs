using BeatEngine.Rpg;
using System.Globalization;
using System.Numerics;

int passed = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
}
void Test(string name, Action action)
{
    action();
    passed++;
    Console.WriteLine("PASS: " + name);
}
RpgMap Fixture() => RpgMap.Load(new StringReader("""
BACKGROUND
floor,0,0
floor,500,500
FOREGROUND
shelf,100,0
HITBOXES
shelf,100,0;130,0;130,200;100,200
"""));
RpgMap Original()
{
    using var reader = File.OpenText(Environment.GetEnvironmentVariable("RPG_TEST_MAP") ?? Path.Combine(AppContext.BaseDirectory, "test.map"));
    return RpgMap.Load(reader);
}

Test("original map preserves library tiles, layers and bookshelf hitboxes", () =>
{
    var map = Original();
    Check(map.Tiles.Count == 1467 && map.Tiles.Count(t => t.Foreground) == 207 && map.Obstacles.Count == 53 && map.Interactions.Count == 1, "Map data lost");
    Check(map.Tiles[0].Position == Vector2.Zero, "Authored coordinates changed");
    Check(map.IsWalkable(map.Spawn), "Spawn is blocked");
});
Test("map decimal coordinates parse under Spanish locale", () =>
{
    var before = CultureInfo.CurrentCulture;
    try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-AR"); Check(RpgMap.Load(new StringReader("BACKGROUND\nfloor,0,604.5")).Tiles[0].Position.Y == 604.5f, "Locale changed coordinates"); }
    finally { CultureInfo.CurrentCulture = before; }
});
Test("malformed maps fail with the offending line", () =>
{
    try { RpgMap.Load(new StringReader("BACKGROUND\nbroken,NaN,0")); }
    catch (InvalidDataException error) { Check(error.Message.Contains("line 2"), "Missing diagnostic"); return; }
    throw new Exception("Bad map accepted");
});
Test("movement follows the configured speed in world pixels per second", () =>
{
    var world = new RpgWorld(Fixture());
    world.Restore(new Vector2(-200, 300), Facing.Right);
    for (int i = 0; i < 60; i++) world.Update(Vector2.UnitX, 1f / 60);
    Check(Math.Abs(world.Position.X - (-200 + RpgWorld.Speed)) < 0.01, "Frame-based movement");
    Check(world.Facing == Facing.Right && world.Moving, "Wrong animation state");
});
Test("30 and 60 fps produce the same displacement", () =>
{
    var a = new RpgWorld(Fixture()); var b = new RpgWorld(Fixture());
    a.Restore(new Vector2(-200, 300), Facing.Right); b.Restore(new Vector2(-200, 300), Facing.Right);
    for (int i = 0; i < 60; i++) a.Update(Vector2.UnitX, 1f / 60);
    for (int i = 0; i < 30; i++) b.Update(Vector2.UnitX, 1f / 30);
    Check(Vector2.Distance(a.Position, b.Position) < 0.01, "Movement depends on FPS");
});
Test("diagonal input has no speed advantage", () =>
{
    var world = new RpgWorld(Fixture());
    world.Restore(new Vector2(-200, 300), Facing.Down);
    var start = world.Position;
    world.Update(Vector2.One, 0.1f);
    Check(Math.Abs(Vector2.Distance(start, world.Position) - RpgWorld.Speed * 0.1f) < 0.01, "Fast diagonal");
});
Test("collision prevents tunneling through a bookshelf", () =>
{
    var world = new RpgWorld(Fixture());
    world.Restore(new Vector2(50, 50), Facing.Right);
    for (int i = 0; i < 30; i++) world.Update(Vector2.UnitX, 0.1f);
    Check(RpgWorld.BodyAt(world.Position).Right <= 100, "Walked through shelf");
    Check(!world.Moving, "Blocked player still walks");
});
Test("collision allows sliding along the obstacle", () =>
{
    var world = new RpgWorld(Fixture());
    world.Restore(new Vector2(50, 50), Facing.Right);
    for (int i = 0; i < 6; i++) world.Update(Vector2.One, 0.1f);
    Check(world.Position.Y > 65 && RpgWorld.BodyAt(world.Position).Right <= 100, "Cannot slide");
});
Test("release and suspend clear the movement animation", () =>
{
    var world = new RpgWorld(Fixture());
    world.Update(Vector2.UnitX, 0.02f);
    world.Stop();
    Check(!world.Moving && world.Frame == 0, "Movement is stuck");
    world.Update(Vector2.Zero, 0.3f);
    Check(!world.Moving, "Idle input moves the player");
});
Test("invalid or blocked restored positions are ignored", () =>
{
    var world = new RpgWorld(Fixture());
    var spawn = world.Position;
    world.Restore(new Vector2(float.NaN, 0), Facing.Down);
    world.Restore(new Vector2(100, 50), Facing.Down);
    Check(world.Position == spawn, "Invalid save was applied");
});

Test("running is exactly 1.5 times walking and still collides", () =>
{
    var walk = new RpgWorld(Fixture()); var run = new RpgWorld(Fixture());
    var start = new Vector2(-200, 300);
    walk.Restore(start, Facing.Right); run.Restore(start, Facing.Right);
    walk.Update(Vector2.UnitX, 0.1f); run.Update(Vector2.UnitX, 0.1f, true);
    Check(Math.Abs((run.Position.X - start.X) / (walk.Position.X - start.X) - 1.5f) < 0.001, "Wrong run factor");
    run.Restore(new Vector2(50, 50), Facing.Right);
    for (int i = 0; i < 30; i++) run.Update(Vector2.UnitX, 0.1f, true);
    Check(RpgWorld.BodyAt(run.Position).Right <= 100, "Running tunneled through obstacle");
});
RpgConversation Conversation()
{
    using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "grandfather_intro.json"));
    return RpgConversation.Load(stream);
}
Test("logical hitboxes are traversable and legacy triggers remain compatible", () =>
{
    foreach (string tag in new[] { "logical:grandfather", "logical_grandfather", "interaction:grandfather" })
    {
        var map = RpgMap.Load(new StringReader("BACKGROUND\nfloor,0,0\nfloor,500,500\nHITBOXES\n" + tag + ",100,100;200,100;200,200;100,200"));
        Check(map.Interactions.Count == 1 && map.Interactions[0].Id == "grandfather" && map.Obstacles.Count == 0, "Logical hitbox blocks movement");
        Check(map.IsWalkable(new Vector2(120, 120)), "Logical zone is not traversable");
    }
});
Test("grandfather interaction zone is traversable and talk requires proximity", () =>
{
    var world = new RpgWorld(Original());
    var dialogue = Conversation();
    world.Restore(new Vector2(656, 768), Facing.Up);
    Check(!dialogue.TryStart(world), "Talk started outside hitbox");
    var zone = world.Map.Interactions.Single(item => item.Id == "grandfather").Bounds;
    var target = new Vector2(zone.X + zone.Width / 2 - 16, zone.Bottom - 35);
    world.Restore(target, Facing.Up);
    Check(world.Position == target && dialogue.TryStart(world), "Cannot reach interaction zone");
    Check(dialogue.LineIndex == 0 && dialogue.Current.Speaker == "Dante", "Wrong first turn");
    Check(!world.CompletedScenes.Contains(dialogue.Id), "Scene completed on entry");
    Check(!dialogue.Continue(world) && dialogue.Current.Speaker == "Abuelo" && dialogue.LineIndex == 1, "First continue skipped a turn");
    Check(!dialogue.Continue(world) && dialogue.Current.Speaker == "Abuelo" && dialogue.LineIndex == 2, "Consecutive grandfather turns were merged");
    for (int i = 3; i < dialogue.Lines.Length; i++) Check(!dialogue.Continue(world), "Premature checkpoint");
    Check(dialogue.Active && dialogue.Current.Speaker == "Dante" && !world.CompletedScenes.Contains(dialogue.Id), "Final line was not displayed");
    Check(dialogue.Continue(world) && !dialogue.Active && world.Checkpoint == dialogue.Id, "Final continue did not create checkpoint");
    Check(dialogue.TryStart(world) && dialogue.Current.Speaker == "Abuelo" && dialogue.LineCount == 1, "Grandfather reminder did not start");
    Check(dialogue.Current.Text.Contains("libro azul"), "Wrong repeat line");
    Check(!dialogue.Continue(world) && !dialogue.Active && world.Checkpoint == dialogue.Id, "Reminder changed checkpoint or stayed open");
    Check(dialogue.TryStart(world), "Reminder cannot be repeated");
    dialogue.Cancel();
});

RpgCheckpoints Checkpoints()
{
    using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "checkpoints.json"));
    return RpgCheckpoints.Load(stream);
}
Test("checkpoint name, ID and numeric aliases restore the right story state", () =>
{
    var world = new RpgWorld(Original());
    var checkpoints = Checkpoints();
    var dialogue = Conversation();
    Check(checkpoints.TryRestore("1", world, out var id) && id == dialogue.Id, "Numeric checkpoint failed");
    Check(world.CompletedScenes.Contains(dialogue.Id) && world.Checkpoint == dialogue.Id && world.Position == world.Map.Spawn && !world.Moving, "Intro checkpoint did not restore story progress at the entrance");
    Check(!dialogue.CanStart(world), "Dialogue started away from grandfather");
    var zone = world.Map.Interactions.Single(item => item.Id == "grandfather").Bounds;
    world.Restore(new Vector2(zone.X + zone.Width / 2 - 16, zone.Bottom - 35), Facing.Up);
    Check(dialogue.TryStart(world) && dialogue.Current.Speaker == "Abuelo" && dialogue.LineCount == 1, "Wrong dialogue after restoring checkpoint");
    dialogue.Cancel();
    Check(checkpoints.TryRestore(" Inicio ", world, out id) && id == "inicio" && world.Position == world.Map.Spawn, "Initial checkpoint failed");
    Check(world.Checkpoint == null && world.CompletedScenes.Count == 0 && !dialogue.CanStart(world), "Initial checkpoint did not rewind progress");
    Check(checkpoints.TryRestore("library_grandfather_intro", world, out id), "Full ID failed");
    Check(checkpoints.TryRestore("despues de hablar con abuelo", world, out id) && world.Position == world.Map.Spawn, "Display name alias did not restore entrance position");
    var position = world.Position;
    Check(!checkpoints.TryRestore("missing", world, out id) && world.Position == position && world.CompletedScenes.Contains(dialogue.Id), "Invalid checkpoint mutated progress");
    Check(checkpoints.TryRestore("0", world, out id) && world.Position == world.Map.Spawn && world.Checkpoint == null, "Zero did not reset to entrance");
});

string directory = Path.Combine(Path.GetTempPath(), "RpgPrototype-tests-" + Guid.NewGuid().ToString("N"));
try
{
    Test("save survives a new world instance and replacement is atomic", () =>
    {
        var store = new RpgSaveStore(directory);
        var world = new RpgWorld(Fixture());
        world.Restore(new Vector2(-200, 300), Facing.Left);
        Check(store.Save(world), "Could not save");
        var restored = new RpgWorld(Fixture());
        Check(store.Load(restored) && restored.Position == world.Position && restored.Facing == Facing.Left, "Save did not round-trip");
        world.Update(Vector2.UnitX, 0.1f);
        Check(store.Save(world) && !File.Exists(Path.Combine(directory, "save.json.tmp")), "Atomic replacement failed");
        Check(store.Load(restored) && restored.Position == world.Position, "Replacement did not load");
    });
    Test("scene checkpoint survives restart, map reset, and an obstructed saved position", () =>
    {
        var store = new RpgSaveStore(directory);
        var world = new RpgWorld(Fixture());
        world.CompleteScene("library_grandfather_intro");
        Check(store.Save(world), "Could not save checkpoint");
        var restored = new RpgWorld(Fixture());
        Check(store.Load(restored) && restored.Checkpoint == "library_grandfather_intro", "Checkpoint lost on restart");
        restored.Reset();
        Check(restored.CompletedScenes.Contains("library_grandfather_intro"), "Map reset erased story progress");
        File.WriteAllText(Path.Combine(directory, "save.json"), "{\"Version\":3,\"X\":100,\"Y\":50,\"Facing\":0,\"CompletedScenes\":[\"library_grandfather_intro\"],\"Checkpoint\":\"library_grandfather_intro\"}");
        restored = new RpgWorld(Fixture());
        Check(store.Load(restored) && restored.Position == restored.Map.Spawn && restored.Checkpoint == "library_grandfather_intro", "Map edit erased checkpoint");
    });
    Test("version 2 saves migrate without losing position", () =>
    {
        var store = new RpgSaveStore(directory);
        File.WriteAllText(Path.Combine(directory, "save.json"), "{\"Version\":2,\"X\":-200,\"Y\":300,\"Facing\":1}");
        var world = new RpgWorld(Fixture());
        Check(store.Load(world) && world.Position == new Vector2(-200, 300) && world.CompletedScenes.Count == 0, "Version 2 migration failed");
    });
    Test("truncated or unsupported saves fall back safely", () =>
    {
        var store = new RpgSaveStore(directory);
        var world = new RpgWorld(Fixture());
        File.WriteAllText(Path.Combine(directory, "save.json"), "{");
        Check(!store.Load(world), "Corrupt save accepted");
        File.WriteAllText(Path.Combine(directory, "save.json"), "{\"Version\":99,\"X\":0,\"Y\":0,\"Facing\":0}");
        Check(!store.Load(world), "Unsupported save accepted");
    });
}
finally
{
    // Remove only this test's two known files; never recursively delete a path.
    foreach (string name in new[] { "save.json", "save.json.tmp", "preferences.json", "preferences.json.tmp" }) File.Delete(Path.Combine(directory, name));
    if (Directory.Exists(directory)) Directory.Delete(directory);
}
Console.WriteLine($"{passed} RPG checks passed.");
