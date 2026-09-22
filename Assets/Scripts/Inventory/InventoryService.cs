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

    public void AddRange(IEnumerable<ItemStack> items)
    {
        foreach (var item in items) Add(item.itemId, item.quantity);
    }

    public void Clear()
    {
        bag.Clear();
    }
}
