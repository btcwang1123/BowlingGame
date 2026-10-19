using System.Collections.Generic;
using UnityEngine;

// One reading from the phone's motion sensors, in the phone's own axes.
public struct MotionSample
{
    public float t;          // seconds
    public Vector3 gyro;     // angular velocity, rad/s
    public Vector3 gravity;  // gravity direction (any length; zero if unknown)
}

public struct SwingResult
{
    public bool valid;
    public string failKey;   // Loc key explaining why the swing didn't count
    public float peakRate;   // swing speed at release, rad/s
    public float yawDeg;     // how far the swing turned left/right (+ = right)
    public float twist;      // wrist roll at release, rad/s (+ = clockwise, hooks right for a right-hander)
    public bool late;        // let go well after the bottom of the swing: a foul (stepped over the line)
    public float forwardDeg; // arc swept by the forward swing (arm swing amplitude), degrees
    public float backDeg;    // arc swept by the backswing, degrees
    public float peakAccel;  // fastest speed-up during the forward swing, rad/s²
}

// Recognises a bowling swing from motion samples recorded while the thumb is on the screen.
// Pure C#, so it can be tested with synthetic swings (Bowling > Swing Check).
//
// How it reads a swing, regardless of how the phone is held:
//  1. The swing axis is the axis the phone rotates around the most (dominant eigenvector of ω ωᵀ).
//  2. The forward swing is the last phase before release; the backswing is the phase before it,
//     rotating the other way. Both must be big enough, which rules out a flick of the wrist.
//  3. Speed = rotation rate at release; aim = turning around "up" during the forward swing;
//     spin = rotation around the phone's long axis near release.
public class SwingDetector
{
    public const float DefaultMinForwardRate = 2f;

    // What counts as a real swing. MotionInput loosens these during calibration (whatever the
    // player's natural swing is, it is the baseline) and to fit a player whose calibrated swing is gentle.
    public float MinForwardRate = DefaultMinForwardRate; // rad/s at release
    public float MinBackswingRate = 1.5f;                // rad/s at the peak of the backswing
    public float MinForwardAngle = 0.7f;                 // rad (~40°) swept in the forward swing
    const float ReleaseWindow = 0.12f;    // s, averaged for speed and direction at release
    const float SpinWindow = 0.2f;        // s
    const float LateFraction = 0.35f;     // released after the swing slowed below this share of its peak = foul

    readonly List<MotionSample> samples = new();

    public int Count => samples.Count;

    public void Begin() => samples.Clear();

    public void Add(MotionSample s) => samples.Add(s);

    // Peak rotation rate so far (used to tell when a swing has started).
    public float CurrentRate => samples.Count == 0 ? 0f : samples[^1].gyro.magnitude;

    // releaseAtPeak: the ball was let go automatically after the arm had already slowed into the
    // follow-through (no thumb on the screen), so read the swing at the peak of the forward swing
    // instead of at the last sample, which would make every swing look slow.
    public SwingResult Evaluate(bool releaseAtPeak = false)
    {
        if (samples.Count < 8 || samples[^1].t - samples[0].t < 0.25f)
            return Fail("swing_too_short");

        int n = samples.Count;
        Vector3 axis = DominantAxis(n);
        if (releaseAtPeak)
        {
            n = PeakOfLastSwing(axis);
            axis = DominantAxis(n);
        }
        float end = samples[n - 1].t;

        // Direction of the forward swing: the average spin around the axis just before release.
        float release = 0f; int count = 0;
        for (int i = n - 1; i >= 0 && samples[i].t >= end - ReleaseWindow; i--) { release += Vector3.Dot(samples[i].gyro, axis); count++; }
        release /= Mathf.Max(count, 1);
        float sign = Mathf.Sign(release);
        if (Mathf.Abs(release) < MinForwardRate) return Fail("swing_too_slow");

        // Walk back through the forward swing.
        int fStart = n - 1;
        float swept = 0f;
        while (fStart > 0 && Vector3.Dot(samples[fStart - 1].gyro, axis) * sign > 0.5f)
        {
            swept += Vector3.Dot(samples[fStart].gyro, axis) * sign * (samples[fStart].t - samples[fStart - 1].t);
            fStart--;
        }
        if (swept < MinForwardAngle) return Fail("swing_too_short");

        // Before it, there must be a backswing rotating the other way.
        float backPeak = 0f;
        for (int i = fStart; i >= 0 && samples[i].t >= samples[fStart].t - 1.5f; i--)
            backPeak = Mathf.Max(backPeak, -Vector3.Dot(samples[i].gyro, axis) * sign);
        if (backPeak < MinBackswingRate) return Fail("no_backswing");

        // Speed: peak rate in the release window.
        float peak = 0f;
        for (int i = n - 1; i >= fStart && samples[i].t >= end - ReleaseWindow; i--)
            peak = Mathf.Max(peak, Mathf.Abs(Vector3.Dot(samples[i].gyro, axis)));

        // Late release: the arm had already slowed right down (well into the follow-through) when the
        // thumb came up. In the game that means the bowler slid over the foul line.
        float forwardPeak = 0f;
        for (int i = fStart; i < n; i++) forwardPeak = Mathf.Max(forwardPeak, Vector3.Dot(samples[i].gyro, axis) * sign);
        float atRelease = Vector3.Dot(samples[n - 1].gyro, axis) * sign;
        bool late = atRelease < forwardPeak * LateFraction;

        // Backswing arc: rotation the other way, walking back from the start of the forward swing.
        float backSwept = 0f;
        for (int i = fStart; i > 0 && samples[i].t >= samples[fStart].t - 1.5f; i--)
        {
            float r = -Vector3.Dot(samples[i].gyro, axis) * sign;
            if (r > 0f) backSwept += r * (samples[i].t - samples[i - 1].t);
        }

        // Acceleration: steepest rise of the swing rate over ~3 samples during the forward swing.
        float accel = 0f;
        for (int i = fStart + 3; i < n; i++)
        {
            float dt = samples[i].t - samples[i - 3].t;
            if (dt <= 1e-4f) continue;
            float rise = (Vector3.Dot(samples[i].gyro, axis) - Vector3.Dot(samples[i - 3].gyro, axis)) * sign / dt;
            accel = Mathf.Max(accel, rise);
        }

        // Aim: rotation around "up" during the forward swing. A straight pendulum swing has none.
        float yaw = 0f;
        for (int i = fStart + 1; i < n; i++)
        {
            Vector3 g = samples[i].gravity;
            if (g.sqrMagnitude < 1e-4f) continue;
            Vector3 up = -g.normalized;
            yaw += Vector3.Dot(samples[i].gyro, up) * (samples[i].t - samples[i - 1].t);
        }

        // Spin: roll around the phone's long axis (made perpendicular to the swing axis).
        Vector3 longAxis = Vector3.up - Vector3.Dot(Vector3.up, axis) * axis;
        float twist = 0f; count = 0;
        if (longAxis.sqrMagnitude > 1e-4f)
        {
            longAxis.Normalize();
            for (int i = n - 1; i >= 0 && samples[i].t >= end - SpinWindow; i--) { twist += Vector3.Dot(samples[i].gyro, longAxis); count++; }
            twist /= Mathf.Max(count, 1);
        }

        return new SwingResult
        {
            valid = true,
            peakRate = peak,
            yawDeg = -yaw * Mathf.Rad2Deg, // turning counter-clockwise (left) about up is negative aim
            twist = twist,
            late = late,
            forwardDeg = swept * Mathf.Rad2Deg,
            backDeg = backSwept * Mathf.Rad2Deg,
            peakAccel = accel,
        };
    }

    // Live progress of a swing that is still being recorded: how many big phases (turning one way,
    // then the other) have happened so far, and whether the latest one is still under way. Phases
    // peaking under minRate, and wobbles that turn the same way as the last counted phase, don't count.
    public (int phases, bool lastActive) Progress(float minRate)
    {
        int n = samples.Count;
        if (n < 3) return (0, false);
        Vector3 axis = DominantAxis(n, 0f);
        int phases = 0;
        float countedSign = 0f, runSign = 0f, runPeak = 0f;
        for (int i = 0; i < n; i++)
        {
            float r = Vector3.Dot(samples[i].gyro, axis);
            if (Mathf.Abs(r) < 0.5f) continue;
            float sg = Mathf.Sign(r);
            if (sg != runSign)
            {
                if (runPeak >= minRate && runSign != countedSign) { phases++; countedSign = runSign; }
                runSign = sg; runPeak = 0f;
            }
            runPeak = Mathf.Max(runPeak, Mathf.Abs(r));
        }
        bool active = false;
        if (runPeak >= minRate && runSign != countedSign)
        {
            phases++;
            float now = Vector3.Dot(samples[n - 1].gyro, axis) * runSign;
            active = now >= runPeak * 0.5f;
        }
        return (phases, active);
    }

    // Dominant eigenvector of Σ ω ωᵀ by power iteration: the axis the swing rotates around.
    // Samples in the spin window are left out, so a wrist roll at release doesn't tilt the axis.
    Vector3 DominantAxis(int n, float tail = SpinWindow)
    {
        float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        float cutoff = samples[n - 1].t - tail;
        for (int k = 0; k < n; k++)
        {
            var s = samples[k];
            if (s.t > cutoff) break;
            Vector3 w = s.gyro;
            xx += w.x * w.x; xy += w.x * w.y; xz += w.x * w.z;
            yy += w.y * w.y; yz += w.y * w.z; zz += w.z * w.z;
        }
        Vector3 v = new Vector3(1f, 0.7f, 0.4f).normalized;
        for (int i = 0; i < 30; i++)
        {
            var next = new Vector3(xx * v.x + xy * v.y + xz * v.z, xy * v.x + yy * v.y + yz * v.z, xz * v.x + yz * v.y + zz * v.z);
            if (next.sqrMagnitude < 1e-12f) break;
            v = next.normalized;
        }
        return v;
    }

    // Sample count up to and including the peak of the most recent real swing phase. Small wobbles
    // after the follow-through (well under the swing's top speed) are skipped.
    int PeakOfLastSwing(Vector3 axis)
    {
        float top = 0f;
        foreach (var s in samples) top = Mathf.Max(top, Mathf.Abs(Vector3.Dot(s.gyro, axis)));
        float significant = Mathf.Max(0.5f, top * 0.35f);

        int i = samples.Count - 1;
        while (i > 0)
        {
            // Skip anything slow, then take the phase that turns one way.
            while (i > 0 && Mathf.Abs(Vector3.Dot(samples[i].gyro, axis)) < 0.5f) i--;
            float sign = Mathf.Sign(Vector3.Dot(samples[i].gyro, axis));
            int best = i; float bestRate = 0f;
            while (i >= 0 && Vector3.Dot(samples[i].gyro, axis) * sign >= 0.5f)
            {
                float r = Vector3.Dot(samples[i].gyro, axis) * sign;
                if (r > bestRate) { bestRate = r; best = i; }
                i--;
            }
            if (bestRate >= significant) return best + 1;
            if (i < 0) break;
        }
        return samples.Count;
    }

    static SwingResult Fail(string key) => new() { valid = false, failKey = key };
}
