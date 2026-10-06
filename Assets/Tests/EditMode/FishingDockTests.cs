using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 낚시터 (Lv.15): 레벨로 열리는 발전 → 선착장 부탁 → 낚시꾼 배치 → 일해야 낚시, 옛 세이브 맞추기.
/// 개발 메뉴 Fast Timers: 진행 중인 건설·주민 작업 줄이기.
/// </summary>
public class FishingDockTests
{
    private const long Now = 1_000_000_000_000L;
    private static readonly long Second = TimeSpan.TicksPerSecond;

    private readonly List<Object> _created = new List<Object>();

    private SettlementConfig _config;
    private DevelopableRegionDefinition _fishing;
    private SettlementOtterDefinition _fisher;
    private BoardRequestDefinition _dock;
    private BoardRequestDefinition _assign;
    // 개발 메뉴 Fast Timers를 켜 둔 에디터에서도 테스트가 같은 결과를 내고, 끝나면 그 설정을 되돌림
    private bool _fastBefore;

    [SetUp]
    public void SetUp()
    {
        _fastBefore = DevTimers.Fast;
        DevTimers.Fast = false;
        _fishing = Create<DevelopableRegionDefinition>();
        var regionSo = new SerializedObject(_fishing);
        regionSo.FindProperty("_regionId").stringValue = "region_fishing";
        regionSo.FindProperty("_discoverDevelopment").stringValue = "fishing_found";
        regionSo.FindProperty("_operationalDevelopment").stringValue = "fishing_dock";
        regionSo.FindProperty("_productionDevelopment").stringValue = "fishing_working";
        // 낚시터 장소: 선착장(fishing_dock)이 있어야 갈 수 있음 (실제 Zone_Fishing과 같음)
        var zone = Create<ZoneDefinition>();
        var zoneSo = new SerializedObject(zone);
        zoneSo.FindProperty("_requiredDevelopment").stringValue = "fishing_dock";
        zoneSo.ApplyModifiedPropertiesWithoutUndo();
        regionSo.FindProperty("_zone").objectReferenceValue = zone;
        regionSo.ApplyModifiedPropertiesWithoutUndo();

        _fisher = Create<SettlementOtterDefinition>();
        var otterSo = new SerializedObject(_fisher);
        otterSo.FindProperty("_otterId").stringValue = "otter_fisher";
        otterSo.FindProperty("_workRegion").objectReferenceValue = _fishing;
        otterSo.ApplyModifiedPropertiesWithoutUndo();

        var construction = Create<ConstructionDefinition>();
        var conSo = new SerializedObject(construction);
        conSo.FindProperty("_constructionId").stringValue = "con_fishing_dock";
        conSo.FindProperty("_durationSeconds").floatValue = 180f;
        conSo.FindProperty("_needsBuilder").boolValue = true;
        conSo.FindProperty("_unlockResultId").stringValue = "fishing_dock";
        conSo.ApplyModifiedPropertiesWithoutUndo();

        _dock = Request("req_fishing_dock", 12, "fishing_found", construction, null);
        var dockSo = new SerializedObject(_dock);
        var arrivals = dockSo.FindProperty("_arrivals");
        arrivals.arraySize = 1;
        arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("_otter").objectReferenceValue = _fisher;
        arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("_state").enumValueIndex = (int)ResidentState.SpecialNpc;
        dockSo.ApplyModifiedPropertiesWithoutUndo();
        _assign = Request("req_assign_fisher", 13, "fishing_dock", null, _fisher);

        _config = Create<SettlementConfig>();
        var so = new SerializedObject(_config);
        SetList(so.FindProperty("_requests"), _dock, _assign);
        SetList(so.FindProperty("_otters"), _fisher);
        SetList(so.FindProperty("_regions"), _fishing);
        var legacy = so.FindProperty("_legacyDevelopments");
        legacy.arraySize = 1;
        legacy.GetArrayElementAtIndex(0).stringValue = "fishing_dock";
        so.ApplyModifiedPropertiesWithoutUndo();
        _config.SetupLevelDevelopments(new[] { new LevelDevelopment(15, "fishing_found", "낚시터 발견") });
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
        DevTimers.Fast = _fastBefore;
    }

    #region 만들기

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    private BoardRequestDefinition Request(string id, int order, string requires, ConstructionDefinition construction,
        SettlementOtterDefinition assign)
    {
        var request = Create<BoardRequestDefinition>();
        var so = new SerializedObject(request);
        so.FindProperty("_requestId").stringValue = id;
        so.FindProperty("_order").intValue = order;
        so.FindProperty("_requiredDevelopment").stringValue = requires;
        so.FindProperty("_construction").objectReferenceValue = construction;
        so.FindProperty("_assignSpecialist").objectReferenceValue = assign;
        so.FindProperty("_stageOnComplete").intValue = -1;
        so.ApplyModifiedPropertiesWithoutUndo();
        return request;
    }

    private static void SetList(SerializedProperty list, params Object[] items)
    {
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    #endregion

    #region 레벨로 열리는 발전 · 선착장 부탁

    [Test]
    public void LevelDevelopment_OpensOnlyAtLevel_Once()
    {
        var settlement = new Settlement();
        var opened = new List<LevelDevelopment>();

        SettlementRules.UnlockLevelDevelopments(_config, settlement, 14, opened);
        Assert.AreEqual(0, opened.Count);
        Assert.AreEqual(RequestStatus.Locked, SettlementRules.GetStatus(_dock, settlement), "Lv.14에는 선착장 부탁이 안 보임");

        SettlementRules.UnlockLevelDevelopments(_config, settlement, 15, opened);
        Assert.AreEqual(1, opened.Count);
        Assert.AreEqual("낚시터 발견", opened[0].Notice);
        Assert.AreEqual(RequestStatus.Available, SettlementRules.GetStatus(_dock, settlement), "Lv.15에 선착장 부탁");

        SettlementRules.UnlockLevelDevelopments(_config, settlement, 16, opened);
        Assert.AreEqual(0, opened.Count, "이미 연 발전은 다시 알리지 않음");
    }

    [Test]
    public void FindLevelLocked_NamesTheLevel_UntilReached()
    {
        var settlement = new Settlement();

        Assert.AreSame(_dock, SettlementRules.FindLevelLocked(_config, settlement, out int level));
        Assert.AreEqual(15, level);

        settlement.UnlockDevelopment("fishing_found");
        Assert.IsNull(SettlementRules.FindLevelLocked(_config, settlement, out _), "레벨이 되면 잠긴 부탁이 아님");
    }

    [Test]
    public void DockDone_FisherVisits_FishingOnlyAfterAssignAndGuide()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment("fishing_found");
        Assert.AreEqual(RegionProgressState.Discovered, SettlementRegionRules.GetRegionState(_fishing, settlement), "발견했지만 선착장 전에는 갈 수 없음");

        Assert.IsTrue(SettlementRules.ApplyCompletion(_dock, settlement));
        Assert.IsTrue(settlement.HasDevelopment("fishing_dock"), "선착장을 고치면 낚시터가 열림");
        Assert.AreEqual(RegionProgressState.Operational, SettlementRegionRules.GetRegionState(_fishing, settlement));
        Assert.IsTrue(SettlementRegionRules.IsVisitingPlaza(_fisher, settlement), "낚시꾼이 광장에 찾아옴");
        Assert.IsFalse(SettlementRegionRules.CanProduce(_fishing, _fisher, settlement), "배치 전에는 낚시 없음");

        SettlementRegionRules.Meet(_fisher, settlement);
        Assert.IsTrue(SettlementRegionRules.Assign(_fisher, settlement));
        Assert.IsFalse(SettlementRegionRules.CanProduce(_fishing, _fisher, settlement), "낚시터 안내를 끝내기 전에는 낚시 없음");

        Assert.IsTrue(SettlementRegionRules.StartWork(_fisher, settlement, Now));
        Assert.IsTrue(SettlementRegionRules.CanProduce(_fishing, _fisher, settlement));
        Assert.IsTrue(settlement.HasDevelopment(SettlementQuestGate.FishingProductionDevelopment), "낚시 퀘스트가 보임");
    }

    #endregion

    #region 옛 세이브

    [Test]
    public void LegacySave_AlreadyHadDock_DockRequestCompletedQuietly()
    {
        // 정착 진행 전 세이브는 fishing_dock만 따로 받았고 부탁 기록은 없음
        var settlement = new Settlement();
        settlement.UnlockDevelopment("fishing_dock");
        var completed = new List<BoardRequestDefinition>();

        SettlementMigration.ReconcileLegacyConstructions(_config, settlement, completed);

        Assert.AreEqual(1, completed.Count);
        Assert.AreSame(_dock, completed[0]);
        Assert.IsTrue(settlement.IsCompleted("req_fishing_dock"), "이미 쓰던 낚시터의 선착장을 다시 짓지 않음");
        SettlementMigration.ReconcileLegacyConstructions(_config, settlement, completed);
        Assert.AreEqual(0, completed.Count, "한 번만");
    }

    [Test]
    public void NewSave_WithoutDock_NothingReconciled()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment("fishing_found");
        var completed = new List<BoardRequestDefinition>();

        SettlementMigration.ReconcileLegacyConstructions(_config, settlement, completed);

        Assert.AreEqual(0, completed.Count);
        Assert.IsFalse(settlement.IsCompleted("req_fishing_dock"));
    }

    [Test]
    public void LegacySave_FisherWorksRightAway()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment("fishing_dock");

        Assert.IsTrue(SettlementRegionRules.MigrateLegacy(_fisher, false, settlement, Now));
        Assert.IsTrue(SettlementRegionRules.CanProduce(_fishing, _fisher, settlement), "옛 세이브는 하던 낚시가 멈추지 않음");
    }

    #endregion

    #region Fast Timers

    [Test]
    public void DevTimers_ShortenNewDurationsOnlyWhenOn()
    {
        Assert.AreEqual(180d, DevTimers.Duration(180d));
        DevTimers.Fast = true;
        Assert.AreEqual(DevTimers.FastSeconds, DevTimers.Duration(180d));
        Assert.AreEqual(3d, DevTimers.Duration(3d), "원래 짧은 것은 그대로");
    }

    [Test]
    public void ShortenTimers_RunningJobAndTasks_EndWithinLimit_KeepProgress()
    {
        var settlement = new Settlement();
        settlement.SetResident("otter_a", ResidentState.Resident);
        // 건설: 100초 중 25초 지남, 작업: 600초 중 300초 지남
        settlement.StartJob("req_house", "con_house", Now - 25 * Second, Now + 75 * Second);
        settlement.StartTask("task_clear", new[] { "otter_a" }, Now - 300 * Second, Now + 300 * Second);

        Assert.IsTrue(settlement.ShortenTimers(Now, TimeSpan.FromSeconds(5)));

        Assert.AreEqual(Now + (long)(0.75 * 5 * Second), settlement.Job.EndUtcTicks);
        Assert.AreEqual(0.25f, settlement.Job.Progress(Now), 0.001f, "막대는 뒤로 가지 않음");
        Assert.IsTrue(settlement.TryGetTaskJob("task_clear", out var task));
        Assert.AreEqual(Now + (long)(0.5 * 5 * Second), task.EndUtcTicks);
        Assert.AreEqual(0.5f, task.Progress(Now), 0.001f);
        Assert.IsTrue(task.HasOtter("otter_a"), "보낸 해달은 그대로");
        Assert.IsTrue(settlement.Job.IsDue(Now + 5 * Second));
        Assert.IsTrue(task.IsDue(Now + 5 * Second));
    }

    [Test]
    public void ShortenTimers_WaitingJob_ShortensDurationOnly_AndLeavesShortOnes()
    {
        var settlement = new Settlement();
        settlement.StartJob("req_house", "con_house", Now, Now + 120 * Second, waitingForWorker: true);

        Assert.IsTrue(settlement.ShortenTimers(Now, TimeSpan.FromSeconds(5)));
        Assert.IsTrue(settlement.Job.WaitingForWorker, "일꾼을 기다리는 건 그대로");
        Assert.AreEqual(TimeSpan.FromSeconds(5), settlement.Job.Duration);

        Assert.IsFalse(settlement.ShortenTimers(Now, TimeSpan.FromSeconds(5)), "이미 짧으면 바꾸지 않음");
    }

    #endregion
}
