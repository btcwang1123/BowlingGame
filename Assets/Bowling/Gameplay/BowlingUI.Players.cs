using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Confirmation dialogs, the multiplayer line-up (name, calibration profile, look, ball and
// technique for each player), continuing a saved game, and the calibration profile manager
// (up to 8 profiles, so everyone sharing the phone keeps their own swing calibration).
public partial class BowlingUI
{
    // ---- Confirmation dialog ----

    class Confirm
    {
        public string title, text;
        public readonly List<(string label, Tone tone, System.Action action)> buttons = new();
    }

    Confirm confirm;

    // Ask before doing something that can't be undone. A Cancel button is added at the end.
    void Ask(string title, string text, params (string label, Tone tone, System.Action action)[] buttons)
    {
        confirm = new Confirm { title = title, text = text };
        confirm.buttons.AddRange(buttons);
        confirm.buttons.Add((Loc.T("cancel"), Tone.Neutral, null));
    }

    void DrawConfirm()
    {
        DimBackground(0.65f);
        var c = confirm;
        var col = Column(0f, 0f, 500f);
        float textH = Mathf.Max(70f, labelCenter.CalcHeight(new GUIContent(c.text), col.width) + 10f);
        float h = 64f + textH + 14f + c.buttons.Count * (TapH + 10f);
        float y = Mathf.Max(20f + safeTop, (H - h) / 2f);
        GUI.Box(new Rect(col.x - 24f, y - 20f, col.width + 48f, h + 34f), GUIContent.none, panel);
        FitLabel(new Rect(col.x, y, col.width, 56f), c.title, title); y += 64f;
        FitLabel(new Rect(col.x, y, col.width, textH), c.text, labelCenter); y += textH + 14f;
        foreach (var (label, tone, action) in c.buttons)
        {
            var r = new Rect(col.x, y, col.width, TapH);
            if (GUI.Button(r, label, Fit(r, label, Btn(tone, true))))
            {
                confirm = null;
                action?.Invoke();
                return;
            }
            y += TapH + 10f;
        }
    }

    // ---- Picking players ----

    // The line-up being edited on the setup screen.
    readonly List<SaveData.PlayerSlot> lineUp = new();
    float setupHeight = 1200f;
    GUIStyle textField;

    void Players()
    {
        float y = Header(Loc.T("play"));
        if (BowlingGame.HasSavedMatch)
        {
            if (Button(Column(y, 64f), Loc.F("continue_match", BowlingGame.SavedMatch.savedAt), Btn(Tone.Green))) ResumeSaved();
            y += 80f;
        }
        GUI.Label(Column(y, 36f), Loc.T("how_many_players"), labelCenter); y += 54f;
        var tones = new[] { Tone.Primary, Tone.Cyan, Tone.Gold, Tone.Green };
        for (int n = 1; n <= 4; n++)
        {
            int players = n;
            if (Button(Column(y, 68f), Loc.F("n_players", n), Btn(tones[n - 1])))
            {
                if (players == 1) StartLineUp(new List<SaveData.PlayerSlot> { BowlingGame.DefaultSlot(0, 1) });
                else
                {
                    lineUp.Clear();
                    for (int i = 0; i < players; i++) lineUp.Add(BowlingGame.DefaultSlot(i, players));
                    Go(Screen.PlayerSetup);
                }
            }
            y += 80f;
        }
        GUI.Label(Column(y, 60f), Loc.T("players_hint"), new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleCenter });
    }

    void ResumeSaved() => WithSafety(() => { if (!game.ResumeMatch()) Go(Screen.Home); });

    // Start a new standard game; if one is saved, ask before replacing it.
    void StartLineUp(List<SaveData.PlayerSlot> players)
    {
        void Begin() => WithSafety(() =>
        {
            if (players.Count > 1)
            {
                // Remember the line-up for next time.
                var saved = SaveSystem.Data.playerSlots;
                for (int i = 0; i < players.Count; i++)
                {
                    if (i < saved.Count) saved[i] = players[i];
                    else saved.Add(players[i]);
                }
                SaveSystem.Save();
            }
            game.StartStandard(players);
        });
        if (!BowlingGame.HasSavedMatch) { Begin(); return; }
        Ask(Loc.T("overwrite_title"), Loc.F("overwrite_text", BowlingGame.SavedMatch.savedAt),
            (Loc.T("new_match"), Tone.Danger, () => { game.DiscardSavedMatch(); Begin(); }),
            (Loc.T("continue_saved"), Tone.Green, ResumeSaved));
    }

    void PlayerSetup()
    {
        float y = Header(Loc.F("setup_title", lineUp.Count));
        var col = Column(y, 0f, 700f);
        var view = new Rect(col.x, y, col.width, H - y - 10f - safeBottom);
        BeginScroll(view, setupHeight);
        float cw = col.width - 20f, ry = 0f;
        GUI.Label(new Rect(0f, ry, cw, 50f), Loc.T("setup_hint"), new GUIStyle(labelSmall) { alignment = TextAnchor.UpperCenter }); ry += 56f;

        var outfits = Outfits.All.Where(Outfits.Owns).ToList();
        var balls = ProShop.Balls.Where(ProShop.Owns).ToList();
        for (int p = 0; p < lineUp.Count; p++)
        {
            var slot = lineUp[p];
            const float rowH = 66f;
            var card = new Rect(0f, ry, cw, 70f + 4 * rowH + 8f);
            GUI.Box(card, GUIContent.none, panelLight);
            RoundTint(new Rect(card.x + 8f, card.y + 10f, 6f, card.height - 20f), BowlingGame.PlayerColors[p]);

            float x = card.x + 26f, w = card.width - 44f, cy = card.y + 12f;
            GUI.Label(new Rect(x, cy, 70f, 48f), $"P{p + 1}", new GUIStyle(labelBold) { fontSize = Fs(26), normal = { textColor = BowlingGame.PlayerColors[p] } });
            slot.name = NameField(new Rect(x + 74f, cy, w - 74f, 48f), slot.name, Loc.F("name_placeholder", p + 1));
            cy += 62f;

            int prof = Mathf.Clamp(slot.profile, 0, GameSettings.Profiles.Count - 1);
            string profText = GameSettings.ProfileName(prof) + (GameSettings.ProfileCalibrated(prof) ? "  ✓" : "  · " + Loc.T("not_calibrated"))
                + "  · " + (GameSettings.Profiles[prof].leftHanded ? Loc.T("left_hand") : Loc.T("right_hand"));
            int d = Cycler(new Rect(x, cy, w, rowH - 8f), Loc.T("profile"), profText, null);
            if (d != 0) slot.profile = (prof + d + GameSettings.Profiles.Count) % GameSettings.Profiles.Count;
            cy += rowH;

            var outfit = string.IsNullOrEmpty(slot.outfit) ? Outfits.Equipped : Outfits.Find(slot.outfit);
            int oi = Mathf.Max(0, outfits.IndexOf(outfit));
            d = Cycler(new Rect(x, cy, w, rowH - 8f), Loc.T("setup_look"), outfit.Name, Outfits.Preview(outfit));
            if (d != 0 && outfits.Count > 0) slot.outfit = outfits[(oi + d + outfits.Count) % outfits.Count].id;
            cy += rowH;

            var ballDesign = string.IsNullOrEmpty(slot.ball) ? ProShop.Equipped : ProShop.Find(slot.ball);
            int bi = Mathf.Max(0, balls.IndexOf(ballDesign));
            d = Cycler(new Rect(x, cy, w, rowH - 8f), Loc.T("setup_ball"), ballDesign.Name, ProShop.Preview(ballDesign));
            if (d != 0 && balls.Count > 0) slot.ball = balls[(bi + d + balls.Count) % balls.Count].id;
            cy += rowH;

            var tech = (BallTechnique)Mathf.Clamp(slot.technique, 0, BallTechniques.All.Length - 1);
            d = Cycler(new Rect(x, cy, w, rowH - 8f), Loc.T("technique"), BallTechniques.Name(tech), null);
            if (d != 0) slot.technique = (int)(d > 0 ? BallTechniques.Next(tech) : BallTechniques.All[((int)tech + BallTechniques.All.Length - 1) % BallTechniques.All.Length]);

            ry += card.height + 14f;
        }
        if (GUI.Button(new Rect(0f, ry, cw, 72f), Loc.T("start"), Btn(Tone.Primary)))
            StartLineUp(lineUp.Select(s => new SaveData.PlayerSlot { name = s.name, profile = s.profile, outfit = s.outfit, ball = s.ball, technique = s.technique }).ToList());
        ry += 90f;
        setupHeight = ry;
        GUI.EndScrollView();
    }

    // "label   ‹  value  ›": returns -1 or +1 when an arrow is tapped.
    int Cycler(Rect r, string name, string value, Texture2D icon)
    {
        float lw = Mathf.Min(130f, r.width * 0.26f), bw = 58f;
        FitLabel(new Rect(r.x, r.y, lw, r.height), name, new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleLeft });
        int d = 0;
        if (GUI.Button(new Rect(r.x + lw, r.y, bw, r.height), "‹", buttonSmall)) d = -1;
        if (GUI.Button(new Rect(r.xMax - bw, r.y, bw, r.height), "›", buttonSmall)) d = 1;
        var mid = new Rect(r.x + lw + bw + 6f, r.y, r.width - lw - 2f * bw - 12f, r.height);
        RoundTint(mid, new Color(0f, 0f, 0f, 0.25f));
        float tx = mid.x + 10f;
        if (icon != null)
        {
            float ih = mid.height - 8f, iw = ih * icon.width / icon.height;
            GUI.DrawTexture(new Rect(mid.x + 8f, mid.y + 4f, iw, ih), icon);
            tx += iw + 6f;
        }
        FitLabel(new Rect(tx, mid.y, mid.xMax - tx - 8f, mid.height), value, new GUIStyle(label) { alignment = TextAnchor.MiddleLeft, wordWrap = false }, 0.55f);
        return d;
    }

    // A rounded text box for names (the phone's keyboard opens when it's tapped).
    string NameField(Rect r, string value, string placeholder)
    {
        if (textField == null)
        {
            textField = new GUIStyle(GUI.skin.textField) { font = label.font, fontSize = 24, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 6, 6), border = new RectOffset(14, 14, 14, 14) };
            textField.normal.background = textField.hover.background = Rounded(new Color(0.05f, 0.04f, 0.12f), new Color(0.03f, 0.02f, 0.08f), new Color(0.45f, 0.38f, 0.75f), 48, 14, 1.5f);
            textField.focused.background = Rounded(new Color(0.08f, 0.06f, 0.18f), new Color(0.05f, 0.03f, 0.12f), Cyan, 48, 14, 2f);
            textField.normal.textColor = textField.hover.textColor = textField.focused.textColor = Color.white;
            scalable.Add((textField, 24));
            textField.fontSize = Fs(24);
        }
        string v = GUI.TextField(r, value ?? "", 12, textField);
        if (string.IsNullOrEmpty(v) && Painting)
            GUI.Label(new Rect(r.x + 16f, r.y, r.width - 32f, r.height), placeholder, new GUIStyle(labelSmall) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(1f, 1f, 1f, 0.35f) } });
        return v;
    }

    // ---- Calibration profiles ----

    float profilesHeight = 1400f;

    void Calibrate()
    {
        float y = Header(Loc.T("calib_profiles"));
        var col = Column(y, 0f, 640f);
        var view = new Rect(col.x, y, col.width, H - y - 10f - safeBottom);
        BeginScroll(view, profilesHeight);
        float cw = col.width - 20f, ry = 0f;
        GUI.Label(new Rect(0f, ry, cw, 54f), Loc.F("calib_profiles_hint", GameSettings.MaxProfiles), new GUIStyle(labelSmall) { alignment = TextAnchor.UpperLeft }); ry += 60f;

        // The list: tap a row to use that profile.
        int active = GameSettings.DefaultProfile;
        var profiles = GameSettings.Profiles;
        for (int i = 0; i < profiles.Count; i++)
        {
            var r = new Rect(0f, ry, cw, 74f);
            bool on = i == active;
            if (GUI.Button(r, GUIContent.none, on ? buttonOn : buttonSmall)) GameSettings.DefaultProfile = i;
            Dot(new Vector2(r.x + 30f, r.center.y), 22f, on ? Color.white : new Color(1f, 1f, 1f, 0.25f));
            if (on) Dot(new Vector2(r.x + 30f, r.center.y), 12f, new Color(0.03f, 0.4f, 0.58f));
            FitLabel(new Rect(r.x + 54f, r.y + 6f, r.width - 70f, 34f), GameSettings.ProfileName(i), new GUIStyle(labelBold) { fontSize = Fs(22), wordWrap = false });
            string status = (profiles[i].calibrated ? $"<color=#{Hex(Good)}>✓ {Loc.T("calibrated")}</color>" : Loc.T("not_calibrated"))
                + " · " + (profiles[i].leftHanded ? Loc.T("left_hand") : Loc.T("right_hand"));
            FitLabel(new Rect(r.x + 54f, r.y + 38f, r.width - 70f, 28f), status, new GUIStyle(labelSmall) { wordWrap = false, normal = { textColor = new Color(0.85f, 0.88f, 1f) } });
            ry += 82f;
        }
        if (profiles.Count < GameSettings.MaxProfiles)
        {
            if (GUI.Button(new Rect(0f, ry, cw, TapHSmall), Loc.F("profile_add", profiles.Count, GameSettings.MaxProfiles), Btn(Tone.Violet, true)))
            {
                int added = GameSettings.AddProfile();
                if (added >= 0) GameSettings.DefaultProfile = added;
            }
            ry += TapHSmall + 18f;
        }
        else ry += 8f;

        // The selected profile: name, hand, calibration, and what to do with it.
        active = GameSettings.DefaultProfile;
        var prof = profiles[active];
        float panelTop = ry;
        GUI.Box(new Rect(0f, ry, cw, profilePanelH), GUIContent.none, panel);
        float x = 18f, w = cw - 36f;
        ry += 14f;
        GUI.Label(new Rect(x, ry, w, 30f), Loc.T("profile_name"), labelSmall); ry += 32f;
        string newName = NameField(new Rect(x, ry, w, 52f), prof.name, GameSettings.ProfileName(active));
        if (newName != prof.name) GameSettings.RenameProfile(active, newName);
        ry += 64f;
        float half = (w - 12f) / 2f;
        GUI.Label(new Rect(x, ry, w, 30f), Loc.T("hand"), labelSmall); ry += 32f;
        if (GUI.Button(new Rect(x, ry, half, TapHSmall), Loc.T("right_hand"), prof.leftHanded ? buttonSmall : buttonOn)) { prof.leftHanded = false; SaveSystem.Save(); }
        if (GUI.Button(new Rect(x + half + 12f, ry, half, TapHSmall), Loc.T("left_hand"), prof.leftHanded ? buttonOn : buttonSmall)) { prof.leftHanded = true; SaveSystem.Save(); }
        ry += TapHSmall + 16f;

        if (prof.calibrated && prof.swingAmplitudeDeg > 0f)
        {
            GUI.Label(new Rect(x, ry, w, 28f), Loc.T("calib_current"), new GUIStyle(labelBold) { normal = { textColor = Gold } }); ry += 30f;
            string numbers = Loc.F("calib_profile", prof.swingReference * Mathf.Rad2Deg, prof.swingAmplitudeDeg, prof.swingAccel * Mathf.Rad2Deg, prof.spinCalibrated ? "✓" : "—");
            var ns = new GUIStyle(labelSmall) { alignment = TextAnchor.UpperLeft };
            float nh = ns.CalcHeight(new GUIContent(numbers), w);
            GUI.Label(new Rect(x, ry, w, nh), numbers, ns); ry += nh + 12f;
        }
        else { GUI.Label(new Rect(x, ry, w, 30f), Loc.T("profile_uncalibrated"), new GUIStyle(labelSmall) { normal = { textColor = Warn } }); ry += 40f; }

        if (!MotionInput.Available)
        {
            GUI.Label(new Rect(x, ry, w, 60f), Loc.T("control_motion_unavailable"), new GUIStyle(labelSmall) { alignment = TextAnchor.UpperLeft }); ry += 66f;
        }
        else
        {
            if (GUI.Button(new Rect(x, ry, w, 70f), prof.calibrated ? Loc.T("recalibrate") : Loc.T("calib_start"), Btn(Tone.Gold)))
                WithSafety(() => game.StartCalibration());
            ry += 82f;
        }
        string pname = GameSettings.ProfileName(active);
        if (GUI.Button(new Rect(x, ry, half, TapHSmall), Loc.T("profile_reset"), buttonSmall))
            Ask(Loc.T("profile_reset"), Loc.F("profile_reset_text", pname), (Loc.T("profile_reset"), Tone.Danger, () => GameSettings.ResetProfile(active)));
        GUI.enabled = profiles.Count > 1;
        if (GUI.Button(new Rect(x + half + 12f, ry, half, TapHSmall), Loc.T("profile_delete"), Btn(Tone.Danger, true)))
            Ask(Loc.T("profile_delete"), Loc.F("profile_delete_text", pname), (Loc.T("profile_delete"), Tone.Danger, () => GameSettings.DeleteProfile(active)));
        GUI.enabled = true;
        ry += TapHSmall + 16f;
        profilePanelH = ry - panelTop;
        ry += 20f;

        // How calibration works.
        GUI.Label(new Rect(0f, ry, cw, 32f), Loc.T("calib_how"), new GUIStyle(labelBold) { fontSize = Fs(22), normal = { textColor = Gold } }); ry += 38f;
        var intro = new GUIStyle(labelSmall) { alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white } };
        float ih = intro.CalcHeight(new GUIContent(Loc.T("calib_intro")), cw);
        GUI.Label(new Rect(0f, ry, cw, ih), Loc.T("calib_intro"), intro); ry += ih + 10f;
        float chipW = (cw - 5 * 8f) / 6f;
        for (int i = 0; i < CalibrationSession.Steps.Length; i++)
        {
            var kind = CalibrationSession.Steps[i];
            GUI.backgroundColor = KindColor(kind) * 0.6f;
            var cr = new Rect(i * (chipW + 8f), ry, chipW, 40f);
            GUI.Box(cr, KindShort(kind), Fit(cr, KindShort(kind), chip));
        }
        GUI.backgroundColor = Color.white;
        ry += 54f;
        float mh = labelSmall.CalcHeight(new GUIContent(Loc.T("calib_measures")), cw);
        GUI.Label(new Rect(0f, ry, cw, mh), Loc.T("calib_measures"), new GUIStyle(labelSmall) { alignment = TextAnchor.UpperLeft }); ry += mh + 20f;
        profilesHeight = ry;
        GUI.EndScrollView();
    }
    float profilePanelH = 520f;
}
