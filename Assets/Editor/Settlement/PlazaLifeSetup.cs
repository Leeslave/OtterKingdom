using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static CollectionSetup;

/// <summary>
/// 광장 생활 (Docs/광장생활_작업기록.md): 해달이 가구를 쓰고, 해달마다 개성이 있고, 도감을 채우는 재미.
/// - 가구 6종 (그루터기 의자 · 통나무 의자 · 나무 벤치 · 가로등 · 피크닉 매트 · 작은 덤불): 아이템 · 꾸미기 물건 · 요정 상점(골드) · 가구 분류
/// - 광장 소품 프리팹(벤치 · 그루터기 · 통나무 · 가로등 · 피크닉)과 공동 식탁에 해달이 쓰는 자리(PlazaSpotView)
/// - 아늑함: 장난감(등급) · 가구 · 의자 · 가로등 · 식탁 건설 자리
/// - 희귀 장난감 해달의 방문 조건 (밤 · 낮 · 가로등 · 식탁 · 아늑함)
/// - 도감 해달 탭: 이야기 해달 5 · 장난감 해달 12 항목 (그림은 광장 모습에 색을 입힌 임시 그림, 못 만났을 때 만나는 방법 힌트)
/// 여러 번 실행해도 결과가 같다
/// </summary>
public static class PlazaLifeSetup
{
    private const string CategoryFolder = "Assets/Scriptable Obejects/Inventory/Category";
    private const string ItemFolder = "Assets/Scriptable Obejects/Inventory/Items/Decor";
    private const string ItemDatabasePath = "Assets/Scriptable Obejects/Inventory/ItemDatabase.asset";
    private const string CommonRarityPath = "Assets/Scriptable Obejects/Inventory/Rarity/Common.asset";
    private const string DecorFolder = "Assets/Scriptable Obejects/Decor/Decors";
    private const string PlotFolder = "Assets/Scriptable Obejects/Decor/Plots";
    private const string DecorCatalogPath = "Assets/Scriptable Obejects/Decor/DecorCatalog.asset";
    private const string ShopFolder = "Assets/Scriptable Obejects/Shop";
    private const string ShopCatalogPath = ShopFolder + "/ShopCatalog.asset";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string PropArtFolder = "Assets/Art/Plaza/Props";
    private const string PropPrefabFolder = "Assets/Prefabs/Plaza";
    private const string FurnitureArtFolder = "Assets/Art/Item/Furniture";
    private const string OtterArtFolder = "Assets/Art/Otter";
    private const string SettlementArtFolder = "Assets/Art/Settlement";
    private const string OtterFolder = "Assets/Scriptable Obejects/Settlement/Otters";
    private const string EntryFolder = "Assets/Scriptable Obejects/Collection/Entries";
    private const string CollectionDatabasePath = "Assets/Scriptable Obejects/Collection/CollectionDatabase.asset";
    private const string OtterTabPath = "Assets/Scriptable Obejects/Collection/Tabs/Tab_Otter.asset";
    private const string OtterSilhouettePath = "Assets/Art/UI/Collection/Silhouettes/SIL_Otter.png";
    private const string PlazaScenePath = "Assets/Scenes/Plaza.unity";

    // 가구: (소품 그림, 아이템 ID, 이름, 설명, 칸, 자리 종류(null = 자리 없음), 자리, 아늑함, 골드, 레벨)
    // 앉는 자리는 그림 안 비율(앉은 발 위치), 먹기·모이기는 차지한 칸 비율 (칸 밖도 됨, 놓인 물건은 걷기 장애물이라 가장자리에서 띄움)
    private static readonly (string prop, string id, string name, string description, Vector2Int size, PlazaSpotKind? spot, Vector2[] points,
        int coziness, int gold, int level)[] Furniture =
    {
        ("Stump", "furniture_stump", "그루터기 의자", "해달 한 마리가 걸터앉아 쉬기 좋은 그루터기예요.", new Vector2Int(1, 1),
            PlazaSpotKind.Sit, new[] { new Vector2(0.5f, 0.66f) }, 3, 400, 3),
        ("Log", "furniture_log", "통나무 의자", "둘이 나란히 걸터앉는 통나무예요.", new Vector2Int(2, 1),
            PlazaSpotKind.Sit, new[] { new Vector2(0.3f, 0.5f), new Vector2(0.62f, 0.66f) }, 4, 700, 4),
        ("Bench", "furniture_bench", "나무 벤치", "등받이가 있는 튼튼한 벤치예요. 둘이 앉아 쉬어요.", new Vector2Int(2, 2),
            PlazaSpotKind.Sit, new[] { new Vector2(0.38f, 0.39f), new Vector2(0.73f, 0.53f) }, 6, 1200, 6),
        ("Lamp", "furniture_lamp", "가로등", "밤이 되면 해달들이 불빛 아래 모여 이야기해요.", new Vector2Int(1, 2),
            PlazaSpotKind.Gather, new[] { new Vector2(-0.9f, 0.1f), new Vector2(1.9f, 0.1f), new Vector2(0.5f, -0.7f) }, 6, 1500, 8),
        ("Picnic", "furniture_picnic", "피크닉 매트", "해달들이 둘러앉아 간식을 먹어요.", new Vector2Int(3, 2),
            PlazaSpotKind.Eat, new[] { new Vector2(-0.15f, 0.5f), new Vector2(1.15f, 0.5f), new Vector2(0.3f, -0.35f), new Vector2(0.7f, -0.35f) }, 8, 2500, 10),
        ("Bush", "furniture_bush", "작은 덤불", "광장을 푸릇하게 꾸며 주는 덤불이에요.", new Vector2Int(1, 1),
            null, new Vector2[0], 2, 300, 2),
    };

    // 광장 소품 프리팹의 자리: (프리팹, 자리 종류, 그림 안 비율, 앉을 때 걸어오는 높이(로컬))
    private static readonly (string prefab, PlazaSpotKind kind, Vector2[] points, float approach)[] PrefabSpots =
    {
        ("Prop_Bench", PlazaSpotKind.Sit, new[] { new Vector2(0.38f, 0.39f), new Vector2(0.73f, 0.53f) }, -0.5f),
        ("Prop_Stump", PlazaSpotKind.Sit, new[] { new Vector2(0.5f, 0.66f) }, -0.45f),
        ("Prop_Log", PlazaSpotKind.Sit, new[] { new Vector2(0.3f, 0.5f), new Vector2(0.62f, 0.66f) }, -0.45f),
        ("Prop_Lamp", PlazaSpotKind.Gather, new[] { new Vector2(0.2f, -0.07f), new Vector2(1.11f, -0.07f), new Vector2(0.75f, -0.22f) }, 0f),
        ("Prop_Picnic", PlazaSpotKind.Eat, new[] { new Vector2(0.22f, 0.45f), new Vector2(0.82f, 0.55f), new Vector2(0.45f, 0.2f), new Vector2(0.6f, 0.85f) }, 0f),
    };

    // 건설 자리의 해달 자리 종류 · 아늑함 (자리는 씬의 소품 프리팹이 가짐, 방문 조건 · 아늑함 셈에만 씀)
    private static readonly (string plot, PlazaSpotKind? spot, int coziness)[] PlotLife =
    {
        ("Plot_Chair", PlazaSpotKind.Sit, 4),
        ("Plot_RestCorner", PlazaSpotKind.Sit, 4),
        ("Plot_Lamp", PlazaSpotKind.Gather, 4),
        ("Plot_CommunityTable", PlazaSpotKind.Eat, 6),
        ("Plot_WelcomePot", null, 2),
        ("Plot_WelcomeClothesline", null, 2),
    };

    // 장난감 아늑함: 등급별 (흔함 · 레어 · 에픽)
    private static readonly int[] ToyCoziness = { 2, 3, 5 };

    // 희귀 장난감 해달의 방문 조건
    private static readonly (string otter, VisitCondition condition, int value)[] Conditions =
    {
        ("otter_toy_kkeujeok", VisitCondition.Night, 0),
        ("otter_toy_duribeon", VisitCondition.Lamp, 0),
        ("otter_toy_jaejal", VisitCondition.Table, 0),
        ("otter_toy_kongkong", VisitCondition.Day, 0),
        ("otter_toy_kkomkkom", VisitCondition.Cozy, 20),
    };

    // 도감 해달 항목: (에셋, 해달 ID, 그림(폴더, 이름), 한 줄 소개, 설명, 좋아하는 것, 못 만났을 때 힌트(장난감 해달은 비우면 자동))
    private static readonly (string asset, string otter, string folder, string portrait, string tagline, string description, string extra, string hint)[] Entries =
    {
        ("Entry_OtterFirst", "otter_first", SettlementArtFolder, "ICON_Otter_Snack", "왕국에 처음 온 탐험가 해달",
            "광산과 밭과 선착장을 처음 찾아낸 호기심 많은 해달이에요.", "새로운 길", ""),
        ("Entry_OtterSleepy", "otter_sleepy", SettlementArtFolder, "ICON_Otter_Sleepy", "늘 졸린 나무꾼 해달",
            "느긋하게 나무를 베다가도 벤치에만 앉으면 꾸벅꾸벅 졸아요.", "그늘진 벤치", "첫 집을 지으면 놀러 와요"),
        ("Entry_OtterPainter", "otter_painter", SettlementArtFolder, "ICON_Otter_Painter", "그림 그리는 해달",
            "광장 곳곳을 그림으로 남기는 손재주 좋은 해달이에요.", "물감", "광산이 열리면 찾아와요"),
        ("Entry_OtterBuilder", "otter_builder", SettlementArtFolder, "ICON_Otter_Builder", "왕국의 건설 해달",
            "집이든 길드든 뚝딱뚝딱 지어 내는 건설 담당이에요.", "망치", "광산에서 일할 친구가 생기면 찾아와요"),
        ("Entry_OtterNeighbor", "otter_p3_neighbor", OtterArtFolder, "ICON_Otter_Farmer", "짐 들고 이사 온 이웃",
            "무거운 짐도 척척 나르는 운반 해달이에요.", "포근한 이불", "공동사업으로 새 이웃의 집을 지으면 이사 와요"),
        ("Entry_OtterToyKungkung", "otter_toy_kungkung", OtterArtFolder, "ICON_Otter_Toy_Kungkung", "덩치 큰 나무꾼 해달",
            "통나무 하나쯤은 번쩍 들어 올리는 힘센 해달이에요.", "흔들리는 나무", ""),
        ("Entry_OtterToyDegul", "otter_toy_degul", OtterArtFolder, "ICON_Otter_Toy_Degul", "조약돌을 모으는 해달",
            "반짝이는 돌만 보면 데굴데굴 굴려 보는 채굴 해달이에요.", "반짝이는 조약돌", ""),
        ("Entry_OtterToyBodeul", "otter_toy_bodeul", OtterArtFolder, "ICON_Otter_Toy_Bodeul", "풀잎 냄새 나는 해달",
            "새싹을 쓰다듬으면 쑥쑥 자란다고 믿는 농사 해달이에요.", "새싹", ""),
        ("Entry_OtterToyPongdang", "otter_toy_pongdang", OtterArtFolder, "ICON_Otter_Toy_Pongdang", "물방울 같은 해달",
            "바다만 보면 퐁당 뛰어드는 낚시 해달이에요.", "바닷가", ""),
        ("Entry_OtterToyYeongcha", "otter_toy_yeongcha", OtterArtFolder, "ICON_Otter_Toy_Yeongcha", "힘센 운반 해달",
            "영차영차 무엇이든 나르는 든든한 해달이에요.", "나뭇가지 더미", ""),
        ("Entry_OtterToyJjalrang", "otter_toy_jjalrang", OtterArtFolder, "ICON_Otter_Toy_Jjalrang", "동전을 좋아하는 해달",
            "짤랑짤랑 동전 소리를 들으면 기분이 좋아지는 장사 해달이에요.", "요정 상점", ""),
        ("Entry_OtterToyJaejal", "otter_toy_jaejal", OtterArtFolder, "ICON_Otter_Toy_Jaejal", "수다쟁이 해달",
            "식탁에 둘러앉아 이야기하는 걸 제일 좋아하는 장사 해달이에요.", "공동 식탁", ""),
        ("Entry_OtterToyKongkong", "otter_toy_kongkong", OtterArtFolder, "ICON_Otter_Toy_Kongkong", "망치 소리 해달",
            "해가 떠 있는 동안 콩콩 망치질을 하는 건설 해달이에요.", "공사 현장", ""),
        ("Entry_OtterToyKkomkkom", "otter_toy_kkomkkom", OtterArtFolder, "ICON_Otter_Toy_Kkomkkom", "꼼꼼한 손재주 해달",
            "아늑하게 꾸민 광장을 보면 찾아오는 손재주 해달이에요.", "예쁜 장난감", ""),
        ("Entry_OtterToyDuribeon", "otter_toy_duribeon", OtterArtFolder, "ICON_Otter_Toy_Duribeon", "길찾기 해달",
            "가로등 불빛을 따라 두리번두리번 찾아오는 탐험 해달이에요.", "가로등", ""),
        ("Entry_OtterToyKkeujeok", "otter_toy_kkeujeok", OtterArtFolder, "ICON_Otter_Toy_Kkeujeok", "밤의 메모쟁이 해달",
            "모두 잠든 밤에 찾아와 이것저것 적어 두는 기록 해달이에요.", "게시판", ""),
        ("Entry_OtterToySallang", "otter_toy_sallang", OtterArtFolder, "ICON_Otter_Sallang", "벚꽃 바람을 타고 온 해달",
            "봄바람에 실려 온 살랑살랑 한정 해달이에요.", "벚꽃 바람개비", ""),
    };

    [MenuItem("Tools/Settlement/Apply Plaza Life")]
    public static void ApplyPlazaLife()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ApplyFurniture();
        ApplyCoziness();
        ApplyPrefabSpots();
        ApplyVisitConditions();
        ApplyOtterEntries();

        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        ApplyTableSpot();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlazaLifeSetup] 광장 생활 적용 완료");
    }

    #region 가구

    private static void ApplyFurniture()
    {
        var decorCategory = AssetDatabase.LoadAssetAtPath<ItemCategory>($"{CategoryFolder}/Decor.asset");
        var (category, categoryIsNew) = LoadOrCreate<ItemCategory>($"{CategoryFolder}/Furniture.asset");
        var categorySo = new SerializedObject(category);
        categorySo.FindProperty("_categoryId").stringValue = "Furniture";
        categorySo.FindProperty("_displayName").stringValue = "가구";
        categorySo.FindProperty("_sortOrder").intValue = 1; // 장난감(0) 다음
        categorySo.FindProperty("_parent").objectReferenceValue = decorCategory;
        categorySo.ApplyModifiedPropertiesWithoutUndo();

        var rarity = AssetDatabase.LoadAssetAtPath<ItemRarity>(CommonRarityPath);
        var gold = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);
        var items = new List<ItemDefinition>();
        var decors = new List<DecorDefinition>();
        var products = new List<ShopProduct>();
        for (int i = 0; i < Furniture.Length; i++)
        {
            var f = Furniture[i];
            var item = LoadOrCreate<ItemDefinition>($"{ItemFolder}/{f.name}.asset").asset;
            var itemSo = new SerializedObject(item);
            itemSo.FindProperty("_itemId").stringValue = f.id;
            itemSo.FindProperty("_displayName").stringValue = f.name;
            itemSo.FindProperty("_description").stringValue = f.description;
            itemSo.FindProperty("_category").objectReferenceValue = category;
            itemSo.FindProperty("_rarity").objectReferenceValue = rarity;
            itemSo.FindProperty("_isSellable").boolValue = false; // 광장에 놓인 채로 팔리는 일이 없게
            itemSo.FindProperty("_maxStack").intValue = 99;
            itemSo.FindProperty("_icon").objectReferenceValue = ImportSprite(FurnitureArtFolder, $"ICON_Furniture_{f.prop}");
            itemSo.ApplyModifiedPropertiesWithoutUndo();
            items.Add(item);

            var decor = LoadOrCreate<DecorDefinition>($"{DecorFolder}/Decor_Furniture_{f.prop}.asset").asset;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropArtFolder}/Prop_{f.prop}.png");
            var decorSo = new SerializedObject(decor);
            decorSo.FindProperty("_item").objectReferenceValue = item;
            decorSo.ApplyModifiedPropertiesWithoutUndo();
            decor.SetupPlacement(f.size, false, sprite);
            decor.SetupFurniture(true, f.spot.HasValue, f.spot ?? PlazaSpotKind.Sit, f.points, f.coziness);
            EditorUtility.SetDirty(decor);
            decors.Add(decor);

            var product = LoadOrCreate<ShopProduct>($"{ShopFolder}/Products/Furniture_{f.prop}.asset").asset;
            var productSo = new SerializedObject(product);
            productSo.FindProperty("_productId").stringValue = $"shop_{f.id}";
            productSo.FindProperty("_sortOrder").intValue = 100 + i;
            productSo.FindProperty("_item").objectReferenceValue = item;
            productSo.FindProperty("_bundleSize").intValue = 1;
            productSo.FindProperty("_priceCurrency").objectReferenceValue = gold;
            productSo.FindProperty("_price").intValue = f.gold;
            productSo.FindProperty("_requiredLevel").intValue = f.level;
            productSo.ApplyModifiedPropertiesWithoutUndo();
            products.Add(product);
        }

        var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        var dbSo = new SerializedObject(database);
        foreach (var item in items)
            AddUnique(dbSo.FindProperty("_items"), item);
        dbSo.ApplyModifiedPropertiesWithoutUndo();

        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>(DecorCatalogPath);
        var catalogSo = new SerializedObject(catalog);
        foreach (var decor in decors)
            AddUnique(catalogSo.FindProperty("_decors"), decor);
        catalogSo.ApplyModifiedPropertiesWithoutUndo();

        // 요정 상점: 가구 탭 + 상품
        var shop = AssetDatabase.LoadAssetAtPath<ShopCatalog>(ShopCatalogPath);
        var shopSo = new SerializedObject(shop);
        var tabs = shopSo.FindProperty("_tabs");
        bool hasTab = false;
        for (int i = 0; i < tabs.arraySize; i++)
            hasTab |= tabs.GetArrayElementAtIndex(i).FindPropertyRelative("_category").objectReferenceValue == category;
        if (!hasTab)
        {
            tabs.arraySize++;
            var tab = tabs.GetArrayElementAtIndex(tabs.arraySize - 1);
            tab.FindPropertyRelative("_label").stringValue = "가구";
            tab.FindPropertyRelative("_category").objectReferenceValue = category;
        }
        foreach (var product in products)
            AddUnique(shopSo.FindProperty("_products"), product);
        shopSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // 장난감(등급별) · 건설 자리의 아늑함과 해달 자리 종류
    private static void ApplyCoziness()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:DecorDefinition", new[] { DecorFolder }))
        {
            var decor = AssetDatabase.LoadAssetAtPath<DecorDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (decor == null || decor.IsFurniture || decor is BuildingDefinition)
                continue;
            int tier = decor.Item != null && decor.Item.Rarity != null ? decor.Item.Rarity.Tier : 0;
            decor.SetupFurniture(false, false, PlazaSpotKind.Sit, null, ToyCoziness[Mathf.Clamp(tier, 0, ToyCoziness.Length - 1)]);
            EditorUtility.SetDirty(decor);
        }
        foreach (var row in PlotLife)
        {
            var plot = AssetDatabase.LoadAssetAtPath<ConstructionPlotDefinition>($"{PlotFolder}/{row.plot}.asset");
            if (plot == null)
                continue;
            plot.SetupFurniture(false, row.spot.HasValue, row.spot ?? PlazaSpotKind.Sit, null, row.coziness);
            EditorUtility.SetDirty(plot);
        }
    }

    #endregion

    #region 해달이 쓰는 자리

    // 소품 프리팹마다 자리 (모든 광장의 벤치 · 그루터기 · 통나무 · 가로등 · 피크닉 매트가 함께 씀)
    private static void ApplyPrefabSpots()
    {
        foreach (var row in PrefabSpots)
        {
            string path = $"{PropPrefabFolder}/{row.prefab}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var sprite = root.GetComponent<SpriteRenderer>() != null ? root.GetComponent<SpriteRenderer>().sprite : null;
                if (sprite == null)
                {
                    Debug.LogWarning($"[PlazaLifeSetup] {row.prefab}: 그림이 없어 자리를 만들지 못했습니다.");
                    continue;
                }
                var points = new List<Transform>();
                for (int i = 0; i < row.points.Length; i++)
                {
                    var point = root.transform.Find($"Spot{i + 1}");
                    if (point == null)
                    {
                        point = new GameObject($"Spot{i + 1}").transform;
                        point.SetParent(root.transform, false);
                    }
                    point.localPosition = (Vector3)((row.points[i] * sprite.rect.size - sprite.pivot) / sprite.pixelsPerUnit);
                    points.Add(point);
                }
                for (int i = row.points.Length; root.transform.Find($"Spot{i + 1}") != null; i++)
                    Object.DestroyImmediate(root.transform.Find($"Spot{i + 1}").gameObject);

                var spot = root.GetComponent<PlazaSpotView>();
                if (spot == null)
                    spot = root.AddComponent<PlazaSpotView>();
                spot.Setup(row.kind, points, null, row.approach);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    // 공동 식탁: 첫 모임 자리(Gathering/Seat1~4)에 둘러서서 먹음 (식탁을 옮기면 모임 자리가 따라감)
    private static void ApplyTableSpot()
    {
        var root = GameObject.Find("PlazaRoot").transform.Find("Settlement/P3");
        var table = root != null ? root.Find("CommunityTable") : null;
        var gathering = root != null ? root.Find("Gathering") : null;
        if (table == null || gathering == null)
        {
            Debug.LogWarning("[PlazaLifeSetup] 공동 식탁 · 모임 자리가 없어 식탁 자리를 만들지 못했습니다.");
            return;
        }
        var seats = new List<Transform>();
        foreach (Transform child in gathering)
        {
            if (child.name.StartsWith("Seat"))
                seats.Add(child);
        }
        seats.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var spot = table.GetComponent<PlazaSpotView>();
        if (spot == null)
            spot = table.gameObject.AddComponent<PlazaSpotView>();
        spot.Setup(PlazaSpotKind.Eat, seats, table, 0f);
        EditorUtility.SetDirty(spot);
    }

    #endregion

    #region 해달

    private static void ApplyVisitConditions()
    {
        var otters = LoadOtters();
        foreach (var row in Conditions)
        {
            if (!otters.TryGetValue(row.otter, out var otter))
                continue;
            var so = new SerializedObject(otter);
            so.FindProperty("_visitCondition").enumValueIndex = (int)row.condition;
            so.FindProperty("_visitConditionValue").intValue = row.value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // 도감 해달 탭 항목 + 해달 연결 + 도감 목록
    private static void ApplyOtterEntries()
    {
        var otters = LoadOtters();
        var tab = AssetDatabase.LoadAssetAtPath<CollectionTab>(OtterTabPath);
        var silhouette = AssetDatabase.LoadAssetAtPath<Sprite>(OtterSilhouettePath);
        var database = AssetDatabase.LoadAssetAtPath<CollectionDatabase>(CollectionDatabasePath);
        var dbSo = new SerializedObject(database);
        for (int i = 0; i < Entries.Length; i++)
        {
            var e = Entries[i];
            if (!otters.TryGetValue(e.otter, out var otter))
            {
                Debug.LogWarning($"[PlazaLifeSetup] 해달 '{e.otter}'이(가) 없어 도감 항목을 건너뜁니다.");
                continue;
            }
            var entry = LoadOrCreate<CollectionEntry>($"{EntryFolder}/{e.asset}.asset").asset;
            var so = new SerializedObject(entry);
            so.FindProperty("_entryId").stringValue = e.otter;
            so.FindProperty("_tab").objectReferenceValue = tab;
            so.FindProperty("_displayName").stringValue = otter.DisplayName;
            so.FindProperty("_sortOrder").intValue = 100 + i;
            so.FindProperty("_portrait").objectReferenceValue = ImportSprite(e.folder, e.portrait);
            so.FindProperty("_silhouette").objectReferenceValue = silhouette;
            so.FindProperty("_tagline").stringValue = e.tagline;
            so.FindProperty("_description").stringValue = e.description;
            so.FindProperty("_extraValue").stringValue = e.extra;
            so.FindProperty("_hint").stringValue = string.IsNullOrEmpty(e.hint) ? ToyHint(otter) : e.hint;
            so.ApplyModifiedPropertiesWithoutUndo();
            AddUnique(dbSo.FindProperty("_entries"), entry);

            var otterSo = new SerializedObject(otter);
            otterSo.FindProperty("_collectionEntry").objectReferenceValue = entry;
            otterSo.ApplyModifiedPropertiesWithoutUndo();
        }
        dbSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // 장난감 해달 힌트: 어떤 장난감을 놓으면 오는지 + 방문 조건
    private static string ToyHint(SettlementOtterDefinition otter)
    {
        string toy = otter.FavoriteToy != null ? $"{otter.FavoriteToy.DisplayName}을(를) 광장에 놓으면 찾아와요"
            : otter.VisitTier >= 2 ? "에픽 장난감을 광장에 놓으면 찾아와요"
            : otter.VisitTier == 1 ? "레어 장난감(퍼즐 등)을 광장에 놓으면 찾아와요"
            : "장난감(축구공 등)을 광장에 놓으면 찾아와요";
        string condition = ToyVisitRules.ConditionText(otter);
        return string.IsNullOrEmpty(condition) ? toy : $"{toy}\n{condition}";
    }

    private static Dictionary<string, SettlementOtterDefinition> LoadOtters()
    {
        var result = new Dictionary<string, SettlementOtterDefinition>();
        foreach (var guid in AssetDatabase.FindAssets("t:SettlementOtterDefinition", new[] { OtterFolder }))
        {
            var otter = AssetDatabase.LoadAssetAtPath<SettlementOtterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (otter != null && !string.IsNullOrEmpty(otter.OtterId))
                result[otter.OtterId] = otter;
        }
        return result;
    }

    #endregion

    private static void AddUnique(SerializedProperty list, Object value)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                return;
        }
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
    }
}
