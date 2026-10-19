using System.Collections.Generic;
using UnityEngine;

public enum VenueParticles { None, Stars, Bubbles, Embers, Snow, Petals, Confetti }

// One bowling alley's look. The scene is built once (neon cosmic); VenueStyler restyles it at
// runtime from one of these: surface colours, neon tube colours, lights, fog and ambient particles.
public class Venue
{
    public string id;
    public Color lane;
    public bool wood;                     // maple boards texture, tinted with the lane colour
    public float laneSmoothness = 0.93f;
    public Color dark, floor;
    public Color fog, ambient;
    public float fogStart = 18f, fogEnd = 45f;
    // Replacements for the scene's four neon colours: cyan, magenta, purple, gold (HDR).
    public Color[] neon;
    // Point light colours: pin left, pin right, lane, bowler; and an intensity multiplier.
    public Color[] lights;
    public float lightScale = 1f;
    public float sunIntensity = 0.15f;
    public VenueParticles particles;
    public Color particleColor = Color.white;

    public string Name => Loc.T("venue_" + id);
    public string Description => Loc.T("venue_" + id + "_desc");
}

public static class Venues
{
    static Color H(float r, float g, float b, float k = 2.5f) => new Color(r * k, g * k, b * k);

    public static readonly Venue[] All =
    {
        new()
        {
            id = "neon", lane = new(0.04f, 0.04f, 0.09f), dark = new(0.015f, 0.015f, 0.03f), floor = new(0.008f, 0.006f, 0.016f),
            fog = new(0.02f, 0.01f, 0.05f), ambient = new(0.035f, 0.03f, 0.07f),
            neon = new[] { new Color(0f, 2.2f, 2.6f), new Color(2.6f, 0.3f, 2.2f), new Color(1.2f, 0.4f, 3f), new Color(3f, 2f, 0.4f) },
            lights = new[] { new Color(1f, 0.2f, 0.85f), new Color(0.2f, 1f, 1f), new Color(0.6f, 0.3f, 1f), new Color(0.4f, 0.9f, 1f) },
            particles = VenueParticles.Stars, particleColor = new(0.7f, 0.6f, 1f),
        },
        new()
        {
            // A normal, bright neighbourhood alley: maple boards, warm lights, carpet.
            id = "classic", wood = true, lane = new(0.86f, 0.66f, 0.42f), laneSmoothness = 0.88f,
            dark = new(0.32f, 0.22f, 0.16f), floor = new(0.2f, 0.06f, 0.08f),
            fog = new(0.42f, 0.36f, 0.3f), ambient = new(0.55f, 0.5f, 0.45f), fogStart = 25f, fogEnd = 70f,
            neon = new[] { H(1f, 0.95f, 0.85f, 1.4f), H(0.9f, 0.15f, 0.12f, 1.4f), H(0.15f, 0.3f, 0.9f, 1.4f), H(1f, 0.8f, 0.3f, 1.4f) },
            lights = new[] { new Color(1f, 0.92f, 0.8f), new Color(1f, 0.92f, 0.8f), new Color(1f, 0.95f, 0.85f), new Color(1f, 0.95f, 0.9f) },
            lightScale = 1.6f, sunIntensity = 0.9f,
        },
        new()
        {
            // 1950s diner: cherry wood, mint and red neon, confetti.
            id = "retro", wood = true, lane = new(0.62f, 0.32f, 0.2f), laneSmoothness = 0.9f,
            dark = new(0.45f, 0.06f, 0.08f), floor = new(0.08f, 0.25f, 0.24f),
            fog = new(0.18f, 0.1f, 0.12f), ambient = new(0.32f, 0.26f, 0.26f), fogStart = 22f, fogEnd = 60f,
            neon = new[] { H(0.3f, 1f, 0.8f), H(1f, 0.15f, 0.2f), H(1f, 0.95f, 0.85f, 1.6f), H(1f, 0.85f, 0.2f) },
            lights = new[] { new Color(1f, 0.4f, 0.4f), new Color(0.4f, 1f, 0.85f), new Color(1f, 0.85f, 0.6f), new Color(1f, 0.9f, 0.75f) },
            lightScale = 1.3f, sunIntensity = 0.45f,
            particles = VenueParticles.Confetti,
        },
        new()
        {
            // Deep-sea aquarium: everything teal and blue, bubbles rising.
            id = "ocean", lane = new(0.02f, 0.1f, 0.16f), dark = new(0.0f, 0.05f, 0.09f), floor = new(0.0f, 0.04f, 0.08f),
            fog = new(0.0f, 0.12f, 0.2f), ambient = new(0.04f, 0.14f, 0.2f), fogStart = 12f, fogEnd = 38f,
            neon = new[] { H(0.1f, 0.9f, 1f), H(0.2f, 0.5f, 1f), H(0.1f, 1f, 0.6f), H(1f, 0.6f, 0.3f) },
            lights = new[] { new Color(0.1f, 0.5f, 1f), new Color(0.1f, 1f, 0.9f), new Color(0.2f, 0.7f, 1f), new Color(0.3f, 0.9f, 1f) },
            lightScale = 1.2f, particles = VenueParticles.Bubbles, particleColor = new(0.6f, 0.95f, 1f),
        },
        new()
        {
            // Volcano: black basalt lane, lava-orange neon, embers drifting up.
            id = "volcano", lane = new(0.06f, 0.03f, 0.03f), dark = new(0.05f, 0.02f, 0.015f), floor = new(0.1f, 0.02f, 0f),
            fog = new(0.18f, 0.04f, 0.01f), ambient = new(0.12f, 0.04f, 0.02f), fogStart = 14f, fogEnd = 40f,
            neon = new[] { H(1f, 0.45f, 0.05f), H(1f, 0.1f, 0.02f), H(1f, 0.75f, 0.1f), H(1f, 0.95f, 0.5f) },
            lights = new[] { new Color(1f, 0.3f, 0.05f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.2f, 0.05f), new Color(1f, 0.6f, 0.3f) },
            lightScale = 1.3f, particles = VenueParticles.Embers, particleColor = new(1f, 0.5f, 0.1f),
        },
        new()
        {
            // Ice palace: pale glassy lane, white-blue light, snow falling.
            id = "ice", lane = new(0.62f, 0.78f, 0.9f), laneSmoothness = 0.98f, dark = new(0.45f, 0.58f, 0.7f), floor = new(0.7f, 0.8f, 0.9f),
            fog = new(0.62f, 0.75f, 0.88f), ambient = new(0.5f, 0.6f, 0.72f), fogStart = 16f, fogEnd = 55f,
            neon = new[] { H(0.5f, 0.9f, 1f), H(0.8f, 0.85f, 1f), H(0.4f, 0.6f, 1f), H(0.9f, 1f, 1f) },
            lights = new[] { new Color(0.7f, 0.85f, 1f), new Color(0.8f, 0.95f, 1f), new Color(0.75f, 0.9f, 1f), new Color(0.9f, 0.95f, 1f) },
            lightScale = 1.4f, sunIntensity = 0.7f, particles = VenueParticles.Snow,
        },
        new()
        {
            // Sakura garden at dusk: light wood, pink neon, petals falling.
            id = "sakura", wood = true, lane = new(0.9f, 0.76f, 0.62f), laneSmoothness = 0.85f,
            dark = new(0.25f, 0.12f, 0.16f), floor = new(0.16f, 0.1f, 0.14f),
            fog = new(0.45f, 0.28f, 0.36f), ambient = new(0.42f, 0.32f, 0.38f), fogStart = 20f, fogEnd = 55f,
            neon = new[] { H(1f, 0.6f, 0.8f), H(1f, 0.35f, 0.6f), H(0.75f, 0.5f, 1f), H(1f, 0.85f, 0.5f) },
            lights = new[] { new Color(1f, 0.6f, 0.75f), new Color(1f, 0.75f, 0.85f), new Color(1f, 0.8f, 0.7f), new Color(1f, 0.85f, 0.85f) },
            lightScale = 1.3f, sunIntensity = 0.5f, particles = VenueParticles.Petals, particleColor = new(1f, 0.7f, 0.82f),
        },
        new()
        {
            // Space station: glossy black lane, white/blue light strips, a starfield all around.
            id = "space", lane = new(0.02f, 0.02f, 0.03f), laneSmoothness = 0.98f, dark = new(0.12f, 0.13f, 0.16f), floor = new(0.0f, 0.0f, 0.01f),
            fog = new(0f, 0f, 0.01f), ambient = new(0.08f, 0.09f, 0.12f), fogStart = 40f, fogEnd = 90f,
            neon = new[] { H(0.85f, 0.95f, 1f, 2f), H(0.2f, 0.5f, 1f), H(1f, 0.3f, 0.3f, 2f), H(0.95f, 0.95f, 1f, 2f) },
            lights = new[] { new Color(0.8f, 0.9f, 1f), new Color(0.8f, 0.9f, 1f), new Color(0.5f, 0.7f, 1f), new Color(0.9f, 0.95f, 1f) },
            lightScale = 1.1f, particles = VenueParticles.Stars, particleColor = Color.white,
        },
    };
}

// Applies a Venue to the built scene. Works on runtime copies of the materials, so the asset files
// never change, and remembers each neon tube's original colour to map it into the venue's palette.
public class VenueStyler
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), BaseMap = Shader.PropertyToID("_BaseMap"),
        Smoothness = Shader.PropertyToID("_Smoothness");
    static readonly Color[] SceneNeon = { new(0f, 2.2f, 2.6f), new(2.6f, 0.3f, 2.2f), new(1.2f, 0.4f, 3f), new(3f, 2f, 0.4f) };
    static readonly string[] LightNames = { "Pin Light Magenta", "Pin Light Cyan", "Lane Light Purple", "Bowler Light" };

    readonly Dictionary<string, Material> copies = new();
    readonly List<(NeonGlow glow, int slot)> tubes = new();
    readonly Light[] lights = new Light[4];
    readonly float[] lightBase = new float[4];
    readonly Light sun;
    readonly Camera cam;
    readonly Material particleMat;
    ParticleSystem particles;
    Texture2D woodTexture;

    public int Current { get; private set; } = -1;

    public VenueStyler(Camera cam, Material particleMaterial, Transform ball)
    {
        this.cam = cam;
        particleMat = particleMaterial;
        foreach (var r in Object.FindObjectsByType<Renderer>())
        {
            var m = r.sharedMaterial;
            if (m == null || r.transform.IsChildOf(ball)) continue;
            string key = m.name.Replace(" (Instance)", "");
            if (key is not ("Lane" or "Dark" or "Floor" or "Gutter" or "Wall")) continue;
            if (!copies.TryGetValue(key, out var copy)) copies[key] = copy = new Material(m) { name = key + " (Venue)" };
            r.sharedMaterial = copy;
        }
        foreach (var g in Object.FindObjectsByType<NeonGlow>())
        {
            if (g.transform.IsChildOf(ball)) continue;
            int best = 0;
            for (int i = 1; i < SceneNeon.Length; i++)
                if (Dist(g.color, SceneNeon[i]) < Dist(g.color, SceneNeon[best])) best = i;
            tubes.Add((g, best));
        }
        foreach (var l in Object.FindObjectsByType<Light>())
        {
            int i = System.Array.IndexOf(LightNames, l.name);
            if (i >= 0) { lights[i] = l; lightBase[i] = l.intensity; }
            else if (l.type == LightType.Directional) sun = l;
        }
    }

    static float Dist(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

    public void Apply(int index)
    {
        index = Mathf.Clamp(index, 0, Venues.All.Length - 1);
        if (index == Current) return;
        Current = index;
        var v = Venues.All[index];

        if (copies.TryGetValue("Lane", out var lane))
        {
            lane.SetColor(BaseColor, v.lane);
            lane.SetTexture(BaseMap, v.wood ? Wood() : null);
            lane.SetFloat(Smoothness, v.laneSmoothness);
        }
        foreach (var key in new[] { "Dark", "Gutter", "Wall" })
            if (copies.TryGetValue(key, out var m)) m.SetColor(BaseColor, v.dark);
        if (copies.TryGetValue("Floor", out var floor)) floor.SetColor(BaseColor, v.floor);

        foreach (var (glow, slot) in tubes) if (glow != null) glow.color = v.neon[slot];
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null) { lights[i].color = v.lights[i]; lights[i].intensity = lightBase[i] * v.lightScale; }
        if (sun != null) sun.intensity = v.sunIntensity;

        RenderSettings.fogColor = v.fog;
        RenderSettings.fogStartDistance = v.fogStart;
        RenderSettings.fogEndDistance = v.fogEnd;
        RenderSettings.ambientLight = v.ambient;
        if (cam != null) cam.backgroundColor = v.fog;

        BuildParticles(v);
    }

    // Lane boards (39 across) with a little grain; greyscale, so the lane colour tints it.
    Texture2D Wood()
    {
        if (woodTexture == null)
        {
            const int w = 390, h = 256;
            woodTexture = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Lane Wood", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var rng = new System.Random(7);
            float[] boardShade = new float[39];
            for (int i = 0; i < 39; i++) boardShade[i] = 0.85f + (float)rng.NextDouble() * 0.15f;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int board = x * 39 / w;
                    float grain = Mathf.PerlinNoise(x * 0.9f + board * 13f, y * 0.04f) * 0.18f;
                    float seam = (x % 10 == 0) ? 0.75f : 1f;
                    float s = boardShade[board] * (0.9f + grain) * seam;
                    px[y * w + x] = new Color(s, s, s, 1f);
                }
            woodTexture.SetPixels(px);
            woodTexture.Apply(true);
        }
        return woodTexture;
    }

    void BuildParticles(Venue v)
    {
        if (particles != null) Object.Destroy(particles.gameObject);
        particles = null;
        if (v.particles == VenueParticles.None || particleMat == null) return;

        var go = new GameObject("Venue Particles");
        go.transform.position = new Vector3(0f, 2f, 10f);
        particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        main.startColor = v.particleColor;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(9f, 4f, 30f);
        var emission = particles.emission;
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        // All three axes must use the same curve mode, so every one is a (min, max) pair.
        void Drift(float x0, float x1, float y0, float y1)
        {
            velocity.x = new ParticleSystem.MinMaxCurve(x0, x1);
            velocity.y = new ParticleSystem.MinMaxCurve(y0, y1);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }
        var noise = particles.noise;

        switch (v.particles)
        {
            case VenueParticles.Stars:
                go.transform.position = new Vector3(0f, 6f, 20f);
                shape.scale = new Vector3(40f, 14f, 30f);
                main.startLifetime = 6f; main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
                main.startSpeed = 0f; emission.rateOverTime = 40f;
                velocity.enabled = false;
                break;
            case VenueParticles.Bubbles:
                go.transform.position = new Vector3(0f, 0.5f, 10f);
                main.startLifetime = 5f; main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
                main.startSpeed = 0f; emission.rateOverTime = 30f;
                Drift(0f, 0f, 0.3f, 0.7f);
                noise.enabled = true; noise.strength = 0.2f; noise.frequency = 0.6f;
                break;
            case VenueParticles.Embers:
                go.transform.position = new Vector3(0f, 0.2f, 12f);
                main.startLifetime = 4f; main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
                main.startSpeed = 0f; emission.rateOverTime = 45f;
                Drift(0f, 0f, 0.4f, 1.1f);
                noise.enabled = true; noise.strength = 0.5f; noise.frequency = 0.8f;
                break;
            case VenueParticles.Snow:
            case VenueParticles.Petals:
                go.transform.position = new Vector3(0f, 4.5f, 10f);
                main.startLifetime = 8f; main.startSize = v.particles == VenueParticles.Snow
                    ? new ParticleSystem.MinMaxCurve(0.03f, 0.07f) : new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                main.startSpeed = 0f; emission.rateOverTime = 35f;
                Drift(-0.1f, 0.25f, -0.7f, -0.35f);
                noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.4f;
                break;
            case VenueParticles.Confetti:
                go.transform.position = new Vector3(0f, 4.5f, 10f);
                main.startLifetime = 7f; main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                main.startSpeed = 0f; emission.rateOverTime = 20f;
                main.startColor = new ParticleSystem.MinMaxGradient(new Gradient
                {
                    mode = GradientMode.Fixed,
                    colorKeys = new[]
                    {
                        new GradientColorKey(new Color(1f, 0.3f, 0.35f), 0.25f), new GradientColorKey(new Color(0.3f, 1f, 0.8f), 0.5f),
                        new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0.75f), new GradientColorKey(new Color(0.5f, 0.6f, 1f), 1f),
                    },
                }) { mode = ParticleSystemGradientMode.RandomColor };
                Drift(0f, 0f, -0.6f, -0.3f);
                noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.5f;
                break;
        }
        var colorLife = particles.colorOverLifetime;
        colorLife.enabled = true;
        colorLife.color = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) },
        };
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = particleMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particles.Play();
        particles.Simulate(6f, true, false); // start already full instead of filling up
        particles.Play();
    }
}
