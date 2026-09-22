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
public class PlotSaveData
{
    public string plotId;
    public bool unlocked;
    public string activeCropId;
    public PlotState state;
    public float remainingSec;
    public int storedCompletedCycles;
    public List<ItemStack> storedItems = new List<ItemStack>();
    public string workerId;
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
    public string lastSaveUtc;
    public List<PlotSaveData> plots = new List<PlotSaveData>();
    public List<ItemStack> inventory = new List<ItemStack>();
    public List<OtterSaveData> otters = new List<OtterSaveData>();
    public List<CurrencyBalance> currencies = new List<CurrencyBalance>();
}
