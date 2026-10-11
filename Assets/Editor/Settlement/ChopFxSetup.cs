using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 나무 베기 · 바위 캐기 손맛 연출 그림을 광장 · 광산 · 밭 씬에 연결한다 (도끼 · 곡괭이 · 반짝 · 먼지 · 나무 조각, 치운 뒤 그루터기 · 자갈).
/// 그림은 Tools/UIGen/chop_fx_art.py. 바위·나무를 새로 만드는 설정(SettlementSetup)도 같은 값을 쓴다.
/// 이미 연결된 치운 뒤 그림은 사람이 고친 값을 지키기 위해 덮어쓰지 않는다.
/// 배치 모드: -executeMethod ChopFxSetup.Apply
/// </summary>
public static class ChopFxSetup
{
    private const string ArtFolder = "Assets/Art/Settlement";
    private const string PropFolder = "Assets/Art/Plaza/Props";
    private static readonly string[] Scenes = { "Assets/Scenes/Plaza.unity", "Assets/Scenes/Mine.unity", "Assets/Scenes/Farm.unity" };

    // 치운 뒤 남는 것의 폭 (장애물 그림 폭 대비): 그루터기는 줄기보다 조금 넓게, 자갈은 바위 자리를 덮을 만큼
    private const float StumpScale = 0.42f;
    private const float RubbleScale = 0.85f;

    [MenuItem("Tools/Settlement/Apply Chop Fx")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[ChopFxSetup] Play 모드를 끄고 실행하세요.");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previous = SceneManager.GetActiveScene().path;
        int fxCount = 0, obstacleCount = 0;
        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var fx in Object.FindObjectsByType<PlazaNodeFx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Fill(fx);
                fxCount++;
            }
            foreach (var obstacle in Object.FindObjectsByType<ClearingObstacleView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                FillObstacle(obstacle);
                obstacleCount++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log($"[ChopFxSetup] 연출 {fxCount}개 · 장애물 {obstacleCount}개에 그림을 연결했습니다.");
    }

    /// <summary>바위·나무 손맛 연출에 도구 · 반짝 · 먼지 · 나무 조각 그림을 넣는다</summary>
    internal static void Fill(PlazaNodeFx fx)
    {
        var so = new SerializedObject(fx);
        so.FindProperty("_axe").objectReferenceValue = Art(ArtFolder, "FX_Axe");
        so.FindProperty("_pickaxe").objectReferenceValue = Art(ArtFolder, "FX_Pickaxe");
        so.FindProperty("_impact").objectReferenceValue = Art(ArtFolder, "FX_Sparkle");
        so.FindProperty("_dust").objectReferenceValue = Art(ArtFolder, "FX_Dust");
        so.FindProperty("_woodChip").objectReferenceValue = Art(ArtFolder, "FX_WoodChip");
        so.FindProperty("_dustColor").colorValue = new Color(1f, 1f, 1f, 0.9f); // 먼지 그림 색 그대로
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>장애물이 치워진 뒤 남길 그림 (나무 = 그루터기, 바위 = 자갈). 사람이 다른 그림을 넣었으면 그대로 둠</summary>
    internal static void FillObstacle(ClearingObstacleView view)
    {
        var so = new SerializedObject(view);
        bool tree = so.FindProperty("_kind").enumValueIndex == (int)ObstacleKind.Tree;
        var standard = tree ? Art(PropFolder, "Prop_Stump") : Art(ArtFolder, "Prop_Rock_Rubble");
        var cleared = so.FindProperty("_clearedSprite");
        if (cleared.objectReferenceValue != null && cleared.objectReferenceValue != standard)
            return;
        cleared.objectReferenceValue = standard;
        so.FindProperty("_clearedScale").floatValue = tree ? StumpScale : RubbleScale;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite Art(string folder, string name) => CollectionSetup.ImportSprite(folder, name);
}
