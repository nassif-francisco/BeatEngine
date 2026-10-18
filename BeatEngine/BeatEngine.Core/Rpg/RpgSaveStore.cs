using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace BeatEngine.Rpg;

public sealed class RpgSaveStore
{
    public sealed record SaveData(int Version, float X, float Y, Facing Facing, string[] CompletedScenes = null, string Checkpoint = null);
    private readonly string path;
    public RpgSaveStore(string directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RPGPrototype");
        path = Path.Combine(directory, "save.json");
    }

    public bool Load(RpgWorld world)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
            if (data == null || (data.Version != 2 && data.Version != 3) || !Enum.IsDefined(data.Facing)) return false;
            world.RestoreProgress(data.CompletedScenes, data.Checkpoint);
            // A map edit may obstruct the old position; keep story progress and use the safe spawn.
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
                JsonSerializer.Serialize(stream, new SaveData(3, world.Position.X, world.Position.Y, world.Facing, world.CompletedScenes.OrderBy(id => id).ToArray(), world.Checkpoint));
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
