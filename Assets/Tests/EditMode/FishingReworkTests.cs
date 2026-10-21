using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 낚시 개편 규칙 (FishingReworkModel): 던지기 거리 · 저절로 감김 · 누르면 빨리 · 찌가 무거우면 깊이 ·
/// 걸리면 낚음(골드 · 도감 · 의뢰) · 무거우면 놓침 · 강화 · 의뢰 받기/바꾸기 · 세이브
/// </summary>
public class FishingReworkTests
{
    private readonly List<Object> _created = new List<Object>();
    private FishingReworkConfig _config;
    private FishDefinition _small, _heavy;

    [SetUp]
    public void SetUp()
    {
        _small = Fish("small", FishRarity.Common, 10, 0.2f, 0.4f, new Vector2(2f, 30f));
        _heavy = Fish("heavy", FishRarity.Rare, 100, 5f, 6f, new Vector2(2f, 30f));
        _config = Track(ScriptableObject.CreateInstance<FishingReworkConfig>());
        _config.SetFish(new List<FishDefinition> { _small, _heavy });
        _config.SetTracks(
            new FishingUpgradeTrack("줄", "거리", "m", null, new[] { 12f, 20f }, new[] { 100 }),
            new FishingUpgradeTrack("찌", "깊이", "m", null, new[] { 4f, 9f }, new[] { 100 }),
            new FishingUpgradeTrack("릴", "무게", "kg", null, new[] { 1f, 8f }, new[] { 100 }));
        _config.SetLayers(new[] { 0, 0, 0, 0 }, new[] { 30f, 30f, 30f, 30f });
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in _created)
            Object.DestroyImmediate(o);
        _created.Clear();
    }

    #region 준비

    private T Track<T>(T o) where T : Object
    {
        _created.Add(o);
        return o;
    }

    private FishDefinition Fish(string id, FishRarity rarity, int price, float minW, float maxW, Vector2 distance)
    {
        var def = Track(ScriptableObject.CreateInstance<FishDefinition>());
        def.Set(id, id, "", null, rarity, price, minW, maxW, distance, new Vector2(0.5f, 3f), false, 100f, 0.5f, 0.6f);
        return def;
    }

    private FishingReworkModel Model(FishingReworkSave save = null) =>
        new FishingReworkModel(_config, save ?? new FishingReworkSave(), new System.Random(1));

    private static float RunUntilReady(FishingReworkModel model, bool holding, float limit = 60f)
    {
        float time = 0f;
        while (model.Phase != FishingPhase.Ready && time < limit)
        {
            model.Tick(0.02f, holding);
            time += 0.02f;
        }
        return time;
    }

    private static void RunUntilInWater(FishingReworkModel model)
    {
        for (int i = 0; i < 500 && model.Phase == FishingPhase.Casting; i++)
            model.Tick(0.02f, false);
    }

    /// <summary>던지고, 물에 닿자마자 바늘 바로 앞에 물고기를 두고, 돌아올 때까지</summary>
    private static void CatchOne(FishingReworkModel model, FishDefinition def, float size = 0.5f)
    {
        Assert.IsTrue(model.TryCast());
        RunUntilInWater(model);
        // 바늘은 저절로 감기며 가라앉음 → 2m 다가왔을 때 있을 자리 (물고기가 사는 가장 얕은 깊이 2.5m보다 아래)
        float down = Mathf.Min(model.MaxDepth, model.SinkSpeed * 2f / model.Config.AutoReel);
        model.PlaceFish(def, model.Hook + new Vector2(-2f, -down), size);
        RunUntilReady(model, false);
    }

    #endregion

    [Test]
    public void Cast_LandsAtLineLength()
    {
        var model = Model();
        Vector2? splash = null;
        model.Splashed += at => splash = at;
        Assert.IsTrue(model.TryCast());
        Assert.IsFalse(model.TryCast(), "날아가는 중에는 다시 못 던짐");
        RunUntilInWater(model);
        Assert.AreEqual(FishingPhase.InWater, model.Phase);
        Assert.IsTrue(splash.HasValue);
        Assert.AreEqual(12f, splash.Value.x, 0.01f, "줄 길이만큼");
    }

    [Test]
    public void Line_ReelsBackByItself_AndHoldingIsFaster()
    {
        var model = Model();
        bool missed = false;
        model.Missed += () => missed = true;
        model.TryCast();
        RunUntilInWater(model);
        float slow = RunUntilReady(model, false);
        Assert.IsTrue(missed, "아무것도 안 걸리면 빈 바늘로 돌아옴");
        Assert.Less(slow, 60f);

        model.TryCast();
        RunUntilInWater(model);
        float fast = RunUntilReady(model, true);
        Assert.Less(fast, slow * 0.7f, "누르고 있으면 더 빨리 감김");
    }

    private static float DeepestDepth(FishingReworkModel model, bool holding)
    {
        model.TryCast();
        RunUntilInWater(model);
        float deepest = 0f;
        while (model.Phase != FishingPhase.Ready)
        {
            model.Tick(0.02f, holding);
            deepest = Mathf.Max(deepest, model.HookDepth);
        }
        return deepest;
    }

    [Test]
    public void HeavierSinker_GoesDeeper_AndHoldingRises()
    {
        var model = Model(new FishingReworkSave { gold = 1000 });
        float released = DeepestDepth(model, false);
        float held = DeepestDepth(model, true);
        Assert.Less(held, released, "누르면 줄이 당겨져 얕게 지나감");

        Assert.IsTrue(model.TryUpgrade(FishingUpgrade.Sinker));
        float heavy = DeepestDepth(model, false);
        Assert.Greater(heavy, released, "찌가 무거우면 더 깊이");
        Assert.AreEqual(4f, released, 0.01f, "찌 깊이까지만");
        Assert.AreEqual(0f, held, 0.01f, "누르고 있으면 수면에");
    }

    [Test]
    public void TouchingAFish_CatchesIt()
    {
        var model = Model();
        FishingCatch result = null;
        model.Caught += c => result = c;
        CatchOne(model, _small);
        Assert.IsNotNull(result);
        Assert.AreSame(_small, result.Def);
        Assert.IsTrue(result.IsNew);
        Assert.AreEqual(0, model.Gold, "골드는 팔 때");
        Assert.AreEqual(1, model.Bag.Count, "가방에 한 마리");
        Assert.AreEqual(_small.PriceAt(0.5f), model.Bag[0].price);
        Assert.AreEqual(result.Weight, model.Bag[0].weight);
        Assert.AreEqual(1, model.CaughtCount(_small));
        Assert.AreEqual(1, model.TotalCaught);
        Assert.IsEmpty(model.Fish, "낚은 물고기는 바다에서 빠짐");
    }

    [Test]
    public void Fish_StayBelowTheSurface_SoNothingBitesAtTheSplash()
    {
        var model = Model();
        var fish = model.PlaceFish(_small, new Vector2(12f, -0.3f), 0.5f);
        bool hooked = false;
        model.Hooked += f => hooked = true;
        model.TryCast();
        RunUntilInWater(model);
        model.Tick(0.05f, true);
        Assert.LessOrEqual(fish.Position.y, -_config.FishTopDepth + 0.001f);
        Assert.IsFalse(hooked, "물에 닿자마자 걸리지 않음");
    }

    [Test]
public void Zones_StayUnderTheirCap_AndRefillOneAtATime()
    {
        _config.SetLayers(new[] { 2, 0, 0, 0 }, new[] { 30f, 30f, 30f, 30f });
        var model = Model(new FishingReworkSave { reelLevel = 1 });
        Assert.AreEqual(2, model.ZoneCount(0, FishLayer.Shallow), "처음엔 가득");
        model.Tick(100f, false);
        Assert.AreEqual(2, model.ZoneCount(0, FishLayer.Shallow), "가득이면 더 안 옴");

        // 칸 0(1.5~9.5m)의 얕은 물고기를 하나씩 바늘 길에 옮겨 낚음
        for (int i = 0; i < 4 && model.ZoneCount(0, FishLayer.Shallow) > 0; i++)
        {
            FishingFish target = null;
            foreach (var fish in model.Fish)
            {
                if (fish.Segment == 0)
                    target = fish;
            }
            model.TryCast();
            RunUntilInWater(model);
            float down = Mathf.Min(model.MaxDepth, model.SinkSpeed * 2f / model.Config.AutoReel);
            target.Position = model.Hook + new Vector2(-2f, -down);
            target.BaseY = target.Position.y;
            target.HomeX = target.Position.x;
            target.Range = 0.01f;
            RunUntilReady(model, false);
        }
        Assert.AreEqual(0, model.ZoneCount(0, FishLayer.Shallow), "비움");
        model.Tick(0.5f, false);
        Assert.AreEqual(0, model.ZoneCount(0, FishLayer.Shallow), "바로 안 옴");
        model.Tick(30f, false);
        Assert.AreEqual(1, model.ZoneCount(0, FishLayer.Shallow), "한 마리씩");
        model.Tick(30f, false);
        Assert.AreEqual(2, model.ZoneCount(0, FishLayer.Shallow), "30초 뒤 또 한 마리");
        model.Tick(100f, false);
        Assert.AreEqual(2, model.ZoneCount(0, FishLayer.Shallow), "최대까지만");
    }
    [Test]
    public void Bag_SellsOneByOne_OrAll()
    {
        var model = Model();
        CatchOne(model, _small, 0f);
        model.Tick(10f, false);
        CatchOne(model, _small, 1f);
        Assert.AreEqual(2, model.Bag.Count);
        var big = model.Bag[1];
        Assert.AreEqual(_small.PriceAt(1f), model.Sell(big), "한 마리씩 팔림 (무게마다 값이 다름)");
        Assert.AreEqual(_small.PriceAt(1f), model.Gold);
        Assert.AreEqual(0, model.Sell(big), "이미 팜");
        Assert.AreEqual(_small.PriceAt(0f), model.SellAll());
        Assert.IsEmpty(model.Bag);
        Assert.AreEqual(_small.PriceAt(1f) + _small.PriceAt(0f), model.Gold);
    }

    [Test]
    public void TooHeavyFish_SlipsAway_UntilTheReelIsStronger()
    {
        var model = Model(new FishingReworkSave { gold = 1000 });
        FishingFish heavy = null;
        FishingCatch result = null;
        model.TooHeavy += f => heavy = f;
        model.Caught += c => result = c;
        CatchOne(model, _heavy);
        Assert.IsNotNull(heavy, "릴(1kg)보다 무거움");
        Assert.IsNull(result);

        Assert.IsTrue(model.TryUpgrade(FishingUpgrade.Reel));
        model.Tick(10f, false);
        CatchOne(model, _heavy);
        Assert.IsNotNull(result, "릴 8kg이면 낚임");
        Assert.AreSame(_heavy, result.Def);
    }

    [Test]
    public void Upgrade_SpendsGold_AndStopsAtMax()
    {
        var model = Model(new FishingReworkSave { gold = 150 });
        Assert.IsTrue(model.TryUpgrade(FishingUpgrade.Line));
        Assert.AreEqual(50, model.Gold);
        Assert.AreEqual(20f, model.CastDistance);
        Assert.IsTrue(model.IsMax(FishingUpgrade.Line));
        Assert.IsFalse(model.TryUpgrade(FishingUpgrade.Line), "끝");
        Assert.IsFalse(model.TryUpgrade(FishingUpgrade.Reel), "골드 모자람");
        Assert.AreEqual(50, model.Gold);
    }

    [Test]
    public void Request_CountsTheRightFish_ThenClaimGivesANewOne()
    {
        var model = Model();
        var request = model.Request;
        Assert.IsNotNull(request);
        Assert.AreEqual(FishingRequestKind.Fish, request.Kind, "닿는 물고기가 하나뿐 (무거운 건 릴이 모자람)");
        Assert.AreSame(_small, request.Fish);
        Assert.IsFalse(model.ClaimRequest(), "아직 못 함");

        for (int i = 0; i < request.Needed; i++)
        {
            model.Tick(10f, false);
            CatchOne(model, _small);
        }
        Assert.IsTrue(request.Done);
        int before = model.Gold;
        Assert.IsTrue(model.ClaimRequest());
        Assert.AreEqual(before + request.Reward, model.Gold);
        Assert.AreNotSame(request, model.Request, "새 의뢰");
        Assert.AreEqual(1, model.Save.requestsDone);
    }

    [Test]
    public void Request_SkipHasACooldown()
    {
        var model = Model();
        Assert.IsTrue(model.SkipRequest());
        Assert.IsFalse(model.SkipRequest(), "잠깐 쉬어야 함");
        model.Tick(_config.RequestSkipCooldown + 1f, false);
        Assert.IsTrue(model.SkipRequest());
    }

    [Test]
    public void Save_KeepsGoldLevelsBookAndRequest()
    {
        var save = new FishingReworkSave { gold = 500 };
        var model = Model(save);
        model.TryUpgrade(FishingUpgrade.Sinker);
        CatchOne(model, _small);
        var json = JsonUtility.ToJson(model.Save);

        var again = Model(JsonUtility.FromJson<FishingReworkSave>(json));
        Assert.AreEqual(model.Gold, again.Gold);
        Assert.AreEqual(1, again.Level(FishingUpgrade.Sinker));
        Assert.AreEqual(1, again.CaughtCount(_small));
        Assert.AreEqual(1, again.Bag.Count, "가방도");
        Assert.AreEqual(model.Bag[0].price, again.Bag[0].price);
        Assert.AreEqual(model.Request.Kind, again.Request.Kind);
        Assert.AreEqual(model.Request.Reward, again.Request.Reward);
        Assert.AreEqual(model.Request.Progress, again.Request.Progress);
    }
}
