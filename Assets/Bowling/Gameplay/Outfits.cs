using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// What sits on the bowler's head (and a few whole-look extras).
public enum HeadStyle { Helmet, Hair, Pompadour, Afro, Cap, Crown, CatEars, Mohawk, Ninja, Astronaut, Robot }

// One character look for the bowler: clothes, skin, hair and headwear, plus the two neon accent
// colours on the belt, stripes, wristbands and soles. Bowler.ApplyOutfit dresses the 3D figure.
public class Outfit
{
    public string id;
    public int price;
    public Color top, bottom, skin, hair, shoes, hat;
    public Color neonA, neonB;          // display colours; Bowler boosts them to HDR for the bloom
    public HeadStyle head;
    public bool shortSleeves, shades;
    public bool suit;                   // full-body suit: gloves and collar in the top colour

    public string Name => Loc.T("outfit_" + id);
    public string Description => Loc.T("outfit_" + id + "_desc");
}

public static class Outfits
{
    static Color C(float r, float g, float b) => new(r, g, b);

    public static readonly Outfit[] All =
    {
        new() { id = "neon", head = HeadStyle.Helmet, suit = true, top = C(0.1f, 0.09f, 0.17f), bottom = C(0.1f, 0.09f, 0.17f), skin = C(0.9f, 0.72f, 0.58f),
                hair = C(0.1f, 0.08f, 0.06f), shoes = C(0.04f, 0.04f, 0.07f), hat = C(0.04f, 0.04f, 0.07f), neonA = C(0f, 0.85f, 1f), neonB = C(1f, 0.12f, 0.85f) },
        new() { id = "classic", head = HeadStyle.Hair, shortSleeves = true, top = C(0.75f, 0.1f, 0.12f), bottom = C(0.08f, 0.08f, 0.1f), skin = C(0.88f, 0.68f, 0.52f),
                hair = C(0.28f, 0.16f, 0.08f), shoes = C(0.92f, 0.92f, 0.9f), hat = C(0.28f, 0.16f, 0.08f), neonA = C(1f, 0.95f, 0.85f), neonB = C(1f, 0.2f, 0.2f) },
        new() { id = "sporty", price = 200, head = HeadStyle.Cap, shortSleeves = true, top = C(0.95f, 0.95f, 0.97f), bottom = C(0.1f, 0.2f, 0.5f), skin = C(0.72f, 0.52f, 0.38f),
                hair = C(0.06f, 0.05f, 0.05f), shoes = C(0.95f, 0.95f, 0.97f), hat = C(0.1f, 0.35f, 0.9f), neonA = C(0.2f, 0.55f, 1f), neonB = C(1f, 1f, 1f) },
        new() { id = "retro", price = 150, head = HeadStyle.Pompadour, shortSleeves = true, top = C(0.55f, 0.9f, 0.78f), bottom = C(0.15f, 0.25f, 0.5f), skin = C(0.95f, 0.76f, 0.62f),
                hair = C(0.05f, 0.04f, 0.04f), shoes = C(0.7f, 0.1f, 0.1f), hat = C(0.05f, 0.04f, 0.04f), neonA = C(0.3f, 1f, 0.8f), neonB = C(1f, 0.2f, 0.3f) },
        new() { id = "kitty", price = 300, head = HeadStyle.CatEars, top = C(1f, 0.55f, 0.75f), bottom = C(0.95f, 0.95f, 0.97f), skin = C(0.98f, 0.82f, 0.72f),
                hair = C(0.98f, 0.85f, 0.55f), shoes = C(1f, 0.55f, 0.75f), hat = C(1f, 0.6f, 0.75f), neonA = C(1f, 0.4f, 0.8f), neonB = C(1f, 0.9f, 1f) },
        new() { id = "ninja", price = 350, head = HeadStyle.Ninja, suit = true, top = C(0.06f, 0.06f, 0.07f), bottom = C(0.06f, 0.06f, 0.07f), skin = C(0.88f, 0.68f, 0.52f),
                hair = C(0.06f, 0.06f, 0.07f), shoes = C(0.03f, 0.03f, 0.03f), hat = C(0.85f, 0.08f, 0.1f), neonA = C(1f, 0.1f, 0.15f), neonB = C(1f, 0.1f, 0.15f) },
        new() { id = "astronaut", price = 400, head = HeadStyle.Astronaut, suit = true, top = C(0.92f, 0.92f, 0.95f), bottom = C(0.92f, 0.92f, 0.95f), skin = C(0.9f, 0.72f, 0.58f),
                hair = C(0.1f, 0.08f, 0.06f), shoes = C(0.6f, 0.62f, 0.66f), hat = C(0.95f, 0.95f, 0.97f), neonA = C(1f, 0.5f, 0.1f), neonB = C(1f, 0.8f, 0.2f) },
        new() { id = "disco", price = 450, head = HeadStyle.Afro, shortSleeves = true, shades = true, top = C(1f, 0.78f, 0.22f), bottom = C(0.97f, 0.97f, 0.97f), skin = C(0.55f, 0.36f, 0.24f),
                hair = C(0.12f, 0.07f, 0.04f), shoes = C(0.97f, 0.97f, 0.97f), hat = C(0.12f, 0.07f, 0.04f), neonA = C(1f, 0.8f, 0.2f), neonB = C(1f, 0.3f, 0.7f) },
        new() { id = "rocker", price = 500, head = HeadStyle.Mohawk, shortSleeves = true, shades = true, top = C(0.08f, 0.07f, 0.08f), bottom = C(0.14f, 0.14f, 0.18f), skin = C(0.9f, 0.7f, 0.55f),
                hair = C(1f, 0.15f, 0.8f), shoes = C(0.1f, 0.1f, 0.1f), hat = C(1f, 0.15f, 0.8f), neonA = C(1f, 0.15f, 0.8f), neonB = C(0.4f, 1f, 0.3f) },
        new() { id = "robot", price = 600, head = HeadStyle.Robot, suit = true, top = C(0.62f, 0.64f, 0.7f), bottom = C(0.45f, 0.47f, 0.52f), skin = C(0.62f, 0.64f, 0.7f),
                hair = C(0.62f, 0.64f, 0.7f), shoes = C(0.25f, 0.26f, 0.3f), hat = C(0.62f, 0.64f, 0.7f), neonA = C(1f, 0.15f, 0.15f), neonB = C(0.2f, 0.9f, 1f) },
        new() { id = "king", price = 800, head = HeadStyle.Crown, top = C(0.38f, 0.1f, 0.58f), bottom = C(0.95f, 0.95f, 0.95f), skin = C(0.95f, 0.78f, 0.65f),
                hair = C(0.45f, 0.28f, 0.1f), shoes = C(0.4f, 0.08f, 0.1f), hat = C(1f, 0.78f, 0.2f), neonA = C(1f, 0.8f, 0.2f), neonB = C(0.75f, 0.3f, 1f) },
    };

    public static Outfit Find(string id) => All.FirstOrDefault(o => o.id == id) ?? All[0];
    public static Outfit Equipped => Find(GameSettings.EquippedOutfit);

    public static bool Owns(Outfit o) =>
        o.price == 0 || (SaveSystem.Data.ownedOutfits ?? new string[0]).Contains(o.id);

    public static bool Buy(Outfit o)
    {
        if (Owns(o) || ProShop.Coins < o.price) return false;
        var data = SaveSystem.Data;
        data.coins -= o.price;
        data.ownedOutfits = (data.ownedOutfits ?? new string[0]).Append(o.id).ToArray();
        SaveSystem.Save();
        return true;
    }

    public static void Equip(Outfit o)
    {
        if (Owns(o)) GameSettings.EquippedOutfit = o.id;
    }

    // ---- A flat front-view portrait for the shop ----

    static readonly Dictionary<string, Texture2D> previews = new();

    public const int PreviewW = 96, PreviewH = 128;

    public static Texture2D Preview(Outfit o)
    {
        if (previews.TryGetValue(o.id, out var t) && t != null) return t;
        t = new Texture2D(PreviewW, PreviewH, TextureFormat.RGBA32, false) { name = "Outfit " + o.id, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
        var px = new Color[PreviewW * PreviewH];
        const float unit = PreviewW / 2f; // pixels per unit: x spans -1…1
        for (int y = 0; y < PreviewH; y++)
            for (int x = 0; x < PreviewW; x++)
            {
                var p = new Vector2((x + 0.5f) / unit - 1f, (y + 0.5f) / unit - 0.3f); // figure centred
                px[y * PreviewW + x] = PaintFigure(o, p, unit);
            }
        t.SetPixels(px);
        t.Apply();
        return previews[o.id] = t;
    }

    static float Box(Vector2 p, float cx, float cy, float hw, float hh, float r)
    {
        var q = new Vector2(Mathf.Abs(p.x - cx) - hw + r, Mathf.Abs(p.y - cy) - hh + r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    static float Circle(Vector2 p, float cx, float cy, float r) => (p - new Vector2(cx, cy)).magnitude - r;

    static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        // Signed distance approximation: max of the three edge half-planes.
        float E(Vector2 u, Vector2 v) { var n = new Vector2(v.y - u.y, u.x - v.x).normalized; return Vector2.Dot(p - u, n); }
        float s = Mathf.Sign((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
        return Mathf.Max(E(a, b) * s, Mathf.Max(E(b, c) * s, E(c, a) * s));
    }

    static Color Shade(Color c, Vector2 p, float cx) => c * (1.08f - 0.25f * Mathf.Clamp01((p.x - cx) * 2f + 0.5f) * 0.6f);

    static Color PaintFigure(Outfit o, Vector2 p, float unit)
    {
        Color col = Color.clear;
        void Put(float sd, Color c)
        {
            float a = Mathf.Clamp01(0.5f - sd * unit);
            if (a <= 0f) return;
            c.a = 1f;
            col = Color.Lerp(col, c, a);
            col.a = Mathf.Max(col.a, a);
        }
        Color Glow(Color c) => Color.Lerp(c, Color.white, 0.25f);

        bool fullHelmet = o.head is HeadStyle.Helmet or HeadStyle.Astronaut or HeadStyle.Robot;
        Color hands = o.suit ? o.top : o.skin;
        float headY = 1.62f, headR = o.head == HeadStyle.Astronaut ? 0.25f : 0.2f;

        // Behind the head: afro.
        if (o.head == HeadStyle.Afro) Put(Circle(p, 0f, headY + 0.04f, 0.32f), o.hair);
        if (o.head == HeadStyle.Astronaut) Put(Box(p, 0f, 1.05f, 0.3f, 0.26f, 0.06f), o.hat * 0.85f); // backpack peeking out

        // Legs and shoes.
        foreach (float s in new[] { -1f, 1f })
        {
            Put(Box(p, s * 0.13f, 0.5f, 0.1f, 0.34f, 0.06f), Shade(o.bottom, p, s * 0.13f));
            Put(Box(p, s * 0.15f, 0.12f, 0.14f, 0.07f, 0.04f), o.shoes);
            Put(Box(p, s * 0.15f, 0.05f, 0.14f, 0.018f, 0.01f), Glow(o.neonB));
        }
        // Arms.
        foreach (float s in new[] { -1f, 1f })
        {
            Put(Box(p, s * 0.4f, 1.12f, 0.085f, 0.2f, 0.07f), Shade(o.top, p, s * 0.4f));
            Put(Box(p, s * 0.42f, 0.78f, 0.075f, 0.17f, 0.06f), o.shortSleeves ? o.skin : Shade(o.top, p, s * 0.42f));
            Put(Box(p, s * 0.42f, 0.66f, 0.08f, 0.018f, 0.01f), Glow(s < 0 ? o.neonB : o.neonA));
            Put(Circle(p, s * 0.42f, 0.58f, 0.075f), hands);
        }
        // Torso, belt, stripe.
        Put(Box(p, 0f, 1.08f, 0.3f, 0.3f, 0.12f), Shade(o.top, p, 0f));
        Put(Box(p, 0f, 0.82f, 0.29f, 0.03f, 0.01f), Glow(o.neonA));
        Put(Box(p, 0f, 1.24f, 0.2f, 0.022f, 0.01f), Glow(o.neonB));
        Put(Box(p, 0f, 1.4f, 0.06f, 0.05f, 0.02f), o.suit ? o.top : o.skin);

        // Head.
        Color headCol = o.head switch
        {
            HeadStyle.Helmet => o.hat, HeadStyle.Astronaut => o.hat, HeadStyle.Robot => o.top, HeadStyle.Ninja => o.top, _ => o.skin,
        };
        if (o.head == HeadStyle.Robot) Put(Box(p, 0f, headY, 0.19f, 0.19f, 0.04f), headCol);
        else Put(Circle(p, 0f, headY, headR), headCol);

        if (!fullHelmet && o.head != HeadStyle.Ninja)
        {
            // Hair on top, then eyes and a smile.
            if (o.head is HeadStyle.Hair or HeadStyle.Pompadour or HeadStyle.CatEars or HeadStyle.Crown or HeadStyle.Cap or HeadStyle.Afro)
                Put(Mathf.Max(Circle(p, 0f, headY + 0.02f, headR + 0.015f), headY + 0.06f - p.y), o.head == HeadStyle.Cap ? o.hat : o.hair);
            if (o.head == HeadStyle.Pompadour) Put(Circle(p, 0.03f, headY + 0.2f, 0.13f), o.hair);
            if (!o.shades)
                foreach (float s in new[] { -1f, 1f }) Put(Circle(p, s * 0.07f, headY - 0.01f, 0.025f), new Color(0.08f, 0.06f, 0.1f));
            Put(Mathf.Max(Circle(p, 0f, headY - 0.06f, 0.07f), -Circle(p, 0f, headY - 0.03f, 0.07f)), new Color(0.45f, 0.15f, 0.15f));
        }
        if (o.shades) Put(Box(p, 0f, headY, 0.17f, 0.04f, 0.02f), new Color(0.03f, 0.03f, 0.05f));

        switch (o.head)
        {
            case HeadStyle.Helmet:
            case HeadStyle.Robot:
                Put(Box(p, 0f, headY, 0.15f, 0.04f, 0.02f), Glow(o.neonA));
                break;
            case HeadStyle.Astronaut:
                Put(Box(p, 0f, headY - 0.01f, 0.17f, 0.1f, 0.06f), Color.Lerp(o.neonB, Color.white, 0.15f));
                break;
            case HeadStyle.Ninja:
                Put(Box(p, 0f, headY, 0.2f, 0.035f, 0.02f), o.skin);
                foreach (float s in new[] { -1f, 1f }) Put(Circle(p, s * 0.07f, headY, 0.02f), new Color(0.08f, 0.06f, 0.1f));
                Put(Box(p, 0f, headY + 0.1f, 0.2f, 0.03f, 0.01f), o.hat);
                break;
            case HeadStyle.Cap:
                Put(Box(p, 0.08f, headY + 0.07f, 0.2f, 0.025f, 0.012f), o.hat * 0.8f);
                break;
            case HeadStyle.Crown:
                Put(Box(p, 0f, headY + 0.24f, 0.15f, 0.04f, 0.01f), o.hat);
                foreach (float s in new[] { -0.12f, 0f, 0.12f })
                    Put(Tri(p, new Vector2(s - 0.05f, headY + 0.27f), new Vector2(s + 0.05f, headY + 0.27f), new Vector2(s, headY + 0.37f)), o.hat);
                Put(Circle(p, 0f, headY + 0.24f, 0.025f), Glow(o.neonB));
                break;
            case HeadStyle.CatEars:
                foreach (float s in new[] { -1f, 1f })
                {
                    Put(Tri(p, new Vector2(s * 0.06f, headY + 0.14f), new Vector2(s * 0.2f, headY + 0.08f), new Vector2(s * 0.17f, headY + 0.3f)), o.hair);
                    Put(Tri(p, new Vector2(s * 0.09f, headY + 0.14f), new Vector2(s * 0.18f, headY + 0.11f), new Vector2(s * 0.16f, headY + 0.24f)), o.hat);
                }
                break;
            case HeadStyle.Mohawk:
                Put(Box(p, 0f, headY + 0.23f, 0.045f, 0.12f, 0.03f), Glow(o.hair));
                break;
        }
        if (o.head == HeadStyle.Robot)
        {
            Put(Box(p, 0f, headY + 0.25f, 0.012f, 0.06f, 0.005f), o.top);
            Put(Circle(p, 0f, headY + 0.32f, 0.035f), Glow(o.neonA));
        }
        return col;
    }
}
