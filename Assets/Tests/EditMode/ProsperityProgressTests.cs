using NUnit.Framework;

public class ProsperityProgressTests
{
    private static ProsperityResearch Started(int level = 20)
    {
        var model = new ProsperityResearch(new ProsperitySaveData());
        model.CompleteIntro(level, true, true);
        return model;
    }

    [Test]
    public void ProjectReward_RequiresEntryAndActualCompletion_AndPaysOnceAcrossRestore()
    {
        var model = new ProsperityResearch(new ProsperitySaveData());
        Assert.IsFalse(model.SyncPlanReward(true));
        model.CompleteIntro(20, true, true);
        Assert.IsFalse(model.SyncPlanReward(false));
        Assert.IsTrue(model.SyncPlanReward(true));
        Assert.AreEqual(5, model.Data.rp);
        var restored = new ProsperityResearch(model.Data);
        Assert.IsFalse(restored.SyncPlanReward(true));
        Assert.AreEqual(5, model.Data.rp);
    }

    [Test]
    public void ProjectReward_RecoversCompletedProjectWhenRpWasNotYetSaved()
    {
        var model = Started(27);
        Assert.AreEqual(9, model.Data.rp);
        Assert.IsTrue(model.SyncPlanReward(true));
        Assert.AreEqual(12, model.Data.rp);
        Assert.IsFalse(model.SyncPlanReward(true));
    }

    [Test]
    public void Advance_RequiresEntry_KnownObjective_AndOnlyOneActiveRequest()
    {
        var model = new ProsperityResearch(new ProsperitySaveData());
        Assert.IsFalse(model.AcceptAdvance(ProsperityResearch.HarvestAdvance, 20));
        model.CompleteIntro(20, true, true);
        Assert.IsFalse(model.AcceptAdvance("unknown", 20));
        Assert.IsTrue(model.AcceptAdvance(ProsperityResearch.HarvestAdvance, 20));
        Assert.AreEqual(26, model.Data.advance.futureLevel);
        Assert.IsFalse(model.AcceptAdvance(ProsperityResearch.SalesAdvance, 20));
        Assert.AreEqual(2, model.Data.rp, "수락만으로 RP가 생기지 않음");
    }

    [Test]
    public void Advance_CountsOnlyMatchingActivityAfterAccept_AndCapsLargeValues()
    {
        var model = Started();
        Assert.IsFalse(model.ReportAdvance(ProsperityResearch.HarvestAdvance, 100));
        model.AcceptAdvance(ProsperityResearch.HarvestAdvance, 20);
        Assert.IsFalse(model.ReportAdvance(ProsperityResearch.SalesAdvance, 1000));
        Assert.IsFalse(model.ReportAdvance(ProsperityResearch.HarvestAdvance, -10));
        Assert.IsFalse(model.ClaimAdvance(20));
        model.ReportAdvance(ProsperityResearch.HarvestAdvance, 29);
        Assert.IsFalse(model.ClaimAdvance(20));
        model.ReportAdvance(ProsperityResearch.HarvestAdvance, int.MaxValue);
        Assert.AreEqual(30, model.Data.advance.progress);
        Assert.IsFalse(model.ReportAdvance(ProsperityResearch.HarvestAdvance, 1));
        Assert.IsTrue(model.ClaimAdvance(20));
        Assert.AreEqual(3, model.Data.rp);
        Assert.IsFalse(model.ClaimAdvance(20));
    }

    [Test]
    public void TwoAdvances_ReplaceFutureGrants_AndNeverIncreaseTotalRpSupply()
    {
        foreach (bool harvestFirst in new[] { true, false })
        {
            var model = Started();
            var first = harvestFirst ? ProsperityResearch.HarvestAdvance : ProsperityResearch.SalesAdvance;
            var second = harvestFirst ? ProsperityResearch.SalesAdvance : ProsperityResearch.HarvestAdvance;
            model.AcceptAdvance(first, 20);
            model.ReportAdvance(first, int.MaxValue);
            Assert.IsTrue(model.ClaimAdvance(20));
            Assert.IsFalse(model.AcceptAdvance(first, 20));
            Assert.IsTrue(model.AcceptAdvance(second, 20));
            Assert.AreEqual(27, model.Data.advance.futureLevel);
            model.ReportAdvance(second, int.MaxValue);
            Assert.IsTrue(model.ClaimAdvance(20));
            model.SyncLevel(26);
            Assert.AreEqual(9, model.Data.rp);
            Assert.IsFalse(model.SyncLevel(27), "Lv.27분도 이미 받음");
            model.SyncPlanReward(true);
            Assert.AreEqual(12, model.Data.rp);
            Assert.AreEqual(0, model.AvailableAdvanceLevel(20));
        }
    }

    [Test]
    public void Level26_OffersOnly27_AndLevel27OffersNoAdvance()
    {
        var model = Started(26);
        Assert.IsTrue(model.AcceptAdvance(ProsperityResearch.SalesAdvance, 26));
        Assert.AreEqual(27, model.Data.advance.futureLevel);
        model.CancelAdvance();
        model.SyncLevel(27);
        Assert.IsFalse(model.AcceptAdvance(ProsperityResearch.SalesAdvance, 27));
        Assert.IsNull(model.Data.advance);
    }

    [Test]
    public void ReachingTargetWhileActive_PaysNormalGrantAndCancelsWithoutDebt()
    {
        var model = Started(25);
        model.AcceptAdvance(ProsperityResearch.HarvestAdvance, 25);
        model.ReportAdvance(ProsperityResearch.HarvestAdvance, 30);
        Assert.IsTrue(model.SyncLevel(26));
        Assert.IsNull(model.Data.advance);
        Assert.IsFalse(model.ClaimAdvance(26));
        Assert.AreEqual(8, model.Data.rp);
        Assert.IsTrue(model.AcceptAdvance(ProsperityResearch.HarvestAdvance, 26));
        Assert.AreEqual(27, model.Data.advance.futureLevel);
    }

    [Test]
    public void ClaimWithStaleLevelNotification_CannotDuplicateNormalGrant()
    {
        var model = Started(25);
        model.AcceptAdvance(ProsperityResearch.HarvestAdvance, 25);
        model.ReportAdvance(ProsperityResearch.HarvestAdvance, 30);
        Assert.IsFalse(model.ClaimAdvance(26));
        Assert.AreEqual(8, model.Data.rp);
        Assert.IsNull(model.Data.advance);
    }

    [Test]
    public void CancelAndReaccept_ResetsProgressWithoutConsumingFutureReward()
    {
        var model = Started();
        model.AcceptAdvance(ProsperityResearch.HarvestAdvance, 20);
        model.ReportAdvance(ProsperityResearch.HarvestAdvance, 29);
        Assert.IsTrue(model.CancelAdvance());
        Assert.IsFalse(model.CancelAdvance());
        Assert.AreEqual(2, model.Data.rp);
        Assert.IsTrue(model.AcceptAdvance(ProsperityResearch.HarvestAdvance, 20));
        Assert.AreEqual(0, model.Data.advance.progress);
        Assert.AreEqual(26, model.Data.advance.futureLevel);
    }

    [Test]
    public void ActiveAndClaimedAdvances_SurviveModelReload()
    {
        var model = Started();
        model.AcceptAdvance(ProsperityResearch.SalesAdvance, 20);
        model.ReportAdvance(ProsperityResearch.SalesAdvance, 700);
        var restored = new ProsperityResearch(model.Data);
        Assert.AreEqual(700, restored.Data.advance.progress);
        restored.ReportAdvance(ProsperityResearch.SalesAdvance, 300);
        Assert.IsTrue(restored.ClaimAdvance(20));
        var claimed = new ProsperityResearch(model.Data);
        Assert.IsNull(claimed.Data.advance);
        Assert.IsFalse(claimed.AcceptAdvance(ProsperityResearch.SalesAdvance, 20));
        claimed.SyncLevel(27);
        Assert.AreEqual(9, claimed.Data.rp);
    }

    [Test]
    public void AdditiveSaveFields_DefaultSafelyWithoutAlteringExistingResearchOrRp()
    {
        var data = new ProsperitySaveData { rp = 7, advanceCompleted = null };
        data.learned.Add(TechIds.Sawmill);
        var model = new ProsperityResearch(data);
        Assert.AreEqual(7, data.rp);
        Assert.IsTrue(model.HasTech(TechIds.Sawmill));
        Assert.IsNotNull(data.advanceCompleted);
        Assert.IsFalse(ProsperityResearch.Sanitize(data));
    }

    [Test]
    public void CorruptAdvance_IsRemovedOrClamped_WithoutGrantingRp()
    {
        foreach (var active in new[]
        {
            new ProsperityAdvanceSaveData { id = "unknown", futureLevel = 26 },
            new ProsperityAdvanceSaveData { id = ProsperityResearch.HarvestAdvance, futureLevel = 999 },
        })
        {
            var model = new ProsperityResearch(new ProsperitySaveData { advance = active });
            Assert.IsNull(model.Data.advance);
            Assert.AreEqual(0, model.Data.rp);
        }
        var valid = Started();
        valid.Data.advance = new ProsperityAdvanceSaveData { id = ProsperityResearch.HarvestAdvance, futureLevel = 26, progress = -1 };
        Assert.IsTrue(ProsperityResearch.Sanitize(valid.Data));
        Assert.AreEqual(0, valid.Data.advance.progress);
        valid.Data.rpGrants.Add("prosperity:level:26");
        Assert.IsTrue(ProsperityResearch.Sanitize(valid.Data));
        Assert.IsNull(valid.Data.advance);
    }
}
