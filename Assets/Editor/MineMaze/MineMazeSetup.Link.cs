using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 광산 미로를 본 게임에 잇기 (Docs/광산_미로탐험과_왕도성장_기획.md 17장).
/// 1) 미로 장소 Zone_MineMaze (씬 MineMaze, 갱도를 찾아야(mine_maze_found) · 왕국 Lv.5부터). 이동 팝업 카드가 생기지 않도록 전역 UI 장소 목록에는 넣지 않음
/// 2) Resources/MineMazeLink (미로 장소 · 돌아갈 광산 장소 · 광산 카트 입구 빛)
/// 3) Build Settings에 MineMaze 씬 추가
/// 메뉴: Tools/Mine Maze/Connect To Main Game · 배치: -executeMethod MineMazeSetup.ConnectToGame (여러 번 실행해도 같음)
/// 메인 Lv.5 퀘스트 "오래된 갱도" · "갱도 속으로"는 QuestSetup 표 (Tools/Quest/Create Quest Data)
/// </summary>
public static partial class MineMazeSetup
{
    private const string ZoneFolder = "Assets/Scriptable Obejects/Navigation";
    private const string MazeZonePath = ZoneFolder + "/Zone_MineMaze.asset";
    private const string MineZonePath = ZoneFolder + "/Zone_Mine.asset";
    private const string LinkPath = "Assets/Resources/" + MineMazeLinkConfig.ResourcePath + ".asset";
    private const string MineIcon = "Assets/Art/UI/Travel/ICON_Place_Mine.png";
    private const string GlowSprite = "Assets/Art/MineRework/Fx/FX_Glow.png";

    [MenuItem("Tools/Mine Maze/Connect To Main Game")]
    public static void ConnectToGame()
    {
        var zone = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(MazeZonePath);
        if (zone == null)
        {
            zone = ScriptableObject.CreateInstance<ZoneDefinition>();
            AssetDatabase.CreateAsset(zone, MazeZonePath);
        }
        var zso = new SerializedObject(zone);
        zso.FindProperty("_zoneId").stringValue = MineMazeRules.SceneName;
        zso.FindProperty("_displayName").stringValue = "오래된 갱도";
        zso.FindProperty("_subtitle").stringValue = "광산 미로 탐험";
        zso.FindProperty("_lockedSubtitle").stringValue = "준비 중";
        if (zso.FindProperty("_icon").objectReferenceValue == null)
            zso.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(MineIcon);
        zso.FindProperty("_sceneName").stringValue = MineMazeRules.SceneName;
        zso.FindProperty("_isAvailable").boolValue = true;
        zso.FindProperty("_requiredDevelopment").stringValue = MineMazeRules.FoundDevelopment;
        zso.FindProperty("_developmentLockedSubtitle").stringValue = "깡깡이가 갱도를 찾으면 열려요";
        zso.FindProperty("_requiredLevel").intValue = MineMazeRules.FoundLevel;
        zso.FindProperty("_sortOrder").intValue = 4;
        zso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(zone);

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        var link = AssetDatabase.LoadAssetAtPath<MineMazeLinkConfig>(LinkPath);
        if (link == null)
        {
            link = ScriptableObject.CreateInstance<MineMazeLinkConfig>();
            AssetDatabase.CreateAsset(link, LinkPath);
        }
        var lso = new SerializedObject(link);
        lso.FindProperty("_mazeZone").objectReferenceValue = zone;
        lso.FindProperty("_mineZone").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(MineZonePath);
        if (lso.FindProperty("_glow").objectReferenceValue == null)
            lso.FindProperty("_glow").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(GlowSprite);
        lso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(link);
        if (lso.FindProperty("_mineZone").objectReferenceValue == null)
            Debug.LogWarning($"[MineMazeSetup] 광산 장소가 없습니다: {MineZonePath} (미로에서 나가기가 동작하지 않음)");

        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[MineMazeSetup] 본 게임 연결: {MazeZonePath}, {LinkPath}, Build Settings {ScenePath}");
    }
}
