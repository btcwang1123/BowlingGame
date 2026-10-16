using System.Collections.Generic;

// All on-screen text in Traditional Chinese and English. Loc.T("key") picks the current language
// (GameSettings.Language); Loc.F("key", args) also fills in {0}, {1}… like string.Format.
public static class Loc
{
    static readonly Dictionary<string, (string zh, string en)> Text = new()
    {
        // Home and menus
        ["tagline"] = ("揮動手機，打出全倒！", "Swing your phone. Knock 'em all down."),
        ["play"] = ("開始遊戲", "Play"),
        ["practice"] = ("練習", "Practice"),
        ["challenges_stars"] = ("挑戰關卡　★ {0}/{1}", "Challenges　★ {0}/{1}"),
        ["stats"] = ("統計", "Stats"),
        ["settings"] = ("設定", "Settings"),
        ["back"] = ("‹ 返回", "‹ Back"),
        ["start"] = ("開始", "Start"),
        ["controls"] = ("操作方式", "Controls"),
        ["control_swipe"] = ("滑動", "Swipe"),
        ["control_motion"] = ("體感揮臂", "Swing the phone"),
        ["control_motion_unavailable"] = ("這台裝置沒有陀螺儀，體感模式無法使用，會改用滑動操作。", "This device has no gyroscope, so swipe controls are used instead."),
        ["how_many_players"] = ("幾位玩家？", "How many players?"),
        ["n_players"] = ("{0} 位玩家", "{0} player(s)"),
        ["players_hint"] = ("多人時輪流傳手機，每局換人。", "Pass the phone around: players take turns each frame."),
        ["practice_pick"] = ("選擇要練習的球瓶", "Pick what to practise"),
        ["leave_full"] = ("全排 10 支", "Full rack"),
        ["leave_random"] = ("隨機殘瓶", "Random leave"),
        ["change_leave"] = ("換一組", "Next leave"),
        ["practice_stats"] = ("{0} 球，清空率 {1}", "{0} balls, {1} cleared"),
        ["locked"] = ("未解鎖", "Locked"),
        ["balls_allowed"] = ("可以投 {0} 球", "{0} balls allowed"),
        ["star_rule"] = ("★★★ {0} 球內完成　★★ {1} 球內完成", "★★★ within {0} ball(s)　★★ within {1}"),

        // In game
        ["hint_swipe"] = ("左右拖曳選位置，往上快速滑動丟球；滑出弧線可以打曲球", "Drag left/right to pick your spot, then flick up to bowl. Curve the flick to hook."),
        ["hint_motion"] = ("握緊手機，拇指按住螢幕，往後擺再往前揮，在最低點放開拇指", "Grip the phone, hold your thumb on the screen, swing back then forward, and lift your thumb at the bottom."),
        ["hint_motion_swing"] = ("揮臂中…往前揮到底時放開拇指！", "Swinging… lift your thumb as your arm comes through!"),
        ["hint_swipe_short"] = ("左右拖曳選位置，再往上滑動丟球", "Drag to aim, flick up to bowl"),
        ["hint_swipe_faster"] = ("再滑快一點！", "Flick up faster!"),
        ["swing_too_short"] = ("揮臂太短了，請做完整的動作", "Too short — make a full swing"),
        ["swing_too_slow"] = ("揮得太慢了，用力一點！", "Too slow — swing harder!"),
        ["no_backswing"] = ("要先往後擺，再往前揮", "Swing back first, then forward"),
        ["throw_info"] = ("出手 {0:0} km/h · 角度 {1:+0.0;-0.0;0.0}° · {2}", "Release {0:0} km/h · angle {1:+0.0;-0.0;0.0}° · {2}"),
        ["spin_right"] = ("右曲球", "hook right"),
        ["spin_left"] = ("左曲球", "hook left"),
        ["spin_none"] = ("直球", "straight"),
        ["vs_last"] = ("（比上一球 {0:+0;-0;±0} km/h）", " ({0:+0;-0;±0} km/h vs last)"),
        ["foul"] = ("犯規！踩線了", "FOUL! Over the line"),
        ["replay"] = ("重播", "Replay"),
        ["replay_label"] = ("重播", "REPLAY"),
        ["strike"] = ("全倒！", "STRIKE!"),
        ["spare"] = ("補中！", "SPARE!"),
        ["gutter"] = ("洗溝…", "Gutter…"),
        ["pins_down"] = ("{0} 支", "{0} pins"),
        ["player_turn"] = ("輪到 P{0}", "P{0}'s turn"),
        ["challenge_clear"] = ("過關！", "CLEARED!"),
        ["challenge_fail"] = ("挑戰失敗", "Not this time"),
        ["balls_used"] = ("用了 {0} / {1} 球", "Used {0} of {1} balls"),
        ["next_level"] = ("下一關", "Next level"),
        ["retry"] = ("再試一次", "Try again"),
        ["game_over"] = ("比賽結束", "GAME OVER"),
        ["new_best"] = ("★ 新紀錄！★", "★ New best score! ★"),
        ["best_score"] = ("最高分：{0}", "Best: {0}"),
        ["game_summary"] = ("全倒 {0}　補中 {1}　洗溝 {2}", "Strikes {0}   Spares {1}   Gutters {2}"),
        ["fastest_ball"] = ("最快球速 {0:0} km/h", "Fastest ball {0:0} km/h"),
        ["play_again"] = ("再玩一場", "Play again"),
        ["paused"] = ("暫停", "Paused"),
        ["resume"] = ("繼續", "Resume"),
        ["restart"] = ("重新開始", "Restart"),
        ["main_menu"] = ("回主選單", "Main menu"),

        // Stats
        ["stat_games"] = ("比賽場數", "Games played"),
        ["stat_best"] = ("最高分", "Best score"),
        ["stat_average"] = ("平均分數", "Average score"),
        ["stat_throws"] = ("總投球數", "Balls thrown"),
        ["stat_strikes"] = ("全倒", "Strikes"),
        ["stat_spares"] = ("補中", "Spares"),
        ["stat_gutters"] = ("洗溝", "Gutters"),
        ["stat_top_speed"] = ("最快球速", "Fastest ball"),
        ["stat_stars"] = ("挑戰星星", "Challenge stars"),

        // Settings
        ["motion_grip_hint"] = ("像拿保齡球一樣握住手機，螢幕朝上、手機頂端朝前。拇指按住螢幕代表握球，放開代表出手。", "Hold the phone like a bowling ball: screen up, top pointing forward. Thumb on the screen = holding the ball; lift it to release."),
        ["calibrate"] = ("揮臂校正", "Calibrate swing"),
        ["recalibrate"] = ("重新校正揮臂", "Recalibrate swing"),
        ["sensitivity"] = ("體感力道", "Swing power"),
        ["aim_direction"] = ("左右方向", "Aim direction"),
        ["spin_direction"] = ("旋轉方向", "Spin direction"),
        ["normal"] = ("正常", "Normal"),
        ["reversed"] = ("反轉", "Reversed"),
        ["hand"] = ("慣用手", "Bowling hand"),
        ["ball_weight"] = ("球的重量", "Ball weight"),
        ["lb"] = ("{0} 磅", "{0} lb"),
        ["right_hand"] = ("右手", "Right"),
        ["left_hand"] = ("左手", "Left"),
        ["volume"] = ("音量", "Volume"),
        ["vibration"] = ("震動", "Vibration"),
        ["aim_guide"] = ("瞄準線", "Aim guide"),
        ["language"] = ("語言", "Language"),
        ["on"] = ("開", "On"),
        ["off"] = ("關", "Off"),

        // Calibration and safety
        ["calib_instructions"] = ("用你覺得舒服的力道揮臂 3 次。\n拇指按住螢幕 → 往後擺 → 往前揮 → 放開拇指。", "Make 3 comfortable swings.\nThumb on screen → swing back → swing forward → lift thumb."),
        ["calib_progress"] = ("{0} / 3", "{0} / 3"),
        ["calib_got"] = ("收到！揮臂速度 {0:0.0} rad/s", "Got it! Swing speed {0:0.0} rad/s"),
        ["calib_done"] = ("校正完成，可以開始玩了！", "All set — go bowl!"),
        ["safety_title"] = ("安全提醒", "Safety first"),
        ["safety_text"] = ("・請握緊手機，建議使用手機吊繩。\n・確認周圍沒有人、寵物或易碎物品。\n・揮臂時不要真的把手機丟出去！\n・用自己舒服的力道就好，不需要用全力。", "• Grip the phone firmly; a wrist strap is a good idea.\n• Make sure nobody, no pets and nothing fragile is nearby.\n• Don't actually let go of the phone!\n• A comfortable swing is plenty — no need to go all out."),
        ["understood"] = ("我知道了", "Got it"),
    };

    public static string T(string key)
    {
        if (!Text.TryGetValue(key, out var t)) return key;
        return GameSettings.Language == "zh" ? t.zh : t.en;
    }

    public static string F(string key, params object[] args) => string.Format(T(key), args);
}
