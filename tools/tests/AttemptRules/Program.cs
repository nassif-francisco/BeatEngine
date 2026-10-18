using BeatEngine.Core.Game;

Syllable Make(int word, int position) => new Syllable { PanelNbr = word, PanelPosition = position };
var first = new[] { Make(1,1), Make(1,2), Make(1,3) };
var second = new[] { Make(2,1), Make(2,2) };
var third = new[] { Make(3,1), Make(3,2) };
var all = first.Concat(second).Concat(third).ToArray();
int passed = 0;
void Check(string name, IEnumerable<IEnumerable<Syllable>> rows, bool skip, AttemptOutcome outcome, int points)
{
    var result = AttemptRules.Evaluate(rows, all, skip);
    if (result.Outcome != outcome || result.ScoreDelta != points)
        throw new Exception(name + ": unexpected result " + result.Outcome + "/" + result.ScoreDelta);
    Console.WriteLine("PASS: " + name);
    passed++;
}
Check("correct words award 30", new[] { first, second, third }, false, AttemptOutcome.Correct, 30);
Check("words can occupy different trays", new[] { third, first, second }, false, AttemptOutcome.Correct, 30);
Check("wrong syllable order costs 400", new[] { first.Reverse().ToArray(), second, third }, false, AttemptOutcome.Mistake, -400);
Check("mixed words cost 400", new[] { new[] { first[0], second[0], first[2] }, new[] { first[1], second[1] }, third }, false, AttemptOutcome.Mistake, -400);
Check("incomplete words cannot win", new[] { first.Take(2).ToArray(), second, third }, false, AttemptOutcome.Mistake, -400);
Check("duplicate words cannot win", new[] { first, first, third }, false, AttemptOutcome.Mistake, -400);
Check("empty tray cannot win", new[] { first, Array.Empty<Syllable>(), third }, false, AttemptOutcome.Mistake, -400);
Check("skip remains minus 10", new[] { first, second, third }, true, AttemptOutcome.Skipped, -10);
Console.WriteLine(passed + " scoring checks passed.");
