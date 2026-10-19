using System.Collections.Generic;
using UnityEngine;

// Instant replay: records the ball + 10 pins at 30 Hz while a roll is live, then plays the
// recording back at normal speed. Driven by BowlingGame's own FixedUpdate/Update — not a
// MonoBehaviour itself. The only replay system; obstacles are intentionally not recorded
// (ReplayData is ball+pins only) — BowlingGame just freezes them for the duration instead.
public class ReplayRecorder
{
    // 180 frames (6 s at 30 Hz) caps a serialized ReplayData at 180 * 11 objects * 12 bytes
    // + 4-byte header = ~23.8 KB, safely under the 24 KB-per-roll budget even in the worst case
    // (a slow ball plus a long settle), not just the typical case.
    const int MaxFrames = 180;
    const int MinFramesForReplay = 6; // ~0.2 s of motion is worth replaying
    const float PlaybackSpeed = 1f;

    readonly Transform[] targets; // [0] = ball, [1..10] = pins 1..10
    readonly List<(Vector3 pos, Quaternion rot)[]> frames = new();
    float sampleTime;
    float playbackTime;
    ReplayData playing;
    bool[] kinematicForPlayback; // which pin indices PlayReplay made kinematic, so Tick can undo just those

    public ReplayRecorder(Transform ball, Transform[] pins)
    {
        targets = new Transform[ReplayData.ObjectCount];
        targets[0] = ball;
        for (int i = 0; i < pins.Length; i++) targets[i + 1] = pins[i];
    }

    // Whether a finished ReplayData (from Finish()) has enough frames to be worth replaying.
    public static bool HasEnoughFrames(ReplayData data) => data != null && data.FrameCount >= MinFramesForReplay;

    public void BeginRecording()
    {
        frames.Clear();
        sampleTime = 0f;
    }

    public void Sample()
    {
        if (frames.Count >= MaxFrames) return;
        sampleTime += Time.fixedDeltaTime;
        if (sampleTime < ReplayData.FrameInterval) return;
        sampleTime -= ReplayData.FrameInterval;
        var frame = new (Vector3 pos, Quaternion rot)[targets.Length];
        for (int i = 0; i < targets.Length; i++) frame[i] = (targets[i].position, targets[i].rotation);
        frames.Add(frame);
    }

    public ReplayData Finish()
    {
        var data = ReplayData.FromFrames(frames);
        frames.Clear();
        return data;
    }

    public void PlayReplay(ReplayData data, bool[] standingBefore)
    {
        playing = data;
        playbackTime = 0f;
        kinematicForPlayback = new bool[standingBefore.Length];
        if (targets[0].TryGetComponent(out Rigidbody ballRb)) ballRb.isKinematic = true;
        for (int i = 0; i < standingBefore.Length; i++)
        {
            if (!standingBefore[i]) continue;
            if (targets[i + 1].TryGetComponent(out Rigidbody rb)) { rb.isKinematic = true; kinematicForPlayback[i] = true; }
        }
    }

    public bool TickPlayback(float unscaledDeltaTime)
    {
        playbackTime += unscaledDeltaTime * PlaybackSpeed;
        int frame = Mathf.FloorToInt(playbackTime / ReplayData.FrameInterval);
        bool done = frame >= playing.FrameCount - 1;
        ApplyFrame(Mathf.Min(frame, playing.FrameCount - 1));
        if (done) EndPlayback();
        return done;
    }

    void ApplyFrame(int frame)
    {
        var poses = playing.GetFrame(frame);
        for (int i = 0; i < targets.Length; i++)
        {
            targets[i].SetPositionAndRotation(poses[i].pos, poses[i].rot);
            if (targets[i].TryGetComponent(out Rigidbody rb)) { rb.position = poses[i].pos; rb.rotation = poses[i].rot; }
        }
    }

    void EndPlayback()
    {
        for (int i = 0; i < kinematicForPlayback.Length; i++)
        {
            if (!kinematicForPlayback[i]) continue;
            var rb = targets[i + 1].GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }
}
