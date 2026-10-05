using System.Collections.Generic;

// What happened while the game was closed, for the return popup. Each list
// holds one entry per item id, in the order the item first showed up.
public class OfflineReport
{
    // Real time away, and the part of it that counted (capped at
    // OfflineProductionService.MaxCreditedSec).
    public double ElapsedSec;
    public double CreditedSec;
    public bool Capped => ElapsedSec > CreditedSec;
    public readonly List<ItemStack> Received = new List<ItemStack>();
    // Harvested or caught but didn't fit in the bag.
    public readonly List<ItemStack> Lost = new List<ItemStack>();
    // Keyed by seed item id (seed_*).
    public readonly List<ItemStack> SeedsUsed = new List<ItemStack>();
    // cropIds whose registrations stopped because the seeds ran out.
    public readonly List<string> OutOfSeeds = new List<string>();
    // Names of otters that visited, in visit order (repeats possible).
    public readonly List<string> OtterVisits = new List<string>();
    // Settlement news lines: construction finished or still going, plaza
    // gather spots that grew back (SettlementManager.CollectAbsenceNews).
    public readonly List<string> SettlementNews = new List<string>();
    // The board request to do next. Shown at the bottom, but on its own
    // doesn't make the popup appear.
    public string NextGoal;

    public bool HasAnything => Received.Count > 0 || Lost.Count > 0 || OutOfSeeds.Count > 0 || OtterVisits.Count > 0
                               || SettlementNews.Count > 0;

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
