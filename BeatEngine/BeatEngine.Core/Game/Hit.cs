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
        public int X { get; set; }
        public int Y { get; set; }

        public double Time { get; set; }
        public Hit(int x, int y, double time)
        {
            this.X = x;
            this.Y = y;
            this.Time = time;   
        }
        public string TileTag { get; set; }
    }
}
