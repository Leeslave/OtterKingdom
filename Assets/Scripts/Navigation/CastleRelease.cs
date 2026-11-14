using UnityEngine.SceneManagement;

/// <summary>왕국의 성까지 공개하는 빌드. 광장 이외의 씬에서도 같은 진행 범위를 사용한다.</summary>
public static class CastleRelease
{
    public const string PlazaPath = "Assets/Scenes/CastleRelease/Plaza.unity";
    public const string FinalQuestId = "main_lv20_castle";
    public const string UpdateMessage = "업데이트를 기다려주세요!";

    public static bool IsActive => SceneUtility.GetBuildIndexByScenePath(PlazaPath) >= 0
        || SceneManager.GetActiveScene().path == PlazaPath;
}
