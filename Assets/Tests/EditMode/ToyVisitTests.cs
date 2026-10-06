using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 특성 · 장난감 방문 (P4, Docs/특성사회성장_P4_기획반영.md):
/// 특성 수는 주민만, 장난감이 없으면 시계를 지우지 않음, 등급 이하 해달만, 간격표(회차), 시간이 되면 한 마리(Visitor · 방명록 · 회차),
/// 기다리는 해달이 가득하면 멈춤, 올 해달이 없으면 멈춤, 같은 회차는 같은 해달, 없는 특성 가중치,
/// 빈 집(입주민 없음 · 사업이 정해 둔 자리 아님)에만 입주, 만나기 전에는 입주 못 함, 입주 = 주민 + 특성 +1, 세이브 왕복.
/// </summary>
public class ToyVisitTests
{
    private readonly List<Object> _created = new List<Object>();

    private SettlementOtterDefinition _first;
    private SettlementOtterDefinition _neighbor;
    private SettlementOtterDefinition _woodCommon;
    private SettlementOtterDefinition _fishCommon;
    private SettlementOtterDefinition _tradeRare;
    private SettlementConfig _config;

    private const string ReservedSlot = "slot_neighbor_house";
    private static readonly long Start = new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc).Ticks;

    [SetUp]
    public void SetUp()
    {
        _first = Otter("otter_first", OtterTrait.Exploring, false, 0);
        _neighbor = Otter("otter_neighbor", OtterTrait.Hauling, false, 0);
        _woodCommon = Otter("otter_toy_wood", OtterTrait.Woodcutting, true, 0);
        _fishCommon = Otter("otter_toy_fish", OtterTrait.Fishing, true, 0);
        _tradeRare = Otter("otter_toy_trade", OtterTrait.Trading, true, 1);

        _config = Create<SettlementConfig>();
        SetList(_config, "_otters", new Object[] { _first, _neighbor, _woodCommon, _fishCommon, _tradeRare });
        _config.SetupToyVisits(new[] { 7f, 12f }, 2);

        // 새 이웃의 집 자리는 공동사업이 포근이에게 정해 둠
        var project = Create<CommunityProjectDefinition>();
        project.Setup("p3_neighbor_01", 1, "새 이웃", 1, "", 0, null, new[]
        {
            new ProjectStageDefinition("move_in", ProjectActionKind.SettleResident, "입주").With(resident: _neighbor, houseSlotId: ReservedSlot),
        }, "p3_neighbor_settled", 0);
        _config.SetupP3(new[] { project }, null, null, "", 7, "", "");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _created)
            Object.DestroyImmediate(asset);
        _created.Clear();
    }

    #region 특성 수

    [Test]
    public void TraitCount_CountsResidentsOnly()
    {
        var settlement = new Settlement();
        settlement.SetResident(_first.OtterId, ResidentState.Resident);
        settlement.SetResident(_woodCommon.OtterId, ResidentState.Visitor);
        settlement.SetResident(_neighbor.OtterId, ResidentState.SettlementCandidate);

        Assert.AreEqual(1, TraitRules.Count(_config, settlement, OtterTrait.Exploring));
        Assert.AreEqual(0, TraitRules.Count(_config, settlement, OtterTrait.Woodcutting), "방문 해달은 세지 않음");
        Assert.AreEqual(0, TraitRules.Count(_config, settlement, OtterTrait.Hauling), "정착 후보는 세지 않음");

        var counts = TraitRules.CountAll(_config, settlement);
        Assert.AreEqual(OtterTraits.Count + 1, counts.Length);
        Assert.AreEqual(1, counts[(int)OtterTrait.Exploring]);
        Assert.AreEqual(0, counts[(int)OtterTrait.None]);
    }

    #endregion

    #region 방문 시계

    [Test]
    public void NoToy_KeepsClock()
    {
        var settlement = new Settlement();
        settlement.SetToyVisitDue(Start + 100);

        Assert.AreEqual(ToyVisitStep.NoToy, ToyVisitRules.Step(_config, settlement, -1, Start, out var arrived));
        Assert.IsNull(arrived);
        Assert.AreEqual(Start + 100, settlement.ToyVisitDueUtcTicks, "꾸미기를 불러오기 전일 수 있어 시계를 지우지 않음");
    }

    [Test]
    public void CommonToy_SchedulesThenArrivesCommonOtter()
    {
        var settlement = new Settlement();

        Assert.AreEqual(ToyVisitStep.Counting, ToyVisitRules.Step(_config, settlement, 0, Start, out _));
        Assert.AreEqual(Start + TimeSpan.FromMinutes(7).Ticks, settlement.ToyVisitDueUtcTicks, "첫 방문 7분");

        long almost = settlement.ToyVisitDueUtcTicks - 1;
        Assert.AreEqual(ToyVisitStep.Counting, ToyVisitRules.Step(_config, settlement, 0, almost, out _));

        Assert.AreEqual(ToyVisitStep.Arrived, ToyVisitRules.Step(_config, settlement, 0, almost + 1, out var arrived));
        Assert.IsNotNull(arrived);
        Assert.AreNotEqual(_tradeRare, arrived, "흔한 장난감으로는 레어 해달이 오지 않음");
        Assert.IsTrue(settlement.TryGetResidentState(arrived.OtterId, out var state));
        Assert.AreEqual(ResidentState.Visitor, state);
        Assert.IsTrue(settlement.HasGuestbook(GuestbookId(arrived)), "첫 방문 방명록");
        Assert.AreEqual(1, settlement.ToyVisitSerial);
        Assert.AreEqual(0, settlement.ToyVisitDueUtcTicks);

        // 다음 시계는 두 번째 간격 (12분)
        long now = almost + 1;
        Assert.AreEqual(ToyVisitStep.Counting, ToyVisitRules.Step(_config, settlement, 0, now, out _));
        Assert.AreEqual(now + TimeSpan.FromMinutes(12).Ticks, settlement.ToyVisitDueUtcTicks);
    }

    [Test]
    public void IntervalBeyondTable_UsesLast()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(7), ToyVisitRules.Interval(_config, 0));
        Assert.AreEqual(TimeSpan.FromMinutes(12), ToyVisitRules.Interval(_config, 1));
        Assert.AreEqual(TimeSpan.FromMinutes(12), ToyVisitRules.Interval(_config, 9));
    }

    [Test]
    public void RareToy_IncludesCommonAndRare()
    {
        var candidates = new List<SettlementOtterDefinition>();
        ToyVisitRules.CollectCandidates(_config, new Settlement(), 1, candidates);
        CollectionAssert.AreEquivalent(new[] { _woodCommon, _fishCommon, _tradeRare }, candidates);

        ToyVisitRules.CollectCandidates(_config, new Settlement(), 0, candidates);
        CollectionAssert.AreEquivalent(new[] { _woodCommon, _fishCommon }, candidates);
    }

    [Test]
    public void WaitingVisitorsFull_StopsClock()
    {
        var settlement = new Settlement();
        settlement.SetResident(_woodCommon.OtterId, ResidentState.Visitor);
        settlement.SetResident(_fishCommon.OtterId, ResidentState.Visitor);
        settlement.SetToyVisitDue(Start + 100);

        Assert.AreEqual(ToyVisitStep.Full, ToyVisitRules.Step(_config, settlement, 1, Start, out _));
        Assert.AreEqual(0, settlement.ToyVisitDueUtcTicks);
    }

    [Test]
    public void EveryoneArrived_NoCandidate()
    {
        var settlement = new Settlement();
        settlement.SetResident(_woodCommon.OtterId, ResidentState.Resident);
        settlement.SetResident(_fishCommon.OtterId, ResidentState.Resident);

        Assert.AreEqual(ToyVisitStep.NoCandidate, ToyVisitRules.Step(_config, settlement, 0, Start, out _));
        Assert.AreEqual(0, settlement.ToyVisitDueUtcTicks);
    }

    #endregion

    #region 고르기

    [Test]
    public void Pick_SameSerialSameOtter()
    {
        var candidates = new List<SettlementOtterDefinition> { _woodCommon, _fishCommon, _tradeRare };
        var counts = new int[OtterTraits.Count + 1];
        for (int serial = 0; serial < 20; serial++)
            Assert.AreEqual(ToyVisitRules.Pick(candidates, 1, counts, serial), ToyVisitRules.Pick(candidates, 1, counts, serial));
    }

    [Test]
    public void Weight_SameTierAndMissingTrait()
    {
        var counts = new int[OtterTraits.Count + 1];
        counts[(int)OtterTrait.Woodcutting] = 1;

        Assert.AreEqual(ToyVisitRules.SameTierWeight, ToyVisitRules.Weight(_woodCommon, 0, counts), "같은 등급, 이미 있는 특성");
        Assert.AreEqual(1, ToyVisitRules.Weight(_woodCommon, 1, counts), "낮은 등급, 이미 있는 특성");
        Assert.AreEqual(ToyVisitRules.MissingTraitWeight, ToyVisitRules.Weight(_fishCommon, 1, counts), "낮은 등급, 없는 특성");
        Assert.AreEqual(ToyVisitRules.SameTierWeight * ToyVisitRules.MissingTraitWeight, ToyVisitRules.Weight(_tradeRare, 1, counts));
    }

    [Test]
    public void Pick_FavorsMissingTrait()
    {
        // 나무캐기는 이미 있고 낚시는 없음 → 1 : 3
        var candidates = new List<SettlementOtterDefinition> { _woodCommon, _fishCommon };
        var counts = new int[OtterTraits.Count + 1];
        counts[(int)OtterTrait.Woodcutting] = 1;
        int fish = 0;
        for (int serial = 0; serial < 400; serial++)
        {
            if (ToyVisitRules.Pick(candidates, 1, counts, serial) == _fishCommon)
                fish++;
        }
        Assert.Greater(fish, 240, "없는 특성 쪽이 대략 3/4");
        Assert.Less(fish, 360);
    }

    #endregion

    #region 입주

    [Test]
    public void FreeHome_SkipsOccupiedAndReserved()
    {
        var settlement = new Settlement();
        settlement.AddHouse("house_reserved", "con_p3_house", ReservedSlot);
        Assert.IsNull(ToyVisitRules.FindFreeHome(_config, settlement), "사업이 정해 둔 집은 빈 집이 아님");

        settlement.AddHouse("home_1", "con_home", "slot_home_1");
        settlement.AddHouse("home_2", "con_home", "slot_home_2");
        settlement.SetHouseResident("home_1", "someone");
        Assert.AreEqual("home_2", ToyVisitRules.FindFreeHome(_config, settlement).InstanceId);
    }

    [Test]
    public void MoveIn_NeedsMeetingAndFreeHome()
    {
        var settlement = new Settlement();
        settlement.SetResident(_woodCommon.OtterId, ResidentState.Visitor);

        Assert.IsFalse(ToyVisitRules.IsHomelessVisitor(settlement, _woodCommon), "광장에서 만나기 전");
        settlement.MarkMet(_woodCommon.OtterId);
        Assert.IsTrue(ToyVisitRules.IsHomelessVisitor(settlement, _woodCommon));
        Assert.IsFalse(ToyVisitRules.CanMoveIn(_config, settlement, _woodCommon), "빈 집 없음");
        Assert.IsFalse(ToyVisitRules.MoveIn(_config, settlement, _woodCommon));

        settlement.AddHouse("home_1", "con_home", "slot_home_1");
        Assert.IsTrue(ToyVisitRules.CanMoveIn(_config, settlement, _woodCommon));
        Assert.IsTrue(ToyVisitRules.MoveIn(_config, settlement, _woodCommon));

        Assert.IsTrue(settlement.TryGetResidentState(_woodCommon.OtterId, out var state));
        Assert.AreEqual(ResidentState.Resident, state);
        Assert.AreEqual(_woodCommon.OtterId, settlement.FindHouseBySlot("slot_home_1").ResidentId);
        Assert.AreEqual(1, TraitRules.Count(_config, settlement, OtterTrait.Woodcutting), "입주하면 특성 +1");
        Assert.IsFalse(ToyVisitRules.MoveIn(_config, settlement, _woodCommon), "연타해도 한 번");
        Assert.AreEqual(0, ToyVisitRules.WaitingCount(_config, settlement));
    }

    [Test]
    public void NonToyOtter_NeverHomelessVisitor()
    {
        var settlement = new Settlement();
        settlement.SetResident(_neighbor.OtterId, ResidentState.Visitor);
        settlement.MarkMet(_neighbor.OtterId);
        settlement.AddHouse("home_1", "con_home", "slot_home_1");

        Assert.IsFalse(ToyVisitRules.IsHomelessVisitor(settlement, _neighbor), "새 이웃은 공동사업 입주로");
        Assert.IsFalse(ToyVisitRules.CanMoveIn(_config, settlement, _neighbor));
    }

    #endregion

    [Test]
    public void SaveRoundTrip_KeepsClockAndSerial()
    {
        var settlement = new Settlement();
        Assert.AreEqual(ToyVisitStep.Counting, ToyVisitRules.Step(_config, settlement, 0, Start, out _));
        Assert.AreEqual(ToyVisitStep.Arrived, ToyVisitRules.Step(_config, settlement, 0, settlement.ToyVisitDueUtcTicks, out var arrived));
        ToyVisitRules.Step(_config, settlement, 0, Start + 1, out _);
        long due = settlement.ToyVisitDueUtcTicks;

        var saved = new SettlementSaveData();
        settlement.Write(saved);
        var json = JsonUtility.ToJson(saved);
        var loaded = new Settlement();
        loaded.Load(JsonUtility.FromJson<SettlementSaveData>(json));

        Assert.AreEqual(due, loaded.ToyVisitDueUtcTicks);
        Assert.AreEqual(1, loaded.ToyVisitSerial);
        Assert.IsTrue(loaded.TryGetResidentState(arrived.OtterId, out var state));
        Assert.AreEqual(ResidentState.Visitor, state);

        // 옛 세이브 (필드 없음)
        var old = new Settlement();
        old.Load(new SettlementSaveData());
        Assert.AreEqual(0, old.ToyVisitDueUtcTicks);
        Assert.AreEqual(0, old.ToyVisitSerial);
    }

    #region 만들기

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    private SettlementOtterDefinition Otter(string id, OtterTrait trait, bool toy, int tier)
    {
        var otter = Create<SettlementOtterDefinition>();
        var entry = Create<GuestbookEntryDefinition>();
        var entrySo = new SerializedObject(entry);
        entrySo.FindProperty("_entryId").stringValue = $"gb_{id}_arrival";
        entrySo.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.FindProperty("_trait").intValue = (int)trait;
        so.FindProperty("_toyVisitor").boolValue = toy;
        so.FindProperty("_visitTier").intValue = tier;
        so.FindProperty("_arrivalEntry").objectReferenceValue = entry;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    private static string GuestbookId(SettlementOtterDefinition otter) => $"gb_{otter.OtterId}_arrival";

    private static void SetList(Object target, string property, Object[] values)
    {
        var so = new SerializedObject(target);
        var list = so.FindProperty(property);
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion
}
