using System;
using System.Collections.Generic;

[Serializable]
public class ItemStack
{
    public string itemId;
    public int quantity;

    public ItemStack() { }

    public ItemStack(string itemId, int quantity)
    {
        this.itemId = itemId;
        this.quantity = quantity;
    }
}

[Serializable]
public class FurrowSlotSaveData
{
    public string cropId;
    public FurrowSlotState state;
    public float remainingSec;
}

[Serializable]
public class PlotSaveData
{
    public const int SlotCount = 3;
    public const int PlotCount = 3;

    public string plotId;
    public bool unlocked;
    public string workerId;
    public List<FurrowSlotSaveData> slots = new List<FurrowSlotSaveData>();

    public static List<FurrowSlotSaveData> CreateEmptySlots()
    {
        var slots = new List<FurrowSlotSaveData>(SlotCount);
        for (int i = 0; i < SlotCount; i++)
        {
            slots.Add(new FurrowSlotSaveData { state = FurrowSlotState.Empty });
        }
        return slots;
    }
}

[Serializable]
public class OtterSaveData
{
    public string instanceId;
    public string speciesId;
    public bool unlocked;
}

[Serializable]
public class CurrencyBalance
{
    public string currencyId;
    public int amount;
}

[Serializable]
public class SaveData
{
    public int schemaVersion = 1;
    public int lifetimeSales;
    public int farmLevel = 1;
    public int rodLevel = 1;
    // True while the fishing otter is assigned to the fishing spot (between
    // the player's "start fishing" and "stop fishing" confirmations).
    public bool fishingActive;
    public string lastSaveUtc;
    public List<PlotSaveData> plots = new List<PlotSaveData>();
    public List<ItemStack> inventory = new List<ItemStack>();
    // Unlocked bag slots. 0 (saves from before the bag had a limit) means
    // InventoryConfig's initial capacity.
    public int inventoryCapacity;
    // Legacy: consumable-seed stock keyed by cropId, from before seeds became
    // bag items (seed_*). GameManager moves these into the bag on load; an
    // entry only stays here if its seed item has no ItemDefinition yet.
    public List<ItemStack> seeds = new List<ItemStack>();
    // cropIds whose starting seed stock (CropDefinition.initialSeedCount) has
    // already been put in the bag, so it is never granted twice.
    public List<string> starterSeedsGranted = new List<string>();
    public List<OtterSaveData> otters = new List<OtterSaveData>();
    public List<CurrencyBalance> currencies = new List<CurrencyBalance>();
}
