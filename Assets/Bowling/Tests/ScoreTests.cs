using System.Linq;
using NUnit.Framework;

// Ports the same scenarios as Bowling/Score Check (Assets/Bowling/Editor/BowlingScoreCheck.cs)
// into individual EditMode tests against BowlingScore directly. The menu tool stays as-is.
public class ScoreTests
{
    static int[] Repeat(int value, int count) => Enumerable.Repeat(value, count).ToArray();
    static int[] Pairs(int a, int b, int frames) => Enumerable.Range(0, frames).SelectMany(_ => new[] { a, b }).ToArray();
    static int[] Concat(int[] a, int[] b) => a.Concat(b).ToArray();

    static BowlingScore Play(int[] knocked)
    {
        var score = new BowlingScore();
        int standing = 10;
        foreach (int k in knocked)
        {
            bool frameOver = score.AddRoll(k, standing, out bool fresh);
            standing = frameOver || fresh ? 10 : standing - k;
        }
        return score;
    }

    static void AssertGame(int[] knocked, int expectedTotal, string expectedLastFrame)
    {
        var score = Play(knocked);
        int? total = score.RunningTotals()[BowlingScore.Frames - 1];
        string last = string.Join(" ", score.MarksFor(BowlingScore.Frames - 1));
        Assert.IsTrue(score.IsGameOver);
        Assert.AreEqual(expectedTotal, total);
        Assert.AreEqual(expectedLastFrame, last);
    }

    [Test]
    public void PerfectGame() => AssertGame(Repeat(10, 12), 300, "X X X");

    [Test]
    public void AllGutters() => AssertGame(Repeat(0, 20), 0, "- -");

    [Test]
    public void All9Then0() => AssertGame(Pairs(9, 0, 10), 90, "9 -");

    [Test]
    public void All5SlashPlus5() => AssertGame(Concat(Pairs(5, 5, 10), new[] { 5 }), 150, "5 / 5");

    [Test]
    public void ZeroThenTenIsASpare() => AssertGame(Concat(Pairs(0, 10, 10), new[] { 0 }), 100, "- / -");

    [Test]
    public void TenthFrameStrikeSevenSlash() => AssertGame(Concat(Repeat(0, 18), new[] { 10, 7, 3 }), 20, "X 7 /");

    [Test]
    public void TenthFrameStrikeThreeFour() => AssertGame(Concat(Repeat(0, 18), new[] { 10, 3, 4 }), 17, "X 3 4");

    // A game saved part-way through rebuilds to exactly the same scorecard and turn state.
    [Test]
    public void FromHistoryRebuildsMidGame()
    {
        var original = Play(new[] { 10, 7, 3, 9, 0, 10, 10, 4 });
        var copy = BowlingScore.FromHistory(original.Rolls.ToList(), original.StandingBefore.ToList());
        Assert.AreEqual(original.Frame, copy.Frame);
        Assert.AreEqual(original.RollInFrame, copy.RollInFrame);
        CollectionAssert.AreEqual(original.RunningTotals(), copy.RunningTotals());
        for (int f = 0; f < BowlingScore.Frames; f++) CollectionAssert.AreEqual(original.MarksFor(f), copy.MarksFor(f));
        Assert.AreEqual(10, copy.StandingBefore[copy.StandingBefore.Count - 1]); // the last roll was at a fresh rack
    }

    [Test]
    public void FromHistoryOfFullGameIsOver()
    {
        var original = Play(Repeat(10, 12));
        var copy = BowlingScore.FromHistory(original.Rolls.ToList(), original.StandingBefore.ToList());
        Assert.IsTrue(copy.IsGameOver);
        Assert.AreEqual(300, copy.RunningTotals()[BowlingScore.Frames - 1]);
    }
}
