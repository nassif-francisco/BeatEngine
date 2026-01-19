using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatEngine.Core.Game
{
    public class HitAnimation
    {
        private Texture2D texture;
        private Vector2 origin;

        public readonly int PointValue = 30;
        public readonly Color Color = Color.Yellow;

        private Animation starAnimation;
        private AnimationPlayer sprite;

        public bool IsAnimationStillPlaying { get; set; }
        public float Time;
        public float DefaultAnimationDuration = 0.3f;
        public float DefaultUpwardMovementRate = 0.3f;

        // The gem is animated from a base position along the Y axis.
        private Vector2 basePosition;
        public Vector2 originalPosition { get; set; }
        private float bounce;

        public Vector2 Position
        {
            get
            {
                return basePosition + new Vector2(0.0f, bounce);
            }
            set
            {
                basePosition = value;
            }
        }

        public ContentManager Content
        {
            get { return content; }
        }

        ContentManager content;
        public HitAnimation(Vector2 position, ContentManager Content)
        {
            this.basePosition = position;
            content = Content;  

            LoadContent();
        }
        public void LoadContent()
        {
            texture = content.Load<Texture2D>("Sprites/Star");
            starAnimation = new Animation(content.Load<Texture2D>("Sprites/Star"), 0.1f, true);
            origin = new Vector2(texture.Width / 2.0f, texture.Height / 2.0f);
        }

        public void BeginAnimation()
        {
            Time = DefaultAnimationDuration;
            IsAnimationStillPlaying = true;
            sprite.PlayAnimation(starAnimation);
        }

        public void UpdatePosition(GameTime gameTime)
        {
            Position = new Vector2(Position.X, Position.Y - 100 * (float)gameTime.ElapsedGameTime.TotalSeconds);

            if(Position.Y < originalPosition.Y - 100)
            {
                ResetAnimation();
            }
        }

        public void ResetAnimation()
        {
            Position = originalPosition;
            sprite.Reset();
        }

        public void Update(GameTime gameTime)
        {
            if(IsAnimationStillPlaying)
            {
                Time -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                UpdatePosition(gameTime);

                if (Time < 0)
                {
                    IsAnimationStillPlaying = false;
                    ResetAnimation();
                }
            }
        }
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            //spriteBatch.Draw(texture, Position, null, Color, 0.0f, origin, 5.0f, SpriteEffects.None, 0.0f);
            UpdatePosition(gameTime);
            sprite.PlayAnimation(starAnimation);
            sprite.Draw(gameTime, spriteBatch, Position, SpriteEffects.None);
        }
    }
}
