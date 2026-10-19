using UnityEngine;

// Settings as a draft (Save in the top-right, or a "save changes?" prompt on the way out), the
// sound and music pickers, and the "tap to continue" prompt after a throw.
public partial class BowlingUI
{
    // ---- Draft editing ----

    bool SettingsDirty => settingsSnapshot != null && GameSettings.ChangedSince(settingsSnapshot);

    void BeginSettingsEdit()
    {
        settingsSnapshot = GameSettings.Snapshot();
        SaveSystem.BeginDeferred();
    }

    void SaveSettingsEdit(bool keepEditing)
    {
        SaveSystem.EndDeferred();
        SaveSystem.Save();
        savedToastTime = Time.unscaledTime;
        settingsSnapshot = null;
        if (keepEditing) BeginSettingsEdit(); // stay on the screen with a fresh draft
    }

    void DiscardSettingsEdit()
    {
        if (settingsSnapshot == null) return;
        bool changed = GameSettings.ChangedSince(settingsSnapshot);
        if (changed) GameSettings.Restore(settingsSnapshot);
        settingsSnapshot = null;
        SaveSystem.EndDeferred();
        if (!changed) return;
        // Undo the side effects the edits had while previewing.
        Loc.NotifyLanguageChanged();
        game.ApplyAudio();
    }

    // Leave Settings, asking first if there are unsaved changes.
    void LeaveSettings(System.Action then)
    {
        if (SettingsDirty) { leaveAction = then; return; }
        DiscardSettingsEdit();
        then();
    }

    void DrawLeaveSettingsPrompt()
    {
        DimBackground(0.6f);
        var col = Column(H * 0.26f, 0f, 480f);
        float y = col.y;
        GUI.Box(new Rect(col.x - 24f, y - 20f, col.width + 48f, 380f), GUIContent.none, panel);
        GUI.Label(new Rect(col.x, y, col.width, 56f), Loc.T("unsaved_title"), title); y += 64f;
        GUI.Label(new Rect(col.x, y, col.width, 70f), Loc.T("unsaved_text"), labelCenter); y += 84f;
        var then = leaveAction;
        if (GUI.Button(new Rect(col.x, y, col.width, 68f), Loc.T("save_and_leave"), Btn(Tone.Green)))
        {
            leaveAction = null;
            SaveSettingsEdit(keepEditing: false);
            then();
            return;
        }
        y += 78f;
        float half = col.width / 2f - 6f;
        if (GUI.Button(new Rect(col.x, y, half, TapH), Loc.T("discard_changes"), Btn(Tone.Danger, true)))
        {
            leaveAction = null;
            DiscardSettingsEdit();
            then();
        }
        else if (GUI.Button(new Rect(col.x + half + 12f, y, half, TapH), Loc.T("keep_editing"), buttonSmall)) leaveAction = null;
    }

    // ---- Sound and music ----

    static readonly SoundPack[] Packs = { SoundPack.Alley, SoundPack.Neon, SoundPack.Arcade, SoundPack.Cartoon };

    // Volume, sound pack and background music. Returns the y below it.
    float SettingsAudio(float ry, float cw)
    {
        GUI.Label(new Rect(0f, ry, cw, 34f), Loc.T("audio_section"), new GUIStyle(labelBold) { fontSize = Fs(22), normal = { textColor = Gold } }); ry += 40f;

        GUI.Label(new Rect(0f, ry, cw, 30f), $"{Loc.T("volume")}: {GameSettings.Volume * 100f:0}%", labelSmall); ry += 30f;
        float vol = BigSlider(new Rect(0f, ry, cw, 44f), GameSettings.Volume, 0f, 1f); ry += 58f;
        if (!Mathf.Approximately(vol, GameSettings.Volume)) GameSettings.Volume = vol;

        // Sound effects: four packs, each previewed when picked.
        GUI.Label(new Rect(0f, ry, cw, 30f), Loc.T("sound_pack"), labelSmall); ry += 32f;
        float half = (cw - 12f) / 2f;
        for (int i = 0; i < Packs.Length; i++)
        {
            var pack = Packs[i];
            var r = new Rect((i % 2) * (half + 12f), ry + (i / 2) * (TapH + 10f), half, TapH);
            bool on = GameSettings.Sounds == pack;
            string name = Loc.T("pack_" + pack.ToString().ToLowerInvariant());
            if (GUI.Button(r, on ? $"♪ {name}" : name, on ? buttonOn : buttonSmall))
            {
                GameSettings.Sounds = pack;
                if (BowlingAudio.Instance != null) BowlingAudio.Instance.Preview();
            }
        }
        ry += 2 * (TapH + 10f);
        GUI.Label(new Rect(0f, ry, cw, 30f), Loc.T("pack_" + GameSettings.Sounds.ToString().ToLowerInvariant() + "_desc"), Small(DimText, TextAnchor.MiddleLeft, 16)); ry += 40f;

        // Background music: off, match the venue, or one style for every venue.
        GUI.Label(new Rect(0f, ry, cw, 30f), Loc.T("music"), labelSmall); ry += 32f;
        int count = MusicSynth.Styles.Length + 2;
        for (int i = 0; i < count; i++)
        {
            var r = new Rect((i % 2) * (half + 12f), ry + (i / 2) * (TapHSmall + 10f), half, TapHSmall);
            bool on = GameSettings.MusicTrack == i;
            string name = i == GameSettings.MusicOff ? Loc.T("music_off")
                : i == GameSettings.MusicAuto ? Loc.T("music_auto")
                : MusicSynth.Styles[i - 2].Name;
            if (GUI.Button(r, on && i != GameSettings.MusicOff ? $"♪ {name}" : name, on ? buttonOn : buttonSmall))
            {
                GameSettings.MusicTrack = i;
                if (BowlingMusic.Instance != null) BowlingMusic.Instance.Refresh();
            }
        }
        ry += Mathf.CeilToInt(count / 2f) * (TapHSmall + 10f);
        var playing = BowlingMusic.WantedStyle();
        string now = playing == null ? Loc.T("music_off") : playing.Name;
        GUI.Label(new Rect(0f, ry, cw, 30f), GameSettings.MusicTrack == GameSettings.MusicAuto
            ? Loc.F("music_auto_now", Venues.All[GameSettings.Venue].Name, now) : Loc.F("music_now", now), Small(DimText, TextAnchor.MiddleLeft, 16));
        ry += 36f;
        if (GameSettings.MusicTrack != GameSettings.MusicOff)
        {
            GUI.Label(new Rect(0f, ry, cw, 30f), $"{Loc.T("music_volume")}: {GameSettings.MusicVolume * 100f:0}%", labelSmall); ry += 30f;
            float mv = BigSlider(new Rect(0f, ry, cw, 44f), GameSettings.MusicVolume, 0f, 1f); ry += 58f;
            if (!Mathf.Approximately(mv, GameSettings.MusicVolume)) GameSettings.MusicVolume = mv;
        }
        ry += 8f;
        return ry;
    }

    // ---- After a throw: wait for a tap ----

    void DrawTapToContinue()
    {
        float tw = Mathf.Min(440f, W - 32f);
        var r = new Rect((W - tw) / 2f, H * 0.28f + 140f, tw, 52f);
        float pulse = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
        GUI.backgroundColor = new Color(0.1f, 0.05f, 0.25f, 0.85f);
        GUI.Box(r, GUIContent.none, chip);
        GUI.backgroundColor = Color.white;
        FitLabel(r, Loc.T("tap_to_continue"), new GUIStyle(labelBold) { fontSize = Fs(24), alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 1f, 1f, pulse) } });
    }
}
