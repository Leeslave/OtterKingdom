using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>P1 개간 지역 · 주민 작업: 지역 단계, 보낼 수 있는 해달, 작업 시작·끝, 세이브</summary>
public class SettlementTaskTests
{
    private const long Second = System.TimeSpan.TicksPerSecond;

    private readonly List<Object> _created = new List<Object>();

    private SettlementOtterDefinition _first;
    private SettlementOtterDefinition _painter;
    private SettlementOtterDefinition _builder;
    private SettlementTaskDefinition _tidy;
    private DevelopableRegionDefinition _region;
    private ZoneDefinition _zone;

    [SetUp]
    public void SetUp()
    {
        _first = Otter("otter_first", false);
        _painter = Otter("otter_painter", false);
        _builder = Otter("otter_builder", true);
        _tidy = Task("task_mine_tidy", 1, 30f, "mine_path_open", "mine_tidy");

        _zone = Create<ZoneDefinition>();
        var zoneSo = new SerializedObject(_zone);
        zoneSo.FindProperty("_requiredDevelopment").stringValue = "chair";
        zoneSo.ApplyModifiedPropertiesWithoutUndo();

        _region = Create<DevelopableRegionDefinition>();
        var so = new SerializedObject(_region);
        so.FindProperty("_regionId").stringValue = "region_mine";
        so.FindProperty("_zone").objectReferenceValue = _zone;
        so.FindProperty("_discoverDevelopment").stringValue = "chair";
        so.FindProperty("_playerClearDevelopment").stringValue = "mine_path_open";
        so.FindProperty("_operationalDevelopment").stringValue = "mine_cleared";
        var tasks = so.FindProperty("_preparationTasks");
        tasks.arraySize = 1;
        tasks.GetArrayElementAtIndex(0).objectReferenceValue = _tidy;
        so.ApplyModifiedPropertiesWithoutUndo();
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

    private SettlementOtterDefinition Otter(string id, bool builder)
    {
        var otter = Create<SettlementOtterDefinition>();
        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_isBuilder").boolValue = builder;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    private SettlementTaskDefinition Task(string id, int workers, float seconds, string requires, string result)
    {
        var task = Create<SettlementTaskDefinition>();
        var so = new SerializedObject(task);
        so.FindProperty("_taskId").stringValue = id;
        so.FindProperty("_requiredWorkers").intValue = workers;
        so.FindProperty("_durationSeconds").floatValue = seconds;
        so.FindProperty("_requiredDevelopment").stringValue = requires;
        so.FindProperty("_resultDevelopment").stringValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();
        return task;
    }

    private static Settlement WithFirstResident()
    {
        var settlement = new Settlement();
        settlement.SetResident("otter_first", ResidentState.Resident);
        return settlement;
    }

    #endregion

    [Test]
    public void Region_GoesThroughEveryStage()
    {
        var settlement = WithFirstResident();
        Assert.AreEqual(RegionProgressState.Locked, SettlementRegionRules.GetRegionState(_region, settlement));

        settlement.UnlockDevelopment("chair");
        Assert.AreEqual(RegionProgressState.PlayerClearing, SettlementRegionRules.GetRegionState(_region, settlement));
        Assert.AreEqual(SettlementTaskState.Locked, SettlementRegionRules.GetTaskState(_tidy, settlement), "길을 치우기 전에는 정비 작업이 잠김");

        settlement.UnlockDevelopment("mine_path_open");
        Assert.AreEqual(RegionProgressState.AwaitingWorkers, SettlementRegionRules.GetRegionState(_region, settlement));
        Assert.AreEqual(_tidy, SettlementRegionRules.CurrentPreparation(_region, settlement));

        settlement.StartTask("task_mine_tidy", new[] { "otter_first" }, 0, 30 * Second);
        Assert.AreEqual(RegionProgressState.WorkerPreparing, SettlementRegionRules.GetRegionState(_region, settlement));

        settlement.FinishTask("task_mine_tidy", "mine_tidy");
        Assert.IsTrue(SettlementRegionRules.ArePreparationsDone(_region, settlement));
        Assert.AreEqual(RegionProgressState.AwaitingWorkers, SettlementRegionRules.GetRegionState(_region, settlement),
            "운영 발전이 열리기 전까지는 아직 운영 아님 (SettlementManager가 부탁을 끝내 엶)");

        settlement.UnlockDevelopment("mine_cleared");
        Assert.AreEqual(RegionProgressState.Operational, SettlementRegionRules.GetRegionState(_region, settlement));
    }

    [Test]
    public void Region_DiscoveredButUnreachable_IsDiscovered()
    {
        var zoneSo = new SerializedObject(_zone);
        zoneSo.FindProperty("_requiredDevelopment").stringValue = "bridge";
        zoneSo.ApplyModifiedPropertiesWithoutUndo();
        var settlement = WithFirstResident();
        settlement.UnlockDevelopment("chair");

        Assert.AreEqual(RegionProgressState.Discovered, SettlementRegionRules.GetRegionState(_region, settlement));
    }

    [Test]
    public void CanWork_OnlyIdleResidents_NotBuilder()
    {
        var settlement = WithFirstResident();
        settlement.SetResident("otter_painter", ResidentState.SettlementCandidate);
        settlement.SetResident("otter_builder", ResidentState.Resident);

        Assert.IsTrue(SettlementRegionRules.CanWork(_first, settlement));
        Assert.IsFalse(SettlementRegionRules.CanWork(_painter, settlement), "정착 후보는 작업 불가");
        Assert.IsFalse(SettlementRegionRules.CanWork(_builder, settlement), "건설 해달은 주민 작업에 보내지 않음");

        settlement.SetResident("otter_painter", ResidentState.Visitor);
        Assert.IsFalse(SettlementRegionRules.CanWork(_painter, settlement), "방문객은 작업 불가");

        settlement.StartTask("task_a", new[] { "otter_first" }, 0, Second);
        Assert.AreEqual(ResidentWorkState.Working, settlement.GetWorkState("otter_first"));
        Assert.IsFalse(SettlementRegionRules.CanWork(_first, settlement), "이미 작업 중인 해달은 다시 보낼 수 없음");
    }

    [Test]
    public void StartTask_SameOtterTwice_Throws()
    {
        var settlement = WithFirstResident();
        settlement.StartTask("task_a", new[] { "otter_first" }, 0, Second);

        Assert.Throws<System.InvalidOperationException>(() => settlement.StartTask("task_b", new[] { "otter_first" }, 0, Second));
        Assert.Throws<System.InvalidOperationException>(() => settlement.StartTask("task_a", new[] { "otter_painter" }, 0, Second));
    }

    [Test]
    public void FinishTask_FreesOttersAndOpensResult_Once()
    {
        var settlement = WithFirstResident();
        var opened = new List<string>();
        settlement.OnDevelopmentUnlocked += opened.Add;
        settlement.StartTask("task_mine_tidy", new[] { "otter_first" }, 0, 30 * Second);

        Assert.IsFalse(settlement.TryGetTaskJob("task_mine_tidy", out var job) && job.IsDue(29 * Second));
        Assert.IsTrue(job.IsDue(30 * Second));
        Assert.AreEqual(0.5f, job.Progress(15 * Second), 0.001f);

        Assert.IsNotNull(settlement.FinishTask("task_mine_tidy", "mine_tidy"));
        Assert.IsNull(settlement.FinishTask("task_mine_tidy", "mine_tidy"), "두 번 끝나지 않음");
        Assert.AreEqual(ResidentWorkState.Idle, settlement.GetWorkState("otter_first"));
        Assert.IsTrue(settlement.IsTaskCompleted("task_mine_tidy"));
        CollectionAssert.AreEqual(new[] { "mine_tidy" }, opened);
        Assert.Throws<System.InvalidOperationException>(() => settlement.StartTask("task_mine_tidy", new[] { "otter_first" }, 0, Second),
            "끝낸 작업은 다시 시작하지 않음");
    }

    [Test]
    public void Save_KeepsRunningAndCompletedTasks()
    {
        var settlement = WithFirstResident();
        settlement.SetResident("otter_painter", ResidentState.Resident);
        settlement.StartTask("task_mine_tidy", new[] { "otter_first", "otter_painter" }, 10 * Second, 40 * Second);
        settlement.StartTask("task_done", new[] { "otter_painter_2" }, 0, Second);
        settlement.FinishTask("task_done", null);

        var saved = new SettlementSaveData();
        settlement.Write(saved);
        var loaded = new Settlement();
        loaded.Load(saved);

        Assert.IsTrue(loaded.TryGetTaskJob("task_mine_tidy", out var job));
        CollectionAssert.AreEqual(new[] { "otter_first", "otter_painter" }, job.OtterIds);
        Assert.AreEqual(10 * Second, job.StartUtcTicks);
        Assert.AreEqual(40 * Second, job.EndUtcTicks);
        Assert.AreEqual(ResidentWorkState.Working, loaded.GetWorkState("otter_painter"));
        Assert.IsTrue(loaded.IsTaskCompleted("task_done"));
    }

    [Test]
    public void Load_DropsBrokenTaskLines()
    {
        var saved = new SettlementSaveData();
        saved.tasks.Add(new SettlementTaskSaveData { taskId = "task_a", endUtcTicks = Second, assignedOtterIds = new List<string> { "otter_first" } });
        // 같은 해달이 두 작업에 / 해달 없음 / 이미 끝낸 작업
        saved.tasks.Add(new SettlementTaskSaveData { taskId = "task_b", endUtcTicks = Second, assignedOtterIds = new List<string> { "otter_first" } });
        saved.tasks.Add(new SettlementTaskSaveData { taskId = "task_c", endUtcTicks = Second });
        saved.tasks.Add(new SettlementTaskSaveData { taskId = "task_d", endUtcTicks = Second, assignedOtterIds = new List<string> { "otter_x" } });
        saved.completedTasks.Add("task_d");

        var settlement = new Settlement();
        settlement.Load(saved);

        Assert.IsTrue(settlement.TryGetTaskJob("task_a", out _));
        Assert.IsFalse(settlement.TryGetTaskJob("task_b", out _));
        Assert.IsFalse(settlement.TryGetTaskJob("task_c", out _));
        Assert.IsFalse(settlement.TryGetTaskJob("task_d", out _));
        Assert.AreEqual(ResidentWorkState.Idle, settlement.GetWorkState("otter_x"));
    }

    [Test]
    public void OldSave_WithoutTasks_LoadsEmpty()
    {
        var saved = JsonUtility.FromJson<SettlementSaveData>("{\"initialized\":true,\"kingdomStage\":1}");
        var settlement = new Settlement();
        settlement.Load(saved);

        Assert.AreEqual(0, settlement.TaskJobs.Count);
        Assert.IsFalse(settlement.IsTaskCompleted("task_mine_tidy"));
    }
}
