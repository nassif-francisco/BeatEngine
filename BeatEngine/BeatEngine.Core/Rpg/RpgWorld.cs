using System;
using System.Numerics;

namespace BeatEngine.Rpg;

public enum Facing { Down, Left, Right, Up }

public sealed class RpgWorld
{
    public const float Speed = 128; // 4 32px tiles per second.
    public const float SpriteScale = 1f;
    public const int FrameSize = 32;
    public RpgMap Map { get; }
    public Vector2 Position { get; private set; }
    public Facing Facing { get; private set; }
    public bool Moving { get; private set; }
    public float AnimationTime { get; private set; }
    public int Frame => (int)(AnimationTime / (Moving ? 0.1f : 0.3f)) % 4;

    public RpgWorld(RpgMap map)
    {
        Map = map;
        Position = map.Spawn;
    }

    // A small foot rectangle lets the 32px character walk behind tall shelves.
    public static WorldRect BodyAt(Vector2 position) => new(position.X + 10, position.Y + 23, 12, 8);

    public void Restore(Vector2 position, Facing facing)
    {
        if (Map.IsWalkable(position) && Enum.IsDefined(facing))
        {
            Position = position;
            Facing = facing;
        }
    }

    public void Reset()
    {
        Position = Map.Spawn;
        Facing = Facing.Down;
        Stop();
    }

    public void Stop()
    {
        Moving = false;
        AnimationTime = 0;
    }

    public void Update(Vector2 input, float seconds)
    {
        if (!float.IsFinite(seconds) || seconds <= 0 || !float.IsFinite(input.X) || !float.IsFinite(input.Y)) return;
        seconds = Math.Min(seconds, 0.1f); // Never jump through obstacles after resuming.
        if (input.LengthSquared() > 1) input = Vector2.Normalize(input);
        if (input.LengthSquared() < 0.01f) input = Vector2.Zero;
        if (input != Vector2.Zero)
            Facing = Math.Abs(input.X) > Math.Abs(input.Y)
                ? (input.X > 0 ? Facing.Right : Facing.Left)
                : (input.Y > 0 ? Facing.Down : Facing.Up);

        Vector2 before = Position;
        Vector2 delta = input * Speed * seconds;
        // Small axis-separated steps preserve collision at every frame rate and
        // allow the player to slide along a bookshelf rather than sticking to it.
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y)) / 2));
        Vector2 step = delta / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector2 next = Position + new Vector2(step.X, 0);
            if (Map.IsWalkable(next)) Position = next;
            next = Position + new Vector2(0, step.Y);
            if (Map.IsWalkable(next)) Position = next;
        }
        bool moving = Vector2.DistanceSquared(before, Position) > 0.0001f;
        if (moving != Moving) AnimationTime = 0;
        Moving = moving;
        AnimationTime = (AnimationTime + seconds) % 120;
    }
}
