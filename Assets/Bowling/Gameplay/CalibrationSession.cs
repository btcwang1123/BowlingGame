using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// On-lane calibration: the player throws two straight balls, two left hooks and two right hooks
// with their real swing (ball at the chest → backswing → forward swing). From those it learns their
// comfortable swing speed, their natural left/right drift, and how their wrist roll differs
// between a straight ball and each hook, so later throws follow what the player meant.
public class CalibrationSession
{
    public enum Kind { Straight, Left, Right }

    public static readonly Kind[] Steps = { Kind.Straight, Kind.Straight, Kind.Left, Kind.Left, Kind.Right, Kind.Right };

    const float TargetHook = 0.11f;       // m/s²: what a typical calibrated hook throw gives
    const float MinTwistSeparation = 0.6f; // rad/s between left and right hooks to trust the spin reading

    readonly List<(Kind kind, SwingResult swing)> results = new();

    public int Step => results.Count;
    public bool Done => results.Count >= Steps.Length;
    public Kind CurrentKind => Steps[Mathf.Min(results.Count, Steps.Length - 1)];
    public IReadOnlyList<(Kind kind, SwingResult swing)> Results => results;

    // Filled by Apply().
    public float SwingSpeed { get; private set; }       // rad/s
    public float Amplitude { get; private set; }        // forward swing arc, degrees
    public float Backswing { get; private set; }        // backswing arc, degrees
    public float Accel { get; private set; }            // rad/s²
    public float AimTrim { get; private set; }          // degrees
    public float Consistency { get; private set; }      // 0–1: how alike the straight-ball speeds were
    public float LeftTwist { get; private set; }        // wrist roll relative to a straight ball, rad/s
    public float RightTwist { get; private set; }
    public bool SpinLearned { get; private set; }

    public void Add(SwingResult swing)
    {
        if (!Done) results.Add((CurrentKind, swing));
    }

    // The ball the game rolls during calibration: the player's real speed, straight down the
    // middle or hooking the way the step asked for, so they see what that motion will do.
    public (float speed, float angle, float hook) DemoThrow(SwingResult swing, Kind kind)
    {
        var (speed, _, _) = MotionInput.ToThrow(swing);
        float hook = kind switch { Kind.Left => -TargetHook, Kind.Right => TargetHook, _ => 0f };
        return (speed, 0f, hook);
    }

    // Work out the player's profile and save it to GameSettings.
    public void Apply()
    {
        var all = results.Select(r => r.swing).ToList();
        var straight = results.Where(r => r.kind == Kind.Straight).Select(r => r.swing).ToList();
        var left = results.Where(r => r.kind == Kind.Left).Select(r => r.swing).ToList();
        var right = results.Where(r => r.kind == Kind.Right).Select(r => r.swing).ToList();
        if (all.Count == 0) return;

        SwingSpeed = all.Average(s => s.peakRate);
        Amplitude = all.Average(s => s.forwardDeg);
        Backswing = all.Average(s => s.backDeg);
        Accel = all.Average(s => s.peakAccel);
        GameSettings.SwingReference = SwingSpeed;
        GameSettings.SetSwingProfile(Amplitude, Accel);

        // A straight ball should go straight: dial out the player's natural sideways drift.
        float flipAim = GameSettings.FlipAim ? -1f : 1f;
        AimTrim = straight.Count > 0 ? Mathf.Clamp(straight.Average(s => s.yawDeg * 0.08f * flipAim), -2.5f, 2.5f) : 0f;
        GameSettings.AimOffsetDeg = AimTrim;

        if (straight.Count > 1)
        {
            float mean = straight.Average(s => s.peakRate);
            float spread = straight.Max(s => s.peakRate) - straight.Min(s => s.peakRate);
            Consistency = Mathf.Clamp01(1f - spread / Mathf.Max(mean, 0.1f));
        }
        else Consistency = 1f;

        // Spin: how far each hook's wrist roll sits from a straight ball's.
        float bias = straight.Count > 0 ? straight.Average(s => s.twist) : 0f;
        LeftTwist = left.Count > 0 ? left.Average(s => s.twist) - bias : 0f;
        RightTwist = right.Count > 0 ? right.Average(s => s.twist) - bias : 0f;
        float separation = RightTwist - LeftTwist;
        SpinLearned = left.Count > 0 && right.Count > 0 && Mathf.Abs(separation) >= MinTwistSeparation;
        if (SpinLearned)
        {
            // Signed scale: the player's "right hook" roll gives +TargetHook whichever way it turns.
            float scale = 2f * TargetHook / separation;
            float straightNoise = straight.Count > 0 ? straight.Max(s => Mathf.Abs(s.twist - bias)) : 0f;
            float smallerHook = Mathf.Min(Mathf.Abs(LeftTwist), Mathf.Abs(RightTwist));
            float deadZone = Mathf.Clamp(Mathf.Max(0.4f, straightNoise * 1.2f, smallerHook * 0.3f), 0.3f, Mathf.Max(0.3f, smallerHook * 0.7f));
            GameSettings.FlipSpin = false;
            GameSettings.SetSpinCalibration(true, bias, scale, deadZone);
        }
        GameSettings.Calibrated = true;
    }
}
