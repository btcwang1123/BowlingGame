using UnityEngine;

public enum ControlMode { Swipe, Motion }

// Player preferences, saved via SaveSystem as soon as they change.
public static class GameSettings
{
    static SaveData.Settings S => SaveSystem.Data.settings;

    public static ControlMode Control
    {
        get => (ControlMode)S.control;
        set { S.control = (int)value; SaveSystem.Save(); }
    }

    public static bool LeftHanded
    {
        get => S.leftHanded;
        set { S.leftHanded = value; SaveSystem.Save(); }
    }

    public static float Volume
    {
        get => S.volume;
        set { S.volume = Mathf.Clamp01(value); AudioListener.volume = Mathf.Clamp01(value); SaveSystem.Save(); }
    }

    // "zh" (Traditional Chinese) or "en". Defaults to the device language.
    public static string Language
    {
        get => S.language;
        set { S.language = value; SaveSystem.Save(); }
    }

    public static bool Vibration
    {
        get => S.vibration;
        set { S.vibration = value; SaveSystem.Save(); }
    }

    public static bool AimGuide
    {
        get => S.aimGuide;
        set { S.aimGuide = value; SaveSystem.Save(); }
    }

    // Multiplies motion-control ball speed (0.7 – 1.3).
    public static float MotionSensitivity
    {
        get => S.motionSensitivity;
        set { S.motionSensitivity = Mathf.Clamp(value, 0.7f, 1.3f); SaveSystem.Save(); }
    }

    // The player's comfortable swing speed (rad/s at release), measured by calibration.
    public static float SwingReference
    {
        get => S.swingReference;
        set { S.swingReference = Mathf.Clamp(value, 3f, 20f); SaveSystem.Save(); }
    }

    // Ball weight in pounds (8–16). Heavier hits harder but leaves the hand a little slower.
    public static int BallWeightLb
    {
        get => Mathf.Clamp(S.ballWeightLb, 8, 16);
        set { S.ballWeightLb = Mathf.Clamp(value, 8, 16); SaveSystem.Save(); }
    }

    public static float BallMassKg => BallWeightLb * 0.4536f;

    // Release speed multiplier for the ball weight: 8 lb is 6% faster, 16 lb 6% slower.
    public static float BallSpeedFactor => Mathf.Lerp(1.06f, 0.94f, (BallWeightLb - 8) / 8f);

    public static bool Calibrated
    {
        get => S.calibrated;
        set { S.calibrated = value; SaveSystem.Save(); }
    }

    // Sensor axes differ between phones; these let a player fix a reversed direction or spin.
    public static bool FlipAim
    {
        get => S.flipAim;
        set { S.flipAim = value; SaveSystem.Save(); }
    }

    public static bool FlipSpin
    {
        get => S.flipSpin;
        set { S.flipSpin = value; SaveSystem.Save(); }
    }

    public static bool SafetyAcknowledged
    {
        get => S.safetyAcknowledged;
        set { S.safetyAcknowledged = value; SaveSystem.Save(); }
    }

    public static int PlayerCount
    {
        get => Mathf.Clamp(S.playerCount, 1, 4);
        set { S.playerCount = Mathf.Clamp(value, 1, 4); SaveSystem.Save(); }
    }
}
