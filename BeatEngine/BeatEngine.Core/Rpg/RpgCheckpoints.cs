using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BeatEngine.Rpg;

public sealed class RpgCheckpoint
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string[] CompletedScenes { get; set; } = Array.Empty<string>();
}

public sealed class RpgCheckpoints
{
    public RpgCheckpoint[] Entries { get; set; }

    public static RpgCheckpoints Load(Stream stream)
    {
        var result = JsonSerializer.Deserialize<RpgCheckpoints>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (result?.Entries == null || result.Entries.Length == 0 || result.Entries.Any(entry =>
            entry == null || string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Name) ||
            entry.CompletedScenes == null))
            throw new InvalidDataException("Invalid checkpoint definitions.");
        return result;
    }

    public bool TryRestore(string nameOrId, RpgWorld world, out string restoredId)
    {
        restoredId = null;
        if (string.IsNullOrWhiteSpace(nameOrId)) return false;
        string key = nameOrId.Trim();
        var entry = Entries.FirstOrDefault(item => string.Equals(key, item.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, item.Name, StringComparison.OrdinalIgnoreCase) ||
            (item.Aliases ?? Array.Empty<string>()).Any(alias => string.Equals(key, alias, StringComparison.OrdinalIgnoreCase)));
        restoredId = entry?.Id;
        if (entry == null) return false;
        world.RestoreProgress(entry.CompletedScenes, entry.Id == "inicio" ? null : entry.Id);
        world.Reset();
        return true;
    }
}
