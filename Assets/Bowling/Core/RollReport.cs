// Everything about one completed roll: who threw it, which pins fell, the mark it scored,
// and (optionally) the replay recorded for it. Eventually this is what gets sent to a server;
// for now it's just handed to RollFinished listeners and IMatchDriver.OnRollFinished.
public struct RollReport
{
    public int playerIndex;
    public bool[] standingBefore;   // length 10, pins[i] was standing right before this throw
    public bool[] knockedMask;      // length 10, true iff standingBefore[i] && that pin is down now
    public string mark;             // the scorecard mark for this roll ("X","/","-", or a digit string)
    public bool foul;
    public float speedMps, angleDeg, hookMag;
    public ReplayData replay;       // may be null if a replay wasn't recorded

    public int KnockedCount => knockedMask == null ? 0 : System.Array.FindAll(knockedMask, b => b).Length;

    // Pure, testable: builds knockedMask from standingBefore + the actual down/up state right now.
    // A foul means nothing counts: every pin is treated as still standing (mask all false).
    public static bool[] BuildKnockedMask(bool[] standingBefore, bool[] isDownNow, bool foul)
    {
        var mask = new bool[standingBefore.Length];
        if (foul) return mask;
        for (int i = 0; i < mask.Length; i++) mask[i] = standingBefore[i] && isDownNow[i];
        return mask;
    }
}
