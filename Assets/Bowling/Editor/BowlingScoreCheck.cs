using System.Linq;
using UnityEditor;
using UnityEngine;

// Plays known games through BowlingScore and checks the final totals and frame marks.
public static class BowlingScoreCheck
{
    [MenuItem("Bowling/Score Check")]
    public static void Run()
    {
        int failures = 0;
        void Check(string name, int[] knocked, int expected, string expectedLastFrame = null)
        {
            var score = new BowlingScore();
            int standing = 10;
            foreach (int k in knocked)
            {
                bool frameOver = score.AddRoll(k, standing, out bool fresh);
                standing = frameOver || fresh ? 10 : standing - k;
            }
            int? total = score.RunningTotals()[BowlingScore.Frames - 1];
            string last = string.Join(" ", score.MarksFor(BowlingScore.Frames - 1));
            bool ok = score.IsGameOver && total == expected && (expectedLastFrame == null || last == expectedLastFrame);
            if (!ok) failures++;
            Debug.Log($"[BowlingScoreCheck] {(ok ? "PASS" : "FAIL")} {name}: total {total} (want {expected}), 10th \"{last}\", over {score.IsGameOver}");
        }

        Check("perfect game", Repeat(10, 12), 300, "X X X");
        Check("all gutters", Repeat(0, 20), 0, "- -");
        Check("all 9-0", Pairs(9, 0, 10), 90, "9 -");
        Check("all 5/ + 5", Concat(Pairs(5, 5, 10), new[] { 5 }), 150, "5 / 5");
        Check("0 then 10 is a spare", Concat(Pairs(0, 10, 10), new[] { 0 }), 100, "- / -");
        Check("10th: X 7 /", Concat(Repeat(0, 18), new[] { 10, 7, 3 }), 20, "X 7 /");
        Check("10th: X 3 4", Concat(Repeat(0, 18), new[] { 10, 3, 4 }), 17, "X 3 4");

        Debug.Log(failures == 0 ? "[BowlingScoreCheck] all passed" : $"[BowlingScoreCheck] {failures} FAILED");
    }

    static int[] Repeat(int value, int count) => Enumerable.Repeat(value, count).ToArray();
    static int[] Pairs(int a, int b, int frames) => Enumerable.Range(0, frames).SelectMany(_ => new[] { a, b }).ToArray();
    static int[] Concat(int[] a, int[] b) => a.Concat(b).ToArray();
}
