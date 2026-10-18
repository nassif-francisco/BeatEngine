using System.Collections.Generic;
using System.Linq;

namespace BeatEngine.Core.Game
{
    public enum AttemptOutcome { Correct, Mistake, Skipped }

    public readonly struct AttemptResult
    {
        public AttemptOutcome Outcome { get; }
        public int ScoreDelta => Outcome == AttemptOutcome.Correct ? 30 :
            Outcome == AttemptOutcome.Skipped ? -10 : -400;
        public AttemptResult(AttemptOutcome outcome) { Outcome = outcome; }
    }

    public static class AttemptRules
    {
        public static AttemptResult Evaluate(IEnumerable<IEnumerable<Syllable>> rows,
            IEnumerable<Syllable> syllables, bool skip)
        {
            if (skip) return new AttemptResult(AttemptOutcome.Skipped);
            var expected = syllables.GroupBy(s => s.PanelNbr).ToDictionary(g => g.Key,
                g => g.OrderBy(s => s.PanelPosition).Select(s => s.PanelPosition).ToArray());
            var used = new HashSet<int>();
            int rowCount = 0;
            foreach (var row in rows)
            {
                var word = row.ToArray();
                if (word.Length == 0 || word.Any(s => s.PanelNbr != word[0].PanelNbr) ||
                    !used.Add(word[0].PanelNbr) ||
                    !expected.TryGetValue(word[0].PanelNbr, out var positions) ||
                    !positions.SequenceEqual(word.Select(s => s.PanelPosition)))
                    return new AttemptResult(AttemptOutcome.Mistake);
                rowCount++;
            }
            return new AttemptResult(rowCount == expected.Count && rowCount > 0 ?
                AttemptOutcome.Correct : AttemptOutcome.Mistake);
        }
    }
}
