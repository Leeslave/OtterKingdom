using UnityEngine;

// Mining rules with no scene dependencies: pickaxe level/upgrade, the diamond
// vs stone roll, time between finds, and whether the miner otter is inside.
// Once the otter is put in the mine, mining keeps going in every scene:
// GameManager (one per zone scene) calls Tick each frame, and the time
// towards the next find is saved so a scene change doesn't reset it. The
// mine scene only shows it (MinerOtterController's bubble).
public class MiningService
{
    private readonly SaveData save;
    private readonly MiningBalanceData balance;

    public MiningService(SaveData save, MiningBalanceData balance)
    {
        this.save = save;
        this.balance = balance;
        save.pickaxeLevel = Mathf.Clamp(save.pickaxeLevel, 1, MaxPickaxeLevel);
    }

    public MiningBalanceData Balance => balance;

    public bool IsActive => save.miningActive;

    public void SetActive(bool active)
    {
        save.miningActive = active;
        // Stopping throws away the time towards the next find
        save.miningSecToNextFind = 0f;
    }

    // Advances online mining by deltaSec. Returns the item id dug up this
    // frame, or null (not mining / not yet). At most one find per call.
    public string Tick(float deltaSec)
    {
        if (!IsActive) return null;

        if (save.miningSecToNextFind <= 0f) save.miningSecToNextFind = RollFindIntervalSec();
        save.miningSecToNextFind -= deltaSec;
        if (save.miningSecToNextFind > 0f) return null;

        save.miningSecToNextFind = RollFindIntervalSec();
        return RollFind();
    }

    public int PickaxeLevel => save.pickaxeLevel;
    public int MaxPickaxeLevel => balance.MaxPickaxeLevel;
    public bool CanUpgradePickaxe => PickaxeLevel < MaxPickaxeLevel;
    public int NextPickaxeUpgradeCost => CanUpgradePickaxe ? balance.pickaxeUpgradeCosts[PickaxeLevel - 1] : 0;

    public float DiamondChance => DiamondChanceAt(PickaxeLevel);

    public float DiamondChanceAt(int pickaxeLevel) => balance.DiamondChanceAt(pickaxeLevel);

    public float RollFindIntervalSec()
    {
        Vector2 range = balance.findIntervalSec;
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    // Returns the item id of what was dug up.
    public string RollFind()
    {
        return Random.value < DiamondChance ? balance.diamondItemId : balance.stoneItemId;
    }

    public bool IsOre(string itemId) => itemId == balance.diamondItemId || itemId == balance.stoneItemId;

    // TrySpend rejects a zero cost (throws), so a free upgrade skips it
    // (same as FishingService.TryUpgradeRod).
    public bool TryUpgradePickaxe(CurrencyManager currencyManager, Currency currency)
    {
        if (!CanUpgradePickaxe) return false;

        int cost = NextPickaxeUpgradeCost;
        if (cost > 0 && !currencyManager.TrySpend(currency, cost, TransactionSource.PickaxeUpgrade)) return false;

        save.pickaxeLevel++;
        return true;
    }
}
