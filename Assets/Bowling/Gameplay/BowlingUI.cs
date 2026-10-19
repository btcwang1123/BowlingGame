using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Every screen: home menu, mode pickers, challenges, stats, settings, calibration, pro shop, venues,
// safety notice, the first-run tutorial, the in-game HUD, pause and results. IMGUI keeps it all in
// code with no scene setup; layout is on a virtual canvas at least 720 units on its short side, so
// it fits portrait and landscape, inset by the device safe area so nothing hides under a notch.
// Drawing helpers and charts live in BowlingUI.Draw.cs; shop, venue and calibration screens in
// BowlingUI.Screens.cs; settings drafts, sound/music pickers and the tap-to-continue prompt in
// BowlingUI.Settings.cs.
public partial class BowlingUI : MonoBehaviour
{
    enum Screen { Home, Players, PlayerSetup, Practice, Challenges, ChallengeIntro, Stats, Settings, Calibrate, Safety, Tutorial, Shop, Venues }

    // Button colours: each kind of action gets its own, so menus are easy to scan.
    enum Tone { Primary, Cyan, Gold, Green, Violet, Neutral, Danger }

    BowlingGame game;
    Screen screen = Screen.Home;
    Screen afterSafety;
    System.Action afterSafetyAction;
    int pickedChallenge;
    Vector2 scroll;

    // Settings are edited on a draft: the snapshot taken on entry, so Back can offer to save or
    // roll back. leaveAction is where the player was heading when the "save changes?" prompt opened.
    string settingsSnapshot;
    System.Action leaveAction;
    float savedToastTime = -10f;

    // First-run interactive tutorial: steps 0-2 are menu panels, step 3 guides a real throw, step 4
    // is the "you've got it" finish. Driven independently of `screen` so it can sit over the HUD too.
    bool tutorialActive;
    int tutorialStep;

    // Rects of HUD buttons drawn last frame (screen pixels, GUI coordinates), so a tap on them isn't a throw.
    readonly List<Rect> hudButtons = new();
    readonly List<Rect> hudButtonsDrawing = new();

    float scale;
    float W, H;
    float safeTop, safeBottom, safeLeft, safeRight;
    GUIStyle label, labelSmall, labelCenter, labelBold, title, logo, big, glow, button, buttonSmall, buttonOn, panel, panelLight, cell, cellCurrent, cellName;
    GUIStyle sliderTrack, sliderThumb, chip;
    Texture2D white, circle;
    readonly Dictionary<(Tone, bool), GUIStyle> buttonStyles = new();

    static readonly Color Cyan = new(0.3f, 1f, 1f), Pink = new(1f, 0.4f, 0.9f), Gold = new(1f, 0.85f, 0.3f), DimText = new(0.65f, 0.7f, 0.85f),
        Good = new(0.4f, 1f, 0.55f), Warn = new(1f, 0.6f, 0.25f), Bad = new(1f, 0.35f, 0.35f);

    // Touch-friendly sizes (virtual units): nothing tappable is shorter than this.
    const float TapH = 64f, TapHSmall = 60f;

    void Awake()
    {
        game = GetComponent<BowlingGame>();
        game.PointerOverUI = p => hudButtons.Any(r => r.Contains(new Vector2(p.x, UnityEngine.Screen.height - p.y)));
        // First launch: start the player in the interactive tutorial.
        if (!GameSettings.TutorialDone)
        {
            tutorialActive = true;
            tutorialStep = 0;
            screen = Screen.Tutorial;
        }
    }

    // For BowlingAutoplay screenshots: open a menu screen by name ("Home", "Settings", …).
    public void ShowScreen(string name)
    {
        if (System.Enum.TryParse(name, out Screen s)) Go(s);
    }

    // ---- Styles ----

    void MakeStyles()
    {
        if (label != null) return;
        // Bundled Noto Sans TC so Chinese renders on every platform — OS fonts don't resolve reliably
        // on device, which is why text used to go blank on phones. Medium for body, Bold for emphasis.
        var bodyFont = Resources.Load<Font>("Fonts/NotoSansTC-Medium");
        var boldFont = Resources.Load<Font>("Fonts/NotoSansTC-Bold");
        if (bodyFont == null)
            bodyFont = Font.CreateDynamicFontFromOSFont(new[]
            {
                "PingFang TC", "Heiti TC", "Microsoft JhengHei UI", "Microsoft JhengHei",
                "Noto Sans CJK TC", "Noto Sans TC", "Noto Sans CJK", "Droid Sans Fallback", "Arial",
            }, 24);
        if (boldFont == null) boldFont = bodyFont;
        white = Solid(Color.white);
        circle = Rounded(Color.white, Color.white, Color.white, 32, 16, 0f);

        GUIStyle Text(Font f, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = anchor, fontStyle = FontStyle.Normal, wordWrap = true, richText = true };
            s.normal.textColor = color;
            scalable.Add((s, size));
            return s;
        }
        label = Text(bodyFont, 24, Color.white);
        labelSmall = Text(bodyFont, 18, DimText);
        labelCenter = Text(bodyFont, 24, Color.white, TextAnchor.MiddleCenter);
        labelBold = Text(boldFont, 20, Color.white);
        title = Text(boldFont, 44, Pink, TextAnchor.MiddleCenter);
        logo = Text(boldFont, 74, Cyan, TextAnchor.MiddleCenter);
        big = Text(boldFont, 56, Color.white, TextAnchor.MiddleCenter);
        glow = Text(boldFont, 56, new Color(1f, 0.3f, 0.9f, 0.35f), TextAnchor.MiddleCenter);

        // Rounded, two-tone buttons: lighter on top, a bright rim, darker when pressed.
        (Color top, Color bottom, Color rim, Color text) ToneColors(Tone t) => t switch
        {
            Tone.Primary => (new(0.98f, 0.3f, 0.78f), new(0.62f, 0.08f, 0.55f), new(1f, 0.65f, 0.95f), Color.white),
            Tone.Cyan => (new(0.15f, 0.72f, 0.85f), new(0.03f, 0.4f, 0.58f), new(0.55f, 1f, 1f), Color.white),
            Tone.Gold => (new(1f, 0.82f, 0.3f), new(0.85f, 0.5f, 0.06f), new(1f, 0.95f, 0.65f), new(0.22f, 0.1f, 0f)),
            Tone.Green => (new(0.3f, 0.82f, 0.45f), new(0.07f, 0.5f, 0.26f), new(0.6f, 1f, 0.7f), Color.white),
            Tone.Violet => (new(0.55f, 0.38f, 1f), new(0.3f, 0.14f, 0.7f), new(0.78f, 0.68f, 1f), Color.white),
            Tone.Danger => (new(0.95f, 0.35f, 0.35f), new(0.62f, 0.1f, 0.15f), new(1f, 0.7f, 0.7f), Color.white),
            _ => (new(0.26f, 0.22f, 0.4f), new(0.14f, 0.11f, 0.25f), new(0.5f, 0.45f, 0.72f), Color.white),
        };
        foreach (Tone tone in System.Enum.GetValues(typeof(Tone)))
            foreach (bool small in new[] { false, true })
            {
                var (top, bottom, rim, text) = ToneColors(tone);
                var s = new GUIStyle(GUI.skin.button)
                {
                    font = boldFont, fontSize = small ? 22 : 27, fontStyle = FontStyle.Normal, wordWrap = true, richText = true,
                    border = new RectOffset(24, 24, 24, 24), padding = new RectOffset(16, 16, 8, 8), alignment = TextAnchor.MiddleCenter,
                };
                s.normal.background = Rounded(top, bottom, rim, 64, 22, 2.5f);
                s.hover.background = Rounded(Color.Lerp(top, Color.white, 0.12f), Color.Lerp(bottom, Color.white, 0.08f), rim, 64, 22, 2.5f);
                s.active.background = Rounded(Color.Lerp(bottom, Color.black, 0.2f), Color.Lerp(top, Color.black, 0.25f), rim, 64, 22, 2.5f);
                s.normal.textColor = s.hover.textColor = s.active.textColor = text;
                buttonStyles[(tone, small)] = s;
                scalable.Add((s, s.fontSize));
            }
        button = Btn(Tone.Primary);
        buttonSmall = Btn(Tone.Neutral, true);
        buttonOn = Btn(Tone.Cyan, true);

        panel = new GUIStyle(GUI.skin.box) { border = new RectOffset(22, 22, 22, 22) };
        panel.normal.background = Rounded(new Color(0.07f, 0.05f, 0.16f, 0.94f), new Color(0.03f, 0.02f, 0.08f, 0.94f), new Color(0.38f, 0.28f, 0.65f, 0.9f), 64, 20, 2f);
        panelLight = new GUIStyle(panel);
        panelLight.normal.background = Rounded(new Color(0.13f, 0.1f, 0.26f, 0.92f), new Color(0.09f, 0.07f, 0.19f, 0.92f), new Color(0.32f, 0.26f, 0.55f, 0.8f), 64, 16, 1.5f);
        chip = new GUIStyle(GUI.skin.box) { font = boldFont, fontSize = 18, alignment = TextAnchor.MiddleCenter, border = new RectOffset(14, 14, 14, 14), padding = new RectOffset(10, 10, 2, 2) };
        chip.normal.background = Rounded(Color.white, Color.white, Color.white, 32, 14, 0f);
        chip.normal.textColor = Color.white;
        scalable.Add((chip, 18));

        sliderTrack = new GUIStyle { fixedHeight = 0f };
        sliderThumb = new GUIStyle { fixedWidth = 44f, fixedHeight = 44f };
        sliderThumb.normal.background = sliderThumb.hover.background = sliderThumb.active.background =
            Rounded(new Color(1f, 0.6f, 0.95f), new Color(0.75f, 0.2f, 0.7f), Color.white, 44, 22, 2.5f);

        cell = new GUIStyle(GUI.skin.box) { font = boldFont, fontSize = 16, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Normal, border = new RectOffset(8, 8, 8, 8) };
        cell.normal.background = Rounded(new Color(0.05f, 0.03f, 0.12f, 0.8f), new Color(0.03f, 0.02f, 0.08f, 0.8f), new Color(0.25f, 0.2f, 0.45f, 0.8f), 24, 6, 1f);
        cell.normal.textColor = Cyan;
        cellCurrent = new GUIStyle(cell);
        cellCurrent.normal.background = Rounded(new Color(0.45f, 0.06f, 0.4f, 0.9f), new Color(0.28f, 0.03f, 0.24f, 0.9f), new Color(1f, 0.5f, 0.9f, 0.9f), 24, 6, 1.5f);
        cellCurrent.normal.textColor = Color.white;
        cellName = new GUIStyle(cell) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
        scalable.Add((cell, 16));
        scalable.Add((cellCurrent, 16));
        scalable.Add((cellName, 17));
    }

    // ---- Text size (Settings → text size) ----

    // Every shared style with its base size, rescaled whenever the text-size setting changes.
    readonly List<(GUIStyle style, int size)> scalable = new();
    float appliedFontScale = -1f;
    readonly Dictionary<string, int> fitCache = new();

    // A font size scaled by the text-size setting.
    static int Fs(int size) => Mathf.RoundToInt(size * GameSettings.FontScale);

    void ApplyFontScale()
    {
        float k = GameSettings.FontScale;
        if (Mathf.Approximately(k, appliedFontScale)) return;
        appliedFontScale = k;
        foreach (var (style, size) in scalable) style.fontSize = Mathf.RoundToInt(size * k);
        fitCache.Clear();
    }

    // The style shrunk (down to minScale of its size) until the text fits the rect, so long lines,
    // narrow phones and a large text-size setting never cut words off.
    GUIStyle Fit(Rect r, string text, GUIStyle style, float minScale = 0.5f)
    {
        if (string.IsNullOrEmpty(text) || r.width <= 0f || r.height <= 0f) return style;
        int size = style.fontSize > 0 ? style.fontSize : 24;
        string key = $"{text}|{size}|{(int)r.width}|{(int)r.height}|{style.wordWrap}|{style.padding.horizontal}";
        if (!fitCache.TryGetValue(key, out int fitted))
        {
            var content = new GUIContent(text);
            var probe = new GUIStyle(style);
            int min = Mathf.Max(9, Mathf.RoundToInt(size * minScale));
            fitted = size;
            while (fitted > min)
            {
                probe.fontSize = fitted;
                Vector2 one = probe.CalcSize(content);
                bool fits = probe.wordWrap ? probe.CalcHeight(content, r.width) <= r.height + 1f && (one.x <= r.width || HasBreaks(text))
                    : one.x <= r.width + 1f && one.y <= r.height + 1f;
                if (fits) break;
                fitted--;
            }
            if (fitCache.Count > 512) fitCache.Clear();
            fitCache[key] = fitted;
        }
        return fitted == size ? style : new GUIStyle(style) { fontSize = fitted };
    }

    // Word-wrapped CJK breaks anywhere; a single long Latin word must fit on its line by itself.
    static bool HasBreaks(string text) => text.IndexOf(' ') >= 0 || text.IndexOf('\n') >= 0 || text.Length > 0 && text[0] > 0x2E80;

    void FitLabel(Rect r, string text, GUIStyle style, float minScale = 0.5f) => GUI.Label(r, text, Fit(r, text, style, minScale));

    GUIStyle Btn(Tone tone, bool small = false) => buttonStyles[(tone, small)];

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    // A rounded rectangle (9-sliced by the styles), vertical gradient, anti-aliased rim.
    static Texture2D Rounded(Color top, Color bottom, Color rim, int size, int radius, float rimWidth)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[size * size];
        float c = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - c) - (c - radius), dy = Mathf.Abs(y + 0.5f - c) - (c - radius);
                float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
                float d = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius; // signed distance, < 0 inside
                float v = (float)y / (size - 1);
                Color fill = Color.Lerp(bottom, top, v);
                if (v > 0.55f) fill = Color.Lerp(fill, Color.white, 0.05f); // soft gloss on the top half
                Color col = rimWidth > 0f ? Color.Lerp(fill, rim, Mathf.Clamp01(d + rimWidth + 0.5f)) : fill;
                col.a *= Mathf.Clamp01(0.5f - d);
                px[y * size + x] = col;
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    bool Button(Rect r, string text, GUIStyle style = null)
    {
        hudButtonsDrawing.Add(new Rect(r.x * scale, r.y * scale, r.width * scale, r.height * scale));
        return GUI.Button(r, text, Fit(r, text, style ?? button, 0.6f));
    }

    // Centre column used by the menus.
    Rect Column(float y, float height, float width = 520f)
    {
        float cw = Mathf.Min(width, W - 32f - safeLeft - safeRight);
        return new Rect((W - cw) / 2f, y, cw, height);
    }

    // A big touch slider: rounded track with a filled part and a 44-unit thumb.
    float BigSlider(Rect r, float value, float min, float max)
    {
        var track = new Rect(r.x + 4f, r.center.y - 6f, r.width - 8f, 12f);
        Tint(track, new Color(0.2f, 0.16f, 0.35f));
        float t = Mathf.InverseLerp(min, max, value);
        Tint(new Rect(track.x, track.y, track.width * t, track.height), Pink);
        return GUI.HorizontalSlider(r, value, min, max, sliderTrack, sliderThumb);
    }

    // ---- Frame ----

    void OnGUI()
    {
        MakeStyles();
        ApplyFontScale();
        scale = Mathf.Min(UnityEngine.Screen.width, UnityEngine.Screen.height) / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        W = UnityEngine.Screen.width / scale;
        H = UnityEngine.Screen.height / scale;
        var sa = UnityEngine.Screen.safeArea;
        safeLeft = sa.xMin / scale;
        safeRight = (UnityEngine.Screen.width - sa.xMax) / scale;
        safeTop = (UnityEngine.Screen.height - sa.yMax) / scale;
        safeBottom = sa.yMin / scale;
        // A drag just turned into a scroll: the button it started on must not click when it lifts.
        if (stealPress) { stealPress = false; GUIUtility.hotControl = 0; }
        if (Event.current.type == EventType.Layout) { hudButtons.Clear(); hudButtons.AddRange(hudButtonsDrawing); hudButtonsDrawing.Clear(); }

        if (game.CurrentPhase == BowlingGame.Phase.Menu)
        {
            DimBackground(screen == Screen.Home ? 0.35f : 0.6f); // keep text readable over the bright neon
            // A dialog is open: the screen underneath is drawn but gets no clicks.
            bool modal = ModalOpen;
            if (!modal || !(Event.current.isMouse || Event.current.isScrollWheel)) DrawMenus();
            if (pendingBuy != null) DrawBuyConfirm();
            if (leaveAction != null) DrawLeaveSettingsPrompt();
            if (confirm != null) DrawConfirm();
        }
        else
        {
            // A dialog over the game: what's underneath is drawn but gets no taps.
            if (confirm == null || !(Event.current.isMouse || Event.current.isScrollWheel))
            {
                DrawHud();
                if (game.CurrentPhase == BowlingGame.Phase.Finished) DrawFinished();
                else if (game.Paused) DrawPause();
                if (tutorialActive) DrawTutorialInGame();
            }
            if (confirm != null) DrawConfirm();
        }
    }

    // ---- Menus ----

    void DrawMenus()
    {
        switch (screen)
        {
            case Screen.Home: Home(); break;
            case Screen.Players: Players(); break;
            case Screen.PlayerSetup: PlayerSetup(); break;
            case Screen.Practice: Practice(); break;
            case Screen.Challenges: Challenges(); break;
            case Screen.ChallengeIntro: ChallengeIntro(); break;
            case Screen.Stats: Stats(); break;
            case Screen.Settings:
                if (settingsSnapshot == null) BeginSettingsEdit();
                Settings();
                break;
            case Screen.Calibrate: Calibrate(); break;
            case Screen.Safety: Safety(); break;
            case Screen.Tutorial: Tutorial(); break;
            case Screen.Shop: Shop(); break;
            case Screen.Venues: VenuePicker(); break;
        }
    }

    void Home()
    {
        CoinChip(new Rect(W - 190f - safeRight, 16f + safeTop, 174f, 48f));
        float y = Mathf.Max(24f, H * 0.08f) + safeTop;
        FitLabel(Column(y, 96f, 700f), "STRIKE SWING", logo); y += 96f;
        GUI.Label(Column(y, 36f), Loc.T("tagline"), labelCenter); y += Mathf.Max(40f, H * 0.05f);

        if (BowlingGame.HasSavedMatch)
        {
            if (Button(Column(y, 62f), Loc.F("continue_match", BowlingGame.SavedMatch.savedAt), Btn(Tone.Green))) ResumeSaved();
            y += 70f;
        }
        if (Button(Column(y, 70f), Loc.T("play"), Btn(Tone.Primary))) Go(Screen.Players); y += 80f;
        if (Button(Column(y, 66f), Loc.T("practice"), Btn(Tone.Cyan))) Go(Screen.Practice); y += 76f;
        if (Button(Column(y, 66f), Loc.F("challenges_stars", PlayerStats.TotalStars, ChallengeLevels.All.Count * 3), Btn(Tone.Gold))) Go(Screen.Challenges); y += 76f;

        var row = Column(y, TapH);
        float hw = row.width / 2f - 6f;
        if (Button(new Rect(row.x, y, hw, TapH), Loc.T("shop"), Btn(Tone.Green, true))) Go(Screen.Shop);
        if (Button(new Rect(row.x + hw + 12f, y, hw, TapH), Loc.T("venues"), Btn(Tone.Violet, true))) Go(Screen.Venues);
        y += TapH + 10f;
        if (Button(new Rect(row.x, y, hw, TapH), Loc.T("stats"), buttonSmall)) Go(Screen.Stats);
        if (Button(new Rect(row.x + hw + 12f, y, hw, TapH), Loc.T("settings"), buttonSmall)) Go(Screen.Settings);
        y += TapH + 10f;

        string control = GameSettings.Control == ControlMode.Motion
            ? (MotionInput.Available ? Loc.T("control_motion") : Loc.T("control_motion_unavailable"))
            : Loc.T("control_swipe");
        GUI.Label(Column(y, 30f), $"{Loc.T("controls")}: {control} · {Venues.All[GameSettings.Venue].Name}", new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
    }

    void Practice()
    {
        float y = Header(Loc.T("practice"));
        GUI.Label(Column(y, 36f), Loc.T("practice_pick"), labelCenter); y += 50f;
        var col = Column(y, 0f, 600f);
        float bw = (col.width - 12f) / 2f;
        for (int i = 0; i < BowlingGame.PracticeLeaves.Length; i++)
        {
            int leave = i;
            var r = new Rect(col.x + (i % 2) * (bw + 12f), y + (i / 2) * 74f, bw, TapH);
            var tone = i == 0 ? Tone.Primary : i == BowlingGame.PracticeLeaves.Length - 1 ? Tone.Gold : Tone.Cyan;
            if (Button(r, LeaveName(i), Btn(tone, true))) WithSafety(() => game.StartPractice(leave));
        }
    }

    public static string LeaveName(int i)
    {
        int[] leave = BowlingGame.PracticeLeaves[i];
        if (leave.Length == 10) return Loc.T("leave_full");
        if (leave.Length == 0) return Loc.T("leave_random");
        return string.Join("-", leave);
    }

    void Challenges()
    {
        float y = Header(Loc.F("challenges_stars", PlayerStats.TotalStars, ChallengeLevels.All.Count * 3));
        var col = Column(y, 0f, 680f);
        int perRow = W >= 600f ? 3 : 2;
        float bw = (col.width - (perRow - 1) * 10f) / perRow, bh = 96f;
        var view = new Rect(col.x, y, col.width, H - y - 16f - safeBottom);
        int rows = Mathf.CeilToInt(ChallengeLevels.All.Count / (float)perRow);
        BeginScroll(view, rows * (bh + 10f));
        for (int i = 0; i < ChallengeLevels.All.Count; i++)
        {
            var r = new Rect((i % perRow) * (bw + 10f), (i / perRow) * (bh + 10f), bw, bh);
            bool open = PlayerStats.Unlocked(i);
            int stars = PlayerStats.Stars(i);
            string text = open ? $"{i + 1}. {ChallengeLevels.All[i].LocalizedName()}\n<color=#FFD94D>{Stars(stars)}</color>" : $"{i + 1}.\n{Loc.T("locked")}";
            GUI.enabled = open;
            if (GUI.Button(r, text, Btn(!open ? Tone.Neutral : stars == 3 ? Tone.Green : stars > 0 ? Tone.Cyan : Tone.Violet, true)))
            { pickedChallenge = i; Go(Screen.ChallengeIntro); }
            GUI.enabled = true;
        }
        GUI.EndScrollView();
    }

    void ChallengeIntro()
    {
        var level = ChallengeLevels.All[pickedChallenge];
        float y = Header($"{pickedChallenge + 1}. {level.LocalizedName()}");
        var col = Column(y, 0f);
        GUI.Box(new Rect(col.x - 16f, y - 10f, col.width + 32f, 240f), GUIContent.none, panel);
        GUI.Label(new Rect(col.x, y, col.width, 70f), level.LocalizedDescription(), labelCenter); y += 76f;
        GUI.Label(new Rect(col.x, y, col.width, 32f), Loc.F("balls_allowed", level.balls), new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter }); y += 34f;
        GUI.Label(new Rect(col.x, y, col.width, 32f),
            Loc.F("star_rule", level.threeStarBalls, level.twoStarBalls), new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter }); y += 34f;
        GUI.Label(new Rect(col.x, y, col.width, 40f), $"<color=#FFD94D>{Stars(PlayerStats.Stars(pickedChallenge))}</color>", labelCenter); y += 76f;
        if (Button(Column(y, 70f), Loc.T("start"), Btn(Tone.Gold))) WithSafety(() => game.StartChallenge(pickedChallenge));
    }

    void Stats()
    {
        float y = Header(Loc.T("stats"));
        var col = Column(y, 0f, 640f);
        var view = new Rect(col.x - 16f, y - 6f, col.width + 32f, H - y - 10f - safeBottom);
        float cw = view.width - 12f;
        BeginScroll(view, statsHeight);
        float ry = 0f;
        GUI.Box(new Rect(0f, ry, cw, 9 * 40f + 20f), GUIContent.none, panel);
        ry += 10f;
        void Row(string name, string value)
        {
            GUI.Label(new Rect(18f, ry, cw * 0.6f, 38f), name, label);
            GUI.Label(new Rect(cw * 0.4f, ry, cw * 0.6f - 18f, 38f), value, new GUIStyle(label) { alignment = TextAnchor.MiddleRight });
            ry += 40f;
        }
        Row(Loc.T("stat_games"), PlayerStats.GamesPlayed.ToString());
        Row(Loc.T("stat_best"), PlayerStats.BestScore.ToString());
        Row(Loc.T("stat_average"), PlayerStats.AverageScore.ToString("0.0"));
        Row(Loc.T("stat_throws"), PlayerStats.Throws.ToString());
        Row(Loc.T("stat_strikes"), $"{PlayerStats.Strikes}  ({PlayerStats.StrikeRate * 100f:0}%)");
        Row(Loc.T("stat_spares"), PlayerStats.Spares.ToString());
        Row(Loc.T("stat_gutters"), PlayerStats.Gutters.ToString());
        Row(Loc.T("stat_top_speed"), $"{PlayerStats.TopSpeedKmh:0} km/h");
        Row(Loc.T("stat_stars"), $"{PlayerStats.TotalStars} / {ChallengeLevels.All.Count * 3}");
        ry += 20f;
        ry = DrawLifetimeCharts(new Rect(0f, ry, cw, 0f));
        statsHeight = ry + 20f;
        GUI.EndScrollView();
    }
    float statsHeight = 1200f;

    void Settings()
    {
        float y = Header(Loc.T("settings"), saveButton: true);
        var col = Column(y, 0f, 600f);
        var view = new Rect(col.x, y, col.width, H - y - 12f - safeBottom);
        BeginScroll(view, settingsHeight);
        float cw = col.width - 20f, ry = 0f;
        float half = (cw - 12f) / 2f;

        bool Pair(string name, string a, string b, bool firstOn)
        {
            GUI.Label(new Rect(0f, ry, cw, 30f), name, labelSmall); ry += 32f;
            bool pickA = GUI.Button(new Rect(0f, ry, half, TapHSmall), a, firstOn ? buttonOn : buttonSmall);
            bool pickB = GUI.Button(new Rect(half + 12f, ry, half, TapHSmall), b, firstOn ? buttonSmall : buttonOn);
            ry += TapHSmall + 14f;
            return pickA ? true : pickB ? false : firstOn;
        }
        float Slider(string name, float value, float min, float max, string shown)
        {
            GUI.Label(new Rect(0f, ry, cw, 30f), $"{name}: {shown}", labelSmall); ry += 30f;
            float v = BigSlider(new Rect(0f, ry, cw, 44f), value, min, max); ry += 58f;
            return v;
        }

        bool motion = !Pair(Loc.T("controls"), Loc.T("control_swipe"), Loc.T("control_motion"), GameSettings.Control == ControlMode.Swipe);
        if (motion != (GameSettings.Control == ControlMode.Motion))
        {
            if (motion && !GameSettings.SafetyAcknowledged) { afterSafety = Screen.Settings; afterSafetyAction = null; screen = Screen.Safety; }
            GameSettings.Control = motion ? ControlMode.Motion : ControlMode.Swipe;
        }
        if (motion)
        {
            GUI.Label(new Rect(0f, ry, cw, 54f), MotionInput.Available ? Loc.T("motion_grip_hint") : Loc.T("control_motion_unavailable"), labelSmall); ry += 58f;
            string calibText = Loc.F("profile_button", GameSettings.ProfileName(GameSettings.DefaultProfile),
                GameSettings.Calibrated ? Loc.T("calibrated") : Loc.T("not_calibrated"));
            if (GUI.Button(new Rect(0f, ry, cw, TapH), calibText, Fit(new Rect(0f, ry, cw, TapH), calibText, Btn(Tone.Gold, true))))
                LeaveSettings(() => Go(Screen.Calibrate));
            ry += TapH + 14f;
            GameSettings.MotionSensitivity = Slider(Loc.T("sensitivity"), GameSettings.MotionSensitivity, 0.7f, 1.3f, $"{GameSettings.MotionSensitivity:0.00}×");
            string dir = GameSettings.AimOffsetDeg > 0.05f ? Loc.T("trim_left") : GameSettings.AimOffsetDeg < -0.05f ? Loc.T("trim_right") : Loc.T("trim_none");
            GameSettings.AimOffsetDeg = Slider(Loc.T("aim_offset"), GameSettings.AimOffsetDeg, -2.5f, 2.5f, $"{GameSettings.AimOffsetDeg:+0.0;-0.0;0.0}° {dir}");
            GUI.Label(new Rect(0f, ry, cw, 40f), Loc.T("aim_offset_hint"), labelSmall); ry += 46f;
            GameSettings.FlipAim = !Pair(Loc.T("aim_direction"), Loc.T("normal"), Loc.T("reversed"), !GameSettings.FlipAim);
            GameSettings.FlipSpin = !Pair(Loc.T("spin_direction"), Loc.T("normal"), Loc.T("reversed"), !GameSettings.FlipSpin);
        }
        GameSettings.LeftHanded = !Pair(Loc.T("hand"), Loc.T("right_hand"), Loc.T("left_hand"), !GameSettings.LeftHanded);
        GUI.Label(new Rect(0f, ry - 8f, cw, 30f), Loc.F("settings_profile_note", GameSettings.ProfileName(GameSettings.DefaultProfile)), Small(DimText, TextAnchor.MiddleLeft, 15)); ry += 26f;
        ry = SettingsAudio(ry, cw);
        GameSettings.Vibration = Pair(Loc.T("vibration"), Loc.T("on"), Loc.T("off"), GameSettings.Vibration);
        GameSettings.AimGuide = Pair(Loc.T("aim_guide"), Loc.T("on"), Loc.T("off"), GameSettings.AimGuide);
        bool zh = Pair(Loc.T("language"), "繁體中文", "English", Loc.Language == "zh");
        if (zh != (Loc.Language == "zh")) Loc.Language = zh ? "zh" : "en";
        // Text size: snaps to 5% steps; everything resizes live so the player sees the result.
        float fs = Slider(Loc.T("text_size"), GameSettings.FontScale, GameSettings.MinFontScale, GameSettings.MaxFontScale, $"{GameSettings.FontScale * 100f:0}%");
        fs = Mathf.Round(fs * 20f) / 20f;
        if (!Mathf.Approximately(fs, GameSettings.FontScale)) GameSettings.FontScale = fs;
        GUI.Label(new Rect(0f, ry, cw, 34f), Loc.T("text_size_sample"), label); ry += 44f;
        GUI.Label(new Rect(0f, ry, cw, 30f), Loc.T("ball_weight_moved"), labelSmall); ry += 36f;
        if (GUI.Button(new Rect(0f, ry, cw, TapHSmall), Loc.T("tutorial"), Btn(Tone.Violet, true)))
            LeaveSettings(() => { tutorialActive = true; tutorialStep = 0; screen = Screen.Tutorial; });
        ry += TapHSmall + 16f;
        settingsHeight = ry;
        GUI.EndScrollView();
    }
    float settingsHeight = 1200f;

    void Safety()
    {
        float y = Header(Loc.T("safety_title"));
        var col = Column(y, 0f);
        GUI.Box(new Rect(col.x - 16f, y - 10f, col.width + 32f, 254f), GUIContent.none, panel);
        GUI.Label(new Rect(col.x, y, col.width, 230f), Loc.T("safety_text"), label); y += 264f;
        if (Button(Column(y, 70f), Loc.T("understood"), Btn(Tone.Green)))
        {
            GameSettings.SafetyAcknowledged = true;
            screen = afterSafety;
            afterSafetyAction?.Invoke();
            afterSafetyAction = null;
        }
    }

    // ---- First-run tutorial (menu steps 0-2) ----

    void Tutorial()
    {
        DimBackground(0.72f);
        var col = Column(Mathf.Max(80f, H * 0.14f) + safeTop, 0f, 560f);
        float y = col.y;
        GUI.Box(new Rect(col.x - 18f, y - 18f, col.width + 36f, 452f), GUIContent.none, panel);

        if (tutorialStep == 0)
        {
            GUI.Label(new Rect(col.x, y, col.width, 56f), Loc.T("tutorial_welcome_title"), title); y += 74f;
            GUI.Label(new Rect(col.x, y, col.width, 170f), Loc.T("tutorial_welcome"), labelCenter); y += 186f;
            if (Button(new Rect(col.x, y, col.width, 70f), Loc.T("tutorial_next"), Btn(Tone.Primary))) tutorialStep = 1; y += 82f;
            if (Button(new Rect(col.x, y, col.width, TapHSmall), Loc.T("tutorial_skip"), buttonSmall)) FinishTutorial();
        }
        else if (tutorialStep == 1)
        {
            GUI.Label(new Rect(col.x, y, col.width, 52f), Loc.T("controls"), title); y += 68f;
            string txt = game.UsingMotion ? Loc.T("tutorial_controls_motion") : Loc.T("tutorial_controls_swipe");
            GUI.Label(new Rect(col.x, y, col.width, 210f), txt, label); y += 216f;
            if (Button(new Rect(col.x, y, col.width, 70f), Loc.T("tutorial_next"), Btn(Tone.Primary))) tutorialStep = 2; y += 82f;
            if (Button(new Rect(col.x, y, col.width, TapHSmall), Loc.T("tutorial_skip"), buttonSmall)) FinishTutorial();
        }
        else // step 2: go throw one for real
        {
            GUI.Label(new Rect(col.x, y, col.width, 170f), Loc.T("tutorial_try"), labelCenter); y += 186f;
            if (Button(new Rect(col.x, y, col.width, 70f), Loc.T("tutorial_try_go"), Btn(Tone.Green))) { tutorialStep = 3; game.StartPractice(0); } y += 82f;
            if (Button(new Rect(col.x, y, col.width, TapHSmall), Loc.T("tutorial_skip"), buttonSmall)) FinishTutorial();
        }
    }

    // Tutorial over the live HUD: step 3 guides the throw, step 4 is the finish.
    void DrawTutorialInGame()
    {
        if (tutorialStep == 3)
        {
            if (game.CurrentPhase is BowlingGame.Phase.Result or BowlingGame.Phase.Finished) { tutorialStep = 4; return; }
            string b = game.UsingMotion ? Loc.T("tutorial_banner_motion") : Loc.T("tutorial_banner_swipe");
            GUI.Box(new Rect(12f, H * 0.44f, W - 24f, 64f), GUIContent.none, panel);
            FitLabel(new Rect(24f, H * 0.44f, W - 48f, 64f), b, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Gold } });
        }
        else if (tutorialStep == 4)
        {
            DimBackground(0.72f);
            var col = Column(Mathf.Max(100f, H * 0.26f) + safeTop, 0f);
            float y = col.y;
            GUI.Box(new Rect(col.x - 18f, y - 18f, col.width + 36f, 328f), GUIContent.none, panel);
            GUI.Label(new Rect(col.x, y, col.width, 56f), Loc.T("tutorial_done_title"), title); y += 74f;
            GUI.Label(new Rect(col.x, y, col.width, 140f), Loc.T("tutorial_done"), labelCenter); y += 156f;
            if (Button(new Rect(col.x, y, col.width, 70f), Loc.T("tutorial_finish"), Btn(Tone.Green))) FinishTutorial();
        }
    }

    void FinishTutorial()
    {
        GameSettings.TutorialDone = true;
        tutorialActive = false;
        tutorialStep = 0;
        if (game.CurrentPhase != BowlingGame.Phase.Menu) game.BackToMenu();
        Go(Screen.Home);
    }

    // Motion players see the safety notice once before their first throw.
    void WithSafety(System.Action start)
    {
        if (GameSettings.Control == ControlMode.Motion && MotionInput.Available && !GameSettings.SafetyAcknowledged)
        {
            afterSafety = Screen.Home;
            afterSafetyAction = start;
            screen = Screen.Safety;
            return;
        }
        screen = Screen.Home;
        start();
    }

    void Go(Screen s)
    {
        // Leaving Settings with nothing changed (paths that changed something ask first, see LeaveSettings).
        if (screen == Screen.Settings && s != Screen.Settings && s != Screen.Safety && settingsSnapshot != null)
            DiscardSettingsEdit();
        screen = s;
        scroll = Vector2.zero;
    }

    // Title with a back button; returns where content starts.
    float Header(string text, bool saveButton = false)
    {
        float top = 16f + safeTop;
        if (Button(new Rect(16f + safeLeft, top, 132f, TapHSmall), Loc.T("back"), buttonSmall))
        {
            game.Motion.Cancel();
            if (screen == Screen.Settings) LeaveSettings(() => Go(Screen.Home));
            else Go(screen switch
            {
                Screen.ChallengeIntro => Screen.Challenges,
                Screen.Calibrate => Screen.Settings,
                Screen.PlayerSetup => Screen.Players,
                _ => Screen.Home,
            });
        }
        if (saveButton)
        {
            bool dirty = SettingsDirty;
            var r = new Rect(W - 148f - safeRight, top, 132f, TapHSmall);
            bool justSaved = Time.unscaledTime - savedToastTime < 1.6f;
            if (Button(r, justSaved && !dirty ? Loc.T("saved") : Loc.T("save"), dirty ? Btn(Tone.Green, true) : buttonSmall)) SaveSettingsEdit(keepEditing: true);
            if (dirty) Dot(new Vector2(r.xMax - 10f, r.y + 10f), 14f, Gold); // unsaved changes
        }
        FitLabel(new Rect(156f + safeLeft, top, W - 312f - safeLeft - safeRight, 60f), text, title);
        return top + 80f;
    }

    static string Stars(int n) => new string('★', n) + new string('☆', 3 - n);

    // ---- In-game HUD ----

    void DrawHud()
    {
        float top = 8f + safeTop;
        switch (game.CurrentMode)
        {
            case BowlingGame.Mode.Standard: top = DrawScorecards(); break;
            case BowlingGame.Mode.Practice: top = DrawPracticeBar(); break;
            case BowlingGame.Mode.Challenge: top = DrawChallengeBar(); break;
            case BowlingGame.Mode.Calibration: top = DrawCalibrationBar(); break;
        }

        if (Button(new Rect(W - 84f - safeRight, top + 8f, 72f, TapHSmall), "II", buttonSmall)) game.SetPaused(true);
        if (game.CurrentMode == BowlingGame.Mode.Practice && game.CurrentPhase == BowlingGame.Phase.Aiming &&
            Button(new Rect(12f + safeLeft, top + 8f, 190f, TapHSmall), Loc.T("change_leave"), Btn(Tone.Violet, true)))
            game.SetPracticeLeave((game.PracticeLeave + 1) % BowlingGame.PracticeLeaves.Length);

        var phase = game.CurrentPhase;
        if (game.CanReplay && Button(new Rect(W - 212f - safeRight, H - 84f - safeBottom, 196f, TapH), Loc.T("replay"), Btn(Tone.Violet))) game.StartReplay();
        if (phase is BowlingGame.Phase.Settling or BowlingGame.Phase.Result && !game.Paused)
            ZoomButton(new Rect(W - 212f - safeRight, H - 160f - safeBottom, 196f, TapH));
        if (phase == BowlingGame.Phase.Replay)
        {
            var tag = new GUIStyle(title) { fontSize = Fs(30), alignment = TextAnchor.MiddleLeft };
            tag.normal.textColor = Color.Lerp(Pink, Color.white, Mathf.PingPong(Time.unscaledTime * 2f, 1f));
            FitLabel(new Rect(24f + safeLeft, top + 74f, 300f, 44f), Loc.T("replay_label"), tag);
        }
        if (game.ZoomPins)
        {
            float zw = Mathf.Min(420f, W - 32f);
            var r = new Rect((W - zw) / 2f, top + 76f, zw, 44f);
            GUI.backgroundColor = new Color(0.1f, 0.05f, 0.25f, 0.85f);
            GUI.Box(r, Loc.T("zoom_hint"), Fit(r, Loc.T("zoom_hint"), chip));
            GUI.backgroundColor = Color.white;
        }
        if (game.Message.Length > 0 && phase != BowlingGame.Phase.Finished) DrawMessage();
        if (game.WaitingForTap && !(tutorialActive && tutorialStep == 4)) DrawTapToContinue();

        if (game.CurrentThrow != null && phase is BowlingGame.Phase.Rolling or BowlingGame.Phase.Settling or BowlingGame.Phase.Result or BowlingGame.Phase.Replay)
            DrawThrowAnalysis(game.CurrentThrow);

        if (phase == BowlingGame.Phase.Aiming) DrawThrowControls();
    }

    void ZoomButton(Rect r)
    {
        if (Button(r, game.ZoomPins ? Loc.T("zoom_off") : Loc.T("zoom_on"), Btn(game.ZoomPins ? Tone.Gold : Tone.Cyan, true))) game.ToggleZoom();
    }

    // The bottom-of-screen control prompt while aiming, with the wipe and magnifier buttons above it.
    void DrawThrowControls()
    {
        float panelH;
        if (!game.UsingMotion)
        {
            panelH = 76f;
            GUI.Box(new Rect(-4f, H - panelH - safeBottom, W + 8f, panelH + safeBottom + 30f), GUIContent.none, panel);
            FitLabel(new Rect(16f + safeLeft, H - panelH - safeBottom + 4f, W - 32f - safeLeft - safeRight, panelH - 8f), Loc.T("hint_swipe"),
                new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Cyan } });
        }
        else if (!game.MotionArmed)
        {
            // Ready-pose prompt with a big OK that starts capturing the swing.
            panelH = 166f;
            GUI.Box(new Rect(-4f, H - panelH - safeBottom, W + 8f, panelH + safeBottom + 30f), GUIContent.none, panel);
            bool timedOut = game.SwingTimedOut;
            string prompt = timedOut ? Loc.T("motion_timeout")
                : game.CurrentMode == BowlingGame.Mode.Calibration ? Loc.T("calib_ready_prompt") : Loc.T("motion_ready_prompt");
            FitLabel(new Rect(20f + safeLeft, H - panelH - safeBottom + 8f, W - 40f - safeLeft - safeRight, 70f), prompt,
                new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = timedOut ? Warn : Cyan } });
            GUI.enabled = !game.Wiping;
            if (Button(Column(H - 86f - safeBottom, 72f, 400f), Loc.T("motion_ready_ok"), Btn(Tone.Green))) game.ArmMotion();
            GUI.enabled = true;
        }
        else
        {
            // Capturing the swing: tell them to swing, and let them back out.
            panelH = 100f;
            GUI.Box(new Rect(-4f, H - panelH - safeBottom, W + 8f, panelH + safeBottom + 30f), GUIContent.none, panel);
            var st = new GUIStyle(big) { fontSize = Fs(40) };
            st.normal.textColor = Color.Lerp(Cyan, Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 1f));
            string cue = game.Motion.Stage switch
            {
                MotionInput.SwingStage.SwingBack => "motion_swing_back",
                MotionInput.SwingStage.SwingingBack => "motion_swinging_back",
                _ => "motion_swing_now",
            };
            FitLabel(new Rect(16f + safeLeft, H - 92f - safeBottom, W - 32f - 130f - safeLeft - safeRight, 68f), Loc.T(cue), st);
            if (Button(new Rect(W - 140f - safeRight, H - 84f - safeBottom, 124f, TapH), Loc.T("cancel"), Btn(Tone.Danger, true))) game.DisarmMotion();
            // Countdown: when it runs out the capture stops and OK has to be pressed again.
            float remaining = 1f - Mathf.Clamp01(game.SwingTimeoutProgress);
            var bar = new Rect(16f + safeLeft, H - panelH - safeBottom + 6f, W - 32f - safeLeft - safeRight, 6f);
            RoundTint(bar, new Color(1f, 1f, 1f, 0.12f));
            RoundTint(new Rect(bar.x, bar.y, bar.width * remaining, bar.height), remaining < 0.35f ? Warn : Cyan);
            return;
        }

        // Tools row: wipe the ball (not during calibration) and the magnifier.
        float ty = H - panelH - safeBottom - TapH - 12f;
        if (game.CurrentMode != BowlingGame.Mode.Calibration)
        {
            var wipe = new Rect(12f + safeLeft, ty, 230f, TapH);
            GUI.enabled = game.CanWipe || game.Wiping;
            string wipeText = game.Wiping ? Loc.T("wiping")
                : game.BallOil < 0.01f ? Loc.T("wipe_clean") : Loc.F("wipe_ball", Mathf.RoundToInt(game.BallOil * 100f));
            if (Button(wipe, wipeText, Btn(game.BallOil >= 0.4f ? Tone.Gold : Tone.Cyan, true))) game.WipeBall();
            GUI.enabled = true;
            if (game.Wiping) Tint(new Rect(wipe.x + 14f, wipe.yMax - 12f, (wipe.width - 28f) * game.WipeProgress, 6f), Color.white);
        }
        var zoom = new Rect(W - 196f - safeRight, ty, 184f, TapH);
        ZoomButton(zoom);

        // Technique: one-handed / spinner / two-handed. Between the wipe and magnifier buttons when
        // there's room, otherwise on its own row above them.
        if (game.CurrentMode == BowlingGame.Mode.Calibration) return;
        float left = 12f + safeLeft + 230f + 10f;
        var tech = zoom.x - 10f - left >= 170f ? new Rect(left, ty, zoom.x - 10f - left, TapH)
            : new Rect(12f + safeLeft, ty - TapH - 10f, Mathf.Min(320f, W - 24f - safeLeft - safeRight), TapH);
        GUI.enabled = game.CanChangeTechnique;
        var t = game.Technique;
        if (Button(tech, $"{Loc.T("technique")}: {BallTechniques.Name(t)} ›", Btn(t == BallTechnique.OneHanded ? Tone.Neutral : Tone.Violet, true)))
        {
            game.Technique = BallTechniques.Next(t);
            techniqueHintTime = Time.unscaledTime;
        }
        GUI.enabled = true;
        // Just switched: say what it does for a few seconds.
        if (Time.unscaledTime - techniqueHintTime < 3.5f)
        {
            var hint = new Rect(12f + safeLeft, tech.y - 64f, W - 24f - safeLeft - safeRight, 56f);
            GUI.Box(hint, GUIContent.none, panel);
            FitLabel(new Rect(hint.x + 12f, hint.y + 4f, hint.width - 24f, hint.height - 8f), BallTechniques.Description(game.Technique),
                new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
        }
    }
    float techniqueHintTime = -10f;

    // One row of 10 frames per player; the current player is highlighted.
    float DrawScorecards()
    {
        var scores = game.Scores;
        float nameW = scores.Count > 1 ? 76f : 0f;
        float cellW = Mathf.Min(64f, (W - 16f - nameW) / BowlingScore.Frames);
        float rowH = scores.Count > 1 ? 50f : 64f;
        float x0 = (W - nameW - cellW * BowlingScore.Frames) / 2f, y = 8f + safeTop;
        for (int p = 0; p < scores.Count; p++)
        {
            var score = scores[p];
            var totals = score.RunningTotals();
            bool active = p == game.CurrentPlayer;
            if (nameW > 0f)
            {
                cellName.normal.textColor = BowlingGame.PlayerColors[p];
                string pn = game.PlayerName(p);
                if (pn.Length > 5) pn = pn.Substring(0, 5);
                var nr = new Rect(x0, y, nameW - 3f, rowH - 3f);
                GUI.Box(nr, pn, Fit(nr, pn, active ? cellCurrent : cellName));
            }
            for (int f = 0; f < BowlingScore.Frames; f++)
            {
                var r = new Rect(x0 + nameW + f * cellW, y, cellW - 3f, rowH - 3f);
                string marks = string.Join(" ", score.MarksFor(f));
                string total = totals[f]?.ToString() ?? "";
                bool current = active && f == score.Frame && !score.IsGameOver;
                GUI.Box(r, scores.Count > 1 ? $"{marks}\n{total}" : $"{f + 1}\n{marks}\n{total}", current ? cellCurrent : cell);
            }
            y += rowH;
        }
        if (scores.Count > 1 && game.CurrentPhase == BowlingGame.Phase.Aiming)
        {
            var turn = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = BowlingGame.PlayerColors[game.CurrentPlayer] } };
            FitLabel(new Rect(16f, y + 4f, W - 32f, 34f), Loc.F("player_turn_named", game.PlayerName(game.CurrentPlayer)), turn);
            y += 36f;
        }
        return y;
    }

    float DrawPracticeBar()
    {
        float y0 = safeTop;
        GUI.Box(new Rect(-4f, -30f, W + 8f, 94f + y0), GUIContent.none, panel);
        string rate = game.PracticeThrows == 0 ? "-" : $"{100f * game.PracticeCleared / game.PracticeThrows:0}%";
        FitLabel(new Rect(16f + safeLeft, 6f + y0, W - 32f - safeLeft - safeRight, 52f),
            $"{Loc.T("practice")} · {LeaveName(game.PracticeLeave)} · {Loc.F("practice_stats", game.PracticeThrows, rate)}",
            new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        return 64f + y0;
    }

    float DrawChallengeBar()
    {
        float y0 = safeTop;
        var level = game.Challenge;
        GUI.Box(new Rect(-4f, -30f, W + 8f, 116f + y0), GUIContent.none, panel);
        FitLabel(new Rect(16f + safeLeft, 4f + y0, W - 32f - safeLeft - safeRight, 40f), $"{game.ChallengeIndex + 1}. {level.LocalizedName()}", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Gold } });
        string balls = new string('●', Mathf.Max(level.balls - game.BallsUsed, 0)) + new string('○', Mathf.Min(game.BallsUsed, level.balls));
        FitLabel(new Rect(16f + safeLeft, 42f + y0, W - 32f - safeLeft - safeRight, 38f), $"{level.LocalizedDescription()}   {balls}", new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
        return 86f + y0;
    }

    // Result text pops in big and settles; strikes and spares cycle through neon colours.
    void DrawMessage()
    {
        float age = Time.unscaledTime - game.MessageTime;
        float pop = 1f + 0.8f * Mathf.Exp(-age * 9f);
        var r = new Rect(0f, H * 0.28f, W, 130f);
        var saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * pop, r.center * scale);
        var shadow = new GUIStyle(glow) { fontSize = Fs(84) };
        var main = new GUIStyle(big) { fontSize = Fs(84) };
        main.normal.textColor = game.MessageRainbow ? Color.HSVToRGB(Mathf.Repeat(age * 0.8f, 1f), 0.7f, 1f) : Color.white;
        r = new Rect(16f, r.y, W - 32f, r.height);
        GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), game.Message, Fit(r, game.Message, shadow, 0.3f));
        GUI.Label(r, game.Message, Fit(r, game.Message, main, 0.3f));
        GUI.matrix = saved;
    }

    // ---- Overlays ----

    void DimBackground(float alpha = 0.55f)
    {
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, W, H), white);
        GUI.color = Color.white;
    }

    void DrawPause()
    {
        DimBackground();
        var col = Column(H * 0.22f, 0f, 460f);
        float y = col.y;
        GUI.Box(new Rect(col.x - 24f, y - 20f, col.width + 48f, 330f), GUIContent.none, panel);
        GUI.Label(new Rect(col.x, y, col.width, 60f), Loc.T("paused"), title); y += 80f;
        if (Button(new Rect(col.x, y, col.width, 70f), Loc.T("resume"), Btn(Tone.Green))) game.SetPaused(false); y += 80f;
        if (Button(new Rect(col.x, y, col.width, 66f), Loc.T("restart"), Btn(Tone.Cyan)))
            Ask(Loc.T("restart_confirm_title"), Loc.T(game.CurrentMode == BowlingGame.Mode.Standard ? "restart_confirm_game" : "restart_confirm_text"),
                (Loc.T("restart"), Tone.Danger, () => game.Restart()));
        y += 76f;
        if (Button(new Rect(col.x, y, col.width, 66f), Loc.T("main_menu"), buttonSmall))
        {
            void Leave() { game.BackToMenu(); Go(Screen.Home); }
            if (game.CurrentMode == BowlingGame.Mode.Standard)
                Ask(Loc.T("leave_confirm_title"), Loc.T("leave_confirm_game"),
                    (Loc.T("save_and_quit"), Tone.Green, Leave),   // every ball is saved as it's set up
                    (Loc.T("quit_no_save"), Tone.Danger, () => { game.DiscardSavedMatch(); Leave(); }));
            else
                Ask(Loc.T("leave_confirm_title"), Loc.T("leave_confirm_text"), (Loc.T("main_menu"), Tone.Danger, Leave));
        }
    }

    void DrawFinished()
    {
        DimBackground();
        switch (game.CurrentMode)
        {
            case BowlingGame.Mode.Challenge: DrawChallengeFinished(); break;
            case BowlingGame.Mode.Calibration: DrawCalibrationReport(); break;
            default: DrawGameSummary(); break;
        }
    }

    void DrawChallengeFinished()
    {
        var col = Column(Mathf.Max(100f, H * 0.16f) + safeTop, 0f);
        float y = col.y;
        bool won = game.ChallengeStars > 0;
        bool hasNext = won && game.ChallengeIndex + 1 < ChallengeLevels.All.Count;
        GUI.Box(new Rect(col.x - 18f, y - 14f, col.width + 36f, 64f + 90f + 44f + 50f + (hasNext ? 3 : 2) * 80f + 14f), GUIContent.none, panel);
        GUI.Label(new Rect(col.x, y, col.width, 56f), won ? Loc.T("challenge_clear") : Loc.T("challenge_fail"), title); y += 64f;
        GUI.Label(new Rect(col.x, y, col.width, 80f), $"<color=#FFD94D>{Stars(game.ChallengeStars)}</color>", new GUIStyle(big) { fontSize = Fs(64) }); y += 90f;
        GUI.Label(new Rect(col.x, y, col.width, 34f), Loc.F("balls_used", game.BallsUsed, game.Challenge.balls), labelCenter); y += 44f;
        if (game.CoinsThisGame > 0) CoinLine(new Rect(col.x, y, col.width, 34f), game.CoinsThisGame);
        y += 50f;
        if (hasNext && Button(new Rect(col.x, y, col.width, 70f), Loc.T("next_level"), Btn(Tone.Gold))) game.StartChallenge(game.ChallengeIndex + 1);
        if (hasNext) y += 80f;
        if (Button(new Rect(col.x, y, col.width, 70f), Loc.T("retry"), Btn(Tone.Cyan))) game.Restart(); y += 80f;
        if (Button(new Rect(col.x, y, col.width, 66f), Loc.T("main_menu"), buttonSmall)) { game.BackToMenu(); Go(Screen.Challenges); }
    }
}
