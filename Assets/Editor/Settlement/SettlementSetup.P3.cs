using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// P3 공동사업 · 생활 의뢰 · 요정 방문 순서 (기획: 해달왕국_P3_기획서.md, 작업기록: Docs/P3_공동사업_작업기록.md).
/// - 데이터: 공동사업 5개(첫 비축 → 새 이웃 → 환영 소품 → 첫 모임 → 반복 "마을 생활 준비"), 사업 건설 부탁·건설(비용 0),
///   길드 주변 정리 작업, 새 이웃 해달, 방명록, 생활 의뢰 3종, 요정 방문 발전 (농부 파견 → 예약)
///   광장 확장은 영토 확장(SettlementSetup.Territory: 서쪽·북쪽 숲 개간)으로 바뀜 — 새 이웃 맞이하기는 처음 땅을 넓힌 뒤
/// - 그림: Tools/UIGen/p3_art.py가 만든 임시 소품(비축 상자·공동 식탁·화분·빨랫줄)
/// - 광장: 회관 앞 비축 상자·식탁·모임 연출, 이웃집 묶음(집·화분·빨랫줄·공사 현장 — 처음 넓힌 영토 쪽으로 옮겨짐, TerritoryAnchor),
///   회관 앞 장식(반복 사업 3회), 길드 주변 정리 자리, 요정은 도착 발전으로, 끝에 영토(지도·숲·걷기 칸·개간 자리·카메라 범위)
/// 한 번에 적용: Tools/Settlement/Apply P3 Projects (여러 번 실행해도 결과가 같음). Setup Plaza · Apply P2를 다시 돌려도 P3 배치가 함께 다시 만들어짐
/// </summary>
public static partial class SettlementSetup
{
    #region ID · 표

    internal const string FairyInvitedDevelopment = "fairy_invited";
    internal const string FairyArrivedDevelopment = "fairy_arrived";
    private const string PlazaExpandDevelopment = "plaza_expand_01";
    private const string ObstaclesClearedDevelopment = "p3_plaza_obstacles_cleared";
    private const string SupplyBuiltDevelopment = "p3_supply_box_built";
    private const string HouseBuiltDevelopment = "p3_house_built";
    private const string FlowerPotDevelopment = "p3_flowerpot_built";
    private const string ClotheslineDevelopment = "p3_clothesline_built";
    private const string TableBuiltDevelopment = "p3_table_built";
    private const string HallDecorDevelopment = "p3_hall_decor_1";
    private const string NeighborOtterId = "otter_p3_neighbor";
    private const string HouseSlotId = "slot_plaza_expand_01_house";
    private const string ExpandTaskId = "task_p3_plaza_expand";
    private const string HallTidyTaskId = "task_life_hall_tidy";
    private const int P3ContentVersion = 3;
    private const string ProjectFolder = DataFolder + "/Projects";
    private const string LifeFolder = DataFolder + "/LifeRequests";
    private const string CarrotItemPath = "Assets/Scriptable Obejects/Inventory/Items/Farming/당근.asset";
    private const string CucumberItemPath = "Assets/Scriptable Obejects/Inventory/Items/Farming/오이.asset";
    private const string BushArtPath = "Assets/Art/Plaza/Props/Prop_Bush.png";
    private const float P3PropPixelsPerUnit = 90f;

    private static readonly Vector2 SupplyPixel = new Vector2(905, 795);
    private static readonly Vector2 TablePixel = new Vector2(720, 808);
    private static readonly Vector2[] HallDecorPixels = { new Vector2(765, 730), new Vector2(885, 730) };
    // 이웃집 묶음: territory_layout.json의 homes (서쪽 / 북쪽 첫 땅). 화분·빨랫줄은 집 기준 (예전 동쪽 배치와 같은 간격)
    private static readonly Vector2 HouseFallbackPixel = new Vector2(-520, 560);
    private static readonly Vector2 FlowerPotOffset = new Vector2(-100, 28);
    private static readonly Vector2 ClotheslineOffset = new Vector2(115, 45);
    private static readonly Vector2[] TidyStandPixels = { new Vector2(795, 748), new Vector2(855, 752) };
    private static readonly Vector2 TidyLookPixel = new Vector2(825, 700);

    #endregion

    [MenuItem("Tools/Settlement/Apply P3 Projects")]
    public static void ApplyP3Projects()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ImportP3Art();
        ImportTerritoryArt(LoadTerritoryLayout());
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        if (config == null)
        {
            Debug.LogError("[SettlementSetup] 정착 설정이 없습니다. Tools/Settlement/Create Data를 먼저 실행하세요.");
            return;
        }
        var configSo = new SerializedObject(config);
        var otters = new Dictionary<string, SettlementOtterDefinition>();
        foreach (var otter in config.Otters)
        {
            if (otter != null)
                otters[otter.OtterId] = otter;
        }
        CreateP3Data(configSo, otters);
        // 특성 · 장난감 해달 (P4) — 새 이웃(포근이)이 생긴 뒤에
        CreateTraitData(configSo, otters);
        configSo.ApplyModifiedPropertiesWithoutUndo();
        // 자리를 골라 짓는 건물 (P4)
        CreateBuildingData();
        // Lv.10~15 해달의 부탁 (P4 — 건물·장난감 해달이 생긴 뒤에)
        var storySo = new SerializedObject(config);
        CreateP4Story(storySo, otters);
        storySo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        PlaceP3Plaza();
        Debug.Log("[SettlementSetup] P3 적용 완료: 공동사업·생활 의뢰·영토 확장 데이터, 요정 방문 순서, 광장 P3·영토 배치");
    }

    #region 그림

    // 소품: 아래 가운데 피벗, 기존 소품과 비슷한 크기(PPU 90)
    private static void ImportP3Art()
    {
        foreach (var name in new[] { "Prop_SupplyBox", "Prop_CommunityTable", "Prop_FlowerPot", "Prop_Clothesline" })
            ImportP3Sprite($"{ArtFolder}/{name}.png", P3PropPixelsPerUnit, new Vector2(0.5f, 0.08f));
    }

    private static Sprite ImportP3Sprite(string path, float pixelsPerUnit, Vector2 pivot)
    {
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"[SettlementSetup] 그림이 없습니다: {path} (python Tools/UIGen/p3_art.py)");
            return null;
        }
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    #endregion

    #region 데이터

    // CreateData 끝에서도 부름 (P0~P2 표를 다시 만들어도 P3가 빠지지 않게)
    private static void CreateP3Data(SerializedObject configSo, Dictionary<string, SettlementOtterDefinition> otters)
    {
        EnsureFolder(ProjectFolder);
        EnsureFolder(LifeFolder);
        EnsureFolder($"{DataFolder}/Tasks");

        var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodItemPath);
        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var carrot = AssetDatabase.LoadAssetAtPath<ItemDefinition>(CarrotItemPath);
        var cucumber = AssetDatabase.LoadAssetAtPath<ItemDefinition>(CucumberItemPath);
        var apple = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AppleItemPath);
        var gold = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);
        if (wood == null || stone == null || carrot == null)
            Debug.LogError("[SettlementSetup] 목재·돌·당근 아이템을 찾지 못했습니다.");

        // 요정: 농부를 밭에 파견하면 방문 예약
        if (otters.TryGetValue("otter_farmer", out var farmer))
            SetString(farmer, "_assignDevelopment", FairyInvitedDevelopment);

        // 방명록
        var entries = new Dictionary<string, GuestbookEntryDefinition>();
        (string id, string otter, string tag, string message)[] entryTable =
        {
            ("gb_p3_supply", "otter_receptionist", "첫 비축", "광장에 우리 마을의 첫 비축 상자가 생겼어요."),
            ("gb_p3_plaza", "otter_first", "광장 확장", "숲을 걷어 내니 광장이 훨씬 넓어졌어요!"),
            ("gb_p3_neighbor_arrival", NeighborOtterId, "첫 방문", "새 집이 생겼다는 소식을 듣고 찾아왔어요."),
            ("gb_p3_neighbor", NeighborOtterId, "입주", "짐을 풀고 나니 이제 정말 우리 집 같아요."),
            ("gb_p3_welcome", NeighborOtterId, "환영", "문 앞이 환해졌어요. 반겨 줘서 고마워요!"),
            ("gb_p3_gathering", "otter_receptionist", "첫 모임", "광장 식탁에 모두 모여 첫 모임을 열었어요."),
        };
        foreach (var e in entryTable)
            entries[e.id] = LoadOrCreate<GuestbookEntryDefinition>($"{DataFolder}/Guestbook/{e.id}.asset").asset;

        // 새 이웃 (기존 일반 해달 모습 재사용: 농부와 같은 광장 프리팹 — 농부는 밭에 가 있어 광장에서 겹치지 않음)
        var neighbor = LoadOrCreate<SettlementOtterDefinition>($"{DataFolder}/Otters/Otter_Neighbor.asset").asset;
        var neighborSo = new SerializedObject(neighbor);
        neighborSo.FindProperty("_otterId").stringValue = NeighborOtterId;
        neighborSo.FindProperty("_displayName").stringValue = "포근이";
        neighborSo.FindProperty("_portrait").objectReferenceValue = farmer != null ? farmer.Portrait : null;
        neighborSo.FindProperty("_plazaPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlazaPrefabFolder}/PlazaOtter.prefab");
        neighborSo.FindProperty("_isBuilder").boolValue = false;
        neighborSo.FindProperty("_arrivalEntry").objectReferenceValue = entries["gb_p3_neighbor_arrival"];
        neighborSo.FindProperty("_moveInLine").stringValue = "새 집이 생겼다고 들었어요!\n저 집에서 살아도 될까요?";
        SetStrings(neighborSo.FindProperty("_lines"), "여기 정말 살기 좋아요!", "이웃이 많아서 든든해요.", "오늘은 뭘 도와줄까요?");
        OtterCastSetup.ApplyTo(neighborSo, NeighborOtterId); // 따로 만든 모습이 있으면 농부 모습 대신
        neighborSo.ApplyModifiedPropertiesWithoutUndo();
        otters[NeighborOtterId] = neighbor;

        foreach (var e in entryTable)
        {
            var so = new SerializedObject(entries[e.id]);
            so.FindProperty("_entryId").stringValue = e.id;
            so.FindProperty("_otter").objectReferenceValue = otters.TryGetValue(e.otter, out var o) ? o : null;
            so.FindProperty("_tag").stringValue = e.tag;
            so.FindProperty("_message").stringValue = e.message;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 작업: 광장 확장 정비(예전 동쪽 확장 — 정비 중이던 세이브가 이어서 끝낼 수 있게만 남김) · 길드 주변 정리(생활 의뢰 틀)
        var expandTask = CreateP3Task(ExpandTaskId, "광장 확장 정비", "덤불을 걷어 낸 땅을 고르고\n집 지을 자리까지 길을 내요.",
            "광장이 넓어졌어요!", 1, 60f, ObstaclesClearedDevelopment, PlazaExpandDevelopment);
        var tidyTask = CreateP3Task(HallTidyTaskId, "길드 주변 정리", "길드 앞을 쓸고 물건을 가지런히 놓아요.",
            "길드 앞이 깔끔해졌어요!", 1, 20f, "", "");

        // 사업 단계의 건설 (비용 0 — 재료는 사업 납품으로 냄)
        var conSupply = CreateP3Construction("con_p3_supply_box", "비축 상자", "Prop_SupplyBox", ConstructionTarget.House, 30f,
            "비축 상자 놓는 중", "다 놓으면 첫 비축 완료!", SupplyBuiltDevelopment);
        var conHouse = CreateP3Construction("con_p3_house", "새 이웃의 집", "ICON_House_Blue", ConstructionTarget.House, 90f,
            "이웃집 짓는 중", "완성되면 새 이웃이 찾아와요", HouseBuiltDevelopment);
        var conPot = CreateP3Construction("con_p3_flowerpot", "환영 화분", "Prop_FlowerPot", ConstructionTarget.House, 30f,
            "화분 놓는 중", "이웃집 앞이 환해져요", FlowerPotDevelopment);
        var conLine = CreateP3Construction("con_p3_clothesline", "환영 빨랫줄", "Prop_Clothesline", ConstructionTarget.House, 30f,
            "빨랫줄 거는 중", "이웃집 앞에 생활 소품이 생겨요", ClotheslineDevelopment);
        var conTable = CreateP3Construction("con_p3_table", "공동 식탁", "Prop_CommunityTable", ConstructionTarget.House, 60f,
            "공동 식탁 준비 중", "식탁이 생기면 모임을 열 수 있어요", TableBuiltDevelopment);

        otters.TryGetValue("otter_receptionist", out var clerk);
        otters.TryGetValue("otter_first", out var first);
        otters.TryGetValue("otter_builder", out var builder);

        var reqSupply = CreateP3Request("req_p3_supply_box", "우리 마을의 첫 비축", conSupply, "p3_supply_01_paid", clerk, null);
        var reqHouse = CreateP3Request("req_p3_house", "새 이웃 맞이하기", conHouse, "p3_neighbor_01_paid", builder, neighbor);
        var reqPot = CreateP3Request("req_p3_flowerpot", "환영 화분", conPot, "p3_welcome_01_paid", neighbor, null);
        var reqLine = CreateP3Request("req_p3_clothesline", "환영 빨랫줄", conLine, "p3_welcome_01_paid", neighbor, null);
        var reqTable = CreateP3Request("req_p3_table", "우리 마을의 첫 모임", conTable, "p3_gathering_01_paid", clerk, null);

        // 공동사업 (경험치 = 기획서 11장: 구간 시작 레벨의 필요 경험치 E7 800 · E8 1,050 · E9 1,350 × 비율)
        var supply = CreateProject("p3_supply_01", 1, "우리 마을의 첫 비축",
            "길드는 생겼는데 함께 쓸 재료가 없네. 다음 공사를 위해 조금씩 모아 둘까?", clerk, LoadArt("ICON_TownHall"),
            "광장에 비축 상자가 생겨요", "우리 마을의 첫 비축을 마쳤어요!\n광장에 비축 상자가 생겼어요.",
            7, TownHallDevelopment, 300, new[] { (wood, 12), (stone, 6) },
            new[] { new ProjectStageDefinition("build_box", ProjectActionKind.CompleteConstruction, "상자 설치")
                .With(construction: reqSupply, hint: "고른 자리에 건설 해달이 비축 상자를 놓아요.") },
            "supply_ready", 560, entries["gb_p3_supply"]);
        // 영토 확장 (광장 첫 확장 대신): 서쪽·북쪽 숲 개간 → 마을회관 영토 확장 미션. 처음 넓힌 땅에 새 이웃의 집 자리
        CreateTerritoryData(configSo, first, entries["gb_p3_plaza"]);
        var neighborProject = CreateProject("p3_neighbor_01", 3, "새 이웃 맞이하기",
            "넓어진 곳에 집을 지으면 새 친구가 찾아올 거예요.", builder, LoadArt("ICON_House_Blue"),
            "집 한 채와 새 주민이 생겨요 (일손 +1)", "포근이가 새 집에 자리 잡았어요!\n함께 일할 주민이 늘었어요.",
            9, TerritoryRules.FirstDevelopment, 800, new[] { (wood, 24), (stone, 16) },
            new[]
            {
                new ProjectStageDefinition("build_house", ProjectActionKind.CompleteConstruction, "이웃집 짓기")
                    .With(construction: reqHouse, hint: "건설 해달이 넓어진 땅에 집을 지어요."),
                new ProjectStageDefinition("move_in", ProjectActionKind.SettleResident, "입주")
                    .With(resident: neighbor, houseSlotId: HouseSlotId, hint: "새 집을 보고 찾아온 해달을 광장에서 만나요."),
            },
            "p3_neighbor_settled", 610, entries["gb_p3_neighbor"]);
        var welcome = CreateProject("p3_welcome_01", 4, "환영 공간 꾸미기",
            "짐을 풀고 나니 이제 정말 우리 집 같아. 문 앞에 작은 화분 하나 두면 좋겠어.", neighbor, LoadArt("Prop_FlowerPot"),
            "이웃집 앞에 생활 소품이 생겨요", "이웃집 앞이 환해졌어요!",
            9, "p3_neighbor_settled", 150, new[] { (wood, 6) },
            new[]
            {
                new ProjectStageDefinition("welcome_prop", ProjectActionKind.PlaceWelcomeProp, "소품 설치")
                    .With(options: new[] { reqPot, reqLine }, hint: "화분과 빨랫줄 중 하나를 골라요."),
            },
            "welcome_corner_ready", 340, entries["gb_p3_welcome"]);
        var gathering = CreateProject("p3_gathering_01", 5, "우리 마을의 첫 모임",
            "마을이 이만큼 컸으니, 다 같이 모여 밥 한 끼 할까요?", clerk, LoadArt("Prop_CommunityTable"),
            "광장에 공동 식탁이 생기고 첫 모임을 열어요", "우리 마을의 첫 모임을 열었어요!\n길드에서 마을 생활 준비를 이어 갈 수 있어요.",
            10, "welcome_corner_ready", 300, new[] { (wood, 8), (carrot, 12) },
            new[]
            {
                new ProjectStageDefinition("build_table", ProjectActionKind.CompleteConstruction, "식탁 준비")
                    .With(construction: reqTable, hint: "고른 자리에 건설 해달이 공동 식탁을 놓아요."),
                new ProjectStageDefinition("hold_gathering", ProjectActionKind.HoldGathering, "모임 열기")
                    .With(hint: "광장에서 [모임 열기]를 누르면 주민들이 식탁에 모여요."),
            },
            "first_gathering_complete", 425, entries["gb_p3_gathering"]);
        var repeat = CreateProject("p3_village_life", 6, "마을 생활 준비",
            "길드 살림을 조금씩 채워 두면 마을이 더 든든해져요.", clerk, LoadArt("ICON_TownHall"),
            "3번 마치면 길드 앞이 꽃으로 꾸며져요", "마을 생활 준비를 마쳤어요!",
            10, "first_gathering_complete", 0, System.Array.Empty<(ItemDefinition, int)>(),
            System.Array.Empty<ProjectStageDefinition>(), "", 0, null);
        repeat.SetupRepeat(new[] { "식탁 준비", "휴식 공간 정돈", "길드 물품 보충" },
            new[]
            {
                new ProjectCycleCost(200, new[] { new ItemAmount(carrot, 10), new ItemAmount(wood, 4) }),
                new ProjectCycleCost(150, new[] { new ItemAmount(wood, 10), new ItemAmount(stone, 6) }),
                new ProjectCycleCost(250, new[] { new ItemAmount(stone, 8), new ItemAmount(apple != null ? apple : wood, 4) }),
            },
            0.25f, 2f, 5f, 3, HallDecorDevelopment);
        EditorUtility.SetDirty(repeat);

        // 생활 의뢰 (경험치 = 받는 순간 레벨의 8%: E7 64 · E8 84 · E9 108)
        var snack = LoadOrCreate<LifeRequestTemplate>($"{LifeFolder}/Life_snack.asset").asset;
        snack.Setup("life_snack", LifeRequestKind.Deliver, "오늘의 간식", "출출한데 {item} {amount}개만 나눠 줄래요?",
            new[] { carrot, cucumber }.Where(i => i != null), 8, null, 60, 8f, LoadArt("ICON_Otter_Snack"), GameManager.FarmZoneId);
        var repair = LoadOrCreate<LifeRequestTemplate>($"{LifeFolder}/Life_repair.asset").asset;
        repair.Setup("life_repair", LifeRequestKind.Deliver, "집수리 재료", "지붕이 삐걱거려요. {item} {amount}개만 있으면 고칠 수 있어요.",
            new[] { wood, stone }.Where(i => i != null), 6, null, 80, 8f, LoadArt("ICON_Item_Wood"));
        var tidy = LoadOrCreate<LifeRequestTemplate>($"{LifeFolder}/Life_hall_tidy.asset").asset;
        tidy.Setup("life_hall_tidy", LifeRequestKind.ResidentWork, "길드 주변 정리", "길드 앞이 어수선해요. 주민 한 명이 잠깐 정리해 줄래요?",
            null, 1, tidyTask, 40, 8f, LoadArt("ICON_TidyBoard"));
        foreach (var asset in new Object[] { snack, repair, tidy })
            EditorUtility.SetDirty(asset);

        // 설정
        AddUnique(configSo.FindProperty("_otters"), neighbor);
        foreach (var entry in entries.Values)
            AddUnique(configSo.FindProperty("_guestbookEntries"), entry);
        AddUnique(configSo.FindProperty("_tasks"), expandTask);
        AddUnique(configSo.FindProperty("_tasks"), tidyTask);
        SetList(configSo.FindProperty("_projects"), new List<CommunityProjectDefinition> { supply, neighborProject, welcome, gathering, repeat });
        SetList(configSo.FindProperty("_projectRequests"), new List<BoardRequestDefinition> { reqSupply, reqHouse, reqPot, reqLine, reqTable });
        SetList(configSo.FindProperty("_lifeRequests"), new List<LifeRequestTemplate> { snack, tidy, repair });
        configSo.FindProperty("_lifeRequestDevelopment").stringValue = TownHallDevelopment;
        configSo.FindProperty("_lifeRequestLevel").intValue = 7;
        configSo.FindProperty("_fairyInvitedDevelopment").stringValue = FairyInvitedDevelopment;
        configSo.FindProperty("_fairyArrivedDevelopment").stringValue = FairyArrivedDevelopment;
        _ = gold;
    }

    private static SettlementTaskDefinition CreateP3Task(string id, string title, string description, string done, int workers,
        float seconds, string requires, string result)
    {
        var task = LoadOrCreate<SettlementTaskDefinition>($"{DataFolder}/Tasks/{id}.asset").asset;
        var so = new SerializedObject(task);
        so.FindProperty("_taskId").stringValue = id;
        so.FindProperty("_title").stringValue = title;
        so.FindProperty("_description").stringValue = description;
        so.FindProperty("_completionMessage").stringValue = done;
        so.FindProperty("_requiredWorkers").intValue = workers;
        so.FindProperty("_durationSeconds").floatValue = seconds;
        so.FindProperty("_requiredGold").intValue = 0;
        so.FindProperty("_requiredItems").arraySize = 0;
        so.FindProperty("_requiredDevelopment").stringValue = requires;
        so.FindProperty("_resultDevelopment").stringValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();
        return task;
    }

    private static ConstructionDefinition CreateP3Construction(string id, string name, string icon, ConstructionTarget target,
        float seconds, string label, string hint, string result)
    {
        var construction = LoadOrCreate<ConstructionDefinition>($"{DataFolder}/Constructions/{id}.asset").asset;
        var so = new SerializedObject(construction);
        so.FindProperty("_constructionId").stringValue = id;
        so.FindProperty("_displayName").stringValue = name;
        so.FindProperty("_icon").objectReferenceValue = LoadPropArt(icon);
        so.FindProperty("_targetType").enumValueIndex = (int)target;
        so.FindProperty("_requiredGold").intValue = 0;
        so.FindProperty("_requiredItems").arraySize = 0;
        so.FindProperty("_durationSeconds").floatValue = seconds;
        so.FindProperty("_needsBuilder").boolValue = true;
        so.FindProperty("_progressLabel").stringValue = label;
        so.FindProperty("_progressHint").stringValue = hint;
        so.FindProperty("_unlockResultId").stringValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();
        return construction;
    }

    // 게시판에 보이지 않는 사업 건설 부탁 (조건 = 사업의 납품 완료 발전). arrival이 있으면 완성될 때 그 해달이 방문으로 찾아옴
    private static BoardRequestDefinition CreateP3Request(string id, string title, ConstructionDefinition construction, string requires,
        SettlementOtterDefinition requester, SettlementOtterDefinition arrival)
    {
        var request = LoadOrCreate<BoardRequestDefinition>($"{DataFolder}/Requests/{id}.asset").asset;
        var so = new SerializedObject(request);
        so.FindProperty("_requestId").stringValue = id;
        so.FindProperty("_order").intValue = 100;
        so.FindProperty("_category").enumValueIndex = (int)RequestCategory.Main;
        so.FindProperty("_contentVersion").intValue = P3ContentVersion;
        so.FindProperty("_title").stringValue = title;
        so.FindProperty("_description").stringValue = "";
        so.FindProperty("_icon").objectReferenceValue = construction.Icon;
        so.FindProperty("_requester").objectReferenceValue = requester;
        so.FindProperty("_requiredDevelopment").stringValue = requires;
        so.FindProperty("_minResidents").intValue = 0;
        so.FindProperty("_construction").objectReferenceValue = construction;
        so.FindProperty("_assignSpecialist").objectReferenceValue = null;
        so.FindProperty("_assignRole").objectReferenceValue = null;
        so.FindProperty("_completionTask").objectReferenceValue = null;
        so.FindProperty("_clearZone").objectReferenceValue = null;
        so.FindProperty("_settles").arraySize = 0;
        var arrivals = so.FindProperty("_arrivals");
        arrivals.arraySize = arrival != null ? 1 : 0;
        if (arrival != null)
        {
            var element = arrivals.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("_otter").objectReferenceValue = arrival;
            element.FindPropertyRelative("_state").enumValueIndex = (int)ResidentState.Visitor;
        }
        so.FindProperty("_stageOnComplete").intValue = -1;
        so.FindProperty("_completionEntry").objectReferenceValue = null;
        so.FindProperty("_completionMessage").stringValue = construction.ProgressHint;
        so.FindProperty("_kingdomLevel").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        return request;
    }

    private static CommunityProjectDefinition CreateProject(string id, int order, string title, string line,
        SettlementOtterDefinition requester, Sprite icon, string preview, string done, int level, string requires, int gold,
        (ItemDefinition item, int amount)[] items, ProjectStageDefinition[] stages, string result, int xp, GuestbookEntryDefinition entry)
    {
        var project = LoadOrCreate<CommunityProjectDefinition>($"{ProjectFolder}/Project_{id}.asset").asset;
        project.Setup(id, order, title, level, requires, gold,
            items.Where(i => i.item != null).Select(i => new ItemAmount(i.item, i.amount)), stages, result, xp);
        project.SetupText(line, preview, done, requester, icon, entry);
        EditorUtility.SetDirty(project);
        return project;
    }

    private static void SetString(Object target, string property, string value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStrings(SerializedProperty list, params string[] values)
    {
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    private static void AddUnique(SerializedProperty list, Object value)
    {
        if (value == null)
            return;
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                return;
        }
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
    }

    #endregion

    #region 광장

    private const string P3RootName = "P3";

    [MenuItem("Tools/Settlement/Setup P3 Plaza")]
    public static void PlaceP3Plaza()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        var plazaRoot = GameObject.Find("PlazaRoot").transform;
        var root = plazaRoot.Find(RootName);
        if (root == null)
        {
            Debug.LogError("[SettlementSetup] 광장에 Settlement가 없습니다. Tools/Settlement/Setup Plaza를 먼저 실행하세요.");
            return;
        }
        var background = plazaRoot.Find("Background").GetComponent<SpriteRenderer>();
        var bounds = GroundBounds(background);
        Vector3 ToWorld(Vector2 px) => new Vector3(bounds.min.x + px.x / GroundPixelsPerUnit, bounds.max.y - px.y / GroundPixelsPerUnit, 0f);
        BuildP3(root, plazaRoot.Find("Props"), root.GetComponent<SettlementPlazaView>(), background, ToWorld);
        ApplyBoardExpansion(plazaRoot);
        ApplyPlotAnchors(plazaRoot);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SettlementSetup] 광장 P3 배치 완료");
    }

    // Settlement 아래 P3를 다시 만들고 SettlementPlazaView·카메라 범위를 맞춤 (BuildP2도 끝에서 부름)
    private static void BuildP3(Transform root, Transform props, SettlementPlazaView view, SpriteRenderer background,
        System.Func<Vector2, Vector3> toWorld)
    {
        var old = root.Find(P3RootName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var p3 = new GameObject(P3RootName).transform;
        p3.SetParent(root, false);
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        var camera = Object.FindAnyObjectByType<PlazaCameraController>();
        var sites = new List<ConstructionSiteView>();
        var taskSites = new List<PlazaTaskSiteView>();
        var focus = new List<Transform>();

        // 1) 요정: 농부 파견 뒤 광장에 실제로 나타난 발전으로 보임 (나타날 때 카메라가 비춤)
        var fairy = Object.FindAnyObjectByType<FairyNpcView>(FindObjectsInactive.Include);
        if (fairy != null)
            AddGate(fairy.gameObject, FairyArrivedDevelopment, true, true);

        // 2) 회관 앞 비축 상자
        Vector3 supply = toWorld(SupplyPixel);
        var supplyProp = P3Prop(p3, "SupplyBox", "Prop_SupplyBox", supply, 1f, new Rect(-1.1f, -0.1f, 2.2f, 0.9f));
        AddGate(supplyProp.gameObject, SupplyBuiltDevelopment, true, true);
        var (supplySite, supplySiteSo) = CreateSite(p3, "Site_SupplyBox", "con_p3_supply_box", supply,
            supply + new Vector3(-1.9f, -0.3f, 0f), supply + new Vector3(0f, 2.6f, 0f), 2.2f);
        supplySiteSo.ApplyModifiedPropertiesWithoutUndo();
        sites.Add(supplySite);

        // 3) 공동 식탁 + 모임 연출
        Vector3 table = toWorld(TablePixel);
        var tableProp = P3Prop(p3, "CommunityTable", "Prop_CommunityTable", table, 1f, new Rect(-2.2f, -0.1f, 4.4f, 1.6f));
        AddGate(tableProp.gameObject, TableBuiltDevelopment, true, true);
        var (tableSite, tableSiteSo) = CreateSite(p3, "Site_CommunityTable", "con_p3_table", table,
            table + new Vector3(-3f, -0.4f, 0f), table + new Vector3(0f, 3.4f, 0f), 4f);
        tableSiteSo.ApplyModifiedPropertiesWithoutUndo();
        sites.Add(tableSite);
        BuildGathering(p3, table, camera);

        // 4) 회관 앞 장식 (반복 사업 3회): 화분 두 개
        var decor = new GameObject("HallDecor").transform;
        decor.SetParent(p3, false);
        foreach (var pixel in HallDecorPixels)
            P3Prop(decor, "FlowerPot", "Prop_FlowerPot", toWorld(pixel), 0.85f, new Rect(-0.5f, -0.1f, 1f, 0.4f));
        AddGate(decor.gameObject, HallDecorDevelopment, true, true);

        // 5) 길드 주변 정리 (생활 의뢰): 표지판 없이 일할 자리만
        var tidy = BuildPlazaTaskSite(p3, "TaskSite_HallTidy", HallTidyTaskId, config, toWorld(TidyLookPixel) + new Vector3(0f, 2.4f, 0f),
            TidyStandPixels.Select(toWorld).ToArray(), toWorld(TidyLookPixel), "hint_hall_tidy");
        var tidySo = new SerializedObject(tidy);
        tidySo.FindProperty("_repeatedWork").boolValue = true;
        tidySo.FindProperty("_hint").objectReferenceValue = null;
        tidySo.ApplyModifiedPropertiesWithoutUndo();
        var tidyHint = tidy.transform.Find("TapHint");
        if (tidyHint != null)
            Object.DestroyImmediate(tidyHint.gameObject);
        taskSites.Add(tidy);

        // 6) 이웃집 묶음 (집 · 환영 소품 · 공사 현장): 처음 넓힌 영토 쪽으로 옮겨짐 (서쪽 / 북쪽 첫 땅)
        var layout = LoadTerritoryLayout();
        Vector2 westHome = layout != null && layout.homes != null ? new Vector2(layout.homes.west[0], layout.homes.west[1]) : HouseFallbackPixel;
        Vector2 northHome = layout != null && layout.homes != null ? new Vector2(layout.homes.north[0], layout.homes.north[1]) : HouseFallbackPixel;
        var neighborhood = new GameObject("Neighborhood").transform;
        neighborhood.SetParent(p3, false);
        neighborhood.position = toWorld(westHome);
        var anchor = neighborhood.gameObject.AddComponent<TerritoryAnchor>();
        var anchorSo = new SerializedObject(anchor);
        anchorSo.FindProperty("_westPosition").vector3Value = toWorld(westHome);
        anchorSo.FindProperty("_northPosition").vector3Value = toWorld(northHome);
        anchorSo.FindProperty("_northDevelopment").stringValue = TerritoryRules.HomeDevelopment(TerritoryDirection.North);
        anchorSo.ApplyModifiedPropertiesWithoutUndo();

        // 이웃집 (같은 집 그림을 거울처럼 뒤집어 재사용) + 공사 현장
        Vector3 housePos = toWorld(westHome);
        var house = InstantiateProp("House_Blue", neighborhood, housePos, PropScale(props, "House_Blue"), true);
        house.name = "House_Blue_P3";
        var houseRenderer = house.GetComponent<SpriteRenderer>();
        float depth = FootprintDepthOffset(house.transform);
        SetDepthOffset(houseRenderer, depth);
        AddGate(house, HouseBuiltDevelopment, true, true);
        sites.Add(BuildHouseSite(neighborhood, "Site_P3House", "con_p3_house", houseRenderer, depth));

        // 환영 소품 자리 (고른 것 하나만 지어짐)
        Vector3 pot = toWorld(westHome + FlowerPotOffset);
        var potProp = P3Prop(neighborhood, "WelcomePot", "Prop_FlowerPot", pot, 1f, new Rect(-0.55f, -0.1f, 1.1f, 0.45f));
        AddGate(potProp.gameObject, FlowerPotDevelopment, true, true);
        var (potSite, potSiteSo) = CreateSite(neighborhood, "Site_WelcomePot", "con_p3_flowerpot", pot,
            pot + new Vector3(-1.2f, -0.3f, 0f), pot + new Vector3(0f, 2.2f, 0f), 1.4f);
        potSiteSo.ApplyModifiedPropertiesWithoutUndo();
        sites.Add(potSite);
        Vector3 line = toWorld(westHome + ClotheslineOffset);
        var lineProp = P3Prop(neighborhood, "WelcomeClothesline", "Prop_Clothesline", line, 1f, new Rect(-1.7f, -0.1f, 3.4f, 0.4f));
        AddGate(lineProp.gameObject, ClotheslineDevelopment, true, true);
        var (lineSite, lineSiteSo) = CreateSite(neighborhood, "Site_WelcomeClothesline", "con_p3_clothesline", line,
            line + new Vector3(-2.2f, -0.3f, 0f), line + new Vector3(0f, 3f, 0f), 3f);
        lineSiteSo.ApplyModifiedPropertiesWithoutUndo();
        sites.Add(lineSite);

        LinkP3ToView(view, sites, taskSites, focus);

        // 7) 영토: 전체 지도 · 숲 · 칸별 걷기 영역 · 개간 자리 · 카메라 범위
        BuildTerritory(root, view, background, toWorld);
    }

    // 새 소품: 그림(아래 가운데 피벗, 앞뒤 정렬) + 바닥 발자국 + 장난감을 못 놓는 자리
    private static SpriteRenderer P3Prop(Transform parent, string name, string art, Vector3 position, float scale, Rect footprint)
    {
        var renderer = CreateSprite(name, parent, LoadPropArt(art), position, true);
        renderer.transform.localScale = Vector3.one * scale;
        AddFootprint(renderer.transform, footprint);
        AddDecorBlock(renderer.transform, new Vector2(footprint.center.x, footprint.center.y), new Vector2(footprint.width, Mathf.Max(0.8f, footprint.height)));
        return renderer;
    }

    // 모임 연출: 식탁 둘레 자리 4개 (왼쪽·오른쪽·앞 두 자리)
    private static void BuildGathering(Transform parent, Vector3 table, PlazaCameraController camera)
    {
        var go = new GameObject("Gathering");
        go.transform.SetParent(parent, false);
        go.transform.position = table;
        var seats = new List<Transform>
        {
            Point(go.transform, "Seat1", table + new Vector3(-2.6f, 0.6f, 0f)),
            Point(go.transform, "Seat2", table + new Vector3(2.6f, 0.6f, 0f)),
            Point(go.transform, "Seat3", table + new Vector3(-1.1f, -0.9f, 0f)),
            Point(go.transform, "Seat4", table + new Vector3(1.1f, -0.9f, 0f)),
        };
        var director = go.AddComponent<GatheringDirector>();
        var so = new SerializedObject(director);
        so.FindProperty("_table").objectReferenceValue = go.transform;
        SetList(so.FindProperty("_seats"), seats);
        so.FindProperty("_camera").objectReferenceValue = camera;
        so.FindProperty("_seconds").floatValue = 12f;
        SetStrings(so.FindProperty("_lines"), "우리 마을 첫 모임이에요!", "다 같이 모이니 좋네요~", "당근 맛있다!", "다음에도 또 모여요!");
        so.FindProperty("_speechBubble").objectReferenceValue = LoadPropArt("UI_Bubble_Speech");
        so.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontAssetPath);
        so.FindProperty("_memoryTitle").stringValue = "첫 마을 모임";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 지난 P3(지운 것) 칸을 빼고 새 현장·작업 현장·비출 곳을 붙임
    private static void LinkP3ToView(SettlementPlazaView view, List<ConstructionSiteView> sites, List<PlazaTaskSiteView> taskSites,
        List<Transform> focus)
    {
        var so = new SerializedObject(view);
        AppendAlive(so.FindProperty("_sites"), sites);
        AppendAlive(so.FindProperty("_taskSites"), taskSites);
        AppendAlive(so.FindProperty("_focusPoints"), focus);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AppendAlive<T>(SerializedProperty list, List<T> added) where T : Object
    {
        var kept = new List<T>();
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue is T item && item != null && !added.Contains(item))
                kept.Add(item);
        }
        kept.AddRange(added);
        SetList(list, kept);
    }

    #endregion
}
