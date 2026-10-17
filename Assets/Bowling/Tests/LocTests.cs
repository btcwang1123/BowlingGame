using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class LocTests
{
    [Serializable]
    class Entry
    {
        public string key;
        public string value;
    }

    [Serializable]
    class LocFile
    {
        public Entry[] entries;
    }

    string originalLanguage;

    [SetUp]
    public void SetUp()
    {
        // Isolate GameSettings.Language changes from the developer's real save.json.
        SaveSystem.ResetForTests(new NoopStore());
        originalLanguage = GameSettings.Language;
    }

    [TearDown]
    public void TearDown()
    {
        GameSettings.Language = originalLanguage;
        SaveSystem.ResetForTests(new FileSaveStore());
    }

    [Test]
    public void ZhAndEnResourceFilesHaveExactlyTheSameKeys()
    {
        var zhKeys = LoadKeys("zh-TW");
        var enKeys = LoadKeys("en");

        CollectionAssert.AreEquivalent(zhKeys, enKeys);
        Assert.IsNotEmpty(zhKeys);
    }

    [Test]
    public void TReturnsTextForTheCurrentLanguage()
    {
        Loc.Language = "en";
        Assert.AreEqual("Play", Loc.T("play"));

        Loc.Language = "zh";
        Assert.AreEqual("開始遊戲", Loc.T("play"));
    }

    [Test]
    public void FFormatsArgsIntoLocalizedText()
    {
        Loc.Language = "en";
        Assert.AreEqual("3 player(s)", Loc.F("n_players", 3));
    }

    [Test]
    public void MissingKeyReturnsHashPrefixedKeyAndLogsAnEditorWarning()
    {
        LogAssert.Expect(LogType.Warning, new Regex("not_a_real_key"));
        Assert.AreEqual("#not_a_real_key", Loc.T("not_a_real_key"));
    }

    [Test]
    public void SettingLanguageRaisesLanguageChangedOnlyOnActualChange()
    {
        Loc.Language = "en";
        int fired = 0;
        void OnChanged() => fired++;
        Loc.LanguageChanged += OnChanged;
        try
        {
            Loc.Language = "en"; // same value, no event
            Assert.AreEqual(0, fired);

            Loc.Language = "zh"; // changed, fires once
            Assert.AreEqual(1, fired);

            Loc.Language = "zh"; // same value again, no extra event
            Assert.AreEqual(1, fired);
        }
        finally
        {
            Loc.LanguageChanged -= OnChanged;
        }
    }

    static string[] LoadKeys(string fileName)
    {
        var asset = Resources.Load<TextAsset>("Loc/" + fileName);
        Assert.IsNotNull(asset, $"Resources/Loc/{fileName}.json is missing");
        var file = JsonUtility.FromJson<LocFile>(asset.text);
        return file.entries.Select(e => e.key).ToArray();
    }

    // ISaveStore that never persists, so GameSettings writes during this test don't touch disk.
    class NoopStore : ISaveStore
    {
        public string Load() => null;
        public void Write(string json) { }
    }
}
