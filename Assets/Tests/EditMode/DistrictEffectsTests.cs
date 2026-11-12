using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 왕국 화면 구역 효과: 용도 바꾸기 미리보기(세이브는 그대로 · 맞지 않게 되는 건물 · 보너스 · 질서도), 효과 글(이름 + 수치),
/// 새 건물 하나가 들어서면 커지는 보너스, 용도 고르기 설명.
/// </summary>
public class DistrictEffectsTests
{
    private readonly List<Object> _created = new List<Object>();
    private SettlementConfig _config;
    private DistrictBlockDefinition _center;
    private DistrictBlockDefinition _a;
    private DistrictBlockDefinition _b;
    private DistrictBlockDefinition _c;

    // a: 집 두 채, b: 가게 한 채
    private static readonly DistrictPlacement HomeA1 = new DistrictPlacement(DistrictType.Residential, new RectInt(31, 1, 6, 4));
    private static readonly DistrictPlacement HomeA2 = new DistrictPlacement(DistrictType.Residential, new RectInt(41, 1, 4, 3));
    private static readonly DistrictPlacement ShopB = new DistrictPlacement(DistrictType.Commercial, new RectInt(61, 1, 4, 3));

    [SetUp]
    public void SetUp()
    {
        _center = Block("center", DistrictType.Central, "", new RectInt(0, 0, 20, 20));
        _a = Block("a", DistrictType.None, "a_open", new RectInt(30, 0, 20, 20));
        _b = Block("b", DistrictType.None, "b_open", new RectInt(60, 0, 20, 20));
        _c = Block("c", DistrictType.None, "c_open", new RectInt(0, 30, 20, 20));
        _config = Create<SettlementConfig>();
        _config.SetupDistricts(new[] { _center, _a, _b, _c }, DistrictType.Residential, 60, 10, 10, 80, new[]
        {
            new DistrictBonusEntry(DistrictType.Residential, KingdomBonusKind.VisitSpeed, 0, 5, 10),
            new DistrictBonusEntry(DistrictType.Commercial, KingdomBonusKind.SellPrice, 0, 5, 10),
            new DistrictBonusEntry(DistrictType.Agricultural, KingdomBonusKind.GrowSpeed, 10, 0, 10),
        });
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _created)
            Object.DestroyImmediate(asset);
        _created.Clear();
    }

    [Test]
    public void Preview_HomesBlockToCommercial_LosesHomesBonusAndOrder_SaveUnchanged()
    {
        var settlement = Town();
        var placements = new[] { HomeA1, HomeA2, ShopB };
        Assert.AreEqual(80, DistrictRules.Evaluate(_config, settlement, placements).Score, "60 × 3/3 + 10 × 2");

        var preview = DistrictEffects.Preview(_config, settlement, placements, _a, DistrictType.Commercial);

        Assert.AreEqual(80, preview.OrderBefore);
        Assert.AreEqual(40, preview.OrderAfter, "60 × 1/3 + 10 × 2");
        Assert.AreEqual(2, preview.Lost, "집 두 채가 주거 구역 밖이 됨");
        Assert.AreEqual(0, preview.Gained, "가게는 이미 맞는 구역");
        CollectionAssert.AreEqual(new[] { (KingdomBonusKind.VisitSpeed, 10, 0) }, preview.Bonuses, "주거 구역이 없어짐 · 판매가는 그대로");
        Assert.AreEqual(DistrictType.Residential, settlement.GetDistrict("a"), "미리보기는 세이브를 바꾸지 않음");

        var lines = DistrictEffects.PreviewLines(preview);
        CollectionAssert.AreEqual(new[]
        {
            "· 맞지 않게 되는 건물 2채 (이사로 옮길 수 있어요)",
            "· 해달 방문 속도 +10% → 0%",
            "· 왕국 질서도 80 → 40",
        }, lines);
    }

    [Test]
    public void Preview_EmptyBlock_AddsDistrictScoreAndFlatBonus()
    {
        var settlement = Town();
        settlement.UnlockDevelopment("c_open");
        var placements = new[] { HomeA1, ShopB };

        var preview = DistrictEffects.Preview(_config, settlement, placements, _c, DistrictType.Agricultural);

        Assert.AreEqual(80, preview.OrderBefore);
        Assert.AreEqual(90, preview.OrderAfter, "정한 블록 +10");
        Assert.AreEqual(0, preview.Lost);
        CollectionAssert.AreEqual(new[] { (KingdomBonusKind.GrowSpeed, 0, 10) }, preview.Bonuses);
        CollectionAssert.Contains(DistrictEffects.PreviewLines(preview), "· 밭 자라는 시간 0% → -10%");
        Assert.AreEqual(DistrictType.None, settlement.GetDistrict("c"));
    }

    [Test]
    public void Preview_ShopsBlockGainsMatchingWhenHomesLandThere()
    {
        var settlement = Town();
        // b(상업)에 집이 잘못 들어가 있음 → b를 주거로 바꾸면 그 집이 맞는 구역이 되고, 가게는 빠짐
        var misplacedHome = new DistrictPlacement(DistrictType.Residential, new RectInt(66, 8, 4, 3));
        var preview = DistrictEffects.Preview(_config, settlement, new[] { ShopB, misplacedHome }, _b, DistrictType.Residential);
        Assert.AreEqual(1, preview.Lost);
        Assert.AreEqual(1, preview.Gained);
    }

    [Test]
    public void Labels_NameAndSignedAmount()
    {
        Assert.AreEqual("판매가 +5%", DistrictEffects.Label(KingdomBonusKind.SellPrice, 5));
        Assert.AreEqual("밭 자라는 시간 -10%", DistrictEffects.Label(KingdomBonusKind.GrowSpeed, 10));
        Assert.AreEqual("생산 속도 0%", DistrictEffects.Label(KingdomBonusKind.ProductionSpeed, 0));
        Assert.AreEqual("맞는 건물마다 판매가 +5% (최대 10%)", DistrictEffects.Describe(_config, DistrictType.Commercial));
        Assert.AreEqual("밭 자라는 시간 -10%", DistrictEffects.Describe(_config, DistrictType.Agricultural));
        Assert.AreEqual("", DistrictEffects.Describe(_config, DistrictType.Production), "효과 표에 없는 용도");
    }

    [Test]
    public void NextBuilding_ShowsGrowthUntilMax()
    {
        var settlement = Town();
        var one = DistrictRules.Evaluate(_config, settlement, new[] { HomeA1 });
        Assert.AreEqual("해달 방문 속도 +5% → +10%", DistrictEffects.NextBuildingText(_config, settlement, one, DistrictType.Residential));
        var two = DistrictRules.Evaluate(_config, settlement, new[] { HomeA1, HomeA2 });
        Assert.AreEqual("해달 방문 속도 +10% (최대)", DistrictEffects.NextBuildingText(_config, settlement, two, DistrictType.Residential));
        Assert.IsNull(DistrictEffects.NextBuildingText(_config, settlement, two, DistrictType.Agricultural), "건물마다 커지는 효과가 없음");
    }

    [Test]
    public void Lines_OnePerBonusWithDistrictState()
    {
        var settlement = Town();
        var order = DistrictRules.Evaluate(_config, settlement, new[] { HomeA1, ShopB });
        var lines = DistrictEffects.Lines(_config, settlement, order);
        Assert.AreEqual(3, lines.Count);
        Assert.IsTrue(lines[0].HasDistrict);
        Assert.AreEqual(1, lines[0].Matching);
        Assert.AreEqual(5, lines[0].Percent);
        Assert.AreEqual(5, lines[1].Percent, "가게 한 채");
        Assert.IsFalse(lines[2].HasDistrict, "농업 구역 없음");
        Assert.AreEqual(0, lines[2].Percent);
    }

    // a = 주거, b = 상업 (첫 구역은 주거)
    private Settlement Town()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment("a_open");
        settlement.UnlockDevelopment("b_open");
        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, settlement, _a, DistrictType.Residential));
        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, settlement, _b, DistrictType.Commercial));
        return settlement;
    }

    private DistrictBlockDefinition Block(string id, DistrictType fixedType, string open, RectInt area)
    {
        var block = Create<DistrictBlockDefinition>();
        block.Setup(id, id, fixedType, DistrictType.None, new[] { new DistrictStage(open, new[] { area }) }, new DistrictSlot[0]);
        return block;
    }

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }
}
