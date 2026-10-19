using System.Collections.Generic;
using System.Linq;

// Standard ten-pin scoring for one player. Knows nothing about physics:
// the game reports how many pins fell each roll, this keeps the scorecard.
public class BowlingScore
{
    public const int Frames = 10;

    readonly List<int> rolls = new();
    readonly List<int> standings = new();
    readonly List<string>[] marks = new List<string>[Frames];
    bool rackIsFresh;

    public int Frame { get; private set; }          // 0-based, == Frames when the game is over
    public int RollInFrame { get; private set; }    // 0-based
    public bool IsGameOver => Frame >= Frames;

    public BowlingScore() => Reset();

    public void Reset()
    {
        rolls.Clear();
        standings.Clear();
        for (int i = 0; i < Frames; i++) marks[i] = new List<string>();
        Frame = 0;
        RollInFrame = 0;
        rackIsFresh = true;
        LastMark = "";
    }

    public IReadOnlyList<string> MarksFor(int frame) => marks[frame];

    // Every roll so far (pins knocked) and how many pins were standing for it: enough to rebuild
    // the scorecard exactly with FromHistory, for saving a game part-way through.
    public IReadOnlyList<int> Rolls => rolls;
    public IReadOnlyList<int> StandingBefore => standings;

    public static BowlingScore FromHistory(IReadOnlyList<int> knocked, IReadOnlyList<int> standingBefore)
    {
        var score = new BowlingScore();
        if (knocked == null || standingBefore == null) return score;
        int n = System.Math.Min(knocked.Count, standingBefore.Count);
        for (int i = 0; i < n && !score.IsGameOver; i++) score.AddRoll(knocked[i], standingBefore[i], out _);
        return score;
    }

    // Mark of the most recent roll: "X", "/", "-" or a digit.
    public string LastMark { get; private set; } = "";

    // How many times a mark appears in the game, e.g. Count("X") for strikes.
    public int Count(string mark) => marks.Sum(f => f.Count(m => m == mark));

    // Record a roll. standingBefore is how many pins were up when the ball was thrown,
    // so a full rack cleared is a strike and a partial rack cleared is a spare.
    // Returns true when the frame is over (the next roll needs a full rack).
    // Returns false when the same frame continues; needsFreshRack says whether to re-rack (10th frame).
    public bool AddRoll(int knocked, int standingBefore, out bool needsFreshRack)
    {
        rolls.Add(knocked);
        standings.Add(standingBefore);
        bool cleared = knocked == standingBefore;
        // A strike is clearing a freshly set rack in one ball; clearing what's left is a spare
        // (even 0 then 10 is a spare).
        LastMark = cleared ? (rackIsFresh ? "X" : "/") : knocked == 0 ? "-" : knocked.ToString();
        marks[Frame].Add(LastMark);
        needsFreshRack = cleared;
        rackIsFresh = cleared;

        bool frameOver;
        if (Frame < Frames - 1)
        {
            frameOver = cleared && RollInFrame == 0 || RollInFrame == 1;
        }
        else
        {
            // 10th frame: a third roll only after a strike or spare.
            var f = marks[Frame];
            frameOver = RollInFrame == 2 || RollInFrame == 1 && f[0] != "X" && f[1] != "/";
        }

        if (frameOver) { Frame++; RollInFrame = 0; rackIsFresh = true; }
        else RollInFrame++;
        return frameOver;
    }

    // Running totals per frame; null for frames that can't be scored yet.
    public int?[] RunningTotals()
    {
        var totals = new int?[Frames];
        int score = 0, i = 0;
        for (int frame = 0; frame < Frames; frame++)
        {
            if (i >= rolls.Count) break;
            if (rolls[i] == 10)
            {
                if (i + 2 >= rolls.Count) break;
                score += 10 + rolls[i + 1] + rolls[i + 2];
                i += 1;
            }
            else
            {
                if (i + 1 >= rolls.Count) break;
                int pair = rolls[i] + rolls[i + 1];
                if (pair == 10)
                {
                    if (i + 2 >= rolls.Count) break;
                    score += 10 + rolls[i + 2];
                }
                else score += pair;
                i += 2;
            }
            totals[frame] = score;
        }
        return totals;
    }
}
