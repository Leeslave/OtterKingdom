using System.Collections.Generic;
using NUnit.Framework;

public class ProsperityOnboardingTests
{
    private static SaveData Eligible(int version, int level)
    {
        var save = new SaveData { schemaVersion = version, prosperity = null };
        save.profile.level = level;
        save.profile.exp = 123;
        save.mineMaze = new MineMazeSave();
        save.mineMaze.community.sourceRemoved = true;
        save.settlement.initialized = true;
        save.settlement.completedRequests.Add(CastleRules.RequestId);
        save.inventory.Add(new ItemStack("material_wood", 37));
        save.currencies.Add(new CurrencyBalance { currencyId = "gold", amount = 3456 });
        save.settlement.industry.warehouse.Add(new ItemCountSaveData { itemId = "plank", amount = 7 });
        return save;
    }

    private static ProsperityFlowStep Next(SaveData save, ProsperityResearch model) =>
        ProsperityFlow.Next(model, save.profile.level,
            save.settlement.unlockedDevelopments.Contains("mine_source_removed"),
            save.settlement.unlockedDevelopments.Contains(CastleRules.Built), false, false);

    [Test]
    public void EveryPreviousSchema_ReconnectsRealCompletionWithoutResettingProgress()
    {
        for (int version = 0; version < SaveData.CurrentSchemaVersion; version++)
        {
            var save = Eligible(version, 21);
            var inventory = save.inventory;
            var industry = save.settlement.industry;
            SaveDataSanitizer.Sanitize(save);
            SaveMigrations.Run(save);
            var model = new ProsperityResearch(save.prosperity);
            Assert.AreEqual(ProsperityFlowStep.Intro, Next(save, model), $"schema {version}");
            Assert.AreEqual(0, model.Data.rp, "안내 완료 전에 보상을 주지 않음");
            Assert.IsFalse(model.Data.introCompleted);
            Assert.AreSame(inventory, save.inventory);
            Assert.AreSame(industry, save.settlement.industry);
            Assert.AreEqual(37, save.inventory[0].quantity);
            Assert.AreEqual(3456, save.currencies[0].amount);
            Assert.AreEqual(7, industry.warehouse[0].amount);
            Assert.AreEqual(21, save.profile.level);
            Assert.AreEqual(123, save.profile.exp);
            Assert.IsTrue(model.CompleteIntro(21, true, true));
            Assert.AreEqual(3, model.Data.rp);
            Assert.IsFalse(model.CompleteIntro(21, true, true));
            Assert.IsEmpty(SaveMigrations.Run(save));
        }
    }

    [Test]
    public void CurrentSchema_WithMissingFlags_IsRepairedOnEveryLoad()
    {
        var save = Eligible(SaveData.CurrentSchemaVersion, 21);
        SaveDataSanitizer.Sanitize(save);
        Assert.IsEmpty(SaveMigrations.Run(save));
        Assert.AreEqual(ProsperityFlowStep.Intro, Next(save, new ProsperityResearch(save.prosperity)));
        Assert.IsFalse(ProsperityMigration.Reconcile(save), "보정은 여러 번 적용해도 같음");
    }

    [Test]
    public void MigrationAlone_AlsoHandlesSchema5ManualEntrySave()
    {
        var save = Eligible(5, 21);
        CollectionAssert.AreEqual(new[] { "prosperity onboarding" }, SaveMigrations.Run(save));
        Assert.IsNotNull(save.prosperity);
        Assert.AreEqual(ProsperityFlowStep.Intro, Next(save, new ProsperityResearch(save.prosperity)));
    }

    [Test]
    public void ExistingHighLevels_GetEntitledRpAfterIntro()
    {
        foreach (int level in new[] { 20, 21, 27, 40, 50 })
        {
            var save = Eligible(4, level);
            SaveMigrations.Run(save);
            var model = new ProsperityResearch(save.prosperity);
            Assert.AreEqual(ProsperityFlowStep.Intro, Next(save, model));
            model.CompleteIntro(level, true, true);
            Assert.AreEqual(2 + level - 20, model.Data.rp);
            Assert.IsFalse(model.SyncLevel(level));
            Assert.IsEmpty(model.Data.learned, "과거 레벨만으로 연구를 강제로 완료하지 않음");
        }
    }

    [Test]
    public void LowLevelSave_WaitsUntil20_ThenEntersWithoutAnotherMigration()
    {
        var save = Eligible(4, 19);
        SaveMigrations.Run(save);
        var model = new ProsperityResearch(save.prosperity);
        Assert.AreEqual(ProsperityFlowStep.None, Next(save, model));
        save.profile.level = 20;
        Assert.AreEqual(ProsperityFlowStep.Intro, Next(save, model));
        Assert.AreEqual(0, model.Data.rp);
    }

    [Test]
    public void HighLevelWithoutRealCompletion_DoesNotInventGasOrCastleProgress()
    {
        var save = new SaveData { schemaVersion = 5 };
        save.profile.level = 50;
        SaveMigrations.Run(save);
        Assert.IsFalse(save.settlement.unlockedDevelopments.Contains("mine_source_removed"));
        Assert.IsFalse(save.settlement.unlockedDevelopments.Contains(CastleRules.Built));
        Assert.AreEqual(ProsperityFlowStep.None, Next(save, new ProsperityResearch(save.prosperity)));
    }

    [Test]
    public void GasResolvedWithoutCastle_GuidesCastleBeforeIntro()
    {
        var model = new ProsperityResearch(new ProsperitySaveData());
        Assert.AreEqual(ProsperityFlowStep.Castle, ProsperityFlow.Next(model, 20, true, false, false, false));
        Assert.AreEqual(ProsperityFlowStep.Intro, ProsperityFlow.Next(model, 20, true, true, false, false));
    }

    [Test]
    public void ExistingDevelopmentFlags_WorkWithoutMineSave()
    {
        var save = new SaveData { schemaVersion = 5 };
        save.profile.level = 21;
        save.settlement.unlockedDevelopments.AddRange(new[] { "mine_source_removed", CastleRules.Built });
        SaveMigrations.Run(save);
        Assert.AreEqual(ProsperityFlowStep.Intro, Next(save, new ProsperityResearch(save.prosperity)));
    }

    [Test]
    public void StartedSave_KeepsResearchSpentRpAndOneTimeGrantRecords()
    {
        var save = Eligible(5, 25);
        var model = new ProsperityResearch(new ProsperitySaveData());
        model.CompleteIntro(25, true, true);
        model.TryLearn(TechIds.Sawmill, 25);
        model.TryLearn(TechIds.Warehouse, 25);
        save.prosperity = model.Data;
        var grants = new List<string>(model.Data.rpGrants);
        int balance = model.Data.rp;
        SaveMigrations.Run(save);
        var restored = new ProsperityResearch(save.prosperity);
        Assert.IsTrue(restored.Data.introCompleted);
        Assert.IsTrue(restored.HasTech(TechIds.Sawmill));
        Assert.IsTrue(restored.HasTech(TechIds.Warehouse));
        Assert.AreEqual(balance, restored.Data.rp);
        CollectionAssert.AreEqual(grants, restored.Data.rpGrants);
        Assert.IsFalse(restored.CompleteIntro(25, true, true));
        Assert.AreEqual(ProsperityFlowStep.FirstPlank, Next(save, restored));
    }

    [Test]
    public void GuideFollowsFirstResearchFirstPlankPlanAndWarehouse()
    {
        var model = new ProsperityResearch(new ProsperitySaveData());
        model.CompleteIntro(20, true, true);
        Assert.AreEqual(ProsperityFlowStep.Sawmill, ProsperityFlow.Next(model, 20, true, true, false, false));
        model.TryLearn(TechIds.Sawmill, 20);
        Assert.AreEqual(ProsperityFlowStep.FirstPlank, ProsperityFlow.Next(model, 20, true, true, false, false));
        Assert.AreEqual(ProsperityFlowStep.Plan, ProsperityFlow.Next(model, 20, true, true, true, false));
        model.SyncPlanReward(true);
        Assert.AreEqual(ProsperityFlowStep.Warehouse, ProsperityFlow.Next(model, 20, true, true, true, true));
        model.TryLearn(TechIds.Warehouse, 20);
        Assert.AreEqual(ProsperityFlowStep.None, ProsperityFlow.Next(model, 20, true, true, true, true));
        Assert.AreEqual(3, model.Data.rp);
    }

    [Test]
    public void AlreadyPerformedActions_AreNotRequestedAgain()
    {
        var model = new ProsperityResearch(new ProsperitySaveData());
        model.CompleteIntro(25, true, true);
        model.TryLearn(TechIds.Sawmill, 25);
        model.TryLearn(TechIds.Warehouse, 25);
        Assert.AreEqual(ProsperityFlowStep.None, ProsperityFlow.Next(model, 25, true, true, true, true));
    }

    [Test]
    public void IntroWaitsForPlazaLoadedStateAndAllPresentationGates()
    {
        Assert.IsFalse(ProsperityFlow.CanPresent(false, true, false, false));
        Assert.IsFalse(ProsperityFlow.CanPresent(true, false, false, false));
        Assert.IsFalse(ProsperityFlow.CanPresent(true, true, true, false));
        Assert.IsFalse(ProsperityFlow.CanPresent(true, true, false, true));
        Assert.IsTrue(ProsperityFlow.CanPresent(true, true, false, false));
    }
}
