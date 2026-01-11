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
    public class Hit
    {
        private Texture2D texture;
        private Vector2 origin;

        public readonly int PointValue = 30;
        public readonly Color Color = Color.Yellow;

        private Animation starAnimation;
        private AnimationPlayer sprite;

        // The gem is animated from a base position along the Y axis.
        private Vector2 basePosition;
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
        public Hit(Vector2 position, ContentManager Content)
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

        public void Update(GameTime gameTime)
        {
            // Bounce control constants
            const float BounceHeight = 0.18f;
            const float BounceRate = 3.0f;
            const float BounceSync = -0.75f;

            // Bounce along a sine curve over time.
            // Include the X coordinate so that neighboring gems bounce in a nice wave pattern.            
            double t = gameTime.TotalGameTime.TotalSeconds * BounceRate + Position.X * BounceSync;
            bounce = (float)Math.Sin(t) * BounceHeight * texture.Height;
        }
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            //spriteBatch.Draw(texture, Position, null, Color, 0.0f, origin, 5.0f, SpriteEffects.None, 0.0f);
            sprite.PlayAnimation(starAnimation);
            sprite.Draw(gameTime, spriteBatch, Position, SpriteEffects.None);
        }
    }
}
