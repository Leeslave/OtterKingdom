using System;
using System.IO;
using UnityEngine;

public enum SaveLoadStatus
{
    Loaded,              // primary save read fine
    RecoveredFromBackup, // primary missing/corrupt, backup used
    NoSave,              // no save files at all — genuine first launch
    Corrupted            // save files exist but none could be read
}

// Temp-write + verify + backup save flow (design doc 11.4):
//   1. write save.json.tmp and read it back before trusting it,
//   2. swap it in as save.json, rotating the old save.json to save.json.bak
//      only if that old file is itself valid — a corrupt primary must never
//      overwrite a good backup,
//   3. unreadable files are renamed to *.corrupt-<timestamp>, never deleted.
//
// When every save file is unreadable, Load() reports Corrupted and all writes
// are blocked until the player explicitly chooses to start over
// (DiscardCorruptedAndStartOver) — otherwise the next autosave would silently
// replace their progress with a fresh game. The corrupt files are left in
// place until then, so quitting and relaunching asks again.
//
// Still not handled: unknown-item-id migration and schemaVersion upgrades.
public class SaveService
{
    private readonly string savePath;
    private readonly string backupPath;
    private readonly string tempPath;

    private bool warnedBlocked;

    public SaveService(string fileName = "save.json")
    {
        savePath = Path.Combine(Application.persistentDataPath, fileName);
        backupPath = savePath + ".bak";
        tempPath = savePath + ".tmp";
    }

    public bool WritesBlocked { get; private set; }

    public SaveLoadStatus Load(out SaveData data)
    {
        if (TryReadFrom(savePath, out data))
        {
            return SaveLoadStatus.Loaded;
        }

        bool primaryExists = File.Exists(savePath);

        if (TryReadFrom(backupPath, out data))
        {
            Debug.LogWarning("[SaveService] Primary save missing or invalid — recovered from backup.");
            // The backup stays as the recovery copy; set the bad primary aside
            // so the next save doesn't have to decide what to do with it.
            if (primaryExists) Quarantine(savePath);
            return SaveLoadStatus.RecoveredFromBackup;
        }

        if (!primaryExists && !File.Exists(backupPath))
        {
            Debug.Log("[SaveService] No save found, starting a new game.");
            return SaveLoadStatus.NoSave;
        }

        Debug.LogError("[SaveService] Save and backup are both unreadable — saving is blocked until the player decides.");
        WritesBlocked = true;
        return SaveLoadStatus.Corrupted;
    }

    // The player confirmed "start over" after a Corrupted load: keep the bad
    // files under a corrupt-* name and allow saving again.
    public void DiscardCorruptedAndStartOver()
    {
        if (File.Exists(savePath)) Quarantine(savePath);
        if (File.Exists(backupPath)) Quarantine(backupPath);
        WritesBlocked = false;
        warnedBlocked = false;
    }

    // stampTime: false keeps data.lastSaveUtc as is — only the dev tool that
    // rewinds it to fake time away uses that.
    public bool Save(SaveData data, bool stampTime = true)
    {
        if (WritesBlocked)
        {
            if (!warnedBlocked)
            {
                warnedBlocked = true;
                Debug.LogWarning("[SaveService] Save skipped — the existing save is corrupted and the player hasn't chosen to start over.");
            }
            return false;
        }

        if (stampTime) data.lastSaveUtc = DateTime.UtcNow.ToString("o");
        string json = JsonUtility.ToJson(data, true);

        try
        {
            File.WriteAllText(tempPath, json);
            if (!TryReadFrom(tempPath, out _))
            {
                Debug.LogError("[SaveService] Written temp save failed verification — keeping the previous save.");
                File.Delete(tempPath);
                return false;
            }

            if (!File.Exists(savePath))
            {
                File.Move(tempPath, savePath);
            }
            else if (TryReadFrom(savePath, out _))
            {
                ReplaceFile(tempPath, savePath, backupPath);
            }
            else
            {
                // Current primary is bad: replace it but leave the backup alone.
                Quarantine(savePath);
                File.Move(tempPath, savePath);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Save failed: {e.Message}");
            return false;
        }
    }

    // File.Replace swaps in one OS call where supported; fall back to copies
    // on platforms/filesystems that reject it.
    private static void ReplaceFile(string source, string destination, string backup)
    {
        try
        {
            File.Replace(source, destination, backup);
        }
        catch (Exception e) when (e is PlatformNotSupportedException || e is IOException)
        {
            File.Copy(destination, backup, true);
            File.Copy(source, destination, true);
            File.Delete(source);
        }
    }

    private static void Quarantine(string path)
    {
        string target = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        for (int i = 1; File.Exists(target); i++)
        {
            target = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}-{i}";
        }

        try
        {
            File.Move(path, target);
            Debug.LogWarning($"[SaveService] Moved unreadable save to {target}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Couldn't move unreadable save {path}: {e.Message}");
        }
    }

    private static bool TryReadFrom(string path, out SaveData data)
    {
        data = null;
        if (!File.Exists(path)) return false;

        try
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<SaveData>(json);
            if (IsValid(data)) return true;

            Debug.LogError($"[SaveService] Save at {path} parsed but failed validation.");
            data = null;
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Failed to parse save at {path}: {e.Message}");
            data = null;
            return false;
        }
    }

    // JsonUtility happily turns "{}" (or any JSON object) into a SaveData full
    // of field defaults, so parsing alone doesn't prove the file is ours.
    // Every save this service writes has a timestamp and at least one plot.
    private static bool IsValid(SaveData data)
    {
        return data != null
            && data.schemaVersion > 0
            && !string.IsNullOrEmpty(data.lastSaveUtc)
            && data.plots != null && data.plots.Count > 0
            && data.inventory != null
            && data.currencies != null;
    }
}
