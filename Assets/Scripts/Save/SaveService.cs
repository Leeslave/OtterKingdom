using System;
using System.IO;
using UnityEngine;

// Minimal temp-write + backup save flow. Does not yet handle unknown-item-id
// migration or corruption-recovery UI prompts (see design doc 11.4) — that
// hardening belongs to M3.
public class SaveService
{
    private readonly string savePath;
    private readonly string backupPath;
    private readonly string tempPath;

    public SaveService(string fileName = "save.json")
    {
        savePath = Path.Combine(Application.persistentDataPath, fileName);
        backupPath = savePath + ".bak";
        tempPath = savePath + ".tmp";
    }

    public SaveData Load()
    {
        if (TryReadFrom(savePath, out var data))
        {
            return data;
        }

        Debug.LogWarning("[SaveService] Primary save missing or invalid, trying backup.");

        if (TryReadFrom(backupPath, out var backupData))
        {
            return backupData;
        }

        Debug.Log("[SaveService] No valid save found, starting a new game.");
        return null;
    }

    public void Save(SaveData data)
    {
        data.lastSaveUtc = DateTime.UtcNow.ToString("o");
        string json = JsonUtility.ToJson(data, true);

        try
        {
            File.WriteAllText(tempPath, json);

            if (File.Exists(savePath))
            {
                File.Copy(savePath, backupPath, true);
            }

            File.Copy(tempPath, savePath, true);
            File.Delete(tempPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Save failed: {e.Message}");
        }
    }

    private bool TryReadFrom(string path, out SaveData data)
    {
        data = null;
        if (!File.Exists(path)) return false;

        try
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<SaveData>(json);
            return data != null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Failed to parse save at {path}: {e.Message}");
            return false;
        }
    }
}
