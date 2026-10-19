using System;
using NUnit.Framework;
using UnityEditor;

public partial class MineMazeTests
{
    private void SetRecipe(int gold, params int[] amounts)
    {
        var so = new SerializedObject(_rework.Pickaxes[1]);
        so.FindProperty("_goldCost").intValue = gold;
        var recipe = so.FindProperty("_recipe");
        recipe.arraySize = amounts.Length;
        for (int i = 0; i < amounts.Length; i++)
        {
            recipe.GetArrayElementAtIndex(i).FindPropertyRelative("_ore").objectReferenceValue = _copper;
            recipe.GetArrayElementAtIndex(i).FindPropertyRelative("_amount").intValue = amounts[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private MineMazeSave Stock(int copper, int gold = 200)
    {
        var save = new MineMazeSave { progressionVersion = 1, gold = gold, kingdomLevel = 9 };
        save.ores.Add(new MineOreCountSave { id = _copper.OreId, count = copper });
        return save;
    }

    private void WalkToStairs(MineMazeModel model)
    {
        model.Enter(T0);
        model.Go(MazeTurn.Left);
        model.Go(MazeTurn.Forward);
        model.Go(MazeTurn.Right);
        for (int i = 0; i < 3; i++) model.Go(MazeTurn.Forward);
        Assert.IsTrue(model.OnStairs);
    }

    [Test]
    public void Craft_InsufficientMaterialsOrGoldChangesNothing()
    {
        SetRecipe(150, 3, 4);
        var model = Model(save: Stock(6));
        Assert.IsFalse(model.TryCraft(out _));
        Assert.AreEqual(6, model.Count(_copper));
        Assert.AreEqual(200, model.Gold);
        model = Model(save: Stock(7, 149));
        Assert.IsFalse(model.TryCraft(out _));
        Assert.AreEqual(7, model.Count(_copper));
    }

    [Test]
    public void Craft_AggregatesIngredientsAndPersistsWithoutRepeatCharge()
    {
        SetRecipe(150, 3, 4);
        var model = Model(save: Stock(9));
        Assert.IsTrue(model.TryCraft(out _));
        Assert.AreEqual(2, model.Count(_copper));
        Assert.AreEqual(50, model.Gold);
        model = Model(save: model.ToSave());
        Assert.AreEqual(1, model.PickaxeTier);
        Assert.IsFalse(model.TryCraft(out _));
        Assert.AreEqual(50, model.Gold);
    }

    [Test]
    public void Gate_RequiresLocationKingdomAndPickaxe()
    {
        var model = Model();
        Assert.IsFalse(model.HitGate(out _));
        WalkToStairs(model);
        Assert.IsFalse(model.HitGate(out _));
        Assert.AreEqual(0, model.GateDamage);
        var save = Stock(0);
        save.pickaxeTier = 1;
        save.kingdomLevel = 8;
        model = Model(save: save);
        WalkToStairs(model);
        Assert.IsFalse(model.HitGate(out _));
    }

    [Test]
    public void Gate_PartialProgressSurvivesExitAndCompletesOnlyOnce()
    {
        var save = Stock(0); save.pickaxeTier = 1;
        var model = Model(save: save);
        WalkToStairs(model);
        for (int i = 0; i < 7; i++) Assert.IsTrue(model.HitGate(out _));
        model.Exit(T0.AddMinutes(1));
        model = Model(save: model.ToSave());
        Assert.AreEqual(7, model.GateDamage);
        WalkToStairs(model);
        while (!model.FloorCleared) Assert.IsTrue(model.HitGate(out _));
        Assert.IsFalse(model.HitGate(out _));
        Assert.IsTrue(Model(save: model.ToSave()).FloorCleared);
    }

    [Test]
    public void Miner_OnlyUnlockedAfterClearAndDoesNotCountAsManualRunLoot()
    {
        var model = Model();
        Assert.AreEqual(0, model.AdvanceMiner(100));
        var save = Stock(0); save.floorCleared = true;
        model = Model(save: save);
        model.Enter(T0);
        Assert.AreEqual(2, model.AdvanceMiner(model.MinerInterval * 2));
        Assert.AreEqual(2, model.Count(_copper));
        Assert.AreEqual(0, model.Count(_fossil));
        Assert.IsEmpty(model.Exit(T0.AddSeconds(20)).Gains);
    }

    [Test]
    public void Miner_SplitIntervalsAndSaveMatchContinuousSettlement()
    {
        var save = Stock(0); save.floorCleared = true;
        var model = Model(save: save);
        double interval = model.MinerInterval;
        Assert.AreEqual(0, model.AdvanceMiner(interval * .4));
        model = Model(save: model.ToSave());
        Assert.AreEqual(1, model.AdvanceMiner(interval * .6));
        Assert.AreEqual(1, model.Count(_copper));
        Assert.AreEqual(0, model.AdvanceMiner(0));
        Assert.AreEqual(0, model.AdvanceMiner(double.NaN));
    }

    [Test]
    public void Miner_OfflineUsesSlowdownAndCap()
    {
        var save = Stock(0); save.floorCleared = true;
        var model = Model(save: save);
        Assert.AreEqual(1, model.AdvanceMiner(model.MinerInterval * _rework.OfflineSlowdown, true));
        Assert.LessOrEqual(model.AdvanceMiner(86400, true), _rework.OfflineMaxFinds);
    }

    [Test]
    public void LegacySaveGetsStartingGoldButSpentGoldIsNotRestored()
    {
        Assert.AreEqual(_rework.StartGold, Model().Gold);
        Assert.AreEqual(0, Model(save: Stock(0, 0)).Gold);
    }

    [Test]
    public void CompleteLoop_ManualMiningCraftGateAndProductionWithoutCheats()
    {
        SetRecipe(150, 2);
        var model = Model();
        model.Enter(T0);
        model.Go(MazeTurn.Forward);
        Assert.IsTrue(BreakAhead(model, T0).Broken);
        Assert.AreEqual(2, model.Count(_copper));
        Assert.IsTrue(model.TryCraft(out _));
        Assert.AreEqual(0, model.Count(_copper));
        model.Exit(T0.AddMinutes(1));
        WalkToStairs(model);
        while (!model.FloorCleared) model.HitGate(out _);
        model = Model(save: model.ToSave());
        Assert.IsTrue(model.FloorCleared);
        Assert.AreEqual(1, model.AdvanceMiner(model.MinerInterval));
        Assert.AreEqual(1, model.Count(_copper));
    }

    [Test]
    public void Sale_RejectsInvalidAmountsAndCreditsOnlyOwnedOre()
    {
        var so = new SerializedObject(_copper);
        so.FindProperty("_sellPrice").intValue = 6;
        so.ApplyModifiedPropertiesWithoutUndo();
        var model = Model(save: Stock(3, 0));
        Assert.IsFalse(model.Sell(_copper, -1));
        Assert.IsFalse(model.Sell(_copper, 4));
        Assert.IsTrue(model.Sell(_copper, 2));
        Assert.AreEqual(12, model.Gold);
        Assert.AreEqual(1, model.Count(_copper));
    }
}
