using UnityEngine;

// Fishing rules with no scene dependencies: rod level/upgrade, the fish vs
// trash roll, bite delay, and whether the fishing otter is on duty. Whether
// fishing is active is saved so the otter goes straight back to its spot on
// the next visit. Catches in progress are not saved — leaving the scene
// mid-catch throws that catch away, same as the player stopping it.
public class FishingService
{
    private readonly SaveData save;
    private readonly FishingBalanceData balance;

    public FishingService(SaveData save, FishingBalanceData balance)
    {
        this.save = save;
        this.balance = balance;
        save.rodLevel = Mathf.Clamp(save.rodLevel, 1, MaxRodLevel);
    }

    public FishingBalanceData Balance => balance;

    public bool IsActive => save.fishingActive;
    public void SetActive(bool active) => save.fishingActive = active;

    public int RodLevel => save.rodLevel;
    public int MaxRodLevel => balance.MaxRodLevel;
    public bool CanUpgradeRod => RodLevel < MaxRodLevel;
    public int NextRodUpgradeCost => CanUpgradeRod ? balance.rodUpgradeCosts[RodLevel - 1] : 0;

    public float FishChance => FishChanceAt(RodLevel);

    public float FishChanceAt(int rodLevel) => balance.FishChanceAt(rodLevel);

    public float RollBiteDelaySec()
    {
        Vector2 range = balance.biteDelaySec;
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    // Returns the item id of what was hooked.
    public string RollCatch()
    {
        return Random.value < FishChance ? balance.fishItemId : balance.trashItemId;
    }

    public bool IsFish(string itemId) => itemId == balance.fishItemId;

    // TrySpend rejects a zero cost (throws), so a free upgrade skips it
    // (same as FarmService.TryUnlockPlot).
    public bool TryUpgradeRod(CurrencyManager currencyManager, Currency currency)
    {
        if (!CanUpgradeRod) return false;

        int cost = NextRodUpgradeCost;
        if (cost > 0 && !currencyManager.TrySpend(currency, cost, TransactionSource.RodUpgrade)) return false;

        save.rodLevel++;
        return true;
    }
}
