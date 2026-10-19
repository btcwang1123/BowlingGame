using System.Linq;
using UnityEngine;

// Pro shop, venue picker, and the on-lane calibration HUD bar and report (the profile manager is in BowlingUI.Players.cs).
public partial class BowlingUI
{
    // A purchase waiting for the "buy it?" confirmation: a ball or a character.
    class Purchase
    {
        public string name;
        public int price;
        public Texture2D image;
        public System.Func<bool> buy;
        public System.Action equip;
    }

    Purchase pendingBuy;
    float shopHeight = 1400f, venueHeight = 1200f;
    int shopTab;              // 0 = balls, 1 = characters
    bool showBallGuide;

    static readonly Color HookColor = new(1f, 0.4f, 0.75f), LengthColor = new(0.3f, 0.85f, 1f), BackendColor = new(1f, 0.75f, 0.25f), ControlColor = new(0.45f, 1f, 0.55f);

    // ---- Pro shop: balls (look, reaction, weight, size) and characters ----

    void Shop()
    {
        float y = Header(Loc.T("shop"));
        CoinChip(new Rect(W - 190f - safeRight, 22f + safeTop, 174f, 48f));
        var col = Column(y, 0f, 720f);
        float tw = (col.width - 12f) / 2f;
        if (Button(new Rect(col.x, y, tw, TapHSmall), Loc.T("shop_tab_balls"), shopTab == 0 ? buttonOn : buttonSmall) && shopTab != 0) { shopTab = 0; scroll = Vector2.zero; }
        if (Button(new Rect(col.x + tw + 12f, y, tw, TapHSmall), Loc.T("shop_tab_characters"), shopTab == 1 ? buttonOn : buttonSmall) && shopTab != 1) { shopTab = 1; scroll = Vector2.zero; }
        y += TapHSmall + 12f;
        var view = new Rect(col.x, y, col.width, H - y - 10f - safeBottom);
        BeginScroll(view, shopHeight);
        float cw = col.width - 20f;
        shopHeight = shopTab == 0 ? ShopBalls(cw) : ShopCharacters(cw);
        GUI.EndScrollView();
        if (GUI.changed) game.ApplyEquipment();
    }

    // One ten-segment bar for a ball property; compare (0–1, or < 0 for none) marks the equipped ball.
    void StatBar(Rect r, string name, float value, Color color, float compare, float labelW, int fontSize)
    {
        GUI.Label(new Rect(r.x, r.y, labelW, r.height), name, Small(DimText, TextAnchor.MiddleLeft, fontSize));
        float bx = r.x + labelW, bw = r.width - labelW, gap = 2f, seg = (bw - gap * 9f) / 10f, sh = Mathf.Min(12f, r.height - 8f);
        int filled = Mathf.Clamp(Mathf.RoundToInt(value * 10f), 1, 10);
        for (int k = 0; k < 10; k++)
            RoundTint(new Rect(bx + k * (seg + gap), r.center.y - sh / 2f, seg, sh), k < filled ? color : new Color(1f, 1f, 1f, 0.1f));
        if (compare >= 0f && Mathf.Abs(compare - value) > 0.04f)
        {
            int c = Mathf.Clamp(Mathf.RoundToInt(compare * 10f), 1, 10);
            Tint(new Rect(bx + c * (seg + gap) - gap / 2f - 1.5f, r.center.y - sh / 2f - 4f, 3f, sh + 8f), Color.white);
        }
    }

    void BallStats(BallDesign ball, BallDesign compareTo, float x, float y, float w, float rowH, float labelW, int fontSize)
    {
        bool cmp = compareTo != null && compareTo != ball;
        StatBar(new Rect(x, y, w, rowH), Loc.T("stat_hook"), ball.HookStat, HookColor, cmp ? compareTo.HookStat : -1f, labelW, fontSize); y += rowH;
        StatBar(new Rect(x, y, w, rowH), Loc.T("stat_length"), ball.LengthStat, LengthColor, cmp ? compareTo.LengthStat : -1f, labelW, fontSize); y += rowH;
        StatBar(new Rect(x, y, w, rowH), Loc.T("stat_backend"), ball.BackendStat, BackendColor, cmp ? compareTo.BackendStat : -1f, labelW, fontSize); y += rowH;
        StatBar(new Rect(x, y, w, rowH), Loc.T("stat_control"), ball.ControlStat, ControlColor, cmp ? compareTo.ControlStat : -1f, labelW, fontSize);
    }

    float ShopBalls(float cw)
    {
        float ry = 0f;

        // Equipped ball: preview, what it is, its reaction, weight and size.
        var eq = ProShop.Equipped;
        var card = new Rect(0f, ry, cw, 486f);
        GUI.Box(card, GUIContent.none, panel);
        GUI.Label(new Rect(18f, ry + 10f, cw - 36f, 30f), Loc.T("shop_equipped"), new GUIStyle(labelBold) { normal = { textColor = Gold } });
        float prev = Mathf.Min(150f, cw * 0.3f);
        GUI.DrawTexture(new Rect(18f, ry + 52f, prev, prev), ProShop.Preview(eq));
        float x = 36f + prev, w = cw - x - 18f;
        GUI.Label(new Rect(x, ry + 44f, w, 34f), eq.Name, new GUIStyle(labelBold) { fontSize = Fs(26) });
        GUI.Label(new Rect(x, ry + 78f, w, 24f), $"{eq.CoverName} · {eq.CoreName} · {eq.SurfaceName}", Small(Gold, TextAnchor.MiddleLeft, 15));
        GUI.Label(new Rect(x, ry + 102f, w, 44f), eq.BestFor, new GUIStyle(labelSmall) { fontSize = Fs(15), alignment = TextAnchor.UpperLeft });
        BallStats(eq, null, x, ry + 146f, w, 28f, 74f, 16);

        // Weight stepper and size: under the preview, full width.
        float sy = ry + 270f, bx = 18f + 110f;
        GUI.Label(new Rect(18f, sy, 110f, TapHSmall), Loc.T("ball_weight"), Small(Color.white, TextAnchor.MiddleLeft, 20));
        if (GUI.Button(new Rect(bx, sy, TapHSmall, TapHSmall), "−", Btn(Tone.Neutral, true))) GameSettings.BallWeightLb--;
        GUI.Label(new Rect(bx + TapHSmall, sy, 100f, TapHSmall), Loc.F("lb", GameSettings.BallWeightLb), new GUIStyle(labelBold) { fontSize = Fs(24), alignment = TextAnchor.MiddleCenter });
        if (GUI.Button(new Rect(bx + TapHSmall + 100f, sy, TapHSmall, TapHSmall), "+", Btn(Tone.Neutral, true))) GameSettings.BallWeightLb++;
        sy += TapHSmall + 10f;
        GUI.Label(new Rect(18f, sy, 110f, TapHSmall), Loc.T("ball_size"), Small(Color.white, TextAnchor.MiddleLeft, 20));
        for (int i = 0; i < 3; i++)
            if (GUI.Button(new Rect(bx + i * (TapHSmall + 12f), sy, TapHSmall, TapHSmall), ProShop.SizeNames[i], i == GameSettings.BallSize ? buttonOn : buttonSmall))
                GameSettings.BallSize = i;
        sy += TapHSmall + 8f;
        GUI.Label(new Rect(18f, sy, cw - 36f, 60f), Loc.T("shop_weight_hint"), new GUIStyle(labelSmall) { fontSize = Fs(15), alignment = TextAnchor.UpperLeft });
        ry += card.height + 14f;

        // What to look at when buying a ball.
        if (GUI.Button(new Rect(0f, ry, cw, TapHSmall), (showBallGuide ? "▾ " : "▸ ") + Loc.T("ball_guide_title"), Btn(Tone.Violet, true))) showBallGuide = !showBallGuide;
        ry += TapHSmall + 10f;
        if (showBallGuide)
        {
            var g = new GUIStyle(labelSmall) { fontSize = Fs(16), alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white } };
            string text = Loc.T("ball_guide");
            float gh = g.CalcHeight(new GUIContent(text), cw - 36f) + 24f;
            GUI.Box(new Rect(0f, ry, cw, gh), GUIContent.none, panelLight);
            GUI.Label(new Rect(18f, ry + 12f, cw - 36f, gh - 24f), text, g);
            ry += gh + 14f;
        }

        // Catalogue.
        GUI.Label(new Rect(0f, ry, cw, 32f), Loc.T("shop_catalogue"), new GUIStyle(labelBold) { fontSize = Fs(22) });
        GUI.Label(new Rect(0f, ry, cw, 32f), Loc.T("shop_compare_hint"), Small(DimText, TextAnchor.MiddleRight, 14));
        ry += 38f;
        int perRow = cw >= 600f ? 3 : 2;
        float gap = 12f, cardW = (cw - gap * (perRow - 1)) / perRow, cardH = 364f;
        for (int i = 0; i < ProShop.Balls.Length; i++)
        {
            var ball = ProShop.Balls[i];
            var r = new Rect((i % perRow) * (cardW + gap), ry + (i / perRow) * (cardH + gap), cardW, cardH);
            bool owned = ProShop.Owns(ball), equipped = ball == eq;
            GUI.Box(r, GUIContent.none, equipped ? panel : panelLight);
            if (equipped) RoundTint(new Rect(r.x + 6f, r.y + 6f, r.width - 12f, 4f), Good);
            float p = 100f;
            GUI.DrawTexture(new Rect(r.center.x - p / 2f, r.y + 12f, p, p), ProShop.Preview(ball));
            GUI.Label(new Rect(r.x + 8f, r.y + 114f, r.width - 16f, 30f), ball.Name, new GUIStyle(labelBold) { alignment = TextAnchor.MiddleCenter, fontSize = Fs(20) });
            GUI.Label(new Rect(r.x + 8f, r.y + 142f, r.width - 16f, 22f), $"{ball.CoverName} · {ball.SurfaceName}", Small(DimText, TextAnchor.MiddleCenter, 14));
            BallStats(ball, eq, r.x + 12f, r.y + 168f, r.width - 24f, 26f, 56f, 14);
            var b = new Rect(r.x + 12f, r.yMax - TapHSmall - 12f, r.width - 24f, TapHSmall);
            if (equipped)
            {
                GUI.enabled = false;
                GUI.Button(b, Loc.T("shop_in_use"), buttonSmall);
                GUI.enabled = true;
            }
            else if (owned)
            {
                if (GUI.Button(b, Loc.T("shop_equip"), Btn(Tone.Green, true))) { ProShop.Equip(ball); game.ApplyEquipment(); }
            }
            else
            {
                GUI.enabled = ProShop.Coins >= ball.price;
                if (GUI.Button(b, Loc.F("shop_price", ball.price.ToString("N0")), Btn(Tone.Gold, true)))
                    pendingBuy = new Purchase { name = ball.Name, price = ball.price, image = ProShop.Preview(ball), buy = () => ProShop.Buy(ball), equip = () => ProShop.Equip(ball) };
                GUI.enabled = true;
            }
        }
        ry += Mathf.CeilToInt(ProShop.Balls.Length / (float)perRow) * (cardH + gap);
        GUI.Label(new Rect(0f, ry, cw, 50f), Loc.T("shop_earn_hint"), Small(DimText, TextAnchor.MiddleCenter, 16)); ry += 60f;
        return ry;
    }

    float ShopCharacters(float cw)
    {
        float ry = 0f;
        GUI.Label(new Rect(0f, ry, cw, 30f), Loc.T("shop_char_hint"), Small(DimText, TextAnchor.MiddleCenter, 16)); ry += 40f;
        var eq = Outfits.Equipped;
        int perRow = cw >= 600f ? 3 : 2;
        float gap = 12f, cardW = (cw - gap * (perRow - 1)) / perRow, cardH = 330f;
        for (int i = 0; i < Outfits.All.Length; i++)
        {
            var o = Outfits.All[i];
            var r = new Rect((i % perRow) * (cardW + gap), ry + (i / perRow) * (cardH + gap), cardW, cardH);
            bool owned = Outfits.Owns(o), equipped = o == eq;
            GUI.Box(r, GUIContent.none, equipped ? panel : panelLight);
            if (equipped) RoundTint(new Rect(r.x + 6f, r.y + 6f, r.width - 12f, 4f), Good);
            float ph = 150f, pw = ph * Outfits.PreviewW / Outfits.PreviewH;
            GUI.DrawTexture(new Rect(r.center.x - pw / 2f, r.y + 12f, pw, ph), Outfits.Preview(o));
            GUI.Label(new Rect(r.x + 8f, r.y + 166f, r.width - 16f, 30f), o.Name, new GUIStyle(labelBold) { alignment = TextAnchor.MiddleCenter, fontSize = Fs(20) });
            GUI.Label(new Rect(r.x + 12f, r.y + 196f, r.width - 24f, 50f), o.Description, new GUIStyle(labelSmall) { fontSize = Fs(14), alignment = TextAnchor.UpperCenter });
            var b = new Rect(r.x + 12f, r.yMax - TapHSmall - 12f, r.width - 24f, TapHSmall);
            if (equipped)
            {
                GUI.enabled = false;
                GUI.Button(b, Loc.T("shop_in_use"), buttonSmall);
                GUI.enabled = true;
            }
            else if (owned)
            {
                if (GUI.Button(b, Loc.T("shop_wear"), Btn(Tone.Green, true))) { Outfits.Equip(o); game.ApplyEquipment(); }
            }
            else
            {
                GUI.enabled = ProShop.Coins >= o.price;
                if (GUI.Button(b, Loc.F("shop_price", o.price.ToString("N0")), Btn(Tone.Gold, true)))
                    pendingBuy = new Purchase { name = o.Name, price = o.price, image = Outfits.Preview(o), buy = () => Outfits.Buy(o), equip = () => Outfits.Equip(o) };
                GUI.enabled = true;
            }
        }
        ry += Mathf.CeilToInt(Outfits.All.Length / (float)perRow) * (cardH + gap);
        GUI.Label(new Rect(0f, ry, cw, 50f), Loc.T("shop_earn_hint"), Small(DimText, TextAnchor.MiddleCenter, 16)); ry += 60f;
        return ry;
    }

    void DrawBuyConfirm()
    {
        DimBackground(0.6f);
        var col = Column(H * 0.24f, 0f, 460f);
        float y = col.y;
        GUI.Box(new Rect(col.x - 24f, y - 20f, col.width + 48f, 350f), GUIContent.none, panel);
        float ih = 140f, iw = ih * pendingBuy.image.width / pendingBuy.image.height;
        GUI.DrawTexture(new Rect(col.center.x - iw / 2f, y, iw, ih), pendingBuy.image); y += ih + 8f;
        GUI.Label(new Rect(col.x, y, col.width, 40f), Loc.F("shop_confirm", pendingBuy.name, pendingBuy.price.ToString("N0")), labelCenter); y += 64f;
        float half = col.width / 2f - 6f;
        if (GUI.Button(new Rect(col.x, y, half, 70f), Loc.T("shop_buy"), Btn(Tone.Gold)))
        {
            if (pendingBuy.buy()) { pendingBuy.equip(); game.ApplyEquipment(); }
            pendingBuy = null;
        }
        else if (GUI.Button(new Rect(col.x + half + 12f, y, half, 70f), Loc.T("cancel"), buttonSmall)) pendingBuy = null;
    }

    // ---- Venues ----

    void VenuePicker()
    {
        float y = Header(Loc.T("venues"));
        var col = Column(y, 0f, 760f);
        var view = new Rect(col.x, y, col.width, H - y - 10f - safeBottom);
        BeginScroll(view, venueHeight);
        float cw = col.width - 20f, ry = 0f;

        // Hero: the current venue, big, with its name over a soft shade.
        var cur = Venues.All[GameSettings.Venue];
        float heroH = Mathf.Min(300f, cw * 0.56f);
        var hero = new Rect(0f, ry, cw, heroH);
        VenueImage(hero, cur, 18f);
        Shade(new Rect(hero.x, hero.yMax - heroH * 0.55f, hero.width, heroH * 0.55f), 18f);
        RoundBorder(hero, Neon(cur.neon[0]), 2.5f, 18f);
        GUI.backgroundColor = new Color(0.1f, 0.6f, 0.3f, 0.95f);
        var badge = new Rect(hero.x + 16f, hero.y + 16f, 150f, 36f);
        GUI.Box(badge, "✓ " + Loc.T("venue_current"), Fit(badge, "✓ " + Loc.T("venue_current"), chip));
        GUI.backgroundColor = Color.white;
        FitLabel(new Rect(hero.x + 22f, hero.yMax - 104f, hero.width - 44f, 48f), cur.Name, new GUIStyle(title) { alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } });
        FitLabel(new Rect(hero.x + 22f, hero.yMax - 58f, hero.width - 44f, 48f), cur.Description, new GUIStyle(labelSmall) { alignment = TextAnchor.UpperLeft, normal = { textColor = new Color(0.9f, 0.9f, 1f) } });
        ry += heroH + 24f;

        GUI.Label(new Rect(4f, ry, cw, 34f), Loc.T("venue_all"), new GUIStyle(labelBold) { fontSize = Fs(22), normal = { textColor = Gold } });
        ry += 42f;

        int perRow = cw >= 520f ? 2 : 1;
        float gap = 16f, cardW = (cw - gap * (perRow - 1)) / perRow;
        float imgH = cardW * 0.56f, cardH = imgH + 196f;
        for (int i = 0; i < Venues.All.Length; i++)
        {
            var v = Venues.All[i];
            var r = new Rect((i % perRow) * (cardW + gap), ry + (i / perRow) * (cardH + gap), cardW, cardH);
            bool current = i == GameSettings.Venue;
            Color accent = Neon(v.neon[0]);
            GUI.Box(r, GUIContent.none, panelLight);

            var img = new Rect(r.x + 8f, r.y + 8f, r.width - 16f, imgH);
            VenueImage(img, v, 12f);
            if (current) RoundBorder(img, accent, 3f, 12f);

            float x = r.x + 18f, w = r.width - 36f, cy = img.yMax + 10f;
            FitLabel(new Rect(x, cy, w, 34f), v.Name, new GUIStyle(labelBold) { fontSize = Fs(23) }); cy += 36f;
            FitLabel(new Rect(x, cy, w, 48f), v.Description, new GUIStyle(labelSmall) { fontSize = Fs(15), alignment = TextAnchor.UpperLeft }); cy += 52f;
            // Its colours: the four neon tubes and the lane.
            for (int k = 0; k < 4; k++) Dot(new Vector2(x + 9f + k * 26f, cy + 10f), 16f, Neon(v.neon[k]));
            RoundTint(new Rect(x + 4f * 26f + 4f, cy + 3f, 34f, 14f), v.lane + new Color(0.05f, 0.05f, 0.07f));
            cy += 30f;

            var b = new Rect(x, r.yMax - TapHSmall - 12f, w, TapHSmall);
            if (current)
            {
                GUI.enabled = false;
                GUI.Button(b, "✓ " + Loc.T("venue_current"), Btn(Tone.Green, true));
                GUI.enabled = true;
            }
            else if (GUI.Button(b, Loc.T("venue_pick"), Btn(Tone.Violet, true)))
            {
                GameSettings.Venue = i;
                game.ApplyVenue();
            }
        }
        ry += Mathf.CeilToInt(Venues.All.Length / (float)perRow) * (cardH + gap) + 10f;
        venueHeight = ry;
        GUI.EndScrollView();
    }

    // A venue picture with rounded corners (a soft placeholder while it is being painted).
    void VenueImage(Rect r, Venue v, float radius)
    {
        if (!Painting) return;
        var tex = VenueArt.Preview(v);
        if (tex == null)
        {
            GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0f, Color.Lerp(v.fog, v.dark, 0.5f) + new Color(0.05f, 0.05f, 0.08f), 0f, radius);
            return;
        }
        GUI.DrawTexture(r, tex, ScaleMode.ScaleAndCrop, true, 0f, Color.white, 0f, radius);
    }

    // A rounded outline.
    void RoundBorder(Rect r, Color c, float width, float radius)
    {
        if (!Painting) return;
        GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0f, c, width, radius);
    }

    // Darkens towards the bottom so text over a picture stays readable.
    void Shade(Rect r, float radius)
    {
        if (!Painting) return;
        if (shadeTex == null)
        {
            shadeTex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 32; i++) shadeTex.SetPixel(0, i, new Color(0.02f, 0.01f, 0.06f, Mathf.SmoothStep(0.85f, 0f, i / 31f)));
            shadeTex.Apply();
        }
        GUI.DrawTexture(r, shadeTex, ScaleMode.StretchToFill, true, 0f, Color.white, 0f, radius);
    }
    Texture2D shadeTex;

    // HDR neon colour → something displayable.
    static Color Neon(Color hdr)
    {
        float m = Mathf.Max(1f, Mathf.Max(hdr.r, Mathf.Max(hdr.g, hdr.b)));
        return new Color(hdr.r / m, hdr.g / m, hdr.b / m, 1f);
    }

    // ---- Calibration: intro (menu), the bar on the lane, and the report ----

    static Color KindColor(CalibrationSession.Kind k) => k switch
    {
        CalibrationSession.Kind.Left => Cyan,
        CalibrationSession.Kind.Right => Pink,
        _ => Color.white,
    };

    static string KindShort(CalibrationSession.Kind k) => k switch
    {
        CalibrationSession.Kind.Left => Loc.T("shape_left"),
        CalibrationSession.Kind.Right => Loc.T("shape_right"),
        _ => Loc.T("shape_straight"),
    };

    float DrawCalibrationBar()
    {
        var c = game.Calibration;
        float y0 = safeTop;
        float h = 150f;
        GUI.Box(new Rect(-4f, -30f, W + 8f, h + 30f + y0), GUIContent.none, panel);
        int step = Mathf.Min(c.Step + 1, CalibrationSession.Steps.Length);
        FitLabel(new Rect(16f + safeLeft, y0 + 6f, W - 120f - safeLeft, 34f), Loc.F("calib_bar_named", step, CalibrationSession.Steps.Length, GameSettings.ProfileName(GameSettings.ActiveProfile)), new GUIStyle(labelBold) { fontSize = Fs(22), normal = { textColor = Gold } });
        var kind = c.CurrentKind;
        string instr = kind switch
        {
            CalibrationSession.Kind.Left => Loc.T("calib_do_left"),
            CalibrationSession.Kind.Right => Loc.T("calib_do_right"),
            _ => Loc.T("calib_do_straight"),
        };
        FitLabel(new Rect(16f + safeLeft, y0 + 40f, W - 120f - safeLeft, 56f), c.Done ? Loc.T("calib_done_short") : instr,
            new GUIStyle(label) { fontSize = Fs(21), normal = { textColor = KindColor(kind) } });
        // Progress pips.
        float pw = 46f, px = 16f;
        for (int i = 0; i < CalibrationSession.Steps.Length; i++)
        {
            var k = CalibrationSession.Steps[i];
            Color col = i < c.Step ? Good : i == c.Step ? Color.Lerp(KindColor(k), Color.white, Mathf.PingPong(Time.unscaledTime * 2f, 1f)) : new Color(1f, 1f, 1f, 0.2f);
            RoundTint(new Rect(px + i * (pw + 8f), y0 + 104f, pw, 10f), col);
            GUI.Label(new Rect(px + i * (pw + 8f), y0 + 116f, pw, 22f), KindShort(k), Small(DimText, TextAnchor.MiddleCenter, 13));
        }
        return h + y0;
    }

    void DrawCalibrationReport()
    {
        var c = game.Calibration;
        var col = Column(Mathf.Max(30f, H * 0.08f) + safeTop, 0f, 640f);
        float y = col.y;
        float h = Mathf.Min(H - y - 20f - safeBottom, 660f);
        GUI.Box(new Rect(col.x - 18f, y - 14f, col.width + 36f, h), GUIContent.none, panel);
        GUI.Label(new Rect(col.x, y, col.width, 52f), Loc.T("calib_report_title"), title); y += 58f;

        void Row(string name, string value, Color color)
        {
            GUI.Label(new Rect(col.x, y, col.width * 0.5f, 30f), name, Small(DimText, TextAnchor.MiddleLeft, 18));
            GUI.Label(new Rect(col.x + col.width * 0.45f, y, col.width * 0.55f, 30f), value, Small(color, TextAnchor.MiddleRight, 19));
            y += 31f;
        }
        Row(Loc.T("cr_speed"), $"{c.SwingSpeed * Mathf.Rad2Deg:0} °/s", Color.white);
        Row(Loc.T("cr_amplitude"), Loc.F("cr_amplitude_value", c.Backswing, c.Amplitude), Color.white);
        Row(Loc.T("cr_accel"), $"{c.Accel * Mathf.Rad2Deg:0} °/s²", Color.white);
        Row(Loc.T("cr_consistency"), $"{c.Consistency * 100f:0}%", c.Consistency >= 0.8f ? Good : Warn);
        Row(Loc.T("cr_aim"), Mathf.Abs(c.AimTrim) < 0.05f ? Loc.T("trim_none") : $"{c.AimTrim:+0.0;-0.0}° {(c.AimTrim > 0f ? Loc.T("trim_left") : Loc.T("trim_right"))}", Color.white);
        Row(Loc.T("cr_left"), $"{c.LeftTwist:+0.0;-0.0;0.0} rad/s", Cyan);
        Row(Loc.T("cr_right"), $"{c.RightTwist:+0.0;-0.0;0.0} rad/s", Pink);
        GUI.Label(new Rect(col.x, y, col.width, 50f), c.SpinLearned ? Loc.T("cr_spin_ok") : Loc.T("cr_spin_unclear"),
            new GUIStyle(labelSmall) { fontSize = Fs(17), normal = { textColor = c.SpinLearned ? Good : Warn } });
        y += 54f;

        // Each throw's swing speed and arc, coloured by what was asked for.
        var chart = new Rect(col.x, y, col.width, Mathf.Max(80f, col.y + h - y - 120f));
        var res = c.Results;
        if (res.Count > 0 && chart.height > 60f)
        {
            float max = res.Max(r => r.swing.peakRate) * 1.15f;
            float slot = chart.width / res.Count, bw = slot * 0.55f;
            Tint(new Rect(chart.x, chart.yMax - 22f, chart.width, 1f), ChartAxis);
            for (int i = 0; i < res.Count; i++)
            {
                float bh = res[i].swing.peakRate / max * (chart.height - 44f);
                var bar = new Rect(chart.x + i * slot + (slot - bw) / 2f, chart.yMax - 22f - bh, bw, bh);
                RoundTint(bar, KindColor(res[i].kind));
                GUI.Label(new Rect(bar.x - 10f, bar.y - 20f, bw + 20f, 20f), $"{res[i].swing.forwardDeg:0}°", Small(Color.white, TextAnchor.MiddleCenter, 13));
                GUI.Label(new Rect(bar.x - 10f, chart.yMax - 20f, bw + 20f, 20f), KindShort(res[i].kind), Small(DimText, TextAnchor.MiddleCenter, 13));
            }
        }

        var buttons = new Rect(col.x, col.y + h - 100f, col.width, 72f);
        float half = buttons.width / 2f - 6f;
        if (Button(new Rect(buttons.x, buttons.y, half, 72f), Loc.T("calib_finish"), Btn(Tone.Green))) { game.BackToMenu(); Go(Screen.Calibrate); }
        if (Button(new Rect(buttons.x + half + 12f, buttons.y, half, 72f), Loc.T("calib_again"), Btn(Tone.Cyan))) game.StartCalibration();
    }
}
