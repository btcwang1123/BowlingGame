using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Feeds SwingDetector synthetic sensor data for typical swings (and ways to cheat it) and checks the verdicts.
public static class BowlingSwingCheck
{
    const float Rate = 60f; // samples per second, like a phone at 60 fps

    [MenuItem("Bowling/Swing Check")]
    public static void Run()
    {
        int failures = 0;
        void Expect(string name, SwingResult r, bool valid, string failKey = null, System.Func<SwingResult, bool> extra = null, string detail = "")
        {
            bool ok = r.valid == valid && (valid || r.failKey == failKey) && (extra == null || extra(r));
            if (!ok) failures++;
            Debug.Log($"[BowlingSwingCheck] {(ok ? "PASS" : "FAIL")} {name}: valid {r.valid} {r.failKey} peak {r.peakRate:0.0} yaw {r.yawDeg:0.0}° twist {r.twist:0.0} {detail}");
        }

        var x = Vector3.right;
        Expect("normal swing", Swing(x, back: 5f, forward: 8f), true, extra: r => Mathf.Abs(r.peakRate - 8f) < 1f && Mathf.Abs(r.yawDeg) < 2f && Mathf.Abs(r.twist) < 0.5f);
        Expect("gentle swing", Swing(x, back: 3f, forward: 4f), true, extra: r => r.peakRate > 3f && r.peakRate < 5f);
        Expect("other grip (tilted axis)", Swing(new Vector3(0.6f, 0f, 0.8f), back: 5f, forward: 8f, gravity: new Vector3(0f, -1f, 0f)), true,
            extra: r => Mathf.Abs(r.peakRate - 8f) < 1f);
        Expect("reversed axis", Swing(-x, back: 5f, forward: 8f), true, extra: r => Mathf.Abs(r.peakRate - 8f) < 1f);
        Expect("swing across the body", Swing(x, back: 5f, forward: 8f, yawRate: 1f), true, extra: r => r.yawDeg < -10f);
        Expect("swing out", Swing(x, back: 5f, forward: 8f, yawRate: -1f), true, extra: r => r.yawDeg > 10f);
        Expect("wrist twist", Swing(x, back: 5f, forward: 8f, twist: 4f), true, extra: r => r.twist > 2.5f);
        Expect("normal swing is not a foul", Swing(x, back: 5f, forward: 8f), true, extra: r => !r.late);
        Expect("late release = foul", Swing(x, back: 5f, forward: 8f, lateRelease: true), true, extra: r => r.late);
        Expect("wrist flick only", Flick(), false, "swing_too_short");
        Expect("forward only, no backswing", Swing(x, back: 0f, forward: 8f), false, "no_backswing");
        Expect("too slow", Swing(x, back: 1f, forward: 1.2f), false, "swing_too_slow");

        Debug.Log(failures == 0 ? "[BowlingSwingCheck] all passed" : $"[BowlingSwingCheck] {failures} FAILED");
    }

    // A pendulum swing about `axis`: still, backswing (0.5 s), forward swing (0.45 s), release at its peak.
    static SwingResult Swing(Vector3 axis, float back, float forward, float yawRate = 0f, float twist = 0f, Vector3? gravity = null, bool lateRelease = false)
    {
        var d = new SwingDetector();
        d.Begin();
        axis.Normalize();
        Vector3 g = gravity ?? new Vector3(0f, 0f, -1f);
        Vector3 up = -g.normalized;
        float t = 0f;
        void Add(Vector3 w) { d.Add(new MotionSample { t = t, gyro = w, gravity = g }); t += 1f / Rate; }

        for (int i = 0; i < 12; i++) Add(Vector3.zero);
        int nb = (int)(0.5f * Rate), nf = (int)(0.45f * Rate);
        for (int i = 0; i < nb; i++) Add(-axis * back * Mathf.Sin(Mathf.PI * i / nb));
        for (int i = 0; i < nf; i++)
        {
            float u = (i + 1f) / nf;                    // 0 → 1 through the forward swing
            Vector3 w = axis * forward * Mathf.Sin(Mathf.PI * 0.5f * u); // speeds up to the release point
            w += up * yawRate;
            if (u > 0.6f) w += Vector3.up * twist;      // roll around the phone's long axis near release
            Add(w);
        }
        // Holding on into the follow-through: the arm slows to a fifth of its speed before the thumb lifts.
        if (lateRelease)
            for (int i = 1; i <= 15; i++) Add(axis * forward * Mathf.Lerp(1f, 0.2f, i / 15f));
        return d.Evaluate();
    }

    // A quick snap of the wrist with no real swing.
    static SwingResult Flick()
    {
        var d = new SwingDetector();
        d.Begin();
        float t = 0f;
        for (int i = 0; i < 20; i++) { d.Add(new MotionSample { t = t, gyro = Vector3.zero, gravity = Vector3.down }); t += 1f / Rate; }
        for (int i = 0; i < 4; i++) { d.Add(new MotionSample { t = t, gyro = Vector3.right * -3f, gravity = Vector3.down }); t += 1f / Rate; }
        for (int i = 0; i < 5; i++) { d.Add(new MotionSample { t = t, gyro = Vector3.right * 10f, gravity = Vector3.down }); t += 1f / Rate; }
        return d.Evaluate();
    }
}
