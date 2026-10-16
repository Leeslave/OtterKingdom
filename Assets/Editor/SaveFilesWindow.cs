using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

// Dev window for wiping the save (to replay the tutorial / a first launch)
// and restoring it afterwards. Nothing is deleted: a wipe moves the save
// files into a snapshot, and restoring copies a snapshot back (snapshotting
// the save it replaces first). Only outside Play mode — the running game
// keeps its state in memory and would write it straight back.
public class SaveFilesWindow : EditorWindow
{
    // Created in OnEnable: persistentDataPath can't be read from a
    // ScriptableObject constructor, where field initializers run.
    private SaveService service;
    private SaveData current;
    private SaveLoadStatus currentStatus;
    private List<string> snapshots = new List<string>();
    private readonly Dictionary<string, SaveData> snapshotData = new Dictionary<string, SaveData>();
    private Vector2 scroll;

    [MenuItem("OtterKingdom/Dev/Save Files")]
    private static void Open()
    {
        GetWindow<SaveFilesWindow>("세이브 파일");
    }

    private void OnEnable()
    {
        service = new SaveService();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Refresh();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode) Refresh();
        Repaint();
    }

    private void OnGUI()
    {
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이를 멈춘 뒤에 쓸 수 있어요. 실행 중인 게임이 곧바로 다시 저장해 버리거든요.",
                MessageType.Info);
            return;
        }

        CurrentGui();
        EditorGUILayout.Space();
        SnapshotsGui();
    }

    // --------------------------------------------------------------- current

    private void CurrentGui()
    {
        EditorGUILayout.LabelField("지금 세이브", EditorStyles.boldLabel);
        if (current != null) EditorGUILayout.LabelField(Describe(current), EditorStyles.wordWrappedLabel);
        else if (currentStatus == SaveLoadStatus.Corrupted) EditorGUILayout.LabelField("손상돼서 읽을 수 없어요.");
        else EditorGUILayout.LabelField("없음 — 다음 실행 때 새 게임으로 시작해요.");

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!service.HasSaveFiles))
            {
                if (GUILayout.Button("세이브 날리기 (보관함에 보관)")) Wipe();
            }
            if (GUILayout.Button("다시 읽기", GUILayout.Width(80))) Refresh();
        }
        if (GUILayout.Button("세이브 폴더 열기")) EditorUtility.RevealInFinder(service.SavePath);
    }

    private void Wipe()
    {
        if (!EditorUtility.DisplayDialog("세이브 날리기",
                "지금 세이브를 보관함으로 옮기고, 다음 실행 때 새 게임으로 시작해요.\n보관함에서 언제든 복구할 수 있어요.",
                "날리기", "취소")) return;

        // Deferred like the snapshot buttons, so the lists don't change mid-GUI.
        EditorApplication.delayCall += () =>
        {
            string name = service.WipeToSnapshot();
            if (name != null) Debug.Log($"[SaveFilesWindow] 세이브를 '{name}'(으)로 보관하고 날렸어요.");
            Refresh();
        };
    }

    // ------------------------------------------------------------- snapshots

    private void SnapshotsGui()
    {
        EditorGUILayout.LabelField("보관함", EditorStyles.boldLabel);
        if (snapshots.Count == 0)
        {
            EditorGUILayout.LabelField("보관된 세이브가 없어요.");
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var name in snapshots)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(snapshotData.TryGetValue(name, out var data) && data != null
                    ? Describe(data)
                    : "읽을 수 없는 세이브 (손상 상태 그대로 복구돼요)", EditorStyles.wordWrappedLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("복구")) Restore(name);
                    if (GUILayout.Button("삭제", GUILayout.Width(60))) Delete(name);
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void Restore(string name)
    {
        string message = service.HasSaveFiles
            ? $"'{name}' 세이브로 되돌려요.\n지금 세이브는 보관함에 따로 보관돼요."
            : $"'{name}' 세이브로 되돌려요.";
        if (!EditorUtility.DisplayDialog("세이브 복구", message, "복구", "취소")) return;

        // Deferred: restoring adds a snapshot, which changes the list being drawn.
        EditorApplication.delayCall += () =>
        {
            if (service.RestoreSnapshot(name)) Debug.Log($"[SaveFilesWindow] '{name}' 세이브를 복구했어요.");
            Refresh();
        };
    }

    private void Delete(string name)
    {
        if (!EditorUtility.DisplayDialog("보관본 삭제", $"'{name}' 보관본을 완전히 지워요. 되돌릴 수 없어요.",
                "삭제", "취소")) return;

        EditorApplication.delayCall += () =>
        {
            service.DeleteSnapshot(name);
            Refresh();
        };
    }

    // ------------------------------------------------------------------ data

    private void Refresh()
    {
        currentStatus = service.Load(out current);
        snapshots = service.ListSnapshots();
        snapshotData.Clear();
        foreach (var name in snapshots)
        {
            snapshotData[name] = service.TryReadSnapshot(name, out var data) ? data : null;
        }
        RefreshOtherWindows();
        Repaint();
    }

    // The offline test window caches the save file it last read.
    private static void RefreshOtherWindows()
    {
        foreach (var window in Resources.FindObjectsOfTypeAll<OfflineTestWindow>())
        {
            window.ReloadFile();
            window.Repaint();
        }
    }

    private static string Describe(SaveData save)
    {
        int level = save.profile != null ? save.profile.level : 1;
        return $"마지막 저장 {DescribeTime(save.lastSaveUtc)}\n" +
               $"왕국 Lv.{level} · 밭 Lv.{save.farmLevel} · 낚싯대 Lv.{save.rodLevel} · 누적 판매 {save.lifetimeSales} · " +
               $"첫 안내 {(save.firstPlantGuideDone ? "끝남" : "안 끝남")}";
    }

    private static string DescribeTime(string utc)
    {
        if (string.IsNullOrEmpty(utc) ||
            !DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            return "(없음)";
        return parsed.ToLocalTime().ToString("MM-dd HH:mm:ss");
    }
}
