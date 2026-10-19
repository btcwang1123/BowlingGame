using System;

// Lifetime statistics and challenge progress, saved via SaveSystem.
public static class PlayerStats
{
    static SaveData.Stats S => SaveSystem.Data.stats;

    public static int GamesPlayed => S.gamesPlayed;
    public static int BestScore => S.bestScore;
    public static int TotalScore => S.totalScore;
    public static int Throws => S.throwsCount;
    public static int Strikes => S.strikes;
    public static int Spares => S.spares;
    public static int Gutters => S.gutters;
    public static float TopSpeedKmh => S.topSpeedKmh;

    public static float AverageScore => GamesPlayed == 0 ? 0f : (float)TotalScore / GamesPlayed;
    public static int[] RecentScores => S.recentScores ?? new int[0];
    public static float StrikeRate => Throws == 0 ? 0f : (float)Strikes / Throws;
    public static float PocketRate => S.analyzedThrows == 0 ? 0f : (float)S.pocketHits / S.analyzedThrows;

    public static void RecordEntry(bool pocket)
    {
        S.analyzedThrows++;
        if (pocket) S.pocketHits++;
        SaveSystem.Save();
    }

    public static void RecordThrow(string mark, float speedKmh)
    {
        S.throwsCount++;
        if (mark == "X") S.strikes++;
        else if (mark == "/") S.spares++;
        else if (mark == "-") S.gutters++;
        if (speedKmh > S.topSpeedKmh) S.topSpeedKmh = speedKmh;
        SaveSystem.Save();
    }

    // Returns true if this is a new best score.
    public static bool RecordGame(int score)
    {
        S.gamesPlayed++;
        S.totalScore += score;
        var recent = new System.Collections.Generic.List<int>(RecentScores) { score };
        if (recent.Count > 20) recent.RemoveRange(0, recent.Count - 20);
        S.recentScores = recent.ToArray();
        bool best = score > S.bestScore;
        if (best) S.bestScore = score;
        SaveSystem.Save();
        return best;
    }

    public static int Stars(int level)
    {
        var stars = SaveSystem.Data.challengeStars;
        return level >= 0 && level < stars.Length ? stars[level] : 0;
    }

    public static void RecordStars(int level, int stars)
    {
        var data = SaveSystem.Data;
        if (level >= data.challengeStars.Length) Array.Resize(ref data.challengeStars, level + 1);
        if (stars > data.challengeStars[level])
        {
            data.challengeStars[level] = stars;
            SaveSystem.Save();
        }
    }

    public static int TotalStars
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < ChallengeLevels.All.Count; i++) sum += Stars(i);
            return sum;
        }
    }

    // A level is open once the previous one has at least one star.
    public static bool Unlocked(int level) => level == 0 || Stars(level - 1) > 0;
}
