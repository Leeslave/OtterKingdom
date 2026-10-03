using UnityEditor;

/// <summary>
/// 시간대 빛 미리 보기 (플레이 중에 눌러 바로 확인). 실제 시각으로 돌아가려면 "실제 시각".
/// </summary>
public static class TimeOfDayMenu
{
    private const string Root = "Tools/Time Of Day/";

    [MenuItem(Root + "실제 시각", false, 0)]
    private static void RealTime() => TimeOfDay.PreviewHour = -1f;

    [MenuItem(Root + "새벽 (06:00)", false, 20)]
    private static void Dawn() => TimeOfDay.PreviewHour = 6f;

    [MenuItem(Root + "낮 (12:00)", false, 21)]
    private static void Noon() => TimeOfDay.PreviewHour = 12f;

    [MenuItem(Root + "노을 (18:15)", false, 22)]
    private static void Sunset() => TimeOfDay.PreviewHour = 18.25f;

    [MenuItem(Root + "땅거미 (19:40)", false, 23)]
    private static void Dusk() => TimeOfDay.PreviewHour = 19.67f;

    [MenuItem(Root + "밤 (22:00)", false, 24)]
    private static void Night() => TimeOfDay.PreviewHour = 22f;
}
