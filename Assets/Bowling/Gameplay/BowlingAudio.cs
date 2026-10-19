using System.Collections.Generic;
using UnityEngine;

// Sound effect packs the player picks in Settings. All are synthesized at startup (SynthKit), so the
// project needs no audio files; swap any clip for a recorded one later by assigning it in the Inspector.
public enum SoundPack { Alley, Neon, Arcade, Cartoon }

// All game sounds. Background music is BowlingMusic's job (added alongside this component).
public class BowlingAudio : MonoBehaviour
{
    public static BowlingAudio Instance { get; private set; }

    const int Rate = 44100;

    // Optional recorded overrides: when set, they replace that sound in every pack.
    [SerializeField] AudioClip roll, ballHitPin, thud, strike, spare, miss, gameOver, newBest, boom;
    [SerializeField] AudioClip[] pinClacks;
    [SerializeField, Range(0f, 1f)] float volume = 0.8f;

    class Kit
    {
        public AudioClip roll, ballHitPin, thud, strike, spare, miss, gameOver, newBest, boom;
        public AudioClip[] pinClacks;
    }

    static readonly Dictionary<SoundPack, Kit> kits = new();
    Kit kit;
    SoundPack loadedPack = (SoundPack)(-1);

    AudioSource oneShots, rollLoop;
    BowlingBall ball;

    // Many pins collide in the same instant; cap how many clacks start per window so it doesn't clip.
    const float ClackWindow = 0.05f;
    const int MaxClacksPerWindow = 4;
    float windowStart;
    int clacksInWindow;

    void Awake()
    {
        Instance = this;
        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        rollLoop = gameObject.AddComponent<AudioSource>();
        rollLoop.loop = true;
        rollLoop.volume = 0f;
        ApplyPack();
        ball = FindAnyObjectByType<BowlingBall>();
        if (!TryGetComponent<BowlingMusic>(out _)) gameObject.AddComponent<BowlingMusic>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Load (synthesizing on first use) the pack chosen in Settings.
    public void ApplyPack()
    {
        var pack = GameSettings.Sounds;
        if (pack == loadedPack && kit != null) return;
        loadedPack = pack;
        if (!kits.TryGetValue(pack, out kit)) kits[pack] = kit = Build(pack);
        // (Unity objects need == null, not ??=, to detect "not assigned".)
        if (roll != null) kit.roll = roll;
        if (ballHitPin != null) kit.ballHitPin = ballHitPin;
        if (thud != null) kit.thud = thud;
        if (strike != null) kit.strike = strike;
        if (spare != null) kit.spare = spare;
        if (miss != null) kit.miss = miss;
        if (gameOver != null) kit.gameOver = gameOver;
        if (newBest != null) kit.newBest = newBest;
        if (boom != null) kit.boom = boom;
        if (pinClacks != null && pinClacks.Length > 0) kit.pinClacks = pinClacks;
        if (rollLoop.clip != kit.roll)
        {
            rollLoop.clip = kit.roll;
            rollLoop.Play();
        }
    }

    // Settings: let the player hear a pack — a ball into the pins, then the strike sound.
    public void Preview()
    {
        ApplyPack();
        oneShots.Stop();
        Play(kit.ballHitPin, 0.8f);
        for (int i = 0; i < 3; i++) Play(kit.pinClacks[i % kit.pinClacks.Length], 0.7f - i * 0.15f);
        Invoke(nameof(PreviewStrike), 0.35f);
    }

    void PreviewStrike() => Play(kit.strike, 0.8f);

    void Update()
    {
        // Rolling rumble follows the ball: louder and higher when fast, silent once it's in the pit.
        float target = 0f, pitch = 1f;
        if (ball != null && ball.IsRolling)
        {
            Vector3 p = ball.transform.position;
            float speed = ball.Velocity.magnitude;
            bool onSurface = p.y > -0.1f && p.z < BowlingLane.LaneEndZ;
            if (onSurface)
            {
                target = Mathf.Clamp01(speed / 8f) * Mathf.Lerp(1f, 0.6f, p.z / BowlingLane.HeadPinZ);
                pitch = Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(speed / 10f));
                if (Mathf.Abs(p.x) > BowlingLane.HalfWidth) pitch *= 0.85f; // hollow gutter sound
            }
        }
        rollLoop.volume = Mathf.MoveTowards(rollLoop.volume, target * volume, Time.deltaTime * 4f);
        rollLoop.pitch = pitch;
        BowlingMusic.Duck = ball != null && ball.IsRolling; // let the pins be heard
    }

    // impact = relative speed of the collision in m/s.
    public void PinHit(float impact)
    {
        if (Time.time - windowStart > ClackWindow) { windowStart = Time.time; clacksInWindow = 0; }
        if (++clacksInWindow > MaxClacksPerWindow) return;
        Play(kit.pinClacks[Random.Range(0, kit.pinClacks.Length)], Mathf.Clamp01(impact / 4f));
    }

    public void BallHitPin(float impact) => Play(kit.ballHitPin, Mathf.Clamp01(impact / 6f));
    public void Thud(float impact) => Play(kit.thud, Mathf.Clamp01(impact / 5f));
    public void Strike() => Play(kit.strike, 0.8f);
    public void Spare() => Play(kit.spare, 0.8f);
    public void Miss() => Play(kit.miss, 0.7f);
    public void Boom() => Play(kit.boom, 1f);
    public void GameOver(bool best) => Play(best ? kit.newBest : kit.gameOver, 0.8f);

    void Play(AudioClip clip, float loudness)
    {
        if (clip != null && loudness > 0.01f) oneShots.PlayOneShot(clip, loudness * volume);
    }

    // ---- The packs ----

    static Kit Build(SoundPack pack)
    {
        var k = new Kit { roll = Clip("roll", RollLoop(2f)), boom = Clip("boom", Knock(0.9f, new[] { 42f, 66f, 105f }, 5f, 1.4f, 9)) };
        switch (pack)
        {
            case SoundPack.Alley: BuildAlley(k); break;
            case SoundPack.Arcade: BuildArcade(k); break;
            case SoundPack.Cartoon: BuildCartoon(k); break;
            default: BuildNeon(k); break;
        }
        return k;
    }

    static AudioClip Clip(string name, float[] data) => Synth.Clip(name, data, Rate);

    static float[] Buf(float seconds) => new float[(int)(seconds * Rate)];

    static float[] Finish(float[] data, float peak, float room = 0f, float mix = 0f, float tail = 0f)
    {
        if (mix > 0f) data = Synth.WithReverb(data, Rate, room, mix, tail);
        Synth.Normalize(data, peak);
        return data;
    }

    // Real alley: hollow maple pins, a heavy ball, and the crowd cheering your strikes.
    static void BuildAlley(Kit k)
    {
        var clacks = new AudioClip[5];
        for (int i = 0; i < clacks.Length; i++)
        {
            float f = 1f + (i - 2) * 0.06f;
            clacks[i] = Clip("pin", Finish(Pin(0.35f, f, i + 20, 1f), 0.9f, 0.3f, 0.12f, 0.25f));
        }
        k.pinClacks = clacks;
        var hit = Pin(0.5f, 0.85f, 3, 1.6f);
        Synth.Hit(hit, Rate, 0, Drum.Kick, 0.9f, seed: 4);
        k.ballHitPin = Clip("ballhit", Finish(hit, 0.95f, 0.3f, 0.12f, 0.3f));
        k.thud = Clip("thud", Finish(Thump(0.35f, 70f), 0.8f));

        var strike = Applause(2.6f, 70f, 1);
        Mix(strike, Crowd(2.4f, 16, 230f, 420f, 1.25f, Vowel.Eh, Vowel.Ah, 2), 0.9f);
        Chime(strike, 0f, new[] { 76f, 83f, 88f }, 0.12f, 0.45f);
        k.strike = Clip("strike", Finish(strike, 0.85f, 0.5f, 0.2f, 0.6f));

        var spare = Applause(1.8f, 45f, 3);
        Mix(spare, Crowd(1.4f, 9, 220f, 380f, 1.12f, Vowel.Eh, Vowel.Ah, 4), 0.55f);
        Chime(spare, 0f, new[] { 79f, 84f }, 0.12f, 0.45f);
        k.spare = Clip("spare", Finish(spare, 0.8f, 0.5f, 0.2f, 0.5f));

        k.miss = Clip("miss", Finish(Crowd(1.5f, 12, 200f, 330f, 0.72f, Vowel.Ah, Vowel.Oo, 5), 0.6f, 0.5f, 0.2f, 0.4f));
        var over = Applause(2.4f, 40f, 6);
        Chime(over, 0f, new[] { 72f, 76f, 79f, 84f }, 0.16f, 0.4f);
        k.gameOver = Clip("gameover", Finish(over, 0.8f, 0.5f, 0.2f, 0.6f));
        var best = Applause(3.6f, 90f, 7);
        Mix(best, Crowd(3.2f, 20, 230f, 440f, 1.3f, Vowel.Eh, Vowel.Ah, 8), 1f);
        Chime(best, 0f, new[] { 72f, 76f, 79f, 84f, 79f, 84f, 88f }, 0.13f, 0.45f);
        k.newBest = Clip("best", Finish(best, 0.9f, 0.5f, 0.2f, 0.8f));
    }

    // Neon: glassy bells and shimmer, with a long reverb.
    static void BuildNeon(Kit k)
    {
        k.pinClacks = new[]
        {
            Clip("pin", Finish(Knock(0.25f, new[] { 950f, 1480f, 2350f }, 30f, 0.25f, 3), 0.85f, 0.4f, 0.15f, 0.3f)),
            Clip("pin", Finish(Knock(0.25f, new[] { 1050f, 1620f, 2600f }, 32f, 0.25f, 4), 0.85f, 0.4f, 0.15f, 0.3f)),
            Clip("pin", Finish(Knock(0.25f, new[] { 860f, 1350f, 2150f }, 28f, 0.25f, 5), 0.85f, 0.4f, 0.15f, 0.3f)),
        };
        k.ballHitPin = Clip("ballhit", Finish(Knock(0.35f, new[] { 380f, 640f, 1050f }, 18f, 0.5f, 1), 0.9f, 0.4f, 0.15f, 0.3f));
        k.thud = Clip("thud", Finish(Knock(0.3f, new[] { 70f, 110f }, 14f, 0.35f, 2), 0.8f));
        var bell = new Voice(Wave.Bell, 0.004f, 3.5f, 0f, 0.3f);
        var pad = new Voice(Wave.SuperSaw, 0.05f, 1.5f, 0.3f, 0.6f, cutoff: 2500f);
        k.strike = Clip("strike", Finish(Notes(new[] { 72f, 76f, 79f, 84f, 88f, 91f }, 0.07f, 0.9f, bell, pad, 3.2f), 0.75f, 0.85f, 0.45f, 1.4f));
        k.spare = Clip("spare", Finish(Notes(new[] { 76f, 79f, 84f, 88f }, 0.08f, 0.7f, bell, pad, 2.4f), 0.7f, 0.85f, 0.45f, 1.2f));
        var miss = Buf(1.2f);
        Synth.Note(miss, Rate, 0, 0.7f, Synth.Midi(64), 0.5f, new Voice(Wave.Triangle, 0.01f, 1.5f, 0.4f, 0.2f, vibrato: 0.3f), glideTo: Synth.Midi(55));
        k.miss = Clip("miss", Finish(miss, 0.55f, 0.8f, 0.4f, 1f));
        k.gameOver = Clip("gameover", Finish(Notes(new[] { 79f, 76f, 72f, 67f }, 0.16f, 1.2f, bell, pad, 3f), 0.7f, 0.85f, 0.45f, 1.4f));
        k.newBest = Clip("best", Finish(Notes(new[] { 72f, 76f, 79f, 84f, 79f, 84f, 88f, 91f, 96f }, 0.1f, 1.4f, bell, pad, 4f), 0.8f, 0.85f, 0.45f, 1.6f));
    }

    // Arcade: 8-bit square waves and noise, like an old cabinet.
    static void BuildArcade(Kit k)
    {
        var sq = new Voice(Wave.Square, 0.001f, 0f, 1f, 0.02f);
        var pulse = new Voice(Wave.Pulse, 0.001f, 0f, 1f, 0.02f);
        k.pinClacks = new AudioClip[3];
        for (int i = 0; i < 3; i++)
        {
            var c = Buf(0.12f);
            Synth.Note(c, Rate, 0, 0.06f, Synth.Midi(84 + i * 3), 0.35f, sq, glideTo: Synth.Midi(72 + i * 3));
            Synth.Note(c, Rate, 0, 0.05f, 1f, 0.5f, new Voice(Wave.Noise, 0.001f, 40f), seed: i);
            k.pinClacks[i] = Clip("pin", Finish(c, 0.8f));
        }
        var hit = Buf(0.25f);
        Synth.Note(hit, Rate, 0, 0.16f, Synth.Midi(60), 0.6f, sq, glideTo: Synth.Midi(36));
        Synth.Note(hit, Rate, 0, 0.12f, 1f, 0.6f, new Voice(Wave.Noise, 0.001f, 25f), seed: 9);
        k.ballHitPin = Clip("ballhit", Finish(hit, 0.85f));
        var thud = Buf(0.2f);
        Synth.Note(thud, Rate, 0, 0.12f, 1f, 0.8f, new Voice(Wave.Noise, 0.001f, 30f, cutoff: 900f), seed: 3);
        k.thud = Clip("thud", Finish(thud, 0.7f));

        k.strike = Clip("strike", Finish(Tune(new[] { (72f, 1), (76f, 1), (79f, 1), (84f, 1), (0f, 1), (79f, 1), (84f, 6) }, 0.07f, pulse, 0.45f, harmony: -12), 0.75f));
        k.spare = Clip("spare", Finish(Tune(new[] { (83f, 1), (88f, 6) }, 0.07f, sq, 0.45f), 0.7f));
        k.miss = Clip("miss", Finish(Tune(new[] { (67f, 3), (66f, 3), (65f, 3), (64f, 9) }, 0.09f, sq, 0.4f, vibratoLast: true), 0.6f));
        k.gameOver = Clip("gameover", Finish(Tune(new[] { (72f, 2), (67f, 2), (64f, 2), (69f, 3), (71f, 3), (69f, 2), (68f, 4), (67f, 6) }, 0.08f, pulse, 0.45f, harmony: -12), 0.75f));
        k.newBest = Clip("best", Finish(Tune(new[] { (67f, 1), (72f, 1), (76f, 1), (79f, 1), (84f, 1), (88f, 1), (91f, 4), (88f, 2), (91f, 8) }, 0.07f, pulse, 0.45f, harmony: -12), 0.8f));
    }

    // Cartoon: wood blocks, marimba, slide whistles and a "wah-wah" for gutter balls.
    static void BuildCartoon(Kit k)
    {
        var wood = new Voice(Wave.Marimba, 0.001f, 30f, 0f, 0.02f);
        var mar = new Voice(Wave.Marimba, 0.002f, 5f, 0f, 0.2f);
        k.pinClacks = new AudioClip[4];
        for (int i = 0; i < 4; i++)
        {
            var c = Buf(0.2f);
            Synth.Note(c, Rate, 0, 0.12f, Synth.Midi(79 + i * 2), 0.8f, wood);
            Synth.Hit(c, Rate, 0, Drum.Rim, 0.5f, seed: i);
            k.pinClacks[i] = Clip("pin", Finish(c, 0.8f));
        }
        var bonk = Buf(0.4f);
        Synth.Note(bonk, Rate, 0, 0.25f, 420f, 0.9f, new Voice(Wave.Sine, 0.001f, 9f), glideTo: 140f);
        Synth.Hit(bonk, Rate, 0, Drum.Rim, 0.5f, seed: 2);
        k.ballHitPin = Clip("ballhit", Finish(bonk, 0.85f));
        var thud = Buf(0.3f);
        Synth.Hit(thud, Rate, 0, Drum.Tom, 0.9f, seed: 1);
        k.thud = Clip("thud", Finish(thud, 0.75f));

        var whistle = new Voice(Wave.Flute, 0.02f, 0f, 1f, 0.08f, vibrato: 0.15f);
        var strike = Buf(2f);
        for (int i = 0; i < 12; i++) Synth.Note(strike, Rate, i * 0.035, 0.3f, Synth.Midi(72 + new[] { 0, 2, 4, 7, 9 }[i % 5] + 12 * (i / 5)), 0.35f, mar);
        Synth.Note(strike, Rate, 0.45, 0.25f, Synth.Midi(84), 0.5f, mar);
        Synth.Note(strike, Rate, 0.62, 0.9f, Synth.Midi(91), 0.5f, mar);
        Synth.Note(strike, Rate, 0.62, 0.9f, Synth.Midi(84), 0.4f, mar);
        Synth.Hit(strike, Rate, 0.62, Drum.Crash, 0.35f, seed: 3);
        k.strike = Clip("strike", Finish(strike, 0.8f, 0.4f, 0.15f, 0.4f));
        var spare = Buf(1.4f);
        Synth.Note(spare, Rate, 0, 0.5f, 900f, 0.35f, whistle, glideTo: 1800f);
        Synth.Note(spare, Rate, 0.45, 0.6f, Synth.Midi(88), 0.5f, mar);
        Synth.Note(spare, Rate, 0.45, 0.6f, Synth.Midi(84), 0.4f, mar);
        k.spare = Clip("spare", Finish(spare, 0.75f, 0.4f, 0.15f, 0.4f));
        var wah = new Voice(Wave.Saw, 0.02f, 0f, 1f, 0.06f, cutoff: 900f, vibrato: 0.2f);
        var miss = Buf(2f);
        float[] slide = { 58f, 57f, 56f };
        for (int i = 0; i < 3; i++) Synth.Note(miss, Rate, i * 0.32, 0.26f, Synth.Midi(slide[i]), 0.4f, wah);
        Synth.Note(miss, Rate, 0.96, 0.8f, Synth.Midi(55), 0.4f, new Voice(Wave.Saw, 0.02f, 0f, 1f, 0.2f, cutoff: 700f, vibrato: 0.6f), glideTo: Synth.Midi(54));
        k.miss = Clip("miss", Finish(miss, 0.6f));
        var over = Buf(2f);
        float[] tune = { 72f, 76f, 79f, 76f, 72f, 79f, 84f };
        for (int i = 0; i < tune.Length; i++) Synth.Note(over, Rate, i * 0.14, i == tune.Length - 1 ? 0.8f : 0.13f, Synth.Midi(tune[i]), 0.5f, mar);
        k.gameOver = Clip("gameover", Finish(over, 0.75f, 0.4f, 0.15f, 0.4f));
        var best = Buf(3f);
        Synth.Note(best, Rate, 0, 0.6f, 700f, 0.35f, whistle, glideTo: 2100f);
        float[] fan = { 72f, 76f, 79f, 84f, 79f, 84f, 88f };
        for (int i = 0; i < fan.Length; i++) Synth.Note(best, Rate, 0.55 + i * 0.12, i == fan.Length - 1 ? 1.2f : 0.11f, Synth.Midi(fan[i]), 0.5f, mar);
        Synth.Hit(best, Rate, 0.55 + 6 * 0.12, Drum.Crash, 0.4f, seed: 5);
        k.newBest = Clip("best", Finish(best, 0.8f, 0.4f, 0.15f, 0.5f));
    }

    // ---- Building blocks ----

    static void Mix(float[] into, float[] add, float gain)
    {
        for (int i = 0; i < Mathf.Min(into.Length, add.Length); i++) into[i] += add[i] * gain;
    }

    // Scoreboard chime: soft bells, one per step.
    static void Chime(float[] into, float start, float[] notes, float step, float amp)
    {
        var bell = new Voice(Wave.Bell, 0.003f, 3f, 0f, 0.3f);
        for (int i = 0; i < notes.Length; i++)
            Synth.Note(into, Rate, start + i * step, i == notes.Length - 1 ? 0.8f : step, Synth.Midi(notes[i]), amp * (i == notes.Length - 1 ? 1f : 0.8f), bell);
    }

    // A rising run of notes, with the final chord held on a pad.
    static float[] Notes(float[] notes, float step, float hold, Voice lead, Voice pad, float seconds)
    {
        var d = Buf(seconds);
        for (int i = 0; i < notes.Length; i++) Synth.Note(d, Rate, i * step, i == notes.Length - 1 ? hold : step * 2f, Synth.Midi(notes[i]), 0.5f, lead);
        float end = (notes.Length - 1) * step;
        foreach (float n in new[] { notes[^1] - 12f, notes[^1] - 8f, notes[^1] - 5f })
            Synth.Note(d, Rate, end, hold, Synth.Midi(n), 0.12f, pad);
        return d;
    }

    // A chiptune melody: (midi note or 0 for a rest, length in steps).
    static float[] Tune((float note, int steps)[] notes, float step, Voice v, float amp, int harmony = 0, bool vibratoLast = false)
    {
        int total = 0;
        foreach (var n in notes) total += n.steps;
        var d = Buf(total * step + 0.2f);
        float t = 0f;
        for (int i = 0; i < notes.Length; i++)
        {
            var (note, steps) = notes[i];
            if (note > 0f)
            {
                var voice = v;
                if (vibratoLast && i == notes.Length - 1) voice.vibrato = 0.5f;
                Synth.Note(d, Rate, t, steps * step * 0.92f, Synth.Midi(note), amp, voice);
                if (harmony != 0) Synth.Note(d, Rate, t, steps * step * 0.92f, Synth.Midi(note + harmony), amp * 0.5f, new Voice(Wave.Triangle, 0.001f, 0f, 1f, 0.02f));
            }
            t += steps * step;
        }
        return d;
    }

    // A struck bowling pin: a few inharmonic body modes of hollow maple, plus a sharp click.
    static float[] Pin(float seconds, float pitch, int seed, float weight)
    {
        var d = Buf(seconds);
        var rng = new System.Random(seed);
        float[] modes = { 1080f, 2350f, 3850f, 5600f, 420f };
        float[] decays = { 38f, 52f, 70f, 95f, 30f };
        float[] amps = { 1f, 0.6f, 0.35f, 0.18f, 0.5f * weight };
        var bp = new Biquad();
        bp.Bandpass(2600f * pitch, 0.9f, Rate);
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate, s = 0f;
            for (int m = 0; m < modes.Length; m++)
                s += Mathf.Sin(2f * Mathf.PI * modes[m] * pitch * (1f + 0.02f * m * (seed % 3)) * t) * Mathf.Exp(-t * decays[m]) * amps[m];
            s += bp.Process((float)(rng.NextDouble() * 2 - 1)) * 2.5f * Mathf.Exp(-t * 160f);
            d[i] = s;
        }
        return d;
    }

    // Low body hit: a falling sine and a puff of low noise.
    static float[] Thump(float seconds, float freq)
    {
        var d = Buf(seconds);
        var rng = new System.Random(11);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = (float)i / Rate;
            phase += (freq + 60f * Mathf.Exp(-t * 25f)) / Rate;
            lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.05f;
            d[i] = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * 12f) + lp * 1.5f * Mathf.Exp(-t * 30f);
        }
        return d;
    }

    // Applause: lots of short, band-passed noise claps, thick at first and thinning out.
    static float[] Applause(float seconds, float clapsPerSecond, int seed)
    {
        var d = Buf(seconds);
        var rng = new System.Random(seed);
        int clapLen = (int)(0.025f * Rate);
        var bp = new Biquad();
        for (float t = 0.05f; t < seconds - 0.05f;)
        {
            float density = clapsPerSecond * Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-Mathf.Max(0f, t - 0.6f) * 1.4f);
            t += 1f / Mathf.Max(4f, density) * (float)(0.4 + rng.NextDouble() * 1.2);
            int start = (int)(t * Rate);
            bp.Bandpass(900f + (float)rng.NextDouble() * 1800f, 1.4f, Rate);
            float amp = 0.3f + (float)rng.NextDouble() * 0.7f;
            for (int i = 0; i < clapLen && start + i < d.Length; i++)
                d[start + i] += bp.Process((float)(rng.NextDouble() * 2 - 1)) * amp * Mathf.Exp(-i / (float)Rate * 150f);
        }
        return d;
    }

    enum Vowel { Ah, Eh, Oo }

    static (float f1, float f2) Formants(Vowel v) => v switch { Vowel.Eh => (530f, 1840f), Vowel.Oo => (320f, 870f), _ => (730f, 1090f) };

    // A crowd: several voices on a buzzy saw through two vowel formants, sliding in pitch
    // (glide > 1 rises like a cheer, < 1 falls like "aww").
    static float[] Crowd(float seconds, int voices, float lo, float hi, float glide, Vowel from, Vowel to, int seed)
    {
        var d = Buf(seconds);
        var rng = new System.Random(seed);
        var (a1, a2) = Formants(from);
        var (b1, b2) = Formants(to);
        for (int v = 0; v < voices; v++)
        {
            float f0 = lo + (float)rng.NextDouble() * (hi - lo);
            float start = (float)rng.NextDouble() * 0.25f, len = seconds * (0.55f + (float)rng.NextDouble() * 0.4f) - start;
            float amp = 0.5f + (float)rng.NextDouble() * 0.5f, vib = 4f + (float)rng.NextDouble() * 3f;
            var fa = new Biquad();
            var fb = new Biquad();
            int n0 = (int)(start * Rate), n = (int)(len * Rate);
            float phase = (float)rng.NextDouble();
            for (int i = 0; i < n && n0 + i < d.Length; i++)
            {
                float u = (float)i / n, t = (float)i / Rate;
                if (i % 64 == 0)
                {
                    float vu = Mathf.SmoothStep(0f, 1f, u);
                    fa.Bandpass(Mathf.Lerp(a1, b1, vu), 6f, Rate);
                    fb.Bandpass(Mathf.Lerp(a2, b2, vu), 8f, Rate);
                }
                float f = f0 * Mathf.Lerp(1f, glide, Mathf.Sqrt(u)) * (1f + 0.012f * Mathf.Sin(2f * Mathf.PI * vib * t));
                phase += f / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                float env = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((1f - u) / 0.35f);
                d[n0 + i] += (fa.Process(saw) + 0.6f * fb.Process(saw)) * env * amp;
            }
        }
        // Breath and chatter under the voices.
        var nb = new Biquad();
        nb.Bandpass(1000f, 0.7f, Rate);
        for (int i = 0; i < d.Length; i++)
        {
            float u = (float)i / d.Length;
            d[i] += nb.Process((float)(rng.NextDouble() * 2 - 1)) * 0.12f * Mathf.Clamp01(u / 0.08f) * (1f - u);
        }
        return d;
    }

    // Low rumble: brown noise plus a wobbling low tone, crossfaded so the loop has no click.
    static float[] RollLoop(float seconds)
    {
        int n = (int)(seconds * Rate), fade = Rate / 5;
        var raw = new float[n + fade];
        var rng = new System.Random(7);
        float brown = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            brown = brown * 0.995f + (float)(rng.NextDouble() * 2 - 1) * 0.05f;
            float t = (float)i / Rate;
            float hum = Mathf.Sin(2f * Mathf.PI * 55f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 3f * t));
            raw[i] = brown * 1.6f + hum * 0.12f;
        }
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            data[i] = raw[i];
            if (i < fade) data[i] = Mathf.Lerp(raw[n + i], raw[i], (float)i / fade);
        }
        Synth.Normalize(data, 0.5f);
        return data;
    }

    // Struck object: a few decaying partials plus a short noise click for the attack.
    static float[] Knock(float seconds, float[] partials, float decay, float noise, int seed)
    {
        int n = (int)(seconds * Rate);
        var data = new float[n];
        var rng = new System.Random(seed);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate, s = 0f;
            for (int k = 0; k < partials.Length; k++)
                s += Mathf.Sin(2f * Mathf.PI * partials[k] * t) * Mathf.Exp(-t * decay * (1f + k * 0.6f)) / (k + 1);
            s += (float)(rng.NextDouble() * 2 - 1) * noise * Mathf.Exp(-t * 120f);
            data[i] = s;
        }
        Synth.Normalize(data, 0.9f);
        return data;
    }
}
