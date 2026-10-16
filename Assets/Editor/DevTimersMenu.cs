using UnityEditor;
using UnityEngine;

/// <summary>
/// OtterKingdom/Dev/Fast Timers (5s): 건설·주민 작업(영토 개간·생활 의뢰 포함)을 길어도 5초 안에 끝나게 한다 (테스트용).
/// 켜는 순간 이미 기다리는 것도 5초 안으로 줄고, 켜 둔 동안 새로 시작하는 것도 5초. 끄면 새로 시작하는 것부터 원래 시간.
/// 켜 둔 상태는 이 컴퓨터의 에디터에 남는다 (EditorPrefs). 빌드에는 들어가지 않는다.
/// </summary>
[InitializeOnLoad]
public static class DevTimersMenu
{
    private const string MenuPath = "OtterKingdom/Dev/Fast Timers (5s)";
    private const string PrefKey = "OtterKingdom.Dev.FastTimers";

    // 스크립트를 다시 불러올 때마다(플레이 시작 포함) 켜 둔 상태를 되살림
    static DevTimersMenu()
    {
        DevTimers.Fast = EditorPrefs.GetBool(PrefKey, false);
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        bool on = !DevTimers.Fast;
        DevTimers.Fast = on;
        EditorPrefs.SetBool(PrefKey, on);
        if (on && EditorApplication.isPlaying && SettlementManager.Instance != null)
            SettlementManager.Instance.DevShortenTimers();
        Debug.Log(on
            ? $"[Dev] Fast Timers 켬: 건설·주민 작업이 {DevTimers.FastSeconds:0}초 안에 끝나요."
            : "[Dev] Fast Timers 끔: 새로 시작하는 건설·주민 작업부터 원래 시간이에요.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, DevTimers.Fast);
        return true;
    }
}
