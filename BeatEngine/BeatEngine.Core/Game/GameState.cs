using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatEngine.Core.Game
{
    public class GameState
    {
        public GameState() 
        {
            Level = 1;
            Score = 0;
        }
        public int Level { get; set; }

        public int Score { get; set; }
    }
}
