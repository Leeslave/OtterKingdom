using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class OfflineProductionTests
{
    // Online 30s, offline x4 = 120s per cycle (multiplier 1 at every level).
    private const float CarrotCycleSec = 120f;
    // Online 60s -> 240s offline.
    private const float PotatoCycleSec = 240f;

    private CropDefinition _carrot;
    private CropDefinition _potato;
    private FarmBalanceData _farm;
    private FishingBalanceData _fishing;
    private OtterVisitBalanceData _visits;
    private MiningBalanceData _mining;
    private FakeBag _bag;

    private class FakeBag : IOfflineBag
    {
        public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        public int MaxPerItem = int.MaxValue;

        public int GetCount(string itemId) => Counts.TryGetValue(itemId, out var n) ? n : 0;

        public bool TryRemove(string itemId, int amount)
        {
            if (GetCount(itemId) < amount) return false;
            Counts[itemId] = GetCount(itemId) - amount;
            return true;
        }

        public int Add(string itemId, int amount)
        {
            int added = Mathf.Min(amount, MaxPerItem - GetCount(itemId));
            Counts[itemId] = GetCount(itemId) + added;
            return added;
        }
    }

    [SetUp]
    public void SetUp()
    {
        _carrot = CreateCrop("crop_carrot", 30f, 5, SeedType.Permanent);
        _potato = CreateCrop("crop_potato", 60f, 3, SeedType.Consumable);

        _farm = ScriptableObject.CreateInstance<FarmBalanceData>();
        _farm.durationMultiplierByLevel = new[] { 1f, 1f, 1f, 1f, 1f };

        _fishing = ScriptableObject.CreateInstance<FishingBalanceData>();
        _fishing.biteDelaySec = new Vector2(20f, 20f);
        _fishing.onlineAnimationSecPerCatch = 5f; // (20 + 5) x 4 = 100s per catch

        // Off unless a visit test turns it on, so it can't add to other tests' reports.
        _visits = ScriptableObject.CreateInstance<OtterVisitBalanceData>();
        _visits.rollIntervalSec = 1800f;
        _visits.visitChancePerRoll = 0f;

        _mining = ScriptableObject.CreateInstance<MiningBalanceData>();
        _mining.findIntervalSec = new Vector2(20f, 20f);
        _mining.offlineSlowdown = 4f; // 20 x 4 = 80s per find

        _bag = new FakeBag();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_carrot);
        Object.DestroyImmediate(_potato);
        Object.DestroyImmediate(_farm);
        Object.DestroyImmediate(_fishing);
        Object.DestroyImmediate(_visits);
        Object.DestroyImmediate(_mining);
    }

    private static CropDefinition CreateCrop(string id, float durationSec, int yield, SeedType seedType)
    {
        var crop = ScriptableObject.CreateInstance<CropDefinition>();
        crop.cropId = id;
        crop.baseDurationSec = durationSec;
        crop.yieldCount = yield;
        crop.seedType = seedType;
        return crop;
    }

    private OfflineProductionService CreateService(int seed = 1) =>
        new OfflineProductionService(new[] { _carrot, _potato }, _farm, _fishing, _visits, _mining, new System.Random(seed));

    // One unlocked plot (3 slots), rod and pickaxe level 1 so fishing and
    // mining stay off unless a test turns them on.
    private static SaveData CreateSave(int farmLevel, params string[] registeredCrops)
    {
        var save = new SaveData { farmLevel = farmLevel, rodLevel = 1, pickaxeLevel = 1 };
        save.plots.Add(new PlotSaveData { plotId = "plot_1", unlocked = true, slots = PlotSaveData.CreateEmptySlots() });
        save.plots.Add(new PlotSaveData { plotId = "plot_2", unlocked = false, slots = PlotSaveData.CreateEmptySlots() });
        foreach (var cropId in registeredCrops)
        {
            save.offlineFarmSlots.Add(new OfflineFarmSlotSaveData { cropId = cropId });
        }
        return save;
    }

    // ------------------------------------------------------------------ farm

    [Test]
    public void Farm_BelowUnlockLevel_ProducesNothing()
    {
        var save = CreateSave(1, "crop_carrot");

        var report = CreateService().Run(save, CarrotCycleSec * 10, _bag);

        Assert.AreEqual(0, _bag.GetCount("crop_carrot"));
        Assert.IsFalse(report.HasAnything);
    }

    [Test]
    public void Farm_PermanentSeed_HarvestsEveryCycleAndKeepsLeftover()
    {
        var save = CreateSave(2, "crop_carrot");

        var report = CreateService().Run(save, CarrotCycleSec * 2 + 30, _bag);

        Assert.AreEqual(2, _bag.GetCount("crop_carrot"));
        Assert.AreEqual(2, OfflineReport.CountOf(report.Received, "crop_carrot"));
        Assert.AreEqual(30f, save.offlineFarmSlots[0].progressSec, 0.01f);
    }

    [Test]
    public void Farm_LeftoverFromShortAbsences_AddsUp()
    {
        var save = CreateSave(2, "crop_carrot");
        var service = CreateService();

        service.Run(save, CarrotCycleSec * 0.6, _bag);
        Assert.AreEqual(0, _bag.GetCount("crop_carrot"));

        service.Run(save, CarrotCycleSec * 0.6, _bag);
        Assert.AreEqual(1, _bag.GetCount("crop_carrot"));
    }

    [Test]
    public void Farm_IgnoresWhatIsPlantedInTheFurrows()
    {
        var save = CreateSave(2);
        save.plots[0].slots[0].cropId = "crop_carrot";
        save.plots[0].slots[0].state = FurrowSlotState.Growing;

        CreateService().Run(save, CarrotCycleSec * 5, _bag);

        Assert.AreEqual(0, _bag.GetCount("crop_carrot"));
    }

    [Test]
    public void Farm_ConsumableSeed_SpendsOnePerHarvestAndStopsWhenOut()
    {
        var save = CreateSave(2, "crop_potato");
        _bag.Counts["seed_potato"] = 2;

        var report = CreateService().Run(save, PotatoCycleSec * 5, _bag);

        Assert.AreEqual(2, _bag.GetCount("crop_potato"));
        Assert.AreEqual(0, _bag.GetCount("seed_potato"));
        Assert.AreEqual(2, OfflineReport.CountOf(report.SeedsUsed, "seed_potato"));
        CollectionAssert.AreEqual(new[] { "crop_potato" }, report.OutOfSeeds);
        Assert.AreEqual(0f, save.offlineFarmSlots[0].progressSec);
    }

    [Test]
    public void Farm_SharedSeedStock_GoesToWhicheverFinishesFirst()
    {
        var save = CreateSave(2, "crop_potato", "crop_potato");
        save.offlineFarmSlots[1].progressSec = PotatoCycleSec - 10f; // due after 10s
        _bag.Counts["seed_potato"] = 1;

        // Slot 1 harvests at 10s with the only seed; slot 0 is due at 240s and finds none.
        var report = CreateService().Run(save, PotatoCycleSec + 5, _bag);

        Assert.AreEqual(1, _bag.GetCount("crop_potato"));
        Assert.AreEqual(0f, save.offlineFarmSlots[0].progressSec, "slot 0 ran out of seeds");
        Assert.AreEqual(PotatoCycleSec - 5, save.offlineFarmSlots[1].progressSec, 0.01f, "slot 1 still growing");
        CollectionAssert.AreEqual(new[] { "crop_potato" }, report.OutOfSeeds);
    }

    [Test]
    public void Farm_RegistrationsBeyondUnlockedSlots_AreIgnored()
    {
        var save = CreateSave(2, "crop_carrot", "crop_carrot", "crop_carrot", "crop_carrot");

        CreateService().Run(save, CarrotCycleSec, _bag);

        Assert.AreEqual(3, _bag.GetCount("crop_carrot"));
    }

    [Test]
    public void Farm_BagFull_ExtraIsLostAndReported()
    {
        var save = CreateSave(2, "crop_carrot");
        _bag.MaxPerItem = 2;

        var report = CreateService().Run(save, CarrotCycleSec * 3, _bag);

        Assert.AreEqual(2, OfflineReport.CountOf(report.Received, "crop_carrot"));
        Assert.AreEqual(1, OfflineReport.CountOf(report.Lost, "crop_carrot"));
    }

    // --------------------------------------------------------------- fishing

    [Test]
    public void Fishing_BelowUnlockRodLevel_CatchesNothing()
    {
        var save = CreateSave(1);

        var report = CreateService().Run(save, 100f * 10, _bag);

        Assert.IsFalse(report.HasAnything);
    }

    [Test]
    public void Fishing_OneCatchPerInterval_SplitIntoFishAndTrash()
    {
        var save = CreateSave(1);
        save.rodLevel = 2;

        var report = CreateService().Run(save, 100f * 10 + 40, _bag);

        int fish = OfflineReport.CountOf(report.Received, _fishing.fishItemId);
        int trash = OfflineReport.CountOf(report.Received, _fishing.trashItemId);
        Assert.AreEqual(10, fish + trash);
        Assert.AreEqual(40f, save.offlineFishingProgressSec, 0.01f);
    }

    [Test]
    public void Fishing_FishChanceOne_AllFish()
    {
        var save = CreateSave(1);
        save.rodLevel = 2;
        _fishing.baseFishChance = 1f;

        CreateService().Run(save, 100f * 10, _bag);

        Assert.AreEqual(10, _bag.GetCount(_fishing.fishItemId));
        Assert.AreEqual(0, _bag.GetCount(_fishing.trashItemId));
    }

    [Test]
    public void Fishing_ManyCatches_StayNearTheExpectedFishRate()
    {
        var save = CreateSave(1);
        save.rodLevel = 2; // 55% fish

        // 한 번 비운 시간은 8시간까지라 1,000번 낚을 시간을 네 번에 나눠 (남은 시간은 이어짐)
        var service = CreateService(seed: 42);
        for (int i = 0; i < 4; i++)
            service.Run(save, 100f * 1000 / 4, _bag);

        Assert.AreEqual(550, _bag.GetCount(_fishing.fishItemId), 50);
    }

    // ---------------------------------------------------------------- mining

    [Test]
    public void Mining_BelowUnlockPickaxeLevel_FindsNothing()
    {
        var save = CreateSave(1);

        var report = CreateService().Run(save, 80f * 10, _bag);

        Assert.IsFalse(report.HasAnything);
    }

    [Test]
    public void Mining_OneFindPerInterval_SplitIntoDiamondAndStone()
    {
        var save = CreateSave(1);
        save.pickaxeLevel = 2;

        var report = CreateService().Run(save, 80f * 10 + 30, _bag);

        int diamonds = OfflineReport.CountOf(report.Received, _mining.diamondItemId);
        int stones = OfflineReport.CountOf(report.Received, _mining.stoneItemId);
        Assert.AreEqual(10, diamonds + stones);
        Assert.AreEqual(30f, save.offlineMiningProgressSec, 0.01f);
    }

    [Test]
    public void Mining_ManyFinds_StayNearTheExpectedDiamondRate()
    {
        var save = CreateSave(1);
        save.pickaxeLevel = 2; // 35% diamond

        // 한 번 비운 시간은 8시간까지라 1,000번 캘 시간을 네 번에 나눠 (남은 시간은 이어짐)
        var service = CreateService(seed: 42);
        for (int i = 0; i < 4; i++)
            service.Run(save, 80f * 1000 / 4, _bag);

        Assert.AreEqual(350, _bag.GetCount(_mining.diamondItemId), 50);
    }

    [Test]
    public void Mining_DiamondChance_CapsAtMaxPickaxeLevel()
    {
        Assert.AreEqual(0.3f, _mining.DiamondChanceAt(1), 0.001f);
        Assert.AreEqual(0.5f, _mining.DiamondChanceAt(_mining.MaxPickaxeLevel), 0.001f);
    }

    // ---------------------------------------------------------- otter visits

    [Test]
    public void OtterVisits_OneRollPerInterval_EvenAtLevelOne()
    {
        var save = CreateSave(1);
        _visits.visitChancePerRoll = 1f;

        var report = CreateService().Run(save, 1800f * 3 + 100, _bag);

        Assert.AreEqual(3, report.OtterVisits.Count);
        CollectionAssert.IsSubsetOf(report.OtterVisits, _visits.placeholderOtterNames);
        Assert.AreEqual(100f, save.offlineOtterVisitProgressSec, 0.01f);
        Assert.IsTrue(report.HasAnything);
    }

    [Test]
    public void OtterVisits_ShortAbsences_AddUp()
    {
        var save = CreateSave(1);
        _visits.visitChancePerRoll = 1f;
        var service = CreateService();

        Assert.AreEqual(0, service.Run(save, 1000f, _bag).OtterVisits.Count);
        Assert.AreEqual(1, service.Run(save, 1000f, _bag).OtterVisits.Count);
    }

    [Test]
    public void OtterVisits_ZeroChance_NobodyComes()
    {
        var save = CreateSave(1);

        var report = CreateService().Run(save, 1800f * 10, _bag);

        Assert.AreEqual(0, report.OtterVisits.Count);
    }

    [Test]
    public void LongAbsence_CountsAtMostEightHours()
    {
        const double eightHours = 8 * 3600;
        var report = CreateService().Run(CreateSave(2, "crop_carrot"), eightHours * 9, _bag);
        int afterThreeDays = _bag.GetCount("crop_carrot");
        _bag.Counts.Clear();
        CreateService().Run(CreateSave(2, "crop_carrot"), eightHours, _bag);

        Assert.Greater(afterThreeDays, 0);
        Assert.AreEqual(_bag.GetCount("crop_carrot"), afterThreeDays, "사흘을 비워도 8시간만큼");
        Assert.IsTrue(report.Capped);
        Assert.AreEqual(eightHours * 9, report.ElapsedSec, "비운 시간은 그대로 보여 줌");
        Assert.AreEqual(eightHours, report.CreditedSec);
    }

    [Test]
    public void ShortAbsence_NotCapped()
    {
        var report = CreateService().Run(CreateSave(2, "crop_carrot"), 3600, _bag);

        Assert.IsFalse(report.Capped);
        Assert.AreEqual(3600, report.CreditedSec);
    }
}
