#region File Description
//-----------------------------------------------------------------------------
// PlatformerGame.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

using BeatEngine.Core.Game;
using BeatEngine.Core.Game.GameScenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;
using System;
using System.Diagnostics;
using System.IO;

namespace BeatEngine
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class BeatEngineGame : Microsoft.Xna.Framework.Game
    {
        // Resources for drawing.
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        Vector2 baseScreenSize = new Vector2(2664, 1200);
        private Matrix globalTransformation;
        int backbufferWidth, backbufferHeight;
        GameState GameState;

        // Global content.
        private SpriteFont hudFont;

        private Texture2D winOverlay;
        private Texture2D loseOverlay;
        private Texture2D diedOverlay;

        private ISceneBase Scene;
        private bool wasContinuePressed;

        // When the time remaining is less than the warning time, it blinks on the hud
        private static readonly TimeSpan WarningTime = TimeSpan.FromSeconds(30);

        // We store our input states so that we only poll once per frame, 
        // then we use the same input state wherever needed
        private GamePadState gamePadState;
        private KeyboardState keyboardState;
        private TouchCollection touchState;
        private AccelerometerState accelerometerState;
        Stopwatch stopwatch;
        TimeSpan? lastClickTime = null;

        private VirtualGamePad virtualGamePad;

        // The number of levels in the Levels directory of our content. We assume that
        // levels in our content are 0-based and that all numbers under this constant
        // have a level file present. This allows us to not need to check for the file
        // or handle exceptions, both of which can add unnecessary time to level loading.
        private const int numberOfLevels = 3;

        public BeatEngineGame()
        {
            graphics = new GraphicsDeviceManager(this);

#if WINDOWS_PHONE
            TargetElapsedTime = TimeSpan.FromTicks(333333);
#endif
            graphics.IsFullScreen = false;

            graphics.PreferredBackBufferWidth = 2664;
            graphics.PreferredBackBufferHeight = 1200;
            graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;

            Accelerometer2D.Initialize();
            GameState = new GameState();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            this.Content.RootDirectory = "Content";

            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load fonts
            hudFont = Content.Load<SpriteFont>("Fonts/Hud");

            // Load overlay textures
            winOverlay = Content.Load<Texture2D>("Overlays/you_win");
            loseOverlay = Content.Load<Texture2D>("Overlays/you_lose");

            ScalePresentationArea();

            virtualGamePad = new VirtualGamePad(baseScreenSize, globalTransformation);

            if (!OperatingSystem.IsIOS())
            {
                //Known issue that you get exceptions if you use Media PLayer while connected to your PC
                //See http://social.msdn.microsoft.com/Forums/en/windowsphone7series/thread/c8a243d2-d360-46b1-96bd-62b1ef268c66
                //Which means its impossible to test this from VS.
                //So we have to catch the exception and throw it away
                try
                {
                    //MediaPlayer.IsRepeating = true;
                    //stopwatch = Stopwatch.StartNew();

                    //MediaPlayer.Play(Content.Load<Song>("Sounds/ElectricSunshine"));
                }
                catch { }
            }
            LoadScene();
            //LoadNextLevel();
        }

        public void ScalePresentationArea()
        {
            //Work out how much we need to scale our graphics to fill the screen
            backbufferWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            backbufferHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
            float horScaling = backbufferWidth / baseScreenSize.X;
            float verScaling = backbufferHeight / baseScreenSize.Y;
            Vector3 screenScalingFactor = new Vector3(horScaling, verScaling, 1);
            globalTransformation = Matrix.CreateScale(screenScalingFactor);
            var vp = GraphicsDevice.Viewport;
            System.Diagnostics.Debug.WriteLine("Screen Size - Width[" + GraphicsDevice.PresentationParameters.BackBufferWidth + "] Height [" + GraphicsDevice.PresentationParameters.BackBufferHeight + "]");
        }

        
        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            //Confirm the screen has not been resized by the user
            if (backbufferHeight != GraphicsDevice.PresentationParameters.BackBufferHeight ||
                backbufferWidth != GraphicsDevice.PresentationParameters.BackBufferWidth)
            {
                ScalePresentationArea();
            }
            // Handle polling for our input and handling high-level input
            HandleInput(gameTime);

            // update our level, passing down the GameTime along with all of our input states
            Scene.Update(gameTime, keyboardState, gamePadState, 
                         accelerometerState, touchState, Window.CurrentOrientation);

            base.Update(gameTime);
        }

        private void HandleInput(GameTime gameTime)
        {
            // get all of our input states
            keyboardState = Keyboard.GetState();
            touchState = TouchPanel.GetState();
            gamePadState = virtualGamePad.GetState(touchState, GamePad.GetState(PlayerIndex.One), ref stopwatch, lastClickTime, Content);
            accelerometerState = Accelerometer2D.GetState();

            if (!OperatingSystem.IsIOS())
            {
                // Exit the game when back is pressed.
                if (gamePadState.Buttons.Back == ButtonState.Pressed)
                    Exit();
            }

            bool continuePressed =
                keyboardState.IsKeyDown(Keys.Space) ||
                gamePadState.IsButtonDown(Buttons.A) ||
                touchState.AnyTouch();

            wasContinuePressed = continuePressed;

            virtualGamePad.Update(gameTime);
        }

        private void LoadLevel()
        {
            //GameState.Level += 1; //or assign int ID according to song chosen by user

            // Unloads the content for the current level before loading the next one.
            if (Scene != null)
                Scene.Dispose();

            // Load the level.
            string levelPath = string.Format("Content/Levels/{0}.txt", GameState.Level);
            using (Stream fileStream = TitleContainer.OpenStream(levelPath))
                Scene = new Level(Services, fileStream, GameState.Level, globalTransformation, GameState);
        }

        private void LoadStartScene()
        {
            if (Scene != null)
                Scene.Dispose();

            // Load the level.
            string startScenePath = string.Format("Content/Scenes/StartScene.txt", GameState.Level);
            using (Stream fileStream = TitleContainer.OpenStream(startScenePath))
                Scene = new StartGameScene(Services, fileStream, GameState.Level, globalTransformation, GameState);
        }

        private void LoadScene()
        {
            if(GameState.Level == 0)
            {
                LoadStartScene();
            }
            else
            {
                LoadLevel();
            }
        }

        /// <summary>
        /// Draws the game from background to foreground.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            graphics.GraphicsDevice.Clear(Color.Magenta);

            spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null,null, globalTransformation);

            Scene.Draw(gameTime, spriteBatch);

            DrawHud();

            spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawHud()
        {
           
            if (touchState.IsConnected)
                virtualGamePad.Draw(spriteBatch);
        }

        private void DrawShadowedString(SpriteFont font, string value, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, value, position + new Vector2(1.0f, 1.0f), Color.Black);
            spriteBatch.DrawString(font, value, position, color);
        }
    }
}
