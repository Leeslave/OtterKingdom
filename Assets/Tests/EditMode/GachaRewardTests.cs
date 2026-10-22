using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 가챠 개편 1단계 (Docs/가챠개편_특수해달조각_장신구_기획.md): 새 보상 표(재료 · 장신구 · 해달 조각)의 칸 고르기 · 천장 · 픽업 · 빈 칸 몫,
/// 조각 → 해금 → 별 승급(남는 조각은 반짝 조각), 편성(같은 버프 한 마리), 장신구(강화 · 끼우기), 세이브, 실제 데이터 에셋.
/// </summary>
public class GachaRewardTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    private SpecialOtterDefinition Otter(string id, int tier, KingdomBonusKind bonus)
    {
        var otter = ScriptableObject.CreateInstance<SpecialOtterDefinition>();
        otter.Setup(id, id, tier, bonus, new[] { 4, 6, 8, 10, 12 });
        _created.Add(otter);
        return otter;
    }

    private AccessoryDefinition Accessory(string id, int tier, AccessoryEffect effect = AccessoryEffect.MoveSpeed, int percent = 8)
    {
        var accessory = ScriptableObject.CreateInstance<AccessoryDefinition>();
        accessory.Setup(id, id, tier, effect, percent);
        _created.Add(accessory);
        return accessory;
    }

    private static readonly GachaRewardRates PlanRates = new GachaRewardRates(0.55, 0.22, 0.07, 0.01, 0.12, 0.03);

    private GachaRewardTable FullTable(GachaBannerKind kind = GachaBannerKind.Standard, float featuredShare = 0f, int epicPity = 80, int rarePity = 10)
    {
        var table = new GachaRewardTable(kind, PlanRates, epicPity, rarePity, featuredShare, (1, 3), (1, 2), 5);
        table.AddBundle(20, 15, 1);
        table.AddAccessory(Accessory("hat_common", GachaTable.Common));
        table.AddAccessory(Accessory("hat_rare", GachaTable.Rare));
        table.AddAccessory(Accessory("hat_epic", GachaTable.Epic));
        table.AddOtter(Otter("bard", GachaTable.Rare, KingdomBonusKind.MoveSpeed), false);
        table.AddOtter(Otter("architect", GachaTable.Epic, KingdomBonusKind.BuildSpeed), false);
        return table;
    }

    #region 보상 표 · 뽑기

    [Test]
    public void Rates_MustSumToOne()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new GachaRewardRates(0.5, 0.2, 0.07, 0.01, 0.12, 0.03));
        Assert.DoesNotThrow(() => new GachaRewardRates(0.55, 0.22, 0.07, 0.01, 0.12, 0.03));
    }

    [Test]
    public void Roll_PicksSlotByRate()
    {
        var table = FullTable();
        // 쌓는 순서: 재료 0.55 → 일반 장신구 0.77 → 레어 장신구 0.84 → 레어 조각 0.96 → 에픽 장신구 0.97 → 에픽 조각 1
        Assert.AreEqual(GachaRewardKind.Materials, RollAt(table, 0.10).Kind);
        var common = RollAt(table, 0.60);
        Assert.AreEqual(GachaRewardKind.Accessory, common.Kind);
        Assert.AreEqual(GachaTable.Common, common.Tier);
        Assert.AreEqual(GachaTable.Rare, RollAt(table, 0.80).Accessory.Tier);
        var fragments = RollAt(table, 0.90);
        Assert.AreEqual(GachaRewardKind.Fragments, fragments.Kind);
        Assert.AreEqual("bard", fragments.Otter.OtterId);
        Assert.That(fragments.Fragments, Is.InRange(1, 3));
        Assert.AreEqual(GachaTable.Epic, RollAt(table, 0.965).Accessory.Tier);
        var epic = RollAt(table, 0.99);
        Assert.AreEqual("architect", epic.Otter.OtterId);
        Assert.That(epic.Fragments, Is.InRange(1, 2));
    }

    private static GachaReward RollAt(GachaRewardTable table, double roll) =>
        GachaRewardRules.Roll(table, new GachaPity(), new ScriptedRandom(roll));

    [Test]
    public void Materials_ComeFromBundles()
    {
        var reward = RollAt(FullTable(), 0.0);
        Assert.AreEqual(20, reward.Wood);
        Assert.AreEqual(15, reward.Stone);
    }

    [Test]
    public void RarePity_TenthIsRareOrBetter()
    {
        var table = FullTable();
        var pity = new GachaPity();
        for (int i = 0; i < 9; i++)
            Assert.IsFalse(GachaRewardRules.Roll(table, pity, new ScriptedRandom(0.0)).IsRareOrBetter);
        // 10번째: 흔함이 나와도 레어 이상 칸(레어 장신구 · 레어 조각 · 에픽 둘) 중 확률대로 — 0.0은 레어 장신구
        var tenth = GachaRewardRules.Roll(table, pity, new ScriptedRandom(0.0, 0.0));
        Assert.IsTrue(tenth.IsRareOrBetter);
        Assert.IsTrue(tenth.ByPity);
        Assert.AreEqual(0, pity.SinceRare);
    }

    [Test]
    public void EpicPity_GivesEpicFragments()
    {
        var table = FullTable(epicPity: 3, rarePity: 100);
        var pity = new GachaPity();
        GachaRewardRules.Roll(table, pity, new ScriptedRandom(0.0));
        GachaRewardRules.Roll(table, pity, new ScriptedRandom(0.0));
        var third = GachaRewardRules.Roll(table, pity, new ScriptedRandom(0.0));
        Assert.AreEqual(GachaRewardKind.Fragments, third.Kind);
        Assert.AreEqual(GachaTable.Epic, third.Tier);
        Assert.AreEqual(5, third.Fragments, "천장은 에픽 조각 5개");
        Assert.IsTrue(third.ByPity);
        Assert.AreEqual(0, pity.SinceEpic);
    }

    [Test]
    public void EpicAccessory_DoesNotResetEpicPity()
    {
        var table = FullTable();
        var pity = new GachaPity();
        pity.Set(50, 0, false);
        Assert.AreEqual(GachaTable.Epic, GachaRewardRules.Roll(table, pity, new ScriptedRandom(0.965)).Accessory.Tier);
        Assert.AreEqual(51, pity.SinceEpic, "에픽 천장은 에픽 해달 조각을 얻을 때만 다시 셈");
        Assert.AreEqual(0, pity.SinceRare);
    }

    [Test]
    public void EmptySlot_FallsToSameTierThenLower()
    {
        var table = new GachaRewardTable(GachaBannerKind.Standard, PlanRates, 80, 10, 0f, (1, 3), (1, 2), 5);
        table.AddBundle(20, 15, 1);
        table.AddAccessory(Accessory("hat_common", GachaTable.Common));
        table.AddAccessory(Accessory("hat_rare", GachaTable.Rare));
        table.AddOtter(Otter("bard", GachaTable.Rare, KingdomBonusKind.MoveSpeed), false);
        // 에픽이 하나도 없음: 에픽 칸 몫은 레어로 (같은 등급 다른 종류가 없어 아래 등급, 레어 장신구가 먼저)
        Assert.AreEqual((GachaRewardKind.Accessory, GachaTable.Rare), GachaRewardRules.Resolve(table, GachaRewardKind.Fragments, GachaTable.Epic));
        Assert.AreEqual(0.0, GachaRewardRules.Chance(table, GachaRewardKind.Fragments, GachaTable.Epic));
        Assert.AreEqual(0.07 + 0.01 + 0.03, GachaRewardRules.Chance(table, GachaRewardKind.Accessory, GachaTable.Rare), 1e-9);
        double sum = 0;
        foreach (var (kind, tier) in GachaRewardRules.Slots)
            sum += GachaRewardRules.Chance(table, kind, tier);
        Assert.AreEqual(1.0, sum, 1e-9);
        // 에픽 천장도 에픽 조각이 없으면 걸지 않음
        var pity = new GachaPity();
        pity.Set(200, 0, false);
        Assert.AreEqual(GachaRewardKind.Materials, GachaRewardRules.Roll(table, pity, new ScriptedRandom(0.0)).Kind);
    }

    [Test]
    public void Pickup_MissedEpicMakesNextFeatured()
    {
        var table = new GachaRewardTable(GachaBannerKind.Pickup, PlanRates, 60, 10, 0.5f, (1, 3), (1, 2), 5);
        table.AddBundle(20, 15, 1);
        var featured = Otter("pickup", GachaTable.Epic, KingdomBonusKind.SellPrice);
        table.AddOtter(featured, true);
        table.AddOtter(Otter("standard", GachaTable.Epic, KingdomBonusKind.BuildSpeed), false);
        var pity = new GachaPity();

        // 0.99 = 에픽 조각, 0.9 ≥ 0.5 → 픽업을 놓침
        var miss = GachaRewardRules.Roll(table, pity, new ScriptedRandom(0.99, 0.9));
        Assert.IsFalse(miss.Featured);
        Assert.IsTrue(pity.FeaturedGuaranteed);
        var hit = GachaRewardRules.Roll(table, pity, new ScriptedRandom(0.99, 0.9));
        Assert.IsTrue(hit.Featured, "놓친 다음 에픽 조각은 픽업 확정");
        Assert.AreEqual(featured, hit.Otter);
        Assert.IsFalse(pity.FeaturedGuaranteed);
    }

    [Test]
    public void Rates_SumToOneAndSplitFeatured()
    {
        var table = new GachaRewardTable(GachaBannerKind.Pickup, PlanRates, 60, 10, 0.5f, (1, 3), (1, 2), 5);
        table.AddBundle(20, 15, 3);
        table.AddBundle(50, 40, 1);
        table.AddAccessory(Accessory("hat_common", GachaTable.Common));
        table.AddAccessory(Accessory("gloves_common", GachaTable.Common));
        table.AddAccessory(Accessory("hat_rare", GachaTable.Rare));
        table.AddAccessory(Accessory("hat_epic", GachaTable.Epic));
        table.AddOtter(Otter("bard", GachaTable.Rare, KingdomBonusKind.MoveSpeed), false);
        table.AddOtter(Otter("pickup", GachaTable.Epic, KingdomBonusKind.SellPrice), true);
        table.AddOtter(Otter("architect", GachaTable.Epic, KingdomBonusKind.BuildSpeed), false);

        var rates = GachaRewardRules.Rates(table);
        double sum = 0;
        foreach (var rate in rates)
            sum += rate.Chance;
        Assert.AreEqual(1.0, sum, 1e-9);
        Assert.AreEqual(GachaTable.Epic, rates[0].Tier, "에픽부터");
        var pickup = rates.Find(r => r.Featured);
        Assert.AreEqual("pickup 조각", pickup.Name);
        Assert.AreEqual(0.015, pickup.Chance, 1e-9, "에픽 조각 3%의 절반");
        Assert.AreEqual(0.55 * 0.75, rates.Find(r => r.Name == "목재 20 · 돌 15").Chance, 1e-9, "꾸러미 무게 3 : 1");
        Assert.AreEqual(0.11, rates.Find(r => r.Name == "hat_common").Chance, 1e-9, "일반 장신구 22%를 둘이 나눔");
        Assert.AreEqual(0.04, GachaRewardRules.TierChance(table, GachaTable.Epic), 1e-9);
    }

    #endregion

    #region 뽑기 매니저 (보상 설정 연결)

    [Test]
    public void Manager_UsesRewardConfig_GoldBannerRetired()
    {
        var bard = Otter("bard", GachaTable.Rare, KingdomBonusKind.MoveSpeed);
        var architect = Otter("architect", GachaTable.Epic, KingdomBonusKind.BuildSpeed);
        var config = ScriptableObject.CreateInstance<GachaRewardConfig>();
        _created.Add(config);
        config.Setup(null, null, new[] { new GachaRewardConfig.MaterialBundle { wood = 20, stone = 15, weight = 1 } },
            new[] { bard, architect }, new[] { Accessory("hat", GachaTable.Common) });

        var standard = Banner("standard", GachaBannerKind.Standard, null);
        var pickup = Banner("pickup", GachaBannerKind.Pickup, architect);
        var gold = Banner("gold", GachaBannerKind.Gold, null);

        var go = new GameObject("Gacha");
        try
        {
            var manager = go.AddComponent<GachaManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("_rewardConfig").objectReferenceValue = config;
            var banners = so.FindProperty("_banners");
            banners.arraySize = 3;
            banners.GetArrayElementAtIndex(0).objectReferenceValue = standard;
            banners.GetArrayElementAtIndex(1).objectReferenceValue = pickup;
            banners.GetArrayElementAtIndex(2).objectReferenceValue = gold;
            so.ApplyModifiedPropertiesWithoutUndo();
            manager.LoadFromSave(new GachaSaveData());

            var open = manager.OpenBanners();
            CollectionAssert.AreEquivalent(new[] { standard, pickup }, open, "골드 배너는 정리");
            Assert.AreEqual(1, manager.Table(pickup).Featured(GachaTable.Epic).Count);
            Assert.AreEqual(0, manager.Table(standard).Featured(GachaTable.Epic).Count, "상시 배너에는 픽업 없음");
            Assert.AreEqual(architect, manager.PointsReward(pickup));
            Assert.IsNull(manager.PointsReward(standard));

            var entries = manager.ExchangeEntries();
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(architect, entries[0].Otter, "에픽 먼저");
            Assert.AreEqual(CompanionCollection.EpicExchangeShards * config.ExchangeFragments, entries[0].Cost);
            manager.Companions.AddFragments(bard, CompanionCollection.CostToMax(bard, 0));
            Assert.AreEqual(1, manager.ExchangeEntries().Count, "5성까지 쓸 조각이 다 있으면 교환소에서 빠짐");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private GachaBannerDefinition Banner(string id, GachaBannerKind kind, SpecialOtterDefinition featured)
    {
        var banner = ScriptableObject.CreateInstance<GachaBannerDefinition>();
        _created.Add(banner);
        var so = new SerializedObject(banner);
        so.FindProperty("_bannerId").stringValue = id;
        so.FindProperty("_kind").enumValueIndex = (int)kind;
        if (featured != null)
        {
            var list = so.FindProperty("_featuredSpecialOtters");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = featured;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return banner;
    }

    #endregion

    #region 조각 · 해금 · 별

    [Test]
    public void Fragments_UnlockThenStarUp()
    {
        var bard = Otter("bard", GachaTable.Rare, KingdomBonusKind.MoveSpeed);
        var collection = new CompanionCollection();

        Assert.AreEqual(0, collection.AddFragments(bard, 19));
        Assert.IsFalse(collection.CanStarUp(bard), "해금 20개 중 19개");
        collection.AddFragments(bard, 1);
        Assert.IsTrue(collection.TryStarUp(bard));
        Assert.AreEqual(1, collection.Stars(bard));
        Assert.AreEqual(0, collection.Fragments(bard));
        Assert.IsTrue(collection.IsUnlocked(bard));

        collection.AddFragments(bard, 20);
        Assert.IsTrue(collection.TryStarUp(bard));
        Assert.AreEqual(2, collection.Stars(bard));
        Assert.AreEqual(30, CompanionCollection.NextStarCost(bard, 2));
        Assert.IsFalse(collection.TryStarUp(bard), "조각이 모자람");
    }

    [Test]
    public void Fragments_BeyondFiveStarsBecomeShards()
    {
        var bard = Otter("bard", GachaTable.Rare, KingdomBonusKind.MoveSpeed);
        var architect = Otter("architect", GachaTable.Epic, KingdomBonusKind.BuildSpeed);
        var collection = new CompanionCollection();

        Assert.AreEqual(200, CompanionCollection.CostToMax(bard, 0));
        Assert.AreEqual(310, CompanionCollection.CostToMax(architect, 0));
        Assert.AreEqual(10 * CompanionCollection.RareOverflowShards, collection.AddFragments(bard, 210), "5성까지 쓸 200개만 남김");
        Assert.AreEqual(200, collection.Fragments(bard));
        for (int i = 0; i < 5; i++)
            Assert.IsTrue(collection.TryStarUp(bard));
        Assert.AreEqual(5, collection.Stars(bard));
        Assert.IsFalse(collection.CanStarUp(bard));
        Assert.AreEqual(3 * CompanionCollection.RareOverflowShards, collection.AddFragments(bard, 3), "5성 뒤 조각은 모두 반짝 조각");
        Assert.AreEqual(4 * CompanionCollection.EpicOverflowShards, collection.AddFragments(architect, 314));
    }

    #endregion

    #region 편성

    [Test]
    public void Formation_OnlyUnlockedOpenSlotsAndOneOfEachBonus()
    {
        var bard = Otter("bard", GachaTable.Rare, KingdomBonusKind.MoveSpeed);
        var bard2 = Otter("bard2", GachaTable.Rare, KingdomBonusKind.MoveSpeed);
        var gardener = Otter("gardener", GachaTable.Rare, KingdomBonusKind.GrowSpeed);
        var find = Finder(bard, bard2, gardener);
        var collection = new CompanionCollection();

        Assert.AreEqual(FormationOutcome.Locked, collection.Assign(0, bard, 3, find));
        foreach (var otter in new[] { bard, bard2, gardener })
        {
            collection.AddFragments(otter, 20);
            collection.TryStarUp(otter);
        }
        Assert.AreEqual(FormationOutcome.Assigned, collection.Assign(0, bard, 3, find));
        Assert.AreEqual(FormationOutcome.NoSlot, collection.Assign(1, gardener, 1, find), "열린 칸은 1칸");
        Assert.AreEqual(FormationOutcome.SameBonus, collection.Assign(1, bard2, 3, find), "이동속도 해달은 한 마리만");
        Assert.AreEqual(FormationOutcome.Assigned, collection.Assign(1, gardener, 3, find));
        Assert.AreEqual(FormationOutcome.Assigned, collection.Assign(0, bard2, 3, find), "같은 칸이면 바꿔 넣기");

        Assert.AreEqual(4, collection.FormationPercent(KingdomBonusKind.MoveSpeed, 3, find));
        Assert.AreEqual(4, collection.FormationPercent(KingdomBonusKind.GrowSpeed, 3, find));
        Assert.AreEqual(0, collection.FormationPercent(KingdomBonusKind.GrowSpeed, 1, find), "닫힌 칸의 해달은 버프 없음");

        collection.AddFragments(gardener, 20);
        collection.TryStarUp(gardener);
        Assert.AreEqual(6, collection.FormationPercent(KingdomBonusKind.GrowSpeed, 3, find), "2성 = 6%");

        Assert.AreEqual(FormationOutcome.Assigned, collection.Assign(2, gardener, 3, find), "다른 칸으로 옮기기");
        Assert.IsNull(collection.Formation[1]);
        Assert.AreEqual("gardener", collection.Formation[2]);
    }

    private static System.Func<string, SpecialOtterDefinition> Finder(params SpecialOtterDefinition[] otters) =>
        id => System.Array.Find(otters, o => o.OtterId == id);

    #endregion

    #region 장신구

    [Test]
    public void Accessory_DuplicatesEnhanceThenGiveShards()
    {
        var hat = Accessory("hat", GachaTable.Rare, AccessoryEffect.MoveSpeed, 12);
        var collection = new CompanionCollection(id => id == "hat" ? hat : null);

        Assert.AreEqual(AccessoryGain.New, collection.AddAccessory(hat, out _));
        for (int i = 1; i <= AccessoryDefinition.MaxPlus; i++)
        {
            Assert.AreEqual(AccessoryGain.Enhanced, collection.AddAccessory(hat, out int none));
            Assert.AreEqual(0, none);
            Assert.AreEqual(i, collection.Plus(hat));
        }
        Assert.AreEqual(AccessoryGain.Shards, collection.AddAccessory(hat, out int shards));
        Assert.AreEqual(GachaRules.ShardsFor(GachaTable.Rare), shards);
        Assert.AreEqual(12, hat.PercentAt(0));
        Assert.AreEqual(15, hat.PercentAt(1));
        Assert.AreEqual(24, hat.PercentAt(4), "+4 = 기본의 2배");
    }

    [Test]
    public void Accessory_OnePerOtterAndMovesBetweenOtters()
    {
        var hat = Accessory("hat", GachaTable.Common, AccessoryEffect.MoveSpeed, 8);
        var necklace = Accessory("necklace", GachaTable.Common, AccessoryEffect.Yield, 6);
        var collection = new CompanionCollection(id => id == "hat" ? hat : id == "necklace" ? necklace : null);

        Assert.IsFalse(collection.Equip(hat, "otter_farmer"), "없는 장신구는 못 낌");
        collection.AddAccessory(hat, out _);
        collection.AddAccessory(necklace, out _);
        Assert.IsTrue(collection.Equip(hat, "otter_farmer"));
        Assert.AreEqual(8, collection.AccessoryPercent("otter_farmer", AccessoryEffect.MoveSpeed));
        Assert.AreEqual(0, collection.AccessoryPercent("otter_farmer", AccessoryEffect.Yield));

        Assert.IsTrue(collection.Equip(necklace, "otter_farmer"), "한 해달에 하나: 모자는 빠짐");
        Assert.IsNull(collection.Wearer(hat));
        Assert.AreEqual(6, collection.AccessoryPercent("otter_farmer", AccessoryEffect.Yield));

        Assert.IsTrue(collection.Equip(necklace, "otter_miner"), "다른 해달에게 끼우면 옮겨 감");
        Assert.IsNull(collection.EquippedOn("otter_farmer"));
        Assert.AreEqual("necklace", collection.EquippedOn("otter_miner"));
        collection.Unequip(necklace);
        Assert.IsNull(collection.EquippedOn("otter_miner"));
    }

    #endregion

    #region 세이브

    [Test]
    public void Save_RoundTripKeepsUnknownAndCleansBadRecords()
    {
        var bard = Otter("bard", GachaTable.Rare, KingdomBonusKind.MoveSpeed);
        var hat = Accessory("hat", GachaTable.Common);
        var collection = new CompanionCollection(id => id == "hat" ? hat : null);
        collection.AddFragments(bard, 25);
        collection.TryStarUp(bard);
        collection.Assign(1, bard, 3, Finder(bard));
        collection.AddAccessory(hat, out _);
        collection.AddAccessory(hat, out _);
        collection.Equip(hat, "otter_farmer");

        var saved = new GachaSaveData();
        collection.Write(saved);
        // 데이터에 없는 해달 · 장신구 (잠깐 뺀 것) + 같은 해달에 장신구 둘
        saved.specialOtters.Add(new SpecialOtterSaveData { otterId = "removed", fragments = 7, stars = 9 });
        saved.accessories.Add(new AccessorySaveData { accessoryId = "removed_acc", plus = 2, otterId = "otter_farmer" });
        string json = JsonUtility.ToJson(saved);

        var loaded = new CompanionCollection(id => id == "hat" ? hat : null);
        loaded.Load(JsonUtility.FromJson<GachaSaveData>(json));
        Assert.AreEqual(1, loaded.Stars(bard));
        Assert.AreEqual(5, loaded.Fragments(bard));
        Assert.AreEqual("bard", loaded.Formation[1]);
        Assert.AreEqual(1, loaded.Plus(hat));
        Assert.AreEqual("otter_farmer", loaded.Wearer(hat));
        Assert.AreEqual(SpecialOtterDefinition.MaxStars, loaded.Stars("removed"), "별은 5까지로 자름");

        var again = new GachaSaveData();
        loaded.Write(again);
        Assert.AreEqual(2, again.specialOtters.Count, "모르는 해달 기록도 지키기");
        var removedAcc = again.accessories.Find(a => a.accessoryId == "removed_acc");
        Assert.IsNotNull(removedAcc);
        Assert.IsTrue(string.IsNullOrEmpty(removedAcc.otterId), "한 해달에 둘이면 앞의 것만");
    }

    [Test]
    public void Save_OldSaveWithoutListsLoadsEmpty()
    {
        var old = JsonUtility.FromJson<GachaSaveData>("{\"standardSinceEpic\":3}");
        var collection = new CompanionCollection();
        collection.Load(old);
        Assert.AreEqual(3, collection.Formation.Count);
        Assert.IsNull(collection.Formation[0]);
    }

    #endregion

    #region 실제 데이터

    [Test]
    public void RewardConfigAsset_HasPlanRosterAndValidTables()
    {
        var config = AssetDatabase.LoadAssetAtPath<GachaRewardConfig>("Assets/Scriptable Obejects/Gacha/GachaRewardConfig.asset");
        Assert.IsNotNull(config, "Tools/Gacha/Create Companion Data");
        Assert.AreEqual(6, config.Otters.Count);
        Assert.AreEqual(9, config.Accessories.Count);
        Assert.IsNotNull(config.Wood);
        Assert.IsNotNull(config.Stone);

        var ids = new HashSet<string>();
        var bonuses = new HashSet<KingdomBonusKind>();
        foreach (var otter in config.Otters)
        {
            Assert.IsTrue(ids.Add(otter.OtterId), otter.OtterId);
            Assert.IsTrue(bonuses.Add(otter.Bonus), "첫 명단은 해달마다 다른 버프");
            Assert.Greater(otter.PercentAt(SpecialOtterDefinition.MaxStars), otter.PercentAt(1));
        }
        foreach (var accessory in config.Accessories)
            Assert.IsTrue(ids.Add(accessory.AccessoryId), accessory.AccessoryId);

        var table = config.BuildTable(GachaBannerKind.Pickup, 60, 10, 0.5f, new[] { config.Otters[5] });
        Assert.AreEqual(1, table.Featured(GachaTable.Epic).Count);
        Assert.IsFalse(System.Linq.Enumerable.Contains(table.Otters(GachaTable.Epic), config.Otters[5]), "픽업 해달은 픽업 칸에만");
        double sum = 0;
        foreach (var (kind, tier) in GachaRewardRules.Slots)
            sum += GachaRewardRules.Chance(table, kind, tier);
        Assert.AreEqual(1.0, sum, 1e-6);
        Assert.AreEqual(0.03, GachaRewardRules.Chance(table, GachaRewardKind.Fragments, GachaTable.Epic), 1e-6);
    }

    #endregion

    // NextDouble은 정해 둔 값을 차례로 (다 쓰면 마지막 값), Next(n)은 0 — 칸 · 목록의 첫 번째, 조각 수는 최솟값
    private sealed class ScriptedRandom : System.Random
    {
        private readonly double[] _values;
        private int _index;

        public ScriptedRandom(params double[] values) => _values = values;

        public override double NextDouble() => _values[System.Math.Min(_index++, _values.Length - 1)];
        public override int Next(int maxValue) => 0;
        public override int Next(int minValue, int maxValue) => minValue;
    }
}
