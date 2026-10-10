using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 채소 · 물고기 확장 (Docs/채소_물고기_확장.md): 물고기 표(낚싯대 레벨로 열림 · 레어/에픽 무게가 레벨로 커짐 · 쓰레기 · 확률 끝),
/// 표가 비면 예전 물고기 하나, 밭 · 낚싯대 강화 20레벨.
/// </summary>
public class HarvestExpansionTests
{
    private FishingBalanceData _balance;

    [SetUp]
    public void SetUp()
    {
        _balance = ScriptableObject.CreateInstance<FishingBalanceData>();
        _balance.fishTable = new[]
        {
            new FishingBalanceData.FishEntry("fish_common", 0, 1, 100),
            new FishingBalanceData.FishEntry("fish_rare", 1, 5, 15),
            new FishingBalanceData.FishEntry("fish_epic", 2, 10, 3),
        };
        _balance.rareLuckPerRodLevel = 0.05f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_balance);
    }

    [Test]
    public void Fish_LockedUntilRodLevel()
    {
        for (int i = 0; i < 100; i++)
            Assert.AreEqual("fish_common", _balance.RollFish(1, i / 100.0), "Lv.1은 흔함만");
        Assert.AreEqual(0f, _balance.WeightAt(_balance.fishTable[1], 4), "레어는 Lv.5부터");
        Assert.AreEqual("fish_rare", _balance.RollFish(5, 0.999), "Lv.5면 마지막 몫이 레어");
        Assert.AreEqual("fish_epic", _balance.RollFish(10, 0.9999), "Lv.10이면 마지막 몫이 에픽");
        Assert.AreEqual(1, _balance.MinRodLevelOf("fish_common"));
        Assert.AreEqual(10, _balance.MinRodLevelOf("fish_epic"));
        CollectionAssert.AreEqual(new[] { "fish_rare" }, _balance.FirstCaughtAt(5).Select(f => f.itemId).ToArray());
        Assert.IsEmpty(_balance.FirstCaughtAt(6));
    }

    [Test]
    public void RareWeight_GrowsWithRodLevel_CommonDoesNot()
    {
        Assert.AreEqual(15f * 1.2f, _balance.WeightAt(_balance.fishTable[1], 5), 1e-4);
        Assert.AreEqual(15f * 1.95f, _balance.WeightAt(_balance.fishTable[1], 20), 1e-4);
        Assert.AreEqual(100f, _balance.WeightAt(_balance.fishTable[0], 20), 1e-4, "흔함은 그대로");

        int rare5 = 0, rare20 = 0;
        for (int i = 0; i < 10000; i++)
        {
            double roll = (i + 0.5) / 10000.0;
            if (_balance.RollFish(5, roll) == "fish_rare") rare5++;
            if (_balance.RollFish(20, roll) == "fish_rare") rare20++;
        }
        Assert.Greater(rare20, rare5, "레벨이 높으면 레어가 더 자주");
    }

    [Test]
    public void Catch_TrashAboveFishChance_AndChanceCapped()
    {
        _balance.baseFishChance = 0.5f;
        _balance.fishChancePerRodLevel = 0.05f;
        _balance.maxFishChance = 0.95f;
        Assert.AreEqual(0.95f, _balance.FishChanceAt(20), 1e-5, "확률 끝");
        Assert.AreEqual(0.95f, _balance.FishChanceAt(10), 1e-5);
        Assert.AreEqual(_balance.trashItemId, _balance.RollCatch(1, 0.6, 0.1), "물고기 확률(50%)보다 크면 쓰레기");
        Assert.AreEqual("fish_common", _balance.RollCatch(1, 0.4, 0.1));
        Assert.IsTrue(_balance.IsFish("fish_epic"));
        Assert.IsFalse(_balance.IsFish(_balance.trashItemId));
    }

    [Test]
    public void EmptyTable_FallsBackToSingleFish()
    {
        var old = ScriptableObject.CreateInstance<FishingBalanceData>();
        try
        {
            Assert.IsFalse(old.HasFishTable);
            Assert.AreEqual(old.fishItemId, old.RollFish(20, 0.7));
            Assert.IsTrue(old.IsFish(old.fishItemId));
            Assert.AreEqual(1f, old.maxFishChance, "기본은 끝 없음 (예전 동작)");
        }
        finally
        {
            Object.DestroyImmediate(old);
        }
    }

    [Test]
    public void FarmAndRod_UpgradeToLevel20()
    {
        var farm = ScriptableObject.CreateInstance<FarmBalanceData>();
        try
        {
            Assert.AreEqual(20, farm.maxFarmLevel);
            Assert.AreEqual(19, farm.upgradeCostByLevel.Length, "Lv.1→20 강화 19번");
            Assert.AreEqual(20, farm.durationMultiplierByLevel.Length);
            for (int i = 1; i < farm.durationMultiplierByLevel.Length; i++)
                Assert.Less(farm.durationMultiplierByLevel[i], farm.durationMultiplierByLevel[i - 1], "레벨마다 더 빨리 자람");
            for (int i = 1; i < farm.upgradeCostByLevel.Length; i++)
                Assert.Greater(farm.upgradeCostByLevel[i], farm.upgradeCostByLevel[i - 1]);
            Assert.AreEqual(0.30f, farm.DurationMultiplierAt(20), 1e-5);
            Assert.AreEqual(0.30f, farm.DurationMultiplierAt(99), 1e-5, "끝을 넘으면 마지막 값");
        }
        finally
        {
            Object.DestroyImmediate(farm);
        }

        _balance.rodUpgradeCosts = new int[19];
        var service = new FishingService(new SaveData { rodLevel = 25 }, _balance);
        Assert.AreEqual(20, service.MaxRodLevel);
        Assert.AreEqual(20, service.RodLevel, "세이브의 레벨은 끝으로 맞춤");
    }
}
