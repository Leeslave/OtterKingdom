using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static CollectionSetup;

/// <summary>
/// 전문 해달(광부·농부·낚시꾼)과 밭 현장 개간:
/// - 광부·낚시꾼의 광장 프리팹 (광산·낚시터의 그림·클립 그대로, 광장 해달처럼 걷게)
/// - 밭 씬의 개간 현장: 잡목·바위(직접 치움) + 주민 정비 현장 + 개간·농부 배치 전에는 밭·농부를 숨기는 ZoneClearingView
/// - 낚시터 씬: 선착장을 고치고 낚시꾼을 배치하기 전에는 낚시 해달·낚시 자리를 숨기는 ZoneClearingView
/// - 한 번에 적용하는 메뉴 (Tools/Settlement/Apply Specialist Progression, 낚시터만: Apply Fishing Dock)
/// 데이터·프리팹·씬은 이미 저장소에 들어 있다. 이 메뉴는 표를 고친 뒤 다시 만들 때 쓴다 (여러 번 실행해도 결과가 같음).
/// </summary>
public static partial class SettlementSetup
{
    private const string MinerPrefabPath = "Assets/Prefabs/Mine/MinerOtter.prefab";
    private const string MinerPlazaPrefabPath = PlazaPrefabFolder + "/PlazaOtter_Miner.prefab";
    private const string FisherPrefabPath = "Assets/Prefabs/Fishing/FishingOtter.prefab";
    private const string FisherPlazaPrefabPath = PlazaPrefabFolder + "/PlazaOtter_Fisher.prefab";
    // 광부·낚시꾼 그림(둘 다 PPU 145, 키 약 1.52)을 광장 해달 키(1.67)에 맞춤
    private const float MinerPlazaScale = 1.1f;
    private static readonly string[] MinerWalkClips = { "WalkRight", "WalkLeft", "WalkDown", "WalkUp" };

    private const string FishingScenePath = "Assets/Scenes/Fishing.unity";
    private const string FishingDockName = "FishingDock";

    private const string FarmScenePath = "Assets/Scenes/Farm.unity";
    private const string FarmClearingName = "FarmClearing";
    private const string FarmHintObstacle = "farm_rock_01";
    // 밭 해달(농부 0.6배, 키 약 1.2)은 광부(키 약 1.9)보다 작아 잡목·바위도 줄임
    private const float FarmObstacleScale = 0.65f;

    // 밭 고랑 세 줄(위 y 1.9, 가운데 -0.2, 아래 -2.55, 가운데 x 0.3) 위를 덮은 잡목·바위. 카메라 고정 (ortho 8, 가로 ±4.5)
    private static readonly (string id, ObstacleKind kind, Vector2 position)[] FarmObstacles =
    {
        ("farm_tree_01", ObstacleKind.Tree, new Vector2(-2.3f, 2.0f)),
        ("farm_rock_01", ObstacleKind.Rock, new Vector2(0.4f, 1.1f)),
        ("farm_tree_02", ObstacleKind.Tree, new Vector2(2.4f, -0.8f)),
        ("farm_rock_02", ObstacleKind.Rock, new Vector2(-0.9f, -1.7f)),
    };

    // 밭 정비 현장: 가운데 고랑 근처에서 일하고, 화면 아래 길로 들어오고 나감
    private static readonly Vector2[] FarmStandPoints = { new Vector2(-1.0f, 0.1f), new Vector2(1.2f, -0.1f), new Vector2(0.1f, -0.8f) };
    private static readonly Vector2 FarmLookPoint = new Vector2(0.2f, 0.6f);
    private static readonly Vector2 FarmEntryPoint = new Vector2(0.2f, -7f);
    private static readonly Vector2 FarmMarkerPoint = new Vector2(0.2f, 0.4f);
    private static readonly Vector2 FarmProgressPoint = new Vector2(0.2f, 1.7f);

    [MenuItem("Tools/Settlement/Apply Specialist Progression")]
    public static void ApplySpecialistProgression()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        BuildMinerPlazaPrefab();
        BuildFisherPlazaPrefab();
        CollectionSetup.CreateData();
        CreateData();
        QuestSetup.CreateData();
        PlaceMine();
        PlaceFarm();
        PlaceFishing();
        Debug.Log("[SettlementSetup] 전문 해달 진행 적용 완료: 광부·낚시꾼 광장 프리팹, 도감, 정착 데이터, 퀘스트, 광산·밭·낚시터 현장");
    }

    // 낚시터만 (Lv.15 선착장 → 낚시꾼 배치): 낚시꾼 광장 프리팹 → 정착 데이터 → 낚시터 씬
    [MenuItem("Tools/Settlement/Apply Fishing Dock")]
    public static void ApplyFishingDock()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        BuildFisherPlazaPrefab();
        CreateData();
        SetFishingZone();
        PlaceFishing();
        Debug.Log("[SettlementSetup] 낚시터 적용 완료: 낚시꾼 광장 프리팹, 정착 데이터(선착장·낚시꾼·지역·Lv.15), 이동 화면 조건, 낚시터 씬");
    }

    // 이동 화면의 낚시터: 왕국 Lv.15 + 선착장 (전체는 Build Global UI가 같은 값으로 만듦)
    private static void SetFishingZone()
    {
        var zone = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(FishingZonePath);
        var so = new SerializedObject(zone);
        so.FindProperty("_requiredLevel").intValue = FishingLevel;
        so.FindProperty("_requiredDevelopment").stringValue = FishingDevelopment;
        so.FindProperty("_developmentLockedSubtitle").stringValue = "선착장을 고치면 열려요";
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    #region 광부·낚시꾼 광장 프리팹

    // 광산의 광부 프리팹(MinerOtter)이 쓰는 클립(SpriteFrameAnimator: 오른쪽/왼쪽/앞/뒤 걷기)을 그대로 쓰는 광장 해달.
    // OtterVisualController가 클립 이름으로 걷게 한다. 서 있을 때는 앞을 봄 (앞으로 걷기 첫 칸)
    [MenuItem("Tools/Settlement/Build Miner Plaza Otter")]
    public static void BuildMinerPlazaPrefab() => BuildSpecialistPlazaPrefab(MinerPrefabPath, MinerPlazaPrefabPath, "광부", "광산 씬 설정");

    // 낚시터의 낚시 해달 프리팹(FishingOtter)도 같은 이름의 걷기 클립을 가져서 같은 방식
    [MenuItem("Tools/Settlement/Build Fisher Plaza Otter")]
    public static void BuildFisherPlazaPrefab() => BuildSpecialistPlazaPrefab(FisherPrefabPath, FisherPlazaPrefabPath, "낚시꾼", "낚시터 씬 설정");

    private static void BuildSpecialistPlazaPrefab(string sourcePrefabPath, string plazaPrefabPath, string label, string setupName)
    {
        var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
        var sourceAnimator = sourcePrefab != null ? sourcePrefab.GetComponent<SpriteFrameAnimator>() : null;
        if (sourceAnimator == null)
        {
            Debug.LogError($"[SettlementSetup] {label} 프리팹에 SpriteFrameAnimator가 없습니다: {sourcePrefabPath} ({setupName}을 먼저 실행하세요)");
            return;
        }
        var sourceClips = new SerializedObject(sourceAnimator).FindProperty("clips");

        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(plazaPrefabPath) != null;
        var root = exists ? PrefabUtility.LoadPrefabContents(plazaPrefabPath) : new GameObject(System.IO.Path.GetFileNameWithoutExtension(plazaPrefabPath));
        var sortingGroup = GetOrAdd<SortingGroup>(root);
        GetOrAdd<OtterWanderAgent>(root);
        var visual = GetOrAdd<OtterVisualController>(root);

        var body = root.transform.Find("Visual");
        if (body == null)
        {
            body = new GameObject("Visual").transform;
            body.SetParent(root.transform, false);
        }
        // 광부·낚시꾼 그림은 발밑 피벗이라 루트 = 발밑
        body.localPosition = Vector3.zero;
        body.localScale = new Vector3(MinerPlazaScale, MinerPlazaScale, 1f);
        var oldAnimator = body.GetComponent<Animator>();
        if (oldAnimator != null)
            Object.DestroyImmediate(oldAnimator, true);

        var spriteRenderer = GetOrAdd<SpriteRenderer>(body.gameObject);
        var frames = GetOrAdd<SpriteFrameAnimator>(body.gameObject);
        var framesSo = new SerializedObject(frames);
        var clips = framesSo.FindProperty("clips");
        clips.arraySize = 0;
        Sprite front = null;
        foreach (var clipName in MinerWalkClips)
        {
            var source = FindClip(sourceClips, clipName);
            if (source == null)
            {
                Debug.LogError($"[SettlementSetup] {label} 클립이 없습니다: {clipName}");
                continue;
            }
            var sourceFrames = source.FindPropertyRelative("frames");
            var sprites = new Sprite[sourceFrames.arraySize];
            for (int i = 0; i < sprites.Length; i++)
                sprites[i] = (Sprite)sourceFrames.GetArrayElementAtIndex(i).objectReferenceValue;
            AddClip(clips, clipName, sprites, source.FindPropertyRelative("fps").floatValue);
            if (clipName == "WalkDown" && sprites.Length > 0)
                front = sprites[0];
        }
        AddClip(clips, "Idle", new[] { front }, 1f);
        framesSo.ApplyModifiedPropertiesWithoutUndo();
        spriteRenderer.sprite = front;

        var so = new SerializedObject(visual);
        so.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
        so.FindProperty("animator").objectReferenceValue = null;
        so.FindProperty("sortingGroup").objectReferenceValue = sortingGroup;
        so.FindProperty("frameAnimator").objectReferenceValue = frames;
        so.FindProperty("spriteFacesRight").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, plazaPrefabPath);
        if (exists)
            PrefabUtility.UnloadPrefabContents(root);
        else
            Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SettlementSetup] {label} 광장 해달: {plazaPrefabPath}");
    }

    private static SerializedProperty FindClip(SerializedProperty clips, string clipName)
    {
        for (int i = 0; i < clips.arraySize; i++)
        {
            var clip = clips.GetArrayElementAtIndex(i);
            if (clip.FindPropertyRelative("name").stringValue == clipName)
                return clip;
        }
        return null;
    }

    private static void AddClip(SerializedProperty clips, string clipName, Sprite[] sprites, float fps)
    {
        clips.arraySize++;
        var clip = clips.GetArrayElementAtIndex(clips.arraySize - 1);
        clip.FindPropertyRelative("name").stringValue = clipName;
        clip.FindPropertyRelative("fps").floatValue = fps;
        clip.FindPropertyRelative("loop").boolValue = true;
        var list = clip.FindPropertyRelative("frames");
        list.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    #endregion

    #region 밭 현장 개간

    // 밭 씬: 잡목·바위를 직접 치움 → 주민 해달이 농경지 개간(비용) → 운영 → 광장에서 농부를 배치하면 밭·농부가 나타나고 농사 안내
    [MenuItem("Tools/Settlement/Setup Farm Clearing")]
    public static void PlaceFarm()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ImportArt();
        var scene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Single);
        var farmer = Object.FindAnyObjectByType<FarmerOtterController>(FindObjectsInactive.Include);
        if (farmer == null)
        {
            Debug.LogError("[SettlementSetup] 밭 씬에 FarmerOtterController가 없습니다.");
            return;
        }

        var old = GameObject.Find(FarmClearingName);
        if (old != null)
            Object.DestroyImmediate(old);
        var root = new GameObject(FarmClearingName).transform;

        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodItemPath);
        var obstacles = new List<ClearingObstacleView>();
        foreach (var o in FarmObstacles)
            obstacles.Add(BuildObstacle(root, o.id, o.kind, o.position, o.kind == ObstacleKind.Rock ? stone : wood, null,
                FarmHintObstacle, FarmObstacleScale));

        BuildTaskSite(root, FarmRegionPath, farmer.GetComponentInChildren<SpriteRenderer>(), "hint_farm_workers", "농경지 개간\n0:45",
            FarmStandPoints, FarmLookPoint, FarmEntryPoint, FarmMarkerPoint, FarmProgressPoint);

        // 개간하고 농부를 배치하기 전에는 밭(고랑·작물 칸)·농부·오프라인 농사 해달을 숨김
        var hidden = new List<GameObject>();
        foreach (var plot in Object.FindObjectsByType<PlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            hidden.Add(plot.gameObject);
        hidden.Add(farmer.gameObject);
        var offlineNpc = Object.FindAnyObjectByType<OfflineFarmNpcView>(FindObjectsInactive.Include);
        if (offlineNpc != null)
            hidden.Add(offlineNpc.gameObject);

        var view = root.gameObject.AddComponent<ZoneClearingView>();
        var so = new SerializedObject(view);
        so.FindProperty("_zone").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(FarmZonePath);
        SetList(so.FindProperty("_obstacles"), obstacles);
        SetList(so.FindProperty("_hiddenUntilCleared"), hidden);
        so.FindProperty("_guide").stringValue = "밭을 덮은 잡목과 바위를 톡톡 눌러 치워요!";
        so.FindProperty("_awaitingWorkersGuide").stringValue = "다 치웠어요! 망치 표지판을 눌러 주민 해달을 보내요";
        so.FindProperty("_preparingGuide").stringValue = "주민 해달이 농경지를 개간하고 있어요";
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SettlementSetup] 밭 개간 현장 배치 완료");
    }

    #endregion

    #region 낚시터 (선착장 · 낚시꾼 배치)

    // 낚시터 씬: 치울 장애물은 없음. 선착장을 고치고(광장 게시판) 낚시꾼을 광장에서 배치하기 전에는
    // 낚시 해달·낚시 자리를 숨기고 "광장에서 첨벙이를 만나 낚시터에 배치해 주세요" 안내. 배치하면 나타나고 낚시터 안내 → 낚시
    [MenuItem("Tools/Settlement/Setup Fishing Dock")]
    public static void PlaceFishing()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(FishingScenePath, OpenSceneMode.Single);
        var fisher = Object.FindAnyObjectByType<FishingOtterController>(FindObjectsInactive.Include);
        var spot = Object.FindAnyObjectByType<FishingSpotView>(FindObjectsInactive.Include);
        if (fisher == null || spot == null)
        {
            Debug.LogError("[SettlementSetup] 낚시터 씬에 FishingOtterController 또는 FishingSpotView가 없습니다.");
            return;
        }

        var old = GameObject.Find(FishingDockName);
        if (old != null)
            Object.DestroyImmediate(old);
        var root = new GameObject(FishingDockName);

        var view = root.AddComponent<ZoneClearingView>();
        var so = new SerializedObject(view);
        so.FindProperty("_zone").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(FishingZonePath);
        SetList(so.FindProperty("_obstacles"), new List<ClearingObstacleView>());
        SetList(so.FindProperty("_hiddenUntilCleared"), new List<GameObject> { fisher.gameObject, spot.gameObject });
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SettlementSetup] 낚시터 선착장 배치 완료");
    }

    #endregion
}
