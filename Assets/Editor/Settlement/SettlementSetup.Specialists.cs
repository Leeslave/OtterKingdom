using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using static CollectionSetup;

/// <summary>
/// 전문 해달(광부·농부)과 밭 현장 개간:
/// - 광부의 광장 프리팹 (광산 그림 그대로, 광장 해달처럼 걷게)
/// - 밭 씬의 개간 현장: 잡목·바위(직접 치움) + 주민 정비 현장 + 개간·농부 배치 전에는 밭·농부를 숨기는 ZoneClearingView
/// - 한 번에 적용하는 메뉴 (Tools/Settlement/Apply Specialist Progression)
/// 여러 번 실행해도 결과가 같음.
/// </summary>
public static partial class SettlementSetup
{
    private const string MinerSpriteDir = "Assets/Sprites/Characters/MinerOtter";
    private const string MinerAnimDir = "Assets/Animations/MinerPlaza";
    private const string MinerPlazaPrefabPath = PlazaPrefabFolder + "/PlazaOtter_Miner.prefab";

    private const string FarmScenePath = "Assets/Scenes/Farm.unity";
    private const string FarmClearingName = "FarmClearing";
    private const string FarmHintObstacle = "farm_rock_01";

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
        CollectionSetup.CreateData();
        CreateData();
        QuestSetup.CreateData();
        PlaceMine();
        PlaceFarm();
        Debug.Log("[SettlementSetup] 전문 해달 진행 적용 완료: 광부 광장 프리팹, 도감, 정착 데이터, 퀘스트, 광산·밭 현장");
    }

    #region 광부 광장 프리팹

    // 광산 그림(MineSceneSetup이 자른 MinerOtter_Walk: 오른쪽/왼쪽/앞/뒤 줄, 8칸)으로 광장 해달 애니메이터를 만든다.
    // OtterVisualController가 쓰는 파라미터(IsMoving, WalkDir, WalkAnimSpeed)와 같게: 옆 걸음은 오른쪽, 왼쪽은 좌우 뒤집기
    [MenuItem("Tools/Settlement/Build Miner Plaza Otter")]
    public static void BuildMinerPlazaPrefab()
    {
        string walkPath = $"{MinerSpriteDir}/MinerOtter_Walk.png";
        var sprites = AssetDatabase.LoadAllAssetsAtPath(walkPath).OfType<Sprite>().ToDictionary(s => s.name);
        if (!sprites.ContainsKey("MinerOtter_Walk_0_0"))
        {
            Debug.LogError($"[SettlementSetup] 광부 그림이 잘려 있지 않습니다: {walkPath} (광산 씬 설정을 먼저 실행하세요)");
            return;
        }
        Sprite[] Row(int row) => Enumerable.Range(0, 8).Select(col => sprites[$"MinerOtter_Walk_{row}_{col}"]).ToArray();

        if (AssetDatabase.IsValidFolder(MinerAnimDir))
            AssetDatabase.DeleteAsset(MinerAnimDir);
        Directory.CreateDirectory(MinerAnimDir);
        AssetDatabase.Refresh();

        var front = Row(2);
        var clips = new Dictionary<string, AnimationClip>
        {
            // 서 있을 때는 앞을 보고 가끔 한 발짝 (걷기 첫 칸을 길게)
            { "Idle", BuildSpriteClip($"{MinerAnimDir}/Miner_Idle.anim", new[] { front[0], front[0], front[0], front[1] }, 3f) },
            { "Walk", BuildSpriteClip($"{MinerAnimDir}/Miner_Walk.anim", Row(0), 10f) },
            { "Walk_Down", BuildSpriteClip($"{MinerAnimDir}/Miner_Walk_Down.anim", front, 10f) },
            { "Walk_Up", BuildSpriteClip($"{MinerAnimDir}/Miner_Walk_Up.anim", Row(3), 10f) },
        };
        var controller = BuildWalkController($"{MinerAnimDir}/MinerPlaza.controller", clips);

        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(MinerPlazaPrefabPath) != null;
        var root = exists ? PrefabUtility.LoadPrefabContents(MinerPlazaPrefabPath) : new GameObject("PlazaOtter_Miner");
        var sortingGroup = GetOrAdd<SortingGroup>(root);
        GetOrAdd<OtterWanderAgent>(root);
        var visual = GetOrAdd<OtterVisualController>(root);

        var body = root.transform.Find("Visual");
        if (body == null)
        {
            body = new GameObject("Visual").transform;
            body.SetParent(root.transform, false);
        }
        // 광부 그림은 발밑 피벗이라 루트 = 발밑. 광장 해달 키에 맞춰 키움
        var settings = AssetDatabase.LoadAssetAtPath<PlazaSettings>(PlazaSettingsPath);
        float plazaHeight = settings != null ? settings.otterHeight : 1.67f;
        float minerHeight = front[0].bounds.size.y;
        body.localPosition = Vector3.zero;
        body.localScale = Vector3.one * (minerHeight > 0f ? plazaHeight / minerHeight * 0.92f : 1f);

        var spriteRenderer = GetOrAdd<SpriteRenderer>(body.gameObject);
        spriteRenderer.sprite = front[0];
        var animator = GetOrAdd<Animator>(body.gameObject);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var so = new SerializedObject(visual);
        so.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("sortingGroup").objectReferenceValue = sortingGroup;
        so.FindProperty("spriteFacesRight").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, MinerPlazaPrefabPath);
        if (exists)
            PrefabUtility.UnloadPrefabContents(root);
        else
            Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SettlementSetup] 광부 광장 해달: {MinerPlazaPrefabPath}");
    }

    private static AnimationClip BuildSpriteClip(string path, Sprite[] frames, float fps)
    {
        var clip = new AnimationClip { frameRate = fps };
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
        // 마지막 칸을 한 번 더 넣어 모든 칸이 1/fps씩 보이게
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i <= frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[Mathf.Min(i, frames.Length - 1)] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // Idle ↔ 걷기 3방향 (WalkDir: 0 옆, 1 아래, 2 위). OtterVisitorSpriteSetup의 컨트롤러와 같은 모양 (동작 트리거 없음)
    private static AnimatorController BuildWalkController(string path, Dictionary<string, AnimationClip> clips)
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = controller.layers[0].stateMachine;
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("WalkDir", AnimatorControllerParameterType.Int);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "WalkAnimSpeed",
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f,
        });

        var idle = sm.AddState("Idle");
        idle.motion = clips["Idle"];
        sm.defaultState = idle;

        var walks = new (int dir, AnimatorState state)[]
        {
            (0, sm.AddState("Walk")),
            (1, sm.AddState("Walk_Down")),
            (2, sm.AddState("Walk_Up")),
        };
        foreach (var (dir, state) in walks)
        {
            state.motion = clips[state.name];
            state.speedParameter = "WalkAnimSpeed";
            state.speedParameterActive = true;

            var toWalk = idle.AddTransition(state);
            toWalk.hasExitTime = false;
            toWalk.duration = 0f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            toWalk.AddCondition(AnimatorConditionMode.Equals, dir, "WalkDir");

            var toIdle = state.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

            foreach (var (otherDir, other) in walks)
            {
                if (other == state)
                    continue;
                var toOther = state.AddTransition(other);
                toOther.hasExitTime = false;
                toOther.duration = 0f;
                toOther.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
                toOther.AddCondition(AnimatorConditionMode.Equals, otherDir, "WalkDir");
            }
        }
        EditorUtility.SetDirty(controller);
        return controller;
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
            obstacles.Add(BuildObstacle(root, o.id, o.kind, o.position, o.kind == ObstacleKind.Rock ? stone : wood, null, FarmHintObstacle));

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
        so.FindProperty("_awaitingWorkersGuide").stringValue = "다 치웠어요! 주민 해달을 보내 농경지를 개간해요";
        so.FindProperty("_preparingGuide").stringValue = "주민 해달이 농경지를 개간하고 있어요";
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SettlementSetup] 밭 개간 현장 배치 완료");
    }

    #endregion
}
