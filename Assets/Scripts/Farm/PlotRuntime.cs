using System;
using System.Collections.Generic;

// Wraps one PlotSaveData with production logic. Tick() accepts arbitrarily
// large deltaSeconds so the same method drives both per-frame updates and
// offline catch-up on load.
public class PlotRuntime
{
    public readonly PlotSaveData Data;
    private readonly Func<string, CropDefinition> cropLookup;
    private readonly int storageCapacityCycles;

    public PlotRuntime(PlotSaveData data, Func<string, CropDefinition> cropLookup, int storageCapacityCycles)
    {
        Data = data;
        this.cropLookup = cropLookup;
        this.storageCapacityCycles = storageCapacityCycles;
    }

    public CropDefinition ActiveCrop =>
        string.IsNullOrEmpty(Data.activeCropId) ? null : cropLookup(Data.activeCropId);

    public bool HasWorker => !string.IsNullOrEmpty(Data.workerId);

    public bool SelectCrop(string cropId, float levelDurationMultiplier)
    {
        if (!Data.unlocked || cropLookup(cropId) == null) return false;

        Data.activeCropId = cropId;
        TryStartProduction(levelDurationMultiplier);
        return true;
    }

    public void AssignWorker(string workerId, float levelDurationMultiplier)
    {
        Data.workerId = workerId;
        TryStartProduction(levelDurationMultiplier);
    }

    public void Tick(float deltaSeconds, float levelDurationMultiplier)
    {
        while (deltaSeconds > 0f && Data.state == PlotState.Producing)
        {
            if (deltaSeconds < Data.remainingSec)
            {
                Data.remainingSec -= deltaSeconds;
                return;
            }

            deltaSeconds -= Data.remainingSec;
            CompleteCycle(levelDurationMultiplier);
        }
    }

    // Takes the whole plot storage at once (no partial harvest, per design doc 5.2)
    // and leaves any in-progress cycle untouched.
    public List<ItemStack> Harvest(float levelDurationMultiplier)
    {
        var harvested = new List<ItemStack>(Data.storedItems);
        Data.storedItems.Clear();
        Data.storedCompletedCycles = 0;

        if (Data.state == PlotState.Full)
        {
            Data.state = PlotState.Idle;
            TryStartProduction(levelDurationMultiplier);
        }

        return harvested;
    }

    private void TryStartProduction(float levelDurationMultiplier)
    {
        if (Data.state == PlotState.Full || Data.state == PlotState.Producing) return;
        if (!HasWorker || ActiveCrop == null) return;

        Data.remainingSec = ComputeDuration(ActiveCrop, levelDurationMultiplier);
        Data.state = PlotState.Producing;
    }

    private void CompleteCycle(float levelDurationMultiplier)
    {
        var crop = ActiveCrop;
        if (crop == null)
        {
            Data.state = PlotState.Idle;
            return;
        }

        AddToStorage(crop.cropId, crop.yieldCount);
        Data.storedCompletedCycles++;

        if (Data.storedCompletedCycles >= storageCapacityCycles)
        {
            Data.state = PlotState.Full;
            Data.remainingSec = 0f;
            return;
        }

        // Duration is recomputed fresh for each new cycle, so a level-up mid-cycle
        // only takes effect starting next cycle (design doc 7.2).
        Data.remainingSec = ComputeDuration(crop, levelDurationMultiplier);
    }

    private void AddToStorage(string itemId, int quantity)
    {
        var stack = Data.storedItems.Find(s => s.itemId == itemId);
        if (stack != null) stack.quantity += quantity;
        else Data.storedItems.Add(new ItemStack(itemId, quantity));
    }

    private static float ComputeDuration(CropDefinition crop, float multiplier)
    {
        return (float)Math.Ceiling(crop.baseDurationSec * multiplier);
    }
}
