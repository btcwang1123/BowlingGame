using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class SaveSystemTests
{
    // Legacy PlayerPrefs keys this test touches, snapshotted and restored so the migration
    // test doesn't permanently alter the developer's real PlayerPrefs.
    static readonly string[] TouchedKeys = { "set.control", "stat.games", "BestScore", "chal.0" };

    bool[] hadKey;
    int[] originalValue;
    string tempDir;

    [SetUp]
    public void SetUp()
    {
        hadKey = new bool[TouchedKeys.Length];
        originalValue = new int[TouchedKeys.Length];
        for (int i = 0; i < TouchedKeys.Length; i++)
        {
            hadKey[i] = PlayerPrefs.HasKey(TouchedKeys[i]);
            if (hadKey[i]) originalValue[i] = PlayerPrefs.GetInt(TouchedKeys[i]);
        }
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < TouchedKeys.Length; i++)
        {
            if (hadKey[i]) PlayerPrefs.SetInt(TouchedKeys[i], originalValue[i]);
            else PlayerPrefs.DeleteKey(TouchedKeys[i]);
        }
        PlayerPrefs.Save();

        // Point SaveSystem back at a real store (same default as production) so the rest of
        // the EditMode suite doesn't see leftover static state.
        SaveSystem.ResetForTests(new FileSaveStore());

        if (tempDir != null && Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        tempDir = null;
    }

    [Test]
    public void MigratesLegacyPlayerPrefsIntoSaveData()
    {
        PlayerPrefs.SetInt("set.control", 1); // Motion
        PlayerPrefs.SetInt("stat.games", 42);
        PlayerPrefs.SetInt("BestScore", 222);
        PlayerPrefs.SetInt("chal.0", 3);
        PlayerPrefs.Save();

        SaveSystem.ResetForTests(new EmptyStore());
        var data = SaveSystem.Data;

        Assert.AreEqual(SaveSystem.CurrentVersion, data.version);
        Assert.AreEqual(1, data.settings.control);
        Assert.AreEqual(42, data.stats.gamesPlayed);
        Assert.AreEqual(222, data.stats.bestScore);
        Assert.AreEqual(3, data.challengeStars[0]);
    }

    [Test]
    public void FileSaveStoreRoundTripsJson()
    {
        tempDir = NewTempDir();
        var store = new FileSaveStore(tempDir);

        string json = "{\"version\":1,\"note\":\"hello\"}";
        store.Write(json);

        Assert.AreEqual(json, store.Load());
    }

    [Test]
    public void FileSaveStoreFallsBackToBackupWhenSaveFileIsCorrupt()
    {
        tempDir = NewTempDir();
        var store = new FileSaveStore(tempDir);

        string jsonA = "{\"version\":1,\"note\":\"A\"}";
        string jsonB = "{\"version\":1,\"note\":\"B\"}";
        store.Write(jsonA); // save.json = A
        store.Write(jsonB); // save.bak = A, save.json = B

        File.WriteAllText(Path.Combine(tempDir, "save.json"), "not valid json at all");

        Assert.AreEqual(jsonA, store.Load());
    }

    [Test]
    public void FileSaveStoreFallsBackToBackupWhenSaveFileHasBracesButIsNotValidSaveData()
    {
        tempDir = NewTempDir();
        var store = new FileSaveStore(tempDir);

        string jsonA = "{\"version\":1,\"note\":\"A\"}";
        store.Write(jsonA); // save.json = A
        store.Write("{\"version\":2,\"note\":\"B\"}"); // save.bak = A, save.json = B

        // Looks like JSON (braces present) but isn't valid SaveData — must not throw, and must
        // still fall back to the backup rather than surface whatever JsonUtility does with it.
        File.WriteAllText(Path.Combine(tempDir, "save.json"), "{not actually json}");

        Assert.AreEqual(jsonA, store.Load());
    }

    static string NewTempDir()
    {
        string dir = Path.Combine(Application.temporaryCachePath, "SaveSystemTests_" + Guid.NewGuid());
        return dir;
    }

    // ISaveStore that always reports no existing save, forcing the fresh-migration path.
    class EmptyStore : ISaveStore
    {
        public string Load() => null;
        public void Write(string json) { }
    }
}
