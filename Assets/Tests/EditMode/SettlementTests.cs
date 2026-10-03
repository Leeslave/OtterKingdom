using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class SettlementTests
{
    private readonly List<Object> _created = new List<Object>();

    private SettlementOtterDefinition _first;
    private SettlementOtterDefinition _candidate;
    private SettlementOtterDefinition _builder;
    private BoardRequestDefinition _house1;
    private BoardRequestDefinition _house2;
    private BoardRequestDefinition _farmland;
    private SettlementConfig _config;

    [SetUp]
    public void SetUp()
    {
        var arrivalEntry = Entry("gb_first");
        _first = Otter("otter_first", false, arrivalEntry);
        _candidate = Otter("otter_candidate", false, Entry("gb_candidate"));
        _builder = Otter("otter_builder", true, Entry("gb_builder"));

        _house1 = Request("req_house_1", 0, "", 0, Construction("con_house_1", "house_1", 0f, false),
            settles: new[] { _first },
            arrivals: new[] { (_candidate, ResidentState.SettlementCandidate), (_builder, ResidentState.SpecialNpc) },
            stage: 1, completionEntry: Entry("gb_settle"));
        _house2 = Request("req_house_2", 1, "house_1", 0, Construction("con_house_2", "house_2", 30f, true),
            settles: new[] { _candidate, _builder }, stage: 2);
        _farmland = Request("req_farmland", 2, "house_2", 3, Construction("con_farmland", "farmland", 45f, true), stage: 3);

        _config = Create<SettlementConfig>();
        var so = new SerializedObject(_config);
        so.FindProperty("_firstOtter").objectReferenceValue = _first;
        SetList(so.FindProperty("_otters"), _first, _candidate, _builder);
        SetList(so.FindProperty("_requests"), _house1, _house2, _farmland);
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

    private static void SetList(SerializedProperty list, params Object[] values)
    {
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private GuestbookEntryDefinition Entry(string id)
    {
        var entry = Create<GuestbookEntryDefinition>();
        var so = new SerializedObject(entry);
        so.FindProperty("_entryId").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        return entry;
    }

    private SettlementOtterDefinition Otter(string id, bool builder, GuestbookEntryDefinition arrival)
    {
        var otter = Create<SettlementOtterDefinition>();
        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_isBuilder").boolValue = builder;
        so.FindProperty("_arrivalEntry").objectReferenceValue = arrival;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    private ConstructionDefinition Construction(string id, string result, float seconds, bool needsBuilder)
    {
        var construction = Create<ConstructionDefinition>();
        var so = new SerializedObject(construction);
        so.FindProperty("_constructionId").stringValue = id;
        so.FindProperty("_unlockResultId").stringValue = result;
        so.FindProperty("_durationSeconds").floatValue = seconds;
        so.FindProperty("_needsBuilder").boolValue = needsBuilder;
        so.ApplyModifiedPropertiesWithoutUndo();
        return construction;
    }

    private BoardRequestDefinition Request(string id, int order, string requiredDevelopment, int minResidents,
        ConstructionDefinition construction, SettlementOtterDefinition[] settles = null,
        (SettlementOtterDefinition otter, ResidentState state)[] arrivals = null, int stage = -1,
        GuestbookEntryDefinition completionEntry = null, int kingdomLevel = 0, ZoneDefinition clearZone = null)
    {
        var request = Create<BoardRequestDefinition>();
        var so = new SerializedObject(request);
        so.FindProperty("_requestId").stringValue = id;
        so.FindProperty("_order").intValue = order;
        so.FindProperty("_requiredDevelopment").stringValue = requiredDevelopment;
        so.FindProperty("_minResidents").intValue = minResidents;
        so.FindProperty("_construction").objectReferenceValue = construction;
        so.FindProperty("_stageOnComplete").intValue = stage;
        so.FindProperty("_completionEntry").objectReferenceValue = completionEntry;
        so.FindProperty("_kingdomLevel").intValue = kingdomLevel;
        so.FindProperty("_clearZone").objectReferenceValue = clearZone;
        SetList(so.FindProperty("_settles"), settles ?? new SettlementOtterDefinition[0]);

        var list = so.FindProperty("_arrivals");
        arrivals ??= new (SettlementOtterDefinition, ResidentState)[0];
        list.arraySize = arrivals.Length;
        for (int i = 0; i < arrivals.Length; i++)
        {
            var element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("_otter").objectReferenceValue = arrivals[i].otter;
            element.FindPropertyRelative("_state").enumValueIndex = (int)arrivals[i].state;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return request;
    }

    #endregion

    [Test]
    public void NewGame_FirstOtterVisitsOnce_AndIsNotAResident()
    {
        var settlement = new Settlement();

        SettlementRules.InitializeNewGame(_config, settlement);
        SettlementRules.InitializeNewGame(_config, settlement); // 재접속

        Assert.IsTrue(settlement.TryGetResidentState("otter_first", out var state));
        Assert.AreEqual(ResidentState.Visitor, state);
        Assert.AreEqual(1, settlement.ResidentOrder.Count, "첫 해달은 한 마리만");
        Assert.AreEqual(0, settlement.ResidentCount, "첫 집 전에는 주민 수에 안 들어감");
        CollectionAssert.AreEqual(new[] { "gb_first" }, settlement.Guestbook);
    }

    [Test]
    public void Requests_OpenInOrder_AndCompleteOnce()
    {
        var settlement = new Settlement();
        SettlementRules.InitializeNewGame(_config, settlement);

        Assert.AreEqual(RequestStatus.Available, SettlementRules.GetStatus(_house1, settlement));
        Assert.AreEqual(RequestStatus.Locked, SettlementRules.GetStatus(_house2, settlement));
        Assert.AreSame(_house1, SettlementRules.FindCurrent(_config, settlement));

        Assert.IsTrue(SettlementRules.ApplyCompletion(_house1, settlement));
        Assert.IsFalse(SettlementRules.ApplyCompletion(_house1, settlement), "같은 부탁은 한 번만");

        Assert.AreEqual(1, settlement.ResidentCount, "첫 집 후 주민 +1");
        Assert.AreEqual(1, settlement.Stage);
        Assert.IsTrue(settlement.HasDevelopment("house_1"));
        Assert.IsTrue(SettlementRules.HasBuilder(_config, settlement), "건설 해달이 왔음");
        Assert.AreEqual(RequestStatus.Available, SettlementRules.GetStatus(_house2, settlement));
        Assert.AreEqual(RequestStatus.Locked, SettlementRules.GetStatus(_farmland, settlement), "주민 3명 전");
        CollectionAssert.AreEqual(new[] { "gb_first", "gb_candidate", "gb_builder", "gb_settle" }, settlement.Guestbook);
    }

    [Test]
    public void Building_ShowsAsBuilding_ThenThreeResidentsOpenFarmland()
    {
        var settlement = new Settlement();
        SettlementRules.InitializeNewGame(_config, settlement);
        SettlementRules.ApplyCompletion(_house1, settlement);

        settlement.StartJob("req_house_2", "con_house_2", 0, 100);
        Assert.AreEqual(RequestStatus.Building, SettlementRules.GetStatus(_house2, settlement));
        Assert.AreSame(_house2, SettlementRules.FindCurrent(_config, settlement), "건설 중인 부탁이 먼저");
        Assert.AreEqual(0.5f, settlement.Job.Progress(50), 0.0001f);
        Assert.IsFalse(settlement.Job.IsDue(99));
        Assert.IsTrue(settlement.Job.IsDue(100));

        settlement.FinishJob();
        SettlementRules.ApplyCompletion(_house2, settlement);

        Assert.AreEqual(3, settlement.ResidentCount);
        Assert.AreEqual(RequestStatus.Available, SettlementRules.GetStatus(_farmland, settlement));
    }

    [Test]
    public void SaveRoundTrip_KeepsEverything_IncludingRunningJob()
    {
        var settlement = new Settlement();
        SettlementRules.InitializeNewGame(_config, settlement);
        SettlementRules.ApplyCompletion(_house1, settlement);
        settlement.StartJob("req_house_2", "con_house_2", 10, 300);
        settlement.SetGatherReady("branch_1", 777);
        settlement.MarkBoardVisited();

        var saved = new SettlementSaveData();
        settlement.Write(saved);
        // JsonUtility를 거쳐도 같아야 함 (실제 세이브 경로)
        var json = JsonUtility.ToJson(saved);
        var loaded = new Settlement();
        loaded.Load(JsonUtility.FromJson<SettlementSaveData>(json));

        Assert.IsTrue(loaded.Initialized);
        Assert.IsTrue(loaded.BoardVisited);
        Assert.AreEqual(1, loaded.Stage);
        Assert.AreEqual(1, loaded.ResidentCount);
        CollectionAssert.AreEqual(settlement.ResidentOrder, loaded.ResidentOrder);
        CollectionAssert.AreEqual(settlement.Guestbook, loaded.Guestbook);
        Assert.IsTrue(loaded.IsCompleted("req_house_1"));
        Assert.IsTrue(loaded.HasDevelopment("house_1"));
        Assert.AreEqual("req_house_2", loaded.Job.RequestId);
        Assert.AreEqual(300, loaded.Job.EndUtcTicks);
        Assert.IsFalse(loaded.IsGatherReady("branch_1", 776));
        Assert.IsTrue(loaded.IsGatherReady("branch_1", 777));
        Assert.IsTrue(loaded.IsGatherReady("branch_2", 0), "주운 적 없는 자리는 있음");

        // 재접속해도 첫 해달이 또 오지 않음
        SettlementRules.InitializeNewGame(_config, loaded);
        Assert.AreEqual(settlement.ResidentOrder.Count, loaded.ResidentOrder.Count);
    }

    [Test]
    public void CountGatherRegrown_CountsOnlySpotsThatGrewBackInTheWindow()
    {
        var settlement = new Settlement();
        settlement.SetGatherReady("branch_1", 50);    // 나가기 전에 이미 생김
        settlement.SetGatherReady("branch_2", 150);   // 비운 동안 생김
        settlement.SetGatherReady("pebble_1", 200);   // 돌아온 순간 생김
        settlement.SetGatherReady("pebble_2", 500);   // 아직 안 생김

        Assert.AreEqual(2, settlement.CountGatherRegrown(100, 200));
        Assert.AreEqual(0, settlement.CountGatherRegrown(200, 200), "비운 시간이 0이면 없음");
    }

    [Test]
    public void LevelCap_StaysBelowNextUnfinishedMilestone()
    {
        var mine = Create<ZoneDefinition>();
        var chair = Request("req_chair", 1, "house_1", 0, Construction("con_chair", "chair", 8f, false), kingdomLevel: 2);
        var minePath = Request("req_mine_path", 2, "chair", 0, Construction("con_mine_path", "mine_cleared", 0f, false),
            kingdomLevel: 3, clearZone: mine);
        var so = new SerializedObject(_config);
        SetList(so.FindProperty("_requests"), _house1, chair, minePath, _house2, _farmland);
        so.ApplyModifiedPropertiesWithoutUndo();
        var settlement = new Settlement();

        Assert.AreEqual(1, SettlementRules.LevelCap(_config, settlement), "의자 전에는 Lv.1");
        Assert.AreSame(minePath, SettlementRules.FindClearing(_config, mine));
        Assert.IsNull(SettlementRules.FindClearing(_config, null));

        settlement.CompleteRequest("req_chair", "chair");
        Assert.AreEqual(2, SettlementRules.LevelCap(_config, settlement), "광산 길 전에는 Lv.2");

        settlement.CompleteRequest("req_mine_path", "mine_cleared");
        Assert.AreEqual(int.MaxValue, SettlementRules.LevelCap(_config, settlement), "다 끝내면 제한 없음");
    }

    [Test]
    public void Job_WaitsForWorker_ThenRunsFromArrival()
    {
        var settlement = new Settlement();
        settlement.StartJob("req_house_2", "con_house_2", 100, 400, waitingForWorker: true);

        var job = settlement.Job;
        Assert.IsTrue(job.WaitingForWorker);
        Assert.AreEqual(0f, job.Progress(10_000), "도착 전에는 시간이 흐르지 않음");
        Assert.IsFalse(job.IsDue(10_000));
        Assert.AreEqual(300, job.Remaining(10_000).Ticks, "남은 시간 = 걸리는 시간 그대로");

        Assert.IsTrue(settlement.BeginJobWork(1_000));
        job = settlement.Job;
        Assert.IsFalse(job.WaitingForWorker);
        Assert.AreEqual(1_000, job.StartUtcTicks, "도착한 때부터 잼");
        Assert.AreEqual(1_300, job.EndUtcTicks);
        Assert.IsFalse(settlement.BeginJobWork(2_000), "이미 시작했으면 그대로");

        // 세이브를 거쳐도 기다리는 중인지 남음
        settlement.FinishJob();
        settlement.StartJob("req_house_2", "con_house_2", 0, 300, waitingForWorker: true);
        var saved = new SettlementSaveData();
        settlement.Write(saved);
        var loaded = new Settlement();
        loaded.Load(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(saved)));
        Assert.IsTrue(loaded.Job.WaitingForWorker);
    }

    [Test]
    public void EmptySave_FromOldVersion_LoadsAsFreshState()
    {
        var loaded = new Settlement();
        loaded.Load(JsonUtility.FromJson<SettlementSaveData>("{}"));

        Assert.IsFalse(loaded.Initialized);
        Assert.IsNull(loaded.Job, "빈 건설 칸은 건설 없음");
        Assert.AreEqual(0, loaded.Stage);
    }

    [Test]
    public void LegacySave_CompletesEverything_SoFarmStaysOpen()
    {
        var settlement = new Settlement();
        settlement.Load(new SettlementSaveData { legacyComplete = true });

        SettlementRules.CompleteAll(_config, settlement);

        Assert.IsTrue(settlement.HasDevelopment("farmland"));
        Assert.AreEqual(3, settlement.Stage);
        Assert.AreEqual(3, settlement.ResidentCount);
        Assert.IsFalse(settlement.LegacyComplete);
        Assert.IsNull(SettlementRules.FindCurrent(_config, settlement));
    }

    [Test]
    public void Arrive_NeverDemotesAResident()
    {
        var settlement = new Settlement();
        settlement.SetResident("otter_candidate", ResidentState.Resident);

        SettlementRules.Arrive(_candidate, ResidentState.Visitor, settlement);

        settlement.TryGetResidentState("otter_candidate", out var state);
        Assert.AreEqual(ResidentState.Resident, state);
    }

    [Test]
    public void StartJob_TwiceThrows()
    {
        var settlement = new Settlement();
        settlement.StartJob("a", "c", 0, 10);
        Assert.Throws<System.InvalidOperationException>(() => settlement.StartJob("b", "c", 0, 10));
    }
}
