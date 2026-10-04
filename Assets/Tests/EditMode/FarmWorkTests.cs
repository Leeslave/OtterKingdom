using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// P3-F 농사: 씬과 상관없는 농부 수확(다른 지역에서도), 한 번에 몰아 잰 시간과 잘게 잰 시간의 결과가 같음,
/// 앱을 꺼 둔 시간은 성장만, 모종이 없으면 그 칸만 대기·다시 생기면 재개, 마지막 모종을 두 칸이 쓰지 않음,
/// 가방 가득·모종 부족 알림은 시작될 때 한 번, 장면이 바뀌어도(새 FarmService) 수확 중이던 것을 이어서 함.
/// </summary>
public class FarmWorkTests
{
    private CropDefinition _carrot;
    private CropDefinition _potato;
    private FarmBalanceData _balance;

    private class FakeHost : IFarmHost
    {
        public readonly Dictionary<string, int> Seeds = new Dictionary<string, int>();
        public readonly Dictionary<string, int> Stored = new Dictionary<string, int>();
        public bool Working = true;
        public bool BagFull;

        public int SeedCount(string cropId) => Seeds.TryGetValue(SeedOf(cropId), out int n) ? n : 0;

        public bool TryConsumeSeed(string cropId)
        {
            string seed = SeedOf(cropId);
            if (!Seeds.TryGetValue(seed, out int n) || n <= 0)
                return false;
            Seeds[seed] = n - 1;
            return true;
        }

        public bool CanStoreHarvest(string cropId, int amount) => !BagFull;

        public void StoreHarvest(string cropId, int amount) =>
            Stored[cropId] = (Stored.TryGetValue(cropId, out int n) ? n : 0) + amount;

        public bool CanFarmerWork => Working;

        public int StoredOf(string cropId) => Stored.TryGetValue(cropId, out int n) ? n : 0;

        private static string SeedOf(string cropId) => "seed_" + cropId;
    }

    [SetUp]
    public void SetUp()
    {
        _carrot = Crop("crop_carrot", 30f, 5, SeedType.Permanent);
        _potato = Crop("crop_potato", 60f, 3, SeedType.Consumable);
        _balance = ScriptableObject.CreateInstance<FarmBalanceData>();
        _balance.farmerHarvestSec = 10f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_carrot);
        Object.DestroyImmediate(_potato);
        Object.DestroyImmediate(_balance);
    }

    private static CropDefinition Crop(string id, float seconds, int yield, SeedType seed)
    {
        var crop = ScriptableObject.CreateInstance<CropDefinition>();
        crop.cropId = id;
        crop.displayName = id;
        crop.baseDurationSec = seconds;
        crop.yieldCount = yield;
        crop.seedType = seed;
        return crop;
    }

    private static SaveData NewSave()
    {
        var save = new SaveData();
        save.plots.Add(new PlotSaveData { plotId = "plot_1", unlocked = true, slots = PlotSaveData.CreateEmptySlots() });
        return save;
    }

    private FarmService Farm(SaveData save, FakeHost host) =>
        new FarmService(save, new[] { _carrot, _potato }, _balance, host);

    [Test]
    public void FarmerHarvests_WithoutFarmScene_AndReplants()
    {
        var host = new FakeHost();
        var save = NewSave();
        var farm = Farm(save, host);
        Assert.AreEqual(PlantResult.Planted, farm.Plant(0, 0, "crop_carrot"));

        farm.Tick(30f);
        Assert.AreEqual(FurrowSlotState.AwaitingHarvest, farm.GetSlotState(0, 0));
        Assert.IsTrue(farm.TryGetHarvestTarget(out int plot, out int slot));
        Assert.AreEqual((0, 0), (plot, slot));

        farm.Tick(10f);
        Assert.AreEqual(5, host.StoredOf("crop_carrot"), "농부가 장면과 상관없이 수확해 가방에 넣음");
        Assert.AreEqual(FurrowSlotState.Growing, farm.GetSlotState(0, 0), "같은 작물을 다시 심음");
    }

    [Test]
    public void SameTime_InOneStepOrManySteps_SameResult()
    {
        var hostA = new FakeHost();
        var saveA = NewSave();
        var farmA = Farm(saveA, hostA);
        var hostB = new FakeHost();
        var saveB = NewSave();
        var farmB = Farm(saveB, hostB);
        for (int s = 0; s < 3; s++)
        {
            farmA.Plant(0, s, "crop_carrot");
            farmB.Plant(0, s, "crop_carrot");
        }

        farmA.Tick(245f);
        for (int i = 0; i < 2450; i++)
            farmB.Tick(0.1f);

        Assert.AreEqual(hostA.StoredOf("crop_carrot"), hostB.StoredOf("crop_carrot"), "밭에 있든 다른 곳에 있든 같은 시간이면 같은 수확");
        Assert.Greater(hostA.StoredOf("crop_carrot"), 0);
        for (int s = 0; s < 3; s++)
            Assert.AreEqual(farmA.GetSlotState(0, s), farmB.GetSlotState(0, s));
    }

    [Test]
    public void AppClosedTime_GrowsOnly()
    {
        var host = new FakeHost();
        var farm = Farm(NewSave(), host);
        farm.Plant(0, 0, "crop_carrot");

        farm.Grow(1000f);

        Assert.AreEqual(0, host.StoredOf("crop_carrot"), "꺼 둔 시간은 온라인 농부가 일하지 않음 (오프라인 생산 규칙 따로)");
        Assert.AreEqual(FurrowSlotState.AwaitingHarvest, farm.GetSlotState(0, 0));
    }

    [Test]
    public void FarmerNotWorking_NoHarvest()
    {
        var host = new FakeHost { Working = false };
        var farm = Farm(NewSave(), host);
        farm.Plant(0, 0, "crop_carrot");

        farm.Tick(100f);

        Assert.AreEqual(0, host.StoredOf("crop_carrot"));
        Assert.IsFalse(farm.TryGetHarvestTarget(out _, out _));
    }

    [Test]
    public void NoSeed_SlotWaits_ThenResumes_NoticeOncePerShortage()
    {
        var host = new FakeHost();
        host.Seeds["seed_crop_potato"] = 1;
        var save = NewSave();
        save.farmNotice.initialized = true;
        var farm = Farm(save, host);
        var notices = new List<FarmNotice>();
        farm.NoticeRaised += notices.Add;
        Assert.AreEqual(PlantResult.Planted, farm.Plant(0, 0, "crop_potato"));

        farm.Tick(70f);
        Assert.AreEqual(3, host.StoredOf("crop_potato"), "모종이 없어도 자라던 작물은 수확");
        Assert.AreEqual(FurrowSlotState.Empty, farm.GetSlotState(0, 0));
        Assert.AreEqual(_potato, farm.GetWaitingSeedCrop(0, 0), "다음 심기만 대기");
        Assert.AreEqual(1, notices.Count);
        Assert.AreEqual(FarmNoticeKind.SeedShortage, notices[0].Kind);

        farm.Tick(5f);
        farm.Tick(5f);
        Assert.AreEqual(1, notices.Count, "같은 부족 상태에서는 다시 알리지 않음");

        host.Seeds["seed_crop_potato"] = 1;
        farm.Tick(0.1f);
        Assert.AreEqual(FurrowSlotState.Growing, farm.GetSlotState(0, 0), "모종이 다시 생기면 자동 재배 재개");
        Assert.IsNull(farm.GetWaitingSeedCrop(0, 0));

        farm.Tick(70f);
        Assert.AreEqual(2, notices.Count, "해소된 뒤 다시 부족해지면 새 알림");
    }

    [Test]
    public void LastSeed_UsedByOneSlotOnly()
    {
        var host = new FakeHost();
        var save = NewSave();
        foreach (var slot in save.plots[0].slots)
            slot.waitingSeedCropId = "crop_potato";
        host.Seeds["seed_crop_potato"] = 1;
        var farm = Farm(save, host);

        farm.Tick(0.1f);

        Assert.AreEqual(FurrowSlotState.Growing, farm.GetSlotState(0, 0), "정해진 칸 순서대로");
        Assert.AreEqual(FurrowSlotState.Empty, farm.GetSlotState(0, 1));
        Assert.AreEqual(FurrowSlotState.Empty, farm.GetSlotState(0, 2));
        Assert.AreEqual(0, host.SeedCount("crop_potato"));
        Assert.AreEqual(2, farm.CollectSeedWaiting(null));
    }

    [Test]
    public void BagFull_StopsHarvest_NoticeOnce()
    {
        var host = new FakeHost { BagFull = true };
        var save = NewSave();
        save.farmNotice.initialized = true;
        var farm = Farm(save, host);
        var notices = new List<FarmNotice>();
        farm.NoticeRaised += notices.Add;
        farm.Plant(0, 0, "crop_carrot");

        farm.Tick(40f);
        farm.Tick(40f);

        Assert.AreEqual(0, host.StoredOf("crop_carrot"), "상한을 넘겨 쌓거나 버리지 않음");
        Assert.AreEqual(FurrowSlotState.AwaitingHarvest, farm.GetSlotState(0, 0));
        Assert.AreEqual(1, notices.Count);
        Assert.AreEqual(FarmNoticeKind.StorageFull, notices[0].Kind);

        host.BagFull = false;
        farm.Tick(10f);
        Assert.AreEqual(5, host.StoredOf("crop_carrot"), "비우면 다시 수확");
    }

    [Test]
    public void OldSave_WithoutNoticeRecord_DoesNotAnnounceExistingShortage()
    {
        var host = new FakeHost();
        var save = NewSave();
        save.plots[0].slots[0].waitingSeedCropId = "crop_potato";
        save.farmNotice = new FarmNoticeSaveData();
        var farm = Farm(save, host);
        int notices = 0;
        farm.NoticeRaised += _ => notices++;

        farm.Tick(1f);

        Assert.AreEqual(0, notices, "필드가 없던 세이브는 지금 상태로 맞추고 알림을 몰아 내지 않음");
        Assert.IsTrue(save.farmNotice.initialized);
        Assert.IsTrue(save.farmNotice.seedShortage);
    }

    [Test]
    public void SceneChange_NewService_ContinuesHarvest_NoDuplicate()
    {
        var host = new FakeHost();
        var save = NewSave();
        var farm = Farm(save, host);
        farm.Plant(0, 0, "crop_carrot");
        farm.Tick(34f); // 수확 4초째

        var next = Farm(save, host); // 다른 장소의 GameManager
        Assert.IsTrue(next.TryGetHarvestTarget(out _, out _), "수확 중이던 것이 세이브에 남음");
        next.Tick(5f);
        Assert.AreEqual(0, host.StoredOf("crop_carrot"));
        next.Tick(1f);
        Assert.AreEqual(5, host.StoredOf("crop_carrot"));
        next.Tick(1f);
        Assert.AreEqual(5, host.StoredOf("crop_carrot"), "다시 들어와도 같은 작물을 두 번 수확하지 않음");
    }

    [Test]
    public void PlayerChangesCropDuringHarvest_NothingHarvested()
    {
        var host = new FakeHost();
        var farm = Farm(NewSave(), host);
        farm.Plant(0, 0, "crop_carrot");
        farm.Tick(35f);

        farm.ClearSlot(0, 0);
        farm.Tick(10f);

        Assert.AreEqual(0, host.StoredOf("crop_carrot"));
        Assert.AreEqual(FurrowSlotState.Empty, farm.GetSlotState(0, 0));
    }
}
