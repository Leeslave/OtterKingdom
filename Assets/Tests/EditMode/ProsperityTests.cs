using System.Collections.Generic;
using NUnit.Framework;

public class ProsperityTests
{
    [Test]
    public void Market_RequiresPrerequisiteAndLevel_AndLearnsOnce()
    {
        var data = new ProsperitySaveData();
        var model = new ProsperityResearch(data);
        model.CompleteIntro(25, true, true);
        Assert.AreEqual(ResearchResult.NeedsPrerequisite, model.TryLearn(TechIds.Market, 25));
        model.TryLearn(TechIds.Sawmill, 25);
        int before = data.rp;
        Assert.AreEqual(ResearchResult.NeedsLevel, model.TryLearn(TechIds.Market, 20));
        Assert.AreEqual(before, data.rp);
        Assert.AreEqual(ResearchResult.Success, model.TryLearn(TechIds.Market, 25));
        Assert.AreEqual(before - 1, data.rp);
        Assert.IsTrue(model.HasTech(TechIds.Market));
        Assert.AreEqual(ResearchResult.AlreadyLearned, model.TryLearn(TechIds.Market, 25));
    }

    [Test]
    public void Entry_RequiresAllConditions_AndPaysOnlyOnce()
    {
        var data = new ProsperitySaveData();
        var model = new ProsperityResearch(data);
        Assert.IsFalse(model.CompleteIntro(19, true, true));
        Assert.IsFalse(model.CompleteIntro(20, false, true));
        Assert.IsFalse(model.CompleteIntro(20, true, false));
        Assert.AreEqual(0, data.rp);
        Assert.IsTrue(model.CompleteIntro(20, true, true));
        Assert.IsFalse(model.CompleteIntro(20, true, true));
        Assert.AreEqual(2, data.rp);
    }

    [Test]
    public void ExistingP0Save_CatchesUpThrough50_WithoutReplayingSpentRewards()
    {
        var data = new ProsperitySaveData();
        var model = new ProsperityResearch(data);
        Assert.IsFalse(model.SyncLevel(40));
        model.CompleteIntro(27, true, true);
        Assert.AreEqual(ResearchResult.Success, model.TryLearn(TechIds.Sawmill, 27));

        var restored = new ProsperityResearch(data);
        Assert.IsTrue(restored.SyncLevel(40));
        Assert.AreEqual(21, data.rp);
        Assert.IsFalse(restored.SyncLevel(40));
        Assert.IsTrue(restored.SyncLevel(50));
        Assert.IsFalse(restored.SyncLevel(51));
        Assert.AreEqual(31, data.rp);
        Assert.AreEqual(31, data.rpGrants.Count);
        CollectionAssert.AreEqual(new[] { TechIds.Sawmill }, data.learned);
    }

    [Test]
    public void IndustryResearch_CanBeLearnedAtEachRequiredLevelWithNormalRewards()
    {
        var model = new ProsperityResearch(new ProsperitySaveData());
        model.CompleteIntro(20, true, true);
        model.SyncPlanReward(true);

        foreach (var tech in ProsperityResearch.Techs)
        {
            model.SyncLevel(tech.Level);
            Assert.AreEqual(ResearchResult.Success, model.TryLearn(tech.Id, tech.Level), tech.Id);
        }

        Assert.AreEqual(24, model.Data.learned.Count);
        Assert.IsTrue(model.HasTech(TechIds.Palace));
        Assert.AreEqual(1, model.Data.rp);
    }

    [Test]
    public void RestoredResearch_DoesNotRefundSpentRpOrRepeatLearnedEvent()
    {
        var data = new ProsperitySaveData();
        var model = new ProsperityResearch(data);
        model.CompleteIntro(25, true, true);
        Assert.AreEqual(ResearchResult.Success, model.TryLearn(TechIds.Sawmill, 25));
        var restored = new ProsperityResearch(data);
        int notified = 0;
        restored.Learned += _ => notified++;
        Assert.IsFalse(restored.SyncLevel(25));
        Assert.AreEqual(6, data.rp);
        Assert.IsTrue(restored.HasTech(TechIds.Sawmill));
        Assert.AreEqual(ResearchResult.AlreadyLearned, restored.TryLearn(TechIds.Sawmill, 25));
        Assert.AreEqual(6, data.rp);
        Assert.AreEqual(0, notified);
    }

    [Test]
    public void LevelJumpsAndRepeatedNotifications_PayEachLevelExactlyOnce()
    {
        var data = new ProsperitySaveData();
        var model = new ProsperityResearch(data);
        model.CompleteIntro(20, true, true);
        Assert.IsTrue(model.SyncLevel(25));
        Assert.IsFalse(model.SyncLevel(25));
        Assert.IsFalse(model.SyncLevel(21));
        Assert.IsTrue(model.SyncLevel(27));
        Assert.AreEqual(9, data.rp);
    }

    [Test]
    public void FailedResearch_DoesNotConsumeOrNotify()
    {
        var data = new ProsperitySaveData();
        var model = new ProsperityResearch(data);
        Assert.AreEqual(ResearchResult.NeedsIntro, model.TryLearn(TechIds.Sawmill, 20));
        model.CompleteIntro(20, true, true);
        int notified = 0;
        model.Learned += _ => notified++;
        Assert.AreEqual(ResearchResult.UnknownTech, model.TryLearn("bad", 50));
        Assert.AreEqual(ResearchResult.NeedsLevel, model.TryLearn(TechIds.Cart, 20));
        Assert.AreEqual(ResearchResult.NeedsPrerequisite, model.TryLearn(TechIds.Cart, 24));
        Assert.AreEqual(2, data.rp);
        Assert.IsEmpty(data.learned);
        Assert.AreEqual(0, notified);
    }

    [Test]
    public void DoubleClick_ConsumesOnce_AndEventSeesCommittedState()
    {
        var data = new ProsperitySaveData();
        var model = new ProsperityResearch(data);
        model.CompleteIntro(20, true, true);
        int notified = 0;
        model.Learned += id =>
        {
            notified++;
            Assert.IsTrue(model.HasTech(id));
            Assert.AreEqual(1, data.rp);
        };
        Assert.AreEqual(ResearchResult.Success, model.TryLearn(TechIds.Sawmill, 20));
        Assert.AreEqual(ResearchResult.AlreadyLearned, model.TryLearn(TechIds.Sawmill, 20));
        Assert.AreEqual(1, notified);
        Assert.AreEqual(1, data.rp);
    }

    [Test]
    public void InsufficientRp_LeavesStateUnchanged()
    {
        var data = new ProsperitySaveData();
        var model = new ProsperityResearch(data);
        model.CompleteIntro(20, true, true);
        model.TryLearn(TechIds.Sawmill, 20);
        model.TryLearn(TechIds.Warehouse, 20);
        Assert.AreEqual(ResearchResult.NotEnoughRp, model.TryLearn(TechIds.Cart, 24));
        Assert.AreEqual(0, data.rp);
        Assert.IsFalse(model.HasTech(TechIds.Cart));
    }

    [Test]
    public void Repair_MissingListsAndDuplicateKeys_IsIdempotent_AndKeepsUnknownTechs()
    {
        var data = new ProsperitySaveData
        {
            rp = -1,
            learned = new List<string> { null, "I01", "I01", "future-tech" },
            rpGrants = null,
        };
        Assert.IsTrue(ProsperityResearch.Sanitize(data));
        Assert.IsFalse(ProsperityResearch.Sanitize(data));
        CollectionAssert.AreEqual(new[] { "I01", "future-tech" }, data.learned);
        Assert.AreEqual(0, data.rp);
        Assert.IsNotNull(data.rpGrants);
    }

    [Test]
    public void Version4Migration_PreservesIndustryAndXp_WithoutGrantingRpBeforeEntry()
    {
        var save = new SaveData { schemaVersion = 4, prosperity = null };
        save.profile.level = 40;
        save.profile.exp = 123;
        var industry = save.settlement.industry;
        var applied = SaveMigrations.Run(save);
        CollectionAssert.AreEqual(new[] { "prosperity research", "prosperity onboarding" }, applied);
        Assert.AreSame(industry, save.settlement.industry);
        Assert.AreEqual(123, save.profile.exp);
        Assert.AreEqual(0, save.prosperity.rp);
        Assert.IsEmpty(SaveMigrations.Run(save));
    }
}
