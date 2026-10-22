using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 낚시 개편 규칙 (FishingReworkModel): 던지기 거리(고르기 · 기억) · 저절로 감김(물고기 근처는 천천히) · 누르면 올라옴 · 찌가 무거우면 깊이 ·
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

    // 다음 강화 재료(물고기)를 가방에 넣어 줌
    private static void GiveRecipe(FishingReworkModel model, FishingUpgrade upgrade)
    {
        foreach (var cost in model.NextRecipe(upgrade))
        {
            for (int i = 0; i < cost.Amount; i++)
                model.Save.bag.Add(new FishingBagSave { fishId = cost.Fish.FishId, weight = 0.3f, price = cost.Fish.Price });
        }
    }

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
    public void Line_ReelsBackByItself_AndHoldingOnlyLifts()
    {
        var model = Model();
        bool missed = false;
        model.Missed += () => missed = true;
        model.TryCast();
        RunUntilInWater(model);
        float released = RunUntilReady(model, false);
        Assert.IsTrue(missed, "아무것도 안 걸리면 빈 바늘로 돌아옴");
        Assert.Less(released, 60f);

        model.TryCast();
        RunUntilInWater(model);
        float held = RunUntilReady(model, true);
        Assert.AreEqual(released, held, 0.5f, "누르면 올라오기만 함 (앞으로 빨라지지 않음 — 깊이를 맞추다 지나치지 않게)");
    }

    [Test]
    public void Cast_ToChosenDistance_IsRemembered_AndFullFollowsTheLine()
    {
        var model = Model(new FishingReworkSave());
        Assert.AreEqual(12f, model.LastCastDistance, 0.01f, "처음은 끝까지");
        Vector2? splash = null;
        model.Splashed += at => splash = at;
        Assert.IsTrue(model.TryCast(7f));
        RunUntilInWater(model);
        Assert.AreEqual(7f, splash.Value.x, 0.01f, "고른 거리로");
        RunUntilReady(model, false);
        Assert.AreEqual(7f, model.LastCastDistance, 0.01f, "탭은 지난 거리로");
        Assert.IsTrue(model.TryCast(99f));
        RunUntilInWater(model);
        Assert.AreEqual(12f, splash.Value.x, 0.01f, "줄 길이를 넘지 않음");
        RunUntilReady(model, false);

        // 끝까지 던졌으면 줄을 강화한 뒤에도 끝까지
        GiveRecipe(model, FishingUpgrade.Line);
        Assert.IsTrue(model.TryUpgrade(FishingUpgrade.Line));
        Assert.AreEqual(20f, model.LastCastDistance, 0.01f);
        Assert.IsTrue(model.TryCast(1f));
        RunUntilInWater(model);
        Assert.AreEqual(FishingReworkModel.MinCastDistance, splash.Value.x, 0.01f, "너무 가까우면 가장 가까운 거리로");
    }

    [Test]
    public void Reel_SlowsDownNearCatchableFish()
    {
        var model = Model();
        model.TryCast();
        RunUntilInWater(model);
        float startX = model.Hook.x;
        model.Tick(0.5f, false);
        float freeSpeed = (startX - model.Hook.x) / 0.5f;
        Assert.IsFalse(model.NearCatchableFish);

        // 바늘 바로 앞(선착장 쪽)에 낚을 수 있는 물고기
        model.PlaceFish(_small, new Vector2(model.Hook.x - 2f, model.Hook.y - 3f), 0.5f);
        Assert.IsTrue(model.NearCatchableFish);
        float x = model.Hook.x;
        model.Tick(0.1f, false);
        float nearSpeed = (x - model.Hook.x) / 0.1f;
        Assert.LessOrEqual(nearSpeed, model.Config.NearFishReel + 0.01f, "물고기 근처는 천천히");
        Assert.LessOrEqual(nearSpeed, freeSpeed + 0.01f);
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

        GiveRecipe(model, FishingUpgrade.Sinker);
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
    public void Pin_IsPlacedByHand_StaysInLineReach_AndIsSaved()
    {
        var save = new FishingReworkSave();
        var model = Model(save);
        Assert.AreEqual(0f, model.PinDistance, "처음은 핀 없음");

        model.SetPin(8f);
        Assert.AreEqual(8f, model.PinDistance, 0.01f);
        CatchOne(model, _small);
        Assert.AreEqual(8f, model.PinDistance, 0.01f, "낚아도 핀은 그대로 (직접 꽂은 것만)");
        Assert.AreEqual(8f, Model(save).PinDistance, 0.01f, "저장됨");

        model.SetPin(99f);
        Assert.AreEqual(model.CastDistance, model.PinDistance, 0.01f, "줄 길이를 넘지 않음");
        model.ClearPin();
        Assert.AreEqual(0f, model.PinDistance, "핀 빼기");
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

        GiveRecipe(model, FishingUpgrade.Reel);
        Assert.IsTrue(model.TryUpgrade(FishingUpgrade.Reel));
        model.Tick(10f, false);
        CatchOne(model, _heavy);
        Assert.IsNotNull(result, "릴 8kg이면 낚임");
        Assert.AreSame(_heavy, result.Def);
    }

    [Test]
    public void Upgrade_SpendsFishFromTheBag_NotGold_AndStopsAtMax()
    {
        var model = Model(new FishingReworkSave { gold = 150 });
        var recipe = model.NextRecipe(FishingUpgrade.Line);
        Assert.IsNotEmpty(recipe, "재료는 물고기");
        Assert.AreSame(_small, recipe[0].Fish, "처음 강화는 지금 낚을 수 있는 물고기로 (무거운 물고기는 릴이 못 버팀)");
        Assert.IsFalse(model.CanUpgrade(FishingUpgrade.Line), "재료가 없음");
        Assert.IsFalse(model.TryUpgrade(FishingUpgrade.Line));

        GiveRecipe(model, FishingUpgrade.Line);
        model.Save.bag.Add(new FishingBagSave { fishId = _heavy.FishId, weight = 5f, price = 100 });
        Assert.IsTrue(model.TryUpgrade(FishingUpgrade.Line));
        Assert.AreEqual(150, model.Gold, "골드는 안 씀");
        Assert.AreEqual(1, model.Bag.Count, "재료만 냄 (다른 물고기는 그대로)");
        Assert.AreEqual(20f, model.CastDistance);
        Assert.IsTrue(model.IsMax(FishingUpgrade.Line));
        Assert.IsEmpty(model.NextRecipe(FishingUpgrade.Line), "끝이면 재료 없음");
        Assert.IsFalse(model.TryUpgrade(FishingUpgrade.Line), "끝");
    }

    [Test]
    public void CaughtMaterial_IsReportedWithProgress()
    {
        var model = Model();
        var cost = model.NextRecipe(FishingUpgrade.Line)[0];
        Assert.IsTrue(model.TryNeededFor(cost.Fish, out _, out int owned, out int needed));
        Assert.AreEqual(0, owned);
        Assert.GreaterOrEqual(needed, 1, "여러 강화에 쓰이면 그중 하나의 필요한 수");
        CatchOne(model, cost.Fish);
        Assert.IsTrue(model.TryNeededFor(cost.Fish, out _, out owned, out _));
        Assert.AreEqual(1, owned, "낚은 물고기가 재료로 셈");
        Assert.IsTrue(model.TryMissingFor(_heavy, out var missing), "무거운 물고기는 지금 못 낚음");
        Assert.AreEqual(FishingUpgrade.Reel, missing, "릴을 먼저");
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
        GiveRecipe(model, FishingUpgrade.Sinker);
        Assert.IsTrue(model.TryUpgrade(FishingUpgrade.Sinker));
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
