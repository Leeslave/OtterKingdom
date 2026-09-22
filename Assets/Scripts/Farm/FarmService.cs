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
            plots.Add(new PlotRuntime(plotData, LookupCrop, balance.storageCapacityCycles));
        }
    }

    public IReadOnlyList<PlotRuntime> Plots => plots;

    public CropDefinition GetCrop(string id) => LookupCrop(id);

    public bool CanUpgrade => save.farmLevel < balance.maxFarmLevel;

    public int NextUpgradeCost => CanUpgrade ? balance.upgradeCostByLevel[save.farmLevel - 1] : -1;

    public void Tick(float deltaSeconds)
    {
        float multiplier = CurrentDurationMultiplier;
        foreach (var plot in plots)
        {
            plot.Tick(deltaSeconds, multiplier);
        }
    }

    public bool SelectCrop(int plotIndex, string cropId)
    {
        if (plotIndex < 0 || plotIndex >= plots.Count) return false;
        return plots[plotIndex].SelectCrop(cropId, CurrentDurationMultiplier);
    }

    public List<ItemStack> Harvest(int plotIndex)
    {
        if (plotIndex < 0 || plotIndex >= plots.Count) return new List<ItemStack>();
        return plots[plotIndex].Harvest(CurrentDurationMultiplier);
    }

    public void AssignWorker(int plotIndex, string workerId)
    {
        if (plotIndex < 0 || plotIndex >= plots.Count) return;
        plots[plotIndex].AssignWorker(workerId, CurrentDurationMultiplier);
    }

    public bool TryUpgrade(EconomyService economy)
    {
        if (!CanUpgrade) return false;

        int cost = NextUpgradeCost;
        if (!economy.TrySpend(cost)) return false;

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
