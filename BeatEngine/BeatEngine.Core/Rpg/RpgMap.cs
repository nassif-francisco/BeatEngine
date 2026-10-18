using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

namespace BeatEngine.Rpg;

public readonly record struct WorldRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public bool Intersects(WorldRect other) => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
}

public sealed record MapTile(string Name, Vector2 Position, bool Foreground);
public sealed record MapInteraction(string Id, WorldRect Bounds);

public sealed class RpgMap
{
    public List<MapTile> Tiles { get; } = new();
    public List<WorldRect> Obstacles { get; } = new();
    public List<MapInteraction> Interactions { get; } = new();
    public WorldRect Bounds { get; private set; }
    public Vector2 Spawn { get; private set; }

    // Read the original SFML map format, preserving authored coordinates and layer order.
    public static RpgMap Load(TextReader reader, Vector2? spawn = null, WorldRect? bounds = null)
    {
        var map = new RpgMap();
        string section = null;
        string line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            line = line.Trim();
            if (line.Length == 0) continue;
            if (line is "BACKGROUND" or "FOREGROUND" or "HITBOXES")
            {
                section = line;
                continue;
            }
            try
            {
                if (section == "HITBOXES")
                {
                    string[] values = line.Split(new[] { ',', ';' });
                    if (values.Length != 9) throw new FormatException("Expected four hitbox vertices");
                    var vertices = Enumerable.Range(0, 4).Select(i =>
                        new Vector2(Number(values[1 + i * 2]), Number(values[2 + i * 2]))).ToArray();
                    float left = vertices.Min(v => v.X), top = vertices.Min(v => v.Y);
                    var box = new WorldRect(left, top, vertices.Max(v => v.X) - left, vertices.Max(v => v.Y) - top);
                    if (box.Width <= 0 || box.Height <= 0) throw new FormatException("Empty hitbox");
                    string prefix = values[0].StartsWith("logical", StringComparison.Ordinal) ? "logical"
                        : values[0].StartsWith("interaction:", StringComparison.Ordinal) ? "interaction:" : null;
                    if (prefix != null)
                    {
                        string id = values[0].Substring(prefix.Length).TrimStart(':', '_', '-', ' ');
                        if (string.IsNullOrWhiteSpace(id)) throw new FormatException("Logical hitbox requires an action name");
                        map.Interactions.Add(new MapInteraction(id, box));
                    }
                    else map.Obstacles.Add(box);
                }
                else if (section is "BACKGROUND" or "FOREGROUND")
                {
                    string[] values = line.Split(',');
                    if (values.Length != 3 || string.IsNullOrWhiteSpace(values[0])) throw new FormatException("Invalid tile");
                    map.Tiles.Add(new MapTile(values[0], new Vector2(Number(values[1]), Number(values[2])), section == "FOREGROUND"));
                }
                else throw new FormatException("Missing section header");
            }
            catch (Exception error) when (error is FormatException or OverflowException)
            {
                throw new InvalidDataException($"Invalid RPG map at line {lineNumber}: {line}", error);
            }
        }
        if (map.Tiles.Count == 0) throw new InvalidDataException("The RPG map contains no tiles.");
        float minX = map.Tiles.Min(t => t.Position.X), minY = map.Tiles.Min(t => t.Position.Y);
        float maxX = map.Tiles.Max(t => t.Position.X) + 89, maxY = map.Tiles.Max(t => t.Position.Y) + 130;
        map.Bounds = bounds ?? new WorldRect(minX - 400, minY - 400, maxX - minX + 800, maxY - minY + 800);
        // The desktop spawn depended on the window's desktop position. Start on the
        // authored floor instead so the test map is immediately visible on a phone.
        Vector2 center = new((minX + maxX) / 2 - 27, (minY + maxY) / 2 - 27);
        var candidates = new[] { center }.Concat(map.Tiles.Where(t => !t.Foreground).Select(t => t.Position));
        map.Spawn = spawn ?? candidates.FirstOrDefault(position => map.IsWalkable(position), center);
        if (!map.IsWalkable(map.Spawn)) throw new InvalidDataException("The RPG map has no valid spawn.");
        return map;
    }

    private static float Number(string text)
    {
        float value = float.Parse(text, CultureInfo.InvariantCulture);
        return float.IsFinite(value) ? value : throw new FormatException("Non-finite coordinate");
    }

    public bool IsWalkable(Vector2 position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) return false;
        WorldRect body = RpgWorld.BodyAt(position);
        return body.X >= Bounds.X && body.Y >= Bounds.Y && body.Right <= Bounds.Right && body.Bottom <= Bounds.Bottom &&
            Obstacles.All(obstacle => !body.Intersects(obstacle));
    }
}
