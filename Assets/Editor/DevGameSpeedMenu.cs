using UnityEditor;

/// <summary>
/// OtterKingdom/Dev/Game Speed: 테스트용 게임 속도 x1 · x2 · x5 · x10 · x30 (DevGameSpeed).
/// 플레이 전에 골라도 되고 플레이 중에 바꿔도 된다. 에디터를 끄면 x1로 돌아온다 (SessionState)
/// </summary>
[InitializeOnLoad]
public static class DevGameSpeedMenu
{
    private const string Root = "OtterKingdom/Dev/Game Speed/";
    private const string StateKey = "OtterKingdom.Dev.GameSpeed";

    // 스크립트를 다시 불러올 때마다(플레이 시작 포함) 고른 배속을 되살림
    static DevGameSpeedMenu()
    {
        DevGameSpeed.Speed = SessionState.GetFloat(StateKey, 1f);
    }

    /// <summary>배속을 바꾸고 기억 (구역 · 성 테스트 창도 이것을 씀)</summary>
    public static void Set(float speed)
    {
        DevGameSpeed.Speed = speed;
        SessionState.SetFloat(StateKey, speed);
    }

    [MenuItem(Root + "x1 (보통)")]
    private static void Speed1() => Set(1f);

    [MenuItem(Root + "x2")]
    private static void Speed2() => Set(2f);

    [MenuItem(Root + "x5")]
    private static void Speed5() => Set(5f);

    [MenuItem(Root + "x10")]
    private static void Speed10() => Set(10f);

    [MenuItem(Root + "x30")]
    private static void Speed30() => Set(30f);

    [MenuItem(Root + "x1 (보통)", true)]
    private static bool Check1() => Check(1f, "x1 (보통)");

    [MenuItem(Root + "x2", true)]
    private static bool Check2() => Check(2f, "x2");

    [MenuItem(Root + "x5", true)]
    private static bool Check5() => Check(5f, "x5");

    [MenuItem(Root + "x10", true)]
    private static bool Check10() => Check(10f, "x10");

    [MenuItem(Root + "x30", true)]
    private static bool Check30() => Check(30f, "x30");

    private static bool Check(float speed, string item)
    {
        Menu.SetChecked(Root + item, UnityEngine.Mathf.Approximately(DevGameSpeed.Speed, speed));
        return true;
    }
}
