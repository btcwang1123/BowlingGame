using UnityEngine;

// All game sounds, synthesized at startup so the project needs no audio files yet.
// Swap any clip for a recorded one later by assigning it in the Inspector.
public class BowlingAudio : MonoBehaviour
{
    public static BowlingAudio Instance { get; private set; }

    const int Rate = 44100;

    [SerializeField] AudioClip roll, ballHitPin, thud, strike, spare, miss, gameOver, newBest, boom;
    [SerializeField] AudioClip[] pinClacks;
    [SerializeField, Range(0f, 1f)] float volume = 0.8f;

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
        // (Unity objects need == null, not ??=, to detect "not assigned".)
        if (roll == null) roll = Synth.RollLoop(2f);
        if (ballHitPin == null) ballHitPin = Synth.Knock(0.35f, new[] { 380f, 640f, 1050f }, 18f, 0.5f, seed: 1);
        if (thud == null) thud = Synth.Knock(0.3f, new[] { 70f, 110f }, 14f, 0.35f, seed: 2);
        if (pinClacks == null || pinClacks.Length == 0)
            pinClacks = new[]
            {
                Synth.Knock(0.25f, new[] { 950f, 1480f, 2350f }, 30f, 0.25f, seed: 3),
                Synth.Knock(0.25f, new[] { 1050f, 1620f, 2600f }, 32f, 0.25f, seed: 4),
                Synth.Knock(0.25f, new[] { 860f, 1350f, 2150f }, 28f, 0.25f, seed: 5),
            };
        if (strike == null) strike = Synth.Arpeggio(new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.09f, 0.5f);
        if (spare == null) spare = Synth.Arpeggio(new[] { 659f, 784f, 1047f }, 0.1f, 0.4f);
        if (miss == null) miss = Synth.Slide(320f, 140f, 0.6f);
        if (gameOver == null) gameOver = Synth.Arpeggio(new[] { 784f, 659f, 523f }, 0.16f, 0.5f);
        if (boom == null) boom = Synth.Knock(0.9f, new[] { 42f, 66f, 105f }, 5f, 1.4f, seed: 9);
        if (newBest == null) newBest = Synth.Arpeggio(new[] { 523f, 659f, 784f, 1047f, 784f, 1047f, 1319f }, 0.11f, 0.6f);

        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        rollLoop = gameObject.AddComponent<AudioSource>();
        rollLoop.clip = roll;
        rollLoop.loop = true;
        rollLoop.volume = 0f;
        rollLoop.Play();

        ball = FindAnyObjectByType<BowlingBall>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

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
    }

    // impact = relative speed of the collision in m/s.
    public void PinHit(float impact)
    {
        if (Time.time - windowStart > ClackWindow) { windowStart = Time.time; clacksInWindow = 0; }
        if (++clacksInWindow > MaxClacksPerWindow) return;
        Play(pinClacks[Random.Range(0, pinClacks.Length)], Mathf.Clamp01(impact / 4f));
    }

    public void BallHitPin(float impact) => Play(ballHitPin, Mathf.Clamp01(impact / 6f));
    public void Thud(float impact) => Play(thud, Mathf.Clamp01(impact / 5f));
    public void Strike() => Play(strike, 0.8f);
    public void Spare() => Play(spare, 0.8f);
    public void Miss() => Play(miss, 0.7f);
    public void Boom() => Play(boom, 1f);
    public void GameOver(bool best) => Play(best ? newBest : gameOver, 0.8f);

    void Play(AudioClip clip, float loudness)
    {
        if (clip != null && loudness > 0.01f) oneShots.PlayOneShot(clip, loudness * volume);
    }

    // Tiny procedural sound kit.
    static class Synth
    {
        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Low rumble: brown noise plus a wobbling low tone, crossfaded so the loop has no click.
        public static AudioClip RollLoop(float seconds)
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
            Normalize(data, 0.5f);
            return Make("roll", data);
        }

        // Struck object: a few decaying partials plus a short noise click for the attack.
        public static AudioClip Knock(float seconds, float[] partials, float decay, float noise, int seed)
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
            Normalize(data, 0.9f);
            return Make("knock", data);
        }

        // Rising (or falling) notes, each a soft bell tone.
        public static AudioClip Arpeggio(float[] notes, float step, float tail)
        {
            int n = (int)((notes.Length * step + tail) * Rate);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * step * Rate);
                for (int i = start; i < n; i++)
                {
                    float t = (float)(i - start) / Rate;
                    float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 5f);
                    float f = notes[k];
                    data[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * f * t)) * env;
                }
            }
            Normalize(data, 0.7f);
            return Make("jingle", data);
        }

        // "Womp womp": a falling tone.
        public static AudioClip Slide(float from, float to, float seconds)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, u) / Rate;
                float env = Mathf.Min(1f, u * 40f) * (1f - u);
                data[i] = (Mathf.Sin(phase) + 0.4f * Mathf.Sin(2f * phase)) * env;
            }
            Normalize(data, 0.6f);
            return Make("slide", data);
        }

        static void Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }
    }
}
