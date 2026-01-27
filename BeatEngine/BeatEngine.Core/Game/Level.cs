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
using Microsoft.VisualBasic.FileIO;
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
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Formats.Asn1.AsnWriter;
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
        public Tile[,] tiles;

        public static List<(int X, int Y)> PressedTiles = new List<(int X, int Y)> ();

        private Tile[,] mirrorTiles;

        private Dictionary<double, Step[,]> Steps;

        public ScoreManager ScoreManager;

        private Texture2D[] layers;
        // The layer which entities are drawn on top of.
        private const int EntityLayer = 2;

        Accelerometer accelerometer;
        private Matrix globalTransformation;

        private List<Mode> Modes = new List<Mode>();
        public bool IsGetReadyMessageStillPlaying = true;
        Song Song = null;
        public bool IsSongStarted = false;
        public bool IsSongSaved = false;
        public float Time;
        public float DefaultAnimationDuration = 2f;
        Tile draggedTile;
        private Mode CurrentMode { get; set; }

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

        public bool IsInitFlipBackFlip = false;

        public bool IsInitScoreManager = false;
        public bool ReachedExit
        {
            get { return reachedExit; }
        }
        bool reachedExit;      
        public ContentManager Content
        {
            get { return content; }
        }
        ContentManager content;

        public List<Hit> Hits = new List<Hit>();

        private SoundEffect clickSound;

        #region Loading

        public Level(IServiceProvider serviceProvider, Stream fileStream, int levelIndex, Matrix globalTransformation, GameState gameState)
        {
            // Create a new content manager to load content used just by this level.
            content = new ContentManager(serviceProvider, "Content");

            LoadTiles(fileStream);
            PositionTiles();

            //SequenceManager.InitiateSequence(new GameTime(), tiles);

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
            clickSound = Content.Load<SoundEffect>("Sounds/Click");
            this.globalTransformation = Matrix.Invert(globalTransformation);

            hudFont = Content.Load<SpriteFont>("Fonts/Hud");

            AddModes();

            Time = DefaultAnimationDuration;
            CurrentMode = Modes.Where(m => m.Tag == "Play").FirstOrDefault();
            Song = Content.Load<Song>("Sounds/StarlitPulse");

        }

        private void AddModes()
        {
            Modes.Add(new Mode() { Tag = "Intro", NextModeTag = "Show" });
            Modes.Add(new Mode() { Tag = "Play", NextModeTag = "Calculate" });
            Modes.Add(new Mode() { Tag = "Show", NextModeTag = "Play" });
            Modes.Add(new Mode() { Tag = "Calculate", NextModeTag = "Show" });
        }

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

            tiles = new Tile[width, lines.Count];

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

        private Tile LoadTile(string name, TileCollision collision)
        {
            Tile tile = new Tile(Content.Load<Texture2D>("Tiles/" + name), collision, Content);
            tile.SetFlipTexture(Content.Load<Texture2D>("Tiles/front"));

            return tile;
        }

      
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
            switch (CurrentMode.Tag)
            {
                case "Play":
                    CheckModeTransition(gameTime);
                    CheckIfTileIsPressed(touchCollection, gameTime);
                    break;
                case "Calculate":
                    CheckModeTransition(gameTime);
                    break;
            }
        }

       
        #endregion

        #region Draw

        public void ToNextMode()
        {

            string nextModeTag = CurrentMode.ToNextMode();
            CurrentMode = Modes.Where(m => m.Tag == nextModeTag).FirstOrDefault();
        }

        public void CheckModeTransition(GameTime gameTime)
        {
            switch (CurrentMode.Tag)
            {
                case "Intro":
                    
                   
                    break;
                case "Show":

                   
                    break;

                case "Play":
                    
                   

                    break;

                case "Calculate":
                    
                   

                    break;
            }

        }

       

        /// <summary>
        /// Draw everything in the level from background to foreground.
        /// </summary>
        /// 
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            for (int i = 0; i < layers.Length; ++i)
                spriteBatch.Draw(layers[i], Vector2.Zero, Color.White);
            
            switch (CurrentMode.Tag)
            {
                case "Intro":
                    DrawTiles(gameTime, spriteBatch);
                    DrawShadowedString(hudFont, "SCORE: ", new Vector2(700, 30), Color.Brown, spriteBatch);
                    break;
                case "Show":
                    DrawTiles(gameTime, spriteBatch);
                    break;
                case "Play":
                    DrawTiles(gameTime, spriteBatch);
                    DrawFX(gameTime, spriteBatch);
                    DrawShadowedString(hudFont, "SCORE: ", new Vector2(700, 30), Color.Brown, spriteBatch);
                    DrawTimeElapsed(hudFont, "TIME: ", new Vector2(100, 30), Color.Brown, spriteBatch);
                    break;
                case "Calculate":
                    DrawTiles(gameTime, spriteBatch);
                    DrawFX(gameTime, spriteBatch);
                    DrawShadowedString(hudFont, "SCORE: ", new Vector2(700, 30), Color.Brown, spriteBatch);
                    DrawTimeElapsed(hudFont, "TIME: ", new Vector2(100, 30), Color.Brown, spriteBatch);
                    break;
            }
        }


        private void DrawShadowedString(SpriteFont font, string value, Vector2 position, Color color, SpriteBatch spriteBatch)
        {
            spriteBatch.DrawString(font, value, position + new Vector2(1.0f, 1.0f), color, 0, new Vector2(1.0f, 1.0f), 4, SpriteEffects.None, 1);
            //sriteBatch.DrawString(font, value, position, color);
        }

        private void DrawTimeElapsed(SpriteFont font, string value, Vector2 position, Color color, SpriteBatch spriteBatch)
        {
            double songTime = MediaPlayer.PlayPosition.TotalSeconds;
            value += songTime.ToString("F2");
            spriteBatch.DrawString(font, value, position + new Vector2(1.0f, 1.0f), color, 0, new Vector2(1.0f, 1.0f), 4, SpriteEffects.None, 1);
            //sriteBatch.DrawString(font, value, position, color);
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
                        Color tint = Color.White;

                        if(tiles[x, y].IsPressed)
                        {
                            //tint = Color.MonoGameOrange;
                        }

                        spriteBatch.Draw(texture, tiles[x, y].Position, tint);
                        var silabaPosition = new Vector2(tiles[x, y].Position.X + 90, tiles[x, y].Position.Y + tiles[x, y].Height / 3f);
                        string text = string.Empty;
                        if(x%2 == 1)
                        {
                            text = "TXV";
                            silabaPosition = new Vector2(tiles[x, y].Position.X + 60, tiles[x, y].Position.Y + tiles[x, y].Height / 3f);
                        }
                        else
                        {
                            text = "RE";
                            silabaPosition = new Vector2(tiles[x, y].Position.X + 90, tiles[x, y].Position.Y + tiles[x, y].Height / 3f);
                        }

                         DrawShadowedString(hudFont, text, silabaPosition, Color.Black, spriteBatch);

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
                        if (tiles[x, y].IsHit)
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
                        //else 
                        //{
                        //    tiles[x, y].IsHit = false;
                        //}
                    }
                }
            }
        }

        private void PositionTiles()
        {
            int initialPosY = 1534;
            int initialPosX = 920;

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
                initialPosX = 920;
                initialPosY -= 300;
            }
        }

        private void CheckFinishedSFX(GameTime gameTime)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    if (tiles[x, y].IsPlayingSound)
                    {
                        double currentTime = gameTime.TotalGameTime.TotalSeconds;

                        double elapsedTime = tiles[x, y].InitialTime + tiles[x, y].SoundDuration;

                        if (currentTime >= elapsedTime)
                        {
                            tiles[x, y].IsPlayingSound = false;
                        }
                    }
                }
            }   
        }
        public Vector2 dragOffset;
        private void CheckIfTileIsPressed(TouchCollection touchLocations, GameTime gameTime)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    tiles[x, y].IsPressed = false;
                    tiles[x, y].IsHit = false;

                    foreach (var touch in touchLocations)
                    {
                        Vector2 pos = touch.Position;
                        Vector2.Transform(ref pos, ref globalTransformation, out pos);

                        if (touch.State == TouchLocationState.Pressed)
                        {
                            if (tiles[x, y].BoundingRectangle.Contains(pos))
                            {
                                tiles[x, y].IsPressed = true;

                                draggedTile = tiles[x, y];

                                //tiles[x, y].dragOffset = tiles[x, y].Position - touch.Position;

                                dragOffset = draggedTile.Position - touch.Position;


                                if (!tiles[x, y].IsPlayingSound)
                                {
                                    tiles[x, y].IsPlayingSound = true;
                                    tiles[x, y].SoundDuration = clickSound.Duration.TotalSeconds * 0.5; // this is to allow overlapping sounds, because sound have a long end
                                    tiles[x, y].InitialTime = (float)gameTime.TotalGameTime.TotalSeconds;
                                    clickSound.Play();
                                    
                                }
                            }

                        }

                        if(touch.State == TouchLocationState.Moved && draggedTile != null)
                        {
                            draggedTile.Position = touch.Position + dragOffset;

                            //TouchLocation prevLoc;

                            //if (!touch.TryGetPreviousLocation(out prevLoc)) continue;

                            //float deltaX = touch.Position.X - prevLoc.Position.X;
                            //float deltaY = touch.Position.Y - prevLoc.Position.Y;

                            //draggedTile.IsPressed = true;
                            //draggedTile.Position = new Vector2(draggedTile.Position.X + deltaX, draggedTile.Position.Y + deltaY);



                            //if (tiles[x, y].BoundingRectangle.Contains(pos))
                            //{
                            //    tiles[x, y].Position = touch.Position + tiles[x, y].dragOffset;

                            //    //TouchLocation prevLoc;

                            //    //if (!touch.TryGetPreviousLocation(out prevLoc)) continue;

                            //    //float deltaX = touch.Position.X - prevLoc.Position.X;
                            //    //float deltaY = touch.Position.Y - prevLoc.Position.Y;

                            //    //tiles[x, y].IsPressed = true;
                            //    //tiles[x, y].Position = new Vector2(tiles[x, y].Position.X + deltaX, tiles[x, y].Position.Y + deltaY);

                            //}
                        }

                        if (touch.State == TouchLocationState.Released && draggedTile != null)
                        {
                            draggedTile.IsPressed = false;
                            draggedTile = null;

                        }

                    }

                }
            }
        }

        #endregion
    }
}