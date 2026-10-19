using System;
using UnityEngine;

// A small procedural audio toolkit shared by the sound packs (BowlingAudio) and the background
// music (MusicSynth): oscillators with an envelope, drum hits, filters and a loop-friendly reverb.
// Everything renders into plain float buffers, so the music can be built a few notes per frame.

public enum Wave { Sine, Triangle, Square, Pulse, Saw, SuperSaw, Noise, Bell, Marimba, EPiano, Organ, Pluck, Flute }

public enum Drum { Kick, Snare, Clap, HatClosed, HatOpen, Ride, Brush, Shaker, Crash, Tom, Rim, Taiko, Crackle }

public struct Voice
{
    public Wave wave;
    public float attack, decay, sustain, release;   // seconds, 1/s (exponential), level 0–1, seconds
    public float cutoff;      // one-pole low-pass in Hz (0 = open)
    public float vibrato;     // depth in semitones, ~5.5 Hz
    public float drive;       // soft-clip amount (0 = clean)

    public Voice(Wave wave, float attack = 0.005f, float decay = 3f, float sustain = 0f, float release = 0.08f,
        float cutoff = 0f, float vibrato = 0f, float drive = 0f)
    {
        this.wave = wave; this.attack = attack; this.decay = decay; this.sustain = sustain; this.release = release;
        this.cutoff = cutoff; this.vibrato = vibrato; this.drive = drive;
    }
}

public static class Synth
{
    const float TwoPi = Mathf.PI * 2f;

    public static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

    static void Add(float[] dst, int i, float s, bool wrap)
    {
        if (i < dst.Length) dst[i] += s;
        else if (wrap) dst[i % dst.Length] += s;
    }

    static float Blep(double t, double dt)
    {
        if (t < dt) { t /= dt; return (float)(t + t - t * t - 1.0); }
        if (t > 1.0 - dt) { t = (t - 1.0) / dt; return (float)(t * t + t + t + 1.0); }
        return 0f;
    }

    static float Frac(double x) => (float)(x - Math.Floor(x));

    // One note, added into dst at `start` seconds. With wrap the tail continues from the start of the
    // buffer, so a loop has no gap. glideTo slides the pitch (exponentially) over the note.
    public static void Note(float[] dst, int rate, double start, float dur, float freq, float amp, Voice v,
        bool wrap = false, float glideTo = 0f, int seed = 0)
    {
        int n0 = (int)(start * rate);
        int durN = Mathf.Max(1, (int)(dur * rate));
        int len = durN + (int)(v.release * rate);
        if (v.wave == Wave.Pluck) len += (int)(0.6f * rate); // strings ring on
        double phase = 0, phase2 = 0.31, phase3 = 0.67;
        float lp = 0f, lpK = v.cutoff > 0f ? 1f - Mathf.Exp(-TwoPi * v.cutoff / rate) : 1f;
        float breathLp = 0f;
        var rng = new System.Random(seed * 7919 + n0);
        float[] ks = null;
        int ksPos = 0;
        if (v.wave == Wave.Pluck)
        {
            ks = new float[Mathf.Max(2, Mathf.RoundToInt(rate / freq))];
            float prev = 0f;
            for (int i = 0; i < ks.Length; i++) { prev = prev * 0.5f + (float)(rng.NextDouble() * 2 - 1) * 0.5f; ks[i] = prev; }
        }
        float driveNorm = v.drive > 0f ? 1f / (float)Math.Tanh(1f + v.drive) : 1f;
        float relStart = 1f;

        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float f = freq;
            if (glideTo > 0f) f = freq * Mathf.Pow(glideTo / freq, Mathf.Clamp01(t / dur));
            if (v.vibrato > 0f) f *= Mathf.Pow(2f, v.vibrato * Mathf.Sin(TwoPi * 5.5f * t) * Mathf.Clamp01(t * 2.5f) / 12f);
            double dt = f / rate;
            float p = Frac(phase);
            float s;
            switch (v.wave)
            {
                case Wave.Sine: s = Mathf.Sin(TwoPi * p); break;
                case Wave.Triangle: s = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                case Wave.Square: s = (p < 0.5f ? 1f : -1f) + Blep(p, dt) - Blep(Frac(phase + 0.5), dt); break;
                case Wave.Pulse: s = (p < 0.25f ? 1f : -1f) + Blep(p, dt) - Blep(Frac(phase + 0.75), dt); break;
                case Wave.Saw: s = 2f * p - 1f - Blep(p, dt); break;
                case Wave.SuperSaw:
                {
                    float p2 = Frac(phase2), p3 = Frac(phase3);
                    s = (2f * p - 1f - Blep(p, dt) + 2f * p2 - 1f - Blep(p2, dt * 1.007) + 2f * p3 - 1f - Blep(p3, dt * 0.993)) * 0.45f;
                    phase2 += dt * 1.007; phase3 += dt * 0.993;
                    break;
                }
                case Wave.Noise: s = (float)(rng.NextDouble() * 2 - 1); break;
                case Wave.Bell:
                    s = Mathf.Sin(TwoPi * p) + 0.45f * Mathf.Exp(-t * 2.5f) * Mathf.Sin(TwoPi * (float)Frac(phase * 2.76))
                        + 0.22f * Mathf.Exp(-t * 6f) * Mathf.Sin(TwoPi * Frac(phase * 5.4)) + 0.1f * Mathf.Exp(-t * 11f) * Mathf.Sin(TwoPi * Frac(phase * 8.93));
                    break;
                case Wave.Marimba:
                    s = Mathf.Sin(TwoPi * p) + 0.4f * Mathf.Exp(-t * 16f) * Mathf.Sin(TwoPi * Frac(phase * 4)) + 0.12f * Mathf.Exp(-t * 35f) * Mathf.Sin(TwoPi * Frac(phase * 9.9));
                    break;
                case Wave.EPiano:
                    s = Mathf.Sin(TwoPi * p + 1.3f * Mathf.Exp(-t * 4.5f) * Mathf.Sin(TwoPi * p))
                        + 0.18f * Mathf.Exp(-t * 28f) * Mathf.Sin(TwoPi * Frac(phase * 14));
                    break;
                case Wave.Organ:
                    s = Mathf.Sin(TwoPi * p) + 0.5f * Mathf.Sin(TwoPi * Frac(phase * 2)) + 0.3f * Mathf.Sin(TwoPi * Frac(phase * 3))
                        + 0.15f * Mathf.Sin(TwoPi * Frac(phase * 4)) + 0.1f * Mathf.Sin(TwoPi * Frac(phase * 8));
                    s *= 0.55f;
                    break;
                case Wave.Flute:
                {
                    breathLp += (float)((rng.NextDouble() * 2 - 1) - breathLp) * 0.15f;
                    s = Mathf.Sin(TwoPi * p) + 0.15f * Mathf.Sin(TwoPi * Frac(phase * 2)) + breathLp * 0.35f;
                    break;
                }
                case Wave.Pluck:
                {
                    int next = (ksPos + 1) % ks.Length;
                    s = ks[ksPos];
                    ks[ksPos] = (ks[ksPos] + ks[next]) * 0.4985f;
                    ksPos = next;
                    break;
                }
                default: s = 0f; break;
            }
            phase += dt;

            if (v.cutoff > 0f) { lp += (s - lp) * lpK; s = lp; }
            if (v.drive > 0f) s = (float)Math.Tanh(s * (1f + v.drive)) * driveNorm;

            float env;
            if (v.wave == Wave.Pluck) env = 1f;
            else if (t < v.attack) env = t / Mathf.Max(v.attack, 1e-4f);
            else env = v.sustain + (1f - v.sustain) * Mathf.Exp(-v.decay * (t - v.attack));
            if (i == durN) relStart = env;
            if (i >= durN)
            {
                float r = v.release > 0f ? 1f - (t - dur) / v.release : 0f;
                if (v.wave == Wave.Pluck) r = Mathf.Max(r, Mathf.Exp(-(t - dur) * 6f)); // a plucked string just decays
                env = Mathf.Min(env, relStart) * Mathf.Max(0f, r);
            }
            Add(dst, n0 + i, s * env * amp, wrap);
        }
    }

    // One drum or percussion hit.
    public static void Hit(float[] dst, int rate, double start, Drum d, float amp, bool wrap = false, int seed = 0)
    {
        int n0 = (int)(start * rate);
        var rng = new System.Random(seed * 104729 + n0);
        float Rand() => (float)(rng.NextDouble() * 2 - 1);
        float len = d switch { Drum.Crash => 1.6f, Drum.Ride => 0.9f, Drum.HatOpen => 0.45f, Drum.Taiko => 0.8f, Drum.Kick => 0.45f, Drum.Tom => 0.5f, _ => 0.3f };
        int n = (int)(len * rate);
        float lp = 0f, lp2 = 0f, phase = 0f;
        var bp = new Biquad();
        if (d == Drum.Clap) bp.Bandpass(1300f, 1.2f, rate);
        if (d == Drum.Rim) bp.Bandpass(1700f, 6f, rate);
        if (d == Drum.Brush) bp.Bandpass(3500f, 0.8f, rate);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate, s;
            switch (d)
            {
                case Drum.Kick:
                {
                    float f = 45f + 120f * Mathf.Exp(-t * 32f);
                    phase += f / rate;
                    s = Mathf.Sin(TwoPi * phase) * Mathf.Exp(-t * 7f) + Rand() * 0.25f * Mathf.Exp(-t * 300f);
                    break;
                }
                case Drum.Taiko:
                {
                    float f = 70f + 60f * Mathf.Exp(-t * 18f);
                    phase += f / rate;
                    lp += (Rand() - lp) * 0.08f;
                    s = Mathf.Sin(TwoPi * phase) * Mathf.Exp(-t * 5f) + lp * 0.8f * Mathf.Exp(-t * 25f);
                    break;
                }
                case Drum.Tom:
                {
                    float f = 110f + 50f * Mathf.Exp(-t * 20f);
                    phase += f / rate;
                    s = Mathf.Sin(TwoPi * phase) * Mathf.Exp(-t * 9f);
                    break;
                }
                case Drum.Snare:
                {
                    float noise = Rand();
                    lp += (noise - lp) * 0.35f;
                    phase += 185f / rate;
                    s = Mathf.Sin(TwoPi * phase) * 0.55f * Mathf.Exp(-t * 22f) + (noise - lp * 0.6f) * 0.7f * Mathf.Exp(-t * 15f);
                    break;
                }
                case Drum.Clap:
                {
                    float burst = 0f;
                    for (int k = 0; k < 3; k++) { float tk = t - k * 0.011f; if (tk >= 0f) burst = Mathf.Max(burst, Mathf.Exp(-tk * 180f)); }
                    s = bp.Process(Rand()) * (burst + 0.6f * Mathf.Exp(-Mathf.Max(0f, t - 0.022f) * 18f) * (t > 0.022f ? 1f : 0f)) * 2f;
                    break;
                }
                case Drum.HatClosed:
                case Drum.HatOpen:
                case Drum.Crash:
                case Drum.Ride:
                case Drum.Shaker:
                {
                    float noise = Rand();
                    lp += (noise - lp) * 0.55f;
                    float hp = noise - lp;
                    float decay = d switch { Drum.HatClosed => 55f, Drum.HatOpen => 8f, Drum.Crash => 2.4f, Drum.Ride => 4f, _ => 28f };
                    float attack = d == Drum.Shaker ? Mathf.Clamp01(t / 0.018f) : 1f;
                    s = hp * Mathf.Exp(-t * decay) * attack;
                    if (d == Drum.Ride) s = s * 0.6f + Mathf.Sin(TwoPi * 3200f * t) * 0.12f * Mathf.Exp(-t * 3f) + Mathf.Sin(TwoPi * 5100f * t) * 0.08f * Mathf.Exp(-t * 5f);
                    break;
                }
                case Drum.Brush:
                    s = bp.Process(Rand()) * Mathf.Clamp01(t / 0.012f) * Mathf.Exp(-t * 11f) * 1.4f;
                    break;
                case Drum.Rim:
                    s = bp.Process(Rand()) * Mathf.Exp(-t * 60f) * 3f + Mathf.Sin(TwoPi * 820f * t) * Mathf.Exp(-t * 45f) * 0.5f;
                    break;
                case Drum.Crackle:
                    lp2 += (Rand() - lp2) * 0.4f;
                    s = lp2 * Mathf.Exp(-t * 400f);
                    break;
                default: s = 0f; break;
            }
            Add(dst, n0 + i, s * amp, wrap);
        }
    }

    // Peak-normalise.
    public static void Normalize(float[] data, float peak)
    {
        float max = 0.0001f;
        foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
        float k = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= k;
    }

    // Gentle saturation, so a dense mix gets louder without hard clipping.
    public static void SoftClip(float[] data, float drive)
    {
        float norm = 1f / (float)Math.Tanh(drive);
        for (int i = 0; i < data.Length; i++) data[i] = (float)Math.Tanh(data[i] * drive) * norm;
    }

    // A copy with a reverb tail appended (for one-shot effects).
    public static float[] WithReverb(float[] dry, int rate, float room, float mix, float tail)
    {
        var data = new float[dry.Length + (int)(tail * rate)];
        Array.Copy(dry, data, dry.Length);
        var rev = new Reverb(rate, room, 0.35f);
        for (int i = 0; i < data.Length; i++) data[i] += rev.Process(i < dry.Length ? dry[i] : 0f) * mix;
        // Fade the very end so the tail doesn't click off.
        int fade = Mathf.Min(data.Length, rate / 20);
        for (int i = 0; i < fade; i++) data[data.Length - 1 - i] *= (float)i / fade;
        return data;
    }

    public static AudioClip Clip(string name, float[] data, int rate)
    {
        var clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

// RBJ biquad filter (band-pass / low-pass / high-pass).
public class Biquad
{
    float b0, b1, b2, a1, a2, x1, x2, y1, y2;

    public void Bandpass(float freq, float q, int rate)
    {
        float w = 2f * Mathf.PI * freq / rate, alpha = Mathf.Sin(w) / (2f * q), a0 = 1f + alpha;
        b0 = alpha / a0; b1 = 0f; b2 = -alpha / a0; a1 = -2f * Mathf.Cos(w) / a0; a2 = (1f - alpha) / a0;
    }

    public void Lowpass(float freq, float q, int rate)
    {
        float w = 2f * Mathf.PI * freq / rate, alpha = Mathf.Sin(w) / (2f * q), c = Mathf.Cos(w), a0 = 1f + alpha;
        b0 = (1f - c) / 2f / a0; b1 = (1f - c) / a0; b2 = b0; a1 = -2f * c / a0; a2 = (1f - alpha) / a0;
    }

    public void Highpass(float freq, float q, int rate)
    {
        float w = 2f * Mathf.PI * freq / rate, alpha = Mathf.Sin(w) / (2f * q), c = Mathf.Cos(w), a0 = 1f + alpha;
        b0 = (1f + c) / 2f / a0; b1 = -(1f + c) / a0; b2 = b0; a1 = -2f * c / a0; a2 = (1f - alpha) / a0;
    }

    public float Process(float x)
    {
        float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
        x2 = x1; x1 = x; y2 = y1; y1 = y;
        return y;
    }
}

// Mono Schroeder/Freeverb-style reverb: four damped combs into two all-passes.
public class Reverb
{
    readonly float[][] comb;
    readonly float[] combLp;
    readonly int[] combPos;
    readonly float[][] allpass;
    readonly int[] apPos;
    readonly float feedback, damp;

    public Reverb(int rate, float room, float damp)
    {
        float k = rate / 44100f;
        int[] cl = { 1116, 1188, 1277, 1356, 1422 }, al = { 556, 441, 341 };
        comb = new float[cl.Length][];
        for (int i = 0; i < cl.Length; i++) comb[i] = new float[Mathf.Max(1, (int)(cl[i] * k))];
        combLp = new float[cl.Length];
        combPos = new int[cl.Length];
        allpass = new float[al.Length][];
        for (int i = 0; i < al.Length; i++) allpass[i] = new float[Mathf.Max(1, (int)(al[i] * k))];
        apPos = new int[al.Length];
        feedback = Mathf.Lerp(0.7f, 0.9f, room);
        this.damp = damp;
    }

    public float Process(float x)
    {
        float input = x * 0.2f, sum = 0f;
        for (int i = 0; i < comb.Length; i++)
        {
            var buf = comb[i];
            float y = buf[combPos[i]];
            combLp[i] = y * (1f - damp) + combLp[i] * damp;
            buf[combPos[i]] = input + combLp[i] * feedback;
            combPos[i] = (combPos[i] + 1) % buf.Length;
            sum += y;
        }
        for (int i = 0; i < allpass.Length; i++)
        {
            var buf = allpass[i];
            float b = buf[apPos[i]];
            buf[apPos[i]] = sum + b * 0.5f;
            sum = b - sum;
            apPos[i] = (apPos[i] + 1) % buf.Length;
        }
        return sum;
    }
}
