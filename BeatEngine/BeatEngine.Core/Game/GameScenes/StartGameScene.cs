#region File Description
//-----------------------------------------------------------------------------
// Level.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

using BeatEngine.Core.Game;
using BeatEngine.Core.Game.GameScenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;
using MonoGame.Framework.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.IO;
using static System.Net.WebRequestMethods;

namespace BeatEngine
{
    class StartGameScene : ISceneBase
    {
        private Tile[,] buttons;
        private Texture2D[] layers;
        // The layer which entities are drawn on top of.
        private const int EntityLayer = 2;

        private Matrix globalTransformation;

        // Entities in the level.
        public Player Player
        {
            get { return player; }
        }
        Player player;

        // Key locations in the level.        
        private Vector2 start;
        private Point exit = InvalidPosition;
        private static readonly Point InvalidPosition = new Point(-1, -1);

        public int Score
        {
            get { return score; }
        }
        int score;

        public bool ReachedExit
        {
            get { return reachedExit; }
        }
        bool reachedExit;

        //public TimeSpan TimeRemaining
        //{
        //    get { return timeRemaining; }
        //}
        //TimeSpan timeRemaining;


        // Level content.        
        public ContentManager Content
        {
            get { return content; }
        }
        ContentManager content;

        private SoundEffect StartButtonPressedSound;

        private bool StartButtonPressed;

        #region Loading

        /// <summary>
        /// Constructs a new level.
        /// </summary>
        /// <param name="serviceProvider">
        /// The service provider that will be used to construct a ContentManager.
        /// </param>
        /// <param name="fileStream">
        /// A stream containing the tile data.
        /// </param>
        public StartGameScene(IServiceProvider serviceProvider, Stream fileStream, int levelIndex, Matrix globalTransformation, GameState gameState)
        {
            // Create a new content manager to load content used just by this level.
            content = new ContentManager(serviceProvider, "Content");

            LoadButtons(fileStream);
            PositionTiles();

            // Load background layer textures. For now, all levels must
            // use the same backgrounds and only use the left-most part of them.
            layers = new Texture2D[3];
            for (int i = 0; i < layers.Length; ++i)
            {
                // Choose a random segment if each background layer for level variety.
                int segmentIndex = levelIndex;
                layers[i] = Content.Load<Texture2D>("NewBackgrounds/Layer" + 0 + "_" + i);
            }

            // Load sounds.
            StartButtonPressedSound = Content.Load<SoundEffect>("Sounds/GemCollected");
            this.globalTransformation = Matrix.Invert(globalTransformation);
            //Content.Load<Song>("Sounds/ElectricSunshine");
        }

        /// <summary>
        /// Iterates over every tile in the structure file and loads its
        /// appearance and behavior. This method also validates that the
        /// file is well-formed with a player start point, exit, etc.
        /// </summary>
        /// <param name="fileStream">
        /// A stream containing the tile data.
        /// </param>
        private void LoadButtons(Stream fileStream)
        {
            // Load the level and ensure all of the lines are the same length.
            int width;
            List<string> lines = new List<string>();
            using (StreamReader reader = new StreamReader(fileStream))
            {
                string line = reader.ReadLine();
                width = line.Length;
                while (line != null)
                {
                    lines.Add(line);
                    if (line.Length != width)
                        throw new Exception(String.Format("The length of line {0} is different from all preceeding lines.", lines.Count));
                    line = reader.ReadLine();
                }
            }

            // Allocate the tile grid.
            buttons = new Tile[width, lines.Count];

            // Loop over every tile position,
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    // to load each tile.
                    char tileType = lines[y][x];
                    buttons[x, y] = LoadButton(tileType, x, y);
                }
            }

        }

        /// <summary>
        /// Loads an individual tile's appearance and behavior.
        /// </summary>
        /// <param name="tileType">
        /// The character loaded from the structure file which
        /// indicates what should be loaded.
        /// </param>
        /// <param name="x">
        /// The X location of this tile in tile space.
        /// </param>
        /// <param name="y">
        /// The Y location of this tile in tile space.
        /// </param>
        /// <returns>The loaded tile.</returns>
        private Tile LoadButton(char tileType, int x, int y)
        {
            switch (tileType)
            {
                case 'S':
                    return LoadButton("Start_button", TileCollision.Platform);

                case 'E':
                    return LoadButton("Exit_button", TileCollision.Platform);

                // Unknown tile type character
                default:
                    throw new NotSupportedException(String.Format("Unsupported tile type character '{0}' at position {1}, {2}.", tileType, x, y));
            }
        }

        /// <summary>
        /// Creates a new tile. The other tile loading methods typically chain to this
        /// method after performing their special logic.
        /// </summary>
        /// <param name="name">
        /// Path to a tile texture relative to the Content/Tiles directory.
        /// </param>
        /// <param name="collision">
        /// The tile collision type for the new tile.
        /// </param>
        /// <returns>The new tile.</returns>
        private Tile LoadButton(string name, TileCollision collision)
        {
            return new Tile(Content.Load<Texture2D>("UI/Buttons/" + name), collision);
        }


        /// <summary>
        /// Unloads the level content.
        /// </summary>
        public void Dispose()
        {
            Content.Unload();
        }

        #endregion

        #region Bounds and collision

        public int Width
        {
            get { return buttons.GetLength(0); }
        }

        public int Height
        {
            get { return buttons.GetLength(1); }
        }

        #endregion

        #region Update
        public void Update(
            GameTime gameTime,
            KeyboardState keyboardState,
            GamePadState gamePadState,
            AccelerometerState accelState,
            TouchCollection touchCollection,
            DisplayOrientation orientation)
        {
            CheckIfTileIsPressed(touchCollection);

        }


        #endregion

        #region Draw

        /// <summary>
        /// Draw everything in the level from background to foreground.
        /// </summary>
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            for (int i = 0; i <= EntityLayer; ++i)
                spriteBatch.Draw(layers[i], Vector2.Zero, Color.White);

            DrawTiles(spriteBatch);

            for (int i = EntityLayer + 1; i < layers.Length; ++i)
                spriteBatch.Draw(layers[i], Vector2.Zero, Color.White);
        }

        /// <summary>
        /// Draws each tile in the level.
        /// </summary>
        private void DrawTiles(SpriteBatch spriteBatch)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    // If there is a visible tile in that position
                    Texture2D texture = buttons[x, y].Texture;
                    if (texture != null)
                    {
                        // Draw it in screen space.
                        Color tint = Color.White;

                        if (buttons[x, y].IsPressed)
                        {
                            tint = Color.DarkOrchid; //Color.MonoGameOrange, Color.DarkOrange also good candidates

                        }

                        spriteBatch.Draw(texture, buttons[x, y].Position, tint);
                    }
                }
            }
        }

        private void PositionTiles()
        {
            int initialPosX = 1334;
            int initialPosY = 415;

            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    //If there is a visible tile in that position
                    Texture2D texture = buttons[x, y].Texture;
                    if (texture != null)
                    {
                        //Draw it in screen space.
                        Vector2 position = new Vector2(initialPosX, initialPosY);
                        buttons[x, y].Position = position;

                    }
                    initialPosY -= 300;

                }
                initialPosY = 415;
                initialPosX -= 300;
            }


        }

        private void CheckIfTileIsPressed(TouchCollection touchLocations)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    buttons[x, y].IsPressed = false;

                    foreach (var touch in touchLocations)
                    {
                        Vector2 pos = touch.Position;
                        Vector2.Transform(ref pos, ref globalTransformation, out pos);

                        if (touch.State == TouchLocationState.Moved || touch.State == TouchLocationState.Pressed)
                        {
                            if (buttons[x, y].BoundingRectangle.Contains(pos))
                            {
                                buttons[x, y].IsPressed = true;
                            }
                        }

                    }

                }
            }
        }

        #endregion
    }
}