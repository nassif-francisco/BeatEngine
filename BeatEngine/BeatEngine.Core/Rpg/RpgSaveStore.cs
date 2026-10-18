using System;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace BeatEngine.Rpg;

public sealed class RpgSaveStore
{
    public sealed record SaveData(int Version, float X, float Y, Facing Facing);
    private readonly string path;
    public RpgSaveStore(string directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RPGPrototype");
        path = Path.Combine(directory, "save.json");
    }

    private sealed record Preferences(bool JoystickOnRight);
    private string PreferencesPath => Path.Combine(Path.GetDirectoryName(path), "preferences.json");

    public bool LoadJoystickOnRight()
    {
        try
        {
            return File.Exists(PreferencesPath) &&
                JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PreferencesPath))?.JoystickOnRight == true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    public bool SaveJoystickOnRight(bool onRight)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = PreferencesPath + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new Preferences(onRight));
                stream.Flush(true);
            }
            File.Move(temporary, PreferencesPath, true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool Load(RpgWorld world)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
            if (data == null || data.Version != 2 || !Enum.IsDefined(data.Facing) ||
                !world.Map.IsWalkable(new Vector2(data.X, data.Y))) return false;
            world.Restore(new Vector2(data.X, data.Y), data.Facing);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    public bool Save(RpgWorld world)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new SaveData(2, world.Position.X, world.Position.Y, world.Facing));
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
