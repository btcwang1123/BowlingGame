using UnityEngine;

// Lifetime statistics and challenge progress, saved in PlayerPrefs.
public static class PlayerStats
{
    public static int GamesPlayed => PlayerPrefs.GetInt("stat.games", 0);
    public static int BestScore => PlayerPrefs.GetInt("BestScore", 0); // key kept from v0.2
    public static int TotalScore => PlayerPrefs.GetInt("stat.total", 0);
    public static int Throws => PlayerPrefs.GetInt("stat.throws", 0);
    public static int Strikes => PlayerPrefs.GetInt("stat.strikes", 0);
    public static int Spares => PlayerPrefs.GetInt("stat.spares", 0);
    public static int Gutters => PlayerPrefs.GetInt("stat.gutters", 0);
    public static float TopSpeedKmh => PlayerPrefs.GetFloat("stat.topspeed", 0f);

    public static float AverageScore => GamesPlayed == 0 ? 0f : (float)TotalScore / GamesPlayed;

    public static void RecordThrow(string mark, float speedKmh)
    {
        Add("stat.throws", 1);
        if (mark == "X") Add("stat.strikes", 1);
        else if (mark == "/") Add("stat.spares", 1);
        else if (mark == "-") Add("stat.gutters", 1);
        if (speedKmh > TopSpeedKmh) PlayerPrefs.SetFloat("stat.topspeed", speedKmh);
        PlayerPrefs.Save();
    }

    // Returns true if this is a new best score.
    public static bool RecordGame(int score)
    {
        Add("stat.games", 1);
        Add("stat.total", score);
        bool best = score > BestScore;
        if (best) PlayerPrefs.SetInt("BestScore", score);
        PlayerPrefs.Save();
        return best;
    }

    public static int Stars(int level) => PlayerPrefs.GetInt($"chal.{level}", 0);

    public static void RecordStars(int level, int stars)
    {
        if (stars > Stars(level)) { PlayerPrefs.SetInt($"chal.{level}", stars); PlayerPrefs.Save(); }
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

    static void Add(string key, int amount) => PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + amount);
}
