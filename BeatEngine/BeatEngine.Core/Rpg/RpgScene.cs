using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
    private readonly Dictionary<string, Texture2D> portraits = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Rectangle, float> buttonPulses = new();
    private readonly Dictionary<int, Rectangle> buttonTouches = new();
    private readonly HashSet<Rectangle> heldButtons = new();
    private readonly RpgSaveStore saves = new();
    private readonly RpgSceneConfig config;
    private readonly IGrouping<string, MapTile>[] backgroundGroups;
    private readonly MapTile[] foreground;
    private WorldRect visibleWorld;
    public RpgWorld World { get; }
    private bool paused, pauseKeyWasDown;
    private readonly RpgConversation conversation;
    private readonly RpgCheckpoints checkpoints;
    private readonly GameWindow window;
    private string checkpointText = "inicio";
    private bool checkpointEditing, replaceCheckpointText;
    private Task<string> checkpointInputTask;
    private int? runFinger;
    private bool running, actionKeyWasDown;
    private int? joystickFinger;
    private Vector2 joystick;
    private float animationTime, saveTimer;
    private NumericsVector lastSavedPosition;
    private string saveStatus;

    public RpgScene(GraphicsDevice graphics, ContentManager content, GameWindow window)
    {
        this.graphics = graphics;
        this.window = window;
        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS()) SubscribeTextInput();
        batch = new SpriteBatch(graphics);
        using (var stream = TitleContainer.OpenStream("Content/Rpg/scene.json"))
            config = RpgSceneConfig.Load(stream);
        using (var stream = TitleContainer.OpenStream("Content/Rpg/Maps/test.map"))
        using (var reader = new StreamReader(stream))
            World = new RpgWorld(RpgMap.Load(reader, new NumericsVector(config.Spawn[0], config.Spawn[1]),
                new WorldRect(config.Bounds[0], config.Bounds[1], config.Bounds[2], config.Bounds[3])));
        saves.Load(World);
        if (!string.IsNullOrEmpty(config.Dialogue))
            using (var stream = TitleContainer.OpenStream("Content/Rpg/" + config.Dialogue))
                conversation = RpgConversation.Load(stream);
        if (!string.IsNullOrEmpty(config.Checkpoints))
            using (var stream = TitleContainer.OpenStream("Content/Rpg/" + config.Checkpoints))
                checkpoints = RpgCheckpoints.Load(stream);
        checkpointText = World.Checkpoint ?? "inicio";
        lastSavedPosition = World.Position;
        ground = content.Load<Texture2D>("Rpg/Textures/ground");
        walk = content.Load<Texture2D>("Rpg/Textures/player");
        idle = content.Load<Texture2D>("Rpg/Textures/player_idle");
        if (walk.Width != 128 || walk.Height != 128 || idle.Width != 128 || idle.Height != 128)
            throw new InvalidDataException("Library32 requires 4x4 character sheets with 32px frames.");
        font = content.Load<SpriteFont>("Fonts/RpgHud");
        if (config.Portraits != null)
            foreach (var portrait in config.Portraits)
                portraits[portrait.Key] = content.Load<Texture2D>("Rpg/" + Path.ChangeExtension(portrait.Value, null));
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
    private Vector2 JoystickCenter => new(100, UiHeight - 100);
    private Rectangle PauseButton => new((int)UiWidth - 144, 22, 120, 46);
    private Rectangle ResumeButton => new((int)(UiWidth / 2 - 250), 145, 500, 50);
    private Rectangle RestartButton => new((int)(UiWidth / 2 - 250), 210, 500, 50);

    private Rectangle CheckpointInput => new((int)(UiWidth / 2 - 250), 314, 350, 48);
    private Rectangle CheckpointApply => new((int)(UiWidth / 2 + 110), 314, 140, 48);
    private Rectangle ExploreButton => new((int)UiWidth - 180, (int)UiHeight - 265, 150, 62);
    private Rectangle RunButton => new((int)UiWidth - 180, (int)UiHeight - 185, 150, 62);
    private Rectangle TalkButton => new((int)UiWidth - 180, (int)UiHeight - 105, 150, 62);
    private Rectangle ContinueButton => new((int)UiWidth - 222, 462, 156, 42);

    private void Pulse(Rectangle bounds) => buttonPulses[bounds] = 0.16f;

    private void CaptureButton(int finger, Vector2 point)
    {
        Rectangle[] visible = paused ? new[] { ResumeButton, RestartButton, CheckpointInput, CheckpointApply }
            : conversation?.Active == true ? new[] { PauseButton, ContinueButton }
            : new[] { PauseButton, ExploreButton, RunButton, TalkButton };
        foreach (var bounds in visible)
            if (bounds.Contains(point))
            {
                buttonTouches[finger] = bounds;
                heldButtons.Add(bounds);
                Pulse(bounds);
                break;
            }
    }

    private void AdvanceConversation()
    {
        ReleaseInput();
        if (conversation?.Continue(World) == true) Save();
    }

    private void Talk()
    {
        if (conversation?.TryStart(World) == true) ReleaseInput();
    }

    public void Update(GameTime time, bool active)
    {
        float seconds = (float)time.ElapsedGameTime.TotalSeconds;
        if (checkpointInputTask?.IsCompleted == true)
        {
            try
            {
                string result = checkpointInputTask.GetAwaiter().GetResult();
                if (result != null) checkpointText = result.Trim();
            }
            catch (Exception) { saveStatus = "No se pudo abrir el teclado."; }
            checkpointInputTask = null;
            checkpointEditing = false;
        }
        if (!active)
        {
            ReleaseInput();
            heldButtons.Clear();
            buttonTouches.Clear();
            buttonPulses.Clear();
            return;
        }
        foreach (var bounds in buttonPulses.Keys.ToArray())
        {
            buttonPulses[bounds] -= Math.Max(0, seconds);
            if (buttonPulses[bounds] <= 0) buttonPulses.Remove(bounds);
        }
        KeyboardState keys = Keyboard.GetState();
        GamePadState pad = GamePad.GetState(PlayerIndex.One);
        bool pauseKey = keys.IsKeyDown(Keys.Escape) || keys.IsKeyDown(Keys.P) || pad.IsButtonDown(Buttons.Back) || pad.IsButtonDown(Buttons.Start);
        if (pauseKey && !pauseKeyWasDown && !checkpointEditing) SetPaused(!paused);
        pauseKeyWasDown = pauseKey;
        bool actionKey = keys.IsKeyDown(Keys.E) || keys.IsKeyDown(Keys.Enter) || pad.IsButtonDown(Buttons.A);
        bool newAction = actionKey && !actionKeyWasDown;
        actionKeyWasDown = actionKey;
        bool consumedDialogue = false;
        if (!paused && newAction)
        {
            consumedDialogue = conversation?.Active == true;
            Pulse(consumedDialogue ? ContinueButton : TalkButton);
            if (consumedDialogue) AdvanceConversation();
            else Talk();
        }

        var touches = TouchPanel.GetState();
        heldButtons.Clear();
        foreach (var capture in buttonTouches.ToArray())
        {
            var touch = touches.FirstOrDefault(item => item.Id == capture.Key);
            if (touch.State == TouchLocationState.Invalid || touch.State == TouchLocationState.Released)
                buttonTouches.Remove(capture.Key);
            else if (capture.Value.Contains(touch.Position / (graphics.Viewport.Height / UiHeight)))
                heldButtons.Add(capture.Value);
        }
        foreach (var touch in touches)
        {
            Vector2 point = touch.Position / (graphics.Viewport.Height / UiHeight);
            if (touch.State == TouchLocationState.Pressed)
            {
                // Press feedback is independent of whether this button currently has an action.
                CaptureButton(touch.Id, point);
                if (!paused && PauseButton.Contains(point)) { SetPaused(true); break; }
                if (paused)
                {
                    if (CheckpointInput.Contains(point)) { BeginCheckpointInput(); break; }
                    if (CheckpointApply.Contains(point)) { ApplyCheckpoint(); break; }
                    if (ResumeButton.Contains(point)) { SetPaused(false); break; }
                    if (RestartButton.Contains(point))
                    {
                        World.Reset();
                        Save();
                        SetPaused(false);
                        break;
                    }
                    continue;
                }
                if (conversation?.Active == true)
                {
                    if (ContinueButton.Contains(point) && !consumedDialogue)
                    {
                        consumedDialogue = true;
                        AdvanceConversation();
                    }
                    continue;
                }
                if (consumedDialogue) continue;
                if (ExploreButton.Contains(point)) continue; // Reserved for future exploration actions; press feedback is still active.
                if (TalkButton.Contains(point)) { Talk(); continue; }
                if (RunButton.Contains(point)) { runFinger = touch.Id; continue; }
                if (joystickFinger == null && Vector2.Distance(point, JoystickCenter) <= 92)
                    joystickFinger = touch.Id;
            }
            if (touch.Id == runFinger && (touch.State == TouchLocationState.Released || !RunButton.Contains(point)))
                runFinger = null;
            if (touch.Id == joystickFinger)
            {
                if (touch.State == TouchLocationState.Released)
                {
                    joystickFinger = null;
                    joystick = Vector2.Zero;
                }
                else
                {
                    joystick = (point - JoystickCenter) / 64;
                    if (joystick.LengthSquared() > 1) joystick.Normalize();
                    if (joystick.LengthSquared() < 0.025f) joystick = Vector2.Zero;
                }
            }
        }
        if (joystickFinger.HasValue && !touches.Any(touch => touch.Id == joystickFinger && touch.State != TouchLocationState.Released))
        {
            joystickFinger = null;
            joystick = Vector2.Zero;
        }
        if (runFinger.HasValue && !touches.Any(touch => touch.Id == runFinger && touch.State != TouchLocationState.Released))
            runFinger = null;
        if (paused || conversation?.Active == true || consumedDialogue)
        {
            ReleaseInput();
            return;
        }
        Vector2 input = joystick;
        if (keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left)) input.X -= 1;
        if (keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right)) input.X += 1;
        if (keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up)) input.Y -= 1;
        if (keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.Down)) input.Y += 1;
        input += new Vector2(pad.ThumbSticks.Left.X, -pad.ThumbSticks.Left.Y);
        running = runFinger.HasValue || keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift) || pad.IsButtonDown(Buttons.LeftShoulder);
        World.Update(new NumericsVector(input.X, input.Y), seconds, running);
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
        runFinger = null;
        running = false;
        joystick = Vector2.Zero;
        World.Stop();
    }

    private void SetPaused(bool value)
    {
        if (value && !paused && !checkpointEditing) checkpointText = World.Checkpoint ?? "inicio";
        paused = value;
        if (!paused) checkpointEditing = false;
        ReleaseInput();
        if (value) Save();
    }

    public void Suspend() => SetPaused(true);

    public bool Save()
    {
        bool success = saves.Save(World);
        saveStatus = success ? "Progreso guardado" : "No se pudo guardar. Reintenta al pausar.";
        if (success) lastSavedPosition = World.Position;
        return success;
    }

    private void BeginCheckpointInput()
    {
        if (checkpointInputTask != null) return;
        checkpointEditing = true;
        replaceCheckpointText = true;
        ReleaseInput();
        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS()) return;
        try
        {
            checkpointInputTask = KeyboardInput.Show("Set checkpoint to:", "", checkpointText, false);
        }
        catch (Exception)
        {
            checkpointInputTask = null;
            checkpointEditing = false;
            saveStatus = "No se pudo abrir el teclado. Intentalo de nuevo.";
        }
    }

    // Android's MonoGame assembly does not expose GameWindow.TextInput. Keep the
    // desktop event references out of methods that the mobile runtime compiles.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void SubscribeTextInput() => window.TextInput += OnCheckpointText;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void UnsubscribeTextInput() => window.TextInput -= OnCheckpointText;

    private void OnCheckpointText(object sender, TextInputEventArgs args)
    {
        if (!paused || !checkpointEditing || checkpointInputTask != null) return;
        char character = args.Character;
        if (character == '\r' || character == '\n') { checkpointEditing = false; return; }
        if (character == '\b')
        {
            if (replaceCheckpointText) checkpointText = "";
            else if (checkpointText.Length > 0) checkpointText = checkpointText.Substring(0, checkpointText.Length - 1);
            replaceCheckpointText = false;
            return;
        }
        if (!char.IsControl(character) && checkpointText.Length < 80)
        {
            if (replaceCheckpointText) checkpointText = "";
            replaceCheckpointText = false;
            checkpointText += character;
        }
    }

    private void ApplyCheckpoint()
    {
        if (checkpointInputTask != null) return;
        checkpointEditing = false;
        if (checkpoints == null || !checkpoints.TryRestore(checkpointText, World, out string id))
        {
            saveStatus = "Checkpoint desconocido. Revisa el nombre o ID.";
            return;
        }
        conversation?.Cancel();
        ReleaseInput();
        checkpointText = id;
        animationTime = saveTimer = 0;
        if (Save()) SetPaused(false);
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
            if (conversation?.Active == true) DrawConversation(ink);
            else
            {
                Circle(JoystickCenter, 74, Color.White * 0.2f);
                Circle(JoystickCenter, 69, new Color(26, 23, 35) * 0.8f);
                Circle(JoystickCenter + joystick * 45, 27, new Color(181, 115, 161) * 0.95f);
                Centered("MOVER", new Vector2(JoystickCenter.X, UiHeight - 12), ink, 0.65f);
                Button(ExploreButton, "Explorar");
                Button(RunButton, "Correr", running);
                Button(TalkButton, "Hablar");
                string objective = conversation != null && World.CompletedScenes.Contains(conversation.Id)
                    ? conversation.Objective : "Explora la biblioteca";
                Centered(objective, new Vector2(UiWidth / 2, UiHeight - 26), ink, 0.65f);
            }
        }
        else
        {
            batch.Draw(pixel, new Rectangle(0, 0, (int)UiWidth, (int)UiHeight), Color.Black * 0.65f);
            batch.Draw(pixel, new Rectangle((int)(UiWidth / 2 - 300), 70, 600, 410), new Color(31, 27, 40));
            Centered("PAUSA", new Vector2(UiWidth / 2, 105), ink, 1.4f);
            Button(ResumeButton, "REANUDAR");
            Button(RestartButton, "VOLVER AL INICIO DEL MAPA");
            Text("Set checkpoint to:", new Vector2(UiWidth / 2 - 250, 280), ink, 0.8f);
            Button(CheckpointInput, checkpointText.Length > 0 ? checkpointText : "Nombre o ID", checkpointEditing);
            Button(CheckpointApply, "Aplicar");
            string status = saveStatus ?? "";
            Centered(status, new Vector2(UiWidth / 2, 443), ink, Math.Min(0.65f, 550f / Math.Max(1, font.MeasureString(status).X)));
        }
        batch.End();
        if (!paused && conversation?.Active == true && portraits.TryGetValue(conversation.Current.Speaker, out var portrait))
        {
            batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateScale(viewport.Height / UiHeight));
            batch.Draw(portrait, new Rectangle(62, 345, 96, 96), Color.White);
            batch.End();
        }
    }

    private void DrawConversation(Color ink)
    {
        var panel = new Rectangle(40, 318, (int)UiWidth - 80, 202);
        batch.Draw(pixel, panel, new Color(31, 27, 40) * 0.98f);
        batch.Draw(pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(181, 115, 161));
        bool hasPortrait = portraits.ContainsKey(conversation.Current.Speaker);
        float textX = hasPortrait ? 184 : 62;
        if (hasPortrait)
        {
            batch.Draw(pixel, new Rectangle(58, 341, 104, 104), new Color(90, 64, 81));
            batch.Draw(pixel, new Rectangle(60, 343, 100, 100), new Color(44, 35, 48));
        }
        Text(conversation.Current.Speaker, new Vector2(textX, 331), new Color(230, 182, 111), 1f);
        float scale = 0.85f;
        List<string> lines;
        do
        {
            lines = WrapDialogue(conversation.Current.Text, panel.Right - textX - 22, scale);
            if (lines.Count * (font.LineSpacing * scale + 4) <= 88 || scale <= 0.55f) break;
            scale -= 0.05f;
        } while (true);
        float y = 363;
        foreach (string line in lines)
        {
            Text(line, new Vector2(textX, y), ink, scale);
            y += font.LineSpacing * scale + 4;
        }
        Button(ContinueButton, "Continuar");
        Text($"{conversation.LineIndex + 1} / {conversation.LineCount}", new Vector2(62, 476), ink, 0.6f);
    }

    private List<string> WrapDialogue(string text, float width, float scale)
    {
        var lines = new List<string>();
        string line = "";
        foreach (string word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && font.MeasureString(candidate).X * scale > width)
            {
                lines.Add(line);
                line = word;
            }
            else line = candidate;
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
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
        bool pressed = heldButtons.Contains(bounds) || buttonPulses.ContainsKey(bounds);
        var face = bounds;
        batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y + 4, bounds.Width, bounds.Height), new Color(48, 29, 48));
        if (pressed) { face.Y += 3; face.Height -= 3; }
        batch.Draw(pixel, face, pressed ? new Color(184, 116, 165)
            : selected ? new Color(159, 100, 145) : new Color(108, 65, 100));
        if (pressed)
        {
            var edge = new Color(239, 195, 224);
            batch.Draw(pixel, new Rectangle(face.X, face.Y, face.Width, 2), edge);
            batch.Draw(pixel, new Rectangle(face.X, face.Y, 2, face.Height), edge);
            batch.Draw(pixel, new Rectangle(face.Right - 2, face.Y, 2, face.Height), edge);
            batch.Draw(pixel, new Rectangle(face.X, face.Bottom - 2, face.Width, 2), edge);
        }
        else if (selected) batch.Draw(pixel, new Rectangle(face.X, face.Bottom - 3, face.Width, 3), new Color(235, 225, 211));
        float scale = Math.Min(0.8f, (face.Width - 20) / Math.Max(1, font.MeasureString(text).X));
        Centered(text, face.Center.ToVector2(), Color.White, scale);
    }

    public void Dispose()
    {
        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS()) UnsubscribeTextInput();
        Save();
        batch.Dispose();
        pixel.Dispose();
        circle.Dispose();
    }
}
