using System.Collections.Generic;

// What happened while the game was closed, for the return popup. Each list
// holds one entry per item id, in the order the item first showed up.
public class OfflineReport
{
    public double ElapsedSec;
    public readonly List<ItemStack> Received = new List<ItemStack>();
    // Harvested or caught but didn't fit in the bag.
    public readonly List<ItemStack> Lost = new List<ItemStack>();
    // Keyed by seed item id (seed_*).
    public readonly List<ItemStack> SeedsUsed = new List<ItemStack>();
    // cropIds whose registrations stopped because the seeds ran out.
    public readonly List<string> OutOfSeeds = new List<string>();
    // Names of otters that visited, in visit order (repeats possible).
    public readonly List<string> OtterVisits = new List<string>();

    public bool HasAnything => Received.Count > 0 || Lost.Count > 0 || OutOfSeeds.Count > 0 || OtterVisits.Count > 0;

    public static void AddTo(List<ItemStack> stacks, string itemId, int quantity)
    {
        if (quantity <= 0) return;

        var stack = stacks.Find(s => s.itemId == itemId);
        if (stack != null) stack.quantity += quantity;
        else stacks.Add(new ItemStack(itemId, quantity));
    }

    public static int CountOf(List<ItemStack> stacks, string itemId)
    {
        var stack = stacks.Find(s => s.itemId == itemId);
        return stack != null ? stack.quantity : 0;
    }
}
