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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;
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

        //private List<Tile> Panels = new List<Tile> ();
        private List<Panel> Panels = new List<Panel> ();

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
        private SoundEffect levelFinished;

        public List<Syllable> Syllables;
        string Clue = string.Empty;

        #region Loading

        public Level(IServiceProvider serviceProvider, Stream fileStream, int levelIndex, Matrix globalTransformation, GameState gameState)
        {
            // Create a new content manager to load content used just by this level.
            content = new ContentManager(serviceProvider, "Content");
            GameState = gameState;

            LoadSyllables(fileStream);
            ShuffleSyllables();
            LoadTiles();
            LoadPanels();
            PositionTiles();
            PositionPanels();

            LoadLevelEndingTile("Excelente", TileCollision.Passable);


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
            levelFinished = Content.Load<SoundEffect>("Sounds/Check");
            this.globalTransformation = Matrix.Invert(globalTransformation);

            hudFont = Content.Load<SpriteFont>("Fonts/Hud");

            AddModes();

            Time = DefaultAnimationDuration;
            CurrentMode = Modes.Where(m => m.Tag == "Play").FirstOrDefault();
            Song = Content.Load<Song>("Sounds/StarlitPulse");

            MissionAccomplised = false;

        }

        private void AddModes()
        {
            Modes.Add(new Mode() { Tag = "Intro", NextModeTag = "Show" });
            Modes.Add(new Mode() { Tag = "Play", NextModeTag = "Calculate" });
            Modes.Add(new Mode() { Tag = "Show", NextModeTag = "Play" });
            Modes.Add(new Mode() { Tag = "Calculate", NextModeTag = "Show" });
        }

        int DefaultPanelNumber = 3;

        private void LoadPanels()
        {
            for(int k = 0; k< DefaultPanelNumber; k++)
            {
                var newPanel = new Panel(Content.Load<Texture2D>("UI/Environment/Panel"));
                
                //var panel = new Tile(Content.Load<Texture2D>("UI/Environment/Panel"), TileCollision.Platform, Content);
                Panels.Add(newPanel);  
            }
        }

        private Tile LoadLevelEndingTile(string name, TileCollision collision)
        {
            LevelEndingTile = new Tile(Content.Load<Texture2D>("UI/Environment/" + name), collision, Content);
            LevelEndingTile.Position = new Vector2(-150, 800);

            offscreenLeftX = -80000;
            offscreenRightX = 1200;
            centerX = (1300 - LevelEndingTile.Texture.Width) / 2f;

            return LevelEndingTile;
        }

        private void LoadSyllables(Stream fileStream)
        {
            Syllables = new List<Syllable>();

            using (var reader = new StreamReader(fileStream))
            {
                string line;
                int k = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    
                    if (k == 0) //first line is the clue
                    {
                        Clue = line;
                        k++;
                        continue;
                    }

                    // Skip empty lines
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // Expected format: TAG(panelNbr,panelPosition)
                    int openParen = line.IndexOf('(');
                    int closeParen = line.IndexOf(')');

                    if (openParen < 0 || closeParen < 0)
                        continue; // malformed line, skip

                    string tag = line.Substring(0, openParen);
                    string numbers = line.Substring(openParen + 1, closeParen - openParen - 1);

                    var parts = numbers.Split(',');

                    if (parts.Length != 2)
                        continue;

                    if (!int.TryParse(parts[0], out int panelNbr))
                        continue;

                    if (!int.TryParse(parts[1], out int panelPosition))
                        continue;

                    Syllables.Add(new Syllable
                    {
                        SyllableTag = tag,
                        PanelNbr = panelNbr,
                        PanelPosition = panelPosition
                    });

                    k++;
                }
            }
        }
        private static readonly Random _rng = new Random();

        private void ShuffleSyllables()
        {
            for (int i = Syllables.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);

                // swap
                var temp = Syllables[i];
                Syllables[i] = Syllables[j];
                Syllables[j] = temp;
            }
        }

        private float EaseOutPowerFive(float t)
        {
            return 1f - MathF.Pow(1f - t, 5f);
        }

        private float EaseIPowerNine(float t)
        {
            return MathF.Pow(t, 9f);
        }
        private float levelEndingTimer = 0f;

        private float offscreenLeftX;
        private float offscreenRightX;
        private float centerX;
        public Tile LevelEndingTile;
        private void DrawLevelEndingTile(GameTime gameTime, SpriteBatch spriteBatch)
        {
            levelEndingTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            float t = MathHelper.Clamp(levelEndingTimer / DefaultAnimationDuration, 0f, 1f);
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
                levelFinished.Play();
            }
            else
            {
                // Phase 3: Accelerate out (ease-in)
                float p = (t - 0.6f) / 0.4f;
                p = EaseIPowerNine(p);
                x = MathHelper.Lerp(centerX, offscreenRightX, p);
            }

            LevelEndingTile.Position = new Vector2(x, LevelEndingTile.Position.Y);

            spriteBatch.Draw(LevelEndingTile.Texture, LevelEndingTile.Position, Color.White);

            if (t >= 1f)
            {
                LevelFinishedAndEffectsShown = true;
            }
        }
        public bool LevelFinishedAndEffectsShown = false;
        private void LoadTiles()
        {
            int numberOfRows = 4;

            //if(Syllables.Count > 0 && Syllables.Count <= 4)
            //{
            //    numberOfRows = 1;
            //}
            //else if(Syllables.Count > 4 && Syllables.Count <= 8)
            //{
            //    numberOfRows = 2;
            //}
            //else if (Syllables.Count > 8 && Syllables.Count <= 12)
            //{
            //    numberOfRows = 4;
            //}
            //else
            //{
            //    numberOfRows = 4;
            //}

            tiles = new Tile[numberOfRows, numberOfRows];

            int k = 0;
            bool allSyllablesRead= false;

            for (int y = 0; y < 4; ++y)
            {
                if(allSyllablesRead)
                {
                    break;
                }
                
                for (int x = 0; x < 4; ++x)
                {
                    if(k == Syllables.Count)
                    {
                        allSyllablesRead = true;
                        break;
                    }
                    else
                    {
                        tiles[x, y] = LoadTile(Syllables[k]);
                        k++;
                    }
                        
                }
            }
            
        }

      
        private Tile LoadTile(Syllable syllable)
        {
            return LoadTile("magenta", TileCollision.Platform, syllable);
        }

        private Tile LoadTile(string name, TileCollision collision, Syllable syllable)
        {
            Tile tile = new Tile(Content.Load<Texture2D>("Tiles/" + name), collision, Content);
            tile.Syllable = syllable;   

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
                    CheckFinishedSFX(gameTime);
                    CheckIfTileIsPressed(touchCollection, gameTime);
                    AllTilesAssigned = CheckIfAllTilesAreAssignedToPanels();
                    CheckModeTransition(gameTime);
                    break;
                case "Calculate":
                    CheckFinishedSFX(gameTime);
                    CalculateScore();
                    CheckModeTransition(gameTime);
                    break;
                case "Show":
                    MoveToNextScene();
                    break;
            }
        }


        #endregion

        #region Draw
        GameState GameState;
        public void MoveToNextScene()
        {
            if (LevelFinishedAndEffectsShown)
            {
                GameState.Level = 2;
                GameState.DirtyScene = true;
            }
        }


        public bool MissionAccomplised;

        public void CalculateScore()
        {
            int score = 0;
            foreach(var panel in Panels)
            {
                List<Syllable> syllables = new List<Syllable>();
                List<Tile> tiles = panel.Tiles;

                foreach(Tile tile in tiles)
                {
                    syllables.Add(tile.Syllable);
                }

                if(AllSyllableTilesBelongToSamePanel(syllables) && AllSyllableTilesAreOrdered(syllables))
                {
                    score++;
                }

            }

            if(score == Panels.Count)
            {
                MissionAccomplised = true;
                InitiateCelebrationAnimation();
            }
            
        }

        public void InitiateCelebrationAnimation()
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    if (tiles[x, y] == null)
                    {
                        continue;
                    }
                    tiles[x, y].IsHit = true;
                }
            }
        }   

        public bool AllSyllableTilesBelongToSamePanel(List<Syllable> syllables)
        {

            var count = syllables.DistinctBy(s=>s.PanelNbr).ToList().Count();


            return count == 1;
        }

        public bool AllSyllableTilesAreOrdered(List<Syllable> syllables)
        {
            int orderedTilePosition = 1;
            bool allTilesAreOrdered = true;

            foreach (var syllable in syllables)
            {
                if(syllable.PanelPosition != orderedTilePosition)
                {
                    allTilesAreOrdered = false;
                    break;
                }
                orderedTilePosition++;
            }

            return allTilesAreOrdered;
        }

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
                    
                    if (AllTilesAssigned)
                    {
                        ToNextMode();
                    }

                    break;

                case "Calculate":
                    
                   if(MissionAccomplised)
                   {
                        ToNextMode();
                   }

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
                    DrawPanels(gameTime, spriteBatch);
                    DrawLevelEndingTile(gameTime, spriteBatch);
                    DrawTiles(gameTime, spriteBatch);
                    DrawFX(gameTime, spriteBatch);
                    break;
                case "Play":
                    DrawPanels(gameTime, spriteBatch);
                    DrawTiles(gameTime, spriteBatch);
                    DrawFX(gameTime, spriteBatch);
                    DrawClue(hudFont, string.Format( "PISTA: {0}", Clue), new Vector2(100, 30), Color.Brown, spriteBatch);
                    break;
                case "Calculate":
                    DrawPanels(gameTime, spriteBatch);
                    DrawTiles(gameTime, spriteBatch);
                    DrawFX(gameTime, spriteBatch);
                    DrawClue(hudFont, string.Format("PISTA: {0}", Clue), new Vector2(100, 30), Color.Brown, spriteBatch);
                    break;
            }
        }


        private void DrawShadowedString(SpriteFont font, string value, Vector2 position, Color color, SpriteBatch spriteBatch)
        {
            spriteBatch.DrawString(font, value, position + new Vector2(1.0f, 1.0f), color, 0, new Vector2(1.0f, 1.0f), 4, SpriteEffects.None, 1);
            //sriteBatch.DrawString(font, value, position, color);
        }

        private void DrawClue(SpriteFont font, string value, Vector2 position, Color color, SpriteBatch spriteBatch)
        {
            spriteBatch.DrawString(font, value, position + new Vector2(1.0f, 1.0f), color, 0, new Vector2(1.0f, 1.0f), 4, SpriteEffects.None, 1);
            //sriteBatch.DrawString(font, value, position, color);
        }

        private void DrawTiles(GameTime gameTime, SpriteBatch spriteBatch)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    if (tiles[x, y] == null)
                    {
                        continue;
                    }

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

                        string text = tiles[x, y].Syllable.SyllableTag;

                        if(text.Count() == 2)
                        {
                            silabaPosition = new Vector2(tiles[x, y].Position.X + 90, tiles[x, y].Position.Y + tiles[x, y].Height / 3f);
                        }
                        else if (text.Count() == 1)
                        {
                            silabaPosition = new Vector2(tiles[x, y].Position.X + 120, tiles[x, y].Position.Y + tiles[x, y].Height / 3f);
                        }
                        else if(text.Count() == 3)
                        {
                            silabaPosition = new Vector2(tiles[x, y].Position.X + 60, tiles[x, y].Position.Y + tiles[x, y].Height / 3f);
                        }
                        else
                        {
                            silabaPosition = new Vector2(tiles[x, y].Position.X + 37, tiles[x, y].Position.Y + tiles[x, y].Height / 3f);
                        }

                            DrawShadowedString(hudFont, text, silabaPosition, Color.Black, spriteBatch);

                    }
                }
            }
        }

        private void DrawPanels(GameTime gameTime, SpriteBatch spriteBatch)
        {
            for(int k = 0; k < Panels.Count; k++)
            {
                Texture2D texture = Panels[k].Texture;
                spriteBatch.Draw(texture, Panels[k].Position, Color.White * 0.7f);
            }
        }

        private void DrawFX(GameTime gameTime, SpriteBatch spriteBatch)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    if (tiles[x, y] == null)
                    {
                        continue;
                    }

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
            int initialPosY = 1934;
            int initialPosX = 920;

            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    //If there is a visible tile in that position
                    if(tiles[x, y] == null)
                    {
                        continue;
                    }

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

        private void PositionPanels()
        {
            int initialPosY = 734;
            int initialPosX = 10;

            for (int y = 0; y < Panels.Count; ++y)
            {
                Texture2D texture = Panels[y].Texture;
                if (texture != null)
                {
                    //Draw it in screen space.
                    Vector2 position = new Vector2(initialPosX, initialPosY);
                    Panels[y].Position = position;

                }
                initialPosY -= 280;
            }
        }

        private void CheckFinishedSFX(GameTime gameTime)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    if (tiles[x, y] == null)
                    {
                        continue;
                    }

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

        public int CheckIfTileIsDroppedInPanel(Tile draggedTile)
        {
            int k = 0;
            int droppedInPanel = -100;
            int dropeedInPanelDepth = -100;

            foreach(Panel panel in Panels)
            {
                var depth = panel.BoundingRectangle.GetIntersectionDepth(draggedTile.BoundingRectangle);

                if(depth.Y != 0)
                {
                    int currentDepth = (int)Math.Abs(depth.Y);

                    if (k > droppedInPanel &&  currentDepth > dropeedInPanelDepth)
                    {
                        droppedInPanel = k;
                        dropeedInPanelDepth = currentDepth;
                    }

                }

                k++;
            }

            return droppedInPanel;
        }

        public void AssignSlotToTile(Tile draggedTile, Panel panel, int panelNbr)
        {
            Vector2 newPosition;

            if(panel.DeallocatedTiles.Count == 0) //no tiles taken off the panel
            {
                newPosition = new Vector2(panel.Position.X + panel.SlotsOccupied * panel.SlotDimension, panel.Position.Y + panel.YOffset);

                draggedTile.Position = newPosition;
                draggedTile.CurrentPanel = panelNbr;
                draggedTile.CurrentPanelPosition = panel.CurrentSlot;

                panel.CurrentSlot += 1;
                panel.SlotsOccupied += 1;
                panel.DeallocatedTiles.Clear();

                panel.Tiles.Add(draggedTile);
            }
            else
            {
                int minIndex = panel.DeallocatedTiles.IndexOf(panel.DeallocatedTiles.Min());

                newPosition = new Vector2(panel.Position.X + panel.DeallocatedTiles[minIndex] * panel.SlotDimension, panel.Position.Y + panel.YOffset);

                draggedTile.Position = newPosition;
                draggedTile.CurrentPanel = panelNbr;
                draggedTile.CurrentPanelPosition = panel.DeallocatedTiles[minIndex];
                panel.DeallocatedTiles.RemoveAt(minIndex);

                panel.Tiles.Add(draggedTile);

                //panel.CurrentSlot = panel.SlotsOccupied;
            }

        }

        public void DeallocateTile(Tile draggedTile, Panel panel)
        {
            if(draggedTile.CurrentPanel != -100)
            {
                panel.DeallocatedTiles.Add(draggedTile.CurrentPanelPosition);
                //panel.CurrentSlot = draggedTile.CurrentPanelPosition;
                draggedTile.CurrentPanel = -100;
                draggedTile.CurrentPanelPosition = -100;

                panel.Tiles.Remove(draggedTile);
            }
        }

        public bool AllTilesAssigned = false;

        public bool CheckIfAllTilesAreAssignedToPanels()
        {
            bool allTilesAssigned = true;


            for (int y = 0; y < Height; ++y)
            {
                if(!allTilesAssigned)
                { break;
                }
                
                for (int x = 0; x < Width; ++x)
                {
                    if (tiles[x, y] == null)
                    {
                        continue;
                    }

                    if(tiles[x, y].CurrentPanel == -100)
                    {
                        allTilesAssigned = false;
                    }

                    if (!allTilesAssigned)
                    {
                        break;
                    }

                }
            }

            return allTilesAssigned;
        }


        public Vector2 dragOffset;
        private void CheckIfTileIsPressed(TouchCollection touchLocations, GameTime gameTime)
        {
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    if (tiles[x, y] == null)
                    {
                        continue;
                    }

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

                            }

                        }

                        if(touch.State == TouchLocationState.Moved && draggedTile != null)
                        {
                            draggedTile.Position = touch.Position + dragOffset;

                            if (draggedTile.CurrentPanel != -100)
                            {
                                DeallocateTile(draggedTile, Panels[draggedTile.CurrentPanel]);
                            }

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
                            int panel = CheckIfTileIsDroppedInPanel(draggedTile);

                            if(panel != -100)
                            {
                                AssignSlotToTile(draggedTile, Panels[panel], panel);
                                
                                if (!tiles[x, y].IsPlayingSound)
                                {
                                    tiles[x, y].IsPlayingSound = true;
                                    tiles[x, y].SoundDuration = clickSound.Duration.TotalSeconds * 0.5; // this is to allow overlapping sounds, because sound have a long end
                                    tiles[x, y].InitialTime = (float)gameTime.TotalGameTime.TotalSeconds;
                                    clickSound.Play();

                                }
                            }
                            else
                            {
                                if(draggedTile.CurrentPanel != -100)
                                {
                                    DeallocateTile(draggedTile, Panels[draggedTile.CurrentPanel]);
                                }

                            }

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