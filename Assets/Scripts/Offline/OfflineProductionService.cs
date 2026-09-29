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
// Both run offlineSlowdown times slower than online, with no time cap.
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
    private readonly Dictionary<string, CropDefinition> cropsById;
    private readonly FarmBalanceData farmBalance;
    private readonly FishingBalanceData fishingBalance;
    private readonly OtterVisitBalanceData otterVisitBalance;
    private readonly Random random;

    public OfflineProductionService(IEnumerable<CropDefinition> crops, FarmBalanceData farmBalance,
        FishingBalanceData fishingBalance, OtterVisitBalanceData otterVisitBalance, Random random = null)
    {
        cropsById = crops.Where(c => c != null).ToDictionary(c => c.cropId, c => c);
        this.farmBalance = farmBalance;
        this.fishingBalance = fishingBalance;
        this.otterVisitBalance = otterVisitBalance;
        this.random = random ?? new Random();
    }

    public bool IsFarmUnlocked(SaveData save) => save.farmLevel >= farmBalance.offlineUnlockLevel;

    public bool IsFishingUnlocked(SaveData save) => save.rodLevel >= fishingBalance.offlineUnlockRodLevel;

    // How many crops the farm NPC accepts: one per unlocked slot.
    public static int RegistrationLimit(SaveData save)
    {
        int count = 0;
        foreach (var plot in save.plots)
        {
            if (plot.unlocked) count += PlotSaveData.SlotCount;
        }
        return count;
    }

    public OfflineReport Run(SaveData save, double elapsedSec, IOfflineBag bag)
    {
        var report = new OfflineReport { ElapsedSec = elapsedSec };
        if (elapsedSec <= 0) return report;

        if (IsFarmUnlocked(save)) RunFarm(save, elapsedSec, bag, report);
        if (IsFishingUnlocked(save)) RunFishing(save, elapsedSec, bag, report);
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

        float fishChance = fishingBalance.FishChanceAt(save.rodLevel);
        long fish = 0;
        for (long i = 0; i < catches; i++)
        {
            if (random.NextDouble() < fishChance) fish++;
        }

        Store(fishingBalance.fishItemId, ClampToInt(fish), bag, report);
        Store(fishingBalance.trashItemId, ClampToInt(catches - fish), bag, report);
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
