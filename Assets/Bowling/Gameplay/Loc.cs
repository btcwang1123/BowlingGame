using System;
using System.Collections.Generic;
using UnityEngine;

// All on-screen text lives in Resources/Loc/{zh-TW,en}.json. Loc.T("key") picks the current
// language (Loc.Language); Loc.F("key", args) also fills in {0}, {1}… like string.Format.
public static class Loc
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

    static Dictionary<string, string> zhTable;
    static Dictionary<string, string> enTable;

    public static event Action LanguageChanged;

    // "zh" (Traditional Chinese) or "en". Defaults to the device language (via GameSettings).
    public static string Language
    {
        get => GameSettings.Language;
        set
        {
            if (GameSettings.Language == value) return;
            GameSettings.Language = value;
            LanguageChanged?.Invoke();
        }
    }

    public static string T(string key)
    {
        var table = Language == "zh" ? ZhTable : EnTable;
        if (table.TryGetValue(key, out var value)) return value;
#if UNITY_EDITOR
        Debug.LogWarning($"Loc: missing key \"{key}\" for language \"{Language}\"");
#endif
        return "#" + key;
    }

    public static string F(string key, params object[] args) => string.Format(T(key), args);

    static Dictionary<string, string> ZhTable => zhTable ??= LoadTable("zh-TW");
    static Dictionary<string, string> EnTable => enTable ??= LoadTable("en");

    static Dictionary<string, string> LoadTable(string fileName)
    {
        var table = new Dictionary<string, string>();
        var asset = Resources.Load<TextAsset>("Loc/" + fileName);
        if (asset == null)
        {
            Debug.LogError($"Loc: missing Resources/Loc/{fileName}.json");
            return table;
        }

        var file = JsonUtility.FromJson<LocFile>(asset.text);
        if (file?.entries == null) return table;
        foreach (var entry in file.entries) table[entry.key] = entry.value;
        return table;
    }
}
