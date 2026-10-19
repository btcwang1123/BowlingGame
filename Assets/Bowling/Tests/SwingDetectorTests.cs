using NUnit.Framework;
using UnityEngine;

// Ports the same scenarios as Bowling/Swing Check (Assets/Bowling/Editor/BowlingSwingCheck.cs)
// into individual EditMode tests against SwingDetector directly. The menu tool stays as-is.
public class SwingDetectorTests
{
    const float Rate = 60f; // samples per second, like a phone at 60 fps

    // A pendulum swing about `axis`: still, backswing (0.5 s), forward swing (0.45 s), release at its peak.
    static SwingResult Swing(Vector3 axis, float back, float forward, float yawRate = 0f, float twist = 0f, Vector3? gravity = null, bool lateRelease = false,
        bool followThrough = false, SwingDetector detector = null)
    {
        var d = detector ?? new SwingDetector();
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
            float u = (i + 1f) / nf;                    // 0 -> 1 through the forward swing
            Vector3 w = axis * forward * Mathf.Sin(Mathf.PI * 0.5f * u); // speeds up to the release point
            w += up * yawRate;
            if (u > 0.6f) w += Vector3.up * twist;      // roll around the phone's long axis near release
            Add(w);
        }
        // Holding on into the follow-through: the arm slows to a fifth of its speed before the thumb lifts.
        if (lateRelease)
            for (int i = 1; i <= 15; i++) Add(axis * forward * Mathf.Lerp(1f, 0.2f, i / 15f));
        // Released automatically: the arm slows right down, then wobbles back a little.
        if (followThrough)
        {
            for (int i = 1; i <= 12; i++) Add(axis * forward * Mathf.Lerp(1f, 0.1f, i / 12f));
            for (int i = 0; i < 8; i++) Add(-axis * 0.6f);
        }
        return d.Evaluate(releaseAtPeak: followThrough);
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

    [Test]
    public void NormalSwing()
    {
        var r = Swing(Vector3.right, back: 5f, forward: 8f);
        Assert.IsTrue(r.valid);
        Assert.Less(Mathf.Abs(r.peakRate - 8f), 1f);
        Assert.Less(Mathf.Abs(r.yawDeg), 2f);
        Assert.Less(Mathf.Abs(r.twist), 0.5f);
    }

    [Test]
    public void GentleSwing()
    {
        var r = Swing(Vector3.right, back: 3f, forward: 4f);
        Assert.IsTrue(r.valid);
        Assert.Greater(r.peakRate, 3f);
        Assert.Less(r.peakRate, 5f);
    }

    [Test]
    public void OtherGripTiltedAxis()
    {
        var r = Swing(new Vector3(0.6f, 0f, 0.8f), back: 5f, forward: 8f, gravity: new Vector3(0f, -1f, 0f));
        Assert.IsTrue(r.valid);
        Assert.Less(Mathf.Abs(r.peakRate - 8f), 1f);
    }

    [Test]
    public void ReversedAxis()
    {
        var r = Swing(-Vector3.right, back: 5f, forward: 8f);
        Assert.IsTrue(r.valid);
        Assert.Less(Mathf.Abs(r.peakRate - 8f), 1f);
    }

    [Test]
    public void SwingAcrossTheBody()
    {
        var r = Swing(Vector3.right, back: 5f, forward: 8f, yawRate: 1f);
        Assert.IsTrue(r.valid);
        Assert.Less(r.yawDeg, -10f);
    }

    [Test]
    public void SwingOut()
    {
        var r = Swing(Vector3.right, back: 5f, forward: 8f, yawRate: -1f);
        Assert.IsTrue(r.valid);
        Assert.Greater(r.yawDeg, 10f);
    }

    [Test]
    public void WristTwist()
    {
        var r = Swing(Vector3.right, back: 5f, forward: 8f, twist: 4f);
        Assert.IsTrue(r.valid);
        Assert.Greater(r.twist, 2.5f);
    }

    [Test]
    public void NormalSwingIsNotAFoul()
    {
        var r = Swing(Vector3.right, back: 5f, forward: 8f);
        Assert.IsTrue(r.valid);
        Assert.IsFalse(r.late);
    }

    [Test]
    public void LateReleaseIsAFoul()
    {
        var r = Swing(Vector3.right, back: 5f, forward: 8f, lateRelease: true);
        Assert.IsTrue(r.valid);
        Assert.IsTrue(r.late);
    }

    [Test]
    public void WristFlickOnlyIsRejected()
    {
        var r = Flick();
        Assert.IsFalse(r.valid);
        Assert.AreEqual("swing_too_short", r.failKey);
    }

    [Test]
    public void ForwardOnlyNoBackswingIsRejected()
    {
        var r = Swing(Vector3.right, back: 0f, forward: 8f);
        Assert.IsFalse(r.valid);
        Assert.AreEqual("no_backswing", r.failKey);
    }

    [Test]
    public void TooSlowIsRejected()
    {
        var r = Swing(Vector3.right, back: 1f, forward: 1.2f);
        Assert.IsFalse(r.valid);
        Assert.AreEqual("swing_too_slow", r.failKey);
    }

    [Test]
    public void AutoReleaseReadsSpeedAtThePeak()
    {
        // A brisk swing released after the arm slowed down must not read as slow.
        var r = Swing(Vector3.right, back: 4f, forward: 6f, followThrough: true);
        Assert.IsTrue(r.valid, r.failKey);
        Assert.Less(Mathf.Abs(r.peakRate - 6f), 1f);
        Assert.IsFalse(r.late);
    }

    [Test]
    public void CalibrationAcceptsAGentleSwing()
    {
        var d = new SwingDetector { MinForwardRate = 1f, MinBackswingRate = 0.8f, MinForwardAngle = 0.5f };
        var r = Swing(Vector3.right, back: 1.2f, forward: 2f, followThrough: true, detector: d);
        Assert.IsTrue(r.valid, r.failKey);
    }

    // Ready-then-swing flow: pausing at the top of the backswing must not look like a finished swing.
    [Test]
    public void ProgressCountsBackswingThenForward()
    {
        var d = new SwingDetector();
        d.Begin();
        float t = 0f;
        void Add(Vector3 w) { d.Add(new MotionSample { t = t, gyro = w, gravity = Vector3.down }); t += 1f / Rate; }
        for (int i = 0; i < 12; i++) Add(Vector3.zero);
        Assert.AreEqual(0, d.Progress(2.5f).phases);

        for (int i = 0; i < 15; i++) Add(Vector3.right * -5f * Mathf.Sin(Mathf.PI * 0.5f * (i + 1f) / 15f));
        var mid = d.Progress(2.5f);
        Assert.AreEqual(1, mid.phases);
        Assert.IsTrue(mid.lastActive, "still swinging back");

        for (int i = 0; i < 15; i++) Add(Vector3.zero); // pause at the top
        var top = d.Progress(2.5f);
        Assert.AreEqual(1, top.phases);
        Assert.IsFalse(top.lastActive);

        for (int i = 0; i < 27; i++) Add(Vector3.right * 8f * Mathf.Sin(Mathf.PI * 0.5f * (i + 1f) / 27f));
        for (int i = 0; i < 12; i++) Add(Vector3.right * 8f * Mathf.Lerp(1f, 0.1f, (i + 1f) / 12f));
        Assert.AreEqual(2, d.Progress(2.5f).phases);
        Assert.IsTrue(d.Evaluate(releaseAtPeak: true).valid);
    }
}
