using System;
using System.Collections.Generic;
using System.Linq;

// Production while the game is closed, applied in one go on return. No scene
// or animation involved — nothing here waits for the farmer or fishing otter.
//
// Farm (farm level >= offlineUnlockLevel): only the crops registered with the
// farm NPC grow, one registration per unlocked slot, regardless of what is
// actually planted in the furrows. Each finished cycle spends one seed
// (consumable crops) and puts offlineYieldPerHarvest vegetables (not the
// crop's online yield) in the bag; a registration whose
// seeds run out stops there. Harvests are processed in time order so crops
// sharing a seed stock take seeds in the order they finish.
//
// Fishing (rod level >= offlineUnlockRodLevel): one catch per
// OfflineSecPerCatch, each rolled fish/trash at the rod's fish chance. The
// fishing otter doesn't need to be on duty.
//
// Mining (pickaxe level >= offlineUnlockPickaxeLevel): the same, one find
// per OfflineSecPerFind rolled diamond/stone at the pickaxe's diamond chance.
//
// All three run offlineSlowdown times slower than online, and an absence
// counts for at most MaxCreditedSec (8 hours): leaving longer, or pushing the
// device clock forward, gives no more than that.
//
// Otter visits (always on): one roll per rollIntervalSec, each a
// visitChancePerRoll chance that a random otter came by. Only reported for
// now — nothing is saved about who visited.
//
// Leftover time towards the next harvest/catch/roll is kept in the save, so
// short absences add up. Anything that doesn't fit in the bag is lost and
// reported.
public class OfflineProductionService
{
    // Design doc 8.1: one absence earns at most 8 hours of offline production.
    public const double MaxCreditedSec = 8 * 3600;

    public static double Credited(double elapsedSec) => Math.Min(Math.Max(0, elapsedSec), MaxCreditedSec);

    private readonly Dictionary<string, CropDefinition> cropsById;
    private readonly FarmBalanceData farmBalance;
    private readonly FishingBalanceData fishingBalance;
    private readonly OtterVisitBalanceData otterVisitBalance;
    private readonly MiningBalanceData miningBalance;
    private readonly Random random;

    public OfflineProductionService(IEnumerable<CropDefinition> crops, FarmBalanceData farmBalance,
        FishingBalanceData fishingBalance, OtterVisitBalanceData otterVisitBalance, MiningBalanceData miningBalance,
        Random random = null)
    {
        cropsById = crops.Where(c => c != null).ToDictionary(c => c.cropId, c => c);
        this.farmBalance = farmBalance;
        this.fishingBalance = fishingBalance;
        this.otterVisitBalance = otterVisitBalance;
        this.miningBalance = miningBalance;
        this.random = random ?? new Random();
    }

    public bool IsFarmUnlocked(SaveData save) => save.farmLevel >= farmBalance.offlineUnlockLevel;

    public bool IsFishingUnlocked(SaveData save) => save.rodLevel >= fishingBalance.offlineUnlockRodLevel;

    public bool IsMiningUnlocked(SaveData save) => save.pickaxeLevel >= miningBalance.offlineUnlockPickaxeLevel;

    // How many crops the farm NPC accepts: one per unlocked slot.
    public static int RegistrationLimit(SaveData save)
    {
        int count = 0;
        foreach (var plot in save.plots)
        {
            if (plot == null || !plot.unlocked) continue;
            if (plot.slots == null || plot.slots.Count == 0)
            {
                count += PlotSaveData.SlotCount;
                continue;
            }
            foreach (var slot in plot.slots)
            {
                if (slot != null && !slot.locked) count++;
            }
        }
        return count;
    }

    // farmAllowed / miningAllowed / fishingAllowed: whether that zone's
    // specialist (farmer / miner / fisher) is working. Until then the zone
    // produces nothing offline either.
    public OfflineReport Run(SaveData save, double elapsedSec, IOfflineBag bag,
        bool farmAllowed = true, bool miningAllowed = true, bool fishingAllowed = true)
    {
        var report = new OfflineReport { ElapsedSec = elapsedSec, CreditedSec = Credited(elapsedSec) };
        if (elapsedSec <= 0) return report;
        elapsedSec = report.CreditedSec;

        if (farmAllowed && IsFarmUnlocked(save)) RunFarm(save, elapsedSec, bag, report);
        if (fishingAllowed && IsFishingUnlocked(save)) RunFishing(save, elapsedSec, bag, report);
        if (miningAllowed && IsMiningUnlocked(save)) RunMining(save, elapsedSec, bag, report);
        RunOtterVisits(save, elapsedSec, report);
        return report;
    }

    // ------------------------------------------------------------------ farm

    private class Grower
    {
        public OfflineFarmSlotSaveData Slot;
        public CropDefinition Crop;
        public double CycleSec;
        // Seconds from the start of the absence until the next harvest.
        public double NextHarvestSec;
        public bool Stopped;
    }

    private void RunFarm(SaveData save, double elapsedSec, IOfflineBag bag, OfflineReport report)
    {
        save.offlineFarmSlots ??= new List<OfflineFarmSlotSaveData>();
        int limit = Math.Min(RegistrationLimit(save), save.offlineFarmSlots.Count);

        var growers = new List<Grower>();
        for (int i = 0; i < limit; i++)
        {
            var slot = save.offlineFarmSlots[i];
            if (string.IsNullOrEmpty(slot.cropId) || !cropsById.TryGetValue(slot.cropId, out var crop)) continue;

            double cycle = farmBalance.GrowDurationSec(crop, save.farmLevel) * farmBalance.offlineSlowdown;
            if (cycle <= 0) continue;

            // A level-up since last time can leave old progress past a now
            // shorter cycle — that just means it's due right away.
            double progress = Math.Min(Math.Max(slot.progressSec, 0f), cycle);
            growers.Add(new Grower { Slot = slot, Crop = crop, CycleSec = cycle, NextHarvestSec = cycle - progress });
        }

        while (true)
        {
            Grower next = null;
            foreach (var g in growers)
            {
                if (g.Stopped || g.NextHarvestSec > elapsedSec) continue;
                if (next == null || g.NextHarvestSec < next.NextHarvestSec) next = g;
            }
            if (next == null) break;

            Harvest(next, bag, report);
            next.NextHarvestSec += next.CycleSec;
        }

        foreach (var g in growers)
        {
            g.Slot.progressSec = g.Stopped ? 0f : (float)(g.CycleSec - (g.NextHarvestSec - elapsedSec));
        }
    }

    private void Harvest(Grower grower, IOfflineBag bag, OfflineReport report)
    {
        var crop = grower.Crop;
        if (crop.seedType == SeedType.Consumable)
        {
            if (!bag.TryRemove(crop.SeedItemId, 1))
            {
                grower.Stopped = true;
                if (!report.OutOfSeeds.Contains(crop.cropId)) report.OutOfSeeds.Add(crop.cropId);
                return;
            }
            OfflineReport.AddTo(report.SeedsUsed, crop.SeedItemId, 1);
        }

        Store(crop.cropId, farmBalance.offlineYieldPerHarvest, bag, report);
    }

    // --------------------------------------------------------------- fishing

    private void RunFishing(SaveData save, double elapsedSec, IOfflineBag bag, OfflineReport report)
    {
        double perCatch = fishingBalance.OfflineSecPerCatch;
        if (perCatch <= 0) return;

        double total = Math.Max(save.offlineFishingProgressSec, 0f) + elapsedSec;
        long catches = (long)Math.Floor(total / perCatch);
        save.offlineFishingProgressSec = (float)(total - catches * perCatch);

        // 한 마리씩 굴림: 물고기인지, 어떤 물고기인지 (낚싯대 레벨로 낚을 수 있는 것 중에서)
        float fishChance = fishingBalance.FishChanceAt(save.rodLevel);
        var fishCounts = new Dictionary<string, long>();
        long fish = 0;
        for (long i = 0; i < catches; i++)
        {
            if (random.NextDouble() >= fishChance) continue;
            string id = fishingBalance.RollFish(save.rodLevel, random.NextDouble());
            fishCounts[id] = (fishCounts.TryGetValue(id, out long n) ? n : 0) + 1;
            fish++;
        }

        foreach (var pair in fishCounts)
            Store(pair.Key, ClampToInt(pair.Value), bag, report);
        Store(fishingBalance.trashItemId, ClampToInt(catches - fish), bag, report);
    }

    // ---------------------------------------------------------------- mining

    private void RunMining(SaveData save, double elapsedSec, IOfflineBag bag, OfflineReport report)
    {
        double perFind = miningBalance.OfflineSecPerFind;
        if (perFind <= 0) return;

        double total = Math.Max(save.offlineMiningProgressSec, 0f) + elapsedSec;
        long finds = (long)Math.Floor(total / perFind);
        save.offlineMiningProgressSec = (float)(total - finds * perFind);

        float diamondChance = miningBalance.DiamondChanceAt(save.pickaxeLevel);
        long diamonds = 0;
        for (long i = 0; i < finds; i++)
        {
            if (random.NextDouble() < diamondChance) diamonds++;
        }

        Store(miningBalance.diamondItemId, ClampToInt(diamonds), bag, report);
        Store(miningBalance.stoneItemId, ClampToInt(finds - diamonds), bag, report);
    }

    // ----------------------------------------------------------- otter visits

    private void RunOtterVisits(SaveData save, double elapsedSec, OfflineReport report)
    {
        double interval = otterVisitBalance.rollIntervalSec;
        var names = otterVisitBalance.placeholderOtterNames;
        if (interval <= 0 || names == null || names.Length == 0) return;

        double total = Math.Max(save.offlineOtterVisitProgressSec, 0f) + elapsedSec;
        long rolls = (long)Math.Floor(total / interval);
        save.offlineOtterVisitProgressSec = (float)(total - rolls * interval);

        for (long i = 0; i < rolls; i++)
        {
            if (random.NextDouble() < otterVisitBalance.visitChancePerRoll)
                report.OtterVisits.Add(names[random.Next(names.Length)]);
        }
    }

    // ---------------------------------------------------------------- shared

    private static void Store(string itemId, int quantity, IOfflineBag bag, OfflineReport report)
    {
        if (quantity <= 0) return;

        int stored = bag.Add(itemId, quantity);
        OfflineReport.AddTo(report.Received, itemId, stored);
        OfflineReport.AddTo(report.Lost, itemId, quantity - stored);
    }

    private static int ClampToInt(long value) => (int)Math.Min(value, int.MaxValue);
}
