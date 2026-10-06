using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// OtterKingdom/Dev/Clock: 게임 시계(GameClock)를 앞당긴다 (테스트용 시간, 상세기획서 11.6).
/// 일일 퀘스트 초기화·건설·주민 작업이 바로 앞당겨진다. 오프라인 경과는 다음 플레이 시작 때 반영된다 (Play 중지 → 앞당기기 → Play).
/// 앞당긴 값은 이 컴퓨터의 에디터에 남는다 (EditorPrefs). 빌드에는 들어가지 않는다.
/// </summary>
[InitializeOnLoad]
public static class DevClockMenu
{
    private const string Root = "OtterKingdom/Dev/Clock/";
    private const string PrefKey = "OtterKingdom.Dev.ClockOffsetSec";

    // 스크립트를 다시 불러올 때마다(플레이 시작 포함) 앞당긴 값을 되살림
    static DevClockMenu()
    {
        GameClock.DevOffset = TimeSpan.FromSeconds(EditorPrefs.GetFloat(PrefKey, 0f));
    }

    [MenuItem(Root + "+1 Hour")]
    private static void AddHour() => Set(GameClock.DevOffset + TimeSpan.FromHours(1));

    [MenuItem(Root + "+8 Hours")]
    private static void AddEightHours() => Set(GameClock.DevOffset + TimeSpan.FromHours(8));

    [MenuItem(Root + "+1 Day")]
    private static void AddDay() => Set(GameClock.DevOffset + TimeSpan.FromDays(1));

    [MenuItem(Root + "Reset")]
    private static void ResetOffset() => Set(TimeSpan.Zero);

    [MenuItem(Root + "Show")]
    private static void Show()
    {
        Debug.Log($"[Dev] 게임 시계: 실제보다 {GameClock.DevOffset.TotalHours:0.#}시간 앞 (지금 {GameClock.Now:yyyy-MM-dd HH:mm})");
    }

    [MenuItem("OtterKingdom/Dev/Open Analytics Log")]
    private static void OpenAnalytics()
    {
        System.IO.Directory.CreateDirectory(LocalFileAnalyticsSink.Folder);
        EditorUtility.RevealInFinder(LocalFileAnalyticsSink.FilePath);
    }

    private static void Set(TimeSpan offset)
    {
        GameClock.DevOffset = offset;
        EditorPrefs.SetFloat(PrefKey, (float)offset.TotalSeconds);
        Show();
    }
}
