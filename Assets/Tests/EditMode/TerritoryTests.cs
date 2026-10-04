using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 영토 확장 (서쪽·북쪽 숲 개간 → 마을회관 영토 확장 미션 → 땅):
/// 개간 기회(Lv.6에 1번, 레벨마다 1번 쌓임), 한 번에 한 곳만, 3번 끝내야 미션, 미션 납품 → 땅·칸 발전·경험치 한 번,
/// 같은 방향 다음 단계는 앞 단계를 넓힌 뒤, 모서리 칸은 두 방향을 다 넓혀야, 진행 중 개간의 세이브 왕복, 동쪽 확장 세이브 옮기기.
/// </summary>
public class TerritoryTests
{
    private readonly List<Object> _created = new List<Object>();
    private readonly List<GameObject> _objects = new List<GameObject>();

    private ItemDefinition _wood;
    private ItemDefinition _stone;
    private SettlementOtterDefinition _first;
    private SettlementOtterDefinition _painter;
    private TerritoryExpansionDefinition _west1;
    private TerritoryExpansionDefinition _west2;
    private TerritoryExpansionDefinition _north1;
    private SettlementConfig _config;
    private LevelTable _levelTable;

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
        _first = Otter("otter_first");
        _painter = Otter("otter_painter");

        _west1 = Territory("territory_west_1", TerritoryDirection.West, 1, 500);
        _west2 = Territory("territory_west_2", TerritoryDirection.West, 2, 1000);
        _north1 = Territory("territory_north_1", TerritoryDirection.North, 1, 500);

        _config = Create<SettlementConfig>();
        var so = new SerializedObject(_config);
        so.FindProperty("_firstOtter").objectReferenceValue = _first;
        var otters = so.FindProperty("_otters");
        otters.arraySize = 2;
        otters.GetArrayElementAtIndex(0).objectReferenceValue = _first;
        otters.GetArrayElementAtIndex(1).objectReferenceValue = _painter;
        so.ApplyModifiedPropertiesWithoutUndo();
        _config.SetupTerritory(new[] { _west1, _west2, _north1 }, 6);

        _levelTable = Create<LevelTable>();
        var tableSo = new SerializedObject(_levelTable);
        var levels = tableSo.FindProperty("_levels");
        int[] need = { 100, 150, 220, 320, 450, 600, 800, 1050, 1350, 1700, 2100, 2600, 3200 };
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

    private ItemDefinition Item(string id)
    {
        var item = Create<ItemDefinition>();
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private SettlementOtterDefinition Otter(string id)
    {
        var otter = Create<SettlementOtterDefinition>();
        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    private SettlementTaskDefinition Task(string id, string result)
    {
        var task = Create<SettlementTaskDefinition>();
        var so = new SerializedObject(task);
        so.FindProperty("_taskId").stringValue = id;
        so.FindProperty("_title").stringValue = id;
        so.FindProperty("_requiredWorkers").intValue = 1;
        so.FindProperty("_durationSeconds").floatValue = 180f;
        so.FindProperty("_resultDevelopment").stringValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();
        return task;
    }

    private TerritoryExpansionDefinition Territory(string id, TerritoryDirection direction, int tier, int xp)
    {
        var steps = new List<TerritoryClearingStep>();
        for (int k = 1; k <= 3; k++)
            steps.Add(new TerritoryClearingStep(Task($"task_{id}_{k}", $"{id}_c{k}"), new[] { new ItemAmount(_wood, 30) }));
        var mission = Create<CommunityProjectDefinition>();
        mission.Setup($"{id}_mission", 100, id, 1, $"{id}_cleared", 500, new[] { new ItemAmount(_wood, 40), new ItemAmount(_stone, 15) },
            new ProjectStageDefinition[0], id, xp);
        var territory = Create<TerritoryExpansionDefinition>();
        territory.Setup(id, direction, tier, id, steps, mission);
        return territory;
    }

    private static Settlement Village()
    {
        var settlement = new Settlement();
        settlement.MarkInitialized();
        settlement.SetResident("otter_first", ResidentState.Resident);
        settlement.SetResident("otter_painter", ResidentState.Resident);
        settlement.MarkMet("otter_first");
        settlement.MarkMet("otter_painter");
        settlement.SetVersion(SettlementSaveData.CurrentVersion);
        return settlement;
    }

    private static SettlementSaveData Saved(Settlement settlement)
    {
        var save = new SettlementSaveData();
        settlement.Write(save);
        return save;
    }

    private ProfileManager Profile(int level)
    {
        var go = new GameObject("ProfileManager");
        _objects.Add(go);
        go.SetActive(false);
        var profile = go.AddComponent<ProfileManager>();
        var so = new SerializedObject(profile);
        so.FindProperty("_levelTable").objectReferenceValue = _levelTable;
        so.ApplyModifiedPropertiesWithoutUndo();
        typeof(ProfileManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(profile, null);
        profile.LoadFromSave(new ProfileSaveData { name = "test", level = level, exp = 0 });
        return profile;
    }

    private SettlementManager Manager(SettlementSaveData save, FakeWallet wallet = null)
    {
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
        manager.LoadFromSave(save);
        return manager;
    }

    private TaskStartResult Start(SettlementManager manager, TerritoryExpansionDefinition territory, SettlementOtterDefinition worker = null)
    {
        var task = manager.NextTerritoryClearing(territory);
        Assert.IsNotNull(task, $"{territory.ExpansionId}의 다음 개간");
        return manager.TryStartTask(task, new[] { worker ?? _first });
    }

    // 개간을 시작하고 바로 끝냄 (레벨은 그때그때 맞춤)
    private void Clear(SettlementManager manager, ProfileManager profile, TerritoryExpansionDefinition territory)
    {
        if (manager.TerritoryChances == 0)
            profile.ReachLevel(profile.Level + 1);
        Assert.AreEqual(TaskStartResult.Started, Start(manager, territory));
        manager.DevFinishTasks();
    }

    private static FakeWallet Rich() => new FakeWallet { GoldAmount = 100000 };

    #endregion

    [Test]
    public void ChanceStartsAtLevel6AndAccumulates()
    {
        var profile = Profile(5);
        var manager = Manager(Saved(Village()));
        Assert.AreEqual(0, manager.TerritoryChances);
        Assert.AreEqual(TerritoryBlock.NoChance, manager.GetTerritoryBlock(TerritoryDirection.West));
        Assert.AreEqual(TaskStartResult.NotAvailable, Start(manager, _west1), "Lv.5에는 개간할 수 없음");

        profile.ReachLevel(6);
        Assert.AreEqual(1, manager.TerritoryChances);
        Assert.AreEqual(TerritoryBlock.None, manager.GetTerritoryBlock(TerritoryDirection.West));
        Assert.AreEqual(TerritoryBlock.None, manager.GetTerritoryBlock(TerritoryDirection.North));

        // 기회는 쌓임 (Lv.6 → 9: 4번)
        profile.ReachLevel(9);
        Assert.AreEqual(4, manager.TerritoryChances);
    }

    [Test]
    public void OnlyOneClearingAtATime()
    {
        var profile = Profile(8);
        var manager = Manager(Saved(Village()));
        Assert.AreEqual(3, manager.TerritoryChances);
        Assert.AreEqual(TaskStartResult.Started, Start(manager, _north1));
        Assert.AreEqual(2, manager.TerritoryChances, "시작한 개간은 기회를 씀");

        // 북쪽을 개간하는 동안 서쪽은 못 함 (기회와 쉬는 주민이 있어도)
        Assert.AreEqual(TerritoryBlock.OtherClearing, manager.GetTerritoryBlock(TerritoryDirection.West));
        Assert.AreEqual(TaskStartResult.NotAvailable, Start(manager, _west1, _painter));
        Assert.AreEqual(SettlementTaskState.Locked, manager.GetTaskState(_west1.Clearings[0].Task));
        Assert.AreEqual(_north1, manager.WorkingTerritory(out _));

        manager.DevFinishTasks();
        Assert.AreEqual(1, manager.TerritoryClearingsDone(_north1));
        Assert.AreEqual(TerritoryBlock.None, manager.GetTerritoryBlock(TerritoryDirection.West), "끝나면 다른 방향도 고를 수 있음");
        Assert.AreEqual(TaskStartResult.Started, Start(manager, _west1, _painter));
        _ = profile;
    }

    [Test]
    public void ThreeClearingsOpenTheMissionThenDeliveryExpands()
    {
        var profile = Profile(6);
        var wallet = Rich();
        wallet.Items[_wood] = 100;
        wallet.Items[_stone] = 100;
        var manager = Manager(Saved(Village()), wallet);

        Clear(manager, profile, _west1);
        Clear(manager, profile, _west1);
        Assert.AreEqual(TerritoryStage.Clearing, manager.GetTerritoryStage(_west1));
        Assert.AreEqual(0, manager.OpenTerritoryMissions.Count, "3번 다 끝내야 미션");
        Assert.AreEqual(ProjectPhase.Locked, manager.GetProjectStatus(_west1.Mission).Phase);
        Clear(manager, profile, _west1);

        Assert.AreEqual(TerritoryStage.Cleared, manager.GetTerritoryStage(_west1));
        Assert.IsTrue(manager.HasDevelopment(_west1.ClearedDevelopment));
        Assert.AreEqual(TerritoryBlock.Mission, manager.GetTerritoryBlock(TerritoryDirection.West));
        CollectionAssert.AreEqual(new[] { _west1.Mission }, manager.OpenTerritoryMissions);
        Assert.IsFalse(manager.HasDevelopment(TerritoryRules.CellDevelopment(1, 0)), "미션 전에는 땅이 열리지 않음");
        Assert.AreEqual(ProjectPhase.Delivering, manager.GetProjectStatus(_west1.Mission).Phase);

        int expBefore = profile.Progress.Exp;
        int levelBefore = profile.Level;
        Assert.IsTrue(manager.TryDeliverAll(_west1.Mission));
        Assert.AreEqual(100000 - 500, wallet.GoldAmount);
        Assert.AreEqual(60, wallet.Count(_wood));
        Assert.AreEqual(85, wallet.Count(_stone));

        Assert.AreEqual(TerritoryStage.Expanded, manager.GetTerritoryStage(_west1));
        Assert.IsTrue(manager.HasDevelopment("territory_west_1"));
        Assert.IsTrue(manager.HasDevelopment(TerritoryRules.CellDevelopment(1, 0)));
        Assert.IsFalse(manager.HasDevelopment(TerritoryRules.CellDevelopment(1, 1)), "북쪽을 넓히기 전 모서리는 닫힘");
        Assert.IsTrue(manager.HasDevelopment(TerritoryRules.FirstDevelopment));
        Assert.IsTrue(manager.HasDevelopment(TerritoryRules.HomeDevelopment(TerritoryDirection.West)));
        Assert.IsTrue(profile.Level > levelBefore || profile.Progress.Exp > expBefore, "미션 경험치");
        Assert.AreEqual(0, manager.OpenTerritoryMissions.Count);
        Assert.AreEqual(_west2, manager.CurrentTerritory(TerritoryDirection.West), "다음 단계로");

        // 다시 불러와도 경험치를 두 번 주지 않음
        int level = profile.Level;
        int exp = profile.Progress.Exp;
        var again = Manager(Saved(manager.Settlement), wallet);
        Assert.AreEqual(TerritoryStage.Expanded, again.GetTerritoryStage(_west1));
        Assert.AreEqual(level, profile.Level);
        Assert.AreEqual(exp, profile.Progress.Exp);
    }

    [Test]
    public void NextTierWaitsForTheExpansion()
    {
        var profile = Profile(12);
        var manager = Manager(Saved(Village()), Rich());
        Assert.AreEqual(TerritoryStage.Locked, manager.GetTerritoryStage(_west2));
        Assert.AreEqual(TaskStartResult.NotAvailable, manager.TryStartTask(_west2.Clearings[0].Task, new[] { _first }));
        for (int i = 0; i < 3; i++)
            Clear(manager, profile, _west1);
        // 미션 전: 서쪽 2단계는 아직 잠김, 북쪽은 할 수 있음
        Assert.AreEqual(TerritoryStage.Locked, manager.GetTerritoryStage(_west2));
        Assert.AreEqual(TerritoryBlock.None, manager.GetTerritoryBlock(TerritoryDirection.North));
    }

    [Test]
    public void CornerCellNeedsBothDirections()
    {
        var settlement = Village();
        settlement.UnlockDevelopment("territory_north_1");
        var profile = Profile(6);
        var manager = Manager(Saved(settlement));
        Assert.IsTrue(manager.HasDevelopment(TerritoryRules.CellDevelopment(0, 1)));
        Assert.IsTrue(manager.HasDevelopment(TerritoryRules.HomeDevelopment(TerritoryDirection.North)), "북쪽을 먼저 넓힘 → 이웃집도 북쪽");
        Assert.IsFalse(manager.HasDevelopment(TerritoryRules.CellDevelopment(1, 1)));

        settlement = manager.Settlement;
        settlement.UnlockDevelopment("territory_west_1");
        manager = Manager(Saved(settlement));
        Assert.IsTrue(manager.HasDevelopment(TerritoryRules.CellDevelopment(1, 0)));
        Assert.IsTrue(manager.HasDevelopment(TerritoryRules.CellDevelopment(1, 1)));
        Assert.IsFalse(manager.HasDevelopment(TerritoryRules.HomeDevelopment(TerritoryDirection.West)), "집 자리는 처음 넓힌 쪽 그대로");
        _ = profile;
    }

    [Test]
    public void WorkingClearingSurvivesSaveAndCountsAsUsed()
    {
        var profile = Profile(6);
        var manager = Manager(Saved(Village()));
        Assert.AreEqual(TaskStartResult.Started, Start(manager, _west1));
        var again = Manager(Saved(manager.Settlement));
        Assert.AreEqual(_west1, again.WorkingTerritory(out var task));
        Assert.AreEqual(_west1.Clearings[0].Task, task);
        Assert.AreEqual(0, again.TerritoryChances);
        Assert.AreEqual(SettlementTaskState.Working, again.GetTaskState(task));
        again.DevFinishTasks();
        Assert.IsTrue(again.HasDevelopment("territory_west_1_c1"));
        StringAssert.Contains("1/3", again.TerritoryDoneMessage(task));
        _ = profile;
    }

    [Test]
    public void LegacyEastExpansionBecomesWestTierOne()
    {
        var settlement = Village();
        settlement.UnlockDevelopment(SettlementManager.LegacyPlazaExpandDevelopment);
        var profile = Profile(9);
        int exp = profile.Progress.Exp;
        var manager = Manager(Saved(settlement));
        Assert.AreEqual(TerritoryStage.Expanded, manager.GetTerritoryStage(_west1));
        Assert.IsTrue(manager.HasDevelopment(TerritoryRules.CellDevelopment(1, 0)));
        Assert.IsTrue(manager.HasDevelopment(TerritoryRules.FirstDevelopment));
        Assert.IsTrue(manager.HasDevelopment(TerritoryRules.HomeDevelopment(TerritoryDirection.West)));
        Assert.AreEqual(exp, profile.Progress.Exp, "옮길 때 미션 경험치를 주지 않음");
        Assert.AreEqual(9 - 6 + 1 - 3, manager.TerritoryChances, "옮긴 개간 3번은 쓴 기회");
    }
}
