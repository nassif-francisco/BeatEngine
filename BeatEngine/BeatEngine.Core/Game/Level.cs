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
using System.Reflection.Emit;
using static System.Net.WebRequestMethods;

namespace BeatEngine
{
    /// <summary>
    /// A uniform grid of tiles with collections of gems and enemies.
    /// The level owns the player and controls the game's win and lose
    /// conditions as well as scoring.
    /// </summary>
    class Level : ISceneBase
    {
        // Physical structure of the level.
        private Tile[,] tiles;
        private Texture2D[] layers;
        // The layer which entities are drawn on top of.
        private const int EntityLayer = 2;

        Accelerometer accelerometer;
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

        private SpriteFont hudFont;

        // Level game state.
        private Random random = new Random(354668); // Arbitrary, but constant seed

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

        private SoundEffect exitReachedSound;

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
        public Level(IServiceProvider serviceProvider, Stream fileStream, int levelIndex, Matrix globalTransformation, GameState gameState)
        {
            // Create a new content manager to load content used just by this level.
            content = new ContentManager(serviceProvider, "Content");

            LoadTiles(fileStream);
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
            exitReachedSound = Content.Load<SoundEffect>("Sounds/ExitReached");
            this.globalTransformation = Matrix.Invert(globalTransformation);

            hudFont = Content.Load<SpriteFont>("Fonts/Hud");
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
        private void LoadTiles(Stream fileStream)
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
            tiles = new Tile[width, lines.Count];

            // Loop over every tile position,
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    // to load each tile.
                    char tileType = lines[y][x];
                    tiles[x, y] = LoadTile(tileType, x, y);
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
        private Tile LoadTile(char tileType, int x, int y)
        {
            switch (tileType)
            {
                case 'B':
                    return LoadTile("blue", TileCollision.Platform);

                case 'M':
                    return LoadTile("magenta", TileCollision.Platform);

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
        private Tile LoadTile(string name, TileCollision collision)
        {
            return new Tile(Content.Load<Texture2D>("Tiles/" + name), collision, Content);
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

        /// <summary>
        /// Gets the collision mode of the tile at a particular location.
        /// This method handles tiles outside of the levels boundries by making it
        /// impossible to escape past the left or right edges, but allowing things
        /// to jump beyond the top of the level and fall off the bottom.
        /// </summary>
        public TileCollision GetCollision(int x, int y)
        {
            // Prevent escaping past the level ends.
            if (x < 0 || x >= Width)
                return TileCollision.Impassable;
            // Allow jumping past the level top and falling through the bottom.
            if (y < 0 || y >= Height)
                return TileCollision.Passable;

            return tiles[x, y].Collision;
        }

        /// <summary>
        /// Gets the bounding rectangle of a tile in world space.
        /// </summary>        

        /// <summary>
        /// Width of level measured in tiles.
        /// </summary>
        public int Width
        {
            get { return tiles.GetLength(0); }
        }

        /// <summary>
        /// Height of the level measured in tiles.
        /// </summary>
        public int Height
        {
            get { return tiles.GetLength(1); }
        }

        #endregion

        #region Update

        /// <summary>
        /// Updates all objects in the world, performs collision between them,
        /// and handles the time limit with scoring.
        /// </summary>
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

            DrawTiles(gameTime, spriteBatch);
            DrawFX(gameTime, spriteBatch);

            for (int i = EntityLayer + 1; i < layers.Length; ++i)
                spriteBatch.Draw(layers[i], Vector2.Zero, Color.White);
            DrawShadowedString(hudFont, "SCORE: ", new Vector2(500, 300), Color.DarkMagenta, spriteBatch);
        }

        private void DrawShadowedString(SpriteFont font, string value, Vector2 position, Color color, SpriteBatch spriteBatch)
        {
            spriteBatch.DrawString(font, value, position + new Vector2(1.0f, 1.0f), color, 0, new Vector2(1.0f, 1.0f), 4, SpriteEffects.None, 1);
            //spriteBatch.DrawString(font, value, position, color);
        }

        /// <summary>
        /// Draws each tile in the level.
        /// </summary>
        private void DrawTiles(GameTime gameTime, SpriteBatch spriteBatch)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    // If there is a visible tile in that position
                    Texture2D texture = tiles[x, y].Texture;
                    if (texture != null)
                    {
                        // Draw it in screen space.
                        Color tint = Color.White;

                        if (tiles[x, y].IsPressed)
                        {
                            tint = Color.DarkOrchid; //Color.MonoGameOrange, Color.DarkOrange also good candidates
                        }

                        spriteBatch.Draw(texture, tiles[x, y].Position, tint);
                    }
                }
            }
        }

        private void DrawFX(GameTime gameTime, SpriteBatch spriteBatch)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    // If there is a visible tile in that position
                    Texture2D texture = tiles[x, y].Texture;
                    if (texture != null)
                    {
                        if (tiles[x, y].IsPressed)
                        {
                            tiles[x, y].Hit.BeginAnimation();
                        }

                        else
                        {
                            tiles[x, y].Hit.Update(gameTime);
                        }

                        if (tiles[x, y].Hit.IsAnimationStillPlaying)
                        {
                            tiles[x, y].Hit.Draw(gameTime, spriteBatch);
                        }
                    }
                }
            }
        }

        private void PositionTiles()
        {
            int initialPosY = 2334;
            int initialPosX = 915;

            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    //If there is a visible tile in that position
                    Texture2D texture = tiles[x, y].Texture;
                    if (texture != null)
                    {
                        //Draw it in screen space.
                        Vector2 position = new Vector2(initialPosX, initialPosY);
                        tiles[x, y].Position = position;

                    }
                    initialPosX -= 300;

                }
                initialPosX = 915;
                initialPosY -= 300;
            }
        }

        private void CheckIfTileIsPressed(TouchCollection touchLocations)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    tiles[x, y].IsPressed = false;

                    foreach (var touch in touchLocations)
                    {
                        Vector2 pos = touch.Position;
                        Vector2.Transform(ref pos, ref globalTransformation, out pos);

                        if (touch.State == TouchLocationState.Moved || touch.State == TouchLocationState.Pressed)
                        {
                            if (tiles[x, y].BoundingRectangle.Contains(pos))
                            {
                                tiles[x, y].IsPressed = true;
                            }
                        }

                    }

                }
            }
        }

        #endregion
    }
}