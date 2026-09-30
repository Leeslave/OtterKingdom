using System;
using System.Collections.Generic;
using System.Linq;

public class FarmService
{
    private readonly SaveData save;
    private readonly Dictionary<string, CropDefinition> cropsById;
    private readonly FarmBalanceData balance;
    private readonly List<PlotRuntime> plots = new List<PlotRuntime>();
    // Consumable seeds are bag items owned by the inventory; the farm only
    // asks how many there are and spends one per planting (argument: cropId).
    private readonly Func<string, int> seedCount;
    private readonly Func<string, bool> tryConsumeSeed;

    // Fired after a plot is unlocked mid-session (argument: plot index), so
    // things like the farmer otter can start covering it without a reload.
    public event Action<int> PlotUnlocked;

    public FarmService(SaveData save, IEnumerable<CropDefinition> crops, FarmBalanceData balance,
        Func<string, int> seedCount, Func<string, bool> tryConsumeSeed)
    {
        this.save = save;
        this.balance = balance;
        this.seedCount = seedCount;
        this.tryConsumeSeed = tryConsumeSeed;
        cropsById = crops.ToDictionary(c => c.cropId, c => c);

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

    public void Tick(float deltaSeconds)
    {
        foreach (var plot in plots)
        {
            plot.Tick(deltaSeconds);
        }
    }

    public bool IsUnlimitedSeed(string cropId)
    {
        var crop = LookupCrop(cropId);
        return crop != null && crop.seedType == SeedType.Permanent;
    }

    // Remaining consumable seeds. Meaningless for permanent seeds — check
    // IsUnlimitedSeed first.
    public int GetSeedCount(string cropId) => seedCount(cropId);

    // Shared by the player's crop-selection prompt and the otter's replant.
    // Consumable seeds cost 1 per slot planted.
    public PlantResult Plant(int plotIndex, int slotIndex, string cropId)
    {
        if (plotIndex < 0 || plotIndex >= plots.Count) return PlantResult.Failed;

        var crop = LookupCrop(cropId);
        if (crop == null) return PlantResult.Failed;

        bool consumable = crop.seedType == SeedType.Consumable;
        if (consumable && seedCount(cropId) <= 0) return PlantResult.NoSeed;

        if (!plots[plotIndex].Plant(slotIndex, cropId, CurrentDurationMultiplier)) return PlantResult.Failed;

        if (consumable) tryConsumeSeed(cropId);
        return PlantResult.Planted;
    }

    public bool ClearSlot(int plotIndex, int slotIndex)
    {
        if (plotIndex < 0 || plotIndex >= plots.Count) return false;
        return plots[plotIndex].ClearSlot(slotIndex);
    }

    public List<ItemStack> Harvest(int plotIndex, int slotIndex)
    {
        if (plotIndex < 0 || plotIndex >= plots.Count) return new List<ItemStack>();
        return plots[plotIndex].Harvest(slotIndex);
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

    private CropDefinition LookupCrop(string id) => cropsById.TryGetValue(id, out var c) ? c : null;

    private float CurrentDurationMultiplier => balance.DurationMultiplierAt(save.farmLevel);
}
