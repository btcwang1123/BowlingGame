using System.Collections.Generic;
using UnityEngine;

// One background-music style: tempo, chords, drum and bass patterns, and the instruments.
// Patterns are 16 steps per bar: 'x' = hit, 'o' = soft hit, '.' = rest.
public class MusicStyle
{
    public string id;
    public float bpm = 100f, swing;        // swing: how late the off-beat 8ths land (0–0.6 of a step)
    public int key;                         // pitch class of the tonic (0 = C)
    public string[] chords;                 // one per bar, e.g. "Am7", "F#m7", "Bbmaj7", "E5"

    public string kick = "", snare = "", clap = "", hat = "", openHat = "", ride = "", brush = "", shaker = "", rim = "", taiko = "";
    public bool crash, fills, crackle;

    // Bass: R root, 3 third, 5 fifth, 6 sixth, 7 seventh, 8 octave, a = approach note into the next chord.
    public string[] bass;
    public Voice bassVoice;
    public int bassLen;                     // steps; 0 = until the next note

    public string comp;                     // chord hits
    public Voice compVoice;
    public int compLen, compLo = 52;

    public bool pad;
    public Voice padVoice;

    public string arp;                      // digits index the chord tones, low to high
    public Voice arpVoice;
    public int arpLo = 62;

    public Voice leadVoice;
    public int[] scale = { 0, 2, 4, 7, 9 };
    public int leadLo = 67;
    public string[] leadRhythms;

    public float drumAmp = 0.8f, bassAmp = 0.7f, compAmp = 0.3f, padAmp = 0.16f, arpAmp = 0.18f, leadAmp = 0.32f;

    public string Name => Loc.T("music_" + id);
}

// Composes a 16-bar loop for a style (8 bars of groove, then 8 with a generated melody) and renders it.
public static class MusicSynth
{
    public const int Rate = 22050;
    public const int Bars = 16;

    static Voice V(Wave w, float a = 0.005f, float d = 3f, float s = 0f, float r = 0.1f, float cutoff = 0f, float vib = 0f, float drive = 0f) =>
        new(w, a, d, s, r, cutoff, vib, drive);

    public static readonly MusicStyle[] Styles =
    {
        new()
        {
            id = "lounge", bpm = 104f, swing = 0.55f, key = 0,
            chords = new[] { "Dm7", "G7", "Cmaj7", "A7", "Dm7", "G7", "Em7", "A7" },
            ride = "x...x.x.x...x.x.", brush = "....o.......o...", kick = "o.........o.....",
            bass = new[] { "R...3...5...a..." }, bassVoice = V(Wave.Pluck, r: 0.05f, cutoff: 900f), bassAmp = 0.9f,
            comp = "...x......x.....", compLen = 3, compVoice = V(Wave.EPiano, 0.004f, 2f, 0.25f, 0.25f, 3000f), compAmp = 0.32f,
            leadVoice = V(Wave.Bell, 0.004f, 2.2f, 0.1f, 0.4f, vib: 0.08f), leadAmp = 0.3f, leadLo = 69,
            scale = new[] { 0, 2, 4, 7, 9 },
            leadRhythms = new[] { "x..x..x...x.....", "x.....x.x...x...", "..x.x...x..x....", "x...x...x.x.x..." },
        },
        new()
        {
            id = "disco", bpm = 118f, key = 9,
            chords = new[] { "Am7", "D9", "Am7", "D9", "Fmaj7", "Em7", "Dm7", "E7" },
            kick = "x...x...x...x...", clap = "....x.......x...", hat = "o.o.o.o.o.o.o.o.", openHat = "..x...x...x...x.", fills = true, crash = true,
            bass = new[] { "R.8.R.8.R.8.R.85", "R.8.R.8.R.8.5.8a" }, bassVoice = V(Wave.Saw, 0.003f, 6f, 0.35f, 0.05f, 700f), bassLen = 2,
            comp = "..x...x...x...x.", compLen = 1, compVoice = V(Wave.Pluck, r: 0.05f, cutoff: 3500f), compAmp = 0.28f, compLo = 57,
            pad = true, padVoice = V(Wave.SuperSaw, 0.25f, 1f, 0.75f, 0.5f, 2500f), padAmp = 0.12f,
            leadVoice = V(Wave.Square, 0.01f, 2f, 0.6f, 0.08f, 2800f, 0.18f), leadAmp = 0.22f, leadLo = 69,
            scale = new[] { 0, 3, 5, 7, 10 },
            leadRhythms = new[] { "x.x...x.x.x.....", "..x.x.x...x.x...", "x...x.x.x.......", "x.x.x.x...x...x." },
        },
        new()
        {
            id = "synthwave", bpm = 96f, key = 9,
            chords = new[] { "Am", "F", "C", "G", "Am", "F", "Dm", "E" },
            kick = "x...x...x...x...", snare = "....x.......x...", hat = "o.o.o.o.o.o.o.o.", crash = true, fills = true,
            bass = new[] { "R.R.R.R.R.R.R.R." }, bassVoice = V(Wave.Saw, 0.003f, 4f, 0.5f, 0.05f, 450f), bassLen = 2,
            arp = "0123012301230123", arpVoice = V(Wave.Saw, 0.002f, 12f, 0f, 0.06f, 2400f), arpLo = 64,
            pad = true, padVoice = V(Wave.SuperSaw, 0.5f, 0.6f, 0.8f, 0.9f, 1800f), padAmp = 0.15f,
            leadVoice = V(Wave.Square, 0.02f, 1.5f, 0.7f, 0.15f, 2600f, 0.25f), leadAmp = 0.22f, leadLo = 69,
            scale = new[] { 0, 3, 5, 7, 10 },
            leadRhythms = new[] { "x.......x...x...", "x...x...x.......", "x.x.x...x...x...", "x...........x..." },
        },
        new()
        {
            id = "rockabilly", bpm = 168f, swing = 0.4f, key = 0,
            chords = new[] { "C6", "C6", "F6", "C6", "G7", "F6", "C6", "G7" },
            kick = "x.......x.......", snare = "....x.......x...", hat = "x.x.x.x.x.x.x.x.", fills = true,
            bass = new[] { "R...3...5...6...", "8...6...5...3..." }, bassVoice = V(Wave.Pluck, r: 0.05f, cutoff: 1500f), bassAmp = 0.9f,
            comp = "..x...x...x...x.", compLen = 1, compVoice = V(Wave.Pluck, r: 0.04f, cutoff: 2500f), compAmp = 0.3f,
            leadVoice = V(Wave.Pluck, r: 0.1f), leadAmp = 0.42f, leadLo = 67,
            scale = new[] { 0, 2, 3, 4, 7, 9 },
            leadRhythms = new[] { "x.x.x.x.x...x...", "x...x.x.x.x.....", "x.x...x.x.x.x...", "x.x.x.x.x.x.x..." },
        },
        new()
        {
            id = "aquatic", bpm = 76f, key = 2,
            chords = new[] { "Dmaj7", "Bm7", "Gmaj7", "A6", "Dmaj7", "F#m7", "Gmaj7", "A6" },
            shaker = "..o...o...o...o.",
            bass = new[] { "R.......5......." }, bassVoice = V(Wave.Sine, 0.02f, 0.5f, 0.6f, 0.4f), bassAmp = 0.6f,
            pad = true, padVoice = V(Wave.SuperSaw, 0.9f, 0.5f, 0.85f, 1.5f, 1100f), padAmp = 0.16f,
            arp = "0.1.2.4.3.1.2.0.", arpVoice = V(Wave.Marimba, 0.002f, 4f, 0f, 0.3f), arpAmp = 0.22f, arpLo = 62,
            leadVoice = V(Wave.Bell, 0.004f, 1.5f, 0.1f, 0.6f, vib: 0.1f), leadAmp = 0.22f, leadLo = 74,
            scale = new[] { 0, 2, 4, 7, 9 },
            leadRhythms = new[] { "x.......x.......", "x...........x...", "x.....x.........", "....x.......x..." },
        },
        new()
        {
            id = "rock", bpm = 138f, key = 4,
            chords = new[] { "E5", "E5", "C5", "D5", "E5", "G5", "A5", "B5" },
            kick = "x.....x.x.....x.", snare = "....x.......x...", hat = "x.x.x.x.x.x.x.x.", crash = true, fills = true,
            bass = new[] { "R.R.R.R.R.R.R.R." }, bassVoice = V(Wave.Saw, 0.003f, 5f, 0.5f, 0.04f, 600f, drive: 1f), bassLen = 2,
            comp = "x.x.x.x.x.x.x.x.", compLen = 2, compVoice = V(Wave.SuperSaw, 0.003f, 7f, 0.35f, 0.05f, 2200f, drive: 3f), compAmp = 0.22f, compLo = 40,
            leadVoice = V(Wave.Square, 0.01f, 1f, 0.75f, 0.1f, 3200f, 0.3f, 2f), leadAmp = 0.2f, leadLo = 64,
            scale = new[] { 0, 3, 5, 7, 10 },
            leadRhythms = new[] { "x.x.x...x.x.x...", "x...x.x.x.x.x.x.", "x.x.x.x.x.......", "x.......x.x.x.x." },
        },
        new()
        {
            id = "lofi", bpm = 76f, swing = 0.35f, key = 0, crackle = true,
            chords = new[] { "Fmaj7", "Em7", "Dm7", "Cmaj7", "Fmaj7", "Em7", "Am7", "G7" },
            kick = "x......x..x.....", snare = "....o.......o...", hat = "o.o.o.o.o.o.o.o.", drumAmp = 0.65f,
            bass = new[] { "R.........5....." }, bassVoice = V(Wave.Triangle, 0.01f, 1.5f, 0.5f, 0.2f, 400f), bassAmp = 0.8f,
            comp = "x.........x.....", compLen = 6, compVoice = V(Wave.EPiano, 0.004f, 1.2f, 0.3f, 0.5f, 2200f, 0.05f), compAmp = 0.3f,
            leadVoice = V(Wave.EPiano, 0.004f, 1.8f, 0.2f, 0.4f, 2600f), leadAmp = 0.26f, leadLo = 72,
            scale = new[] { 0, 2, 4, 7, 9 },
            leadRhythms = new[] { "x.....x...x.....", "..x.....x.......", "x...x.......x...", "x.......x.x....." },
        },
        new()
        {
            id = "zen", bpm = 80f, key = 2,
            chords = new[] { "Dm7", "Bbmaj7", "C", "Am7", "Dm7", "Gm7", "Bbmaj7", "C" },
            taiko = "x.......x.o.....", shaker = "..o...o...o...o.", rim = "....o.......o...", drumAmp = 0.6f,
            bass = new[] { "R.......R......." }, bassVoice = V(Wave.Sine, 0.02f, 0.8f, 0.5f, 0.4f), bassAmp = 0.55f,
            arp = "0.2.1.3.2.4.1.3.", arpVoice = V(Wave.Pluck, r: 0.2f, cutoff: 3000f), arpAmp = 0.3f, arpLo = 62,
            pad = true, padVoice = V(Wave.SuperSaw, 1f, 0.4f, 0.7f, 1.5f, 800f), padAmp = 0.1f,
            leadVoice = V(Wave.Flute, 0.06f, 0.6f, 0.85f, 0.25f, vib: 0.25f), leadAmp = 0.3f, leadLo = 69,
            scale = new[] { 0, 3, 5, 7, 10 },
            leadRhythms = new[] { "x.......x...x...", "x...........x...", "x.....x.x.......", "x.......x......." },
        },
        new()
        {
            id = "cosmic", bpm = 88f, key = 4,
            chords = new[] { "Em9", "Cmaj7", "Am9", "Bm7", "Em9", "Cmaj7", "Gmaj7", "D6" },
            kick = "x.........x.....", snare = "........x.......", hat = "..o...o...o...o.", drumAmp = 0.7f,
            bass = new[] { "R..............." }, bassVoice = V(Wave.Saw, 0.05f, 0.5f, 0.8f, 0.6f, 300f), bassAmp = 0.75f,
            pad = true, padVoice = V(Wave.SuperSaw, 1.2f, 0.4f, 0.9f, 2f, 1500f), padAmp = 0.17f,
            arp = "0.1.2.3.4.3.2.1.", arpVoice = V(Wave.Bell, 0.003f, 3f, 0f, 0.4f, 4000f), arpAmp = 0.15f, arpLo = 67,
            leadVoice = V(Wave.Sine, 0.08f, 0.5f, 0.85f, 0.4f, vib: 0.2f), leadAmp = 0.3f, leadLo = 71,
            scale = new[] { 0, 3, 5, 7, 10 },
            leadRhythms = new[] { "x.......x.......", "x...........x...", "x.......x...x...", "x..............." },
        },
    };

    // Which style suits each venue (Venues.All ids).
    public static MusicStyle ForVenue(string venueId) => Find(venueId switch
    {
        "neon" => "synthwave",
        "classic" => "disco",
        "retro" => "rockabilly",
        "ocean" => "aquatic",
        "volcano" => "rock",
        "ice" => "lofi",
        "sakura" => "zen",
        "space" => "cosmic",
        _ => "lounge",
    });

    public static MusicStyle Find(string id)
    {
        foreach (var s in Styles) if (s.id == id) return s;
        return Styles[0];
    }

    // ---- Composition ----

    public struct Event
    {
        public double time;
        public float dur, freq, amp;
        public int part;        // 0 bass, 1 comp, 2 pad, 3 arp, 4 lead, -1 drum
        public Drum drum;
    }

    public static float StepSeconds(MusicStyle s) => 60f / s.bpm / 4f;
    public static int LengthSamples(MusicStyle s) => Mathf.RoundToInt(Bars * 16 * StepSeconds(s) * Rate);

    public static Voice PartVoice(MusicStyle s, int part) => part switch
    {
        0 => s.bassVoice, 1 => s.compVoice, 2 => s.padVoice, 3 => s.arpVoice, _ => s.leadVoice,
    };

    static double StepTime(MusicStyle s, int bar, int step)
    {
        double t = (bar * 16 + step) * StepSeconds(s);
        if (step % 4 == 2) t += s.swing * StepSeconds(s);
        else if (step % 2 == 1) t += s.swing * 0.5 * StepSeconds(s);
        return t;
    }

    static readonly Dictionary<char, int> Notes = new() { ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11 };

    // "F#m7" → root 6, intervals {0, 3, 7, 10}.
    public static int[] ParseChord(string sym, out int root)
    {
        root = Notes[sym[0]];
        int i = 1;
        if (i < sym.Length && sym[i] == '#') { root++; i++; }
        else if (i < sym.Length && sym[i] == 'b') { root--; i++; }
        root = (root + 12) % 12;
        return sym.Substring(i) switch
        {
            "m" => new[] { 0, 3, 7 },
            "m7" => new[] { 0, 3, 7, 10 },
            "m9" => new[] { 0, 3, 7, 10, 14 },
            "7" => new[] { 0, 4, 7, 10 },
            "9" => new[] { 0, 4, 7, 10, 14 },
            "maj7" => new[] { 0, 4, 7, 11 },
            "6" => new[] { 0, 4, 7, 9 },
            "5" => new[] { 0, 7, 12 },
            "sus4" => new[] { 0, 5, 7 },
            _ => new[] { 0, 4, 7 },
        };
    }

    // Chord tones folded into [lo, lo + 20).
    static List<int> Voicing(int root, int[] chord, int lo)
    {
        var notes = new List<int>();
        foreach (int iv in chord)
        {
            int n = lo - lo % 12 + root + iv;
            while (n < lo) n += 12;
            while (n >= lo + 20) n -= 12;
            if (!notes.Contains(n)) notes.Add(n);
        }
        notes.Sort();
        return notes;
    }

    public static List<Event> Compose(MusicStyle s, int seed = 1)
    {
        var ev = new List<Event>();
        var rng = new System.Random(seed + s.id.GetHashCode());
        float step = StepSeconds(s);
        int chordCount = s.chords.Length;

        void Drums(string pattern, Drum drum, int bar, float amp)
        {
            if (string.IsNullOrEmpty(pattern)) return;
            for (int k = 0; k < 16 && k < pattern.Length; k++)
                if (pattern[k] != '.')
                    ev.Add(new Event { time = StepTime(s, bar, k), part = -1, drum = drum, amp = amp * (pattern[k] == 'o' ? 0.5f : 1f) * s.drumAmp });
        }

        for (int bar = 0; bar < Bars; bar++)
        {
            var chord = ParseChord(s.chords[bar % chordCount], out int root);
            ParseChord(s.chords[(bar + 1) % chordCount], out int nextRoot);
            bool fillBar = s.fills && bar % 8 == 7;

            // Drums.
            Drums(s.kick, Drum.Kick, bar, 1f);
            string snare = s.snare;
            if (fillBar) snare = snare.Length == 16 ? snare.Substring(0, 12) + "xoxx" : "............xoxx";
            Drums(snare, Drum.Snare, bar, 0.75f);
            Drums(s.clap, Drum.Clap, bar, 0.6f);
            Drums(s.hat, Drum.HatClosed, bar, 0.28f);
            Drums(s.openHat, Drum.HatOpen, bar, 0.22f);
            Drums(s.ride, Drum.Ride, bar, 0.3f);
            Drums(s.brush, Drum.Brush, bar, 0.5f);
            Drums(s.shaker, Drum.Shaker, bar, 0.25f);
            Drums(s.rim, Drum.Rim, bar, 0.3f);
            Drums(s.taiko, Drum.Taiko, bar, 0.8f);
            if (s.crash && bar % 8 == 0) ev.Add(new Event { time = StepTime(s, bar, 0), part = -1, drum = Drum.Crash, amp = 0.3f * s.drumAmp });
            if (s.crackle)
                for (int k = 0; k < 6; k++)
                    ev.Add(new Event { time = (bar * 16 + rng.NextDouble() * 16) * step, part = -1, drum = Drum.Crackle, amp = 0.05f + (float)rng.NextDouble() * 0.08f });

            // Bass.
            if (s.bass != null)
            {
                string pat = s.bass[bar % s.bass.Length];
                for (int k = 0; k < 16; k++)
                {
                    char c = pat[k];
                    if (c == '.' || c == '-') continue;
                    int iv = c switch
                    {
                        '3' => chord.Length > 1 ? chord[1] : 4,
                        '5' => 7,
                        '6' => 9,
                        '7' => chord.Length > 3 ? chord[3] : 10,
                        '8' => 12,
                        'a' => ((nextRoot - root + 12) % 12) - 1,
                        _ => 0,
                    };
                    int len = s.bassLen;
                    if (len == 0) { len = 1; while (k + len < 16 && (pat[k + len] == '.' || pat[k + len] == '-')) len++; }
                    int note = 36 + root;
                    if (note > 43) note -= 12;
                    ev.Add(new Event { time = StepTime(s, bar, k), dur = len * step * 0.92f, freq = Synth.Midi(note + iv), amp = s.bassAmp * 0.5f, part = 0 });
                }
            }

            // Chord hits.
            if (!string.IsNullOrEmpty(s.comp))
            {
                var voicing = Voicing(root, chord, s.compLo);
                for (int k = 0; k < 16; k++)
                {
                    if (s.comp[k] == '.') continue;
                    int len = s.compLen;
                    if (len == 0) { len = 1; while (k + len < 16 && s.comp[k + len] == '.') len++; }
                    for (int n = 0; n < voicing.Count; n++)
                        ev.Add(new Event { time = StepTime(s, bar, k) + n * 0.006, dur = len * step * 0.9f, freq = Synth.Midi(voicing[n]), amp = s.compAmp / Mathf.Sqrt(voicing.Count), part = 1 });
                }
            }

            // Pad: the whole bar.
            if (s.pad)
                foreach (int n in Voicing(root, chord, 50))
                    ev.Add(new Event { time = bar * 16 * step, dur = 16 * step, freq = Synth.Midi(n), amp = s.padAmp, part = 2 });

            // Arpeggio.
            if (!string.IsNullOrEmpty(s.arp))
            {
                var tones = Voicing(root, chord, s.arpLo);
                foreach (int n in Voicing(root, chord, s.arpLo + 12)) if (!tones.Contains(n)) tones.Add(n);
                tones.Sort();
                for (int k = 0; k < 16; k++)
                {
                    char c = s.arp[k];
                    if (c == '.') continue;
                    int idx = Mathf.Min(c - '0', tones.Count - 1);
                    ev.Add(new Event { time = StepTime(s, bar, k), dur = step * 1.8f, freq = Synth.Midi(tones[idx]), amp = s.arpAmp, part = 3 });
                }
            }
        }

        if (s.leadRhythms != null) ComposeMelody(s, ev, rng);
        return ev;
    }

    // The melody over bars 8–15: a 4-bar phrase, then an answer that reuses its rhythm and ends on the tonic.
    static void ComposeMelody(MusicStyle s, List<Event> ev, System.Random rng)
    {
        float step = StepSeconds(s);
        var scaleNotes = new List<int>();
        for (int n = s.leadLo; n < s.leadLo + 20; n++)
            if (System.Array.IndexOf(s.scale, ((n - s.key) % 12 + 12) % 12) >= 0) scaleNotes.Add(n);
        if (scaleNotes.Count == 0) return;

        var rhythms = new string[4];
        for (int i = 0; i < 4; i++) rhythms[i] = s.leadRhythms[rng.Next(s.leadRhythms.Length)];
        int idxNote = scaleNotes.Count / 2;

        for (int bar = 8; bar < 16; bar++)
        {
            int phraseBar = (bar - 8) % 4;
            bool answer = bar >= 12;
            string rhythm = rhythms[phraseBar];
            if (bar == 15) rhythm = "x...x...x.......";
            var chord = ParseChord(s.chords[bar % s.chords.Length], out int root);
            var onsets = new List<int>();
            for (int k = 0; k < 16; k++) if (rhythm[k] == 'x') onsets.Add(k);
            for (int o = 0; o < onsets.Count; o++)
            {
                int k = onsets[o];
                bool last = bar == 15 && o == onsets.Count - 1;
                // Mostly stepwise, the odd leap; strong beats and the end land on chord tones.
                int move = rng.Next(100) < 70 ? (rng.Next(2) == 0 ? -1 : 1) : rng.Next(-3, 4);
                if (answer && rng.Next(100) < 40) move = -Mathf.Abs(move); // answers tend to fall
                idxNote = Mathf.Clamp(idxNote + move, 0, scaleNotes.Count - 1);
                int note = scaleNotes[idxNote];
                if (k % 8 == 0 || last) note = NearestChordTone(note, root, chord, last ? s.key : -1);
                idxNote = ClosestIndex(scaleNotes, note);
                int len = (o + 1 < onsets.Count ? onsets[o + 1] : 16) - k;
                if (last) len = 8;
                ev.Add(new Event { time = StepTime(s, bar, k), dur = len * step * 0.88f, freq = Synth.Midi(note), amp = s.leadAmp, part = 4 });
            }
        }
    }

    // Nearest note whose pitch class is in the chord (or exactly the tonic when one is given).
    static int NearestChordTone(int note, int root, int[] chord, int tonic)
    {
        for (int d = 0; d < 12; d++)
            foreach (int cand in new[] { note - d, note + d })
            {
                int pc = ((cand % 12) + 12) % 12;
                if (tonic >= 0) { if (pc == tonic) return cand; continue; }
                foreach (int iv in chord) if ((root + iv) % 12 == pc) return cand;
            }
        return note;
    }

    static int ClosestIndex(List<int> list, int note)
    {
        int best = 0;
        for (int i = 1; i < list.Count; i++) if (Mathf.Abs(list[i] - note) < Mathf.Abs(list[best] - note)) best = i;
        return best;
    }
}
