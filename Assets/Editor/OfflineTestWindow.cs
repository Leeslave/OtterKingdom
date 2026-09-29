using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

// Dev window for testing offline production: set or reset the farm/rod
// levels that unlock it, and rewind the last save time to fake time away.
// In Play mode it goes through the live GameManager (which would otherwise
// overwrite file edits on its next save); outside Play mode it edits the
// save file directly. Coins are left alone — levels are just set, not bought.
public class OfflineTestWindow : EditorWindow
{
    private SaveData fileSave;
    private SaveLoadStatus fileStatus;
    private FarmBalanceData farmBalance;
    private FishingBalanceData fishingBalance;

    [MenuItem("OtterKingdom/Dev/Offline Test")]
    private static void Open()
    {
        GetWindow<OfflineTestWindow>("오프라인 테스트");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        farmBalance = FindBalance<FarmBalanceData>();
        fishingBalance = FindBalance<FishingBalanceData>();
        ReloadFile();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode) ReloadFile();
        Repaint();
    }

    private void OnInspectorUpdate() => Repaint();

    private static bool IsLive => Application.isPlaying && GameManager.Instance != null;

    private void OnGUI()
    {
        if (Application.isPlaying && GameManager.Instance == null)
        {
            EditorGUILayout.HelpBox("이 씬에는 GameManager가 없어요. 밭이나 낚시터 씬에서 사용하세요.", MessageType.Info);
            return;
        }

        var save = IsLive ? GameManager.Instance.DevSave : fileSave;
        EditorGUILayout.LabelField(IsLive ? "플레이 중인 게임" : "세이브 파일", EditorStyles.boldLabel);

        if (save == null)
        {
            EditorGUILayout.HelpBox(fileStatus == SaveLoadStatus.Corrupted
                ? "세이브 파일이 손상돼서 읽을 수 없어요."
                : "세이브 파일이 없어요. 게임을 한 번 실행하면 생겨요.", MessageType.Info);
            if (GUILayout.Button("다시 읽기")) ReloadFile();
            return;
        }

        LevelsGui(save);
        EditorGUILayout.Space();
        PlotsGui(save);
        EditorGUILayout.Space();
        TimeGui(save);
    }

    // ---------------------------------------------------------------- levels

    private void LevelsGui(SaveData save)
    {
        EditorGUILayout.LabelField("레벨", EditorStyles.boldLabel);
        int farm = LevelRow("밭", save.farmLevel, farmBalance.maxFarmLevel, farmBalance.offlineUnlockLevel);
        int rod = LevelRow("낚싯대", save.rodLevel, fishingBalance.MaxRodLevel, fishingBalance.offlineUnlockRodLevel);

        if (GUILayout.Button("초기화 (밭·낚싯대 Lv.1)")) { farm = 1; rod = 1; }

        if (farm != save.farmLevel || rod != save.rodLevel) SetLevels(save, farm, rod);
    }

    private static int LevelRow(string label, int level, int max, int offlineLevel)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            string offline = level >= offlineLevel ? "오프라인 켜짐" : $"오프라인은 Lv.{offlineLevel}부터";
            EditorGUILayout.LabelField($"{label} Lv.{level} / {max}  ({offline})");
            using (new EditorGUI.DisabledScope(level <= 1))
            {
                if (GUILayout.Button("-", GUILayout.Width(28))) level--;
            }
            using (new EditorGUI.DisabledScope(level >= max))
            {
                if (GUILayout.Button("+", GUILayout.Width(28))) level++;
            }
        }
        return level;
    }

    private void SetLevels(SaveData save, int farm, int rod)
    {
        if (IsLive)
        {
            GameManager.Instance.DevSetLevels(farm, rod);
            return;
        }

        save.farmLevel = farm;
        save.rodLevel = rod;
        WriteFile(stampTime: false);
    }

    // ----------------------------------------------------------------- plots

    private void PlotsGui(SaveData save)
    {
        EditorGUILayout.LabelField("밭고랑", EditorStyles.boldLabel);

        int unlocked = 0;
        foreach (var plot in save.plots)
        {
            if (plot.unlocked) unlocked++;
        }
        EditorGUILayout.LabelField($"해금된 밭고랑 {unlocked} / {PlotSaveData.PlotCount}");

        using (new EditorGUI.DisabledScope(unlocked <= 1))
        {
            if (!GUILayout.Button("밭 2·3 다시 잠그기 (심어 둔 작물은 사라짐)")) return;
        }

        if (IsLive)
        {
            GameManager.Instance.DevRelockExtraPlots();
            return;
        }

        GameManager.RelockExtraPlots(save);
        WriteFile(stampTime: false);
    }

    // ------------------------------------------------------------------ time

    private void TimeGui(SaveData save)
    {
        EditorGUILayout.LabelField("자리 비운 시간 만들기", EditorStyles.boldLabel);

        if (IsLive)
        {
            EditorGUILayout.HelpBox("플레이를 멈춘 뒤에 쓸 수 있어요. 마지막 저장 시각을 과거로 돌리면, " +
                                    "다음 실행 때 그만큼 자리를 비웠던 것으로 계산돼요.", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("마지막 저장", DescribeLastSave(save.lastSaveUtc));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("-10분")) Rewind(save, TimeSpan.FromMinutes(10));
            if (GUILayout.Button("-1시간")) Rewind(save, TimeSpan.FromHours(1));
            if (GUILayout.Button("-8시간")) Rewind(save, TimeSpan.FromHours(8));
        }
    }

    private void Rewind(SaveData save, TimeSpan amount)
    {
        var last = TryParseUtc(save.lastSaveUtc, out var parsed) ? parsed : DateTime.UtcNow;
        save.lastSaveUtc = (last - amount).ToString("o");
        WriteFile(stampTime: false);
    }

    private static string DescribeLastSave(string lastSaveUtc)
    {
        if (!TryParseUtc(lastSaveUtc, out var last)) return "(없음)";

        var ago = DateTime.UtcNow - last;
        return $"{last.ToLocalTime():MM-dd HH:mm:ss}  ({(int)ago.TotalHours}시간 {ago.Minutes}분 전)";
    }

    private static bool TryParseUtc(string value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrEmpty(value)) return false;
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            return false;
        utc = parsed.ToUniversalTime();
        return true;
    }

    // ------------------------------------------------------------------ file

    private void ReloadFile()
    {
        fileStatus = new SaveService().Load(out fileSave);
    }

    private void WriteFile(bool stampTime)
    {
        var service = new SaveService();
        if (service.Load(out _) == SaveLoadStatus.Corrupted) return;
        if (!service.Save(fileSave, stampTime)) Debug.LogError("[OfflineTestWindow] 세이브 파일에 쓰지 못했어요.");
        ReloadFile();
    }

    // The project's balance asset, or built-in defaults if there is none.
    private static T FindBalance<T>() where T : ScriptableObject
    {
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) return asset;
        }
        return CreateInstance<T>();
    }
}
