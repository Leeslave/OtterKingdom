using UnityEditor;

/// <summary>
/// OtterKingdom/Dev/Level +1: Play 중에 왕국 레벨을 한 단계 바로 올린다 (레벨 보상 · 해금은 그대로, 바로 저장).
/// 에디터 메뉴라 빌드에는 들어가지 않는다.
/// </summary>
public static class DevLevelMenu
{
    private const string MenuPath = "OtterKingdom/Dev/Level +1";

    [MenuItem(MenuPath)]
    private static void LevelUp() => ProfileManager.Instance.DevLevelUp();

    [MenuItem(MenuPath, true)]
    private static bool CanLevelUp() => EditorApplication.isPlaying && ProfileManager.Instance != null;
}
