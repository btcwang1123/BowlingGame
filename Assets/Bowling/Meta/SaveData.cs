using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int version = SaveSystem.CurrentVersion;
    public Settings settings = new Settings();
    public Stats stats = new Stats();
    public int[] challengeStars = new int[0];
    public bool tutorialDone;
    public string cachedProfileJson; // raw JSON for the future PlayerProfile (T19+ accounts); unused until then
    public int coins = 300;                          // pro shop currency, earned by playing
    public string[] ownedBalls = { "starter" };      // ProShop ball ids
    public string[] ownedOutfits = { "neon", "classic" }; // Outfits ids
    // Up to 8 swing calibrations (one per person sharing the phone); activeProfile is the one used
    // outside multiplayer. Empty in older saves: GameSettings builds profile 0 from the old fields.
    public List<CalibrationProfile> profiles = new List<CalibrationProfile>();
    public int activeProfile;
    // The last multiplayer line-up (name, calibration, look, ball), offered again next time.
    public List<PlayerSlot> playerSlots = new List<PlayerSlot>();
    // A standard game left part-way through, to continue from the home screen.
    public SavedMatch savedMatch = new SavedMatch();

    [Serializable]
    public class CalibrationProfile
    {
        public string name = "";              // empty = "Player N"
        public bool leftHanded;
        public float motionSensitivity = 1f;
        public float swingReference = 8f;
        public bool calibrated;
        public float aimOffsetDeg;
        public bool spinCalibrated;
        public float twistBias;
        public float spinScale = 0.03f;
        public float spinDeadZone = 1f;
        public float swingAmplitudeDeg;
        public float swingAccel;
    }

    [Serializable]
    public class PlayerSlot
    {
        public string name = "";
        public int profile;                   // profiles index
        public string outfit = "";            // Outfits id; empty = the equipped one
        public string ball = "";              // ProShop ball id; empty = the equipped one
        public int technique;                 // BallTechnique
    }

    [Serializable]
    public class SavedMatch
    {
        public bool valid;
        public string savedAt = "";           // local time, for the "continue" button
        public int currentPlayer;
        public float ballOil;
        public bool[] standing = new bool[0]; // pins up for the current player's next ball
        public List<SavedPlayer> players = new List<SavedPlayer>();
    }

    [Serializable]
    public class SavedPlayer
    {
        public PlayerSlot slot = new PlayerSlot();
        public int[] knocked = new int[0];    // every roll so far…
        public int[] standingBefore = new int[0]; // …and how many pins were up for it
    }

    [Serializable]
    public class Settings
    {
        public int control;
        public bool leftHanded;
        public float volume = 0.8f;
        public string language;
        public bool vibration = true;
        public bool aimGuide = true;
        public float motionSensitivity = 1f;
        public float swingReference = 8f;
        public int ballWeightLb = 13;
        public bool calibrated;
        public bool flipAim;
        public bool flipSpin;
        public float aimOffsetDeg;  // left/right aim trim for motion throws (deg); + pulls the ball left
        public bool safetyAcknowledged;
        public int playerCount = 1;
        public int venue;                     // Venues.All index
        public string equippedBall = "starter";
        public int ballSize = 1;              // 0 = S, 1 = M, 2 = L
        public string outfit = "neon";        // Outfits id
        public int soundPack;                 // SoundPack: 0 Alley, 1 Neon, 2 Arcade, 3 Cartoon
        public int musicTrack = 1;            // 0 = off, 1 = match the venue, 2+ = MusicSynth.Styles index + 2
        public float musicVolume = 0.6f;
        public float fontScale = 1.1f;        // text size multiplier (Settings → text size)
        public int technique;                 // last BallTechnique picked
        // On-lane calibration (straight / left hook / right hook throws).
        public bool spinCalibrated;
        public float twistBias;               // wrist roll of a straight throw, rad/s
        public float spinScale = 0.03f;       // hook (m/s²) per rad/s of wrist roll, sign included (+ = right)
        public float spinDeadZone = 1f;       // wrist roll below this is a straight ball
        public float swingAmplitudeDeg;       // forward swing arc measured in calibration
        public float swingAccel;              // peak angular acceleration measured in calibration, rad/s²
    }

    [Serializable]
    public class Stats
    {
        public int gamesPlayed;
        public int bestScore;
        public int totalScore;
        public int throwsCount;
        public int strikes;
        public int spares;
        public int gutters;
        public float topSpeedKmh;
        public int analyzedThrows;            // throws that reached the pins (for pocket %)
        public int pocketHits;
        public int[] recentScores = new int[0];   // last 20 standard games, oldest first
    }
}
