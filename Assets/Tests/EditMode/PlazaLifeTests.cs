using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 광장 생활 (Docs/광장생활_작업기록.md): 희귀 해달의 방문 조건(밤 · 낮 · 의자 · 가로등 · 식탁 · 아늑함, 조건을 모르면 보지 않음),
/// 아늑함 · 기록관만큼 줄어드는 방문 간격, 조건 문구, 건설소의 공사 시간, 끝낸 부탁이 데려오는 해달 맞추기.
/// </summary>
public class PlazaLifeTests
{
    private readonly List<Object> _created = new List<Object>();
    private SettlementConfig _config;
    private SettlementOtterDefinition _common;
    private SettlementOtterDefinition _night;
    private SettlementOtterDefinition _cozy;

    private static readonly long Start = new DateTime(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc).Ticks;

    [SetUp]
    public void SetUp()
    {
        _common = Otter("otter_toy_common", 0, VisitCondition.Any, 0);
        _night = Otter("otter_toy_night", 0, VisitCondition.Night, 0);
        _cozy = Otter("otter_toy_cozy", 0, VisitCondition.Cozy, 20);
        _config = Create<SettlementConfig>();
        SetList(_config, "_otters", new Object[] { _common, _night, _cozy });
        _config.SetupToyVisits(new[] { 10f }, 2);
    }

    [TearDown]
    public void TearDown()
    {
        KingdomBonus.Source = null;
        foreach (var asset in _created)
            Object.DestroyImmediate(asset);
        _created.Clear();
    }

    [Test]
    public void Conditions_FilterCandidates()
    {
        var settlement = new Settlement();
        var result = new List<SettlementOtterDefinition>();

        ToyVisitRules.CollectCandidates(_config, settlement, 0, null, new ToyVisitContext(false, 0, 0, 0, 0), result);
        CollectionAssert.AreEquivalent(new[] { _common }, result, "낮 · 아늑함 0: 조건 없는 해달만");

        ToyVisitRules.CollectCandidates(_config, settlement, 0, null, new ToyVisitContext(true, 0, 0, 0, 25), result);
        CollectionAssert.AreEquivalent(new[] { _common, _night, _cozy }, result, "밤 · 아늑함 25");

        ToyVisitRules.CollectCandidates(_config, settlement, 0, null, result);
        Assert.AreEqual(3, result.Count, "광장 상태를 모르면 조건을 보지 않음");
    }

    [Test]
    public void Conditions_FurnitureKinds()
    {
        var seat = Otter("otter_toy_seat", 0, VisitCondition.Seat, 0);
        var lamp = Otter("otter_toy_lamp", 0, VisitCondition.Lamp, 0);
        var table = Otter("otter_toy_table", 0, VisitCondition.Table, 0);
        var day = Otter("otter_toy_day", 0, VisitCondition.Day, 0);
        var none = new ToyVisitContext(true, 0, 0, 0, 0);
        Assert.IsFalse(ToyVisitRules.MeetsCondition(seat, none));
        Assert.IsFalse(ToyVisitRules.MeetsCondition(lamp, none));
        Assert.IsFalse(ToyVisitRules.MeetsCondition(table, none));
        Assert.IsFalse(ToyVisitRules.MeetsCondition(day, none), "밤에는 낮 해달이 안 옴");
        var all = new ToyVisitContext(false, 1, 1, 1, 0);
        Assert.IsTrue(ToyVisitRules.MeetsCondition(seat, all));
        Assert.IsTrue(ToyVisitRules.MeetsCondition(lamp, all));
        Assert.IsTrue(ToyVisitRules.MeetsCondition(table, all));
        Assert.IsTrue(ToyVisitRules.MeetsCondition(day, all));
        StringAssert.Contains("가로등", ToyVisitRules.ConditionText(lamp));
        StringAssert.Contains("20", ToyVisitRules.ConditionText(_cozy));
        Assert.AreEqual(string.Empty, ToyVisitRules.ConditionText(_common));
    }

    [Test]
    public void Coziness_ShortensVisitInterval()
    {
        Assert.AreEqual(0f, ToyVisitRules.CozySpeedup(0), 0.0001f);
        Assert.AreEqual(0.25f, ToyVisitRules.CozySpeedup(30), 0.0001f);
        Assert.AreEqual(0.5f, ToyVisitRules.CozySpeedup(500), 0.0001f, "최대 50%");
        Assert.AreEqual(1f, ToyVisitRules.IntervalScale(null), 0.0001f);
        Assert.AreEqual(0.75f * 0.8f, ToyVisitRules.IntervalScale(new ToyVisitContext(false, 0, 0, 0, 30, 20)), 0.0001f, "아늑함 × 기록관");

        var settlement = new Settlement();
        Assert.AreEqual(ToyVisitStep.Counting,
            ToyVisitRules.Step(_config, settlement, 0, null, new ToyVisitContext(false, 0, 0, 0, 60), Start, out _));
        Assert.AreEqual(Start + TimeSpan.FromMinutes(5).Ticks, settlement.ToyVisitDueUtcTicks, "10분 × (1 − 50%)");
    }

    [Test]
    public void BuildSpeed_ShortensConstruction()
    {
        Assert.AreEqual(100f, KingdomBonus.Shorten(KingdomBonusKind.BuildSpeed, 100f), 0.0001f, "효과 없음");
        KingdomBonus.Source = kind => kind == KingdomBonusKind.BuildSpeed ? 15 : 0;
        Assert.AreEqual(85f, KingdomBonus.Shorten(KingdomBonusKind.BuildSpeed, 100f), 0.0001f);
        Assert.AreEqual(100f, KingdomBonus.Shorten(KingdomBonusKind.HarvestYield, 100f), 0.0001f, "다른 종류");
        KingdomBonus.Source = _ => 500;
        Assert.AreEqual(10f, KingdomBonus.Shorten(KingdomBonusKind.BuildSpeed, 100f), 0.0001f, "최대 90%");
    }

    [Test]
    public void ReconcileArrivals_BringsOttersOfCompletedRequests()
    {
        var request = Create<BoardRequestDefinition>();
        var so = new SerializedObject(request);
        so.FindProperty("_requestId").stringValue = "req_granary";
        var arrivals = so.FindProperty("_arrivals");
        arrivals.arraySize = 1;
        arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("_otter").objectReferenceValue = _common;
        arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("_state").enumValueIndex = (int)ResidentState.Visitor;
        so.ApplyModifiedPropertiesWithoutUndo();
        SetList(_config, "_requests", new Object[] { request });

        var notDone = new Settlement();
        Assert.AreEqual(0, SettlementMigration.ReconcileArrivals(_config, notDone).Count, "끝내지 않은 부탁");

        var settlement = new Settlement();
        settlement.CompleteRequest("req_granary", "granary_built");
        Assert.AreEqual(1, SettlementMigration.ReconcileArrivals(_config, settlement).Count);
        Assert.IsTrue(settlement.TryGetResidentState(_common.OtterId, out var state));
        Assert.AreEqual(ResidentState.Visitor, state);
        Assert.AreEqual(0, SettlementMigration.ReconcileArrivals(_config, settlement).Count, "이미 왔으면 그대로");

        var resident = new Settlement();
        resident.CompleteRequest("req_granary", "granary_built");
        resident.SetResident(_common.OtterId, ResidentState.Resident);
        Assert.AreEqual(0, SettlementMigration.ReconcileArrivals(_config, resident).Count);
        resident.TryGetResidentState(_common.OtterId, out state);
        Assert.AreEqual(ResidentState.Resident, state, "주민을 방문으로 낮추지 않음");
    }

    #region 만들기

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    private SettlementOtterDefinition Otter(string id, int tier, VisitCondition condition, int value)
    {
        var otter = Create<SettlementOtterDefinition>();
        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.FindProperty("_toyVisitor").boolValue = true;
        so.FindProperty("_visitTier").intValue = tier;
        so.FindProperty("_visitCondition").enumValueIndex = (int)condition;
        so.FindProperty("_visitConditionValue").intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
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

    #endregion
}
