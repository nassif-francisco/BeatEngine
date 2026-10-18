using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BeatEngine.Core.Game
{
    internal sealed class MenuControls : IDisposable
    {
        private readonly Texture2D pixel;
        private readonly SpriteFont font;
        public MenuControls(GraphicsDevice device, SpriteFont font)
        {
            this.font = font;
            pixel = new Texture2D(device, 1, 1);
            pixel.SetData(new[] { Color.White });
        }
        public void Button(SpriteBatch batch, Rectangle bounds, string label, bool selected = false)
        {
            batch.Draw(pixel, bounds, GameTheme.Accent);
            batch.Draw(pixel, new Rectangle(bounds.X + 3, bounds.Y + 3, bounds.Width - 6, bounds.Height - 6),
                selected ? GameTheme.Pressed : Color.White);
            Text(batch, label, bounds.Center.ToVector2(), 3.6f, bounds.Width - 40);
        }
        public void Text(SpriteBatch batch, string text, Vector2 center, float scale, float width = 1000)
        {
            Vector2 size = font.MeasureString(text);
            scale = Math.Min(scale, width / Math.Max(1f, size.X));
            batch.DrawString(font, text, center, GameTheme.Text, 0, size / 2, scale, SpriteEffects.None, 0);
        }
        public void Dispose() => pixel.Dispose();
    }
}
