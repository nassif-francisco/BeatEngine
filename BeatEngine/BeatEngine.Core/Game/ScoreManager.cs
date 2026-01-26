using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatEngine.Core.Game
{
    public class ScoreManager
    {
        public int TargetScore { get; set; }
        public int Score { get; set; }

        public ScoreManager(int targetScoret) 
        {
            TargetScore = targetScoret;
            Score = 0;
        }

        public int GetScoredScore()
        {
            return Score;   
        }

        public void UpdateScore()
        {
            Score++;
        }

    }
}
