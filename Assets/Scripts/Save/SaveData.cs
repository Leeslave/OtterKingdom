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
    // This crop's own grow time when it was set specially (the very first
    // crop grows in 10s). 0 = from the crop and farm level as usual.
    public float durationSec;
    // Empty slot whose automatic replant ran out of seeds: the crop to plant
    // again once the seed is back in the bag (null = nothing waiting).
    public string waitingSeedCropId;
}

// The farmer's current harvest, kept in the save so it carries over scene
// changes (every zone's GameManager ticks the farm, not only the farm's).
[Serializable]
public class FarmerWorkSaveData
{
    public bool active;
    public int plotIndex;
    public int slotIndex;
    public float remainingSec;
}

// Which farm problems the player was already told about, so a notice comes
// once when the problem starts — not again on every scene change or launch.
// initialized false = a save from before this existed: taken from the farm
// as it is, without announcing anything.
[Serializable]
public class FarmNoticeSaveData
{
    public bool initialized;
    public bool seedShortage;
    public bool storageFull;
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

// One crop registered with the farm NPC for offline growing. Not tied to a
// real furrow slot — the NPC's list only has as many entries in use as there
// are unlocked slots. progressSec carries a half-grown cycle over to the next
// time the game is closed.
[Serializable]
public class OfflineFarmSlotSaveData
{
    public string cropId;
    public float progressSec;
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
    // 2: firstPlantGuideDone exists (1-saves are checked once on load).
    // 3: tutorialsDone exists (older saves skip every zone tutorial).
    // 4: settlement exists (older saves get every board request completed,
    //    so a farm they already use isn't locked again).
    // The upgrade steps live in SaveMigrations — raise this and add a step there.
    public const int CurrentSchemaVersion = 4;
    public int schemaVersion = CurrentSchemaVersion;
    public int lifetimeSales;
    public int farmLevel = 1;
    public int rodLevel = 1;
    public int pickaxeLevel = 1;
    // True while the fishing otter is assigned to the fishing spot (between
    // the player's "start fishing" and "stop fishing" confirmations).
    public bool fishingActive;
    // True while the miner otter is assigned to the mine (between the
    // player's "start mining" and "stop mining" confirmations).
    public bool miningActive;
    // True once the player has planted for the first time, which ends the
    // "당근을 심어 볼까?" guide and the plot highlight for good.
    public bool firstPlantGuideDone;
    // Scene names (Plaza, Farm, Fishing, Mine) whose first-visit tutorial has
    // been seen or skipped.
    public List<string> tutorialsDone = new List<string>();
    public string lastSaveUtc;
    public List<PlotSaveData> plots = new List<PlotSaveData>();
    // Farmer's harvest in progress (FarmService). Missing in older saves = none.
    public FarmerWorkSaveData farmerWork = new FarmerWorkSaveData();
    public FarmNoticeSaveData farmNotice = new FarmNoticeSaveData();
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
    // Crops registered with the farm NPC (farm level 2+), grown only while
    // the game is closed. Empty cropId = an unused registration.
    public List<OfflineFarmSlotSaveData> offlineFarmSlots = new List<OfflineFarmSlotSaveData>();
    // Offline time already spent towards the next catch (rod level 2+).
    public float offlineFishingProgressSec;
    // Offline time already spent towards the next ore find (pickaxe level 2+).
    public float offlineMiningProgressSec;
    // Online mining: seconds left until the next find while mining is on.
    // Kept in the save so it carries over scene changes (every zone's
    // GameManager ticks it, not only the mine's). 0 = roll a new interval.
    public float miningSecToNextFind;
    // Offline time already spent towards the next otter visit roll.
    public float offlineOtterVisitProgressSec;
    public List<OtterSaveData> otters = new List<OtterSaveData>();
    public List<CurrencyBalance> currencies = new List<CurrencyBalance>();
    // Owned by CollectionManager / QuestManager (GlobalUI); copied in and out
    // by GameManager. Missing (null) in saves from before they existed.
    public List<CollectionSaveEntry> collection = new List<CollectionSaveEntry>();
    public List<QuestSaveEntry> quests = new List<QuestSaveEntry>();
    // Owned by ProfileManager (GlobalUI): name and kingdom level/exp.
    // Missing in older saves -> JsonUtility fills defaults (Lv.1).
    public ProfileSaveData profile = new ProfileSaveData();
    // Owned by SettlementManager (GlobalUI): kingdom stage, residents, board
    // requests, construction. Missing in saves before schema 4.
    public SettlementSaveData settlement = new SettlementSaveData();
    // Owned by DecorManager (GlobalUI): toys placed on each zone's grid and
    // unlocked decor regions. Missing in older saves -> nothing placed.
    public DecorSaveData decor = new DecorSaveData();
}
