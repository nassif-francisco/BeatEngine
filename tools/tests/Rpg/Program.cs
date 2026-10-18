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
    using var reader = File.OpenText(Path.Combine(AppContext.BaseDirectory, "test.map"));
    return RpgMap.Load(reader);
}

Test("original map preserves library tiles, layers and bookshelf hitboxes", () =>
{
    var map = Original();
    Check(map.Tiles.Count == 1466 && map.Tiles.Count(t => t.Foreground) == 206 && map.Obstacles.Count == 52, "Map data lost");
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
Test("movement matches the 32px scene at 96 world pixels per second", () =>
{
    var world = new RpgWorld(Fixture());
    world.Restore(new Vector2(-200, 300), Facing.Right);
    for (int i = 0; i < 60; i++) world.Update(Vector2.UnitX, 1f / 60);
    Check(Math.Abs(world.Position.X + 104) < 0.01, "Frame-based movement");
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
    Check(Math.Abs(Vector2.Distance(start, world.Position) - 9.6f) < 0.01, "Fast diagonal");
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

string directory = Path.Combine(Path.GetTempPath(), "RpgPrototype-tests-" + Guid.NewGuid().ToString("N"));
try
{
    Test("joystick side persists independently of progress and tolerates corrupt preferences", () =>
    {
        var store = new RpgSaveStore(directory);
        Check(!store.LoadJoystickOnRight(), "Default should be left");
        Check(store.SaveJoystickOnRight(true), "Could not save right");
        Check(new RpgSaveStore(directory).LoadJoystickOnRight(), "Right side was lost");
        Check(store.SaveJoystickOnRight(false) && !new RpgSaveStore(directory).LoadJoystickOnRight(), "Left side was lost");
        File.WriteAllText(Path.Combine(directory, "preferences.json"), "{");
        Check(!store.LoadJoystickOnRight(), "Corrupt preferences should fall back to left");
    });
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
