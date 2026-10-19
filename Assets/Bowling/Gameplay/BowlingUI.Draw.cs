using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Drawing helpers (bars, dots, polylines, a top-down lane map), the post-throw analysis panel,
// and the charts on the results and stats screens.
public partial class BowlingUI
{
    static readonly Color ChartGrid = new(1f, 1f, 1f, 0.08f), ChartAxis = new(1f, 1f, 1f, 0.25f), LaneBoard = new(0.32f, 0.24f, 0.4f),
        GutterColor = new(0.08f, 0.06f, 0.12f);
    static readonly Color[] RollColors = { new(0.3f, 0.9f, 1f), new(1f, 0.45f, 0.85f), new(1f, 0.8f, 0.3f) };

    int chartPlayer;

    // ---- Primitives (only paint on Repaint; they take no input) ----

    static bool Painting => Event.current.type == EventType.Repaint;

    void Tint(Rect r, Color c)
    {
        if (!Painting) return;
        var saved = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, white);
        GUI.color = saved;
    }

    void RoundTint(Rect r, Color c)
    {
        if (!Painting || r.width <= 0f || r.height <= 0f) return;
        GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0f, c, 0f, Mathf.Min(7f, Mathf.Min(r.width, r.height) / 2f));
    }

    void Dot(Vector2 p, float size, Color c)
    {
        if (!Painting) return;
        var saved = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(p.x - size / 2f, p.y - size / 2f, size, size), circle);
        GUI.color = saved;
    }

    // Lines as a row of dots: no GUI matrix rotation, so they also work inside scroll views.
    void Line(Vector2 a, Vector2 b, float width, Color c, bool dashed = false)
    {
        if (!Painting) return;
        float spacing = Mathf.Max(1.2f, width * 0.6f);
        float len = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / spacing));
        for (int i = 0; i <= steps; i++)
        {
            if (dashed && (i * spacing) % 12f > 6f) continue;
            Dot(Vector2.Lerp(a, b, (float)i / steps), width, c);
        }
    }

    void Polyline(IList<Vector2> pts, float width, Color c, bool dashed = false)
    {
        for (int i = 1; i < pts.Count; i++) Line(pts[i - 1], pts[i], width, c, dashed);
    }

    GUIStyle Small(Color color, TextAnchor anchor = TextAnchor.MiddleLeft, int size = 17) =>
        new(labelSmall) { fontSize = Fs(size), alignment = anchor, wordWrap = false, normal = { textColor = color } };

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    void CoinChip(Rect r)
    {
        GUI.backgroundColor = new Color(0.12f, 0.08f, 0.22f, 0.9f);
        GUI.Box(r, GUIContent.none, chip);
        GUI.backgroundColor = Color.white;
        Dot(new Vector2(r.x + 26f, r.center.y), 26f, Gold);
        Dot(new Vector2(r.x + 26f, r.center.y), 14f, new Color(0.85f, 0.55f, 0.05f));
        GUI.Label(new Rect(r.x + 44f, r.y, r.width - 54f, r.height), ProShop.Coins.ToString("N0"),
            new GUIStyle(labelBold) { fontSize = Fs(22), alignment = TextAnchor.MiddleRight, normal = { textColor = Gold } });
    }

    void CoinLine(Rect r, int coins)
    {
        GUI.Label(r, $"<color=#{Hex(Gold)}>+{coins}</color> {Loc.T("coins")}", labelCenter);
    }

    // ---- Scroll views with touch-drag scrolling ----
    // IMGUI only scrolls with a mouse wheel by itself, and its touch-as-mouse events are unreliable
    // on phones and mobile browsers, so drags are read from the Input System pointer (UpdateDragScroll,
    // every frame). A press that moves up or down past a small threshold becomes a scroll and takes
    // the press away from the button it started on, so flicking through the shop never buys anything;
    // a press that moves sideways first is left alone, so sliders still drag.

    Rect scrollViewRect;          // the scroll view drawn last frame, virtual units
    int scrollViewFrame = -10;    // frame it was drawn on
    float scrollMax;
    bool pressInScroll, dragScrolling, stealPress;
    Vector2 pressStart;
    float scrollAtPress, lastDragY, scrollVelocity;
    const float DragThreshold = 12f;

    void BeginScroll(Rect view, float contentHeight)
    {
        var e = Event.current;
        scrollMax = Mathf.Max(0f, contentHeight - view.height);
        if (e.type == EventType.Repaint) { scrollViewRect = view; scrollViewFrame = Time.frameCount; }
        if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition))
        {
            scroll.y += e.delta.y * 24f;
            scrollVelocity = 0f;
            e.Use();
        }
        // While a drag is scrolling, the pointer events belong to the scroll, not the buttons under it.
        if (dragScrolling && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp)) e.Use();
        scroll.y = Mathf.Clamp(scroll.y, 0f, scrollMax);
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, view.width - 20f, contentHeight), GUIStyle.none, GUIStyle.none);
    }

    bool ModalOpen => pendingBuy != null || leaveAction != null || confirm != null;

    void Update() => UpdateDragScroll();

    void UpdateDragScroll()
    {
        var p = Pointer.current;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
        bool live = scale > 0f && scrollViewFrame >= Time.frameCount - 2 && !ModalOpen;
        if (p == null || !live)
        {
            pressInScroll = dragScrolling = false;
            scrollVelocity = 0f;
            return;
        }
        Vector2 raw = p.position.ReadValue();
        var pos = new Vector2(raw.x / scale, (UnityEngine.Screen.height - raw.y) / scale);
        if (p.press.wasPressedThisFrame)
        {
            pressInScroll = scrollViewRect.Contains(pos);
            dragScrolling = false;
            pressStart = pos;
            lastDragY = pos.y;
            scrollAtPress = scroll.y;
            scrollVelocity = 0f;
        }
        else if (p.press.isPressed && pressInScroll)
        {
            Vector2 d = pos - pressStart;
            if (!dragScrolling)
            {
                if (Mathf.Abs(d.y) > DragThreshold && Mathf.Abs(d.y) > Mathf.Abs(d.x)) { dragScrolling = true; stealPress = true; }
                else if (Mathf.Abs(d.x) > DragThreshold) pressInScroll = false; // sideways: a slider, not a scroll
            }
            if (dragScrolling)
            {
                scroll.y = Mathf.Clamp(scrollAtPress - d.y, 0f, scrollMax);
                scrollVelocity = Mathf.Lerp(scrollVelocity, (lastDragY - pos.y) / dt, 0.4f);
            }
            lastDragY = pos.y;
        }
        else if (!p.press.isPressed)
        {
            pressInScroll = false;
            dragScrolling = false;
            // Fling: keep gliding after the finger lifts, slowing down.
            if (Mathf.Abs(scrollVelocity) > 30f)
            {
                scroll.y += scrollVelocity * dt;
                if (scroll.y <= 0f || scroll.y >= scrollMax) scrollVelocity = 0f;
                scroll.y = Mathf.Clamp(scroll.y, 0f, scrollMax);
                scrollVelocity *= Mathf.Exp(-4f * dt);
            }
            else scrollVelocity = 0f;
        }
    }

    // A titled card for one chart; returns the area inside it.
    Rect ChartCard(Rect r, string heading)
    {
        GUI.Box(r, GUIContent.none, panelLight);
        GUI.Label(new Rect(r.x + 16f, r.y + 8f, r.width - 32f, 30f), heading, new GUIStyle(labelBold) { normal = { textColor = Gold } });
        return new Rect(r.x + 16f, r.y + 44f, r.width - 32f, r.height - 56f);
    }

    // ---- Top-down lane map ----

    // Lane x (with gutters) across the rect; lane z bottom (foul line) to top (pin deck). The last
    // few metres get 45% of the height, so the pins and the pocket are big enough to read.
    static Vector2 LanePoint(Rect r, float x, float z)
    {
        const float span = BowlingLane.HalfWidth + 0.24f;
        float fx = Mathf.InverseLerp(-span, span, x);
        const float split = 16f;
        float fz = z <= split ? Mathf.InverseLerp(0f, split, z) * 0.55f : 0.55f + Mathf.InverseLerp(split, BowlingLane.LaneEndZ, z) * 0.45f;
        return new Vector2(r.x + fx * r.width, r.yMax - Mathf.Clamp01(fz) * r.height);
    }

    void DrawLaneMap(Rect r, IEnumerable<ThrowRecord> throws, ThrowRecord highlight, bool showPaths)
    {
        Tint(r, GutterColor);
        var l = LanePoint(r, -BowlingLane.HalfWidth, 0f).x;
        var rr = LanePoint(r, BowlingLane.HalfWidth, 0f).x;
        Tint(new Rect(l, r.y, rr - l, r.height), LaneBoard);
        Tint(new Rect(l, r.yMax - 2f, rr - l, 2f), Pink);                         // foul line
        float split = LanePoint(r, 0f, 16f).y;
        Tint(new Rect(l, split, rr - l, 1f), ChartGrid);
        // Arrows.
        foreach (int b in new[] { 5, 10, 15, 20, 25, 30, 35 })
        {
            float x = -BowlingLane.HalfWidth + b * (BowlingLane.HalfWidth * 2f / 39f);
            Dot(LanePoint(r, x, 4.6f), 3f, new Color(1f, 1f, 1f, 0.35f));
        }
        // Pocket target for the highlighted (or newest) throw's hand.
        var refThrow = highlight ?? throws.LastOrDefault();
        bool lefty = refThrow?.leftHanded ?? GameSettings.LeftHanded;
        float pocketX = lefty ? -ThrowRecord.PocketOffset : ThrowRecord.PocketOffset;
        var p0 = LanePoint(r, pocketX - ThrowRecord.PocketTolerance, BowlingLane.HeadPinZ - 0.6f);
        var p1 = LanePoint(r, pocketX + ThrowRecord.PocketTolerance, BowlingLane.HeadPinZ - 0.1f);
        Tint(Rect.MinMaxRect(p0.x, p1.y, p1.x, p0.y), new Color(0.3f, 1f, 0.5f, 0.3f));
        // Pins.
        float pinSize = Mathf.Clamp(r.width * 0.08f, 4f, 10f);
        foreach (var spot in BowlingLane.PinSpots()) Dot(LanePoint(r, spot.x, spot.z), pinSize, new Color(1f, 1f, 1f, 0.85f));

        foreach (var t in throws)
        {
            bool hi = t == highlight;
            if (showPaths || hi)
            {
                var pts = t.path.Select(p => LanePoint(r, p.x, p.y)).ToList();
                Polyline(pts, hi ? 3.5f : 2f, hi ? Gold : new Color(0.6f, 0.8f, 1f, 0.35f));
            }
            if (t.reachedPins)
                Dot(LanePoint(r, t.entryX, BowlingLane.HeadPinZ - 0.2f), hi ? 11f : 8f, t.InPocket ? Good : Warn);
        }
    }

    // ---- Post-throw analysis: a small square panel with the lane trace and the throw's numbers ----

    void DrawThrowAnalysis(ThrowRecord t)
    {
        float w = Mathf.Min(340f, W - 24f - safeLeft - safeRight - (W < 600f ? 0f : 220f)), h = 304f;
        var r = new Rect(12f + safeLeft, H - h - 12f - safeBottom, w, h);
        GUI.Box(r, GUIContent.none, panel);
        GUI.Label(new Rect(r.x + 16f, r.y + 8f, 160f, 30f), Loc.T("analysis_title"), new GUIStyle(labelBold) { normal = { textColor = Gold } });

        Color shapeColor = t.BallShape switch { ThrowRecord.Shape.Right => Pink, ThrowRecord.Shape.Left => Cyan, _ => Color.white };
        GUI.backgroundColor = (t.style is ThrowStyle.Spinner or ThrowStyle.TwoHanded ? Gold : shapeColor) * 0.55f;
        var shapeChip = new Rect(r.xMax - 156f, r.y + 10f, 142f, 30f);
        GUI.Box(shapeChip, t.ShapeName, Fit(shapeChip, t.ShapeName, new GUIStyle(chip) { fontSize = Fs(16) }));
        GUI.backgroundColor = Color.white;

        DrawLaneMap(new Rect(r.x + 16f, r.y + 48f, 66f, h - 64f), new[] { t }, t, true);

        float x = r.x + 96f, y = r.y + 48f, lw = 70f, vw = r.xMax - x - lw - 12f;
        void Row(string name, string value, Color color)
        {
            GUI.Label(new Rect(x, y, lw, 30f), name, Small(DimText));
            GUI.Label(new Rect(x + lw, y, vw, 30f), value, Small(color, TextAnchor.MiddleLeft, 18));
            y += 30.5f;
        }

        var log = game.ThrowLog;
        int idx = log.IndexOf(t);
        ThrowRecord prev = idx > 0 ? log[idx - 1] : idx < 0 && log.Count > 0 ? log[^1] : null;
        Row(Loc.T("an_speed"), $"{t.speedKmh:0.0} km/h", Color.white);
        if (prev != null)
        {
            float d = t.speedKmh - prev.speedKmh;
            string arrow = d > 0.05f ? "▲" : d < -0.05f ? "▼" : "＝";
            Row(Loc.T("an_vs_last"), $"{arrow}{Mathf.Abs(d):0.0}  ({Loc.T("an_avg")} {game.AverageSpeedKmh:0.0})", d >= 0f ? Good : Warn);
        }
        else Row(Loc.T("an_vs_last"), "—", DimText);
        string dir = t.angleDeg > 0.05f ? Loc.T("an_right") : t.angleDeg < -0.05f ? Loc.T("an_left") : "";
        Row(Loc.T("an_angle"), $"{t.angleDeg:+0.0;-0.0;0.0}° {dir}", Color.white);
        string bars = new string('■', t.SpinLevel) + new string('□', 3 - t.SpinLevel);
        Row(Loc.T("an_spin"), $"{bars} {Loc.T("spin_level_" + t.SpinLevel)}", shapeColor);
        if (t.gutter) Row(Loc.T("an_entry"), Loc.T("an_gutter"), Bad);
        else if (!t.reachedPins) Row(Loc.T("an_entry"), "…", DimText);
        else if (t.InPocket) Row(Loc.T("an_entry"), Loc.T("an_pocket"), Good);
        else
        {
            float cm = t.PocketError * 100f;
            Row(Loc.T("an_entry"), Loc.F(cm > 0f ? "an_off_right" : "an_off_left", Mathf.Abs(cm)), Warn);
        }
        Row(Loc.T("an_entry_angle"), t.reachedPins ? $"{Mathf.Abs(t.entryAngleDeg):0.0}°" : "—", Color.white);
        Row(Loc.T("an_ball"), t.wiped ? Loc.T("an_wiped") : Loc.F("an_oil", Mathf.RoundToInt(t.oil * 100f)), t.wiped ? Good : t.oil >= 0.4f ? Warn : Color.white);
        Row(Loc.T("an_result"), t.mark.Length == 0 ? "…" : Loc.F("an_pins", t.knocked, t.standingBefore), t.mark == "X" || t.mark == "/" ? Gold : Color.white);
    }

    // ---- End of a standard game: score, ranking, and charts ----

    void DrawGameSummary()
    {
        var col = Column(0f, 0f, 700f);
        float top = 12f + safeTop, bottom = H - 96f - safeBottom;
        var view = new Rect(col.x - 12f, top, col.width + 24f, bottom - top);
        GUI.Box(new Rect(view.x - 6f, view.y - 4f, view.width + 12f, view.height + 8f), GUIContent.none, panel);
        var inner = new Rect(view.x + 8f, view.y + 8f, view.width - 16f, view.height - 16f);
        BeginScroll(inner, summaryHeight);
        float cw = inner.width - 20f, y = 0f;

        GUI.Label(new Rect(0f, y, cw, 56f), Loc.T("game_over"), title); y += 60f;
        var ranked = game.Scores.Select((s, i) => (score: s.RunningTotals()[^1] ?? 0, player: i)).OrderByDescending(p => p.score).ToList();
        if (ranked.Count == 1)
        {
            GUI.Label(new Rect(0f, y, cw, 90f), ranked[0].score.ToString(), new GUIStyle(big) { fontSize = Fs(80), normal = { textColor = Cyan } }); y += 92f;
            var s = game.Scores[0];
            GUI.Label(new Rect(0f, y, cw, 34f), game.NewBest ? Loc.T("new_best") : Loc.F("best_score", PlayerStats.BestScore), labelCenter); y += 36f;
            GUI.Label(new Rect(0f, y, cw, 34f), Loc.F("game_summary", s.Count("X"), s.Count("/"), s.Count("-")), labelCenter); y += 36f;
        }
        else
        {
            for (int i = 0; i < ranked.Count; i++)
            {
                var style = new GUIStyle(label) { fontSize = Fs(i == 0 ? 30 : 24), alignment = TextAnchor.MiddleCenter, normal = { textColor = BowlingGame.PlayerColors[ranked[i].player] } };
                string crown = i == 0 ? "★ " : $"{i + 1}. ";
                FitLabel(new Rect(0f, y, cw, 40f), $"{crown}{game.PlayerName(ranked[i].player)}   {ranked[i].score}", style);
                y += 42f;
            }
        }
        GUI.Label(new Rect(0f, y, cw, 34f), Loc.F("fastest_ball", game.GameTopSpeedKmh), labelCenter); y += 36f;
        CoinLine(new Rect(0f, y, cw, 34f), game.CoinsThisGame); y += 46f;

        // Player picker for the per-player charts.
        int players = game.Scores.Count;
        chartPlayer = Mathf.Clamp(chartPlayer, 0, players - 1);
        if (players > 1)
        {
            float bw = Mathf.Min(120f, (cw - (players - 1) * 10f) / players);
            float x0 = (cw - (bw * players + (players - 1) * 10f)) / 2f;
            for (int p = 0; p < players; p++)
                {
                    var pr = new Rect(x0 + p * (bw + 10f), y, bw, TapHSmall);
                    if (GUI.Button(pr, game.PlayerName(p), Fit(pr, game.PlayerName(p), p == chartPlayer ? buttonOn : buttonSmall))) chartPlayer = p;
                }
            y += TapHSmall + 14f;
        }

        y = ChartRunningTotals(new Rect(0f, y, cw, 270f)) + 14f;
        y = ChartPinsPerFrame(new Rect(0f, y, cw, 250f), chartPlayer) + 14f;
        y = ChartOutcomes(new Rect(0f, y, cw, 150f), chartPlayer) + 14f;
        y = ChartSpeeds(new Rect(0f, y, cw, 230f), chartPlayer) + 14f;
        y = ChartAccuracy(new Rect(0f, y, cw, 300f), chartPlayer) + 14f;
        y = ChartVsRecords(new Rect(0f, y, cw, 230f), chartPlayer) + 14f;
        summaryHeight = y;
        GUI.EndScrollView();

        var buttons = Column(H - 84f - safeBottom, 72f, 700f);
        float half = buttons.width / 2f - 6f;
        if (Button(new Rect(buttons.x, buttons.y, half, 72f), Loc.T("play_again"), Btn(Tone.Primary))) game.Restart();
        if (Button(new Rect(buttons.x + half + 12f, buttons.y, half, 72f), Loc.T("main_menu"), buttonSmall)) { game.BackToMenu(); Go(Screen.Home); }
    }
    float summaryHeight = 1600f;

    // Generic line chart: frame on x, value on y, with grid lines and labels.
    void PlotFrame(Rect plot, float yMin, float yMax, int gridLines, int xCount, System.Func<int, string> xLabel)
    {
        for (int i = 0; i <= gridLines; i++)
        {
            float v = Mathf.Lerp(yMin, yMax, (float)i / gridLines);
            float py = plot.yMax - (float)i / gridLines * plot.height;
            Tint(new Rect(plot.x, py, plot.width, 1f), i == 0 ? ChartAxis : ChartGrid);
            GUI.Label(new Rect(plot.x - 46f, py - 12f, 40f, 24f), v.ToString("0"), Small(DimText, TextAnchor.MiddleRight, 14));
        }
        for (int i = 0; i < xCount; i++)
        {
            string text = xLabel(i);
            if (text == null) continue;
            float px = XAt(plot, i, xCount);
            GUI.Label(new Rect(px - 20f, plot.yMax + 2f, 40f, 22f), text, Small(DimText, TextAnchor.MiddleCenter, 14));
        }
    }

    static float XAt(Rect plot, int i, int count) => count <= 1 ? plot.center.x : plot.x + plot.width * i / (count - 1f);
    static float YAt(Rect plot, float v, float min, float max) => plot.yMax - Mathf.InverseLerp(min, max, v) * plot.height;

    void Legend(Rect r, params (string name, Color color)[] items)
    {
        float x = r.x;
        foreach (var (name, color) in items)
        {
            Dot(new Vector2(x + 7f, r.center.y), 12f, color);
            float w = Small(Color.white).CalcSize(new GUIContent(name)).x + 30f;
            GUI.Label(new Rect(x + 18f, r.y, w, r.height), name, Small(Color.white, TextAnchor.MiddleLeft, 15));
            x += w;
        }
    }

    float ChartRunningTotals(Rect r)
    {
        var area = ChartCard(r, Loc.T("chart_running"));
        var plot = new Rect(area.x + 46f, area.y + 30f, area.width - 56f, area.height - 58f);
        var series = game.Scores.Select(s => s.RunningTotals()).ToList();
        float avg = PlayerStats.AverageScore;
        float max = Mathf.Max(60f, series.SelectMany(t => t).Max(v => (float)(v ?? 0)), avg, PlayerStats.BestScore);
        max = Mathf.Ceil(max * 1.1f / 30f) * 30f;
        PlotFrame(plot, 0f, max, 4, 10, i => (i + 1).ToString());
        // Pace lines: personal average and best, spread evenly over ten frames.
        if (avg > 0f) Line(new Vector2(XAt(plot, 0, 10), YAt(plot, avg / 10f, 0f, max)), new Vector2(XAt(plot, 9, 10), YAt(plot, avg, 0f, max)), 2f, DimText, true);
        if (PlayerStats.BestScore > 0) Line(new Vector2(XAt(plot, 0, 10), YAt(plot, PlayerStats.BestScore / 10f, 0f, max)), new Vector2(XAt(plot, 9, 10), YAt(plot, PlayerStats.BestScore, 0f, max)), 2f, new Color(Gold.r, Gold.g, Gold.b, 0.6f), true);
        for (int p = 0; p < series.Count; p++)
        {
            var pts = new List<Vector2>();
            for (int f = 0; f < 10; f++) if (series[p][f] is int v) pts.Add(new Vector2(XAt(plot, f, 10), YAt(plot, v, 0f, max)));
            Polyline(pts, 4f, BowlingGame.PlayerColors[p]);
            foreach (var pt in pts) Dot(pt, 9f, BowlingGame.PlayerColors[p]);
        }
        var legend = new List<(string, Color)>();
        for (int p = 0; p < series.Count; p++) legend.Add((series.Count > 1 ? game.PlayerName(p) : Loc.T("legend_this_game"), BowlingGame.PlayerColors[p]));
        legend.Add((Loc.T("legend_avg_pace"), DimText));
        legend.Add((Loc.T("legend_best_pace"), Gold));
        Legend(new Rect(area.x, area.y, area.width, 24f), legend.ToArray());
        return r.yMax;
    }

    float ChartPinsPerFrame(Rect r, int player)
    {
        var area = ChartCard(r, Loc.T("chart_frames") + (game.Scores.Count > 1 ? $" · {game.PlayerName(player)}" : ""));
        var plot = new Rect(area.x + 46f, area.y + 30f, area.width - 56f, area.height - 58f);
        var throws = game.ThrowLog.Where(t => t.player == player).ToList();
        float max = Mathf.Max(10f, Enumerable.Range(0, 10).Max(f => throws.Where(t => t.frame == f).Sum(t => t.knocked)));
        for (int i = 0; i <= 2; i++)
        {
            float py = plot.yMax - i / 2f * plot.height;
            Tint(new Rect(plot.x, py, plot.width, 1f), i == 0 ? ChartAxis : ChartGrid);
            GUI.Label(new Rect(plot.x - 46f, py - 12f, 40f, 24f), (max * i / 2f).ToString("0"), Small(DimText, TextAnchor.MiddleRight, 14));
        }
        float slot = plot.width / 10f, bw = slot * 0.6f;
        var score = game.Scores[player];
        for (int f = 0; f < 10; f++)
        {
            float x = plot.x + slot * f + (slot - bw) / 2f, y = plot.yMax;
            int roll = 0;
            foreach (var t in throws.Where(t => t.frame == f))
            {
                float h = t.knocked / max * plot.height;
                if (h > 0f) RoundTint(new Rect(x, y - h, bw, h - 1f), RollColors[Mathf.Min(roll, 2)]);
                y -= h;
                roll++;
            }
            string marks = string.Join(" ", score.MarksFor(f));
            GUI.Label(new Rect(x - 12f, y - 22f, bw + 24f, 20f), marks, Small(marks.Contains("X") ? Gold : Color.white, TextAnchor.MiddleCenter, 14));
            GUI.Label(new Rect(x - 12f, plot.yMax + 2f, bw + 24f, 22f), (f + 1).ToString(), Small(DimText, TextAnchor.MiddleCenter, 14));
        }
        Legend(new Rect(area.x, area.y, area.width, 24f), (Loc.T("legend_ball1"), RollColors[0]), (Loc.T("legend_ball2"), RollColors[1]), (Loc.T("legend_ball3"), RollColors[2]));
        return r.yMax;
    }

    float ChartOutcomes(Rect r, int player)
    {
        var area = ChartCard(r, Loc.T("chart_outcomes"));
        var score = game.Scores[player];
        int strikes = 0, spares = 0, open = 0;
        for (int f = 0; f < 10; f++)
        {
            var m = score.MarksFor(f);
            if (m.Count == 0) continue;
            if (m[0] == "X") strikes++;
            else if (m.Contains("/")) spares++;
            else open++;
        }
        int gutters = game.ThrowLog.Count(t => t.player == player && t.mark == "-");
        var parts = new (string name, int n, Color c)[]
        {
            (Loc.T("out_strike"), strikes, Gold), (Loc.T("out_spare"), spares, Cyan), (Loc.T("out_open"), open, new Color(0.6f, 0.5f, 0.9f)),
        };
        int total = Mathf.Max(1, strikes + spares + open);
        var bar = new Rect(area.x, area.y + 4f, area.width, 34f);
        RoundTint(bar, new Color(1f, 1f, 1f, 0.06f));
        float x = bar.x;
        foreach (var (name, n, c) in parts)
        {
            if (n == 0) continue;
            float w = bar.width * n / total;
            RoundTint(new Rect(x, bar.y, w - 2f, bar.height), c);
            if (w > 44f) GUI.Label(new Rect(x, bar.y, w, bar.height), $"{100f * n / total:0}%", Small(new Color(0.1f, 0.05f, 0.15f), TextAnchor.MiddleCenter, 16));
            x += w;
        }
        Legend(new Rect(area.x, bar.yMax + 12f, area.width, 24f),
            ($"{parts[0].name} {strikes}", Gold), ($"{parts[1].name} {spares}", Cyan), ($"{parts[2].name} {open}", parts[2].c), ($"{Loc.T("out_gutter")} {gutters}", Bad));
        return r.yMax;
    }

    float ChartSpeeds(Rect r, int player)
    {
        var area = ChartCard(r, Loc.T("chart_speed"));
        var speeds = game.ThrowLog.Where(t => t.player == player).Select(t => t.speedKmh).ToList();
        if (speeds.Count == 0) return r.yMax;
        var plot = new Rect(area.x + 46f, area.y + 30f, area.width - 56f, area.height - 58f);
        float lo = Mathf.Floor(speeds.Min() - 2f), hi = Mathf.Ceil(speeds.Max() + 2f), avg = speeds.Average();
        PlotFrame(plot, lo, hi, 3, speeds.Count, i => speeds.Count <= 12 || i % 3 == 0 ? (i + 1).ToString() : null);
        Line(new Vector2(plot.x, YAt(plot, avg, lo, hi)), new Vector2(plot.xMax, YAt(plot, avg, lo, hi)), 2f, DimText, true);
        var pts = speeds.Select((s, i) => new Vector2(XAt(plot, i, speeds.Count), YAt(plot, s, lo, hi))).ToList();
        Polyline(pts, 3f, Cyan);
        int top = speeds.IndexOf(speeds.Max());
        for (int i = 0; i < pts.Count; i++) Dot(pts[i], i == top ? 13f : 8f, i == top ? Gold : Cyan);
        Legend(new Rect(area.x, area.y, area.width, 24f), ($"{Loc.T("legend_avg")} {avg:0.0} km/h", DimText), ($"{Loc.T("legend_top")} {speeds.Max():0.0}", Gold));
        return r.yMax;
    }

    float ChartAccuracy(Rect r, int player)
    {
        var area = ChartCard(r, Loc.T("chart_accuracy"));
        var firstBalls = game.ThrowLog.Where(t => t.player == player && t.standingBefore == 10).ToList();
        var map = new Rect(area.x + 10f, area.y, 110f, area.height);
        DrawLaneMap(map, firstBalls, null, false);
        float x = map.xMax + 24f, w = area.xMax - x, y = area.y;
        int reached = firstBalls.Count(t => t.reachedPins), pocket = firstBalls.Count(t => t.InPocket);
        float rate = reached == 0 ? 0f : (float)pocket / reached;
        GUI.Label(new Rect(x, y, w, 26f), Loc.T("acc_pocket_rate"), Small(DimText)); y += 26f;
        GUI.Label(new Rect(x, y, w, 44f), $"{rate * 100f:0}%", new GUIStyle(labelBold) { fontSize = Fs(36), normal = { textColor = rate >= 0.5f ? Good : Warn } }); y += 46f;
        GUI.Label(new Rect(x, y, w, 24f), Loc.F("acc_lifetime", PlayerStats.PocketRate * 100f), Small(DimText)); y += 30f;
        if (reached > 0)
        {
            float meanErr = firstBalls.Where(t => t.reachedPins).Average(t => t.PocketError) * 100f;
            string side = meanErr > 0.5f ? Loc.T("an_right") : meanErr < -0.5f ? Loc.T("an_left") : "";
            GUI.Label(new Rect(x, y, w, 24f), Loc.F("acc_mean_miss", Mathf.Abs(meanErr), side), Small(Color.white)); y += 34f;
        }
        // Ball shapes used, as small bars.
        var all = game.ThrowLog.Where(t => t.player == player).ToList();
        foreach (var (shape, name, color) in new[] { (ThrowRecord.Shape.Straight, Loc.T("shape_straight"), Color.white), (ThrowRecord.Shape.Left, Loc.T("shape_left"), Cyan), (ThrowRecord.Shape.Right, Loc.T("shape_right"), Pink) })
        {
            int n = all.Count(t => t.BallShape == shape);
            GUI.Label(new Rect(x, y, 80f, 26f), name, Small(DimText));
            var bar = new Rect(x + 84f, y + 6f, Mathf.Max(0f, w - 130f), 14f);
            RoundTint(bar, new Color(1f, 1f, 1f, 0.06f));
            if (all.Count > 0 && n > 0) RoundTint(new Rect(bar.x, bar.y, bar.width * n / all.Count, bar.height), color);
            GUI.Label(new Rect(bar.xMax + 6f, y, 40f, 26f), n.ToString(), Small(Color.white));
            y += 30f;
        }
        return r.yMax;
    }

    float ChartVsRecords(Rect r, int player)
    {
        var area = ChartCard(r, Loc.T("chart_records"));
        int score = game.Scores[player].RunningTotals()[^1] ?? 0;
        float avg = PlayerStats.AverageScore;
        int best = PlayerStats.BestScore;
        float max = Mathf.Max(1f, score, avg, best);
        float y = area.y;
        void Bar(string name, float value, Color c, string text)
        {
            GUI.Label(new Rect(area.x, y, 110f, 30f), name, Small(DimText));
            var bar = new Rect(area.x + 116f, y + 6f, area.width - 190f, 18f);
            RoundTint(bar, new Color(1f, 1f, 1f, 0.06f));
            RoundTint(new Rect(bar.x, bar.y, Mathf.Max(6f, bar.width * value / max), bar.height), c);
            GUI.Label(new Rect(bar.xMax + 8f, y, 70f, 30f), text, Small(Color.white, TextAnchor.MiddleLeft, 18));
            y += 36f;
        }
        Bar(Loc.T("rec_this_game"), score, BowlingGame.PlayerColors[player], score.ToString());
        Bar(Loc.T("rec_average"), avg, DimText, avg.ToString("0"));
        Bar(Loc.T("rec_best"), best, Gold, best.ToString());

        // Strike rate this game vs lifetime.
        var mine = game.ThrowLog.Where(t => t.player == player).ToList();
        float gameStrike = mine.Count == 0 ? 0f : (float)mine.Count(t => t.mark == "X") / mine.Count;
        y += 4f;
        GUI.Label(new Rect(area.x, y, area.width, 26f),
            Loc.F("rec_strike_rate", gameStrike * 100f, PlayerStats.StrikeRate * 100f), Small(gameStrike >= PlayerStats.StrikeRate ? Good : Warn, TextAnchor.MiddleLeft, 18));
        y += 30f;
        float diff = score - avg;
        if (PlayerStats.GamesPlayed > 1)
            GUI.Label(new Rect(area.x, y, area.width, 26f), Loc.F(diff >= 0f ? "rec_above_avg" : "rec_below_avg", Mathf.Abs(diff)), Small(diff >= 0f ? Good : Warn, TextAnchor.MiddleLeft, 18));
        return r.yMax;
    }

    // ---- Stats screen charts: recent games and lifetime outcomes ----

    float DrawLifetimeCharts(Rect r)
    {
        float y = r.y;
        var recent = PlayerStats.RecentScores;
        var card = new Rect(r.x, y, r.width, 250f);
        var area = ChartCard(card, Loc.T("chart_recent"));
        if (recent.Length == 0)
            GUI.Label(area, Loc.T("chart_no_games"), Small(DimText, TextAnchor.MiddleCenter));
        else
        {
            var plot = new Rect(area.x + 46f, area.y + 30f, area.width - 56f, area.height - 58f);
            float max = Mathf.Max(60f, Mathf.Ceil(recent.Max() * 1.1f / 30f) * 30f);
            PlotFrame(plot, 0f, max, 4, recent.Length, i => recent.Length <= 10 || i % 2 == 0 ? (i + 1).ToString() : null);
            float avg = PlayerStats.AverageScore;
            Line(new Vector2(plot.x, YAt(plot, avg, 0f, max)), new Vector2(plot.xMax, YAt(plot, avg, 0f, max)), 2f, DimText, true);
            var pts = recent.Select((s, i) => new Vector2(XAt(plot, i, recent.Length), YAt(plot, s, 0f, max))).ToList();
            Polyline(pts, 3.5f, Pink);
            for (int i = 0; i < pts.Count; i++) Dot(pts[i], recent[i] == PlayerStats.BestScore ? 13f : 8f, recent[i] == PlayerStats.BestScore ? Gold : Pink);
            Legend(new Rect(area.x, area.y, area.width, 24f), (Loc.T("legend_score"), Pink), ($"{Loc.T("legend_avg")} {avg:0}", DimText), (Loc.T("legend_best"), Gold));
        }
        y = card.yMax + 14f;

        card = new Rect(r.x, y, r.width, 170f);
        area = ChartCard(card, Loc.T("chart_lifetime"));
        int throws = Mathf.Max(1, PlayerStats.Throws);
        int other = Mathf.Max(0, PlayerStats.Throws - PlayerStats.Strikes - PlayerStats.Spares - PlayerStats.Gutters);
        var bar = new Rect(area.x, area.y + 4f, area.width, 34f);
        RoundTint(bar, new Color(1f, 1f, 1f, 0.06f));
        float x = bar.x;
        foreach (var (n, c) in new[] { (PlayerStats.Strikes, Gold), (PlayerStats.Spares, Cyan), (other, new Color(0.6f, 0.5f, 0.9f)), (PlayerStats.Gutters, Bad) })
        {
            if (n == 0) continue;
            float w = bar.width * n / throws;
            RoundTint(new Rect(x, bar.y, w - 2f, bar.height), c);
            if (w > 44f) GUI.Label(new Rect(x, bar.y, w, bar.height), $"{100f * n / throws:0}%", Small(new Color(0.1f, 0.05f, 0.15f), TextAnchor.MiddleCenter, 16));
            x += w;
        }
        Legend(new Rect(area.x, bar.yMax + 12f, area.width, 24f),
            (Loc.T("out_strike"), Gold), (Loc.T("out_spare"), Cyan), (Loc.T("out_other"), new Color(0.6f, 0.5f, 0.9f)), (Loc.T("out_gutter"), Bad));
        GUI.Label(new Rect(area.x, bar.yMax + 44f, area.width, 26f), Loc.F("acc_lifetime", PlayerStats.PocketRate * 100f), Small(Color.white, TextAnchor.MiddleLeft, 18));
        return card.yMax;
    }
}
