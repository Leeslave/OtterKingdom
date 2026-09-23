using System;
using System.Collections.Generic;

// Wraps one PlotSaveData (a furrow with up to SlotCount independent crop
// slots). Tick() accepts arbitrarily large deltaSeconds so the same method
// drives both per-frame updates and offline catch-up on load. Unlike the old
// single-crop-per-plot model, a slot only ever runs one grow cycle: once it
// reaches AwaitingHarvest it stays there until something calls Harvest() —
// there is no auto-restart, so no multi-cycle loop is needed here.
public class PlotRuntime
{
    public readonly PlotSaveData Data;
    private readonly Func<string, CropDefinition> cropLookup;

    public PlotRuntime(PlotSaveData data, Func<string, CropDefinition> cropLookup)
    {
        Data = data;
        this.cropLookup = cropLookup;
    }

    public int SlotCount => Data.slots.Count;

    public FurrowSlotState GetSlotState(int slotIndex) => Data.slots[slotIndex].state;

    public CropDefinition GetSlotCrop(int slotIndex)
    {
        string cropId = Data.slots[slotIndex].cropId;
        return string.IsNullOrEmpty(cropId) ? null : cropLookup(cropId);
    }

    public float GetSlotRemainingSec(int slotIndex) => Data.slots[slotIndex].remainingSec;

    public SlotGrowthStage GetGrowthStage(int slotIndex, float levelDurationMultiplier)
    {
        var slot = Data.slots[slotIndex];
        if (slot.state == FurrowSlotState.Empty) return SlotGrowthStage.None;
        if (slot.state == FurrowSlotState.AwaitingHarvest) return SlotGrowthStage.Grown;

        var crop = cropLookup(slot.cropId);
        float duration = crop != null ? ComputeDuration(crop, levelDurationMultiplier) : slot.remainingSec;
        if (duration <= 0f) return SlotGrowthStage.Grown;

        float elapsed = duration - slot.remainingSec;
        if (elapsed < duration / 3f) return SlotGrowthStage.Seed;
        if (elapsed < duration * 2f / 3f) return SlotGrowthStage.Sprout;
        return SlotGrowthStage.Grown;
    }

    public bool Plant(int slotIndex, string cropId, float levelDurationMultiplier)
    {
        if (!Data.unlocked) return false;
        if (slotIndex < 0 || slotIndex >= Data.slots.Count) return false;

        var slot = Data.slots[slotIndex];
        if (slot.state != FurrowSlotState.Empty) return false;

        var crop = cropLookup(cropId);
        if (crop == null) return false;

        slot.cropId = cropId;
        slot.state = FurrowSlotState.Growing;
        slot.remainingSec = ComputeDuration(crop, levelDurationMultiplier);
        return true;
    }

    public void Tick(float deltaSeconds)
    {
        foreach (var slot in Data.slots)
        {
            if (slot.state != FurrowSlotState.Growing) continue;

            slot.remainingSec -= deltaSeconds;
            if (slot.remainingSec <= 0f)
            {
                slot.remainingSec = 0f;
                slot.state = FurrowSlotState.AwaitingHarvest;
            }
        }
    }

    // Called by whatever performs the harvest (currently a debug-panel button
    // standing in for the future otter NPC trigger). Empties the slot so it
    // can be replanted.
    public List<ItemStack> Harvest(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= Data.slots.Count) return new List<ItemStack>();

        var slot = Data.slots[slotIndex];
        if (slot.state != FurrowSlotState.AwaitingHarvest) return new List<ItemStack>();

        var crop = cropLookup(slot.cropId);
        var result = new List<ItemStack>();
        if (crop != null) result.Add(new ItemStack(crop.cropId, crop.yieldCount));

        slot.cropId = null;
        slot.state = FurrowSlotState.Empty;
        slot.remainingSec = 0f;
        return result;
    }

    private static float ComputeDuration(CropDefinition crop, float multiplier)
    {
        return (float)Math.Ceiling(crop.baseDurationSec * multiplier);
    }
}
