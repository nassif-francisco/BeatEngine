#region File Description
//-----------------------------------------------------------------------------
// Tile.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

using System;
using BeatEngine.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace BeatEngine
{
    /// <summary>
    /// Controls the collision detection and response behavior of a tile.
    /// </summary>
    enum TileCollision
    {
        /// <summary>
        /// A passable tile is one which does not hinder player motion at all.
        /// </summary>
        Passable = 0,

        /// <summary>
        /// An impassable tile is one which does not allow the player to move through
        /// it at all. It is completely solid.
        /// </summary>
        Impassable = 1,

        /// <summary>
        /// A platform tile is one which behaves like a passable tile except when the
        /// player is above it. A player can jump up through a platform as well as move
        /// past it to the left and right, but can not fall down through the top of it.
        /// </summary>
        Platform = 2,
    }
    struct Tile
    {
        public Texture2D Texture;
        public Texture2D FlipTexture;
        public Texture2D InterchangeTexture;

        public TileCollision Collision; //will be used to define animation when pressed

        private Vector2 _position;

        public Vector2 Position
        {
            get => _position;
            set
            {
                _position = value;
                Hit.Position = value + new Vector2(100, 0); //place a little bit to the right
                Hit.originalPosition = value + new Vector2(100, 0);
            }
        }
        public bool IsPressed { get; set; }

        public bool IsHit { get; set; }

        public bool IsPlayingSound { get; set; }

        public double SoundDuration { get; set; }

        public bool showingFront = true;
        public float flipProgress = 0f;   // 0 → 1
        public float flipDuration = 0.5f; // seconds
        public bool isFlipping = false;
        public bool isBackFlipping = false;
        public bool isTotallyFlip = false;
        public double flipEndedInTime { get; set; }

        public double InitialTime { get; set; }
        public HitAnimation Hit { get; set; }

        public float Width = 260;
        public float Height = 260;

        public string Tag { get; set; }

        public Vector2 Size = new Vector2();

        public Tile(Texture2D texture, TileCollision collision, ContentManager contentManager)
        {
            Texture = texture;
            Collision = collision;
            Size = new Vector2(Width, Height);
            Hit = new HitAnimation(new Vector2(0,0), contentManager);
        }

        public void SetFlipTexture(Texture2D texture)
        {
            FlipTexture = texture;
        }

        public Rectangle BoundingRectangle
        {
            get
            {
                return new Rectangle((int)Position.X, (int)Position.Y, (int)Texture.Width, (int)Texture.Height);
            }
        }
    }
}