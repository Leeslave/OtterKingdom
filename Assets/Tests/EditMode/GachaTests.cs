using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 보물 조개 뽑기 (Docs/장난감_뽑기_기획.md): 확률 공개표(합 1 · 기획 수치), 천장(에픽 60·80째, 레어 10째),
/// 픽업 반반 → 놓치면 다음 에픽 픽업 확정, 빈 등급은 아래로, 배너 기간, 골드 뽑기 값, 가장 좋은 결과, 세이브 왕복,
/// 한정 해달(좋아하는 장난감이 광장에 있어야 옴), 골드 배너(값 · 에픽 없음 · 레어 천장 · 천장 따로).
/// </summary>
public class GachaTests
{
    // 확률은 float(0.15f = 0.15000000596…)로 저장되므로 비교는 이 정도까지
    private const double Tolerance = 1e-6;

    private readonly List<Object> _created = new List<Object>();
    private ItemRarity _common;
    private ItemRarity _rare;
    private ItemRarity _epic;

    [SetUp]
    public void SetUp()
    {
        _common = Rarity("Common", 0);
        _rare = Rarity("Rare", 1);
        _epic = Rarity("Epic", 2);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _created)
            Object.DestroyImmediate(asset);
        _created.Clear();
    }

    #region 확률 공개표

    [Test]
    public void PickupRates_MatchDesign_AndSumToOne()
    {
        var (table, featuredEpic, _) = PickupTable();
        var rates = GachaRules.Rates(table);

        Assert.AreEqual(1.0, rates.Sum(r => r.Chance), Tolerance, "모든 장난감 확률의 합은 100%");
        Assert.AreEqual(0.03, table.TierChance(GachaTable.Epic), Tolerance);
        Assert.AreEqual(0.15, table.TierChance(GachaTable.Rare), Tolerance);
        Assert.AreEqual(0.82, table.TierChance(GachaTable.Common), Tolerance);

        Assert.AreEqual(0.015, rates.Single(r => r.Item == featuredEpic).Chance, Tolerance, "픽업 에픽 = 에픽 3%의 절반");
        foreach (var rate in rates.Where(r => r.Tier == GachaTable.Epic && !r.Featured))
            Assert.AreEqual(0.0075, rate.Chance, Tolerance, "상시 에픽 2종이 나머지 절반을 나눔");
        foreach (var rate in rates.Where(r => r.Tier == GachaTable.Rare && r.Featured))
            Assert.AreEqual(0.0375, rate.Chance, Tolerance, "픽업 레어 2종이 레어 15%의 절반을 나눔");
        foreach (var rate in rates.Where(r => r.Tier == GachaTable.Rare && !r.Featured))
            Assert.AreEqual(0.01875, rate.Chance, Tolerance);
        foreach (var rate in rates.Where(r => r.Tier == GachaTable.Common))
            Assert.AreEqual(0.82 / 6, rate.Chance, Tolerance);
        Assert.AreEqual(GachaTable.Epic, rates[0].Tier, "표는 에픽부터");
        Assert.IsTrue(rates[0].Featured, "같은 등급에서는 픽업 먼저");
    }

    [Test]
    public void StandardRates_MatchDesign()
    {
        var table = StandardTable(0.025f, 80);
        var rates = GachaRules.Rates(table);

        Assert.AreEqual(1.0, rates.Sum(r => r.Chance), Tolerance);
        foreach (var rate in rates.Where(r => r.Tier == GachaTable.Epic))
            Assert.AreEqual(0.0125, rate.Chance, Tolerance);
        foreach (var rate in rates.Where(r => r.Tier == GachaTable.Rare))
            Assert.AreEqual(0.0375, rate.Chance, Tolerance);
        foreach (var rate in rates.Where(r => r.Tier == GachaTable.Common))
            Assert.AreEqual(0.825 / 6, rate.Chance, Tolerance);
    }

    [Test]
    public void EmptyTier_FallsDownToNextTier()
    {
        var table = new GachaTable(GachaBannerKind.Standard, 0.03f, 0.15f, 0f, 60, 10);
        table.Add(Toy("rare", _rare), GachaTable.Rare, false);
        table.Add(Toy("common", _common), GachaTable.Common, false);

        Assert.AreEqual(GachaTable.Rare, table.AvailableTier(GachaTable.Epic), "에픽이 없으면 레어");
        Assert.AreEqual(0.18, table.TierChance(GachaTable.Rare), Tolerance, "에픽 몫은 레어가 가져감");
        var roll = GachaRules.Roll(table, new GachaPity(), new ScriptedRandom(0.0));
        Assert.AreEqual(GachaTable.Rare, roll.Tier);
        Assert.AreEqual(1.0, GachaRules.Rates(table).Sum(r => r.Chance), Tolerance);
    }

    #endregion

    #region 천장

    [Test]
    public void EpicPity_EpicOnPityPull_EvenWithWorstLuck()
    {
        var table = StandardTable(0.025f, 80);
        var pity = new GachaPity();
        var unlucky = new ScriptedRandom(0.999); // 늘 흔함이 나오는 운

        for (int pull = 1; pull < 80; pull++)
        {
            var roll = GachaRules.Roll(table, pity, unlucky);
            Assert.AreNotEqual(GachaTable.Epic, roll.Tier, $"{pull}번째");
            Assert.AreEqual(80 - pull, GachaRules.EpicPityLeft(table, pity), "남은 수는 하나씩 줄어듦");
        }
        var pityRoll = GachaRules.Roll(table, pity, unlucky);
        Assert.AreEqual(GachaTable.Epic, pityRoll.Tier, "80번째는 에픽 확정");
        Assert.IsTrue(pityRoll.ByPity);
        Assert.AreEqual(0, pity.SinceEpic, "에픽이 나오면 처음부터");
        Assert.AreEqual(80, GachaRules.EpicPityLeft(table, pity));
    }

    [Test]
    public void RarePity_EveryTenPullsHasRareOrBetter()
    {
        var table = StandardTable(0.025f, 80);
        var pity = new GachaPity();
        var unlucky = new ScriptedRandom(0.999);

        var tiers = Enumerable.Range(0, 30).Select(_ => GachaRules.Roll(table, pity, unlucky).Tier).ToList();
        for (int i = 0; i < tiers.Count; i++)
            Assert.AreEqual((i + 1) % 10 == 0 ? GachaTable.Rare : GachaTable.Common, tiers[i], $"{i + 1}번째");
    }

    [Test]
    public void LuckyRare_ResetsRarePity()
    {
        var table = StandardTable(0.025f, 80);
        var pity = new GachaPity();
        var random = new ScriptedRandom(0.999, 0.999, 0.1, 0.999); // 셋째에 레어 (0.025 ≤ 0.1 < 0.175)

        GachaRules.Roll(table, pity, random);
        GachaRules.Roll(table, pity, random);
        var rare = GachaRules.Roll(table, pity, random);
        Assert.AreEqual(GachaTable.Rare, rare.Tier);
        Assert.IsFalse(rare.ByPity);
        Assert.AreEqual(0, pity.SinceRare);
    }

    [Test]
    public void Pickup_LosingFiftyFifty_GuaranteesNextEpicFeatured()
    {
        var (table, featuredEpic, _) = PickupTable();
        var pity = new GachaPity();
        // 에픽(0.0) → 픽업 반반에서 짐(0.9) → 상시 에픽 고르기(Next 0)
        var lose = GachaRules.Roll(table, pity, new ScriptedRandom(0.0, 0.9));
        Assert.AreEqual(GachaTable.Epic, lose.Tier);
        Assert.IsFalse(lose.Featured);
        Assert.IsTrue(pity.FeaturedGuaranteed, "픽업 에픽을 놓쳐서 다음은 확정");

        // 다음 에픽은 반반을 굴리지 않고 픽업
        var win = GachaRules.Roll(table, pity, new ScriptedRandom(0.0, 0.99));
        Assert.AreEqual(featuredEpic, win.Item);
        Assert.IsTrue(win.Featured);
        Assert.IsFalse(pity.FeaturedGuaranteed, "얻으면 풀림");
    }

    [Test]
    public void Pickup_PityEpic_StillFollowsFiftyFifty()
    {
        var (table, featuredEpic, _) = PickupTable();
        var pity = new GachaPity();
        pity.Set(59, 0, true); // 다음이 60번째 + 픽업 확정
        var roll = GachaRules.Roll(table, pity, new ScriptedRandom(0.999));
        Assert.AreEqual(GachaTable.Epic, roll.Tier, "60번째 에픽");
        Assert.AreEqual(featuredEpic, roll.Item, "놓친 뒤라 픽업 확정");
    }

    [Test]
    public void StandardEpic_DoesNotTouchGuarantee()
    {
        var table = StandardTable(0.025f, 80);
        var pity = new GachaPity();
        GachaRules.Roll(table, pity, new ScriptedRandom(0.0));
        Assert.IsFalse(pity.FeaturedGuaranteed, "픽업이 없는 표에서는 확정 표시를 건드리지 않음");
    }

    [Test]
    public void SameRandom_SameResults()
    {
        var (table, _, _) = PickupTable();
        var a = new GachaPity();
        var b = new GachaPity();
        var randomA = new System.Random(1234);
        var randomB = new System.Random(1234);
        for (int i = 0; i < 200; i++)
            Assert.AreEqual(GachaRules.Roll(table, a, randomA).Item, GachaRules.Roll(table, b, randomB).Item);
    }

    [Test]
    public void ManyPulls_RoughlyMatchRates()
    {
        var table = StandardTable(0.025f, 80);
        var pity = new GachaPity();
        var random = new System.Random(42);
        int epics = 0, rares = 0;
        const int pulls = 200000;
        for (int i = 0; i < pulls; i++)
        {
            int tier = GachaRules.Roll(table, pity, random).Tier;
            if (tier == GachaTable.Epic) epics++;
            else if (tier == GachaTable.Rare) rares++;
        }
        // 천장이 있어 공개 확률보다 조금 높음 (기대값: 에픽 ≈ 2.9%, 레어 ≈ 18%)
        Assert.That(epics / (double)pulls, Is.InRange(0.025, 0.034));
        Assert.That(rares / (double)pulls, Is.InRange(0.15, 0.2));
    }

    #endregion

    #region 배너 · 값 · 결과

    [Test]
    public void TableFromBanner_FeaturedOnlyOnce()
    {
        var pinwheel = Toy("toy_pinwheel", _epic);
        var carousel = Toy("toy_carousel", _epic);
        var ball = Toy("toy_ball", _common);
        var banner = Banner(GachaBannerKind.Pickup, new[] { ball, carousel, pinwheel }, new[] { pinwheel }, "", "");

        var table = GachaTable.From(banner);
        CollectionAssert.AreEqual(new[] { pinwheel }, table.Featured(GachaTable.Epic).ToArray());
        CollectionAssert.AreEqual(new[] { carousel }, table.Standard(GachaTable.Epic).ToArray(), "픽업 목록에 있는 장난감은 상시 칸에서 빠짐");
        CollectionAssert.AreEqual(new[] { ball }, table.Standard(GachaTable.Common).ToArray());
        Assert.AreEqual(0.5f, table.FeaturedShare);
    }

    [Test]
    public void BannerPeriod_IncludesBothDays()
    {
        var banner = Banner(GachaBannerKind.Pickup, new ItemDefinition[0], new ItemDefinition[0], "2026-10-07", "2026-10-20");
        Assert.IsFalse(banner.IsOpenAt(new DateTime(2026, 10, 6, 23, 59, 0)));
        Assert.IsTrue(banner.IsOpenAt(new DateTime(2026, 10, 7, 0, 0, 0)));
        Assert.IsTrue(banner.IsOpenAt(new DateTime(2026, 10, 20, 23, 59, 0)), "끝 날은 24시까지");
        Assert.IsFalse(banner.IsOpenAt(new DateTime(2026, 10, 21, 0, 0, 0)));
        Assert.AreEqual(new DateTime(2026, 10, 21), banner.EndsAt);

        var always = Banner(GachaBannerKind.Standard, new ItemDefinition[0], new ItemDefinition[0], "", "");
        Assert.IsTrue(always.IsOpenAt(new DateTime(2030, 1, 1)));
        Assert.IsNull(always.EndsAt);
    }

    [Test]
    public void GoldPullCost_GrowsWithLevel()
    {
        Assert.AreEqual(1800, GachaRules.GoldPullCost(1000, 800, 1));
        Assert.AreEqual(9000, GachaRules.GoldPullCost(1000, 800, 10));
        Assert.AreEqual(1800, GachaRules.GoldPullCost(1000, 800, 0), "레벨은 최소 1");
    }

    [Test]
    public void DuplicateShards_AndExchangeCosts()
    {
        Assert.AreEqual(1, GachaRules.ShardsFor(GachaTable.Common));
        Assert.AreEqual(5, GachaRules.ShardsFor(GachaTable.Rare));
        Assert.AreEqual(30, GachaRules.ShardsFor(GachaTable.Epic));
        Assert.AreEqual(0, GachaRules.ExchangeCost(GachaTable.Common, false), "흔함은 교환소에 없음");
        Assert.AreEqual(60, GachaRules.ExchangeCost(GachaTable.Rare, false));
        Assert.AreEqual(300, GachaRules.ExchangeCost(GachaTable.Epic, false));
        Assert.AreEqual(600, GachaRules.ExchangeCost(GachaTable.Epic, true), "지난 픽업");
    }

    [Test]
    public void BestIndex_HigherTierThenFeatured()
    {
        var report = new GachaPullReport();
        report.Pulls.Add(Pull(Toy("a", _common), GachaTable.Common, false));
        report.Pulls.Add(Pull(Toy("b", _epic), GachaTable.Epic, false));
        report.Pulls.Add(Pull(Toy("c", _rare), GachaTable.Rare, true));
        report.Pulls.Add(Pull(Toy("d", _epic), GachaTable.Epic, true));
        report.Pulls.Add(Pull(Toy("e", _epic), GachaTable.Epic, false));
        Assert.AreEqual(3, report.BestIndex, "에픽 중 픽업");
    }

    [Test]
    public void SaveRoundTrip_KeepsPityPointsAndDay()
    {
        var go = new GameObject("Gacha");
        try
        {
            var manager = go.AddComponent<GachaManager>();
            var saved = new GachaSaveData
            {
                standardSinceEpic = 33,
                standardSinceRare = 4,
                pickupSinceEpic = 51,
                pickupSinceRare = 7,
                pickupGuaranteed = true,
                pointsBannerId = "pickup_spring_breeze",
                points = 42,
                goldPullDay = 739900,
                goldSinceEpic = 12,
                goldSinceRare = 3,
                welcomeGiftGiven = true,
                totalPulls = 120,
                seenBanners = new List<string> { "pickup_spring_breeze" },
            };
            manager.LoadFromSave(saved);
            Assert.IsTrue(manager.IsLoaded);
            Assert.AreEqual(120, manager.TotalPulls);

            var written = new GachaSaveData();
            manager.WriteToSave(written);
            Assert.AreEqual(JsonUtility.ToJson(saved), JsonUtility.ToJson(written));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OldSave_WithoutGacha_LoadsDefaults()
    {
        var save = JsonUtility.FromJson<SaveData>("{\"schemaVersion\":4,\"farmLevel\":3}");
        Assert.IsNotNull(save.gacha, "옛 세이브도 뽑기 기록은 기본값");
        Assert.AreEqual(0, save.gacha.pickupSinceEpic);
        Assert.IsFalse(save.gacha.welcomeGiftGiven);
    }

    #endregion

    #region 골드 배너

    [Test]
    public void GoldBannerCost_GrowsWithLevel_TenIsNineTimes()
    {
        Assert.AreEqual(750, GachaRules.GoldBannerCost(500, 250, 1, 1, 9));
        Assert.AreEqual(3000, GachaRules.GoldBannerCost(500, 250, 10, 1, 9));
        Assert.AreEqual(27000, GachaRules.GoldBannerCost(500, 250, 10, 10, 9), "10회 = 1회 × 9 (한 번 공짜)");
        Assert.AreEqual(5250, GachaRules.GoldBannerCost(500, 250, 19, 1, 9));
        Assert.AreEqual(750, GachaRules.GoldBannerCost(500, 250, 0, 1, 9), "레벨은 최소 1");
        Assert.AreEqual(7500, GachaRules.GoldBannerCost(500, 250, 1, 10, 99), "10회 값은 최대 ×10");
    }

    [Test]
    public void GoldBanner_NoEpic_RareTenPercent_RarePityEveryTen()
    {
        var table = GoldTable();
        var rates = GachaRules.Rates(table);
        Assert.AreEqual(1.0, rates.Sum(r => r.Chance), Tolerance);
        Assert.AreEqual(0.0, table.TierChance(GachaTable.Epic), Tolerance, "에픽 없음");
        Assert.AreEqual(0.1, table.TierChance(GachaTable.Rare), Tolerance);
        Assert.AreEqual(0.9, table.TierChance(GachaTable.Common), Tolerance);
        Assert.IsFalse(rates.Any(r => r.Tier == GachaTable.Epic));

        var pity = new GachaPity();
        var unlucky = new ScriptedRandom(0.999);
        var tiers = Enumerable.Range(0, 30).Select(_ => GachaRules.Roll(table, pity, unlucky).Tier).ToList();
        for (int i = 0; i < tiers.Count; i++)
            Assert.AreEqual((i + 1) % 10 == 0 ? GachaTable.Rare : GachaTable.Common, tiers[i], $"{i + 1}번째");
        Assert.AreEqual(10, GachaRules.RarePityLeft(table, pity));

        // 아무리 운이 좋아도(0.0) 에픽은 나오지 않음
        var lucky = GachaRules.Roll(table, new GachaPity(), new ScriptedRandom(0.0));
        Assert.AreEqual(GachaTable.Rare, lucky.Tier);
    }

    [Test]
    public void GoldBanner_OnlyGold_PityKeptApart()
    {
        var go = new GameObject("Gacha");
        try
        {
            var manager = go.AddComponent<GachaManager>();
            var gold = Banner(GachaBannerKind.Gold, new[] { Toy("ball", _common) }, new ItemDefinition[0], "", "");
            var standard = Banner(GachaBannerKind.Standard, new[] { Toy("duck", _common) }, new ItemDefinition[0], "", "");
            var so = new SerializedObject(gold);
            so.FindProperty("_dailyGoldPull").boolValue = true;
            so.FindProperty("_epicPity").intValue = 999;
            so.FindProperty("_rarePity").intValue = 10;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(gold.IsGoldBanner);
            Assert.IsFalse(gold.HasDailyGoldPull, "골드 배너에는 하루 한 번 골드 뽑기가 없음");
            Assert.IsFalse(gold.IsPickup);
            Assert.AreEqual(0, gold.ExchangePoints);
            Assert.AreEqual(GachaPayment.Gold, manager.PreferredPayment(gold, 1));
            Assert.AreEqual(GachaPayment.Gold, manager.PreferredPayment(gold, 10));
            Assert.AreEqual(750, manager.Price(gold, 1, GachaPayment.Gold).cost, "프로필이 없으면 Lv.1");
            Assert.AreEqual(6750, manager.Price(gold, 10, GachaPayment.Gold).cost);
            Assert.Throws<ArgumentException>(() => manager.TryPull(gold, 1, GachaPayment.Gem, out _), "골드 배너는 골드로만");

            manager.LoadFromSave(new GachaSaveData { standardSinceRare = 7, goldSinceRare = 2 });
            Assert.AreEqual(8, manager.RarePityLeft(gold), "골드 배너 천장은 골드끼리");
            Assert.AreEqual(3, manager.RarePityLeft(standard));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    #endregion

    #region 한정 해달

    [Test]
    public void FavoriteToyOtter_ComesOnlyWithItsToy_AndFirst()
    {
        var pinwheel = Toy("toy_pinwheel", _epic);
        var ball = Toy("toy_ball", _common);
        var common = Otter("otter_toy_common", 0, null);
        var limited = Otter("otter_toy_sallang", 2, pinwheel);
        var config = Create<SettlementConfig>();
        var so = new SerializedObject(config);
        var list = so.FindProperty("_otters");
        list.arraySize = 2;
        list.GetArrayElementAtIndex(0).objectReferenceValue = common;
        list.GetArrayElementAtIndex(1).objectReferenceValue = limited;
        so.ApplyModifiedPropertiesWithoutUndo();
        var settlement = new Settlement();
        var candidates = new List<SettlementOtterDefinition>();

        ToyVisitRules.CollectCandidates(config, settlement, GachaTable.Epic, candidates);
        CollectionAssert.AreEqual(new[] { common }, candidates, "놓인 장난감을 모르면 한정 해달은 안 옴 (예전 호출)");

        ToyVisitRules.CollectCandidates(config, settlement, GachaTable.Epic, new HashSet<ItemDefinition> { ball }, candidates);
        CollectionAssert.AreEqual(new[] { common }, candidates, "에픽 등급이어도 좋아하는 장난감이 없으면 안 옴");

        ToyVisitRules.CollectCandidates(config, settlement, GachaTable.Common, new HashSet<ItemDefinition> { pinwheel }, candidates);
        CollectionAssert.AreEqual(new[] { common, limited }, candidates, "좋아하는 장난감이 있으면 등급과 상관없이 후보");

        Assert.AreEqual(ToyVisitRules.FavoriteToyWeight * ToyVisitRules.MissingTraitWeight, ToyVisitRules.Weight(limited, GachaTable.Common, new int[11]));
        int limitedPicks = 0;
        for (int serial = 0; serial < 100; serial++)
        {
            if (ToyVisitRules.Pick(candidates, GachaTable.Common, new int[11], serial) == limited)
                limitedPicks++;
        }
        Assert.Greater(limitedPicks, 80, "가중치가 커서 거의 먼저 옴");
    }

    #endregion

    #region 도우미

    // 미리 정한 값을 차례로 돌려주는 난수 (다 쓰면 마지막 값을 계속). 고르기(Next)는 늘 첫 번째
    private sealed class ScriptedRandom : System.Random
    {
        private readonly double[] _values;
        private int _index;

        public ScriptedRandom(params double[] values)
        {
            _values = values;
        }

        public override double NextDouble() => _values[Math.Min(_index++, _values.Length - 1)];

        public override int Next(int maxValue) => 0;
    }

    private (GachaTable table, ItemDefinition featuredEpic, ItemDefinition[] featuredRares) PickupTable()
    {
        var table = new GachaTable(GachaBannerKind.Pickup, 0.03f, 0.15f, 0.5f, 60, 10);
        var featuredEpic = Toy("pinwheel", _epic);
        var featuredRares = new[] { Toy("petal_kite", _rare), Toy("butterfly", _rare) };
        table.Add(featuredEpic, GachaTable.Epic, true);
        foreach (var rare in featuredRares)
            table.Add(rare, GachaTable.Rare, true);
        AddStandard(table);
        return (table, featuredEpic, featuredRares);
    }

    // 골드: 흔함 6 + 가구 4 · 레어 4, 에픽 없음
    private GachaTable GoldTable()
    {
        var table = new GachaTable(GachaBannerKind.Gold, 0f, 0.1f, 0f, 999, 10);
        for (int i = 0; i < 10; i++)
            table.Add(Toy($"common_{i}", _common), GachaTable.Common, false);
        for (int i = 0; i < 4; i++)
            table.Add(Toy($"rare_{i}", _rare), GachaTable.Rare, false);
        return table;
    }

    private GachaTable StandardTable(float epicRate, int epicPity)
    {
        var table = new GachaTable(GachaBannerKind.Standard, epicRate, 0.15f, 0f, epicPity, 10);
        AddStandard(table);
        return table;
    }

    // 상시: 흔함 6 · 레어 4 · 에픽 2
    private void AddStandard(GachaTable table)
    {
        for (int i = 0; i < 6; i++)
            table.Add(Toy($"common_{i}", _common), GachaTable.Common, false);
        for (int i = 0; i < 4; i++)
            table.Add(Toy($"rare_{i}", _rare), GachaTable.Rare, false);
        for (int i = 0; i < 2; i++)
            table.Add(Toy($"epic_{i}", _epic), GachaTable.Epic, false);
    }

    private static GachaPull Pull(ItemDefinition item, int tier, bool featured) =>
        new GachaPull(new GachaRoll(item, tier, featured, false), false, 0);

    private ItemRarity Rarity(string id, int tier)
    {
        var rarity = Create<ItemRarity>();
        var so = new SerializedObject(rarity);
        so.FindProperty("_rarityId").stringValue = id;
        so.FindProperty("_tier").intValue = tier;
        so.ApplyModifiedPropertiesWithoutUndo();
        return rarity;
    }

    private ItemDefinition Toy(string id, ItemRarity rarity)
    {
        var item = Create<ItemDefinition>();
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.FindProperty("_rarity").objectReferenceValue = rarity;
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private GachaBannerDefinition Banner(GachaBannerKind kind, ItemDefinition[] pool, ItemDefinition[] featured, string start, string end)
    {
        var banner = Create<GachaBannerDefinition>();
        var so = new SerializedObject(banner);
        so.FindProperty("_bannerId").stringValue = "test_banner";
        so.FindProperty("_kind").enumValueIndex = (int)kind;
        so.FindProperty("_startDate").stringValue = start;
        so.FindProperty("_endDate").stringValue = end;
        SetList(so.FindProperty("_pool"), pool);
        SetList(so.FindProperty("_featured"), featured);
        so.ApplyModifiedPropertiesWithoutUndo();
        return banner;
    }

    private SettlementOtterDefinition Otter(string id, int tier, ItemDefinition favorite)
    {
        var otter = Create<SettlementOtterDefinition>();
        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.FindProperty("_trait").intValue = (int)OtterTrait.Exploring;
        so.FindProperty("_toyVisitor").boolValue = true;
        so.FindProperty("_visitTier").intValue = tier;
        so.FindProperty("_favoriteToy").objectReferenceValue = favorite;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    private static void SetList(SerializedProperty list, Object[] values)
    {
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    #endregion
}
