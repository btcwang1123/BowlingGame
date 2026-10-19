using System;
using UnityEngine;

// Single JSON save file replacing the old scattered PlayerPrefs keys. GameSettings and
// PlayerStats read/write through Data; this class owns loading, migrating legacy PlayerPrefs
// values on first run, and debouncing disk writes.
public static class SaveSystem
{
    public const int CurrentVersion = 1;

    public static event Action Changed;

    static ISaveStore store;
    static SaveData data;
    static Runner runner;
    static float pendingSaveSince = -1f;
    const float SaveDelaySeconds = 0.5f;

    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    static ISaveStore Store => store ??= new FileSaveStore();

    static void Load()
    {
        string json = Store.Load();
        if (json != null)
        {
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch (ArgumentException) { data = null; }
        }
        if (data == null)
        {
            data = MigrateFromPlayerPrefs();
            Store.Write(JsonUtility.ToJson(data));
        }
    }

    static SaveData MigrateFromPlayerPrefs()
    {
        var d = new SaveData { version = CurrentVersion };

        d.settings.control = PlayerPrefs.GetInt("set.control", 0);
        d.settings.leftHanded = PlayerPrefs.GetInt("set.left", 0) == 1;
        d.settings.volume = PlayerPrefs.GetFloat("set.volume", 0.8f);
        d.settings.language = PlayerPrefs.GetString("set.lang", DefaultLanguage());
        d.settings.vibration = PlayerPrefs.GetInt("set.vibrate", 1) == 1;
        d.settings.aimGuide = PlayerPrefs.GetInt("set.aimguide", 1) == 1;
        d.settings.motionSensitivity = PlayerPrefs.GetFloat("set.sens", 1f);
        d.settings.swingReference = PlayerPrefs.GetFloat("set.swingref", 8f);
        d.settings.ballWeightLb = PlayerPrefs.GetInt("set.ballweight", 13);
        d.settings.calibrated = PlayerPrefs.GetInt("set.calibrated", 0) == 1;
        d.settings.flipAim = PlayerPrefs.GetInt("set.flipaim", 0) == 1;
        d.settings.flipSpin = PlayerPrefs.GetInt("set.flipspin", 0) == 1;
        d.settings.safetyAcknowledged = PlayerPrefs.GetInt("set.safety", 0) == 1;
        d.settings.playerCount = PlayerPrefs.GetInt("set.players", 1);

        d.stats.gamesPlayed = PlayerPrefs.GetInt("stat.games", 0);
        d.stats.bestScore = PlayerPrefs.GetInt("BestScore", 0); // key kept from v0.2
        d.stats.totalScore = PlayerPrefs.GetInt("stat.total", 0);
        d.stats.throwsCount = PlayerPrefs.GetInt("stat.throws", 0);
        d.stats.strikes = PlayerPrefs.GetInt("stat.strikes", 0);
        d.stats.spares = PlayerPrefs.GetInt("stat.spares", 0);
        d.stats.gutters = PlayerPrefs.GetInt("stat.gutters", 0);
        d.stats.topSpeedKmh = PlayerPrefs.GetFloat("stat.topspeed", 0f);

        d.challengeStars = new int[ChallengeLevels.All.Count];
        for (int i = 0; i < d.challengeStars.Length; i++)
            d.challengeStars[i] = PlayerPrefs.GetInt($"chal.{i}", 0);

        return d;
    }

    // Same logic as the old GameSettings.DefaultLanguage(): device language -> "zh" for any
    // Chinese variant, else "en".
    static string DefaultLanguage() => Application.systemLanguage switch
    {
        SystemLanguage.ChineseTraditional or SystemLanguage.Chinese or SystemLanguage.ChineseSimplified => "zh",
        _ => "en",
    };

    // While the Settings screen has unsaved edits, writes wait here until the player saves or
    // discards them, so backing out without saving never leaves half-applied settings on disk.
    public static bool Deferred { get; private set; }
    static bool deferredDirty;

    public static void BeginDeferred()
    {
        Deferred = true;
        deferredDirty = false;
    }

    public static void EndDeferred()
    {
        Deferred = false;
        if (deferredDirty) Save();
        deferredDirty = false;
    }

    public static void Save()
    {
        if (Deferred)
        {
            deferredDirty = true;
            return;
        }
        if (!Application.isPlaying)
        {
            Flush();
            return;
        }

        if (runner == null)
        {
            var go = new GameObject("SaveSystem.Runner") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
            runner.Tick = Tick;
            runner.FlushPending = FlushPending;
        }
        pendingSaveSince = Time.unscaledTime;
    }

    static void Tick()
    {
        if (pendingSaveSince < 0f) return;
        if (Time.unscaledTime - pendingSaveSince >= SaveDelaySeconds)
        {
            pendingSaveSince = -1f;
            Flush();
        }
    }

    // A debounced save still waiting to fire must not be lost to the OS suspending or killing
    // the app before the 0.5s delay is up (e.g. the player backs out right after a game ends).
    static void FlushPending()
    {
        if (pendingSaveSince >= 0f && !Deferred) Flush();
    }

    public static void Flush()
    {
        if (data == null) return;
        pendingSaveSince = -1f;
        Store.Write(JsonUtility.ToJson(data));
        Changed?.Invoke();
    }

    // Test-only hook: swaps the backing store and clears cached state so the next Data access
    // reloads from scratch. Needed because all state here is static and EditMode tests share
    // an AppDomain.
    public static void ResetForTests(ISaveStore testStore)
    {
        store = testStore;
        data = null;
        pendingSaveSince = -1f;
        Deferred = false;
        deferredDirty = false;
    }

    class Runner : MonoBehaviour
    {
        public Action Tick;
        public Action FlushPending;
        void Update() => Tick?.Invoke();
        void OnApplicationPause(bool paused) { if (paused) FlushPending?.Invoke(); }
        void OnApplicationQuit() => FlushPending?.Invoke();
    }
}
