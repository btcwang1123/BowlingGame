using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Background music: plays the style picked in Settings (or the one that suits the current venue),
// rendered by MusicSynth a few notes per frame so switching never stalls the game, then cross-faded in.
public class BowlingMusic : MonoBehaviour
{
    public static BowlingMusic Instance { get; private set; }

    // Set while the ball rolls so the pins can be heard over the music.
    public static bool Duck;

    const float FrameBudgetMs = 5f;
    const float FadeSeconds = 1.2f;
    const float BaseVolume = 0.5f;

    AudioSource a, b, current;
    readonly Dictionary<string, AudioClip> cache = new();
    string wanted, building;
    Coroutine job;
    float duckLevel = 1f;

    void Awake()
    {
        Instance = this;
        a = MakeSource();
        b = MakeSource();
        current = a;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    AudioSource MakeSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.volume = 0f;
        return s;
    }

    void Start() => Refresh();

    // The style that should be playing now, or null for silence.
    public static MusicStyle WantedStyle()
    {
        int track = GameSettings.MusicTrack;
        if (track == GameSettings.MusicOff) return null;
        if (track == GameSettings.MusicAuto) return MusicSynth.ForVenue(Venues.All[GameSettings.Venue].id);
        return MusicSynth.Styles[Mathf.Clamp(track - 2, 0, MusicSynth.Styles.Length - 1)];
    }

    // Call after the music setting or the venue changes.
    public void Refresh()
    {
        if (Application.isBatchMode) return;
        var style = WantedStyle();
        wanted = style?.id;
        if (style == null || (current.clip != null && current.clip.name == style.id && current.isPlaying)) return;
        if (cache.TryGetValue(style.id, out var clip)) { CrossFade(clip); return; }
        if (building == style.id) return;
        if (job != null) { StopCoroutine(job); building = null; }
        job = StartCoroutine(Build(style));
    }

    void CrossFade(AudioClip clip)
    {
        var next = current == a ? b : a;
        next.clip = clip;
        next.volume = 0f;
        next.Play();
        current = next;
    }

    void Update()
    {
        duckLevel = Mathf.MoveTowards(duckLevel, Duck ? 0.55f : 1f, Time.unscaledDeltaTime * 1.5f);
        float target = BaseVolume * GameSettings.MusicVolume * duckLevel;
        float stepVol = Time.unscaledDeltaTime / FadeSeconds;
        foreach (var s in new[] { a, b })
        {
            bool on = s == current && wanted != null && s.clip != null && s.clip.name == wanted;
            s.volume = on ? Mathf.MoveTowards(s.volume, target, stepVol * Mathf.Max(target, 0.05f)) : Mathf.MoveTowards(s.volume, 0f, stepVol);
            if (!on && s.isPlaying && s.volume <= 0f) s.Stop();
        }
    }

    IEnumerator Build(MusicStyle style)
    {
        building = style.id;
        var events = MusicSynth.Compose(style);
        var buf = new float[MusicSynth.LengthSamples(style)];
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int rate = MusicSynth.Rate;

        foreach (var e in events)
        {
            if (e.part < 0) Synth.Hit(buf, rate, e.time, e.drum, e.amp, wrap: true);
            else Synth.Note(buf, rate, e.time, e.dur, e.freq, e.amp, MusicSynth.PartVoice(style, e.part), wrap: true, seed: e.part);
            if (watch.Elapsed.TotalMilliseconds > FrameBudgetMs) { yield return null; watch.Restart(); }
        }

        // Room reverb, run over the loop twice so the tail at the end carries into the start.
        var rev = new Reverb(rate, 0.6f, 0.45f);
        var dry = (float[])buf.Clone();
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < buf.Length; i++)
            {
                float wet = rev.Process(dry[i]);
                if (pass == 1) buf[i] = dry[i] + wet * 0.35f;
                if ((i & 4095) == 0 && watch.Elapsed.TotalMilliseconds > FrameBudgetMs) { yield return null; watch.Restart(); }
            }
        Synth.Normalize(buf, 0.9f);
        Synth.SoftClip(buf, 1.4f);
        Synth.Normalize(buf, 0.85f);

        var clip = Synth.Clip(style.id, buf, rate);
        cache[style.id] = clip;
        building = null;
        job = null;
        if (wanted == style.id) CrossFade(clip);
        else Refresh();
    }
}
