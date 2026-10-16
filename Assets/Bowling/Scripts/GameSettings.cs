using UnityEngine;

public enum ControlMode { Swipe, Motion }

// Player preferences, saved in PlayerPrefs as soon as they change.
public static class GameSettings
{
    public static ControlMode Control
    {
        get => (ControlMode)PlayerPrefs.GetInt("set.control", 0);
        set => SetInt("set.control", (int)value);
    }

    public static bool LeftHanded
    {
        get => PlayerPrefs.GetInt("set.left", 0) == 1;
        set => SetInt("set.left", value ? 1 : 0);
    }

    public static float Volume
    {
        get => PlayerPrefs.GetFloat("set.volume", 0.8f);
        set { PlayerPrefs.SetFloat("set.volume", Mathf.Clamp01(value)); AudioListener.volume = Mathf.Clamp01(value); }
    }

    // "zh" (Traditional Chinese) or "en". Defaults to the device language.
    public static string Language
    {
        get => PlayerPrefs.GetString("set.lang", DefaultLanguage());
        set { PlayerPrefs.SetString("set.lang", value); PlayerPrefs.Save(); }
    }

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt("set.vibrate", 1) == 1;
        set => SetInt("set.vibrate", value ? 1 : 0);
    }

    public static bool AimGuide
    {
        get => PlayerPrefs.GetInt("set.aimguide", 1) == 1;
        set => SetInt("set.aimguide", value ? 1 : 0);
    }

    // Multiplies motion-control ball speed (0.7 – 1.3).
    public static float MotionSensitivity
    {
        get => PlayerPrefs.GetFloat("set.sens", 1f);
        set { PlayerPrefs.SetFloat("set.sens", Mathf.Clamp(value, 0.7f, 1.3f)); PlayerPrefs.Save(); }
    }

    // The player's comfortable swing speed (rad/s at release), measured by calibration.
    public static float SwingReference
    {
        get => PlayerPrefs.GetFloat("set.swingref", 8f);
        set { PlayerPrefs.SetFloat("set.swingref", Mathf.Clamp(value, 3f, 20f)); PlayerPrefs.Save(); }
    }

    // Ball weight in pounds (8–16). Heavier hits harder but leaves the hand a little slower.
    public static int BallWeightLb
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("set.ballweight", 13), 8, 16);
        set => SetInt("set.ballweight", Mathf.Clamp(value, 8, 16));
    }

    public static float BallMassKg => BallWeightLb * 0.4536f;

    // Release speed multiplier for the ball weight: 8 lb is 6% faster, 16 lb 6% slower.
    public static float BallSpeedFactor => Mathf.Lerp(1.06f, 0.94f, (BallWeightLb - 8) / 8f);

    public static bool Calibrated
    {
        get => PlayerPrefs.GetInt("set.calibrated", 0) == 1;
        set => SetInt("set.calibrated", value ? 1 : 0);
    }

    // Sensor axes differ between phones; these let a player fix a reversed direction or spin.
    public static bool FlipAim
    {
        get => PlayerPrefs.GetInt("set.flipaim", 0) == 1;
        set => SetInt("set.flipaim", value ? 1 : 0);
    }

    public static bool FlipSpin
    {
        get => PlayerPrefs.GetInt("set.flipspin", 0) == 1;
        set => SetInt("set.flipspin", value ? 1 : 0);
    }

    public static bool SafetyAcknowledged
    {
        get => PlayerPrefs.GetInt("set.safety", 0) == 1;
        set => SetInt("set.safety", value ? 1 : 0);
    }

    public static int PlayerCount
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("set.players", 1), 1, 4);
        set => SetInt("set.players", Mathf.Clamp(value, 1, 4));
    }

    static string DefaultLanguage() => Application.systemLanguage switch
    {
        SystemLanguage.ChineseTraditional or SystemLanguage.Chinese or SystemLanguage.ChineseSimplified => "zh",
        _ => "en",
    };

    static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }
}
