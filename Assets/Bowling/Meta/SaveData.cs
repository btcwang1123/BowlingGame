using System;

[Serializable]
public class SaveData
{
    public int version = SaveSystem.CurrentVersion;
    public Settings settings = new Settings();
    public Stats stats = new Stats();
    public int[] challengeStars = new int[0];
    public bool tutorialDone;
    public string cachedProfileJson; // raw JSON for the future PlayerProfile (T19+ accounts); unused until then

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
        public bool safetyAcknowledged;
        public int playerCount = 1;
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
    }
}
