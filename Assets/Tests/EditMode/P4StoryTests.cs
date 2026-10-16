using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Lv.10~15 해달의 부탁 (P4): 건물 · 입주 부탁은 기록으로 끝남(열려 있을 때만), 결과 발전 · 찾아오는 해달,
/// 레벨 잠금은 아직 넘지 않은 레벨만(지나간 이야기는 선택), 입주 부탁이 기다리는 해달은 장난감 해달이 아니어도 빈 집에 입주,
/// 시설 효과 합계(같은 건물은 한 번), 판매가(반올림) · 얻는 개수(소수는 확률).
/// </summary>
public class P4StoryTests
{
    private readonly List<Object> _created = new List<Object>();
    private SettlementOtterDefinition _trader;
    private SettlementOtterDefinition _sleepy;
    private SettlementOtterDefinition _lumber;
    private BuildingDefinition _house;
    private BuildingDefinition _shop;
    private BoardRequestDefinition _firstHome;
    private BoardRequestDefinition _traderHome;
    private BoardRequestDefinition _shopRequest;
    private BoardRequestDefinition _lumberHome;
    private SettlementConfig _config;

    private static readonly long Now = new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc).Ticks;

    [SetUp]
    public void SetUp()
    {
        _trader = Otter("otter_toy_jjalrang", OtterTrait.Trading, true);
        _sleepy = Otter("otter_sleepy", OtterTrait.Woodcutting, false);
        _lumber = Otter("otter_toy_kungkung", OtterTrait.Woodcutting, true);

        _house = Create<BuildingDefinition>();
        _house.Setup("bld_small_house", "작은 집", BuildingKind.Home, 0, "", null, 0, 600, null, 0.3f, 180f);
        _shop = Create<BuildingDefinition>();
        _shop.Setup("bld_shop", "해달 상점", BuildingKind.Facility, 0, "p4_trader_settled", new[] { new TraitRequirement(OtterTrait.Trading, 1) },
            1, 2000, null, 0f, 300f);
        _shop.SetupEffects(new[] { new BuildingBonus(KingdomBonusKind.SellPrice, 10) }, true);

        _firstHome = Request("req_p4_first_home", 30, "first_gathering_complete", 11, _trader);
        _firstHome.SetupRecordAction(_house, 1, null, "p4_first_home");
        _traderHome = Request("req_p4_trader_home", 31, "p4_first_home", 0, null);
        _traderHome.SetupRecordAction(null, 1, new[] { _trader }, "p4_trader_settled");
        _shopRequest = Request("req_p4_shop", 32, "p4_trader_settled", 12, null);
        _shopRequest.SetupRecordAction(_shop, 1, null, "p4_shop_built");
        _lumberHome = Request("req_p4_lumber_home", 33, "p4_shop_built", 0, null);
        _lumberHome.SetupRecordAction(null, 1, new[] { _sleepy, _lumber }, "p4_lumber_settled");

        _config = Create<SettlementConfig>();
        SetList(_config, "_otters", new Object[] { _trader, _sleepy, _lumber });
        SetList(_config, "_requests", new Object[] { _firstHome, _traderHome, _shopRequest, _lumberHome });
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
    public void BuildRequest_CompletesByRecord_OnlyWhenOpen()
    {
        var settlement = new Settlement();
        BuildingRules.Start(settlement, _house, 1, Now, 1);
        settlement.FinishBuilding(1);
        Assert.IsTrue(SettlementRules.IsRecordComplete(_firstHome, settlement));
        Assert.IsNull(SettlementRules.FindRecordComplete(_config, settlement), "첫 모임 전에는 열리지 않음");

        settlement.UnlockDevelopment("first_gathering_complete");
        Assert.AreEqual(_firstHome, SettlementRules.FindRecordComplete(_config, settlement));

        Assert.IsTrue(SettlementRules.ApplyCompletion(_firstHome, settlement));
        Assert.IsTrue(settlement.HasDevelopment("p4_first_home"));
        Assert.IsTrue(settlement.TryGetResidentState(_trader.OtterId, out var state), "짤랑이가 찾아옴");
        Assert.AreEqual(ResidentState.Visitor, state);
        Assert.IsNull(SettlementRules.FindRecordComplete(_config, settlement), "짤랑이는 아직 주민이 아님");
    }

    [Test]
    public void SettleRequest_NeedsEveryTarget()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment("p4_shop_built");
        settlement.SetResident(_lumber.OtterId, ResidentState.Resident);
        settlement.SetResident(_sleepy.OtterId, ResidentState.Visitor);
        Assert.IsFalse(SettlementRules.IsRecordComplete(_lumberHome, settlement), "꾸벅이는 아직 방문");
        Assert.IsTrue(SettlementRules.IsAwaitedSettler(_config, settlement, _sleepy));
        Assert.IsFalse(SettlementRules.IsAwaitedSettler(_config, settlement, _lumber), "이미 주민");

        // 꾸벅이는 장난감 해달이 아니어도 빈 집에 입주
        settlement.MarkMet(_sleepy.OtterId);
        Assert.IsFalse(ToyVisitRules.CanMoveIn(_config, settlement, _sleepy), "장난감 해달 입주 규칙으로는 안 됨");
        settlement.AddHouse("home_1", "bld_small_house", BuildingRules.HomeSlotId(5));
        Assert.IsTrue(ToyVisitRules.MoveIntoFreeHome(_config, settlement, _sleepy));
        Assert.IsTrue(SettlementRules.IsRecordComplete(_lumberHome, settlement));
        Assert.AreEqual(_lumberHome, SettlementRules.FindRecordComplete(_config, settlement));
        Assert.IsFalse(ToyVisitRules.MoveIntoFreeHome(_config, settlement, _sleepy), "이미 주민");
    }

    [Test]
    public void LevelCap_IgnoresLevelsAlreadyPassed()
    {
        var settlement = new Settlement();
        Assert.AreEqual(10, SettlementRules.LevelCap(_config, settlement, 10), "Lv.10: 첫 집 부탁(→11)을 끝내야");
        Assert.AreEqual(11, SettlementRules.LevelCap(_config, settlement, 11), "Lv.11 세이브: 상점(→12)을 끝내야");
        Assert.AreEqual(int.MaxValue, SettlementRules.LevelCap(_config, settlement, 16), "이미 넘은 세이브는 묶지 않음");

        settlement.CompleteRequest(_firstHome.RequestId, "p4_first_home");
        Assert.AreEqual(11, SettlementRules.LevelCap(_config, settlement, 10));
    }

    [Test]
    public void LevelCapRequest_IsTheRequestHoldingTheCap()
    {
        var settlement = new Settlement();
        Assert.AreEqual(_firstHome, SettlementRules.LevelCapRequest(_config, settlement, 10), "Lv.10: 첫 집 부탁을 끝내면 11");
        Assert.AreEqual(_shopRequest, SettlementRules.LevelCapRequest(_config, settlement, 11), "Lv.11 세이브: 상점");
        Assert.IsNull(SettlementRules.LevelCapRequest(_config, settlement, 16), "이미 넘은 세이브는 묶지 않음");

        settlement.CompleteRequest(_firstHome.RequestId, "p4_first_home");
        Assert.AreEqual(_shopRequest, SettlementRules.LevelCapRequest(_config, settlement, 10));
    }

    [Test]
    public void Bonus_CountsBuiltFacilityOnce()
    {
        var settlement = new Settlement();
        Func<string, BuildingDefinition> find = id => id == _shop.BuildingId ? _shop : id == _house.BuildingId ? _house : null;
        BuildingRules.Start(settlement, _shop, 1, Now, 10);
        Assert.AreEqual(0, BuildingRules.BonusPercent(settlement, find, KingdomBonusKind.SellPrice), "짓는 중에는 없음");
        settlement.FinishBuilding(1);
        Assert.AreEqual(10, BuildingRules.BonusPercent(settlement, find, KingdomBonusKind.SellPrice));
        settlement.StartBuilding(2, _shop.BuildingId, Now, Now);
        settlement.FinishBuilding(2);
        Assert.AreEqual(10, BuildingRules.BonusPercent(settlement, find, KingdomBonusKind.SellPrice), "같은 건물은 한 번");
        Assert.AreEqual(0, BuildingRules.BonusPercent(settlement, find, KingdomBonusKind.HarvestYield));
    }

    [Test]
    public void KingdomBonus_PriceAndAmount()
    {
        Assert.AreEqual(21, KingdomBonus.SellPrice(21), "효과 없음");
        KingdomBonus.Source = kind => kind == KingdomBonusKind.SellPrice ? 10 : kind == KingdomBonusKind.StoneYield ? 20 : 0;
        Assert.AreEqual(23, KingdomBonus.SellPrice(21), "23.1 → 23");
        Assert.AreEqual(55, KingdomBonus.SellPrice(50));
        Assert.AreEqual(2, KingdomBonus.SellPrice(2), "2.2 → 2");

        Assert.AreEqual(3, KingdomBonus.Amount(KingdomBonusKind.StoneYield, 3, 0.9f), "3.6: 0.6 확률 → 굴림 0.9면 3");
        Assert.AreEqual(4, KingdomBonus.Amount(KingdomBonusKind.StoneYield, 3, 0.1f));
        Assert.AreEqual(6, KingdomBonus.Amount(KingdomBonusKind.StoneYield, 5, 0.99f), "6.0은 늘 6");
        Assert.AreEqual(5, KingdomBonus.Amount(KingdomBonusKind.HarvestYield, 5, 0f), "효과 없는 종류");
    }

    #region 만들기

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    private SettlementOtterDefinition Otter(string id, OtterTrait trait, bool toy)
    {
        var otter = Create<SettlementOtterDefinition>();
        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_trait").intValue = (int)trait;
        so.FindProperty("_toyVisitor").boolValue = toy;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    private BoardRequestDefinition Request(string id, int order, string requires, int kingdomLevel, SettlementOtterDefinition arrival)
    {
        var request = Create<BoardRequestDefinition>();
        var so = new SerializedObject(request);
        so.FindProperty("_requestId").stringValue = id;
        so.FindProperty("_order").intValue = order;
        so.FindProperty("_requiredDevelopment").stringValue = requires;
        so.FindProperty("_kingdomLevel").intValue = kingdomLevel;
        so.FindProperty("_stageOnComplete").intValue = -1;
        var arrivals = so.FindProperty("_arrivals");
        arrivals.arraySize = arrival != null ? 1 : 0;
        if (arrival != null)
        {
            arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("_otter").objectReferenceValue = arrival;
            arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("_state").enumValueIndex = (int)ResidentState.Visitor;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return request;
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
