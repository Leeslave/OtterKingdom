using System.Collections.Generic;

public class InventoryService
{
    private readonly List<ItemStack> bag;

    public InventoryService(List<ItemStack> bag)
    {
        this.bag = bag;
    }

    public IReadOnlyList<ItemStack> Items => bag;

    public void Add(string itemId, int quantity)
    {
        if (quantity <= 0) return;

        var stack = bag.Find(s => s.itemId == itemId);
        if (stack != null) stack.quantity += quantity;
        else bag.Add(new ItemStack(itemId, quantity));
    }

    public int GetQuantity(string itemId)
    {
        var stack = bag.Find(s => s.itemId == itemId);
        return stack != null ? stack.quantity : 0;
    }

    public bool Remove(string itemId, int quantity)
    {
        if (quantity <= 0) return false;

        var stack = bag.Find(s => s.itemId == itemId);
        if (stack == null || stack.quantity < quantity) return false;

        stack.quantity -= quantity;
        if (stack.quantity == 0) bag.Remove(stack);
        return true;
    }

    public void AddRange(IEnumerable<ItemStack> items)
    {
        foreach (var item in items) Add(item.itemId, item.quantity);
    }

    public void Clear()
    {
        bag.Clear();
    }
}
