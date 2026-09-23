using System.Collections.Generic;
using System.Linq;

public class FarmService
{
    private readonly SaveData save;
    private readonly Dictionary<string, CropDefinition> cropsById;
    private readonly FarmBalanceData balance;
    private readonly List<PlotRuntime> plots = new List<PlotRuntime>();

    public FarmService(SaveData save, IEnumerable<CropDefinition> crops, FarmBalanceData balance)
    {
        this.save = save;
        this.balance = balance;
        cropsById = crops.ToDictionary(c => c.cropId, c => c);

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

    public IReadOnlyList<PlotRuntime> Plots => plots;

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

    public bool Plant(int plotIndex, int slotIndex, string cropId)
    {
        if (plotIndex < 0 || plotIndex >= plots.Count) return false;
        return plots[plotIndex].Plant(slotIndex, cropId, CurrentDurationMultiplier);
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
        if (!currencyManager.TrySpend(currency, cost, TransactionSource.FarmUpgrade)) return false;

        save.farmLevel++;
        return true;
    }

    private CropDefinition LookupCrop(string id) => cropsById.TryGetValue(id, out var c) ? c : null;

    private float CurrentDurationMultiplier
    {
        get
        {
            int index = save.farmLevel - 1;
            if (index < 0) index = 0;
            if (index >= balance.durationMultiplierByLevel.Length) index = balance.durationMultiplierByLevel.Length - 1;
            return balance.durationMultiplierByLevel[index];
        }
    }
}
