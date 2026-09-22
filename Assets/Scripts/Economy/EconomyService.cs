using System;

public class EconomyService
{
    private readonly SaveData save;

    public EconomyService(SaveData save)
    {
        this.save = save;
    }

    public int Coins => save.coins;

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        save.coins += amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || save.coins < amount) return false;
        save.coins -= amount;
        return true;
    }

    public void RegisterSale(int amount)
    {
        if (amount > 0) save.lifetimeSales += amount;
    }

    // Sells the entire bag at once (MVP has only one sell action) and clears it.
    public int SellAll(InventoryService inventory, Func<string, int> sellPriceLookup)
    {
        int total = 0;
        foreach (var stack in inventory.Items)
        {
            total += sellPriceLookup(stack.itemId) * stack.quantity;
        }

        if (total > 0)
        {
            AddCoins(total);
            RegisterSale(total);
            inventory.Clear();
        }

        return total;
    }
}
