using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 전문 해달(광부·농부): 지역 운영 → 방문 → 광장에서 만남 → 배치 → 생산 안내 끝 → 일함, 세이브·옛 세이브.
/// 기록으로 세는 퀘스트(새 해달 만나기, 건설 완료)의 진행 규칙.
/// </summary>
public class SpecialistTests
{
    private const long Now = 1_000_000_000L;

    private readonly List<Object> _created = new List<Object>();

    private SettlementOtterDefinition _first;
    private SettlementOtterDefinition _miner;
    private DevelopableRegionDefinition _mine;

    [SetUp]
    public void SetUp()
    {
        _mine = Create<DevelopableRegionDefinition>();
        var so = new SerializedObject(_mine);
        so.FindProperty("_regionId").stringValue = "region_mine";
        so.FindProperty("_discoverDevelopment").stringValue = "chair";
        so.FindProperty("_playerClearDevelopment").stringValue = "mine_path_open";
        so.FindProperty("_operationalDevelopment").stringValue = "mine_cleared";
        so.FindProperty("_productionDevelopment").stringValue = "mine_working";
        so.ApplyModifiedPropertiesWithoutUndo();

        _first = Otter("otter_first", null);
        _miner = Otter("otter_miner", _mine);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    #region 만들기

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    private SettlementOtterDefinition Otter(string id, DevelopableRegionDefinition workRegion)
    {
        var otter = Create<SettlementOtterDefinition>();
        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_workRegion").objectReferenceValue = workRegion;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    private ConstructionDefinition Construction(string id, ConstructionTarget target, bool builder)
    {
        var construction = Create<ConstructionDefinition>();
        var so = new SerializedObject(construction);
        so.FindProperty("_constructionId").stringValue = id;
        so.FindProperty("_targetType").enumValueIndex = (int)target;
        so.FindProperty("_needsBuilder").boolValue = builder;
        so.ApplyModifiedPropertiesWithoutUndo();
        return construction;
    }

    private QuestDefinition Quest(QuestGoalType type, int goal, ConstructionDefinition[] only = null, bool builderOnly = false)
    {
        var quest = Create<QuestDefinition>();
        var so = new SerializedObject(quest);
        so.FindProperty("_questId").stringValue = "q_" + type;
        so.FindProperty("_goalType").enumValueIndex = (int)type;
        so.FindProperty("_goal").intValue = goal;
        so.FindProperty("_builderOnly").boolValue = builderOnly;
        if (only != null)
        {
            var list = so.FindProperty("_constructions");
            list.arraySize = only.Length;
            for (int i = 0; i < only.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = only[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return quest;
    }

    // 광산이 운영되고 광부가 찾아온 상태 (광장에는 아직 안 나타남)
    private static Settlement MineOperatedMinerArrived(SettlementOtterDefinition miner)
    {
        var settlement = new Settlement();
        settlement.SetResident("otter_first", ResidentState.Resident);
        settlement.UnlockDevelopment("chair");
        settlement.UnlockDevelopment("mine_path_open");
        settlement.UnlockDevelopment("mine_cleared");
        SettlementRules.Arrive(miner, ResidentState.SpecialNpc, settlement);
        return settlement;
    }

    #endregion

    #region 방문 → 만남 → 배치 → 일함

    [Test]
    public void Arrived_VisitsPlaza_ButCannotBeAssignedBeforeMeeting()
    {
        var settlement = MineOperatedMinerArrived(_miner);

        Assert.IsTrue(SettlementRegionRules.IsVisitingPlaza(_miner, settlement));
        Assert.AreEqual(SpecialistState.NotArrived, settlement.GetSpecialistState("otter_miner"), "찾아오기만 해서는 광장에 와 있는 것이 아님");
        Assert.IsFalse(settlement.HasMet("otter_miner"));
        Assert.IsFalse(SettlementRegionRules.Assign(_miner, settlement));
    }

    [Test]
    public void Meet_RecordsOnce_AndPutsSpecialistAtPlaza()
    {
        var settlement = MineOperatedMinerArrived(_miner);

        Assert.IsTrue(SettlementRegionRules.Meet(_miner, settlement));
        Assert.IsFalse(SettlementRegionRules.Meet(_miner, settlement), "같은 해달은 한 번만");
        Assert.AreEqual(SpecialistState.AtPlaza, settlement.GetSpecialistState("otter_miner"));
        Assert.AreEqual(1, settlement.MetOtters.Count);
    }

    [Test]
    public void Assign_Once_MakesResident_ButNoProductionBeforeGuide()
    {
        var settlement = MineOperatedMinerArrived(_miner);
        SettlementRegionRules.Meet(_miner, settlement);

        Assert.IsTrue(SettlementRegionRules.Assign(_miner, settlement));
        Assert.IsFalse(SettlementRegionRules.Assign(_miner, settlement), "연타해도 한 번만");
        Assert.AreEqual(SpecialistState.Assigned, settlement.GetSpecialistState("otter_miner"));
        Assert.IsTrue(settlement.TryGetResidentState("otter_miner", out var state) && state == ResidentState.Resident, "주민 수에 들어감");
        Assert.IsFalse(SettlementRegionRules.IsVisitingPlaza(_miner, settlement), "배치하면 광장을 떠남");
        Assert.IsFalse(SettlementRegionRules.CanProduce(_mine, _miner, settlement), "생산 안내를 끝내기 전에는 생산 없음");
    }

    [Test]
    public void StartWork_AfterAssign_AllowsProductionAndOpensProductionDevelopment()
    {
        var settlement = MineOperatedMinerArrived(_miner);
        SettlementRegionRules.Meet(_miner, settlement);
        Assert.IsFalse(SettlementRegionRules.StartWork(_miner, settlement, Now), "배치 전에는 일할 수 없음");
        SettlementRegionRules.Assign(_miner, settlement);

        Assert.IsTrue(SettlementRegionRules.StartWork(_miner, settlement, Now));
        Assert.IsFalse(SettlementRegionRules.StartWork(_miner, settlement, Now + 1), "한 번만");
        Assert.IsTrue(SettlementRegionRules.CanProduce(_mine, _miner, settlement));
        Assert.IsTrue(settlement.HasDevelopment("mine_working"));
        Assert.IsTrue(settlement.TryGetSpecialist("otter_miner", out var record));
        Assert.AreEqual(Now, record.WorkingSinceUtcTicks);
        Assert.AreEqual("region_mine", record.RegionId);
    }

    [Test]
    public void CannotAssign_BeforeRegionOperates()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment("chair");
        SettlementRules.Arrive(_miner, ResidentState.SpecialNpc, settlement);
        SettlementRegionRules.Meet(_miner, settlement);

        Assert.IsFalse(SettlementRegionRules.CanAssign(_miner, settlement));
        Assert.IsFalse(SettlementRegionRules.CanProduce(_mine, _miner, settlement));
    }

    [Test]
    public void Specialist_NeverSentToOtherTasks()
    {
        var settlement = MineOperatedMinerArrived(_miner);
        SettlementRegionRules.Meet(_miner, settlement);
        SettlementRegionRules.Assign(_miner, settlement);

        Assert.IsFalse(SettlementRegionRules.CanWork(_miner, settlement), "광산에 배치된 광부는 다른 작업에 못 감");
        Assert.IsTrue(SettlementRegionRules.CanWork(_first, settlement));
    }

    [Test]
    public void WaitingForSpecialist_OnlyWhileOperationalAndNotAssigned()
    {
        var settlement = MineOperatedMinerArrived(_miner);
        Assert.IsTrue(SettlementRegionRules.IsWaitingForSpecialist(_mine, _miner, settlement));

        SettlementRegionRules.Meet(_miner, settlement);
        SettlementRegionRules.Assign(_miner, settlement);
        Assert.IsFalse(SettlementRegionRules.IsWaitingForSpecialist(_mine, _miner, settlement));
    }

    [Test]
    public void AdvanceSpecialist_NeverGoesBack()
    {
        var settlement = new Settlement();
        settlement.AdvanceSpecialist("otter_miner", SpecialistState.Working, "region_mine", Now);

        Assert.IsFalse(settlement.AdvanceSpecialist("otter_miner", SpecialistState.AtPlaza));
        Assert.AreEqual(SpecialistState.Working, settlement.GetSpecialistState("otter_miner"));
    }

    #endregion

    #region 세이브

    [Test]
    public void Save_KeepsSpecialistsAndMetOtters()
    {
        var settlement = MineOperatedMinerArrived(_miner);
        SettlementRegionRules.Meet(_miner, settlement);
        SettlementRegionRules.Assign(_miner, settlement);
        settlement.MarkMet("otter_first");

        var saved = new SettlementSaveData();
        settlement.Write(saved);
        var loaded = new Settlement();
        loaded.Load(saved);

        Assert.AreEqual(SpecialistState.Assigned, loaded.GetSpecialistState("otter_miner"));
        Assert.IsTrue(loaded.TryGetSpecialist("otter_miner", out var record) && record.RegionId == "region_mine");
        Assert.IsTrue(loaded.HasMet("otter_miner"));
        Assert.IsTrue(loaded.HasMet("otter_first"));
    }

    [Test]
    public void Load_DuplicateSpecialistLines_KeepsFurthest()
    {
        var saved = new SettlementSaveData();
        saved.specialists.Add(new SpecialistSaveData { otterId = "otter_miner", state = SpecialistState.Working, regionId = "region_mine" });
        saved.specialists.Add(new SpecialistSaveData { otterId = "otter_miner", state = SpecialistState.AtPlaza });
        var settlement = new Settlement();
        settlement.Load(saved);

        Assert.AreEqual(SpecialistState.Working, settlement.GetSpecialistState("otter_miner"));
    }

    [Test]
    public void OldSave_WithoutSpecialists_LoadsEmpty()
    {
        var saved = new SettlementSaveData { specialists = null, metOtters = null };
        var settlement = new Settlement();
        settlement.Load(saved);

        Assert.AreEqual(SpecialistState.NotArrived, settlement.GetSpecialistState("otter_miner"));
        Assert.AreEqual(0, settlement.MetOtters.Count);
    }

    [Test]
    public void Legacy_OperatingRegionWithoutSpecialist_StartsWorking()
    {
        // 전문 해달이 생기기 전 세이브: 광산은 운영 중인데 광부가 없음 → 하던 채굴이 멈추지 않게 바로 일하는 중
        var settlement = new Settlement();
        settlement.UnlockDevelopment("chair");
        settlement.UnlockDevelopment("mine_path_open");
        settlement.UnlockDevelopment("mine_cleared");

        Assert.IsTrue(SettlementRegionRules.MigrateLegacy(_miner, false, settlement, Now));
        Assert.AreEqual(SpecialistState.Working, settlement.GetSpecialistState("otter_miner"));
        Assert.IsTrue(SettlementRegionRules.CanProduce(_mine, _miner, settlement));
        Assert.IsTrue(settlement.HasDevelopment("mine_working"));
        Assert.IsFalse(SettlementRegionRules.MigrateLegacy(_miner, false, settlement, Now), "한 번만");
    }

    [Test]
    public void Legacy_DoesNotSkipNewFlowVisit()
    {
        // 새 흐름: 광부가 찾아왔고 아직 만나지 않음 → 그대로 (광장에서 만나 배치해야 함)
        var settlement = MineOperatedMinerArrived(_miner);

        Assert.IsFalse(SettlementRegionRules.MigrateLegacy(_miner, false, settlement, Now));
        Assert.AreEqual(SpecialistState.NotArrived, settlement.GetSpecialistState("otter_miner"));
    }

    [Test]
    public void Legacy_AssignRequestAlreadyDone_StartsWorking()
    {
        // 정착 진행 전 세이브는 모든 부탁을 끝낸 것으로 불러옴 (광부도 찾아온 상태) → 배치 부탁이 끝나 있으면 일하는 중
        var settlement = MineOperatedMinerArrived(_miner);

        Assert.IsTrue(SettlementRegionRules.MigrateLegacy(_miner, true, settlement, Now));
        Assert.AreEqual(SpecialistState.Working, settlement.GetSpecialistState("otter_miner"));
    }

    [Test]
    public void Legacy_RegionNotOperating_NothingMoves()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment("chair");

        Assert.IsFalse(SettlementRegionRules.MigrateLegacy(_miner, false, settlement, Now));
    }

    #endregion

    #region 기록으로 세는 퀘스트

    [Test]
    public void CountBuildings_SkipsClearing_AndFilters()
    {
        var house1 = Construction("con_house_1", ConstructionTarget.House, false);
        var chair = Construction("con_chair", ConstructionTarget.House, false);
        var house2 = Construction("con_house_2", ConstructionTarget.House, true);
        var minePath = Construction("con_mine_path", ConstructionTarget.Clearing, false);
        var built = new[] { house1, chair, minePath, house2 };

        Assert.AreEqual(3, QuestProgressRules.CountBuildings(Quest(QuestGoalType.CompleteConstruction, 2), built), "개간은 건설이 아님");
        Assert.AreEqual(2, QuestProgressRules.CountBuildings(Quest(QuestGoalType.CompleteConstruction, 1, new[] { house1, house2 }), built));
        Assert.AreEqual(1, QuestProgressRules.CountBuildings(Quest(QuestGoalType.CompleteConstruction, 1, null, true), built), "건설 해달이 참여한 공사만");
        Assert.AreEqual(0, QuestProgressRules.CountBuildings(Quest(QuestGoalType.Harvest, 1), built));
    }

    [Test]
    public void SetProgressAtLeast_RecountDoesNotDoubleOrShrink()
    {
        var quest = Quest(QuestGoalType.MeetOtter, 2);
        var log = new QuestLog();

        Assert.IsTrue(log.SetProgressAtLeast(quest, 1));
        Assert.IsFalse(log.SetProgressAtLeast(quest, 1), "같은 기록을 다시 세도 더해지지 않음");
        Assert.IsFalse(log.SetProgressAtLeast(quest, 0), "줄지 않음");
        Assert.AreEqual(1, log.GetProgress(quest));

        log.SetProgressAtLeast(quest, 5);
        Assert.AreEqual(2, log.GetProgress(quest), "목표에서 멈춤");
        Assert.IsTrue(log.TryClaim(quest));
        Assert.IsFalse(log.SetProgressAtLeast(quest, 2), "받은 퀘스트는 그대로");
        Assert.AreEqual(QuestStatus.Claimed, log.GetStatus(quest));
    }

    [Test]
    public void HarvestQuest_WithItem_CountsOnlyThatItem()
    {
        var carrot = Create<ItemDefinition>();
        var potato = Create<ItemDefinition>();
        var quest = Quest(QuestGoalType.Harvest, 3);
        var so = new SerializedObject(quest);
        so.FindProperty("_item").objectReferenceValue = carrot;
        so.ApplyModifiedPropertiesWithoutUndo();

        Assert.AreEqual(2, QuestProgressRules.From(quest, new ItemChangedEvent(carrot, 0, 2, ItemChangeReason.Harvest)));
        Assert.AreEqual(0, QuestProgressRules.From(quest, new ItemChangedEvent(potato, 0, 5, ItemChangeReason.Harvest)));
    }

    #endregion
}
