using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 정착 진행(P0) 데이터를 만든다: 해달 4마리, 방명록, 건설 3개, 게시판 부탁 3개, 설정, 목재 아이템, 그림 가져오기.
/// 표의 값으로 매번 덮어쓴다 (수치를 바꾸려면 이 표를 고치고 다시 실행). GlobalUISetup이 함께 호출한다.
/// 화면은 SettlementSetup.UI.cs, 광장 배치는 SettlementSetup.Plaza.cs.
/// 설계: Docs/정착진행_P0.md
/// </summary>
public static partial class SettlementSetup
{
    private const string DataFolder = "Assets/Scriptable Obejects/Settlement";
    internal const string ConfigPath = DataFolder + "/SettlementConfig.asset";
    internal const string ArtFolder = "Assets/Art/Settlement";
    private const string CategoryFolder = "Assets/Scriptable Obejects/Inventory/Category";
    private const string MiningItemFolder = "Assets/Scriptable Obejects/Inventory/Items/Mining";
    private const string WoodItemPath = MiningItemFolder + "/목재.asset";
    private const string StoneItemPath = MiningItemFolder + "/돌.asset";
    private const string PotatoItemPath = "Assets/Scriptable Obejects/Inventory/Items/Farming/감자.asset";
    internal const string AppleItemPath = "Assets/Scriptable Obejects/Inventory/Items/Farming/사과.asset";
    private const int AppleSellPrice = 10;
    private const string ItemDatabasePath = "Assets/Scriptable Obejects/Inventory/ItemDatabase.asset";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string GemPath = "Assets/Scriptable Obejects/Gem.asset";
    private const string PlazaPrefabFolder = "Assets/Prefabs/Plaza";

    // 왕국 단계 이름 (상단바 아래 칩)
    private static readonly string[] StageNames = { "빈터", "첫 정착", "작은 정착지", "자급 시작" };

    // 옛 세이브에 부탁과 별도로 열어 줄 발전 (아직 부탁이 없는 낚시터)
    internal const string FishingDevelopment = "fishing_dock";

    // (에셋, ID, 이름, 얼굴 아이콘, 광장 프리팹, 건설 해달, 처음 왔을 때 방명록)
    private static readonly (string asset, string id, string name, string portrait, string prefab, bool builder, string arrival)[] Otters =
    {
        ("Otter_First", "otter_first", "몽실", "ICON_Otter_Snack", "PlazaOtter_Snack", false, "gb_first_arrival"),
        ("Otter_Painter", "otter_painter", "물감이", "ICON_Otter_Painter", "PlazaOtter_Painter", false, "gb_painter_arrival"),
        ("Otter_Sleepy", "otter_sleepy", "꾸벅이", "ICON_Otter_Sleepy", "PlazaOtter_Sleepy", false, "gb_sleepy_arrival"),
        ("Otter_Builder", "otter_builder", "뚝딱이", "ICON_Otter_Builder", null, true, "gb_builder_arrival"),
    };

    // 광장에서 눌렀을 때 하는 말, 정착 후보의 첫 이야기와 그걸 들으면 열리는 발전 (집 부탁의 조건)
    internal const string PainterIntroDevelopment = "met_painter";
    private static readonly Dictionary<string, (string[] lines, string intro, string introDevelopment)> OtterTalk =
        new Dictionary<string, (string[], string, string)>
    {
        { "otter_first", (new[] { "바닷바람이 좋아요~", "오늘도 좋은 하루!", "조개 줍기 하고 싶다…", "여기가 우리 왕국이에요!" }, "", "") },
        { "otter_painter", (new[] { "그림 그리기 좋은 날이에요!", "광장 풍경이 참 예뻐요.", "창문에 걸 그림을 그리는 중이에요." },
            "저도 여기 살고 싶어요!\n집을 지어 주실래요?", PainterIntroDevelopment) },
        { "otter_sleepy", (new[] { "하암… 졸려요…", "햇살이 따뜻해요…", "조금만 더 잘게요…" }, "", "") },
        { "otter_builder", (new[] { "뚝딱뚝딱!", "뭐든 지어 드릴게요!", "재료만 주면 뚝딱!" }, "", "") },
    };

    // (ID, 남긴 해달, 표시, 말)
    private static readonly (string id, string otter, string tag, string message)[] Entries =
    {
        ("gb_first_arrival", "otter_first", "첫 방문", "바닷바람이 포근한 곳이네요.\n여기 머물러도 될까요?"),
        ("gb_first_settle", "otter_first", "정착", "작은 집이 생겼어요!\n오늘부터 여기가 우리 집이에요."),
        ("gb_painter_arrival", "otter_painter", "방문", "광장이 예뻐서 그림 그리러 왔어요.\n저도 여기 살고 싶어요!"),
        ("gb_sleepy_arrival", "otter_sleepy", "방문", "햇살이 따뜻해서…\n낮잠 자기 딱 좋네요."),
        ("gb_builder_arrival", "otter_builder", "도착", "집 짓는 건 저한테 맡겨요!\n뚝딱뚝딱!"),
        ("gb_neighbor_settle", "otter_painter", "정착", "새 집 정말 고마워요!\n창문에 그림을 걸어 둘게요."),
        ("gb_farmland", "otter_builder", "개간", "길을 열었어요!\n이제 밭에서 먹거리를 길러요."),
    };

    // (ID, 이름, 아이콘, 종류, 골드, 목재, 돌, 초, 건설 해달, 진행 제목, 진행 안내, 결과 발전)
    private static readonly (string id, string name, string icon, ConstructionTarget target, int gold, int wood, int stone,
        float seconds, bool builder, string label, string hint, string result)[] Constructions =
    {
        ("con_house_1", "작은 집", "ICON_House_Blue", ConstructionTarget.House, 0, 8, 5, 10f, false,
            "작은 집 짓는 중", "다 지으면 몽실이가 정착해요", "house_1"),
        ("con_house_2", "새 이웃의 집", "ICON_House_Red", ConstructionTarget.House, 150, 18, 8, 30f, true,
            "새 이웃의 집 짓는 중", "다 지으면 새 이웃이 정착해요", "house_2"),
        ("con_farmland", "농경지 개간", "ICON_Clearing", ConstructionTarget.Clearing, 200, 16, 10, 45f, true,
            "농경지 개간 중", "완료하면 첫 밭이 열려요", "farmland"),
    };

    // (ID, 순서, 제목, 설명, 아이콘, 부탁한 해달, 필요 발전, 필요 주민, 건설, 정착, 찾아옴(해달:상태), 단계, 완료 기록, 완료 문구)
    private static readonly (string id, int order, string title, string description, string icon, string requester,
        string requires, int residents, string construction, string[] settles, (string otter, ResidentState state)[] arrivals,
        int stage, string entry, string message)[] Requests =
    {
        ("req_first_house", 0, "첫 번째 집 만들기", "몽실이가 머물 작은 집이 필요해요.\n목재와 돌로 지어 줘요.", "ICON_House_Blue",
            "otter_first", "", 0, "con_house_1", new[] { "otter_first" },
            new[] { ("otter_painter", ResidentState.SettlementCandidate), ("otter_sleepy", ResidentState.Visitor), ("otter_builder", ResidentState.SpecialNpc) },
            1, "gb_first_settle", "첫 주민이 정착했어요!"),
        ("req_neighbor_house", 1, "새 이웃의 집", "물감이가 이웃이 되고 싶대요.\n건설 해달과 집을 지어 줘요.", "ICON_House_Red",
            "otter_painter", PainterIntroDevelopment, 0, "con_house_2", new[] { "otter_painter", "otter_builder" },
            new (string, ResidentState)[0],
            2, "gb_neighbor_settle", "새 이웃이 정착했어요!\n주민이 3명이 됐어요."),
        ("req_farmland", 2, "먹거리를 길러요", "주민이 늘었어요.\n농경지를 개간해 밭을 만들어요.", "ICON_Clearing",
            "otter_first", "house_2", 3, "con_farmland", new string[0],
            new (string, ResidentState)[0],
            3, "gb_farmland", "농경지가 열렸어요!\n이제 밭에 갈 수 있어요."),
    };

    // 새 게임 시작 재료 = 첫 집 비용 (시안 1: 목재 8, 돌 5)
    private const int StartingWood = 8;
    private const int StartingStone = 5;
    private const int StartingGold = 100; // 시안 1

    [MenuItem("Tools/Settlement/Create Data")]
    public static void CreateData()
    {
        ImportArt();

        EnsureFolder(DataFolder);
        foreach (var sub in new[] { "Otters", "Guestbook", "Constructions", "Requests" })
            EnsureFolder($"{DataFolder}/{sub}");

        var wood = CreateWoodItem();
        CreateFruitItem();
        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        if (stone == null)
            Debug.LogError($"[SettlementSetup] 돌 아이템이 없습니다: {StoneItemPath}");

        // 방명록 → 해달 → (방명록의 해달 연결) 순서: 서로를 참조하므로
        var entries = new Dictionary<string, GuestbookEntryDefinition>();
        foreach (var e in Entries)
            entries[e.id] = LoadOrCreate<GuestbookEntryDefinition>($"{DataFolder}/Guestbook/{e.id}.asset").asset;

        var otters = new Dictionary<string, SettlementOtterDefinition>();
        foreach (var o in Otters)
        {
            var otter = LoadOrCreate<SettlementOtterDefinition>($"{DataFolder}/Otters/{o.asset}.asset").asset;
            var so = new SerializedObject(otter);
            so.FindProperty("_otterId").stringValue = o.id;
            so.FindProperty("_displayName").stringValue = o.name;
            so.FindProperty("_portrait").objectReferenceValue = LoadArt(o.portrait);
            so.FindProperty("_plazaPrefab").objectReferenceValue = o.prefab != null
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{PlazaPrefabFolder}/{o.prefab}.prefab")
                : null;
            so.FindProperty("_isBuilder").boolValue = o.builder;
            so.FindProperty("_arrivalEntry").objectReferenceValue = entries[o.arrival];
            var talk = OtterTalk[o.id];
            var lines = so.FindProperty("_lines");
            lines.arraySize = talk.lines.Length;
            for (int i = 0; i < talk.lines.Length; i++)
                lines.GetArrayElementAtIndex(i).stringValue = talk.lines[i];
            so.FindProperty("_introLine").stringValue = talk.intro;
            so.FindProperty("_introDevelopment").stringValue = talk.introDevelopment;
            so.ApplyModifiedPropertiesWithoutUndo();
            otters[o.id] = otter;
        }

        foreach (var e in Entries)
        {
            var so = new SerializedObject(entries[e.id]);
            so.FindProperty("_entryId").stringValue = e.id;
            so.FindProperty("_otter").objectReferenceValue = otters[e.otter];
            so.FindProperty("_tag").stringValue = e.tag;
            so.FindProperty("_message").stringValue = e.message;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var constructions = new Dictionary<string, ConstructionDefinition>();
        foreach (var c in Constructions)
        {
            var construction = LoadOrCreate<ConstructionDefinition>($"{DataFolder}/Constructions/{c.id}.asset").asset;
            var so = new SerializedObject(construction);
            so.FindProperty("_constructionId").stringValue = c.id;
            so.FindProperty("_displayName").stringValue = c.name;
            so.FindProperty("_icon").objectReferenceValue = LoadArt(c.icon);
            so.FindProperty("_targetType").enumValueIndex = (int)c.target;
            so.FindProperty("_requiredGold").intValue = c.gold;
            var items = so.FindProperty("_requiredItems");
            items.arraySize = 0;
            AddItemAmount(items, wood, c.wood);
            AddItemAmount(items, stone, c.stone);
            so.FindProperty("_durationSeconds").floatValue = c.seconds;
            so.FindProperty("_needsBuilder").boolValue = c.builder;
            so.FindProperty("_progressLabel").stringValue = c.label;
            so.FindProperty("_progressHint").stringValue = c.hint;
            so.FindProperty("_unlockResultId").stringValue = c.result;
            so.ApplyModifiedPropertiesWithoutUndo();
            constructions[c.id] = construction;
        }

        var requests = new List<BoardRequestDefinition>();
        foreach (var r in Requests)
        {
            var request = LoadOrCreate<BoardRequestDefinition>($"{DataFolder}/Requests/{r.id}.asset").asset;
            var so = new SerializedObject(request);
            so.FindProperty("_requestId").stringValue = r.id;
            so.FindProperty("_order").intValue = r.order;
            so.FindProperty("_title").stringValue = r.title;
            so.FindProperty("_description").stringValue = r.description;
            so.FindProperty("_icon").objectReferenceValue = LoadArt(r.icon);
            so.FindProperty("_requester").objectReferenceValue = otters[r.requester];
            so.FindProperty("_requiredDevelopment").stringValue = r.requires;
            so.FindProperty("_minResidents").intValue = r.residents;
            so.FindProperty("_construction").objectReferenceValue = constructions[r.construction];

            var settles = so.FindProperty("_settles");
            settles.arraySize = r.settles.Length;
            for (int i = 0; i < r.settles.Length; i++)
                settles.GetArrayElementAtIndex(i).objectReferenceValue = otters[r.settles[i]];

            var arrivals = so.FindProperty("_arrivals");
            arrivals.arraySize = r.arrivals.Length;
            for (int i = 0; i < r.arrivals.Length; i++)
            {
                var element = arrivals.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_otter").objectReferenceValue = otters[r.arrivals[i].otter];
                element.FindPropertyRelative("_state").enumValueIndex = (int)r.arrivals[i].state;
            }

            so.FindProperty("_stageOnComplete").intValue = r.stage;
            so.FindProperty("_completionEntry").objectReferenceValue = entries[r.entry];
            so.FindProperty("_completionMessage").stringValue = r.message;
            so.ApplyModifiedPropertiesWithoutUndo();
            requests.Add(request);
        }

        var config = LoadOrCreate<SettlementConfig>(ConfigPath).asset;
        var configSo = new SerializedObject(config);
        configSo.FindProperty("_goldCurrency").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);
        var stages = configSo.FindProperty("_stageNames");
        stages.arraySize = StageNames.Length;
        for (int i = 0; i < StageNames.Length; i++)
            stages.GetArrayElementAtIndex(i).stringValue = StageNames[i];
        configSo.FindProperty("_firstOtter").objectReferenceValue = otters["otter_first"];
        SetList(configSo.FindProperty("_otters"), new List<SettlementOtterDefinition>(otters.Values));
        SetList(configSo.FindProperty("_requests"), requests);
        SetList(configSo.FindProperty("_guestbookEntries"), new List<GuestbookEntryDefinition>(entries.Values));
        var legacy = configSo.FindProperty("_legacyDevelopments");
        legacy.arraySize = 1;
        legacy.GetArrayElementAtIndex(0).stringValue = FishingDevelopment;
        var starting = configSo.FindProperty("_startingItems");
        starting.arraySize = 0;
        AddItemAmount(starting, wood, StartingWood);
        AddItemAmount(starting, stone, StartingStone);
        configSo.FindProperty("_startingGold").intValue = StartingGold;
        configSo.FindProperty("_gatherItem").objectReferenceValue = wood;
        configSo.FindProperty("_gatherAmount").intValue = 2;
        configSo.FindProperty("_gatherCooldownSeconds").floatValue = 60f;
        configSo.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
    }

    private static void AddItemAmount(SerializedProperty list, ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
            return;
        list.arraySize++;
        var element = list.GetArrayElementAtIndex(list.arraySize - 1);
        element.FindPropertyRelative("_item").objectReferenceValue = item;
        element.FindPropertyRelative("_amount").intValue = amount;
    }

    #region 목재

    // 가방 "광산" 탭 아래 소분류 "재료"에 목재. 세이브 복원이 찾도록 ItemDatabase에도 등록
    private static ItemDefinition CreateWoodItem()
    {
        var mining = AssetDatabase.LoadAssetAtPath<ItemCategory>($"{CategoryFolder}/Mining.asset");
        var (material, materialIsNew) = LoadOrCreate<ItemCategory>($"{CategoryFolder}/Material.asset");
        if (materialIsNew)
        {
            var so = new SerializedObject(material);
            so.FindProperty("_categoryId").stringValue = "Material";
            so.FindProperty("_displayName").stringValue = "재료";
            so.FindProperty("_sortOrder").intValue = 1; // 광석 다음
            so.FindProperty("_parent").objectReferenceValue = mining;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var (wood, _) = LoadOrCreate<ItemDefinition>(WoodItemPath);
        var woodSo = new SerializedObject(wood);
        woodSo.FindProperty("_itemId").stringValue = "material_wood";
        woodSo.FindProperty("_displayName").stringValue = "목재";
        woodSo.FindProperty("_description").stringValue = "광장에서 주운 나뭇가지를 다듬은 목재. 집을 짓고 길을 여는 데 쓴다.";
        woodSo.FindProperty("_icon").objectReferenceValue = LoadArt("ICON_Item_Wood");
        woodSo.FindProperty("_category").objectReferenceValue = material;
        if (stone != null)
            woodSo.FindProperty("_rarity").objectReferenceValue = new SerializedObject(stone).FindProperty("_rarity").objectReferenceValue;
        woodSo.FindProperty("_isSellable").boolValue = true;
        woodSo.FindProperty("_sellPrice").intValue = 1;
        woodSo.FindProperty("_maxStack").intValue = 999;
        woodSo.ApplyModifiedPropertiesWithoutUndo();

        AddToDatabase(wood);
        return wood;
    }

    // 광장 나무를 흔들면 가끔 떨어지는 열매 (작물 분류, 팔면 골드)
    private static ItemDefinition CreateFruitItem()
    {
        var crops = AssetDatabase.LoadAssetAtPath<ItemCategory>($"{CategoryFolder}/Crops.asset");
        var potato = AssetDatabase.LoadAssetAtPath<ItemDefinition>(PotatoItemPath);
        var (apple, _) = LoadOrCreate<ItemDefinition>(AppleItemPath);
        var so = new SerializedObject(apple);
        so.FindProperty("_itemId").stringValue = "fruit_apple";
        so.FindProperty("_displayName").stringValue = "사과";
        so.FindProperty("_description").stringValue = "광장 나무를 흔들면 가끔 떨어지는 빨간 사과. 팔면 골드가 된다.";
        so.FindProperty("_icon").objectReferenceValue = LoadArt("ICON_Item_Apple");
        so.FindProperty("_category").objectReferenceValue = crops;
        if (potato != null)
            so.FindProperty("_rarity").objectReferenceValue = new SerializedObject(potato).FindProperty("_rarity").objectReferenceValue;
        so.FindProperty("_isSellable").boolValue = true;
        so.FindProperty("_sellPrice").intValue = AppleSellPrice;
        so.FindProperty("_maxStack").intValue = 999;
        so.ApplyModifiedPropertiesWithoutUndo();

        AddToDatabase(apple);
        return apple;
    }

    private static void AddToDatabase(ItemDefinition item)
    {
        var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        var dbSo = new SerializedObject(database);
        var list = dbSo.FindProperty("_items");
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == item)
                return;
        }
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
        dbSo.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region 그림

    // 광장 소품: (이름, 월드 단위 키(세로), 발밑 피벗 Y(0~1)) — Tools/UIGen/settlement_art.py가 만든 PNG
    private static readonly (string name, float worldHeight, float pivotY)[] PropArt =
    {
        ("Prop_Board", 2.7f, 0.03f),
        ("Prop_Branches", 0.95f, 0.18f),
        ("Prop_Pebbles", 0.85f, 0.1f),
        ("Prop_Rock_0", 1.3f, 0.1f), // 광장 바위: 금 간 단계·자갈이 같은 캔버스라 같은 크기·피벗 (fit_plaza_nodes.py)
        ("Prop_Rock_1", 1.3f, 0.1f),
        ("Prop_Rock_2", 1.3f, 0.1f),
        ("Prop_Rock_Rubble", 1.3f, 0.1f),
        ("Prop_AppleTree_0", 3.4f, 0.1f), // 사과나무: 사과 달림 / 흔든 뒤
        ("Prop_AppleTree_1", 3.4f, 0.1f),
        ("FX_StoneChip", 0.3f, 0.5f),
        ("FX_Leaf", 0.3f, 0.5f),
        ("FX_Sparkle", 0.6f, 0.5f),
        ("Prop_Foundation", 3.4f, 0.16f),
        ("Prop_FarmSign_Locked", 1.9f, 0.03f),
        ("Prop_FarmSign", 1.9f, 0.03f),
        ("UI_Bubble_Alert", 0.85f, 0f),
        ("UI_Bubble_Hammer", 0.85f, 0f),
        ("FX_Dust", 0.9f, 0.5f),
        ("UI_Bubble_Speech", 1.75f, 0f), // 해달 말풍선 (꼬리 끝 피벗, 머리 위에 붙음)
    };

    private static void ImportArt()
    {
        foreach (var p in PropArt)
        {
            string path = $"{ArtFolder}/{p.name}.png";
            if (!File.Exists(path))
            {
                Debug.LogError($"[SettlementSetup] 그림이 없습니다: {path} (python Tools/UIGen/settlement_art.py)");
                continue;
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = texture.height / p.worldHeight;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, p.pivotY);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    // 아이콘은 UI용 (가운데 피벗)
    internal static Sprite LoadArt(string name) => ImportSprite(ArtFolder, name);

    internal static Sprite LoadPropArt(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{name}.png");

    #endregion
}
