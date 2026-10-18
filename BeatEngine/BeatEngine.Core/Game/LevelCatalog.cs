using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BeatEngine.Core.Game
{
    internal sealed class LevelDefinition
    {
        public int Id { get; set; }
        public string Theme { get; set; }
        public string[][] Words { get; set; }
    }

    internal sealed class LevelCatalog
    {
        private readonly Dictionary<int, LevelDefinition> levels;

        private LevelCatalog(Dictionary<int, LevelDefinition> levels) => this.levels = levels;

        public LevelDefinition GetLevel(int id) => levels.TryGetValue(id, out var level)
            ? level : throw new InvalidDataException($"Level {id} is missing from the level catalog.");

        public static LevelCatalog Load(Stream stream)
        {
            var definitions = JsonSerializer.Deserialize<LevelDefinition[]>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (definitions == null || definitions.Length == 0)
                throw new InvalidDataException("The level catalog is empty.");

            var levels = new Dictionary<int, LevelDefinition>();
            foreach (var level in definitions)
            {
                if (level == null || level.Id < 1 || string.IsNullOrWhiteSpace(level.Theme) ||
                    level.Words == null || level.Words.Length != 3 ||
                    level.Words.Any(word => word == null || word.Length < 3 || word.Length > 4 ||
                        word.Any(string.IsNullOrWhiteSpace)))
                    throw new InvalidDataException("The level catalog contains an invalid level.");
                if (!levels.TryAdd(level.Id, level))
                    throw new InvalidDataException($"Duplicate level ID {level.Id} in the level catalog.");
            }
            return new LevelCatalog(levels);
        }
    }
}
