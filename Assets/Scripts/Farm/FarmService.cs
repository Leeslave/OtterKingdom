using System;
using System.Collections.Generic;
using System.Linq;

// What the farm needs from the rest of the game: the bag (seeds in, harvest
// out) and whether the farmer is working. GameManager is the real one; tests
// pass a fake.
public interface IFarmHost
{
    // Consumable seeds are bag items owned by the inventory; the farm only
    // asks how many there are and spends one per planting (argument: cropId).
    int SeedCount(string cropId);
    bool TryConsumeSeed(string cropId);

    // Whether this whole yield fits in the bag / puts it there (quests count it).
    bool CanStoreHarvest(string cropId, int amount);
    void StoreHarvest(string cropId, int amount);

    // The farmer is assigned and working (the settlement decides). Until then
    // crops still grow but nobody harvests or replants them.
    bool CanFarmerWork { get; }
}

// A farm problem the player should hear about once, when it starts.
public enum FarmNoticeKind
{
    SeedShortage, // automatic replant ran out of seeds: those slots wait
    StorageFull,  // ready crops don't fit in the bag: harvesting stops
}

public readonly struct FarmNotice
{
    public readonly FarmNoticeKind Kind;
    // Slots affected (waiting for seeds / ready but not fitting).
    public readonly int SlotCount;
    // Crops involved, each once, in slot order.
    public readonly IReadOnlyList<string> CropIds;

    public FarmNotice(FarmNoticeKind kind, int slotCount, IReadOnlyList<string> cropIds)
    {
        Kind = kind;
        SlotCount = slotCount;
        CropIds = cropIds;
    }
}

// The farm model, shared by every zone scene. Growth and the farmer's work
// both live here rather than in the farm scene: wherever the player is
// (plaza, mine, farm), the working farmer harvests ready slots one at a time
// (farmerHarvestSec each, fixed slot order), the yield goes into the bag and
// the same crop is replanted. A replant with no seed leaves the slot empty
// and waiting for that crop; it is planted as soon as the seed is back.
// The farm scene's otter (FarmerOtterController) only walks to HarvestTarget
// and plays the animation — it never harvests, so staying in the farm or
// being elsewhere gives the same result for the same time.
//
// Grow() is growth alone. It covers time the app was closed, which keeps its
// own offline rules (OfflineProductionService: registrations, slowdown,
// seeds) — the online farmer does not work through it.
public class FarmService
{
    // A long catch-up (scene load) runs harvests in time order; this only
    // guards against a data mistake (zero-length cycles) looping forever.
    private const int MaxStepsPerTick = 10000;

    private readonly SaveData save;
    private readonly Dictionary<string, CropDefinition> cropsById;
    private readonly FarmBalanceData balance;
    private readonly List<PlotRuntime> plots = new List<PlotRuntime>();
    private readonly IFarmHost host;
    private readonly List<string> noticeCrops = new List<string>();

    // Fired after a plot is unlocked mid-session (argument: plot index), so
    // things like the farmer otter can start covering it without a reload.
    public event Action<int> PlotUnlocked;

    // The farmer's harvest went into the bag (plot, slot, cropId, amount).
    public event Action<int, int, string, int> Harvested;

    // A farm problem started (once per problem, remembered in the save).
    public event Action<FarmNotice> NoticeRaised;

    public FarmService(SaveData save, IEnumerable<CropDefinition> crops, FarmBalanceData balance, IFarmHost host)
    {
        this.save = save;
        this.balance = balance;
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        cropsById = crops.Where(c => c != null).ToDictionary(c => c.cropId, c => c);

        save.farmerWork ??= new FarmerWorkSaveData();
        save.farmNotice ??= new FarmNoticeSaveData();
        NormalizePlots(save);
        foreach (var plotData in save.plots)
        {
            NormalizeSlots(plotData);
            plots.Add(new PlotRuntime(plotData, LookupCrop));
        }
    }

    // Saves written before the furrow/slot split (or any future slot-count
    // change) may have a mismatched slots list — JsonUtility just leaves it
    // at whatever the field initializer gave it. Pad it out so every plot
    // always has exactly PlotSaveData.SlotCount entries.
    private static void NormalizeSlots(PlotSaveData plotData)
    {
        plotData.slots ??= new List<FurrowSlotSaveData>();
        while (plotData.slots.Count < PlotSaveData.SlotCount)
        {
            plotData.slots.Add(new FurrowSlotSaveData { state = FurrowSlotState.Empty });
        }
    }

    // Saves created before plot unlocking existed only hold plot_1. Pad them
    // out with locked plots so the scene's Plot_2/Plot_3 always have data.
    private static void NormalizePlots(SaveData save)
    {
        save.plots ??= new List<PlotSaveData>();
        while (save.plots.Count < PlotSaveData.PlotCount)
        {
            save.plots.Add(new PlotSaveData
            {
                plotId = $"plot_{save.plots.Count + 1}",
                unlocked = false,
                slots = PlotSaveData.CreateEmptySlots()
            });
        }
    }

    public IReadOnlyList<PlotRuntime> Plots => plots;

    public bool HasAnyPlantedSlot =>
        plots.Any(p => p.Data.slots.Any(s => s.state != FurrowSlotState.Empty));

    public bool IsPlotUnlocked(int plotIndex) =>
        plotIndex >= 0 && plotIndex < plots.Count && plots[plotIndex].Data.unlocked;

    public int PlotUnlockCost => balance.plotUnlockCost;

    public CropDefinition GetCrop(string id) => LookupCrop(id);

    public bool CanUpgrade => save.farmLevel < balance.maxFarmLevel;

    public int NextUpgradeCost => CanUpgrade ? balance.upgradeCostByLevel[save.farmLevel - 1] : -1;

    public float FarmerHarvestSec => Math.Max(0.1f, balance.farmerHarvestSec);

    // ------------------------------------------------------------------ time

    // Growth only (time the app was closed). Nobody harvests or replants.
    public void Grow(float deltaSeconds)
    {
        if (deltaSeconds < 0f) return;
        foreach (var plot in plots)
        {
            plot.Tick(deltaSeconds);
        }
    }

    // Online time, in any zone scene: growth plus the farmer's work, in time
    // order (a long catch-up harvests what became ready along the way).
    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds < 0f) return;
        if (!host.CanFarmerWork)
        {
            Grow(deltaSeconds);
            return;
        }

        ResumeWaitingSlots();
        var work = save.farmerWork;
        float left = deltaSeconds;
        for (int i = 0; i < MaxStepsPerTick && (left > 0f || work.active); i++)
        {
            if (!work.active) StartNextHarvest();

            float step = work.active
                ? Math.Min(left, Math.Max(0f, work.remainingSec))
                : Math.Min(left, NextReadyInSec());
            Grow(step);
            left -= step;

            if (work.active)
            {
                work.remainingSec -= step;
                if (work.remainingSec > 0f) break; // out of time mid-harvest
                FinishHarvest();
            }
            else if (step <= 0f && !HasStorableReadySlot())
            {
                break;
            }
        }
        // 이번에 막 익은 칸은 바로 맡음 (남은 시간은 다음 틱부터)
        if (!work.active) StartNextHarvest();
        UpdateNotices();
    }

    // Seconds until the next growing slot is ready (MaxValue if none grows).
    private float NextReadyInSec()
    {
        float best = float.MaxValue;
        foreach (var plot in plots)
        {
            foreach (var slot in plot.Data.slots)
            {
                if (slot.state == FurrowSlotState.Growing) best = Math.Min(best, Math.Max(0f, slot.remainingSec));
            }
        }
        return best;
    }

    // ---------------------------------------------------------------- farmer

    // The slot the farmer is harvesting now (false when idle). The farm
    // scene's otter walks here and plays the harvest animation.
    public bool TryGetHarvestTarget(out int plotIndex, out int slotIndex)
    {
        var work = save.farmerWork;
        plotIndex = work.active ? work.plotIndex : -1;
        slotIndex = work.active ? work.slotIndex : -1;
        return work.active;
    }

    // 0..1 through the current harvest (0 when idle).
    public float HarvestProgress
    {
        get
        {
            var work = save.farmerWork;
            if (!work.active) return 0f;
            return Math.Max(0f, Math.Min(1f, 1f - work.remainingSec / FarmerHarvestSec));
        }
    }

    // First ready slot (plot order, then slot order) whose yield fits in the bag.
    private void StartNextHarvest()
    {
        for (int p = 0; p < plots.Count; p++)
        {
            if (!plots[p].Data.unlocked) continue;
            for (int s = 0; s < plots[p].SlotCount; s++)
            {
                if (!IsStorableReady(p, s)) continue;
                var work = save.farmerWork;
                work.active = true;
                work.plotIndex = p;
                work.slotIndex = s;
                work.remainingSec = FarmerHarvestSec;
                return;
            }
        }
    }

    private bool HasStorableReadySlot()
    {
        for (int p = 0; p < plots.Count; p++)
        {
            if (!plots[p].Data.unlocked) continue;
            for (int s = 0; s < plots[p].SlotCount; s++)
            {
                if (IsStorableReady(p, s)) return true;
            }
        }
        return false;
    }

    private bool IsStorableReady(int plotIndex, int slotIndex)
    {
        if (plots[plotIndex].GetSlotState(slotIndex) != FurrowSlotState.AwaitingHarvest) return false;
        var crop = plots[plotIndex].GetSlotCrop(slotIndex);
        return crop != null && host.CanStoreHarvest(crop.cropId, crop.yieldCount);
    }

    // The harvest time is up: into the bag, then the same crop again (or the
    // slot waits for its seed). A slot the player changed meanwhile, or whose
    // yield no longer fits, is left alone.
    private void FinishHarvest()
    {
        var work = save.farmerWork;
        int p = work.plotIndex;
        int s = work.slotIndex;
        work.active = false;
        work.remainingSec = 0f;

        if (!IsPlotUnlocked(p) || s < 0 || s >= plots[p].SlotCount) return;
        if (!IsStorableReady(p, s)) return;

        var crop = plots[p].GetSlotCrop(s);
        var harvested = plots[p].Harvest(s);
        foreach (var stack in harvested)
        {
            host.StoreHarvest(stack.itemId, stack.quantity);
        }
        Harvested?.Invoke(p, s, crop.cropId, crop.yieldCount);

        if (Plant(p, s, crop.cropId) == PlantResult.NoSeed) plots[p].MarkWaitingForSeed(s, crop.cropId);
    }

    // Debug panel: harvest this ready slot right now (same rules as the farmer).
    public bool HarvestNow(int plotIndex, int slotIndex)
    {
        if (!IsPlotUnlocked(plotIndex) || !IsStorableReady(plotIndex, slotIndex)) return false;
        var work = save.farmerWork;
        if (work.active && (work.plotIndex != plotIndex || work.slotIndex != slotIndex))
        {
            // Finish this one instead; the farmer picks the next slot afterwards.
            work.active = false;
        }
        work.active = true;
        work.plotIndex = plotIndex;
        work.slotIndex = slotIndex;
        work.remainingSec = 0f;
        FinishHarvest();
        UpdateNotices();
        return true;
    }

    // ----------------------------------------------------------------- seeds

    // Waiting slots whose seed is back are planted again, in slot order, so
    // several slots never spend the same last seed twice (Plant checks and
    // spends one at a time).
    private void ResumeWaitingSlots()
    {
        for (int p = 0; p < plots.Count; p++)
        {
            if (!plots[p].Data.unlocked) continue;
            for (int s = 0; s < plots[p].SlotCount; s++)
            {
                string cropId = plots[p].GetWaitingSeedCrop(s);
                if (string.IsNullOrEmpty(cropId) || plots[p].GetSlotState(s) != FurrowSlotState.Empty) continue;
                if (LookupCrop(cropId) == null)
                {
                    plots[p].Data.slots[s].waitingSeedCropId = null;
                    continue;
                }
                Plant(p, s, cropId);
            }
        }
    }

    // Empty slot waiting for this crop's seed (null if not waiting).
    public CropDefinition GetWaitingSeedCrop(int plotIndex, int slotIndex)
    {
        if (!IsPlotUnlocked(plotIndex)) return null;
        var slot = plots[plotIndex].Data.slots[slotIndex];
        return slot.state == FurrowSlotState.Empty ? LookupCrop(slot.waitingSeedCropId) : null;
    }

    // Slots waiting for seeds and their crops (each once, slot order).
    public int CollectSeedWaiting(List<string> cropIds)
    {
        cropIds?.Clear();
        int count = 0;
        for (int p = 0; p < plots.Count; p++)
        {
            for (int s = 0; s < plots[p].SlotCount; s++)
            {
                var crop = GetWaitingSeedCrop(p, s);
                if (crop == null) continue;
                count++;
                if (cropIds != null && !cropIds.Contains(crop.cropId)) cropIds.Add(crop.cropId);
            }
        }
        return count;
    }

    // Ready slots whose yield doesn't fit in the bag, and their crops.
    public int CollectStorageBlocked(List<string> cropIds)
    {
        cropIds?.Clear();
        int count = 0;
        for (int p = 0; p < plots.Count; p++)
        {
            if (!plots[p].Data.unlocked) continue;
            for (int s = 0; s < plots[p].SlotCount; s++)
            {
                if (plots[p].GetSlotState(s) != FurrowSlotState.AwaitingHarvest) continue;
                var crop = plots[p].GetSlotCrop(s);
                if (crop == null || host.CanStoreHarvest(crop.cropId, crop.yieldCount)) continue;
                count++;
                if (cropIds != null && !cropIds.Contains(crop.cropId)) cropIds.Add(crop.cropId);
            }
        }
        return count;
    }

    // --------------------------------------------------------------- notices

    // A notice when a problem starts (normal -> shortage / full), not on
    // every check, scene change or launch. Cleared when the problem goes
    // away, so the next one is told again. A save from before the notices
    // takes the farm as it is without announcing anything.
    private void UpdateNotices()
    {
        var notice = save.farmNotice;
        int waiting = CollectSeedWaiting(noticeCrops);
        bool shortage = waiting > 0;
        if (notice.initialized && shortage && !notice.seedShortage)
            NoticeRaised?.Invoke(new FarmNotice(FarmNoticeKind.SeedShortage, waiting, new List<string>(noticeCrops)));
        notice.seedShortage = shortage;

        int blocked = CollectStorageBlocked(noticeCrops);
        bool full = blocked > 0;
        if (notice.initialized && full && !notice.storageFull)
            NoticeRaised?.Invoke(new FarmNotice(FarmNoticeKind.StorageFull, blocked, new List<string>(noticeCrops)));
        notice.storageFull = full;

        notice.initialized = true;
    }

    // ----------------------------------------------------------------- slots

    public bool IsUnlimitedSeed(string cropId)
    {
        var crop = LookupCrop(cropId);
        return crop != null && crop.seedType == SeedType.Permanent;
    }

    // Remaining consumable seeds. Meaningless for permanent seeds — check
    // IsUnlimitedSeed first.
    public int GetSeedCount(string cropId) => host.SeedCount(cropId);

    // Shared by the player's crop-selection prompt and the farmer's replant.
    // Consumable seeds cost 1 per slot planted.
    public PlantResult Plant(int plotIndex, int slotIndex, string cropId)
    {
        if (plotIndex < 0 || plotIndex >= plots.Count) return PlantResult.Failed;

        var crop = LookupCrop(cropId);
        if (crop == null) return PlantResult.Failed;

        bool consumable = crop.seedType == SeedType.Consumable;
        if (consumable && host.SeedCount(cropId) <= 0) return PlantResult.NoSeed;

        if (!plots[plotIndex].Plant(slotIndex, cropId, CurrentDurationMultiplier)) return PlantResult.Failed;

        if (consumable) host.TryConsumeSeed(cropId);
        return PlantResult.Planted;
    }

    public bool ClearSlot(int plotIndex, int slotIndex)
    {
        if (plotIndex < 0 || plotIndex >= plots.Count) return false;
        return plots[plotIndex].ClearSlot(slotIndex);
    }

    public FurrowSlotState GetSlotState(int plotIndex, int slotIndex) => plots[plotIndex].GetSlotState(slotIndex);

    public CropDefinition GetSlotCrop(int plotIndex, int slotIndex) => plots[plotIndex].GetSlotCrop(slotIndex);

    public float GetSlotRemainingSec(int plotIndex, int slotIndex) => plots[plotIndex].GetSlotRemainingSec(slotIndex);

    public SlotGrowthStage GetSlotStage(int plotIndex, int slotIndex) =>
        plots[plotIndex].GetGrowthStage(slotIndex, CurrentDurationMultiplier);

    public bool TryUpgrade(CurrencyManager currencyManager, Currency currency)
    {
        if (!CanUpgrade) return false;

        int cost = NextUpgradeCost;
        if (cost > 0 && !currencyManager.TrySpend(currency, cost, TransactionSource.FarmUpgrade)) return false;

        save.farmLevel++;
        return true;
    }

    // TrySpend rejects a zero cost (throws), so a free unlock skips it.
    public bool TryUnlockPlot(int plotIndex, CurrencyManager currencyManager, Currency currency)
    {
        if (plotIndex < 0 || plotIndex >= plots.Count) return false;
        if (IsPlotUnlocked(plotIndex)) return false;

        int cost = PlotUnlockCost;
        if (cost > 0 && !currencyManager.TrySpend(currency, cost, TransactionSource.PlotUnlock)) return false;

        plots[plotIndex].Data.unlocked = true;
        PlotUnlocked?.Invoke(plotIndex);
        return true;
    }

    private CropDefinition LookupCrop(string id) =>
        !string.IsNullOrEmpty(id) && cropsById.TryGetValue(id, out var c) ? c : null;

    private float CurrentDurationMultiplier => balance.DurationMultiplierAt(save.farmLevel);
}
