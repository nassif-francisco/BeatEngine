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

        private Tile[,] mirrorTiles;

        private Dictionary<double, Step[,]> Steps;

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



        private float getReadyTimer = 0f;

        private float offscreenLeftX;
        private float offscreenRightX;
        private float centerX;

        public Tile GetReadyTile;

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

        public List<Hit> Hits = new List<Hit>();

        private SoundEffect clickSound;

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
            LoadGetReadyTile("GetReady", TileCollision.Passable);
            PositionTiles();
            PositionMirrorTiles();

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
            CurrentMode = Modes.Where(m => m.Tag == "Intro").FirstOrDefault();
            Song = Content.Load<Song>("Sounds/StarlitPulse");

        }

        private void AddModes()
        {
            Modes.Add(new Mode() { Tag = "Intro", NextModeTag = "Show" });
            Modes.Add(new Mode() { Tag = "Play", NextModeTag = "Show" });
            Modes.Add(new Mode() { Tag = "Show", NextModeTag = "Play" });
        }

        /// <summary>
        /// Iterates over every tile in the structure file and loads its
        /// appearance and behavior. This method also validates that the
        /// file is well-formed with a player start point, exit, etc.
        /// </summary>
        /// <param name="fileStream">
        /// A stream containing the tile data.
        /// </param>
        /// 
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
            mirrorTiles = new Tile[width, lines.Count];

            // Loop over every tile position,
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    // to load each tile.
                    char tileType = lines[y][x];
                    tiles[x, y] = LoadTile(tileType, x, y);
                    mirrorTiles[x, y] = LoadTile(tileType, x, y);
                }
            }

        }

        private int NumberOfSteps = 3;

       


        public static class SequenceManager
        {
            public static List<(int X, int Y)> Steps { get; set; }

            public static float LstTimeStepShown { get; set; }

            public static float Initialtime { get; set; }

            public static float EndShowSequencetime { get; set; }

            public static float DefaultTimeBetweenSteps = 1f;

            public static int InitialNumberOfSteps = 3;

            public static int CurrentNumberOfSteps = 3;

            public static bool IsSequenceCompletelyShown = false;

            public static bool IsShowingSequence = false;

            public static int CurrentTileInSequence { get; set; }

            public static void CreateSequence(GameTime gameTime, Tile[,] tiles)
            {
                Steps = GetRandomPositions(tiles, CurrentNumberOfSteps);
                Initialtime = (float)gameTime.TotalGameTime.TotalSeconds +4;
                IsSequenceCompletelyShown = false;
                IsShowingSequence = true;
                CurrentTileInSequence = 0;
            }

            public static (int X, int Y) ProvideTileToShowInSequence(GameTime gameTime)
            {
                if(CurrentTileInSequence < Steps.Count())
                {
                    CurrentTileInSequence++;
                    Initialtime = (float)gameTime.TotalGameTime.TotalSeconds;
                    return Steps[CurrentTileInSequence - 1];
                }
                else 
                {
                    IsShowingSequence = false;
                    Initialtime = (float)gameTime.TotalGameTime.TotalSeconds;
                    EndShowSequencetime = (float)gameTime.TotalGameTime.TotalSeconds;
                    return Steps[CurrentTileInSequence -1];
                }       
            }

            public static bool IsTimeToShowNextTile(GameTime gameTime)
            {
                var isTime = (float)gameTime.TotalGameTime.TotalSeconds > Initialtime + DefaultTimeBetweenSteps;
                return isTime;
            }

            public static void CheckIsTimeToQuitShowMode(GameTime gameTime)
            {
                var isTime = (float)gameTime.TotalGameTime.TotalSeconds > EndShowSequencetime + DefaultTimeBetweenSteps;
                if (IsShowingSequence == false && isTime)
                     IsSequenceCompletelyShown = true;
            }

            public static void AdvanceLevel(GameTime gameTime, Tile[,] tiles)
            {
                CurrentNumberOfSteps++;
                CreateSequence(gameTime, tiles);   
            }

            public static void InitiateSequence(GameTime gameTime, Tile[,] tiles)
            {
                CurrentNumberOfSteps = InitialNumberOfSteps;
                CreateSequence(gameTime, tiles);
            }

            public static List<(int X, int Y)> GetRandomPositions(
                    Tile[,] tiles,
                    int count)
            {
                int width = tiles.GetLength(0);
                int height = tiles.GetLength(1);

                int totalPositions = width * height;
                if (count > totalPositions)
                    throw new ArgumentException("Requested more positions than available tiles.");

                Random random = new Random();

                // Create all possible positions
                var allPositions = new List<(int X, int Y)>();
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        allPositions.Add((x, y));
                    }
                }

                // Shuffle positions
                var shuffled = allPositions
                    .OrderBy(_ => random.Next())
                    .Take(count)
                    .ToList();

                return shuffled;
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

        private Tile LoadGetReadyTile(string name, TileCollision collision)
        {
            GetReadyTile =  new Tile(Content.Load<Texture2D>("UI/Environment/" + name), collision, Content);
            GetReadyTile.Position = new Vector2(-150, 800);

            offscreenLeftX = -80000;
            offscreenRightX = 1200;
            centerX = (1300 - GetReadyTile.Texture.Width) / 2f;

            return GetReadyTile;
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
            switch (CurrentMode.Tag)
            {
                case "Intro":
                    //CheckIfTileIsPressed(touchCollection, gameTime);
                    //CheckFinishedSFX(gameTime);
                    //CheckSequence(gameTime);
                    CheckModeTransition();
                    break;
                case "Show":
                    //CheckIfTileIsPressed(touchCollection, gameTime);
                    //CheckFinishedSFX(gameTime);
                    CheckSequence(gameTime);
                    SequenceManager.CheckIsTimeToQuitShowMode(gameTime);
                    CheckModeTransition();
                    
                    break;
                case "Play":
                    CheckIfTileIsPressed(touchCollection, gameTime);
                    CheckFinishedSFX(gameTime);
                    CheckModeTransition();
                    break;
            }
        }

        public void TurnOffMirrorTiles()
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    tiles[x, y].IsPressed = false;
                }
            }
        }

        public void CheckSequence(GameTime gameTime)
        {

            bool isSequenceShowing = SequenceManager.IsShowingSequence;

            if (isSequenceShowing)
            {
                if (SequenceManager.IsTimeToShowNextTile(gameTime))
                {
                    TurnOffMirrorTiles();

                    (int, int) tile = SequenceManager.ProvideTileToShowInSequence(gameTime);
                    tiles[tile.Item1, tile.Item2].IsPressed = true;
                }
            }
            else
            {
                TurnOffMirrorTiles();
            }
            

        }

        #endregion

        #region Draw

        public void ToNextMode()
        {

            string nextModeTag = CurrentMode.ToNextMode();
            CurrentMode = Modes.Where(m => m.Tag == nextModeTag).FirstOrDefault();
        }

        public void CheckModeTransition()
        {
            switch (CurrentMode.Tag)
            {
                case "Intro":
                    
                    if(IsGetReadyMessageStillPlaying == false)
                    {
                        SequenceManager.InitiateSequence(new GameTime(), tiles);
                        ToNextMode();
                    }
                    break;
                case "Show":

                    if (SequenceManager.IsSequenceCompletelyShown == true  )
                    {
                        ToNextMode();
                    }
                    break;
                case "Play":
                    // Nothing to do here yet
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
                    //DrawMirrorTiles(gameTime, spriteBatch);
                    //DrawFX(gameTime, spriteBatch);
                    DrawShadowedString(hudFont, "SCORE: ", new Vector2(700, 30), Color.Brown, spriteBatch);
                    DrawGetReady(gameTime, spriteBatch);
                    break;
                case "Show":
                    DrawTiles(gameTime, spriteBatch);
                    //DrawMirrorTiles(gameTime, spriteBatch);
                    //DrawFX(gameTime, spriteBatch);
                    DrawShadowedString(hudFont, "SCORE: ", new Vector2(700, 30), Color.Brown, spriteBatch);
                    DrawTimeElapsed(hudFont, "TIME: ", new Vector2(100, 30), Color.Brown, spriteBatch);
                    break;
                case "Play":
                    DrawTiles(gameTime, spriteBatch);
                    //DrawMirrorTiles(gameTime, spriteBatch);
                    DrawFX(gameTime, spriteBatch);
                    DrawShadowedString(hudFont, "SCORE: ", new Vector2(700, 30), Color.Brown, spriteBatch);
                    DrawTimeElapsed(hudFont, "TIME: ", new Vector2(100, 30), Color.Brown, spriteBatch);
                    break;
            }
        }

        //private void DrawGetReady(GameTime gameTime, SpriteBatch spriteBatch)
        //{
        //    Time -= (float)gameTime.ElapsedGameTime.TotalSeconds;

        //    Texture2D texture = GetReadyTile.Texture;

        //    spriteBatch.Draw(texture, GetReadyTile.Position, Color.White);

        //    if (Time < 0)
        //    {
        //        IsGetReadyMessageStillPlaying = false;
        //    }
        //}

        private void DrawGetReady(GameTime gameTime, SpriteBatch spriteBatch)
        {
            getReadyTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            float t = MathHelper.Clamp(getReadyTimer / DefaultAnimationDuration, 0f, 1f);
            float x;

            if (t < 0.4f)
            {
                // Phase 1: Enter & slow down (ease-out)
                float p = t / 0.4f;
                p = EaseOutPowerFive(p);
                x = MathHelper.Lerp(offscreenLeftX, centerX, p);
            }
            else if (t < 0.6f)
            {
                // Phase 2: Stay in center
                x = centerX;
            }
            else
            {
                // Phase 3: Accelerate out (ease-in)
                float p = (t - 0.6f) / 0.4f;
                p = EaseIPowerNine(p);
                x = MathHelper.Lerp(centerX, offscreenRightX, p);
            }

            GetReadyTile.Position = new Vector2(x, GetReadyTile.Position.Y);

            spriteBatch.Draw(GetReadyTile.Texture, GetReadyTile.Position, Color.White);

            if (t >= 1f)
            {
                StartPlayingSong(); 
            }
        }

        private void StartPlayingSong()
        {
            IsGetReadyMessageStillPlaying = false;
            getReadyTimer = 0f;
            IsSongStarted = true;
            MediaPlayer.Play(Song);
        }

        private float EaseOutPowerFive(float t)
        {
            return 1f - MathF.Pow(1f - t, 5f);
        }

        private float EaseIPowerNine(float t)
        {
            return MathF.Pow(t, 9f);
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

                        if (tiles[x, y].IsPressed)
                        {
                            tint = Color.HotPink; //check also darkseagreen

                        }

                        spriteBatch.Draw(texture, tiles[x, y].Position, tint);
                    }
                }
            }
        }

        private void DrawMirrorTiles(GameTime gameTime, SpriteBatch spriteBatch)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    // If there is a visible tile in that position
                    Texture2D texture = mirrorTiles[x, y].Texture;

                    if (texture != null)
                    {
                        // Draw it in screen space.
                        Color tint = Color.White * 0.8f;

                        if (mirrorTiles[x, y].IsPressed)
                        {
                            tint = Color.DarkSeaGreen;//Color.MonoGameOrange, Color.DarkOrange also good candidates
                        }

                        spriteBatch.Draw(texture, mirrorTiles[x, y].Position, tint);
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

        private void PositionMirrorTiles()
        {
            int initialPosY = 1034;
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
                        mirrorTiles[x, y].Position = position;

                    }
                    initialPosX -= 300;

                }
                initialPosX = 920;
                initialPosY -= 300;
            }
        }

        private void ReadHitsFromFile()
        {
            string filePath = GetSavePath();
            BuildHitListFromSaveFile(filePath);
        }

        //private void TurnOnTiles()
        //{
        //    foreach (Hit hit in Hits)
        //    {
        //        //tiles[hit.X, hit.Y].IsPressed = true;
        //        mirrorTiles[hit.X, hit.Y].IsPressed = true;
        //    }

        //    for (int y = 0; y < Height; ++y)
        //    {
        //        for (int x = 0; x < Width; ++x)
        //        {
        //            tiles[x, y].IsPressed = false;

        //            double songTime = MediaPlayer.PlayPosition.TotalSeconds;

        //            List<Hit> futureHits = Hits
        //                .Where(h => h.Time > songTime)
        //                .ToList();

        //            List<Hit> currentHits = futureHits.Where(h => h.X == x && h.Y == y).ToList();

        //            foreach (Hit hit in currentHits)
        //            {
        //                tiles[x, y].IsPressed = true;
        //            }
        //        }
        //    }
        //}
        private const double HIT_WINDOW = 0.05; // seconds

        private void TurnOnTiles()
        {
            double songTime = MediaPlayer.PlayPosition.TotalSeconds;

            // Clear all tiles
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    mirrorTiles[x, y].IsPressed = false;

            foreach (Hit hit in Hits)
            {
                double delta = Math.Abs(hit.Time - songTime);

                if (delta <= HIT_WINDOW)
                {
                    //tiles[hit.X, hit.Y].IsPressed = true;
                    mirrorTiles[hit.X, hit.Y].IsPressed = true;
                }
            }
        }
        private void BuildHitListFromSaveFile(string filePath)
        {
            var lines = System.IO.File.ReadLines(filePath);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length == 3)
                {
                    int x = int.Parse(parts[0]);
                    int y = int.Parse(parts[1]);
                    double time = double.Parse(parts[2]);
                    Hits.Add(new Hit(x, y, time));
                }
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

        private void CheckIfTileIsPressed(TouchCollection touchLocations, GameTime gameTime)
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

                                if(!tiles[x, y].IsPlayingSound)
                                {
                                    tiles[x, y].IsPlayingSound = true;
                                    tiles[x, y].SoundDuration = clickSound.Duration.TotalSeconds * 0.5; // this is to allow overlapping sounds, because sound have a long end
                                    tiles[x, y].InitialTime = (float)gameTime.TotalGameTime.TotalSeconds;
                                    clickSound.Play();
                                }

                               
                            }
                        }

                    }

                }
            }
        }

        public void AddHit(int x, int y, double time)
        {
            Hits.Add(new Hit(x, y, time));
        }

        public void SaveGame(List<Hit> hits)
        {
            string filePath = GetSavePath();
            string data = "";

            foreach (Hit hit in hits)
            {
                data += $"{hit.X},{hit.Y},{hit.Time} \n";
            }
            //var lines = System.IO.File.ReadLines(filePath); // Or use BinaryWriter for better efficiency
            //System.IO.File.WriteAllText(filePath, data); // Or use BinaryWriter for better efficiency

            using (var writer = new StreamWriter(filePath, false))
            {
                foreach (var hit in hits)
                {
                    writer.Write(hit.X);
                    writer.Write(',');
                    writer.Write(hit.Y);
                    writer.Write(',');
                    writer.WriteLine(hit.Time.ToString(CultureInfo.InvariantCulture));
                }
            } // ← file is CLOSED here
        }

        public string GetSavePath()
        {
            // Gets /storage/emulated/0/Android/data/[your.package.name]/files/
            var basePath = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                );

            Directory.CreateDirectory(basePath);

            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(path, "savegame.sav");

        }



        #endregion
    }
}