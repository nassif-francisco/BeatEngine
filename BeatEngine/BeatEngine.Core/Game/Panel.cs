using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatEngine.Core.Game
{
    public class Panel
    {
        public Vector2 Position { get; set; }
        public List<Tile> Tiles { get; set; }

        public Texture2D Texture { get; set; }

        public int CurrentSlot { get; set; }
        public int SlotsOccupied { get; set; }

        public List<int> DeallocatedTiles { get; set; }

        public int SlotDimension = 225;

        public int YOffset = 2;

        public Rectangle BoundingRectangle
        {
            get
            {
                return new Rectangle((int)Position.X, (int)Position.Y, (int)Texture.Width, (int)Texture.Height);
            }
        }

        public Panel(Texture2D texture2D) 
        {
            Texture = texture2D;
            CurrentSlot = 0;
            SlotsOccupied = 0;
            DeallocatedTiles = new List<int> { };
        }
    }
}
