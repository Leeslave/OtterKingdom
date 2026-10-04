using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// P3 공동사업 · 생활 의뢰 · 요정 방문 순서:
/// 선행 체인과 레벨(하던 사업은 레벨과 상관없이), 부분 납품(남은 양·가진 양 중 작은 만큼, 결제와 기록이 함께, 재접속해도 같음),
/// 건설 큐가 차 있으면 납품 보존, 단계 = 원본 기록(건설·장애물·작업·입주·모임), 보상 한 번(다시 불러와도·주다 만 것은 마저),
/// 집 인스턴스와 입주(입주 전 노동력 없음), 소품 한 번 고르기, 모임(건너뛰어도 완료), 반복 사업 비용 상한,
/// 생활 의뢰(회차로 정해짐, 교체는 보상 없이, 주민 작업 기록이 쌓이지 않음), 요정(파견 → 예약 → 도착, P3 전 세이브 옮기기).
/// </summary>
public class P3ProjectTests
{
    private readonly List<Object> _created = new List<Object>();
    private readonly List<GameObject> _objects = new List<GameObject>();

    private ItemDefinition _wood;
    private ItemDefinition _stone;
    private ItemDefinition _carrot;
    private SettlementOtterDefinition _first;
    private SettlementOtterDefinition _painter;
    private SettlementOtterDefinition _builder;
    private SettlementOtterDefinition _farmer;
    private SettlementOtterDefinition _neighbor;
    private SettlementTaskDefinition _expandTask;
    private SettlementTaskDefinition _tidyTask;
    private BoardRequestDefinition _reqSupply;
    private BoardRequestDefinition _reqHouse;
    private BoardRequestDefinition _reqPot;
    private BoardRequestDefinition _reqLine;
    private BoardRequestDefinition _reqTable;
    private CommunityProjectDefinition _supply;
    private CommunityProjectDefinition _plaza;
    private CommunityProjectDefinition _neighborProject;
    private CommunityProjectDefinition _welcome;
    private CommunityProjectDefinition _gathering;
    private CommunityProjectDefinition _repeat;
    private LifeRequestTemplate _snack;
    private LifeRequestTemplate _repair;
    private LifeRequestTemplate _tidy;
    private SettlementConfig _config;
    private LevelTable _levelTable;

    private static readonly string[] Obstacles = { "p3_brush_01", "p3_brush_02" };

    private class FakeWallet : IProjectWallet
    {
        public int GoldAmount;
        public readonly Dictionary<ItemDefinition, int> Items = new Dictionary<ItemDefinition, int>();

        public int Gold => GoldAmount;

        public int Count(ItemDefinition item) => item != null && Items.TryGetValue(item, out int n) ? n : 0;

        public bool TrySpendGold(int amount)
        {
            if (amount > GoldAmount)
                return false;
            GoldAmount -= amount;
            return true;
        }

        public bool TryRemove(ItemDefinition item, int amount)
        {
            if (Count(item) < amount)
                return false;
            Items[item] = Count(item) - amount;
            return true;
        }
    }

    #region 만들기

    [SetUp]
    public void SetUp()
    {
        _wood = Item("wood");
        _stone = Item("stone");
        _carrot = Item("crop_carrot");

        var farm = Create<DevelopableRegionDefinition>();
        Set(farm, "_regionId", "region_farm");
        Set(farm, "_operationalDevelopment", "farmland");
        Set(farm, "_productionDevelopment", "farm_working");
        _first = Otter("otter_first", null, false);
        _painter = Otter("otter_painter", null, false);
        _builder = Otter("otter_builder", null, true);
        _farmer = Otter("otter_farmer", farm, false);
        Set(_farmer, "_assignDevelopment", "fairy_invited");
        _neighbor = Otter("otter_p3_neighbor", null, false);

        _expandTask = Task("task_p3_plaza_expand", "p3_plaza_obstacles_cleared", "plaza_expand_01", 60f);
        _tidyTask = Task("task_life_hall_tidy", "", "", 20f);

        _reqSupply = Request("req_p3_supply_box", "p3_supply_01_paid", Construction("con_p3_supply_box", "p3_supply_box_built"), null);
        _reqHouse = Request("req_p3_house", "p3_neighbor_01_paid", Construction("con_p3_house", "p3_house_built"), _neighbor);
        _reqPot = Request("req_p3_flowerpot", "p3_welcome_01_paid", Construction("con_p3_flowerpot", "p3_flowerpot_built"), null);
        _reqLine = Request("req_p3_clothesline", "p3_welcome_01_paid", Construction("con_p3_clothesline", "p3_clothesline_built"), null);
        _reqTable = Request("req_p3_table", "p3_gathering_01_paid", Construction("con_p3_table", "p3_table_built"), null);

        _supply = Project("p3_supply_01", 1, 7, "town_hall_built", 300, new[] { (_wood, 12), (_stone, 6) },
            new[] { new ProjectStageDefinition("build_box", ProjectActionKind.CompleteConstruction, "상자 설치").With(construction: _reqSupply) },
            "supply_ready", 560);
        _plaza = Project("p3_plaza_01", 2, 8, "supply_ready", 500, new[] { (_wood, 18), (_stone, 12) },
            new[]
            {
                new ProjectStageDefinition("clear_brush", ProjectActionKind.ClearObstacles, "덤불 치우기")
                    .With(obstacleIds: Obstacles, clearedDevelopment: "p3_plaza_obstacles_cleared"),
                new ProjectStageDefinition("prepare_ground", ProjectActionKind.CompleteRegionTask, "주민 정비").With(task: _expandTask),
            },
            "plaza_expand_01", 735);
        _neighborProject = Project("p3_neighbor_01", 3, 9, "plaza_expand_01", 800, new[] { (_wood, 24), (_stone, 16) },
            new[]
            {
                new ProjectStageDefinition("build_house", ProjectActionKind.CompleteConstruction, "이웃집 짓기").With(construction: _reqHouse),
                new ProjectStageDefinition("move_in", ProjectActionKind.SettleResident, "입주")
                    .With(resident: _neighbor, houseSlotId: "slot_plaza_expand_01_house"),
            },
            "p3_neighbor_settled", 610);
        _welcome = Project("p3_welcome_01", 4, 9, "p3_neighbor_settled", 150, new[] { (_wood, 6) },
            new[] { new ProjectStageDefinition("welcome_prop", ProjectActionKind.PlaceWelcomeProp, "소품 설치").With(options: new[] { _reqPot, _reqLine }) },
            "welcome_corner_ready", 340);
        _gathering = Project("p3_gathering_01", 5, 10, "welcome_corner_ready", 300, new[] { (_wood, 8), (_carrot, 12) },
            new[]
            {
                new ProjectStageDefinition("build_table", ProjectActionKind.CompleteConstruction, "식탁 준비").With(construction: _reqTable),
                new ProjectStageDefinition("hold_gathering", ProjectActionKind.HoldGathering, "모임 열기"),
            },
            "first_gathering_complete", 425);
        _repeat = Project("p3_village_life", 6, 10, "first_gathering_complete", 0, new (ItemDefinition, int)[0],
            new ProjectStageDefinition[0], "", 0);
        _repeat.SetupRepeat(new[] { "식탁 준비", "휴식 공간 정돈", "회관 물품 보충" },
            new[]
            {
                new ProjectCycleCost(200, new[] { new ItemAmount(_carrot, 10) }),
                new ProjectCycleCost(150, new[] { new ItemAmount(_wood, 10) }),
                new ProjectCycleCost(250, new[] { new ItemAmount(_stone, 8) }),
            },
            0.25f, 2f, 5f, 3, "p3_hall_decor_1");

        _snack = Create<LifeRequestTemplate>();
        _snack.Setup("life_snack", LifeRequestKind.Deliver, "오늘의 간식", "{item} {amount}개", new[] { _carrot }, 8, null, 60, 8f, null, "Farm");
        _repair = Create<LifeRequestTemplate>();
        _repair.Setup("life_repair", LifeRequestKind.Deliver, "집수리 재료", "{item} {amount}개", new[] { _wood, _stone }, 6, null, 80, 8f);
        _tidy = Create<LifeRequestTemplate>();
        _tidy.Setup("life_hall_tidy", LifeRequestKind.ResidentWork, "회관 주변 정리", "정리", null, 1, _tidyTask, 40, 8f);

        _config = Create<SettlementConfig>();
        SetRef(_config, "_firstOtter", _first);
        SetList(_config, "_otters", new Object[] { _first, _painter, _builder, _farmer, _neighbor });
        SetList(_config, "_tasks", new Object[] { _expandTask, _tidyTask });
        Set(_config, "_boardUpgradeDevelopment", "board_upgraded");
        Set(_config, "_boardManagedDevelopment", "receptionist_assigned");
        Set(_config, "_guildDevelopment", "guild_office_built");
        Set(_config, "_townHallDevelopment", "town_hall_built");
        SetInt(_config, "_residentRequestSlots", 2);
        _config.SetupP3(new[] { _supply, _plaza, _neighborProject, _welcome, _gathering, _repeat },
            new[] { _reqSupply, _reqHouse, _reqPot, _reqLine, _reqTable }, new[] { _snack, _tidy, _repair },
            "town_hall_built", 7, "fairy_invited", "fairy_arrived");

        _levelTable = Create<LevelTable>();
        var tableSo = new SerializedObject(_levelTable);
        var levels = tableSo.FindProperty("_levels");
        int[] need = { 100, 150, 220, 320, 450, 600, 800, 1050, 1350, 1700, 2100 };
        levels.arraySize = need.Length;
        for (int i = 0; i < need.Length; i++)
            levels.GetArrayElementAtIndex(i).FindPropertyRelative("_expToNext").intValue = need[i];
        tableSo.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _objects)
            Object.DestroyImmediate(go);
        _objects.Clear();
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
        typeof(SettlementManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
        typeof(ProfileManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
    }

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    private static void Set(Object target, string property, string value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string property, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRef(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetList(Object target, string property, Object[] values)
    {
        var so = new SerializedObject(target);
        var list = so.FindProperty(property);
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private ItemDefinition Item(string id)
    {
        var item = Create<ItemDefinition>();
        Set(item, "_itemId", id);
        Set(item, "_displayName", id);
        return item;
    }

    private SettlementOtterDefinition Otter(string id, DevelopableRegionDefinition workRegion, bool builder)
    {
        var otter = Create<SettlementOtterDefinition>();
        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.FindProperty("_workRegion").objectReferenceValue = workRegion;
        so.FindProperty("_isBuilder").boolValue = builder;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    private SettlementTaskDefinition Task(string id, string requires, string result, float seconds)
    {
        var task = Create<SettlementTaskDefinition>();
        var so = new SerializedObject(task);
        so.FindProperty("_taskId").stringValue = id;
        so.FindProperty("_title").stringValue = id;
        so.FindProperty("_requiredWorkers").intValue = 1;
        so.FindProperty("_durationSeconds").floatValue = seconds;
        so.FindProperty("_requiredDevelopment").stringValue = requires;
        so.FindProperty("_resultDevelopment").stringValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();
        return task;
    }

    private ConstructionDefinition Construction(string id, string result)
    {
        var construction = Create<ConstructionDefinition>();
        var so = new SerializedObject(construction);
        so.FindProperty("_constructionId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.FindProperty("_durationSeconds").floatValue = 30f;
        so.FindProperty("_needsBuilder").boolValue = true;
        so.FindProperty("_unlockResultId").stringValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();
        return construction;
    }

    private BoardRequestDefinition Request(string id, string requires, ConstructionDefinition construction, SettlementOtterDefinition arrival)
    {
        var request = Create<BoardRequestDefinition>();
        var so = new SerializedObject(request);
        so.FindProperty("_requestId").stringValue = id;
        so.FindProperty("_title").stringValue = id;
        so.FindProperty("_contentVersion").intValue = 3;
        so.FindProperty("_requiredDevelopment").stringValue = requires;
        so.FindProperty("_construction").objectReferenceValue = construction;
        so.FindProperty("_stageOnComplete").intValue = -1;
        if (arrival != null)
        {
            var arrivals = so.FindProperty("_arrivals");
            arrivals.arraySize = 1;
            arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("_otter").objectReferenceValue = arrival;
            arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("_state").enumValueIndex = (int)ResidentState.Visitor;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return request;
    }

    private CommunityProjectDefinition Project(string id, int order, int level, string requires, int gold,
        (ItemDefinition item, int amount)[] items, ProjectStageDefinition[] stages, string result, int xp)
    {
        var project = Create<CommunityProjectDefinition>();
        var amounts = new List<ItemAmount>();
        foreach (var (item, amount) in items)
            amounts.Add(new ItemAmount(item, amount));
        project.Setup(id, order, id, level, requires, gold, amounts, stages, result, xp);
        return project;
    }

    // 마을회관까지 끝난 P2 끝 상태 (주민: 몽실·물감이·뚝딱이, 농부 일하는 중)
    private static Settlement HallDone()
    {
        var settlement = new Settlement();
        settlement.SetResident("otter_first", ResidentState.Resident);
        settlement.SetResident("otter_painter", ResidentState.Resident);
        settlement.SetResident("otter_builder", ResidentState.Resident);
        settlement.SetResident("otter_farmer", ResidentState.Resident);
        settlement.AdvanceSpecialist("otter_farmer", SpecialistState.Working, "region_farm", 1);
        foreach (var dev in new[] { "farmland", "farm_working", "board_upgraded", "receptionist_assigned", "guild_office_built",
                     "town_hall_built", "fairy_invited", "fairy_arrived" })
            settlement.UnlockDevelopment(dev);
        settlement.MarkMet("otter_first");
        settlement.MarkMet("otter_painter");
        settlement.MarkMet("otter_builder");
        settlement.SetVersion(SettlementSaveData.CurrentVersion);
        return settlement;
    }

    private static SettlementSaveData Saved(Settlement settlement)
    {
        var save = new SettlementSaveData();
        settlement.Write(save);
        return save;
    }

    private ProfileManager Profile(int level, int exp = 0)
    {
        var go = new GameObject("ProfileManager");
        _objects.Add(go);
        go.SetActive(false);
        var profile = go.AddComponent<ProfileManager>();
        var so = new SerializedObject(profile);
        so.FindProperty("_levelTable").objectReferenceValue = _levelTable;
        so.ApplyModifiedPropertiesWithoutUndo();
        typeof(ProfileManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(profile, null);
        profile.LoadFromSave(new ProfileSaveData { name = "test", level = level, exp = exp });
        return profile;
    }

    private SettlementManager Manager(SettlementSaveData save, FakeWallet wallet = null, bool fairyShopSeen = false)
    {
        // 다시 불러오기: 새 매니저가 주인이 되게 (앞 매니저는 꺼 둔 채로 남음)
        typeof(SettlementManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
        var go = new GameObject("SettlementManager");
        _objects.Add(go);
        go.SetActive(false);
        var manager = go.AddComponent<SettlementManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("_config").objectReferenceValue = _config;
        so.ApplyModifiedPropertiesWithoutUndo();
        typeof(SettlementManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        manager.WalletOverride = wallet ?? new FakeWallet();
        manager.LoadFromSave(save, fairyShopSeen);
        return manager;
    }

    // 한 프레임 (기록이 바뀐 뒤 사업을 다시 봄)
    private static void Frame(SettlementManager manager) =>
        typeof(SettlementManager).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);

    private static FakeWallet Rich() => new FakeWallet { GoldAmount = 100000 };

    private FakeWallet RichWithItems()
    {
        var wallet = Rich();
        wallet.Items[_wood] = 1000;
        wallet.Items[_stone] = 1000;
        wallet.Items[_carrot] = 1000;
        return wallet;
    }

    #endregion

    #region 선행 체인 · 레벨

    [Test]
    public void Chain_NoTownHall_NoProject()
    {
        var settlement = HallDone();
        var save = Saved(settlement);
        save.unlockedDevelopments.Remove("town_hall_built");
        Profile(10);
        var manager = Manager(save);

        Assert.IsNull(manager.ActiveProject);
        Assert.IsFalse(manager.IsLifeRequestOpen);
    }

    [Test]
    public void Chain_HallLevel6_NeedsLevel_Level7_Delivering()
    {
        Profile(6);
        var manager = Manager(Saved(HallDone()));
        Assert.AreEqual(_supply, manager.ActiveProject);
        Assert.AreEqual(ProjectPhase.NeedsLevel, manager.GetProjectStatus(_supply).Phase);

        ProfileManager.Instance.ReachLevel(7);
        Assert.AreEqual(ProjectPhase.Delivering, manager.GetProjectStatus(_supply).Phase);
    }

    [Test]
    public void Chain_HighLevelSave_DoesNotSkipOrAutoComplete()
    {
        Profile(11);
        var manager = Manager(Saved(HallDone()));

        Assert.AreEqual(_supply, manager.ActiveProject, "레벨이 높아도 순서를 건너뛰지 않음");
        Assert.AreEqual(ProjectPhase.Delivering, manager.GetProjectStatus(_supply).Phase, "레벨만으로 사업을 끝내지 않음");
    }

    #endregion

    #region 납품

    [Test]
    public void Deliver_PartialThenRest_NoOverpay_SurvivesReload()
    {
        Profile(7);
        var wallet = new FakeWallet { GoldAmount = 200 };
        wallet.Items[_wood] = 5;
        var manager = Manager(Saved(HallDone()), wallet);

        Assert.IsTrue(manager.TryDeliverAll(_supply));
        Assert.AreEqual(0, wallet.GoldAmount);
        Assert.AreEqual(0, wallet.Count(_wood));
        Assert.IsFalse(manager.TryDeliverAll(_supply), "넣을 것이 없으면 아무것도 안 함");

        // 종료 → 재접속
        var save = Saved(manager.Settlement);
        var wallet2 = new FakeWallet { GoldAmount = 1000 };
        wallet2.Items[_wood] = 100;
        wallet2.Items[_stone] = 100;
        var reloaded = Manager(save, wallet2);
        var materials = new List<ProjectMaterial>();
        reloaded.CollectProjectMaterials(_supply, materials);
        Assert.AreEqual(200, materials[0].Delivered, "넣은 골드 그대로");
        Assert.AreEqual(5, materials[1].Delivered, "넣은 목재 그대로");

        Assert.IsTrue(reloaded.TryDeliverAll(_supply));
        Assert.AreEqual(900, wallet2.GoldAmount, "남은 100만 냄 (초과 차감 없음)");
        Assert.AreEqual(93, wallet2.Count(_wood));
        Assert.AreEqual(94, wallet2.Count(_stone));
        Assert.IsTrue(reloaded.HasDevelopment("p3_supply_01_paid"));
        Assert.AreEqual(ProjectPhase.Stage, reloaded.GetProjectStatus(_supply).Phase);
        Assert.IsFalse(reloaded.TryDeliverAll(_supply), "연타해도 더 내지 않음");
        Assert.AreEqual(900, wallet2.GoldAmount);
    }

    [Test]
    public void Deliver_OneMaterial_TakesMinOfRemainingAndOwned()
    {
        Profile(7);
        var wallet = new FakeWallet { GoldAmount = 50 };
        wallet.Items[_stone] = 100;
        var manager = Manager(Saved(HallDone()), wallet);

        Assert.AreEqual(50, manager.TryDeliver(_supply, 0), "골드는 가진 만큼");
        Assert.AreEqual(6, manager.TryDeliver(_supply, 2), "돌은 남은 6개만");
        Assert.AreEqual(0, manager.TryDeliver(_supply, 2));
        Assert.AreEqual(94, wallet.Count(_stone));
    }

    #endregion

    #region 단계 · 완료 · 보상

    [Test]
    public void Supply_BuildFromQueue_NoSecondPayment_CompletesOnceWithXp()
    {
        var profile = Profile(7);
        var wallet = RichWithItems();
        var manager = Manager(Saved(HallDone()), wallet);
        int completed = 0;
        manager.OnProjectCompleted += (_, __) => completed++;
        manager.TryDeliverAll(_supply);
        int goldAfterDelivery = wallet.GoldAmount;

        Assert.AreEqual(ProjectActionResult.Done, manager.TryAdvanceProject(_supply), "건설 시작");
        Assert.AreEqual(ProjectActionResult.NotNow, manager.TryAdvanceProject(_supply), "연타: 공사는 한 건");
        Assert.AreEqual("req_p3_supply_box", manager.Settlement.Job.RequestId);
        Assert.AreEqual(goldAfterDelivery, wallet.GoldAmount, "건설에서 다시 결제하지 않음");

        manager.DevFinishJob();
        Frame(manager);

        Assert.IsTrue(manager.HasDevelopment("supply_ready"));
        Assert.AreEqual(1, completed);
        Assert.AreEqual(7, profile.Level);
        Assert.AreEqual(560, profile.Progress.Exp, "고정 경험치 560 (0.7 × E7)");
        Assert.AreEqual(_plaza, manager.ActiveProject);

        // 다시 불러와도 보상·완료 한 번
        var reloaded = Manager(Saved(manager.Settlement), RichWithItems());
        Frame(reloaded);
        Assert.AreEqual(560, profile.Progress.Exp);
        Assert.AreEqual(_plaza, reloaded.ActiveProject);
    }

    [Test]
    public void Supply_QueueBusy_DeliveryKept_StartsLater()
    {
        Profile(7);
        var settlement = HallDone();
        settlement.StartJob("req_other", "con_other", SettlementManager.NowTicks, SettlementManager.NowTicks + 1000);
        var manager = Manager(Saved(settlement), RichWithItems());
        manager.TryDeliverAll(_supply);

        var status = manager.GetProjectStatus(_supply);
        Assert.AreEqual(ProjectStageState.Busy, status.StageState, "재료 준비 완료 / 건설 대기");
        Assert.AreEqual(ProjectActionResult.Busy, manager.TryAdvanceProject(_supply));
        Assert.IsTrue(CommunityProjectRules.IsDelivered(_supply, manager.Settlement), "납품은 그대로 보존");
    }

    [Test]
    public void Load_RecordsDoneButRewardMissing_GrantsOnceSilently()
    {
        var profile = Profile(7);
        var settlement = HallDone();
        settlement.Deliver("p3_supply_01", 300, new[] { ("wood", 12), ("stone", 6) });
        settlement.UnlockDevelopment("p3_supply_01_paid");
        settlement.CompleteRequest("req_p3_supply_box", "p3_supply_box_built"); // 완료 직후 꺼짐 (보상 전)
        int announced = 0;
        var manager = Manager(Saved(settlement));
        manager.OnProjectCompleted += (_, __) => announced++;

        Assert.IsTrue(manager.HasDevelopment("supply_ready"));
        Assert.IsTrue(manager.Settlement.HasReward("p3_supply_01#0"));
        Assert.AreEqual(560, profile.Progress.Exp, "주다 만 경험치를 마저 줌");
        Frame(manager);
        Assert.AreEqual(560, profile.Progress.Exp, "두 번 주지 않음");
        Assert.AreEqual(0, announced, "불러올 때는 알림 없이");
    }

    [Test]
    public void Plaza_ObstaclesThenResidentTask_ExpandsAndReturnsWorker()
    {
        Profile(8);
        var settlement = HallDone();
        settlement.MarkProjectCompleted("p3_supply_01");
        settlement.UnlockDevelopment("supply_ready");
        var manager = Manager(Saved(settlement), RichWithItems());
        Assert.AreEqual(_plaza, manager.ActiveProject);
        Assert.IsFalse(manager.IsProjectObstacleActive("p3_brush_01"), "납품 전에는 치울 수 없음");

        manager.TryDeliverAll(_plaza);
        Assert.IsTrue(manager.IsProjectObstacleActive("p3_brush_01"));
        Assert.IsTrue(manager.TryClearProjectObstacle("p3_brush_01"));
        Assert.IsFalse(manager.TryClearProjectObstacle("p3_brush_01"), "같은 장애물은 한 번");
        Assert.AreEqual(ProjectStageState.Waiting, CommunityProjectRules.StageState(_plaza, _plaza.Stages[1], manager.Settlement));
        Assert.IsTrue(manager.TryClearProjectObstacle("p3_brush_02"));
        Assert.IsTrue(manager.HasDevelopment("p3_plaza_obstacles_cleared"));

        Assert.AreEqual(ProjectActionResult.OpenTask, manager.TryAdvanceProject(_plaza), "주민 고르기 화면");
        Assert.IsTrue(manager.CanAssign(_painter), "확장 전 주민으로 작업 가능");
        Assert.AreEqual(TaskStartResult.Started, manager.TryStartTask(_expandTask, new[] { _painter }));
        Assert.IsFalse(manager.CanAssign(_painter));
        manager.DevFinishTasks();
        Frame(manager);

        Assert.IsTrue(manager.HasDevelopment("plaza_expand_01"));
        Assert.IsTrue(manager.CanAssign(_painter), "노동력 반환");
        Assert.AreEqual(_neighborProject, manager.ActiveProject);
    }

    [Test]
    public void Neighbor_HouseThenMoveIn_LaborOnlyAfterMoveIn()
    {
        Profile(9);
        var settlement = HallDone();
        foreach (var p in new[] { "p3_supply_01", "p3_plaza_01" })
            settlement.MarkProjectCompleted(p);
        settlement.UnlockDevelopment("supply_ready");
        settlement.UnlockDevelopment("plaza_expand_01");
        var manager = Manager(Saved(settlement), RichWithItems());
        int workforce = manager.GetLaborSummary().Workforce;

        manager.TryDeliverAll(_neighborProject);
        Assert.AreEqual(ProjectActionResult.Done, manager.TryAdvanceProject(_neighborProject));
        manager.DevFinishJob();
        Frame(manager);

        var house = manager.Settlement.FindHouseBySlot("slot_plaza_expand_01_house");
        Assert.IsNotNull(house, "집 인스턴스");
        Assert.AreEqual("house_slot_plaza_expand_01_house", house.InstanceId);
        Assert.AreEqual("con_p3_house", house.DefinitionId);
        Assert.IsTrue(manager.Settlement.TryGetResidentState("otter_p3_neighbor", out var state));
        Assert.AreEqual(ResidentState.Visitor, state, "방문으로 찾아옴");
        Assert.AreEqual(workforce, manager.GetLaborSummary().Workforce, "집만으로 주민·노동력이 늘지 않음");
        Assert.IsFalse(manager.CanMoveIn(_neighbor), "광장에서 만나기 전");

        manager.Meet(_neighbor);
        Assert.IsTrue(manager.CanMoveIn(_neighbor));
        Assert.IsTrue(manager.TryMoveIn(_neighbor));
        Assert.IsFalse(manager.TryMoveIn(_neighbor), "연타");
        Assert.AreEqual("otter_p3_neighbor", manager.Settlement.FindHouseBySlot("slot_plaza_expand_01_house").ResidentId);
        Assert.AreEqual(workforce + 1, manager.GetLaborSummary().Workforce, "입주 후 정확히 하나 늘어남");
        Assert.IsTrue(manager.HasDevelopment("p3_neighbor_settled"));
        Assert.AreEqual(_welcome, manager.ActiveProject);

        // 방문 중·입주 뒤 종료: 집과 주민 연결 한 건
        var reloaded = Manager(Saved(manager.Settlement));
        Assert.AreEqual(1, reloaded.Settlement.Houses.Count);
        Assert.AreEqual("otter_p3_neighbor", reloaded.Settlement.Houses[0].ResidentId);
    }

    [Test]
    public void Welcome_ChooseOnce()
    {
        Profile(9);
        var settlement = HallDone();
        foreach (var p in new[] { "p3_supply_01", "p3_plaza_01", "p3_neighbor_01" })
            settlement.MarkProjectCompleted(p);
        foreach (var dev in new[] { "supply_ready", "plaza_expand_01", "p3_neighbor_settled" })
            settlement.UnlockDevelopment(dev);
        var manager = Manager(Saved(settlement), RichWithItems());
        manager.TryDeliverAll(_welcome);

        Assert.AreEqual(ProjectActionResult.NeedChoice, manager.TryAdvanceProject(_welcome));
        Assert.AreEqual(ProjectActionResult.Done, manager.TryChooseWelcomeProp(_welcome, _reqPot));
        Assert.AreEqual(ProjectActionResult.NotNow, manager.TryChooseWelcomeProp(_welcome, _reqLine), "공사 중에는 바꿀 수 없음");
        Assert.AreEqual("req_p3_flowerpot", manager.Settlement.GetProject("p3_welcome_01").Choice);
        manager.DevFinishJob();
        Frame(manager);
        Assert.IsTrue(manager.HasDevelopment("welcome_corner_ready"));
        Assert.IsFalse(manager.Settlement.IsCompleted("req_p3_clothesline"), "고르지 않은 소품은 남음");
    }

    [Test]
    public void Gathering_HoldOnce_ThenRepeatProject()
    {
        var profile = Profile(10);
        var settlement = HallDone();
        foreach (var p in new[] { "p3_supply_01", "p3_plaza_01", "p3_neighbor_01", "p3_welcome_01" })
            settlement.MarkProjectCompleted(p);
        foreach (var dev in new[] { "supply_ready", "plaza_expand_01", "p3_neighbor_settled", "welcome_corner_ready" })
            settlement.UnlockDevelopment(dev);
        var manager = Manager(Saved(settlement), RichWithItems());
        int held = 0;
        manager.OnGatheringHeld += _ => held++;
        manager.TryDeliverAll(_gathering);
        Assert.IsFalse(manager.TryHoldGathering(_gathering), "식탁 전에는 열 수 없음");
        manager.TryAdvanceProject(_gathering);
        manager.DevFinishJob();
        Frame(manager);

        Assert.IsTrue(manager.TryHoldGathering(_gathering));
        Assert.IsFalse(manager.TryHoldGathering(_gathering), "한 번만");
        Assert.AreEqual(1, held);
        Assert.IsTrue(manager.HasDevelopment("first_gathering_complete"));
        Assert.IsTrue(manager.HasHeldGathering);
        Assert.AreEqual(425, profile.Progress.Exp);
        Assert.AreEqual(_repeat, manager.ActiveProject, "첫 모임 뒤 반복 사업");

        // 연출을 보지 않고 꺼도 완료·보상은 그대로, 다시 볼 수 있음
        var reloaded = Manager(Saved(manager.Settlement));
        Assert.IsTrue(reloaded.HasHeldGathering);
        Assert.IsFalse(reloaded.Settlement.HasFlag(SettlementManager.GatheringViewedFlag));
        Assert.AreEqual(425, profile.Progress.Exp);
    }

    [Test]
    public void Repeat_CostCapped_CycleAdvancesOnce_MilestoneAfterThree()
    {
        Profile(10);
        var settlement = HallDone();
        settlement.UnlockDevelopment("first_gathering_complete");
        foreach (var p in new[] { "p3_supply_01", "p3_plaza_01", "p3_neighbor_01", "p3_welcome_01", "p3_gathering_01" })
            settlement.MarkProjectCompleted(p);
        var manager = Manager(Saved(settlement), RichWithItems());
        Assert.AreEqual(_repeat, manager.ActiveProject);

        Assert.AreEqual(1f, CommunityProjectRules.CostMultiplier(_repeat, 0));
        Assert.AreEqual(2f, CommunityProjectRules.CostMultiplier(_repeat, 10), "상한까지만");

        for (int i = 0; i < 3; i++)
        {
            Assert.IsTrue(manager.TryDeliverAll(_repeat), $"{i}회차");
            Assert.AreEqual(i + 1, manager.Settlement.ProjectCycle("p3_village_life"));
        }
        Assert.IsTrue(manager.HasDevelopment("p3_hall_decor_1"));
        var materials = new List<ProjectMaterial>();
        manager.CollectProjectMaterials(_repeat, materials);
        Assert.AreEqual(350, materials[0].Required, "4회차 = 첫 재료 × 1.75");
        Assert.AreEqual(0, materials[0].Delivered, "회차마다 새로 넣음");
    }

    #endregion

    #region 생활 의뢰

    [Test]
    public void LifeRules_PickIsDeterministic_SkipsUnobtainable()
    {
        Assert.IsTrue(LifeRequestRules.TryPick(_config, 0, _ => true, null, out var t0, out var i0, out int a0));
        Assert.IsTrue(LifeRequestRules.TryPick(_config, 0, _ => true, null, out var t0b, out var i0b, out _));
        Assert.AreEqual(t0, t0b, "같은 회차는 늘 같은 의뢰");
        Assert.AreEqual(i0, i0b);
        Assert.AreEqual(_snack, t0);
        Assert.AreEqual(8, a0);

        Assert.IsTrue(LifeRequestRules.TryPick(_config, 0, item => item != _carrot, null, out var t1, out _, out _));
        Assert.AreEqual(_tidy, t1, "작물을 얻을 수 없으면 다음 틀");
        Assert.IsTrue(LifeRequestRules.TryPick(_config, 0, _ => true, "life_snack", out var t2, out _, out _));
        Assert.AreNotEqual(_snack, t2, "이미 걸린 틀은 다른 틀이 있으면 피함");
    }

    [Test]
    public void Life_FillsResidentSlots_DeliverRewardsAndNext_SwapWithoutReward()
    {
        var profile = Profile(7);
        var wallet = RichWithItems();
        var manager = Manager(Saved(HallDone()), wallet);
        manager.EnsureLifeRequests();

        Assert.IsTrue(manager.IsLifeRequestOpen);
        Assert.AreEqual(2, manager.Settlement.LifeRequests.Count, "주민 부탁 칸 2개");
        var first = manager.Settlement.LifeRequests[0];
        Assert.AreEqual("life_snack", first.TemplateId);

        Assert.IsTrue(manager.TryCompleteLifeDelivery(first.Serial));
        Assert.AreEqual(992, wallet.Count(_carrot));
        Assert.AreEqual(64, profile.Progress.Exp, "Lv.7 필요 경험치 800의 8%");
        Assert.AreEqual(2, manager.Settlement.LifeRequests.Count, "다음 의뢰가 걸림");
        Assert.IsFalse(manager.TryCompleteLifeDelivery(first.Serial), "같은 의뢰를 두 번 끝내지 않음");
        Assert.AreEqual(1, manager.Settlement.LifeRequestsDone);

        var second = manager.Settlement.LifeRequests[0];
        int exp = profile.Progress.Exp;
        Assert.IsTrue(manager.TrySwapLifeRequest(second.Serial));
        Assert.AreEqual(exp, profile.Progress.Exp, "교체는 보상 없음");
        Assert.AreEqual(2, manager.Settlement.LifeRequests.Count);

        // 재접속해도 다시 뽑지 않음
        var before = new List<string>();
        foreach (var r in manager.Settlement.LifeRequests)
            before.Add($"{r.Serial}:{r.TemplateId}:{r.ItemId}:{r.Amount}");
        var reloaded = Manager(Saved(manager.Settlement), wallet);
        reloaded.EnsureLifeRequests();
        var after = new List<string>();
        foreach (var r in reloaded.Settlement.LifeRequests)
            after.Add($"{r.Serial}:{r.TemplateId}:{r.ItemId}:{r.Amount}");
        CollectionAssert.AreEqual(before, after);
    }

    [Test]
    public void Life_ResidentWork_UsesLabor_RewardOnFinish_NoTaskRecordPileUp()
    {
        var profile = Profile(7);
        var settlement = HallDone();
        var manager = Manager(Saved(settlement), RichWithItems());
        var record = manager.Settlement.AddLifeRequest("life_hall_tidy", null, 0);

        Assert.AreEqual(TaskStartResult.Started, manager.TryStartLifeWork(record.Serial, _painter));
        Assert.AreEqual(TaskStartResult.NotAvailable, manager.TryStartLifeWork(record.Serial, _first), "한 의뢰에 한 작업");
        Assert.IsFalse(manager.CanAssign(_painter), "작업 중인 주민은 다른 일에 못 감");
        Assert.IsFalse(manager.TrySwapLifeRequest(record.Serial), "작업 중인 의뢰는 바꿀 수 없음");
        Assert.AreEqual(_tidyTask, manager.PlazaTaskOf("otter_painter"), "광장 현장에서 일함");

        manager.DevFinishTasks();

        Assert.IsTrue(manager.CanAssign(_painter));
        Assert.AreEqual(64, profile.Progress.Exp);
        Assert.IsFalse(manager.Settlement.TryGetLifeRequest(record.Serial, out _));
        Assert.IsFalse(manager.Settlement.IsTaskCompleted($"task_life_hall_tidy#{record.Serial}"), "회차 작업 기록은 쌓이지 않음");
        Assert.IsFalse(manager.Settlement.IsTaskCompleted("task_life_hall_tidy"), "틀 작업은 그대로");
    }

    #endregion

    #region 저장

    [Test]
    public void Save_RoundTrip_KeepsP3Records_DropsBrokenLines()
    {
        var settlement = HallDone();
        settlement.Deliver("p3_supply_01", 120, new[] { ("wood", 4) });
        settlement.SetProjectChoice("p3_welcome_01", "req_p3_flowerpot");
        settlement.AddReward("p3_supply_01#0");
        settlement.AddHouse("house_a", "con_p3_house", "slot_a");
        settlement.SetHouseResident("house_a", "otter_p3_neighbor");
        settlement.AddLifeRequest("life_repair", "wood", 6);
        var save = Saved(settlement);
        save.houses.Add(new HouseSaveData { instanceId = "house_b", slotId = "slot_a", residentId = "x" }); // 같은 자리 두 번
        save.houses.Add(new HouseSaveData { instanceId = "house_c", slotId = "slot_c", residentId = "otter_p3_neighbor" }); // 한 해달 두 집
        save.projects.Add(new ProjectSaveData { projectId = "p3_supply_01", gold = 999 }); // 같은 사업 두 번
        save.lifeRequestSerial = 0; // 걸린 의뢰보다 뒤처진 회차

        var loaded = new Settlement();
        loaded.Load(save);

        Assert.AreEqual(120, loaded.GetProject("p3_supply_01").Gold);
        Assert.AreEqual(4, loaded.GetProject("p3_supply_01").Delivered("wood"));
        Assert.AreEqual("req_p3_flowerpot", loaded.GetProject("p3_welcome_01").Choice);
        Assert.IsTrue(loaded.HasReward("p3_supply_01#0"));
        Assert.AreEqual(2, loaded.Houses.Count);
        Assert.AreEqual("otter_p3_neighbor", loaded.FindHouseBySlot("slot_a").ResidentId);
        Assert.IsNull(loaded.FindHouseBySlot("slot_c").ResidentId, "한 해달은 한 집에만");
        Assert.AreEqual(1, loaded.LifeRequests.Count);
        Assert.AreEqual(1, loaded.LifeRequestSerial, "같은 회차를 두 번 쓰지 않게");
    }

    #endregion

    #region 요정

    [Test]
    public void Fairy_DispatchReserves_ArrivesOnce()
    {
        var settlement = HallDone();
        var save = Saved(settlement);
        save.unlockedDevelopments.Remove("fairy_invited");
        save.unlockedDevelopments.Remove("fairy_arrived");
        save.unlockedDevelopments.Remove("farm_working");
        save.specialists.Clear(); // 농부는 찾아와 있지만 아직 파견 전
        var manager = Manager(save);
        manager.Settlement.AdvanceSpecialist("otter_farmer", SpecialistState.AtPlaza);

        Assert.IsFalse(manager.IsFairyComing, "밭 개간만으로는 요정이 오지 않음");
        Assert.IsFalse(manager.IsFairyArrived);
        Assert.IsTrue(manager.TryAssignSpecialist(_farmer));
        Assert.IsTrue(manager.IsFairyComing, "파견이 저장되면 방문 예약");
        Assert.IsFalse(manager.IsFairyArrived, "실제 등장은 광장의 안전한 때");

        Assert.IsTrue(manager.TryMarkFairyArrived());
        Assert.IsFalse(manager.TryMarkFairyArrived(), "한 번만");
        Assert.IsTrue(manager.IsFairyArrived);
    }

    [Test]
    public void Fairy_Migration_OldSaves()
    {
        // P2 세이브: 밭 개간 + 농부 일하는 중 → 이미 요정이 광장에 있었음 → 그대로
        var dispatched = Saved(HallDone());
        dispatched.version = 2;
        dispatched.unlockedDevelopments.Remove("fairy_invited");
        dispatched.unlockedDevelopments.Remove("fairy_arrived");
        Assert.IsTrue(Manager(dispatched).IsFairyArrived);

        // 밭 개간만 하고 농부 미파견 · 상점 안 열어 봄 → 새 규칙 (예약 없음)
        var notDispatched = Saved(HallDone());
        notDispatched.version = 2;
        notDispatched.unlockedDevelopments.Remove("fairy_invited");
        notDispatched.unlockedDevelopments.Remove("fairy_arrived");
        notDispatched.specialists.Clear();
        var manager = Manager(notDispatched);
        Assert.IsFalse(manager.IsFairyArrived);
        Assert.IsFalse(manager.IsFairyComing);

        // 같은 세이브지만 상점 안내를 이미 봄 → 그대로
        var seen = Saved(HallDone());
        seen.version = 2;
        seen.unlockedDevelopments.Remove("fairy_invited");
        seen.unlockedDevelopments.Remove("fairy_arrived");
        seen.specialists.Clear();
        Assert.IsTrue(Manager(seen, null, true).IsFairyArrived);

        // P3 세이브는 옮기지 않음 (파견했는데 예약이 없으면 예약만 맞춤)
        var p3 = Saved(HallDone());
        p3.unlockedDevelopments.Remove("fairy_invited");
        p3.unlockedDevelopments.Remove("fairy_arrived");
        var p3Manager = Manager(p3);
        Assert.IsFalse(p3Manager.IsFairyArrived);
        Assert.IsTrue(p3Manager.IsFairyComing);
    }

    #endregion
}
