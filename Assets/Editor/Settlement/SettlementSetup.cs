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
    private const string MineZonePath = "Assets/Scriptable Obejects/Navigation/Zone_Mine.asset";
    private const string FarmZonePath = "Assets/Scriptable Obejects/Navigation/Zone_Farm.asset";
    private const string PlazaPrefabFolder = "Assets/Prefabs/Plaza";

    // 왕국 단계 이름 (상단바 아래 칩)
    private static readonly string[] StageNames = { "빈터", "첫 정착", "작은 정착지", "자급 시작" };

    // 옛 세이브에 부탁과 별도로 열어 줄 발전 (아직 부탁이 없는 낚시터)
    internal const string FishingDevelopment = "fishing_dock";

    // (에셋, ID, 이름, 얼굴 아이콘(Art/Settlement 또는 Art/Otter), 광장 프리팹, 건설 해달, 처음 왔을 때 방명록)
    // 광부·농부: 광장 그림은 광산·밭에서 쓰는 그림 그대로 (광부 = SpecialistOtterSetup이 만드는 PlazaOtter_Miner, 농부 = 농부 모습의 PlazaOtter)
    private static readonly (string asset, string id, string name, string portrait, string prefab, bool builder, string arrival)[] Otters =
    {
        ("Otter_First", "otter_first", "몽실", "ICON_Otter_Snack", "PlazaOtter_Snack", false, "gb_first_arrival"),
        ("Otter_Painter", "otter_painter", "물감이", "ICON_Otter_Painter", "PlazaOtter_Painter", false, "gb_painter_arrival"),
        ("Otter_Sleepy", "otter_sleepy", "꾸벅이", "ICON_Otter_Sleepy", "PlazaOtter_Sleepy", false, "gb_sleepy_arrival"),
        ("Otter_Builder", "otter_builder", "뚝딱이", "ICON_Otter_Builder", null, true, "gb_builder_arrival"),
        ("Otter_Miner", "otter_miner", "깡깡이", "ICON_Otter_Miner", "PlazaOtter_Miner", false, "gb_miner_arrival"),
        ("Otter_Farmer", "otter_farmer", "새싹이", "ICON_Otter_Farmer", "PlazaOtter", false, "gb_farmer_arrival"),
    };

    // 전문 해달: (ID, 일할 지역 에셋, 배치하면 등록되는 도감 항목 에셋, 광장에서 배치 전에 하는 말)
    private const string CollectionEntryFolder = "Assets/Scriptable Obejects/Collection/Entries";
    private static readonly (string otter, string region, string entry, string assignLine)[] Specialists =
    {
        ("otter_miner", MineRegionPath, "Entry_OtterMiner", "광산 소식을 듣고 왔어요!\n광산에서 일하고 싶어요."),
        ("otter_farmer", FarmRegionPath, "Entry_OtterFarmer", "밭이 생겼다고 들었어요!\n밭에서 일하고 싶어요."),
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
        { "otter_miner", (new[] { "깡깡! 오늘도 반짝이는 돌을 찾아요.", "광산은 시원해서 좋아요.", "다이아몬드는 어디 숨었을까?" }, "", "") },
        { "otter_farmer", (new[] { "새싹이 쑥쑥 자라요!", "당근이 제일 좋아요.", "흙냄새가 좋아요~" }, "", "") },
    };

    // (ID, 남긴 해달, 표시, 말)
    private static readonly (string id, string otter, string tag, string message)[] Entries =
    {
        ("gb_first_arrival", "otter_first", "첫 방문", "바닷바람이 포근한 곳이네요.\n여기 머물러도 될까요?"),
        ("gb_first_settle", "otter_first", "정착", "작은 집이 생겼어요!\n오늘부터 여기가 우리 집이에요."),
        ("gb_chair", "otter_first", "발견", "의자에 앉아 쉬다 보니\n숲 너머에 동굴이 보여요!"),
        ("gb_mine_open", "otter_first", "개척", "광산 길이 열렸어요!\n이제 돌을 캘 수 있어요."),
        ("gb_painter_arrival", "otter_painter", "방문", "광장이 예뻐서 그림 그리러 왔어요.\n저도 여기 살고 싶어요!"),
        ("gb_sleepy_arrival", "otter_sleepy", "방문", "햇살이 따뜻해서…\n낮잠 자기 딱 좋네요."),
        ("gb_builder_arrival", "otter_builder", "도착", "집 짓는 건 저한테 맡겨요!\n뚝딱뚝딱!"),
        ("gb_neighbor_settle", "otter_painter", "정착", "새 집 정말 고마워요!\n창문에 그림을 걸어 둘게요."),
        ("gb_farmland", "otter_first", "개간", "농경지를 일궜어요!\n이제 밭에서 먹거리를 길러요."),
        ("gb_miner_arrival", "otter_miner", "방문", "광산이 열렸다는 소식을 듣고 왔어요.\n곡괭이는 제가 챙겨 왔어요!"),
        ("gb_miner_assigned", "otter_miner", "배치", "오늘부터 광산은 제게 맡겨요!\n깡깡!"),
        ("gb_farmer_arrival", "otter_farmer", "방문", "밭이 생겼다고 해서 달려왔어요.\n씨앗 냄새가 나요!"),
        ("gb_farmer_assigned", "otter_farmer", "배치", "밭은 제가 돌볼게요.\n첫 당근을 같이 거둬요!"),
    };

    // (ID, 이름, 아이콘, 종류, 골드, 목재, 돌, 초, 건설 해달, 진행 제목, 진행 안내, 결과 발전)
    private static readonly (string id, string name, string icon, ConstructionTarget target, int gold, int wood, int stone,
        float seconds, bool builder, string label, string hint, string result)[] Constructions =
    {
        ("con_house_1", "작은 집", "ICON_House_Blue", ConstructionTarget.House, 0, 8, 5, 10f, false,
            "작은 집 짓는 중", "다 지으면 몽실이가 정착해요", "house_1"),
        ("con_chair", "나무 그늘 의자", "ICON_Chair", ConstructionTarget.House, 50, 6, 0, 8f, false,
            "의자 만드는 중", "다 만들면 몽실이가 쉴 수 있어요", "chair"),
        ("con_mine_path", "광산 길 열기", "ICON_MinePath", ConstructionTarget.Clearing, 0, 0, 0, 0f, false,
            "", "", "mine_cleared"),
        ("con_house_2", "새 이웃의 집", "ICON_House_Red", ConstructionTarget.House, 150, 18, 8, 30f, true,
            "새 이웃의 집 짓는 중", "다 지으면 새 이웃이 정착해요", "house_2"),
        // 농경지: 밭에서 직접 치우고 주민이 개간(비용은 주민 작업 task_farm_till)하면 끝나는 부탁 (광산 길 열기와 같은 방식)
        ("con_farmland", "농경지 개간", "ICON_Clearing", ConstructionTarget.Clearing, 0, 0, 0, 0f, false,
            "", "", FarmOperationalDevelopment),
    };

    // 개척 기획(2026-10-02): 첫 집 → 의자(왕국 Lv.2, 동굴 발견) → 광산 길 열기(직접 치움 → 주민 정비, Lv.3, 광부 방문)
    // → 광부 배치(물감이·뚝딱이 방문) → 새 이웃의 집 → 농경지(밭에서 직접 치움 → 주민 개간, 농부 방문) → 농부 배치
    // P1: 광산 길 열기는 나무·돌을 다 치운 뒤 주민 해달이 "광산 주변 정리"를 끝내야 완료 (SettlementSetup.Tasks)
    // 배치 부탁: 건설 없이, 광장에서 그 전문 해달과 대화해 배치하면 완료
    // (ID, 순서, 제목, 설명, 아이콘, 부탁한 해달, 필요 발전, 필요 주민, 건설, 정착, 찾아옴(해달:상태), 단계, 완료 기록, 완료 문구, 왕국 레벨, 직접 치울 장소, 배치할 전문 해달)
    private static readonly (string id, int order, string title, string description, string icon, string requester,
        string requires, int residents, string construction, string[] settles, (string otter, ResidentState state)[] arrivals,
        int stage, string entry, string message, int level, string zone, string assign)[] Requests =
    {
        ("req_first_house", 0, "첫 번째 집 만들기", "몽실이가 머물 작은 집이 필요해요.\n목재와 돌로 지어 줘요.", "ICON_House_Blue",
            "otter_first", "", 0, "con_house_1", new[] { "otter_first" },
            new[] { ("otter_sleepy", ResidentState.Visitor) },
            1, "gb_first_settle", "첫 주민이 정착했어요!", 0, "", ""),
        ("req_chair", 1, "쉬어 갈 의자", "몽실이가 나무 그늘에서 쉴 의자를 갖고 싶대요.\n목재와 골드로 만들어 줘요.", "ICON_Chair",
            "otter_first", "house_1", 0, "con_chair", new string[0],
            new (string, ResidentState)[0],
            -1, "gb_chair", "의자 완성! 몽실이가 숲 너머에서\n동굴을 발견했어요.", 2, "", ""),
        ("req_mine_path", 2, "광산 길 열기", "몽실이가 찾은 동굴은 광산이었어요!\n길을 막은 나무와 돌을 치워 줘요.", "ICON_MinePath",
            "otter_first", "chair", 0, "con_mine_path", new string[0],
            new[] { ("otter_miner", ResidentState.SpecialNpc) },
            -1, "gb_mine_open", "광산 정비가 끝났어요!\n소식을 듣고 광부 해달이 광장에 찾아왔어요.", 3, MineZonePath, ""),
        ("req_assign_miner", 3, "광산에서 일할 친구", "광산 소식을 듣고 광부 해달 깡깡이가 찾아왔어요.\n광장에서 만나 광산에 배치해 주세요.", "ICON_Otter_Miner",
            "otter_miner", MineOperationalDevelopment, 0, "", new string[0],
            new[] { ("otter_painter", ResidentState.SettlementCandidate), ("otter_builder", ResidentState.SpecialNpc) },
            -1, "gb_miner_assigned", "깡깡이가 광산에서 일하기 시작해요!\n소식을 듣고 새 해달들이 찾아왔어요.", 0, "", "otter_miner"),
        ("req_neighbor_house", 4, "새 이웃의 집", "물감이가 이웃이 되고 싶대요.\n건설 해달과 집을 지어 줘요.", "ICON_House_Red",
            "otter_painter", PainterIntroDevelopment, 0, "con_house_2", new[] { "otter_painter", "otter_builder" },
            new (string, ResidentState)[0],
            2, "gb_neighbor_settle", "새 이웃이 정착했어요!\n주민이 늘었어요.", 0, "", ""),
        ("req_farmland", 5, "먹거리를 길러요", "주민이 늘었어요.\n밭에 가서 잡목과 바위를 치우고\n주민과 함께 농경지를 개간해요.", "ICON_Clearing",
            "otter_first", FarmDiscoverDevelopment, 3, "con_farmland", new string[0],
            new[] { ("otter_farmer", ResidentState.SpecialNpc) },
            3, "gb_farmland", "농경지 개간이 끝났어요!\n소식을 듣고 농부 해달이 광장에 찾아왔어요.", 0, FarmZonePath, ""),
        ("req_assign_farmer", 6, "농사를 지을 해달이 필요해요", "광장으로 돌아가 새로 찾아온 농부 해달 새싹이를 만나 보세요.\n밭에 배치하면 농사를 시작해요.", "ICON_Otter_Farmer",
            "otter_farmer", FarmOperationalDevelopment, 0, "", new string[0],
            new (string, ResidentState)[0],
            -1, "gb_farmer_assigned", "새싹이가 밭에서 일하기 시작해요!\n당근을 심어 첫 수확을 해 봐요.", 0, "", "otter_farmer"),
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
            so.FindProperty("_portrait").objectReferenceValue = LoadPortrait(o.portrait);
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
            so.FindProperty("_icon").objectReferenceValue = LoadPortrait(r.icon);
            so.FindProperty("_requester").objectReferenceValue = otters[r.requester];
            so.FindProperty("_requiredDevelopment").stringValue = r.requires;
            so.FindProperty("_minResidents").intValue = r.residents;
            so.FindProperty("_construction").objectReferenceValue =
                string.IsNullOrEmpty(r.construction) ? null : constructions[r.construction];
            so.FindProperty("_assignSpecialist").objectReferenceValue = string.IsNullOrEmpty(r.assign) ? null : otters[r.assign];

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
            so.FindProperty("_kingdomLevel").intValue = r.level;
            so.FindProperty("_clearZone").objectReferenceValue =
                string.IsNullOrEmpty(r.zone) ? null : AssetDatabase.LoadAssetAtPath<ZoneDefinition>(r.zone);
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
        CreateRegionData(configSo);
        configSo.ApplyModifiedPropertiesWithoutUndo();
        LinkSpecialists(otters);

        AssetDatabase.SaveAssets();
    }

    // 전문 해달 ↔ 일할 지역·도감 항목 (지역은 CreateRegionData가 만든 뒤에)
    private static void LinkSpecialists(Dictionary<string, SettlementOtterDefinition> otters)
    {
        foreach (var s in Specialists)
        {
            var so = new SerializedObject(otters[s.otter]);
            so.FindProperty("_workRegion").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DevelopableRegionDefinition>(s.region);
            var entry = AssetDatabase.LoadAssetAtPath<CollectionEntry>($"{CollectionEntryFolder}/{s.entry}.asset");
            if (entry == null)
                Debug.LogWarning($"[SettlementSetup] 도감 항목이 없습니다: {s.entry} (CollectionSetup.CreateData를 먼저 실행하세요)");
            so.FindProperty("_collectionEntry").objectReferenceValue = entry;
            so.FindProperty("_assignLine").stringValue = s.assignLine;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // 얼굴·부탁 그림: 정착 그림 폴더에 없으면 해달 그림 폴더 (광부·농부 아이콘)
    private static Sprite LoadPortrait(string name) =>
        File.Exists($"{ArtFolder}/{name}.png") ? LoadArt(name) : ImportSprite(OtterFolder, name);

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
        // 분위기 연출 (plaza_nodes_art.py): 발밑 그림자, 구름 그림자, 나비
        ("FX_Shadow", 0.5f, 0.5f),
        ("FX_CloudShadow", 3f, 0.5f),
        ("FX_Butterfly_0", 0.45f, 0.5f),
        ("FX_Butterfly_1", 0.45f, 0.5f),
        ("FX_Firefly", 0.35f, 0.5f),
        // 바위·나무가 다시 생기기까지 작은 시계 (plaza_nodes_art.py)
        ("FX_Regrow_0", 0.42f, 0.5f),
        ("FX_Regrow_1", 0.42f, 0.5f),
        ("FX_Regrow_2", 0.42f, 0.5f),
        ("FX_Regrow_3", 0.42f, 0.5f),
        ("FX_Regrow_4", 0.42f, 0.5f),
        ("FX_Regrow_5", 0.42f, 0.5f),
        ("FX_Regrow_6", 0.42f, 0.5f),
        ("FX_Regrow_7", 0.42f, 0.5f),
        ("Prop_Foundation", 3.4f, 0.16f),
        ("Prop_FarmSign_Locked", 1.9f, 0.03f),
        ("Prop_FarmSign", 1.9f, 0.03f),
        ("UI_Bubble_Alert", 0.85f, 0f),
        ("UI_Bubble_Hammer", 0.85f, 0f),
        ("FX_Dust", 0.9f, 0.5f),
        ("FX_Twinkle", 0.5f, 0.5f), // 집 완성: 집 위 별빛 (celebrate_fx.py)
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
