using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using NumericsVector = System.Numerics.Vector2;

namespace BeatEngine.Rpg;

public sealed class RpgScene : IDisposable
{
    private const float UiHeight = 540;
    private readonly GraphicsDevice graphics;
    private readonly SpriteBatch batch;
    private readonly Texture2D ground, walk, idle, pixel, circle;
    private readonly SpriteFont font;
    private readonly Dictionary<string, (Texture2D Texture, int Frames)> textures = new();
    private readonly RpgSaveStore saves = new();
    private readonly RpgSceneConfig config;
    private readonly IGrouping<string, MapTile>[] backgroundGroups;
    private readonly MapTile[] foreground;
    private WorldRect visibleWorld;
    public RpgWorld World { get; }
    private bool paused, pauseKeyWasDown;
    private bool joystickOnRight;
    private int? joystickFinger;
    private Vector2 joystick;
    private float animationTime, saveTimer;
    private NumericsVector lastSavedPosition;
    private string saveStatus;

    public RpgScene(GraphicsDevice graphics, ContentManager content)
    {
        this.graphics = graphics;
        batch = new SpriteBatch(graphics);
        using (var stream = TitleContainer.OpenStream("Content/Rpg/scene.json"))
            config = RpgSceneConfig.Load(stream);
        using (var stream = TitleContainer.OpenStream("Content/Rpg/Maps/test.map"))
        using (var reader = new StreamReader(stream))
            World = new RpgWorld(RpgMap.Load(reader, new NumericsVector(config.Spawn[0], config.Spawn[1]),
                new WorldRect(config.Bounds[0], config.Bounds[1], config.Bounds[2], config.Bounds[3])));
        saves.Load(World);
        joystickOnRight = saves.LoadJoystickOnRight();
        lastSavedPosition = World.Position;
        ground = content.Load<Texture2D>("Rpg/Textures/ground");
        walk = content.Load<Texture2D>("Rpg/Textures/player");
        idle = content.Load<Texture2D>("Rpg/Textures/player_idle");
        if (walk.Width != 128 || walk.Height != 128 || idle.Width != 128 || idle.Height != 128)
            throw new InvalidDataException("Library32 requires 4x4 character sheets with 32px frames.");
        font = content.Load<SpriteFont>("Fonts/RpgHud");
        foreach (string name in World.Map.Tiles.Select(tile => tile.Name).Distinct())
        {
            Match animation = Regex.Match(name, @"_anim(\d+)$");
            int frames = animation.Success ? Math.Max(1, int.Parse(animation.Groups[1].Value)) : 1;
            textures.Add(name, (content.Load<Texture2D>("Rpg/Textures/" + name), frames));
        }
        backgroundGroups = World.Map.Tiles.Where(tile => !tile.Foreground).GroupBy(tile => tile.Name).ToArray();
        foreground = World.Map.Tiles.Where(tile => tile.Foreground)
            .OrderBy(tile => tile.Position.Y + textures[tile.Name].Texture.Height).ToArray();
        pixel = new Texture2D(graphics, 1, 1);
        pixel.SetData(new[] { Color.White });
        circle = new Texture2D(graphics, 128, 128);
        var colors = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
                colors[y * 128 + x] = Color.White * MathHelper.Clamp(64 - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64)), 0, 1);
        circle.SetData(colors);
    }

    private float UiWidth => graphics.Viewport.Width / (graphics.Viewport.Height / UiHeight);
    private Vector2 JoystickCenter => new(joystickOnRight ? UiWidth - 100 : 100, UiHeight - 100);
    private Rectangle PauseButton => new((int)UiWidth - 144, 22, 120, 46);
    private Rectangle ResumeButton => new((int)(UiWidth / 2 - 160), 222, 320, 52);
    private Rectangle RestartButton => new((int)(UiWidth / 2 - 160), 292, 320, 52);

    private Rectangle LeftPadButton => new((int)(UiWidth / 2 - 160), 390, 150, 46);
    private Rectangle RightPadButton => new((int)(UiWidth / 2 + 10), 390, 150, 46);

    public void Update(GameTime time, bool active)
    {
        float seconds = (float)time.ElapsedGameTime.TotalSeconds;
        if (!active) { ReleaseInput(); return; }
        KeyboardState keys = Keyboard.GetState();
        GamePadState pad = GamePad.GetState(PlayerIndex.One);
        bool pauseKey = keys.IsKeyDown(Keys.Escape) || keys.IsKeyDown(Keys.P) || pad.IsButtonDown(Buttons.Back) || pad.IsButtonDown(Buttons.Start);
        if (pauseKey && !pauseKeyWasDown) SetPaused(!paused);
        pauseKeyWasDown = pauseKey;

        var touches = TouchPanel.GetState();
        foreach (var touch in touches)
        {
            Vector2 point = touch.Position / (graphics.Viewport.Height / UiHeight);
            if (touch.State == TouchLocationState.Pressed)
            {
                if (!paused && PauseButton.Contains(point)) { SetPaused(true); break; }
                if (paused)
                {
                    if (LeftPadButton.Contains(point) || RightPadButton.Contains(point))
                    {
                        joystickOnRight = RightPadButton.Contains(point);
                        ReleaseInput();
                        if (!saves.SaveJoystickOnRight(joystickOnRight))
                            saveStatus = "No se pudo guardar la preferencia.";
                        break;
                    }
                    if (ResumeButton.Contains(point)) { SetPaused(false); break; }
                    if (RestartButton.Contains(point))
                    {
                        World.Reset();
                        Save();
                        SetPaused(false);
                        break;
                    }
                }
                else if (joystickFinger == null && Vector2.Distance(point, JoystickCenter) <= 92)
                    joystickFinger = touch.Id;
            }
            if (touch.Id == joystickFinger)
            {
                if (touch.State == TouchLocationState.Released) ReleaseInput();
                else
                {
                    joystick = (point - JoystickCenter) / 64;
                    if (joystick.LengthSquared() > 1) joystick.Normalize();
                    if (joystick.LengthSquared() < 0.025f) joystick = Vector2.Zero;
                }
            }
        }
        // A lost/cancelled touch must never leave the player walking indefinitely.
        if (joystickFinger.HasValue && !touches.Any(touch => touch.Id == joystickFinger && touch.State != TouchLocationState.Released))
            ReleaseInput();
        if (paused) return;
        Vector2 input = joystick;
        if (keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left)) input.X -= 1;
        if (keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right)) input.X += 1;
        if (keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up)) input.Y -= 1;
        if (keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down)) input.Y += 1;
        input += new Vector2(pad.ThumbSticks.Left.X, -pad.ThumbSticks.Left.Y);
        World.Update(new NumericsVector(input.X, input.Y), seconds);
        animationTime += Math.Min(seconds, 0.1f);
        saveTimer += seconds;
        if (saveTimer >= 3)
        {
            saveTimer = 0;
            if (lastSavedPosition != World.Position) Save();
        }
    }

    private void ReleaseInput()
    {
        joystickFinger = null;
        joystick = Vector2.Zero;
        World.Stop();
    }

    private void SetPaused(bool value)
    {
        paused = value;
        ReleaseInput();
        if (value) Save();
    }

    public void Suspend() => SetPaused(true);

    public void Save()
    {
        bool success = saves.Save(World);
        saveStatus = success ? "Progreso guardado" : "No se pudo guardar. Reintenta al pausar.";
        if (success) lastSavedPosition = World.Position;
    }

    public void Draw()
    {
        var viewport = graphics.Viewport;
        float zoom = viewport.Height / config.ViewHeight;
        Vector2 camera = new(World.Position.X + 16, World.Position.Y + 16);
        float width = viewport.Width / zoom, height = viewport.Height / zoom;
        Vector2 topLeft = camera - new Vector2(width, height) / 2;
        visibleWorld = new WorldRect(topLeft.X - 1, topLeft.Y - 1, width + 2, height + 2);
        Matrix worldTransform = Matrix.CreateTranslation(-topLeft.X, -topLeft.Y, 0) * Matrix.CreateScale(zoom);
        batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: worldTransform);
        // The original 1024x768 background is not power-of-two in both axes.
        // Draw visible repeats explicitly so older GLES2 phones need no NPOT wrap support.
        int firstX = (int)Math.Floor((topLeft.X + 1000) / ground.Width);
        int firstY = (int)Math.Floor((topLeft.Y + 1000) / ground.Height);
        for (int y = firstY; -1000 + y * ground.Height < topLeft.Y + height; y++)
            for (int x = firstX; -1000 + x * ground.Width < topLeft.X + width; x++)
                batch.Draw(ground, new Vector2(-1000 + x * ground.Width, -1000 + y * ground.Height), Color.White);
        batch.End();

        batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: worldTransform);
        foreach (var group in backgroundGroups)
            foreach (var tile in group) DrawTile(tile);
        // Sort tall props around the player's feet, allowing real shelf aisles:
        // behind a shelf the player is occluded; in front the player stays visible.
        bool playerDrawn = false;
        foreach (var tile in foreground)
        {
            if (!playerDrawn && World.Position.Y + 31 <= tile.Position.Y + textures[tile.Name].Texture.Height)
            {
                DrawPlayer();
                playerDrawn = true;
            }
            DrawTile(tile);
        }
        if (!playerDrawn) DrawPlayer();
        batch.End();

        batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: Matrix.CreateScale(viewport.Height / UiHeight));
        var ink = new Color(235, 225, 211);
        batch.Draw(pixel, new Rectangle(18, 18, 288, 54), new Color(23, 22, 31) * 0.9f);
        Text(config.Name.ToUpperInvariant(), new Vector2(34, 29), ink, 1.05f);
        if (!paused)
        {
            Button(PauseButton, "PAUSA");
            Circle(JoystickCenter, 74, Color.White * 0.2f);
            Circle(JoystickCenter, 69, new Color(26, 23, 35) * 0.8f);
            Circle(JoystickCenter + joystick * 45, 27, new Color(181, 115, 161) * 0.95f);
            Centered("MOVER", new Vector2(JoystickCenter.X, UiHeight - 12), ink, 0.65f);
            Centered("Explora la biblioteca", new Vector2(UiWidth / 2, UiHeight - 26), ink, 0.75f);
        }
        else
        {
            batch.Draw(pixel, new Rectangle(0, 0, (int)UiWidth, (int)UiHeight), Color.Black * 0.65f);
            batch.Draw(pixel, new Rectangle((int)(UiWidth / 2 - 210), 120, 420, 370), new Color(31, 27, 40));
            Centered("PAUSA", new Vector2(UiWidth / 2, 176), ink, 1.4f);
            Button(ResumeButton, "REANUDAR");
            Button(RestartButton, "VOLVER AL INICIO DEL MAPA");
            Centered("ESTABLECER POSICI\u00d3N DEL MANDO", new Vector2(UiWidth / 2, 365), ink, 0.7f);
            Button(LeftPadButton, "IZQUIERDA", !joystickOnRight);
            Button(RightPadButton, "DERECHA", joystickOnRight);
            Centered(saveStatus ?? "", new Vector2(UiWidth / 2, 463), ink, 0.65f);
        }
        batch.End();
    }

    private void DrawPlayer()
    {
        int size = RpgWorld.FrameSize;
        var source = new Rectangle(World.Frame * size, (int)World.Facing * size, size, size);
        batch.Draw(World.Moving ? walk : idle, new Vector2(World.Position.X, World.Position.Y), source, Color.White,
            0, Vector2.Zero, RpgWorld.SpriteScale, SpriteEffects.None, 0);
    }

    private void DrawTile(MapTile tile)
    {
        var (texture, frames) = textures[tile.Name];
        if (!visibleWorld.Intersects(new WorldRect(tile.Position.X, tile.Position.Y, texture.Width / frames, texture.Height))) return;
        Rectangle? source = frames > 1
            ? new Rectangle(((int)(animationTime / 0.1f) % frames) * (texture.Width / frames), 0, texture.Width / frames, texture.Height)
            : null;
        batch.Draw(texture, new Vector2(tile.Position.X, tile.Position.Y), source, Color.White);
    }

    private void Circle(Vector2 center, float radius, Color color) => batch.Draw(circle,
        new Rectangle((int)(center.X - radius), (int)(center.Y - radius), (int)(radius * 2), (int)(radius * 2)), color);
    private void Text(string text, Vector2 position, Color color, float scale) => batch.DrawString(font, text, position, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
    private void Centered(string text, Vector2 center, Color color, float scale) => Text(text, center - font.MeasureString(text) * scale / 2, color, scale);
    private void Button(Rectangle bounds, string text, bool selected = false)
    {
        batch.Draw(pixel, bounds, selected ? new Color(159, 100, 145) : new Color(108, 65, 100));
        if (selected) batch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3), new Color(235, 225, 211));
        float scale = Math.Min(0.8f, (bounds.Width - 20) / Math.Max(1, font.MeasureString(text).X));
        Centered(text, bounds.Center.ToVector2(), Color.White, scale);
    }

    public void Dispose()
    {
        Save();
        batch.Dispose();
        pixel.Dispose();
        circle.Dispose();
    }
}
