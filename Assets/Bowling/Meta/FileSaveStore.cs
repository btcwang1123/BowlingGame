using System;
using System.IO;
using UnityEngine;

// Real ISaveStore, backed by save.json (+ save.bak) under a directory. Writes go through a
// temp file and a backup copy so a kill mid-write can never leave save.json corrupted.
public class FileSaveStore : ISaveStore
{
    readonly string directory;

    public FileSaveStore(string directory = null)
    {
        this.directory = directory ?? Application.persistentDataPath;
    }

    string SavePath => Path.Combine(directory, "save.json");
    string BackupPath => Path.Combine(directory, "save.bak");
    string TempPath => Path.Combine(directory, "save.tmp");

    public string Load()
    {
        string json = TryRead(SavePath);
        if (json != null) return json;
        return TryRead(BackupPath);
    }

    public void Write(string json)
    {
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(TempPath, json);
        if (File.Exists(SavePath)) File.Copy(SavePath, BackupPath, true);
        if (File.Exists(SavePath)) File.Delete(SavePath);
        File.Move(TempPath, SavePath);
    }

    string TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            string text = File.ReadAllText(path);
            string trimmed = text.Trim();
            if (trimmed.Length == 0 || !trimmed.StartsWith("{") || !trimmed.EndsWith("}")) return null;
            // Brace-shape alone isn't enough: a file can look like JSON but fail to parse as
            // SaveData, which would otherwise throw uncaught deeper in SaveSystem and skip the
            // save.bak fallback entirely.
            if (JsonUtility.FromJson<SaveData>(text) == null) return null;
            return text;
        }
        catch (IOException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
