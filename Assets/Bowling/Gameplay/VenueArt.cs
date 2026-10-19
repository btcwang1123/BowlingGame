using System.Collections.Generic;
using UnityEngine;

// Smooth preview pictures of each venue for the venue picker, painted once per venue: the lane
// in perspective with its boards and reflections, glowing neon rails, the pin deck, haze and a
// vignette, all anti-aliased so it reads as a small render rather than blocks of colour.
public static class VenueArt
{
    public const int Width = 480, Height = 300;

    static readonly Dictionary<string, Texture2D> cache = new();

    static int lastPaintFrame = -1;

    // The venue's picture, or null while it is still waiting to be painted: at most one is painted
    // per frame, so opening the picker doesn't stall on all of them at once.
    public static Texture2D Preview(Venue v)
    {
        if (cache.TryGetValue(v.id, out var t) && t != null) return t;
        if (lastPaintFrame == Time.frameCount) return null;
        lastPaintFrame = Time.frameCount;
        t = Paint(v);
        cache[v.id] = t;
        return t;
    }

    // For tools: paint every venue now.
    public static Texture2D PaintNow(Venue v) => Paint(v);

    // HDR neon → displayable, keeping its hue.
    static Color Lo(Color hdr)
    {
        float m = Mathf.Max(1f, Mathf.Max(hdr.r, Mathf.Max(hdr.g, hdr.b)));
        return new Color(hdr.r / m, hdr.g / m, hdr.b / m, 1f);
    }

    static Texture2D Paint(Venue v)
    {
        const int w = Width, h = Height;
        var px = new Color[w * h];
        Color n0 = Lo(v.neon[0]), n1 = Lo(v.neon[1]), n2 = Lo(v.neon[2]), n3 = Lo(v.neon[3]);
        // Bright venues (wood lanes, daylight) read better a touch lighter than their fog colour.
        Color skyTop = Color.Lerp(v.dark, v.fog, 0.5f) * 0.9f + new Color(0.02f, 0.02f, 0.035f);
        Color skyBottom = Color.Lerp(v.floor, v.dark, 0.4f) + new Color(0.01f, 0.01f, 0.02f);
        Color lane = v.lane + new Color(0.025f, 0.025f, 0.04f);

        const float horizon = 0.34f;           // pin deck height (fraction from the top)
        const float nearHalf = 0.42f, farHalf = 0.085f;
        for (int y = 0; y < h; y++)
        {
            // fy: 0 at the top of the picture, 1 at the bottom.
            float fy = 1f - (y + 0.5f) / h;
            for (int x = 0; x < w; x++)
            {
                float fx = (x + 0.5f) / w;
                float cx = fx - 0.5f;

                // Backdrop: gradient plus two soft neon glows high on the walls.
                Color c = Color.Lerp(skyTop, skyBottom, fy);
                c += n1 * 0.22f * Glow(fx, fy, 0.12f, 0.12f, 0.28f);
                c += n2 * 0.22f * Glow(fx, fy, 0.88f, 0.12f, 0.28f);
                c += n3 * 0.10f * Glow(fx, fy, 0.5f, 0.02f, 0.22f);

                if (fy >= horizon - 0.06f)
                {
                    // Depth along the lane: 0 at the pins, 1 at the bottom edge (nearest).
                    float d = Mathf.Clamp01((fy - horizon) / (1f - horizon));
                    float persp = d * d * 0.35f + d * 0.65f;
                    float half = Mathf.Lerp(farHalf, nearHalf, d);   // straight edges, like real perspective
                    float pxPerUnit = w;
                    float edge = (half - Mathf.Abs(cx)) * pxPerUnit;   // > 0 inside the lane, in pixels

                    if (fy >= horizon)
                    {
                        // Gutters and the dark floor beside them.
                        float gutterOuter = half * 1.18f;
                        float gEdge = (gutterOuter - Mathf.Abs(cx)) * pxPerUnit;
                        if (gEdge > -1f)
                        {
                            Color gutter = Color.Lerp(v.dark, Color.black, 0.45f) + n0 * 0.05f;
                            c = Color.Lerp(c, gutter, Mathf.Clamp01(gEdge + 0.5f));
                        }

                        // The lane: boards, a long reflection down the middle, darker far away.
                        if (edge > -1f)
                        {
                            float u = cx / half;                                   // -1 … 1 across
                            Color l = lane * Mathf.Lerp(0.55f, 1.05f, persp);
                            if (v.wood)
                            {
                                float board = Mathf.Repeat((u + 1f) * 9.75f, 1f);
                                float seam = Mathf.SmoothStep(0f, 1f, Mathf.Abs(board - 0.5f) * 2f);
                                l *= 1f - 0.12f * seam * seam;
                                l *= 0.94f + 0.06f * Mathf.Sin(u * 31f + d * 7f);
                            }
                            else
                            {
                                float board = Mathf.Repeat((u + 1f) * 9.75f, 1f);
                                l += n0 * 0.035f * Mathf.Pow(Mathf.Abs(board - 0.5f) * 2f, 8f);
                            }
                            float spec = Mathf.Exp(-u * u * 9f) * Mathf.Lerp(0.35f, 0.08f, persp) * v.laneSmoothness;
                            l += Color.Lerp(n3, Color.white, 0.6f) * spec * 0.35f;
                            l += n1 * 0.10f * Mathf.Exp(-(u + 0.75f) * (u + 0.75f) * 30f) * (1f - persp);
                            l += n2 * 0.10f * Mathf.Exp(-(u - 0.75f) * (u - 0.75f) * 30f) * (1f - persp);
                            // Targeting arrows a third of the way down.
                            float arrowRow = Mathf.Exp(-Mathf.Pow((d - 0.62f) * 40f, 2f));
                            float arrows = Mathf.Pow(Mathf.Abs(Mathf.Cos(u * Mathf.PI * 3.5f)), 30f);
                            l = Color.Lerp(l, Color.Lerp(n1, Color.white, 0.3f), arrowRow * arrows * 0.6f);
                            c = Color.Lerp(c, l, Mathf.Clamp01(edge + 0.5f));
                        }

                        // Neon rails along both sides, with a soft bloom.
                        float rail = Mathf.Abs(Mathf.Abs(cx) - half * 1.2f) * pxPerUnit;
                        float railW = Mathf.Lerp(0.8f, 2.6f, persp);
                        c += n0 * (Mathf.Clamp01(railW - rail + 0.5f) * 0.9f + Mathf.Exp(-rail / (railW * 4f)) * 0.35f);
                    }
                }

                // Pin deck: ten pins in a triangle just above the horizon line.
                c = Pins(c, fx, fy, horizon, n3);

                // Haze, then a vignette.
                float haze = Mathf.Exp(-Mathf.Pow((fy - horizon) * 6f, 2f)) * 0.18f;
                c = Color.Lerp(c, v.fog + n2 * 0.1f, haze);
                float vig = 1f - 0.55f * Mathf.Pow(Mathf.Clamp01(new Vector2(cx * 1.1f, fy - 0.55f).magnitude * 1.25f), 2.2f);
                c *= vig;
                c.a = 1f;
                px[y * w + x] = c;
            }
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true)
        { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
        t.SetPixels(px);
        t.Apply(true);
        return t;
    }

    static float Glow(float x, float y, float gx, float gy, float r)
    {
        float dx = (x - gx) / r, dy = (y - gy) / r;
        return Mathf.Exp(-(dx * dx + dy * dy) * 2f);
    }

    static Color Pins(Color c, float fx, float fy, float horizon, Color accent)
    {
        const float spacing = 0.034f, rowStep = 0.021f;
        for (int row = 3; row >= 0; row--)   // back row first, so front pins are drawn over it
        {
            for (int i = 0; i <= row; i++)
            {
                float px = 0.5f + (i - row / 2f) * spacing;
                float baseY = horizon - row * rowStep + 0.005f;
                float hgt = 0.085f - row * 0.005f, wid = 0.012f - row * 0.0008f;
                // Body: a rounded capsule from base to head, with a slimmer neck.
                float t = Mathf.Clamp01((baseY - fy) / hgt);
                if (fy > baseY + 0.004f || fy < baseY - hgt - 0.006f) continue;
                float profile = t < 0.55f ? Mathf.Lerp(0.75f, 1f, Mathf.Sin(t / 0.55f * Mathf.PI * 0.5f))
                    : t < 0.72f ? Mathf.Lerp(1f, 0.5f, (t - 0.55f) / 0.17f)
                    : 0.5f + 0.25f * Mathf.Sin((t - 0.72f) / 0.28f * Mathf.PI);
                float halfW = wid * profile;
                float dist = (halfW - Mathf.Abs(fx - px)) * Width;
                float top = (fy - (baseY - hgt)) * Height;
                float alpha = Mathf.Clamp01(dist + 0.5f) * Mathf.Clamp01(top + 0.5f);
                if (alpha <= 0f) continue;
                float shade = 0.75f + 0.25f * (1f - Mathf.Abs(fx - px) / Mathf.Max(halfW, 1e-4f));
                Color pin = new Color(0.97f, 0.96f, 0.94f) * shade;
                if (t > 0.6f && t < 0.68f) pin = Color.Lerp(new Color(0.85f, 0.1f, 0.15f), accent, 0.2f);
                c = Color.Lerp(c, pin, alpha);
            }
        }
        return c;
    }
}
