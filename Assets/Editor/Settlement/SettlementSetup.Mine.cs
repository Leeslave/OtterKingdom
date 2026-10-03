using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static CollectionSetup;

/// <summary>
/// 광산 씬에 개척을 배치한다: 입구 앞을 막은 나무·돌(ClearingObstacleView)과 ZoneClearingView.
/// 다 치우기 전에는 광산 입구를 숨긴다. 여러 번 실행해도 결과가 같음.
/// </summary>
public static partial class SettlementSetup
{
    private const string MineScenePath = "Assets/Scenes/Mine.unity";
    private const string MineClearingName = "MineClearing";
    private const string PlazaTreeArtPath = "Assets/Art/Plaza/Props/Prop_Tree.png";
    private const float MineTreeScale = 0.32f;
    // 광산 배경(0)·입구 동그라미(1) 위, 광부 말풍선(3) 아래. 광산은 광장식 깊이 정렬(PlazaProp)을 쓰지 않음
    private const int MineObstacleOrder = 2;
    private const string MineHintObstacle = "mine_rock_01";

    // 입구(0.76, 3.83)로 가는 모래 길 가운데를 가로막음 (카메라 고정, 화면 위쪽은 안내 말풍선 자리라 피함)
    private static readonly (string id, ObstacleKind kind, Vector2 position)[] MineObstacles =
    {
        ("mine_tree_01", ObstacleKind.Tree, new Vector2(-2.0f, 0.6f)),
        ("mine_rock_01", ObstacleKind.Rock, new Vector2(-0.3f, 0.0f)),
        ("mine_tree_02", ObstacleKind.Tree, new Vector2(1.9f, -0.4f)),
        ("mine_rock_02", ObstacleKind.Rock, new Vector2(0.7f, -1.5f)),
    };

    [MenuItem("Tools/Settlement/Setup Mine Clearing")]
    public static void PlaceMine()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ImportArt();
        var scene = EditorSceneManager.OpenScene(MineScenePath, OpenSceneMode.Single);
        var entrance = Object.FindAnyObjectByType<MineEntranceView>(FindObjectsInactive.Include);
        if (entrance == null)
        {
            Debug.LogError("[SettlementSetup] 광산 씬에 MineEntranceView가 없습니다.");
            return;
        }

        var old = GameObject.Find(MineClearingName);
        if (old != null)
            Object.DestroyImmediate(old);
        var root = new GameObject(MineClearingName).transform;

        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodItemPath);
        // 광부 해달이 장애물 위를 걷지 않게: 걷기 영역이 장애물 발자국을 보고, 치우면 다시 계산
        var walkable = Object.FindAnyObjectByType<PlazaWalkableArea>();
        AddPolygonRoot(walkable, root);
        var obstacles = new System.Collections.Generic.List<ClearingObstacleView>();
        foreach (var o in MineObstacles)
            obstacles.Add(BuildObstacle(root, o.id, o.kind, o.position, o.kind == ObstacleKind.Rock ? stone : wood, walkable));

        // 광산: 배경(0) 위·장애물(2) 아래 그림자, 나비 한 마리, 구름 그림자는 없음 (바위로 둘러싸인 곳)
        BuildAmbience(root, 1, 1, 6, 0, 0, 3);

        var view = root.gameObject.AddComponent<ZoneClearingView>();
        var so = new SerializedObject(view);
        so.FindProperty("_zone").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(MineZonePath);
        SetList(so.FindProperty("_obstacles"), obstacles);
        SetList(so.FindProperty("_hiddenUntilCleared"), new System.Collections.Generic.List<GameObject> { entrance.gameObject });
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SettlementSetup] 광산 개척 배치 완료");
    }

    // 밭·낚시터 분위기 연출: (씬, 그림자 순서, 나비 수, 구름 그림자 수). 두 씬 모두 배경 0 이하, 해달 2
    private static readonly (string scene, int shadowOrder, int butterflies, int clouds, int fireflies)[] ZoneAmbience =
    {
        ("Assets/Scenes/Farm.unity", 1, 2, 1, 4),    // 밭: 고랑 0, 작물 칸 1 → 그림자는 작물 칸과 같은 높이(작물 위에 살짝 드리워짐)
        ("Assets/Scenes/Fishing.unity", 1, 1, 2, 3), // 낚시터: 배경 0, 낚시 자리 동그라미 1
    };

    [MenuItem("Tools/Settlement/Setup Zone Ambience")]
    public static void PlaceZoneAmbience()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ImportArt();
        foreach (var z in ZoneAmbience)
        {
            var scene = EditorSceneManager.OpenScene(z.scene, OpenSceneMode.Single);
            var old = GameObject.Find("Ambience");
            if (old != null)
                Object.DestroyImmediate(old);
            var root = new GameObject("AmbienceRoot").transform;
            BuildAmbience(root, z.shadowOrder, z.butterflies, 9, z.clouds, 10, z.fireflies);
            // 루트 이름을 Ambience로 (BuildAmbience가 만든 자식을 올림)
            var ambience = root.Find("Ambience");
            ambience.SetParent(null, true);
            Object.DestroyImmediate(root.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("[SettlementSetup] 밭·낚시터 분위기 연출 배치 완료");
    }

    private static ClearingObstacleView BuildObstacle(Transform root, string id, ObstacleKind kind, Vector2 position, ItemDefinition reward,
        PlazaWalkableArea walkable)
    {
        var node = new GameObject($"Obstacle_{id}").transform;
        node.SetParent(root, false);
        node.position = position;

        bool rock = kind == ObstacleKind.Rock;
        var sprite = rock ? LoadPropArt("Prop_Rock_0") : AssetDatabase.LoadAssetAtPath<Sprite>(PlazaTreeArtPath);
        var visual = CreateSprite("Visual", node, sprite, position, false);
        visual.sortingOrder = MineObstacleOrder;
        if (!rock)
            visual.transform.localScale = Vector3.one * MineTreeScale;
        var footprint = AddFootprint(node, rock ? RockFootprint : new Rect(-0.5f, -0.15f, 1.0f, 0.5f));

        var tap = node.gameObject.AddComponent<CircleCollider2D>();
        tap.radius = rock ? 0.85f : 1.1f;
        tap.offset = new Vector2(0f, rock ? 0.45f : 1.4f);

        var view = node.gameObject.AddComponent<ClearingObstacleView>();
        var so = new SerializedObject(view);
        so.FindProperty("_obstacleId").stringValue = id;
        so.FindProperty("_kind").enumValueIndex = (int)kind;
        so.FindProperty("_hits").intValue = rock ? 4 : 3;
        so.FindProperty("_reward").objectReferenceValue = reward;
        so.FindProperty("_rewardAmount").intValue = 2;
        var sprites = so.FindProperty("_sprites");
        sprites.arraySize = rock ? 3 : 1;
        for (int i = 0; i < sprites.arraySize; i++)
            sprites.GetArrayElementAtIndex(i).objectReferenceValue = rock ? LoadPropArt($"Prop_Rock_{i}") : sprite;
        so.FindProperty("_pieceSprite").objectReferenceValue = LoadPropArt(rock ? "FX_StoneChip" : "FX_Leaf");
        so.FindProperty("_renderer").objectReferenceValue = visual;
        so.FindProperty("_tapArea").objectReferenceValue = tap;
        so.FindProperty("_fx").objectReferenceValue = CreateNodeFx(node);
        so.FindProperty("_footprint").objectReferenceValue = footprint;
        so.FindProperty("_walkableArea").objectReferenceValue = walkable;
        // 화면 가운데 바위 하나에만 처음 안내
        if (id == MineHintObstacle)
            so.FindProperty("_hint").objectReferenceValue = CreateTapHint(node, "hint_obstacle", "톡톡 눌러 치워요!", new Vector3(0f, 1.35f, 0f));
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }
}
