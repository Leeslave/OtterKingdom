using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class ProsperityQuestPlanTests
{
    [Test]
    public void P0Has25UniqueRowsAndFiveStepsPerLevel()
    {
        Assert.That(ProsperityQuestPlan.Rows.Count, Is.EqualTo(25));
        Assert.That(ProsperityQuestPlan.Rows.Select(r => r.Id).Distinct().Count(), Is.EqualTo(25));
        for (int level = 23; level <= 27; level++)
            Assert.That(ProsperityQuestPlan.Rows.Where(r => r.Level == level).Select(r => r.Step), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
    }

    [Test]
    public void AllocationPreservesEveryIntegerXpAndGoldBudget()
    {
        foreach (int total in new[] { 0, 1, 4, 5, 7, 5250, 15250, 19999, int.MaxValue })
        {
            long sum = 0;
            for (int step = 1; step <= 5; step++) sum += ProsperityQuestPlan.Share(total, step);
            Assert.That(sum, Is.EqualTo((long)total));
        }
    }

    [Test]
    public void NewAndEarlySavesBeginAt23()
    {
        foreach (int level in new[] { 1, 20, 21, 22, 23 })
            Assert.That(ProsperityQuestPlan.ChooseStart(level, new Dictionary<string, QuestRecord>()), Is.EqualTo(23));
    }

    [Test]
    public void UntouchedCurrentLevelIsTheFirstNewLevel()
    {
        for (int level = 24; level <= 27; level++)
            Assert.That(ProsperityQuestPlan.ChooseStart(level, new Dictionary<string, QuestRecord>()), Is.EqualTo(level));
    }

    [Test]
    public void PartialLegacyProgressStaysOnLegacyLevel()
    {
        var records = new Dictionary<string, QuestRecord> { ["main_lv23_harvest"] = new QuestRecord(317, false) };
        Assert.That(ProsperityQuestPlan.ChooseStart(23, records), Is.EqualTo(24));
        Assert.That(records["main_lv23_harvest"].Progress, Is.EqualTo(317));
        Assert.That(records.Count, Is.EqualTo(1));
    }

    [Test]
    public void UnclaimedAndClaimedLegacyRewardsCannotAlsoUseNewLevel()
    {
        foreach (bool claimed in new[] { false, true })
        {
            var records = new Dictionary<string, QuestRecord> { ["main_lv25_sales"] = new QuestRecord(72000, claimed, claimed ? 25 : 0) };
            Assert.That(ProsperityQuestPlan.ChooseStart(25, records), Is.EqualTo(26));
            Assert.That(records["main_lv25_sales"].Claimed, Is.EqualTo(claimed));
        }
    }

    [Test]
    public void FutureLegacyProgressIsAlsoPreserved()
    {
        var records = new Dictionary<string, QuestRecord> { ["main_lv26_daily"] = new QuestRecord(1, false) };
        Assert.That(ProsperityQuestPlan.ChooseStart(23, records), Is.EqualTo(27));
    }

    [Test]
    public void Touched27DefersWholeTransition()
    {
        var records = new Dictionary<string, QuestRecord> { ["main_lv27_daily"] = new QuestRecord(1, false) };
        Assert.That(ProsperityQuestPlan.ChooseStart(27, records), Is.Zero);
    }

    [Test]
    public void HighLevelUsersStayOnExistingChain()
    {
        foreach (int level in new[] { 28, 40, 50 })
            Assert.That(ProsperityQuestPlan.ChooseStart(level, new Dictionary<string, QuestRecord>()), Is.Zero);
    }

    [Test]
    public void EmptyAndUnrelatedRecordsDoNotDisableP0()
    {
        var records = new Dictionary<string, QuestRecord>
        {
            ["main_lv23_daily"] = new QuestRecord(0, false),
            ["main_lv22_sales"] = new QuestRecord(100000, true),
            ["unknown_future_quest"] = new QuestRecord(100, false),
        };
        Assert.That(ProsperityQuestPlan.ChooseStart(23, records), Is.EqualTo(23));
    }

    [Test]
    public void ExistingRecordsAndTransitionMarkerRoundTripWithoutLoss()
    {
        var log = new QuestLog();
        log.LoadRecord("main_lv23_harvest", 317, false);
        log.LoadRecord("unknown_future_quest", 19, true, 22);
        log.LoadRecord(ProsperityQuestPlan.TransitionId, 24, false);
        log.LoadCarry("Main|Harvest", 50);
        var saved = new List<QuestSaveEntry>();
        QuestSaveConverter.Write(log, saved);
        var restored = new QuestLog();
        QuestSaveConverter.Read(saved, restored);
        Assert.That(restored.Records[ProsperityQuestPlan.TransitionId].Progress, Is.EqualTo(24));
        Assert.That(restored.Records["main_lv23_harvest"].Progress, Is.EqualTo(317));
        Assert.That(restored.Records["unknown_future_quest"].ClaimedLevel, Is.EqualTo(22));
        Assert.That(restored.Carry["Main|Harvest"], Is.EqualTo(50));
    }

    [Test]
    public void ResearchAndOrderTargetsReferToKnownImplementedFeatures()
    {
        foreach (var row in ProsperityQuestPlan.Rows)
        {
            if (row.Target.StartsWith("tech:", StringComparison.Ordinal)) Assert.That(ProsperityResearch.Find(row.Target.Substring(5)), Is.Not.Null);
            if (row.Target.StartsWith("market:", StringComparison.Ordinal)) Assert.That(ProsperityResearch.FindMarketOrder(row.Target.Substring(7)), Is.Not.Null);
            Assert.That(row.Goal, Is.GreaterThan(0));
        }
    }
}
