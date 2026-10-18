using BeatEngine.Rpg;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace BeatEngine;

// Retain the shared entry point used by the existing platform launchers.
public sealed class BeatEngineGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private RpgScene scene;

    public BeatEngineGame()
    {
        Content.RootDirectory = "Content";
        Window.Title = "RPG Prototype";
        graphics = new GraphicsDeviceManager(this)
        {
            IsFullScreen = !System.OperatingSystem.IsWindows(),
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight
        };
        if (System.OperatingSystem.IsAndroid() || System.OperatingSystem.IsIOS())
        {
            var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            graphics.PreferredBackBufferWidth = System.Math.Max(display.Width, display.Height);
            graphics.PreferredBackBufferHeight = System.Math.Min(display.Width, display.Height);
        }
        Window.AllowUserResizing = true;
        TouchPanel.EnableMouseTouchPoint = true;
        IsMouseVisible = true;
        Deactivated += (_, _) => scene?.Suspend();
        Exiting += (_, _) => scene?.Save();
    }

    protected override void LoadContent()
    {
        scene = new RpgScene(GraphicsDevice, Content);
    }

    protected override void Update(GameTime gameTime)
    {
        scene.Update(gameTime, IsActive);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(25, 23, 31));
        scene.Draw();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        scene?.Dispose();
        base.UnloadContent();
    }
}
