using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Pure-data tests: RollReport.BuildKnockedMask/KnockedCount and ReplayData's quantized round-trip
// and size budget. No scene or physics needed.
public class RollReportTests
{
    static bool[] Mask(params int[] standingIndices)
    {
        var m = new bool[10];
        foreach (int i in standingIndices) m[i] = true;
        return m;
    }

    [Test]
    public void BuildKnockedMask_AllStandingAllDown_AllTrue()
    {
        var standingBefore = Mask(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        var isDownNow = new[] { true, true, true, true, true, true, true, true, true, true };
        var mask = RollReport.BuildKnockedMask(standingBefore, isDownNow, foul: false);
        Assert.IsTrue(System.Array.TrueForAll(mask, b => b));
    }

    [Test]
    public void BuildKnockedMask_IgnoresPinsNotStandingBefore()
    {
        // Pin 0 was already down before this throw; it shouldn't count even though it reads "down now".
        var standingBefore = Mask(1, 2);
        var isDownNow = new[] { true, true, true, false, false, false, false, false, false, false };
        var mask = RollReport.BuildKnockedMask(standingBefore, isDownNow, foul: false);
        Assert.AreEqual(new[] { false, true, true, false, false, false, false, false, false, false }, mask);
    }

    [Test]
    public void BuildKnockedMask_FoulAlwaysAllFalse()
    {
        var standingBefore = Mask(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        var isDownNow = new[] { true, true, true, true, true, true, true, true, true, true };
        var mask = RollReport.BuildKnockedMask(standingBefore, isDownNow, foul: true);
        Assert.IsTrue(System.Array.TrueForAll(mask, b => !b));
    }

    [Test]
    public void KnockedCount_MatchesPlainCountOfTrueEntries()
    {
        var standingBefore = Mask(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        var isDownNow = new[] { true, false, true, false, true, false, false, false, false, false };
        var mask = RollReport.BuildKnockedMask(standingBefore, isDownNow, foul: false);
        var report = new RollReport { knockedMask = mask };
        int plainCount = System.Array.FindAll(mask, b => b).Length;
        Assert.AreEqual(plainCount, report.KnockedCount);
        Assert.AreEqual(3, report.KnockedCount);
    }

    static (Vector3 pos, Quaternion rot)[] SyntheticFrame(int frameIndex)
    {
        var frame = new (Vector3 pos, Quaternion rot)[ReplayData.ObjectCount];
        for (int i = 0; i < ReplayData.ObjectCount; i++)
        {
            float t = frameIndex + i * 0.37f;
            Vector3 pos = new(Mathf.Sin(t) * 1.5f, Mathf.Cos(t * 0.5f) * 1f, 10f + Mathf.Sin(t * 0.3f) * 8f);
            Quaternion rot = Quaternion.Euler(Mathf.Repeat(t * 40f, 360f), Mathf.Repeat(t * 77f, 360f), Mathf.Repeat(t * 13f, 360f));
            frame[i] = (pos, rot);
        }
        return frame;
    }

    [Test]
    public void ReplayData_RoundTripsWithinQuantizationTolerance()
    {
        var frames = new List<(Vector3 pos, Quaternion rot)[]>();
        for (int f = 0; f < 10; f++) frames.Add(SyntheticFrame(f));

        var replay = ReplayData.FromFrames(frames);
        var bytes = replay.Serialize();
        var roundTripped = ReplayData.Deserialize(bytes);

        Assert.AreEqual(frames.Count, roundTripped.FrameCount);
        for (int f = 0; f < frames.Count; f++)
        {
            var original = frames[f];
            var read = roundTripped.GetFrame(f);
            for (int i = 0; i < ReplayData.ObjectCount; i++)
            {
                Assert.Less(Vector3.Distance(original[i].pos, read[i].pos), 0.01f, $"frame {f} object {i} position");
                Assert.Less(Quaternion.Angle(original[i].rot, read[i].rot), 1f, $"frame {f} object {i} rotation");
            }
        }
    }

    [Test]
    public void ReplayData_TypicalRollSerializesWithinSizeBudget()
    {
        // ~5 s at 30 Hz: a realistic rolling+settling duration.
        var frames = new List<(Vector3 pos, Quaternion rot)[]>();
        for (int f = 0; f < 150; f++) frames.Add(SyntheticFrame(f));

        var bytes = ReplayData.FromFrames(frames).Serialize();
        Assert.LessOrEqual(bytes.Length, 24 * 1024);
    }

    [Test]
    public void ReplayRecorder_HasEnoughFrames_MatchesTheMinimumThreshold()
    {
        var frames = new List<(Vector3 pos, Quaternion rot)[]>();
        for (int f = 0; f < 5; f++) frames.Add(SyntheticFrame(f));
        Assert.IsFalse(ReplayRecorder.HasEnoughFrames(ReplayData.FromFrames(frames)));

        frames.Add(SyntheticFrame(5));
        Assert.IsTrue(ReplayRecorder.HasEnoughFrames(ReplayData.FromFrames(frames)));

        Assert.IsFalse(ReplayRecorder.HasEnoughFrames(null));
    }

    [Test]
    public void ReplayData_WorstCaseRecordingLengthStillWithinSizeBudget()
    {
        // ReplayRecorder caps a recording at 180 frames (its MaxFrames) so even a slow ball
        // plus a long settle can't blow the 24 KB-per-roll budget.
        var frames = new List<(Vector3 pos, Quaternion rot)[]>();
        for (int f = 0; f < 180; f++) frames.Add(SyntheticFrame(f));

        var bytes = ReplayData.FromFrames(frames).Serialize();
        Assert.LessOrEqual(bytes.Length, 24 * 1024);
    }
}
