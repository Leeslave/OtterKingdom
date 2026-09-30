using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 꾸미기 데이터(장난감 아이템·물건·격자·구역)를 만들고, 광장·밭·낚시터 씬에 격자(DecorBoardView)를 배치한다.
/// 이미 있는 에셋은 사람이 고친 값을 지키기 위해 덮어쓰지 않는다. 씬의 격자 위치·칸 크기도 이미 있으면 그대로 둔다.
/// 배치 모드: Unity.exe -batchmode -projectPath . -executeMethod DecorSetup.Run -quit
/// </summary>
public static class DecorSetup
{
    private const string DataFolder = "Assets/Scriptable Obejects/Decor";
    internal const string CatalogPath = DataFolder + "/DecorCatalog.asset";
    private const string CategoryFolder = "Assets/Scriptable Obejects/Inventory/Category";
    private const string ItemFolder = "Assets/Scriptable Obejects/Inventory/Items/Decor";
    private const string ItemDatabasePath = "Assets/Scriptable Obejects/Inventory/ItemDatabase.asset";
    private const string CommonRarityPath = "Assets/Scriptable Obejects/Inventory/Rarity/Common.asset";
    private const string ToyArtFolder = "Assets/Art/Item/Toy";
    private const string ZoneFolder = "Assets/Scriptable Obejects/Navigation";

    // (에셋, 아이템 ID, 이름, 설명, 아이콘, 놀이 반응, 동시 인원)
    private static readonly (string asset, string id, string name, string description, string icon, DecorPlayStyle style, int players)[] Toys =
    {
        ("SoccerBall", "toy_soccerball", "축구공", "해달들이 통통 차며 노는 공이에요.", "ICON_Toy_SoccerBall", DecorPlayStyle.Bounce, 2),
        ("Puzzle", "toy_puzzle", "퍼즐", "조각을 맞추며 시간을 보내요.", "ICON_Toy_Puzzle", DecorPlayStyle.Wiggle, 1),
    };

    // 장소별 격자: (ID, 장소 에셋, 씬, 격자 왼쪽 아래, 칸 크기, 칸 수, 깊이 정렬, 고정 순서, 걷기 영역 밖 허용, 장애물)
    // 광장: 걷기 영역 전체 / 밭: 흙마당 (밭고랑은 DecorBlockArea) / 낚시터: 부두 뒤쪽 가장자리 한 줄 (해달은 앞쪽에서 놂)
    private static readonly (string id, string zone, string scene, Vector2 origin, float cell, Vector2Int size, bool depth, int order, bool outside, bool blockWalk)[] Boards =
    {
        ("plaza", "Zone_Plaza", "Assets/Scenes/Plaza.unity", new Vector2(-12f, -14.4f), 1f, new Vector2Int(24, 33), true, 0, false, true),
        ("farm", "Zone_Farm", "Assets/Scenes/Farm.unity", new Vector2(-3.6f, -5.4f), 0.6f, new Vector2Int(13, 16), false, 1, true, false),
        ("fishing", "Zone_Fishing", "Assets/Scenes/Fishing.unity", new Vector2(-4.4f, 1.85f), 0.8f, new Vector2Int(6, 1), false, 1, true, false),
    };

    [MenuItem("Tools/Decor/Setup Decor (데이터 + 씬 격자)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[DecorSetup] Play 모드를 끄고 실행하세요.");
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string previousScene = SceneManager.GetActiveScene().path;
        CreateData();
        PlaceInScenes();

        if (!string.IsNullOrEmpty(previousScene))
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        Debug.Log("[DecorSetup] 완료 — 전역 UI에 DecorManager를 넣으려면 Tools/Navigation/Build Global UI도 실행하세요.");
    }

    #region 데이터

    public static void CreateData()
    {
        EnsureFolder(DataFolder + "/Decors");
        EnsureFolder(DataFolder + "/Boards");
        EnsureFolder(DataFolder + "/Regions");
        EnsureFolder(ItemFolder);

        var (decorCategory, toyCategory) = CreateCategories();
        var rarity = AssetDatabase.LoadAssetAtPath<ItemRarity>(CommonRarityPath);

        var decors = new List<DecorDefinition>();
        var items = new List<ItemDefinition>();
        foreach (var t in Toys)
        {
            var (item, itemIsNew) = LoadOrCreate<ItemDefinition>($"{ItemFolder}/{t.name}.asset");
            var itemSo = new SerializedObject(item);
            if (itemIsNew)
            {
                itemSo.FindProperty("_itemId").stringValue = t.id;
                itemSo.FindProperty("_displayName").stringValue = t.name;
                itemSo.FindProperty("_description").stringValue = t.description;
                itemSo.FindProperty("_category").objectReferenceValue = toyCategory;
                itemSo.FindProperty("_rarity").objectReferenceValue = rarity;
                itemSo.FindProperty("_isSellable").boolValue = false; // 광장에 놓인 채로 팔리는 일이 없게
                itemSo.FindProperty("_maxStack").intValue = 99;
            }
            FillIfEmpty(itemSo, "_icon", ImportSprite(ToyArtFolder, t.icon));
            itemSo.ApplyModifiedPropertiesWithoutUndo();
            items.Add(item);

            var (decor, decorIsNew) = LoadOrCreate<DecorDefinition>($"{DataFolder}/Decors/Decor_{t.asset}.asset");
            if (decorIsNew)
            {
                var so = new SerializedObject(decor);
                so.FindProperty("_item").objectReferenceValue = item;
                so.FindProperty("_footprint").vector2IntValue = Vector2Int.one;
                so.FindProperty("_playStyle").enumValueIndex = (int)t.style;
                so.FindProperty("_maxPlayers").intValue = t.players;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            decors.Add(decor);
        }

        RegisterItems(items);

        // 목록은 폴더 안의 모든 물건으로 다시 채움 (사람이 추가한 물건도 포함)
        var (catalog, _) = LoadOrCreate<DecorCatalog>(CatalogPath);
        var catalogSo = new SerializedObject(catalog);
        SetList(catalogSo.FindProperty("_decors"), FindAll<DecorDefinition>(DataFolder + "/Decors"));
        catalogSo.ApplyModifiedPropertiesWithoutUndo();

        foreach (var b in Boards)
            CreateBoard(b.id, b.zone, b.size);

        AssetDatabase.SaveAssets();
    }

    // 가방 탭 "꾸미기" (그 안에 소분류 칩 "장난감")
    private static (ItemCategory decor, ItemCategory toy) CreateCategories()
    {
        var (decor, decorIsNew) = LoadOrCreate<ItemCategory>($"{CategoryFolder}/Decor.asset");
        if (decorIsNew)
        {
            var so = new SerializedObject(decor);
            so.FindProperty("_categoryId").stringValue = "Decor";
            so.FindProperty("_displayName").stringValue = "꾸미기";
            so.FindProperty("_sortOrder").intValue = 2; // 농사(0) · 낚시(1) 다음
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 꾸미기 물건은 광장에 놓는 것이라 가방 칸을 차지하지 않음 (하위 분류 장난감도 따라감)
        var decorSo = new SerializedObject(decor);
        decorSo.FindProperty("_usesBagCapacity").boolValue = false;
        decorSo.ApplyModifiedPropertiesWithoutUndo();

        var (toy, toyIsNew) = LoadOrCreate<ItemCategory>($"{CategoryFolder}/Toy.asset");
        if (toyIsNew)
        {
            var so = new SerializedObject(toy);
            so.FindProperty("_categoryId").stringValue = "Toy";
            so.FindProperty("_displayName").stringValue = "장난감";
            so.FindProperty("_sortOrder").intValue = 0;
            so.FindProperty("_parent").objectReferenceValue = decor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return (decor, toy);
    }

    // 세이브 복원이 아이템을 찾을 수 있도록 ItemDatabase에 추가 (이미 있으면 그대로)
    private static void RegisterItems(List<ItemDefinition> items)
    {
        var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        if (database == null)
        {
            Debug.LogWarning($"[DecorSetup] ItemDatabase가 없습니다: {ItemDatabasePath}");
            return;
        }

        var so = new SerializedObject(database);
        var list = so.FindProperty("_items");
        var existing = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
            existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

        foreach (var item in items.Where(item => !existing.Contains(item)))
        {
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 격자 + 격자 전체를 덮는 기본 구역(처음부터 열림). 잠긴 구역·해금 조건은 기획이 정하면 에셋으로 추가
    private static void CreateBoard(string id, string zoneAsset, Vector2Int size)
    {
        var (region, regionIsNew) = LoadOrCreate<DecorRegionDefinition>($"{DataFolder}/Regions/Region_{id}_base.asset");
        if (regionIsNew)
        {
            var so = new SerializedObject(region);
            so.FindProperty("_regionId").stringValue = $"{id}_base";
            so.FindProperty("_displayName").stringValue = "기본 구역";
            so.FindProperty("_unlockedByDefault").boolValue = true;
            var areas = so.FindProperty("_areas");
            areas.arraySize = 1;
            areas.GetArrayElementAtIndex(0).rectIntValue = new RectInt(Vector2Int.zero, size);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var (board, boardIsNew) = LoadOrCreate<DecorBoardDefinition>($"{DataFolder}/Boards/Board_{id}.asset");
        if (boardIsNew)
        {
            var so = new SerializedObject(board);
            so.FindProperty("_boardId").stringValue = id;
            so.FindProperty("_zone").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ZoneDefinition>($"{ZoneFolder}/{zoneAsset}.asset");
            so.FindProperty("_size").vector2IntValue = size;
            var regions = so.FindProperty("_regions");
            regions.arraySize = 1;
            regions.GetArrayElementAtIndex(0).objectReferenceValue = region;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>전역 UI의 DecorManager에 넣을 격자 목록</summary>
    internal static List<DecorBoardDefinition> LoadBoards()
    {
        return Boards.Select(b => AssetDatabase.LoadAssetAtPath<DecorBoardDefinition>($"{DataFolder}/Boards/Board_{b.id}.asset"))
            .Where(board => board != null)
            .ToList();
    }

    /// <summary>가방 원본(InventoryTestScene)의 탭 목록에 꾸미기·장난감을 넣는다 (GlobalUISetup이 복제 전에 호출)</summary>
    internal static void EnsureBagCategories(GameObject sourceCanvas)
    {
        var presenter = sourceCanvas.GetComponentInChildren<InventoryPresenter>(true);
        var so = new SerializedObject(presenter);
        var list = so.FindProperty("_categories");
        var existing = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
            existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

        foreach (var name in new[] { "Decor", "Toy" })
        {
            var category = AssetDatabase.LoadAssetAtPath<ItemCategory>($"{CategoryFolder}/{name}.asset");
            if (category == null || existing.Contains(category))
                continue;

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = category;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region 씬 배치

    public static void PlaceInScenes()
    {
        foreach (var b in Boards)
        {
            if (!File.Exists(b.scene))
                continue;

            var scene = EditorSceneManager.OpenScene(b.scene, OpenSceneMode.Single);
            var board = AssetDatabase.LoadAssetAtPath<DecorBoardDefinition>($"{DataFolder}/Boards/Board_{b.id}.asset");

            var view = Object.FindAnyObjectByType<DecorBoardView>(FindObjectsInactive.Include);
            bool isNew = view == null;
            if (isNew)
            {
                var go = new GameObject("DecorBoard");
                go.transform.position = b.origin;
                view = go.AddComponent<DecorBoardView>();
            }

            var so = new SerializedObject(view);
            so.FindProperty("_board").objectReferenceValue = board;
            if (isNew)
            {
                so.FindProperty("_cellSize").floatValue = b.cell;
                so.FindProperty("_depthSorted").boolValue = b.depth;
                so.FindProperty("_sortingOrder").intValue = b.order;
                so.FindProperty("_allowOutsideWalkable").boolValue = b.outside;
                so.FindProperty("_toysBlockWalking").boolValue = b.blockWalk;
                so.FindProperty("_walkableArea").objectReferenceValue = Object.FindAnyObjectByType<PlazaWalkableArea>();
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (b.id == "farm")
                SetupFarm(view);
            else if (b.id == "fishing")
                SetupFishing(view);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[DecorSetup] {b.scene}: 꾸미기 격자 {(isNew ? "추가" : "갱신")}");
        }
    }

    // 밭: 밭고랑마다 막는 영역, 농부 해달의 웨이포인트·수확 자리·오프라인 NPC 자리는 비워 둠
    private static void SetupFarm(DecorBoardView view)
    {
        var slots = Object.FindObjectsByType<FurrowSlotView>(FindObjectsSortMode.None);
        var blocks = BlockRoot();
        foreach (var group in slots.GroupBy(slot => slot.PlotIndex))
        {
            string name = $"DecorBlock_Plot{group.Key}";
            if (GameObject.Find(name) != null)
                continue;

            float minX = group.Min(slot => slot.transform.position.x);
            float maxX = group.Max(slot => slot.transform.position.x);
            float y = group.Average(slot => slot.transform.position.y);
            var go = new GameObject(name);
            go.transform.SetParent(blocks, true);
            go.transform.position = new Vector3((minX + maxX) * 0.5f, y, 0f);
            var area = go.AddComponent<DecorBlockArea>();
            var areaSo = new SerializedObject(area);
            areaSo.FindProperty("_size").vector2Value = new Vector2(maxX - minX + 1.6f, 1.3f);
            areaSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var reserved = new List<Transform>();
        var farmer = Object.FindAnyObjectByType<FarmerOtterController>();
        if (farmer != null)
        {
            reserved.Add(farmer.transform);
            var farmerSo = new SerializedObject(farmer);
            var waypoints = farmerSo.FindProperty("wanderWaypoints");
            for (int i = 0; i < waypoints.arraySize; i++)
            {
                if (waypoints.GetArrayElementAtIndex(i).objectReferenceValue is Transform t)
                    reserved.Add(t);
            }
        }
        reserved.AddRange(slots.Select(slot => slot.transform));
        var npc = Object.FindAnyObjectByType<OfflineFarmNpcView>();
        if (npc != null)
            reserved.Add(npc.transform);

        SetReserved(view, reserved);
    }

    // 낚시터: 낚시 자리는 비워 둠 (격자가 부두 뒤쪽 한 줄이라 겹치지는 않지만, 격자를 늘릴 때를 대비)
    private static void SetupFishing(DecorBoardView view)
    {
        var spot = Object.FindAnyObjectByType<FishingSpotView>();
        if (spot == null || GameObject.Find("DecorBlock_FishingSpot") != null)
            return;

        var go = new GameObject("DecorBlock_FishingSpot");
        go.transform.SetParent(BlockRoot(), true);
        go.transform.position = spot.transform.position;
        var area = go.AddComponent<DecorBlockArea>();
        var so = new SerializedObject(area);
        so.FindProperty("_size").vector2Value = new Vector2(2f, 1.6f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 막는 영역은 격자와 따로 둔다: 격자 위치를 옮겨도 밭고랑·낚시 자리 영역은 제자리에 있어야 함
    private static Transform BlockRoot()
    {
        var root = GameObject.Find("DecorBlocks");
        if (root == null)
            root = new GameObject("DecorBlocks");
        return root.transform;
    }

    private static void SetReserved(DecorBoardView view, List<Transform> points)
    {
        var so = new SerializedObject(view);
        var list = so.FindProperty("_reservedPoints");
        list.arraySize = points.Count;
        for (int i = 0; i < points.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion
}
