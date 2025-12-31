using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatEngine.Core.Game.GameScenes
{
    public interface ISceneBase
    {
        public void Update(
           GameTime gameTime,
           KeyboardState keyboardState,
           GamePadState gamePadState,
           AccelerometerState accelState,
           TouchCollection touchCollection,
           DisplayOrientation orientation);

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch);

        public void Dispose();
    }
}
