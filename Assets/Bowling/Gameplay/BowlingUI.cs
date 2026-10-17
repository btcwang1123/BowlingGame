using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Every screen: home menu, mode pickers, challenges, stats, settings, calibration, safety notice,
// the in-game HUD, pause and results. IMGUI keeps it all in code with no scene setup;
// layout is on a virtual canvas at least 720 units on its short side, so it fits portrait and landscape.
public class BowlingUI : MonoBehaviour
{
    enum Screen { Home, Players, Practice, Challenges, ChallengeIntro, Stats, Settings, Calibrate, Safety }

    BowlingGame game;
    Screen screen = Screen.Home;
    Screen afterSafety;
    System.Action afterSafetyAction;
    int pickedChallenge;
    Vector2 scroll;

    // Calibration: three comfortable practice swings.
    readonly List<float> calibrationSwings = new();
    string calibrationNote = "";

    // Rects of HUD buttons drawn last frame (screen pixels, GUI coordinates), so a tap on them isn't a throw.
    readonly List<Rect> hudButtons = new();
    readonly List<Rect> hudButtonsDrawing = new();

    float scale;
    float W, H;
    Font font;
    GUIStyle label, labelSmall, labelCenter, title, logo, big, glow, button, buttonSmall, buttonOn, panel, cell, cellCurrent, cellName;
    Texture2D white;

    static readonly Color Cyan = new(0.3f, 1f, 1f), Pink = new(1f, 0.4f, 0.9f), Gold = new(1f, 0.85f, 0.3f), DimText = new(0.65f, 0.7f, 0.85f);

    void Awake()
    {
        game = GetComponent<BowlingGame>();
        game.PointerOverUI = p => hudButtons.Any(r => r.Contains(new Vector2(p.x, UnityEngine.Screen.height - p.y)));
    }

    void LateUpdate()
    {
        // Calibration reads the swing through the same MotionInput the game uses.
        if (screen != Screen.Calibrate || game.CurrentPhase != BowlingGame.Phase.Menu) return;
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer == null || !MotionInput.Available) return;
        if (pointer.press.wasPressedThisFrame && !OverAnyButton(pointer.position.ReadValue())) game.Motion.Grip();
        if (pointer.press.wasReleasedThisFrame && game.Motion.Recording)
        {
            var r = game.Motion.Release();
            if (!r.valid) { calibrationNote = Loc.T(r.failKey); return; }
            calibrationSwings.Add(r.peakRate);
            calibrationNote = Loc.F("calib_got", r.peakRate);
            if (calibrationSwings.Count >= 3)
            {
                GameSettings.SwingReference = calibrationSwings.Average();
                GameSettings.Calibrated = true;
                calibrationNote = Loc.T("calib_done");
            }
        }
    }

    // For BowlingAutoplay screenshots: open a menu screen by name ("Home", "Settings", …).
    public void ShowScreen(string name)
    {
        if (System.Enum.TryParse(name, out Screen s)) Go(s);
    }

    bool OverAnyButton(Vector2 screenPos) => hudButtons.Any(r => r.Contains(new Vector2(screenPos.x, UnityEngine.Screen.height - screenPos.y)));

    // ---- Styles ----

    void MakeStyles()
    {
        if (label != null) return;
        // A CJK-capable system font on each platform, so Chinese text renders.
        font = Font.CreateDynamicFontFromOSFont(new[]
        {
            "Microsoft JhengHei UI", "Microsoft JhengHei", "PingFang TC", "Heiti TC", "Noto Sans CJK TC",
            "Noto Sans TC", "Noto Sans CJK", "Droid Sans Fallback", "Arial",
        }, 24);
        white = Solid(Color.white);

        GUIStyle Text(int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle fs = FontStyle.Normal)
        {
            var s = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = anchor, fontStyle = fs, wordWrap = true, richText = true };
            s.normal.textColor = color;
            return s;
        }
        label = Text(22, Color.white);
        labelSmall = Text(17, DimText);
        labelCenter = Text(22, Color.white, TextAnchor.MiddleCenter);
        title = Text(40, Pink, TextAnchor.MiddleCenter, FontStyle.Bold);
        logo = Text(68, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
        big = Text(52, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        glow = Text(52, new Color(1f, 0.3f, 0.9f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);

        button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true };
        button.normal.background = Solid(new Color(0.16f, 0.04f, 0.2f, 0.92f));
        button.hover.background = Solid(new Color(0.32f, 0.06f, 0.36f, 0.95f));
        button.active.background = Solid(new Color(0.5f, 0.1f, 0.5f, 1f));
        button.normal.textColor = button.hover.textColor = button.active.textColor = Color.white;
        buttonSmall = new GUIStyle(button) { fontSize = 18 };
        buttonOn = new GUIStyle(buttonSmall);
        buttonOn.normal.background = buttonOn.hover.background = Solid(new Color(0.05f, 0.35f, 0.4f, 0.95f));
        buttonOn.normal.textColor = buttonOn.hover.textColor = Color.white;

        panel = new GUIStyle(GUI.skin.box);
        panel.normal.background = Solid(new Color(0.02f, 0.01f, 0.07f, 0.88f));

        cell = new GUIStyle(GUI.skin.box) { font = font, fontSize = 15, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
        cell.normal.background = Solid(new Color(0.03f, 0.02f, 0.08f, 0.78f));
        cell.normal.textColor = Cyan;
        cellCurrent = new GUIStyle(cell);
        cellCurrent.normal.background = Solid(new Color(0.28f, 0.03f, 0.24f, 0.88f));
        cellCurrent.normal.textColor = Color.white;
        cellName = new GUIStyle(cell) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
    }

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    bool Button(Rect r, string text, GUIStyle style = null)
    {
        hudButtonsDrawing.Add(new Rect(r.x * scale, r.y * scale, r.width * scale, r.height * scale));
        return GUI.Button(r, text, style ?? button);
    }

    // Centre column used by the menus.
    Rect Column(float y, float height, float width = 460f)
    {
        float cw = Mathf.Min(width, W - 32f);
        return new Rect((W - cw) / 2f, y, cw, height);
    }

    // ---- Frame ----

    void OnGUI()
    {
        MakeStyles();
        scale = Mathf.Min(UnityEngine.Screen.width, UnityEngine.Screen.height) / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        W = UnityEngine.Screen.width / scale;
        H = UnityEngine.Screen.height / scale;
        if (Event.current.type == EventType.Layout) { hudButtons.Clear(); hudButtons.AddRange(hudButtonsDrawing); hudButtonsDrawing.Clear(); }

        if (game.CurrentPhase == BowlingGame.Phase.Menu)
        {
            DimBackground(screen == Screen.Home ? 0.35f : 0.6f); // keep text readable over the bright neon
            DrawMenus();
        }
        else
        {
            DrawHud();
            if (game.CurrentPhase == BowlingGame.Phase.Finished) DrawFinished();
            else if (game.Paused) DrawPause();
        }
    }

    // ---- Menus ----

    void DrawMenus()
    {
        switch (screen)
        {
            case Screen.Home: Home(); break;
            case Screen.Players: Players(); break;
            case Screen.Practice: Practice(); break;
            case Screen.Challenges: Challenges(); break;
            case Screen.ChallengeIntro: ChallengeIntro(); break;
            case Screen.Stats: Stats(); break;
            case Screen.Settings: Settings(); break;
            case Screen.Calibrate: Calibrate(); break;
            case Screen.Safety: Safety(); break;
        }
    }

    void Home()
    {
        float y = Mathf.Max(30f, H * 0.12f);
        GUI.Label(Column(y, 90f, 700f), "NEON BOWL", logo); y += 96f;
        GUI.Label(Column(y, 36f), Loc.T("tagline"), labelCenter); y += Mathf.Max(60f, H * 0.08f);

        if (Button(Column(y, 64f), Loc.T("play"))) Go(Screen.Players); y += 76f;
        if (Button(Column(y, 64f), Loc.T("practice"))) Go(Screen.Practice); y += 76f;
        if (Button(Column(y, 64f), Loc.F("challenges_stars", PlayerStats.TotalStars, ChallengeLevels.All.Count * 3))) Go(Screen.Challenges); y += 76f;
        var half = Column(y, 56f);
        if (Button(new Rect(half.x, y, half.width / 2f - 6f, 56f), Loc.T("stats"), buttonSmall)) Go(Screen.Stats);
        if (Button(new Rect(half.x + half.width / 2f + 6f, y, half.width / 2f - 6f, 56f), Loc.T("settings"), buttonSmall)) Go(Screen.Settings);
        y += 72f;

        string control = GameSettings.Control == ControlMode.Motion
            ? (MotionInput.Available ? Loc.T("control_motion") : Loc.T("control_motion_unavailable"))
            : Loc.T("control_swipe");
        GUI.Label(Column(y, 30f), $"{Loc.T("controls")}: {control}", new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
    }

    void Players()
    {
        float y = Header(Loc.T("play"));
        GUI.Label(Column(y, 36f), Loc.T("how_many_players"), labelCenter); y += 50f;
        for (int n = 1; n <= 4; n++)
        {
            int players = n;
            if (Button(Column(y, 60f), Loc.F("n_players", n))) WithSafety(() => game.StartStandard(players));
            y += 72f;
        }
        GUI.Label(Column(y, 60f), Loc.T("players_hint"), new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
    }

    void Practice()
    {
        float y = Header(Loc.T("practice"));
        GUI.Label(Column(y, 36f), Loc.T("practice_pick"), labelCenter); y += 48f;
        var col = Column(y, 0f, 560f);
        float bw = (col.width - 12f) / 2f;
        for (int i = 0; i < BowlingGame.PracticeLeaves.Length; i++)
        {
            int leave = i;
            var r = new Rect(col.x + (i % 2) * (bw + 12f), y + (i / 2) * 64f, bw, 54f);
            if (Button(r, LeaveName(i), buttonSmall)) WithSafety(() => game.StartPractice(leave));
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
        var col = Column(y, 0f, 640f);
        int perRow = W >= 600f ? 3 : 2;
        float bw = (col.width - (perRow - 1) * 10f) / perRow, bh = 84f;
        var view = new Rect(col.x, y, col.width, H - y - 16f);
        int rows = Mathf.CeilToInt(ChallengeLevels.All.Count / (float)perRow);
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, col.width - 20f, rows * (bh + 10f)), GUIStyle.none, GUIStyle.none);
        for (int i = 0; i < ChallengeLevels.All.Count; i++)
        {
            var r = new Rect((i % perRow) * (bw + 10f), (i / perRow) * (bh + 10f), bw, bh);
            bool open = PlayerStats.Unlocked(i);
            string stars = Stars(PlayerStats.Stars(i));
            string text = open ? $"{i + 1}. {ChallengeLevels.All[i].LocalizedName()}\n<color=#FFD94D>{stars}</color>" : $"{i + 1}.\n{Loc.T("locked")}";
            GUI.enabled = open;
            if (GUI.Button(r, text, buttonSmall)) { pickedChallenge = i; Go(Screen.ChallengeIntro); }
            GUI.enabled = true;
        }
        GUI.EndScrollView();
    }

    void ChallengeIntro()
    {
        var level = ChallengeLevels.All[pickedChallenge];
        float y = Header($"{pickedChallenge + 1}. {level.LocalizedName()}");
        var col = Column(y, 0f);
        GUI.Box(new Rect(col.x - 12f, y - 8f, col.width + 24f, 230f), GUIContent.none, panel);
        GUI.Label(new Rect(col.x, y, col.width, 70f), level.LocalizedDescription(), labelCenter); y += 76f;
        GUI.Label(new Rect(col.x, y, col.width, 32f), Loc.F("balls_allowed", level.balls), new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter }); y += 34f;
        GUI.Label(new Rect(col.x, y, col.width, 32f),
            Loc.F("star_rule", level.threeStarBalls, level.twoStarBalls), new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter }); y += 34f;
        GUI.Label(new Rect(col.x, y, col.width, 40f), $"<color=#FFD94D>{Stars(PlayerStats.Stars(pickedChallenge))}</color>", labelCenter); y += 70f;
        if (Button(Column(y, 64f), Loc.T("start"))) WithSafety(() => game.StartChallenge(pickedChallenge));
    }

    void Stats()
    {
        float y = Header(Loc.T("stats"));
        var col = Column(y, 0f);
        GUI.Box(new Rect(col.x - 12f, y - 8f, col.width + 24f, 360f), GUIContent.none, panel);
        void Row(string name, string value)
        {
            GUI.Label(new Rect(col.x, y, col.width * 0.6f, 36f), name, label);
            GUI.Label(new Rect(col.x + col.width * 0.6f, y, col.width * 0.4f, 36f), value, new GUIStyle(label) { alignment = TextAnchor.MiddleRight });
            y += 38f;
        }
        Row(Loc.T("stat_games"), PlayerStats.GamesPlayed.ToString());
        Row(Loc.T("stat_best"), PlayerStats.BestScore.ToString());
        Row(Loc.T("stat_average"), PlayerStats.AverageScore.ToString("0.0"));
        Row(Loc.T("stat_throws"), PlayerStats.Throws.ToString());
        Row(Loc.T("stat_strikes"), PlayerStats.Strikes.ToString());
        Row(Loc.T("stat_spares"), PlayerStats.Spares.ToString());
        Row(Loc.T("stat_gutters"), PlayerStats.Gutters.ToString());
        Row(Loc.T("stat_top_speed"), $"{PlayerStats.TopSpeedKmh:0} km/h");
        Row(Loc.T("stat_stars"), $"{PlayerStats.TotalStars} / {ChallengeLevels.All.Count * 3}");
    }

    void Settings()
    {
        float y = Header(Loc.T("settings"));
        var col = Column(y, 0f, 560f);
        var view = new Rect(col.x, y, col.width, H - y - 12f);
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, col.width - 20f, 900f), GUIStyle.none, GUIStyle.none);
        float cw = col.width - 20f, ry = 0f;
        float half = (cw - 10f) / 2f;

        bool Pair(string name, string a, string b, bool firstOn)
        {
            GUI.Label(new Rect(0f, ry, cw, 30f), name, labelSmall); ry += 30f;
            bool pickA = GUI.Button(new Rect(0f, ry, half, 50f), a, firstOn ? buttonOn : buttonSmall);
            bool pickB = GUI.Button(new Rect(half + 10f, ry, half, 50f), b, firstOn ? buttonSmall : buttonOn);
            ry += 62f;
            return pickA ? true : pickB ? false : firstOn;
        }
        float Slider(string name, float value, float min, float max, string shown)
        {
            GUI.Label(new Rect(0f, ry, cw, 30f), $"{name}: {shown}", labelSmall); ry += 32f;
            float v = GUI.HorizontalSlider(new Rect(0f, ry, cw, 24f), value, min, max); ry += 40f;
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
            if (GUI.Button(new Rect(0f, ry, cw, 50f), GameSettings.Calibrated ? Loc.T("recalibrate") : Loc.T("calibrate"), buttonSmall))
            { calibrationSwings.Clear(); calibrationNote = ""; screen = Screen.Calibrate; }
            ry += 62f;
            GameSettings.MotionSensitivity = Slider(Loc.T("sensitivity"), GameSettings.MotionSensitivity, 0.7f, 1.3f, $"{GameSettings.MotionSensitivity:0.00}×");
            GameSettings.FlipAim = !Pair(Loc.T("aim_direction"), Loc.T("normal"), Loc.T("reversed"), !GameSettings.FlipAim);
            GameSettings.FlipSpin = !Pair(Loc.T("spin_direction"), Loc.T("normal"), Loc.T("reversed"), !GameSettings.FlipSpin);
        }
        GameSettings.LeftHanded = !Pair(Loc.T("hand"), Loc.T("right_hand"), Loc.T("left_hand"), !GameSettings.LeftHanded);
        GameSettings.BallWeightLb = Mathf.RoundToInt(Slider(Loc.T("ball_weight"), GameSettings.BallWeightLb, 8f, 16f, Loc.F("lb", GameSettings.BallWeightLb)));
        float vol = Slider(Loc.T("volume"), GameSettings.Volume, 0f, 1f, $"{GameSettings.Volume * 100f:0}%");
        if (!Mathf.Approximately(vol, GameSettings.Volume)) GameSettings.Volume = vol;
        GameSettings.Vibration = Pair(Loc.T("vibration"), Loc.T("on"), Loc.T("off"), GameSettings.Vibration);
        GameSettings.AimGuide = Pair(Loc.T("aim_guide"), Loc.T("on"), Loc.T("off"), GameSettings.AimGuide);
        bool zh = Pair(Loc.T("language"), "繁體中文", "English", Loc.Language == "zh");
        if (zh != (Loc.Language == "zh")) Loc.Language = zh ? "zh" : "en";
        GUI.EndScrollView();
    }

    void Calibrate()
    {
        float y = Header(Loc.T("calibrate"));
        var col = Column(y, 0f);
        GUI.Box(new Rect(col.x - 12f, y - 8f, col.width + 24f, 300f), GUIContent.none, panel);
        if (!MotionInput.Available)
        {
            GUI.Label(new Rect(col.x, y, col.width, 120f), Loc.T("control_motion_unavailable"), labelCenter);
            return;
        }
        GUI.Label(new Rect(col.x, y, col.width, 110f), Loc.T("calib_instructions"), labelCenter); y += 120f;
        GUI.Label(new Rect(col.x, y, col.width, 50f), Loc.F("calib_progress", Mathf.Min(calibrationSwings.Count, 3)), big); y += 60f;
        GUI.Label(new Rect(col.x, y, col.width, 60f), calibrationNote, new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
    }

    void Safety()
    {
        float y = Header(Loc.T("safety_title"));
        var col = Column(y, 0f);
        GUI.Box(new Rect(col.x - 12f, y - 8f, col.width + 24f, 250f), GUIContent.none, panel);
        GUI.Label(new Rect(col.x, y, col.width, 230f), Loc.T("safety_text"), label); y += 260f;
        if (Button(Column(y, 64f), Loc.T("understood")))
        {
            GameSettings.SafetyAcknowledged = true;
            screen = afterSafety;
            afterSafetyAction?.Invoke();
            afterSafetyAction = null;
        }
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
        screen = s;
        scroll = Vector2.zero;
    }

    // Title with a back button; returns where content starts.
    float Header(string text)
    {
        if (Button(new Rect(16f, 16f, 110f, 48f), Loc.T("back"), buttonSmall))
        {
            game.Motion.Cancel();
            Go(screen is Screen.ChallengeIntro ? Screen.Challenges : screen is Screen.Calibrate ? Screen.Settings : Screen.Home);
        }
        GUI.Label(new Rect(130f, 14f, W - 260f, 56f), text, title);
        return 90f;
    }

    static string Stars(int n) => new string('★', n) + new string('☆', 3 - n);

    // ---- In-game HUD ----

    void DrawHud()
    {
        float top = 8f;
        switch (game.CurrentMode)
        {
            case BowlingGame.Mode.Standard: top = DrawScorecards(); break;
            case BowlingGame.Mode.Practice: top = DrawPracticeBar(); break;
            case BowlingGame.Mode.Challenge: top = DrawChallengeBar(); break;
        }

        if (Button(new Rect(W - 76f, top + 6f, 64f, 52f), "II", buttonSmall)) game.SetPaused(true);
        if (game.CurrentMode == BowlingGame.Mode.Practice && game.CurrentPhase == BowlingGame.Phase.Aiming &&
            Button(new Rect(12f, top + 6f, 170f, 52f), Loc.T("change_leave"), buttonSmall))
            game.SetPracticeLeave((game.PracticeLeave + 1) % BowlingGame.PracticeLeaves.Length);

        if (game.CanReplay && Button(new Rect(W - 196f, H - 150f, 180f, 60f), Loc.T("replay"))) game.StartReplay();
        if (game.CurrentPhase == BowlingGame.Phase.Replay)
        {
            var tag = new GUIStyle(title) { fontSize = 30, alignment = TextAnchor.MiddleLeft };
            tag.normal.textColor = Color.Lerp(Pink, Color.white, Mathf.PingPong(Time.unscaledTime * 2f, 1f));
            GUI.Label(new Rect(24f, top + 70f, 300f, 44f), Loc.T("replay_label"), tag);
        }
        if (game.Message.Length > 0 && game.CurrentPhase != BowlingGame.Phase.Finished) DrawMessage();
        if (game.LastThrowInfo.Length > 0 && game.CurrentPhase is not BowlingGame.Phase.Aiming and not BowlingGame.Phase.Throwing)
            GUI.Label(new Rect(0f, H * 0.3f + 110f, W, 34f), game.LastThrowInfo, new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });

        if (game.CurrentPhase == BowlingGame.Phase.Aiming)
        {
            string hint = game.UsingMotion ? (game.MotionRecording ? Loc.T("hint_motion_swing") : Loc.T("hint_motion")) : Loc.T("hint_swipe");
            GUI.Box(new Rect(0f, H - 70f, W, 70f), GUIContent.none, panel);
            GUI.Label(new Rect(12f, H - 66f, W - 24f, 62f), hint, new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Cyan } });
        }
    }

    // One row of 10 frames per player; the current player is highlighted.
    float DrawScorecards()
    {
        var scores = game.Scores;
        float nameW = scores.Count > 1 ? 56f : 0f;
        float cellW = Mathf.Min(64f, (W - 16f - nameW) / BowlingScore.Frames);
        float rowH = scores.Count > 1 ? 50f : 64f;
        float x0 = (W - nameW - cellW * BowlingScore.Frames) / 2f, y = 8f;
        for (int p = 0; p < scores.Count; p++)
        {
            var score = scores[p];
            var totals = score.RunningTotals();
            bool active = p == game.CurrentPlayer;
            if (nameW > 0f)
            {
                cellName.normal.textColor = BowlingGame.PlayerColors[p];
                GUI.Box(new Rect(x0, y, nameW - 3f, rowH - 3f), $"P{p + 1}", active ? cellCurrent : cellName);
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
            GUI.Label(new Rect(0f, y + 4f, W, 34f), Loc.F("player_turn", game.CurrentPlayer + 1), turn);
            y += 36f;
        }
        return y;
    }

    float DrawPracticeBar()
    {
        GUI.Box(new Rect(0f, 0f, W, 64f), GUIContent.none, panel);
        string rate = game.PracticeThrows == 0 ? "-" : $"{100f * game.PracticeCleared / game.PracticeThrows:0}%";
        GUI.Label(new Rect(16f, 6f, W - 32f, 52f),
            $"{Loc.T("practice")} · {LeaveName(game.PracticeLeave)} · {Loc.F("practice_stats", game.PracticeThrows, rate)}",
            new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        return 64f;
    }

    float DrawChallengeBar()
    {
        var level = game.Challenge;
        GUI.Box(new Rect(0f, 0f, W, 86f), GUIContent.none, panel);
        GUI.Label(new Rect(16f, 4f, W - 32f, 40f), $"{game.ChallengeIndex + 1}. {level.LocalizedName()}", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Gold } });
        string balls = new string('●', Mathf.Max(level.balls - game.BallsUsed, 0)) + new string('○', Mathf.Min(game.BallsUsed, level.balls));
        GUI.Label(new Rect(16f, 42f, W - 32f, 38f), $"{level.LocalizedDescription()}   {balls}", new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
        return 86f;
    }

    // Result text pops in big and settles; strikes and spares cycle through neon colours.
    void DrawMessage()
    {
        float age = Time.unscaledTime - game.MessageTime;
        float pop = 1f + 0.8f * Mathf.Exp(-age * 9f);
        var r = new Rect(0f, H * 0.3f, W, 110f);
        var saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * pop, r.center * scale);
        big.normal.textColor = game.MessageRainbow ? Color.HSVToRGB(Mathf.Repeat(age * 0.8f, 1f), 0.7f, 1f) : Color.white;
        GUI.Label(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), game.Message, glow);
        GUI.Label(r, game.Message, big);
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
        float y = H * 0.25f;
        GUI.Label(Column(y, 60f), Loc.T("paused"), title); y += 80f;
        if (Button(Column(y, 64f), Loc.T("resume"))) game.SetPaused(false); y += 76f;
        if (Button(Column(y, 64f), Loc.T("restart"))) game.Restart(); y += 76f;
        if (Button(Column(y, 64f), Loc.T("main_menu"))) { game.BackToMenu(); Go(Screen.Home); }
    }

    // Height of the results panel for what it will show.
    float ResultPanelHeight()
    {
        if (game.CurrentMode == BowlingGame.Mode.Challenge)
        {
            bool hasNext = game.ChallengeStars > 0 && game.ChallengeIndex + 1 < ChallengeLevels.All.Count;
            return 64f + 90f + 60f + (hasNext ? 3 : 2) * 70f + 14f;
        }
        float body = game.Scores.Count == 1 ? 96f + 38f + 38f : game.Scores.Count * 42f + 10f;
        return 64f + body + 54f + 2 * 70f + 14f;
    }

    void DrawFinished()
    {
        DimBackground();
        var col = Column(Mathf.Max(100f, H * 0.18f), 0f);
        float y = col.y;
        GUI.Box(new Rect(col.x - 16f, y - 12f, col.width + 32f, ResultPanelHeight()), GUIContent.none, panel);

        if (game.CurrentMode == BowlingGame.Mode.Challenge)
        {
            bool won = game.ChallengeStars > 0;
            GUI.Label(new Rect(col.x, y, col.width, 56f), won ? Loc.T("challenge_clear") : Loc.T("challenge_fail"), title); y += 64f;
            GUI.Label(new Rect(col.x, y, col.width, 80f), $"<color=#FFD94D>{Stars(game.ChallengeStars)}</color>", new GUIStyle(big) { fontSize = 64 }); y += 90f;
            GUI.Label(new Rect(col.x, y, col.width, 34f), Loc.F("balls_used", game.BallsUsed, game.Challenge.balls), labelCenter); y += 60f;
            bool hasNext = won && game.ChallengeIndex + 1 < ChallengeLevels.All.Count;
            if (hasNext && Button(new Rect(col.x, y, col.width, 60f), Loc.T("next_level"))) game.StartChallenge(game.ChallengeIndex + 1);
            if (hasNext) y += 70f;
            if (Button(new Rect(col.x, y, col.width, 60f), Loc.T("retry"))) game.Restart(); y += 70f;
            if (Button(new Rect(col.x, y, col.width, 60f), Loc.T("main_menu"))) { game.BackToMenu(); Go(Screen.Challenges); }
            return;
        }

        GUI.Label(new Rect(col.x, y, col.width, 56f), Loc.T("game_over"), title); y += 64f;
        var ranked = game.Scores.Select((s, i) => (score: s.RunningTotals()[^1] ?? 0, player: i)).OrderByDescending(p => p.score).ToList();
        if (ranked.Count == 1)
        {
            GUI.Label(new Rect(col.x, y, col.width, 90f), ranked[0].score.ToString(), new GUIStyle(big) { fontSize = 80, normal = { textColor = Cyan } }); y += 96f;
            var s = game.Scores[0];
            GUI.Label(new Rect(col.x, y, col.width, 34f), game.NewBest ? Loc.T("new_best") : Loc.F("best_score", PlayerStats.BestScore), labelCenter); y += 38f;
            GUI.Label(new Rect(col.x, y, col.width, 34f), Loc.F("game_summary", s.Count("X"), s.Count("/"), s.Count("-")), labelCenter); y += 38f;
        }
        else
        {
            for (int i = 0; i < ranked.Count; i++)
            {
                var style = new GUIStyle(label) { fontSize = i == 0 ? 30 : 24, alignment = TextAnchor.MiddleCenter, normal = { textColor = BowlingGame.PlayerColors[ranked[i].player] } };
                string crown = i == 0 ? "★ " : $"{i + 1}. ";
                GUI.Label(new Rect(col.x, y, col.width, 40f), $"{crown}P{ranked[i].player + 1}   {ranked[i].score}", style);
                y += 42f;
            }
            y += 10f;
        }
        GUI.Label(new Rect(col.x, y, col.width, 34f), Loc.F("fastest_ball", game.GameTopSpeedKmh), labelCenter); y += 54f;
        if (Button(new Rect(col.x, y, col.width, 60f), Loc.T("play_again"))) game.Restart(); y += 70f;
        if (Button(new Rect(col.x, y, col.width, 60f), Loc.T("main_menu"))) { game.BackToMenu(); Go(Screen.Home); }
    }
}
