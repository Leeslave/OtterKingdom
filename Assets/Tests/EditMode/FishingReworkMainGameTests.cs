using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 낚시 개편 → 본 게임: 옛 낚싯대 레벨 옮기기, 방치 · 꺼 둔 동안 낚시가 강화로 닿는 물고기만 낚음,
/// 바다 낚시가 본 게임 골드 · 가방을 씀 (세이브 안의 테스트 골드 · 가방은 안 씀)
/// </summary>
public class FishingReworkMainGameTests
{
    private const string ConfigPath = "Assets/Scriptable Obejects/FishingRework/FishingReworkConfig.asset";
    private readonly List<Object> _created = new List<Object>();
    private FishingReworkConfig _config;
    private FishingBalanceData _balance;

    private class FakeHost : IFishingHost
    {
        public int GoldAmount;
        public int Earned;
        public readonly List<FishDefinition> Catches = new List<FishDefinition>();
        public int Gold => GoldAmount;

        public bool TrySpendGold(int amount)
        {
            if (amount > GoldAmount)
                return false;
            GoldAmount -= amount;
            return true;
        }

        public void EarnGold(int amount)
        {
            GoldAmount += amount;
            Earned += amount;
        }

        public void AddCatch(FishDefinition fish) => Catches.Add(fish);
    }

    [SetUp]
    public void SetUp()
    {
        _config = AssetDatabase.LoadAssetAtPath<FishingReworkConfig>(ConfigPath);
        if (_config == null)
            Assert.Ignore("낚시 개편 데이터가 없음 (Tools/Fishing Rework/Apply To Main Game)");
        _balance = ScriptableObject.CreateInstance<FishingBalanceData>();
        _balance.rodUpgradeCosts = new int[19];
        _created.Add(_balance);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in _created)
            Object.DestroyImmediate(o);
        _created.Clear();
    }

    [Test]
    public void OldRodLevel_BecomesLineSinkerReelSteps()
    {
        var save = new SaveData { rodLevel = 10 };
        var service = new FishingService(save, _balance, _config);
        Assert.AreEqual(3, save.fishing.lineLevel);
        Assert.AreEqual(3, save.fishing.sinkerLevel);
        Assert.AreEqual(3, save.fishing.reelLevel);
        Assert.AreEqual(10, service.RodLevel, "강화 9번 + 1");

        var max = new SaveData { rodLevel = 20 };
        new FishingService(max, _balance, _config);
        Assert.AreEqual(19, max.fishing.lineLevel + max.fishing.sinkerLevel + max.fishing.reelLevel);

        // 한 번 옮긴 뒤에는 다시 안 옮김 (바다에서 강화한 게 그대로)
        save.fishing.lineLevel = 5;
        new FishingService(save, _balance, _config);
        Assert.AreEqual(5, save.fishing.lineLevel);
    }

    [Test]
    public void NewPlayer_StartsAtTheFirstSteps_AndFishesOfflineOnlyAfterAnUpgrade()
    {
        var save = new SaveData();
        var service = new FishingService(save, _balance, _config);
        Assert.AreEqual(1, service.RodLevel);
        Assert.IsFalse(FishingService.OfflineUnlocked(save, _balance, _config));
        save.fishing.reelLevel = 1;
        Assert.IsTrue(FishingService.OfflineUnlocked(save, _balance, _config));
    }

    [Test]
    public void IdleCatch_OnlyFishTheGearCanReach()
    {
        var levels = new FishingReworkSave();
        var rng = new System.Random(3);
        var caught = new HashSet<string>();
        for (int i = 0; i < 2000; i++)
            caught.Add(FishingService.RollCatch(_config, levels, "trash_basic", rng.NextDouble(), rng.NextDouble()));
        Assert.Contains("trash_basic", new List<string>(caught), "가끔 쓰레기");
        foreach (var id in caught)
        {
            if (id == "trash_basic")
                continue;
            var fish = _config.FindByItem(id);
            Assert.IsNotNull(fish, id);
            Assert.IsTrue(_config.Catchable(fish, 0, 0, 0), $"{fish.DisplayName}는 처음 강화로 못 낚음");
        }
        Assert.IsFalse(caught.Contains("fish_tuna"), "참치는 먼바다");
        Assert.GreaterOrEqual(caught.Count - 1, 4, "처음에도 여러 종류");
    }

    [Test]
    public void Offline_UsesTheSeaTable()
    {
        var save = new SaveData();
        save.fishing.lineLevel = 1;
        var farm = ScriptableObject.CreateInstance<FarmBalanceData>();
        var visits = ScriptableObject.CreateInstance<OtterVisitBalanceData>();
        var mining = ScriptableObject.CreateInstance<MiningBalanceData>();
        _created.Add(farm);
        _created.Add(visits);
        _created.Add(mining);
        var offline = new OfflineProductionService(new CropDefinition[0], farm, _balance, visits, mining, new System.Random(5))
        {
            FishingConfig = _config,
        };
        Assert.IsTrue(offline.IsFishingUnlocked(save), "바다 강화를 한 번 했음");
        Assert.IsFalse(offline.IsFishingUnlocked(new SaveData { rodLevel = 5 }), "옛 낚싯대 레벨이 아니라 바다 강화로 (옛 레벨은 GameManager가 옮김)");
    }

    [Test]
    public void SeaFishing_UsesMainGoldAndBag()
    {
        var host = new FakeHost { GoldAmount = 1000 };
        var save = new FishingReworkSave();
        var model = new FishingReworkModel(_config, save, new System.Random(2), host);
        Assert.IsTrue(model.Hosted);
        Assert.AreEqual(1000, model.Gold);

        int cost = model.NextCost(FishingUpgrade.Line);
        Assert.IsTrue(model.TryUpgrade(FishingUpgrade.Line));
        Assert.AreEqual(1000 - cost, host.GoldAmount, "강화는 본 게임 골드로");
        Assert.AreEqual(0, save.gold);

        // 바늘 길에 물고기를 두고 낚기
        var anchovy = _config.FindByItem("fish_anchovy");
        model.TryCast();
        for (int i = 0; i < 500 && model.Phase == FishingPhase.Casting; i++)
            model.Tick(0.02f, false);
        float down = Mathf.Min(model.MaxDepth, model.SinkSpeed * 2f / model.Config.AutoReel);
        model.PlaceFish(anchovy, model.Hook + new Vector2(-2f, -down), 0.5f);
        FishingCatch result = null;
        model.Caught += c => result = c;
        for (int i = 0; i < 5000 && model.Phase != FishingPhase.Ready; i++)
            model.Tick(0.02f, false);
        Assert.IsNotNull(result, "낚음");
        Assert.Contains(result.Def, host.Catches, "본 게임 가방으로");
        Assert.IsEmpty(save.bag, "테스트 가방은 안 씀");
        Assert.AreEqual(result.Def.Item.SellPrice, result.Price, "값은 아이템 판매가");
    }
}
