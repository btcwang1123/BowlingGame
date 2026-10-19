using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BallPattern
{
    Solid, Swirl, Stripes, Galaxy, Flame, Checker, TwoTone, Dots,
    Rainbow, Pixel, Zebra, Camo, Hearts, Sparkle, Lightning, Leopard, Marble, Hex, TieDye, Stars, Carbon, Fade,
}

// What a real bowler looks at when buying a ball. The coverstock sets most of the hook; reactive
// resin comes as pearl (long, sharp back end), solid (early, strong, smooth) or hybrid (in between).
public enum Coverstock { Plastic, Urethane, Pearl, Hybrid, Solid }
// The weight block inside: plastic spare balls have a flat "pancake" weight, asymmetric cores flare more.
public enum BallCore { Pancake, Symmetric, Asymmetric }
// Polished balls skid further and turn harder; sanded (matte) ones grab the lane earlier and arc smoothly.
public enum BallSurface { Polished, Matte }

// One ball you can buy: its look (pattern + two colours + band glow) and its reaction (coverstock, core,
// surface). Weight and size are picked separately and apply to whichever ball is equipped.
public class BallDesign
{
    public string id;
    public BallPattern pattern;
    public Color a, b;
    public Color glow;
    public Coverstock cover;
    public BallCore core = BallCore.Symmetric;
    public BallSurface surface = BallSurface.Polished;
    public int price;

    public string Name => Loc.T("ball_" + id);
    public string CoverName => Loc.T("cover_" + cover.ToString().ToLowerInvariant());
    public string CoreName => Loc.T("core_" + core.ToString().ToLowerInvariant());
    public string SurfaceName => Loc.T("surface_" + surface.ToString().ToLowerInvariant());
    public string BestFor => Loc.T("ballfor_" + cover.ToString().ToLowerInvariant());

    // Hook multiplier: plastic barely hooks (a spare ball), solid reactive hooks hardest.
    public float HookFactor =>
        cover switch { Coverstock.Plastic => 0.55f, Coverstock.Urethane => 0.9f, Coverstock.Pearl => 1.15f, Coverstock.Hybrid => 1.25f, _ => 1.35f }
        * core switch { BallCore.Pancake => 0.9f, BallCore.Asymmetric => 1.08f, _ => 1f }
        * (surface == BallSurface.Polished ? 0.93f : 1.07f);

    // Metres down the lane before the ball really starts to turn ("length").
    public float SkidZ => Mathf.Clamp(
        cover switch { Coverstock.Plastic => 13f, Coverstock.Urethane => 7.5f, Coverstock.Pearl => 11f, Coverstock.Hybrid => 9.5f, _ => 8f }
        + (surface == BallSurface.Polished ? 2f : -1.5f), 6f, 15f);

    // 0 = a smooth, even arc; 1 = skids then snaps hard at the end ("back end").
    public float Backend => Mathf.Clamp01(
        cover switch { Coverstock.Plastic => 0f, Coverstock.Urethane => 0.05f, Coverstock.Pearl => 0.55f, Coverstock.Hybrid => 0.4f, _ => 0.3f }
        + (core == BallCore.Asymmetric ? 0.2f : 0f) + (surface == BallSurface.Polished ? 0.15f : -0.1f));

    // 1–5: how predictable the ball stays as lane oil builds up on it.
    public float Control => Mathf.Clamp(
        cover switch { Coverstock.Plastic => 5f, Coverstock.Urethane => 4.5f, Coverstock.Pearl => 2.5f, Coverstock.Hybrid => 3.5f, _ => 3f }
        - (core == BallCore.Asymmetric ? 0.5f : 0f) + (surface == BallSurface.Matte ? 0.5f : 0f), 1f, 5f);

    // Multiplies how much hook an oily ball loses (see BowlingGame).
    public float OilSensitivity => Mathf.Lerp(1.4f, 0.4f, (Control - 1f) / 4f);

    // The four shop bars, 0–1.
    public float HookStat => Mathf.InverseLerp(0.45f, 1.55f, HookFactor);
    public float LengthStat => Mathf.InverseLerp(6f, 15f, SkidZ);
    public float BackendStat => Backend;
    public float ControlStat => Mathf.InverseLerp(1f, 5f, Control);
}

// The pro shop: ball catalogue, coins, ownership and the equipped ball. Coins are earned by
// playing (see BowlingGame), saved via SaveSystem.
public static class ProShop
{
    static Color C(float r, float g, float b) => new(r, g, b);

    public static readonly BallDesign[] Balls =
    {
        new() { id = "starter", pattern = BallPattern.Solid, a = C(0.25f, 0.05f, 0.55f), b = C(0.35f, 0.1f, 0.7f), glow = C(0.35f, 0.05f, 0.8f), cover = Coverstock.Urethane, surface = BallSurface.Matte, price = 0 },
        new() { id = "pearl", pattern = BallPattern.Swirl, a = C(0.92f, 0.92f, 0.96f), b = C(0.7f, 0.75f, 0.85f), glow = C(0.5f, 0.55f, 0.7f), cover = Coverstock.Plastic, core = BallCore.Pancake, price = 150 },
        new() { id = "candy", pattern = BallPattern.Rainbow, a = C(1f, 0.6f, 0.8f), b = C(0.6f, 0.9f, 1f), glow = C(1f, 0.5f, 0.8f), cover = Coverstock.Plastic, core = BallCore.Pancake, price = 200 },
        new() { id = "polka", pattern = BallPattern.Dots, a = C(1f, 0.45f, 0.7f), b = C(1f, 0.92f, 0.3f), glow = C(1f, 0.3f, 0.6f), cover = Coverstock.Plastic, core = BallCore.Pancake, price = 250 },
        new() { id = "sweetheart", pattern = BallPattern.Hearts, a = C(1f, 0.82f, 0.88f), b = C(0.9f, 0.1f, 0.3f), glow = C(1f, 0.2f, 0.45f), cover = Coverstock.Plastic, core = BallCore.Pancake, price = 280 },
        new() { id = "ocean", pattern = BallPattern.Swirl, a = C(0.02f, 0.25f, 0.6f), b = C(0.2f, 0.9f, 1f), glow = C(0.1f, 0.6f, 1f), cover = Coverstock.Urethane, price = 300 },
        new() { id = "cherry", pattern = BallPattern.Stripes, a = C(0.85f, 0.05f, 0.12f), b = C(1f, 0.95f, 0.95f), glow = C(1f, 0.1f, 0.2f), cover = Coverstock.Urethane, surface = BallSurface.Matte, price = 300 },
        new() { id = "pixel", pattern = BallPattern.Pixel, a = C(0.1f, 0.12f, 0.35f), b = C(0.3f, 1f, 0.6f), glow = C(0.3f, 1f, 0.6f), cover = Coverstock.Plastic, core = BallCore.Pancake, price = 350 },
        new() { id = "zebra", pattern = BallPattern.Zebra, a = C(0.95f, 0.95f, 0.95f), b = C(0.05f, 0.05f, 0.06f), glow = C(0.8f, 0.8f, 0.9f), cover = Coverstock.Urethane, surface = BallSurface.Matte, price = 350 },
        new() { id = "camo", pattern = BallPattern.Camo, a = C(0.36f, 0.42f, 0.22f), b = C(0.2f, 0.16f, 0.1f), glow = C(0.5f, 0.7f, 0.2f), cover = Coverstock.Urethane, surface = BallSurface.Matte, price = 380 },
        new() { id = "racer", pattern = BallPattern.Checker, a = C(0.05f, 0.05f, 0.06f), b = C(0.95f, 0.95f, 0.95f), glow = C(0.8f, 0.8f, 0.8f), cover = Coverstock.Urethane, price = 400 },
        new() { id = "sunset", pattern = BallPattern.Fade, a = C(1f, 0.75f, 0.2f), b = C(0.55f, 0.1f, 0.5f), glow = C(1f, 0.45f, 0.3f), cover = Coverstock.Hybrid, surface = BallSurface.Matte, price = 450 },
        new() { id = "glitter", pattern = BallPattern.Sparkle, a = C(0.55f, 0.35f, 0.08f), b = C(1f, 0.9f, 0.55f), glow = C(1f, 0.8f, 0.3f), cover = Coverstock.Pearl, price = 550 },
        new() { id = "lava", pattern = BallPattern.Flame, a = C(0.35f, 0.02f, 0f), b = C(1f, 0.55f, 0.05f), glow = C(1f, 0.35f, 0f), cover = Coverstock.Solid, core = BallCore.Asymmetric, surface = BallSurface.Matte, price = 600 },
        new() { id = "tiedye", pattern = BallPattern.TieDye, a = C(0.9f, 0.2f, 0.6f), b = C(0.2f, 0.8f, 0.9f), glow = C(0.8f, 0.3f, 1f), cover = Coverstock.Hybrid, surface = BallSurface.Matte, price = 600 },
        new() { id = "leopard", pattern = BallPattern.Leopard, a = C(0.85f, 0.6f, 0.3f), b = C(0.12f, 0.07f, 0.03f), glow = C(1f, 0.6f, 0.2f), cover = Coverstock.Solid, surface = BallSurface.Matte, price = 650 },
        new() { id = "toxic", pattern = BallPattern.TwoTone, a = C(0.3f, 1f, 0.2f), b = C(0.04f, 0.06f, 0.04f), glow = C(0.3f, 1f, 0.2f), cover = Coverstock.Hybrid, price = 700 },
        new() { id = "starlight", pattern = BallPattern.Stars, a = C(0.05f, 0.08f, 0.3f), b = C(1f, 0.92f, 0.5f), glow = C(0.4f, 0.5f, 1f), cover = Coverstock.Pearl, price = 700 },
        new() { id = "marble", pattern = BallPattern.Marble, a = C(0.94f, 0.93f, 0.9f), b = C(0.15f, 0.15f, 0.18f), glow = C(0.7f, 0.7f, 0.8f), cover = Coverstock.Hybrid, price = 750 },
        new() { id = "galaxy", pattern = BallPattern.Galaxy, a = C(0.02f, 0.02f, 0.12f), b = C(0.6f, 0.2f, 1f), glow = C(0.5f, 0.2f, 1f), cover = Coverstock.Pearl, core = BallCore.Asymmetric, price = 800 },
        new() { id = "honeycomb", pattern = BallPattern.Hex, a = C(0.95f, 0.65f, 0.05f), b = C(0.15f, 0.08f, 0f), glow = C(1f, 0.7f, 0.1f), cover = Coverstock.Solid, core = BallCore.Asymmetric, surface = BallSurface.Matte, price = 850 },
        new() { id = "thunder", pattern = BallPattern.Lightning, a = C(0.05f, 0.04f, 0.18f), b = C(0.6f, 0.85f, 1f), glow = C(0.4f, 0.7f, 1f), cover = Coverstock.Pearl, core = BallCore.Asymmetric, price = 900 },
        new() { id = "carbon", pattern = BallPattern.Carbon, a = C(0.06f, 0.06f, 0.07f), b = C(0.35f, 0.36f, 0.4f), glow = C(1f, 0.15f, 0.15f), cover = Coverstock.Solid, core = BallCore.Asymmetric, price = 1200 },
        new() { id = "legend", pattern = BallPattern.Swirl, a = C(0.85f, 0.6f, 0.1f), b = C(1f, 0.95f, 0.7f), glow = C(1f, 0.75f, 0.2f), cover = Coverstock.Hybrid, core = BallCore.Asymmetric, price = 1500 },
    };

    public static readonly float[] SizeScale = { 0.95f, 1f, 1.05f };
    public static readonly string[] SizeNames = { "S", "M", "L" };

    public static int Coins => SaveSystem.Data.coins;

    public static BallDesign Find(string id) => Balls.FirstOrDefault(b => b.id == id) ?? Balls[0];
    public static BallDesign Equipped => Find(GameSettings.EquippedBall);

    public static bool Owns(BallDesign ball) =>
        ball.price == 0 || (SaveSystem.Data.ownedBalls ?? new string[0]).Contains(ball.id);

    public static void Earn(int coins)
    {
        if (coins <= 0) return;
        SaveSystem.Data.coins += coins;
        SaveSystem.Save();
    }

    public static bool Buy(BallDesign ball)
    {
        if (Owns(ball) || Coins < ball.price) return false;
        var data = SaveSystem.Data;
        data.coins -= ball.price;
        data.ownedBalls = (data.ownedBalls ?? new string[0]).Append(ball.id).ToArray();
        SaveSystem.Save();
        return true;
    }

    public static void Equip(BallDesign ball)
    {
        if (Owns(ball)) GameSettings.EquippedBall = ball.id;
    }

    // ---- Textures, generated once per ball ----

    static readonly Dictionary<string, Texture2D> surfaces = new(), previews = new();

    // Wrapped onto the ball sphere (u around, v pole to pole).
    public static Texture2D Surface(BallDesign ball)
    {
        if (surfaces.TryGetValue(ball.id, out var t) && t != null) return t;
        const int w = 256, h = 128;
        t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Ball " + ball.id, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = Paint(ball, (float)x / w, (float)y / h);
        t.SetPixels(px);
        t.Apply(true);
        return surfaces[ball.id] = t;
    }

    // A shaded sphere for the shop, with the ball's pattern on it.
    public static Texture2D Preview(BallDesign ball)
    {
        if (previews.TryGetValue(ball.id, out var t) && t != null) return t;
        const int n = 128;
        t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Preview " + ball.id, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        Vector3 light = new Vector3(-0.5f, 0.6f, 0.65f).normalized;
        float gloss = ball.surface == BallSurface.Polished ? 0.75f : 0.25f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float sx = (x + 0.5f) / n * 2f - 1f, sy = (y + 0.5f) / n * 2f - 1f;
                float r2 = sx * sx + sy * sy;
                if (r2 > 1f) { px[y * n + x] = Color.clear; continue; }
                var normal = new Vector3(sx, sy, Mathf.Sqrt(1f - r2));
                // Slight tilt so the pattern doesn't look flat-on.
                var p = Quaternion.Euler(20f, -25f, 0f) * normal;
                float u = Mathf.Repeat(Mathf.Atan2(p.x, p.z) / (2f * Mathf.PI) + 0.5f, 1f);
                float v = Mathf.Acos(Mathf.Clamp(p.y, -1f, 1f)) / Mathf.PI;
                Color c = Paint(ball, u, 1f - v);
                float diffuse = 0.35f + 0.65f * Mathf.Max(0f, Vector3.Dot(normal, light));
                float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(Vector3.Reflect(-light, normal), Vector3.forward)), ball.surface == BallSurface.Polished ? 32f : 8f) * gloss;
                c = c * diffuse + Color.white * spec;
                float edge = Mathf.Clamp01((1f - Mathf.Sqrt(r2)) * n * 0.5f); // anti-aliased rim
                c.a = edge;
                px[y * n + x] = c;
            }
        // Finger holes.
        foreach (var hole in new[] { new Vector2(0.5f, 0.74f), new Vector2(0.42f, 0.62f), new Vector2(0.58f, 0.62f) })
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2((x + 0.5f) / n, (y + 0.5f) / n), hole) * n;
                    if (d < 5f) px[y * n + x] = Color.Lerp(new Color(0f, 0f, 0f, 1f), px[y * n + x], Mathf.Clamp01(d - 4f));
                }
        t.SetPixels(px);
        t.Apply();
        return previews[ball.id] = t;
    }

    // ---- Pattern painting ----

    static float SS(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    // Nearest jittered point in a grid that wraps every `period` cells across u. Returns the
    // distance to it and that cell's id.
    static float Cells(float x, float y, int period, out int id)
    {
        int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
        float best = 9f;
        id = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int gx = cx + dx, gy = cy + dy, wx = ((gx % period) + period) % period;
                float px = gx + 0.15f + 0.7f * Hash(wx, gy), py = gy + 0.15f + 0.7f * Hash(wx + 71, gy + 13);
                float d = new Vector2(x - px, y - py).magnitude;
                if (d < best) { best = d; id = wx * 131 + gy; }
            }
        return best;
    }

    static Color Paint(BallDesign ball, float u, float v)
    {
        float x = u * 8f, y = v * 4f;
        switch (ball.pattern)
        {
            case BallPattern.Swirl:
            {
                // Domain-warped noise: marbled resin.
                float wx = Mathf.PerlinNoise(x * 0.7f + 3.1f, y * 0.7f) * 3f;
                float wy = Mathf.PerlinNoise(x * 0.7f, y * 0.7f + 7.3f) * 3f;
                float n = Mathf.PerlinNoise(x + wx, y + wy);
                float bands = Mathf.Sin(n * 14f) * 0.5f + 0.5f;
                return Color.Lerp(ball.a, ball.b, Mathf.SmoothStep(0.2f, 0.8f, bands));
            }
            case BallPattern.Stripes:
                return Mathf.Repeat(u * 6f + v * 3f, 1f) < 0.5f ? ball.a : ball.b;
            case BallPattern.Checker:
                return ((int)(u * 12f) + (int)(v * 6f)) % 2 == 0 ? ball.a : ball.b;
            case BallPattern.TwoTone:
            {
                float wave = 0.5f + Mathf.Sin(u * Mathf.PI * 4f) * 0.08f;
                return v < wave ? ball.a : ball.b;
            }
            case BallPattern.Dots:
            {
                float cx = Mathf.Repeat(u * 10f, 1f) - 0.5f, cy = Mathf.Repeat(v * 5f + ((int)(u * 10f) % 2) * 0.5f, 1f) - 0.5f;
                return cx * cx + cy * cy < 0.07f ? ball.b : ball.a;
            }
            case BallPattern.Flame:
            {
                float n = Mathf.PerlinNoise(x * 1.3f, y * 2.5f) * 0.6f + Mathf.PerlinNoise(x * 3f, y * 5f) * 0.4f;
                float heat = Mathf.Clamp01(v * 1.2f - 0.1f + (n - 0.5f) * 0.9f);
                Color c = Color.Lerp(ball.a, ball.b, Mathf.SmoothStep(0.25f, 0.75f, heat));
                return Color.Lerp(c, new Color(1f, 0.95f, 0.6f), Mathf.SmoothStep(0.85f, 1f, heat));
            }
            case BallPattern.Galaxy:
            {
                float neb = Mathf.PerlinNoise(x * 0.9f + 11f, y * 0.9f) * Mathf.PerlinNoise(x * 2f, y * 2f + 5f);
                Color c = Color.Lerp(ball.a, ball.b, Mathf.SmoothStep(0.1f, 0.5f, neb));
                c = Color.Lerp(c, new Color(0.2f, 0.7f, 1f), Mathf.SmoothStep(0.35f, 0.6f, Mathf.PerlinNoise(x * 1.7f + 30f, y * 1.7f)) * 0.35f);
                float star = Mathf.PerlinNoise(u * 180f, v * 90f);
                return star > 0.82f ? Color.white : c;
            }
            case BallPattern.Rainbow:
            {
                // Candy-coloured diagonal bands with a soft wobble.
                float hue = Mathf.Repeat(u * 2f + v * 0.6f + Mathf.PerlinNoise(x, y) * 0.15f, 1f);
                Color c = Color.HSVToRGB(hue, 0.55f, 1f);
                return Color.Lerp(c, Color.white, 0.12f);
            }
            case BallPattern.Pixel:
            {
                // Chunky 8-bit blocks in a small palette built from the two colours.
                int cx = (int)(u * 32f), cy = (int)(v * 16f);
                float r = Hash(cx, cy);
                if (r < 0.45f) return ball.a;
                if (r < 0.7f) return Color.Lerp(ball.a, Color.black, 0.35f);
                if (r < 0.85f) return ball.b;
                if (r < 0.95f) return Color.HSVToRGB(Mathf.Repeat(Hash(cy, cx) * 1f, 1f), 0.7f, 1f);
                return Color.white;
            }
            case BallPattern.Zebra:
            {
                float warp = Mathf.PerlinNoise(x * 0.8f, y * 0.8f) * 3f + Mathf.PerlinNoise(x * 2.2f + 5f, y * 2.2f) * 0.8f;
                float s = Mathf.Sin(u * Mathf.PI * 2f * 7f + warp * 2.2f + v * 3f);
                return Color.Lerp(ball.b, ball.a, SS(-0.15f, 0.15f, s));
            }
            case BallPattern.Camo:
            {
                float n = Mathf.PerlinNoise(x * 1.1f + 2f, y * 1.1f) * 0.65f + Mathf.PerlinNoise(x * 2.6f + 9f, y * 2.6f) * 0.35f;
                float m = Mathf.PerlinNoise(x * 1.4f + 40f, y * 1.4f + 3f);
                if (m > 0.66f) return Color.Lerp(ball.b, Color.black, 0.5f);
                if (n < 0.42f) return ball.a;
                if (n < 0.55f) return Color.Lerp(ball.a, ball.b, 0.45f) * 1.05f;
                return ball.b;
            }
            case BallPattern.Hearts:
            {
                int row = (int)(v * 5f);
                float qx = Mathf.Repeat(u * 10f + (row % 2) * 0.5f, 1f) - 0.5f, qy = 0.5f - Mathf.Repeat(v * 5f, 1f);
                float hx = qx * 3.2f, hy = qy * 3.2f + 0.25f;
                float f = Mathf.Pow(hx * hx + hy * hy - 1f, 3f) - hx * hx * hy * hy * hy;
                return f <= 0f ? ball.b : ball.a;
            }
            case BallPattern.Sparkle:
            {
                // Metal-flake: a shimmering base with bright flakes.
                Color c = Color.Lerp(ball.a, ball.a * 1.35f, Mathf.PerlinNoise(x * 3f, y * 3f));
                int fx = (int)(u * 170f), fy = (int)(v * 85f);
                float flake = Hash(fx, fy);
                if (flake > 0.9f) c = Color.Lerp(c, ball.b, 0.5f + 0.5f * Hash(fy, fx));
                if (flake > 0.985f) c = Color.white;
                return c;
            }
            case BallPattern.Lightning:
            {
                float n = Mathf.PerlinNoise(x * 1.3f + 7f, y * 1.3f) * 0.7f + Mathf.PerlinNoise(x * 4f, y * 4f + 3f) * 0.3f;
                float ridge = 1f - Mathf.Abs(n * 2f - 1f);
                Color c = Color.Lerp(ball.a, ball.a * 2.2f + new Color(0.02f, 0.02f, 0.06f), Mathf.PerlinNoise(x * 0.6f, y * 0.6f) * 0.6f);
                c = Color.Lerp(c, ball.b, SS(0.82f, 0.94f, ridge) * 0.45f);   // glow around the bolt
                return Color.Lerp(c, Color.white, SS(0.95f, 0.985f, ridge));    // the bolt itself
            }
            case BallPattern.Leopard:
            {
                float wx = x * 1.6f + Mathf.PerlinNoise(x * 2f, y * 2f) * 0.5f, wy = y * 1.6f + Mathf.PerlinNoise(x * 2f + 5f, y * 2f) * 0.5f;
                float d = Cells(wx, wy, 13, out int id);
                float size = 0.3f + 0.08f * Hash(id, 7);
                Color c = ball.a * (0.95f + 0.1f * Mathf.PerlinNoise(x * 6f, y * 6f));
                if (d < size * 0.62f) return Color.Lerp(ball.a, ball.b, 0.35f);                 // tawny centre
                if (d < size) return ball.b;                                                     // dark rosette
                return c;
            }
            case BallPattern.Marble:
            {
                float turb = Mathf.PerlinNoise(x * 1f, y * 1f) * 2f + Mathf.PerlinNoise(x * 2.3f + 4f, y * 2.3f) * 1f + Mathf.PerlinNoise(x * 5f, y * 5f + 9f) * 0.5f;
                float vein = Mathf.Abs(Mathf.Sin((x * 0.6f + y * 1.2f + turb * 2.4f)));
                Color c = Color.Lerp(ball.a, ball.a * 0.88f, Mathf.PerlinNoise(x * 3f + 20f, y * 3f));
                c = Color.Lerp(ball.b, c, SS(0.02f, 0.18f, vein));
                return Color.Lerp(c, Color.Lerp(ball.b, ball.a, 0.6f), (1f - SS(0f, 0.45f, vein)) * 0.25f);
            }
            case BallPattern.Hex:
            {
                // Honeycomb: hex cells with dark walls.
                var p = new Vector2(u * 18f, v * 9f * 1.15f);
                var r = new Vector2(1f, 1.732f);
                var hh = r * 0.5f;
                var a = new Vector2(Mathf.Repeat(p.x, r.x), Mathf.Repeat(p.y, r.y)) - hh;
                var b = new Vector2(Mathf.Repeat(p.x - hh.x, r.x), Mathf.Repeat(p.y - hh.y, r.y)) - hh;
                var g = a.sqrMagnitude < b.sqrMagnitude ? a : b;
                var ag = new Vector2(Mathf.Abs(g.x), Mathf.Abs(g.y));
                float hexD = Mathf.Max(Vector2.Dot(ag, new Vector2(0.5f, 0.866f)), ag.x);
                var cellId = p - g;
                float shade = 0.85f + 0.3f * Hash(Mathf.RoundToInt(cellId.x * 2f), Mathf.RoundToInt(cellId.y * 2f));
                return Color.Lerp(ball.a * shade, ball.b, SS(0.4f, 0.46f, hexD));
            }
            case BallPattern.TieDye:
            {
                float swirl = Mathf.Sin(u * Mathf.PI * 2f) * 1.6f + v * 5f + Mathf.PerlinNoise(x * 0.9f, y * 0.9f) * 2.2f;
                float band = Mathf.Repeat(swirl, 1f);
                Color.RGBToHSV(ball.a, out float h0, out _, out _);
                Color c = Color.HSVToRGB(Mathf.Repeat(h0 + band * 0.75f, 1f), 0.8f, 1f);
                return Color.Lerp(c, Color.white, (1f - SS(0f, 0.06f, Mathf.Min(band, 1f - band))) * 0.35f);
            }
            case BallPattern.Stars:
            {
                int row = (int)(v * 5f), col = (int)(u * 10f + (row % 2) * 0.5f);
                float qx = Mathf.Repeat(u * 10f + (row % 2) * 0.5f, 1f) - 0.5f, qy = Mathf.Repeat(v * 5f, 1f) - 0.5f;
                Color c = Color.Lerp(ball.a, ball.a * 1.6f, Mathf.PerlinNoise(x * 1.5f, y * 1.5f) * 0.6f);
                if (Mathf.PerlinNoise(u * 150f, v * 75f) > 0.8f) c = Color.Lerp(c, Color.white, 0.7f); // tiny far stars
                if (Hash(col, row) < 0.25f) return c;
                float ang = Mathf.Atan2(qx, qy) + Hash(row, col) * 1.2f, len = Mathf.Sqrt(qx * qx + qy * qy);
                float edge = (0.13f + 0.17f * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(5f * ang), 2.5f)) * (0.75f + 0.35f * Hash(col + 3, row));
                return len < edge ? ball.b : c;
            }
            case BallPattern.Carbon:
            {
                // Woven carbon fibre with a coloured pinstripe round the equator.
                int cx = (int)(u * 64f), cy = (int)(v * 32f);
                float lx = Mathf.Repeat(u * 64f, 1f), ly = Mathf.Repeat(v * 32f, 1f);
                bool across = (cx + cy) % 2 == 0;
                float s = Mathf.Sin((across ? ly : lx) * Mathf.PI);
                Color c = Color.Lerp(ball.a, ball.b, s * s * 0.8f);
                float stripe = Mathf.Abs(v - 0.5f);
                return stripe < 0.015f ? ball.glow : c;
            }
            case BallPattern.Fade:
            {
                float t = v + (Mathf.PerlinNoise(x * 1.2f, y * 1.2f) - 0.5f) * 0.25f;
                Color c = Color.Lerp(ball.b, ball.a, SS(0.15f, 0.85f, t));
                return Color.Lerp(c, Color.white, SS(0.92f, 1f, t) * 0.4f);
            }
            default:
            {
                float n = Mathf.PerlinNoise(x * 2f, y * 2f);
                return Color.Lerp(ball.a, ball.b, n * 0.35f);
            }
        }
    }
}
