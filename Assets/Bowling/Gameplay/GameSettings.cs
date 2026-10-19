using System.Collections.Generic;
using UnityEngine;

public enum ControlMode { Swipe, Motion }

// Player preferences, saved via SaveSystem as soon as they change. The swing calibration and the
// personal feel settings (hand, sensitivity, aim trim) live in one of up to 8 calibration profiles,
// so several people can share a phone; the properties below read the active one.
public static class GameSettings
{
    public const int MaxProfiles = 8;

    static SaveData.Settings S => SaveSystem.Data.settings;
    static SaveData.CalibrationProfile P => Profiles[ActiveProfile];

    // During a multiplayer game each player's turn uses their own profile (not saved as the default).
    static int turnProfile = -1;

    public static List<SaveData.CalibrationProfile> Profiles
    {
        get
        {
            var d = SaveSystem.Data;
            if (d.profiles == null) d.profiles = new List<SaveData.CalibrationProfile>();
            if (d.profiles.Count == 0)
            {
                // Older saves: the one calibration they had becomes profile 1.
                d.profiles.Add(new SaveData.CalibrationProfile
                {
                    leftHanded = S.leftHanded, motionSensitivity = S.motionSensitivity, swingReference = S.swingReference,
                    calibrated = S.calibrated, aimOffsetDeg = S.aimOffsetDeg, spinCalibrated = S.spinCalibrated,
                    twistBias = S.twistBias, spinScale = S.spinScale, spinDeadZone = S.spinDeadZone,
                    swingAmplitudeDeg = S.swingAmplitudeDeg, swingAccel = S.swingAccel,
                });
            }
            return d.profiles;
        }
    }

    // The profile used outside multiplayer turns (Settings and the calibration screen pick it).
    public static int DefaultProfile
    {
        get => Mathf.Clamp(SaveSystem.Data.activeProfile, 0, Profiles.Count - 1);
        set { SaveSystem.Data.activeProfile = Mathf.Clamp(value, 0, Profiles.Count - 1); SaveSystem.Save(); }
    }

    public static int ActiveProfile => turnProfile >= 0 && turnProfile < Profiles.Count ? turnProfile : DefaultProfile;

    public static void UseProfileForTurn(int index) => turnProfile = index;
    public static void ClearTurnProfile() => turnProfile = -1;

    public static string ProfileName(int index)
    {
        if (index < 0 || index >= Profiles.Count) return "";
        string n = Profiles[index].name;
        return string.IsNullOrWhiteSpace(n) ? Loc.F("profile_default_name", index + 1) : n;
    }

    public static void RenameProfile(int index, string name)
    {
        if (index < 0 || index >= Profiles.Count) return;
        Profiles[index].name = name ?? "";
        SaveSystem.Save();
    }

    // Adds an empty profile (copying the current hand and feel) and returns its index, or -1 when full.
    public static int AddProfile()
    {
        if (Profiles.Count >= MaxProfiles) return -1;
        var from = P;
        Profiles.Add(new SaveData.CalibrationProfile { leftHanded = from.leftHanded, motionSensitivity = from.motionSensitivity });
        SaveSystem.Save();
        return Profiles.Count - 1;
    }

    // Forget a profile's calibration but keep its name and hand.
    public static void ResetProfile(int index)
    {
        if (index < 0 || index >= Profiles.Count) return;
        var old = Profiles[index];
        Profiles[index] = new SaveData.CalibrationProfile { name = old.name, leftHanded = old.leftHanded };
        SaveSystem.Save();
    }

    public static void DeleteProfile(int index)
    {
        if (Profiles.Count <= 1 || index < 0 || index >= Profiles.Count) return;
        Profiles.RemoveAt(index);
        var d = SaveSystem.Data;
        if (d.activeProfile > index || d.activeProfile >= Profiles.Count) d.activeProfile = Mathf.Max(0, d.activeProfile - 1);
        // Player line-ups pointed at the later profiles: shift them down too.
        foreach (var slot in d.playerSlots)
            if (slot.profile > index) slot.profile--;
            else if (slot.profile == index) slot.profile = 0;
        SaveSystem.Save();
    }

    public static bool ProfileCalibrated(int index) => index >= 0 && index < Profiles.Count && Profiles[index].calibrated;

    // Text size multiplier for every menu and HUD label (0.85 – 1.35).
    public const float MinFontScale = 0.85f, MaxFontScale = 1.35f;
    public static float FontScale
    {
        get => S.fontScale <= 0f ? 1.1f : Mathf.Clamp(S.fontScale, MinFontScale, MaxFontScale);
        set { S.fontScale = Mathf.Clamp(value, MinFontScale, MaxFontScale); SaveSystem.Save(); }
    }

    // The ball technique picked last (see BallTechnique).
    public static int Technique
    {
        get => Mathf.Max(0, S.technique);
        set { S.technique = Mathf.Max(0, value); SaveSystem.Save(); }
    }

    public static ControlMode Control
    {
        get => (ControlMode)S.control;
        set { S.control = (int)value; SaveSystem.Save(); }
    }

    public static bool LeftHanded
    {
        get => P.leftHanded;
        set { P.leftHanded = value; SaveSystem.Save(); }
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
        get => P.motionSensitivity;
        set { P.motionSensitivity = Mathf.Clamp(value, 0.7f, 1.3f); SaveSystem.Save(); }
    }

    // The player's comfortable swing speed (rad/s at release), measured by calibration.
    public static float SwingReference
    {
        get => P.swingReference;
        set { P.swingReference = Mathf.Clamp(value, 3f, 20f); SaveSystem.Save(); }
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
        get => P.calibrated;
        set { P.calibrated = value; SaveSystem.Save(); }
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

    // Left/right aim trim for motion throws (degrees, -2.5 – 2.5). Positive pulls the ball left,
    // to counter a right-hander whose ball drifts right (and vice versa).
    public static float AimOffsetDeg
    {
        get => P.aimOffsetDeg;
        set { P.aimOffsetDeg = Mathf.Clamp(value, -2.5f, 2.5f); SaveSystem.Save(); }
    }

    // Whether the first-run interactive tutorial has been completed.
    public static bool TutorialDone
    {
        get => SaveSystem.Data.tutorialDone;
        set { SaveSystem.Data.tutorialDone = value; SaveSystem.Save(); }
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

    public static int Venue
    {
        get => Mathf.Clamp(S.venue, 0, Venues.All.Length - 1);
        set { S.venue = Mathf.Clamp(value, 0, Venues.All.Length - 1); SaveSystem.Save(); }
    }

    public static string EquippedBall
    {
        get => string.IsNullOrEmpty(S.equippedBall) ? "starter" : S.equippedBall;
        set { S.equippedBall = value; SaveSystem.Save(); }
    }

    // 0 = S, 1 = M, 2 = L (see ProShop.SizeScale).
    public static int BallSize
    {
        get => Mathf.Clamp(S.ballSize, 0, 2);
        set { S.ballSize = Mathf.Clamp(value, 0, 2); SaveSystem.Save(); }
    }

    public static string EquippedOutfit
    {
        get => string.IsNullOrEmpty(S.outfit) ? "neon" : S.outfit;
        set { S.outfit = value; SaveSystem.Save(); }
    }

    public static SoundPack Sounds
    {
        get => (SoundPack)Mathf.Clamp(S.soundPack, 0, 3);
        set { S.soundPack = (int)value; SaveSystem.Save(); }
    }

    public const int MusicOff = 0, MusicAuto = 1;

    // MusicOff, MusicAuto (suits the venue) or 2 + a MusicSynth.Styles index.
    public static int MusicTrack
    {
        get => Mathf.Clamp(S.musicTrack, 0, MusicSynth.Styles.Length + 1);
        set { S.musicTrack = Mathf.Clamp(value, 0, MusicSynth.Styles.Length + 1); SaveSystem.Save(); }
    }

    public static float MusicVolume
    {
        get => Mathf.Clamp01(S.musicVolume);
        set { S.musicVolume = Mathf.Clamp01(value); SaveSystem.Save(); }
    }

    // ---- Editing on the Settings screen: snapshot, compare, save or roll back ----

    // Settings edits also touch the active calibration profile (hand, sensitivity, aim trim).
    [System.Serializable]
    class Draft
    {
        public SaveData.Settings settings;
        public List<SaveData.CalibrationProfile> profiles;
        public int activeProfile;
    }

    public static string Snapshot() => JsonUtility.ToJson(new Draft { settings = S, profiles = Profiles, activeProfile = SaveSystem.Data.activeProfile });

    public static bool ChangedSince(string snapshot) => Snapshot() != snapshot;

    public static void Restore(string snapshot)
    {
        var d = JsonUtility.FromJson<Draft>(snapshot);
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(d.settings), S);
        SaveSystem.Data.profiles = d.profiles;
        SaveSystem.Data.activeProfile = d.activeProfile;
    }

    // Wrist-roll → hook mapping measured by the on-lane calibration (see CalibrationSession).
    public static bool SpinCalibrated => P.spinCalibrated;
    public static float TwistBias => P.twistBias;
    public static float SpinScale => P.spinScale;
    public static float SpinDeadZone => P.spinDeadZone;
    public static float SwingAmplitudeDeg => P.swingAmplitudeDeg;
    public static float SwingAccel => P.swingAccel;

    public static void SetSpinCalibration(bool calibrated, float bias, float scale, float deadZone)
    {
        P.spinCalibrated = calibrated;
        P.twistBias = bias;
        P.spinScale = scale;
        P.spinDeadZone = deadZone;
        SaveSystem.Save();
    }

    public static void SetSwingProfile(float amplitudeDeg, float accel)
    {
        P.swingAmplitudeDeg = amplitudeDeg;
        P.swingAccel = accel;
        SaveSystem.Save();
    }
}
