using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public partial class MineMazeTests
{
    [Test]
    public void Community_StartsOnlyAfterFishingAndDeepExploration()
    {
        var state = new MineCommunityState();
        Assert.IsFalse(state.Discover(2, true));
        Assert.IsFalse(state.Discover(3, false));
        Assert.AreEqual(1f, state.ProductionMultiplier);
        Assert.IsTrue(state.Discover(3, true));
        Assert.IsFalse(state.Discover(4, true));
        Assert.AreEqual(.9f, state.ProductionMultiplier, .001f);
    }

    [Test]
    public void Community_TreatmentConsumesExactlyTenOnceAndCannotSkipConcern()
    {
        var state = new MineCommunityState();
        state.Discover(3, true);
        var bag = new Inventory(1, 1);
        var herb = BagOre(MineCommunityState.HerbId);
        bag.Add(herb, 12, ItemChangeReason.Grant);
        Assert.IsFalse(state.Treat(bag, herb));
        Assert.IsTrue(state.AcceptConcern());
        Assert.IsFalse(state.AcceptConcern());
        Assert.IsTrue(state.Treat(bag, herb));
        Assert.AreEqual(2, bag.GetCount(herb));
        Assert.IsFalse(state.Treat(bag, herb));
        Assert.AreEqual(.95f, state.ProductionMultiplier, .001f);
    }

    [Test]
    public void Community_InsufficientHerbsAndWrongItemNeverConsume()
    {
        var state = new MineCommunityState();
        state.Discover(3, true);
        state.AcceptConcern();
        var bag = new Inventory(2, 2);
        var herb = BagOre(MineCommunityState.HerbId);
        var other = BagOre("other");
        bag.Add(herb, 9, ItemChangeReason.Grant);
        bag.Add(other, 15, ItemChangeReason.Grant);
        Assert.IsFalse(state.Treat(bag, herb));
        Assert.IsFalse(state.Treat(bag, other));
        Assert.AreEqual(9, bag.GetCount(herb));
        Assert.AreEqual(15, bag.GetCount(other));
        Assert.AreEqual(.9f, state.ProductionMultiplier, .001f);
    }

    [Test]
    public void Community_ResolutionAndTradeArePermanentAndOrdered()
    {
        var state = new MineCommunityState();
        Assert.IsFalse(state.RemoveSource());
        state.Discover(3, true);
        Assert.IsFalse(state.RemoveSource());
        state.concernAccepted = state.treated = true;
        Assert.IsTrue(state.RemoveSource());
        Assert.AreEqual(1f, state.ProductionMultiplier);
        Assert.IsFalse(state.RemoveSource());
        Assert.IsFalse(state.Export(false, true));
        Assert.IsFalse(state.Export(true, false));
        Assert.IsTrue(state.Export(true, true));
        Assert.IsFalse(state.Export(true, true));
        var restored = JsonUtility.FromJson<MineCommunityState>(JsonUtility.ToJson(state));
        Assert.IsFalse(restored.Discover(4, true));
        Assert.AreEqual(1f, restored.ProductionMultiplier);
        Assert.IsTrue(restored.exported);
    }

    [Test]
    public void Community_ModelSavePreservesTreatmentThroughOfflineAndReload()
    {
        var state = new MineCommunityState { started = true, concernAccepted = true, treated = true };
        var model = Model(save: new MineMazeSave { community = state });
        model.Tick(T0.AddDays(90));
        model.AdvanceMiner(90 * 86400, true);
        var restored = Model(save: JsonUtility.FromJson<MineMazeSave>(JsonUtility.ToJson(model.ToSave())));
        Assert.IsTrue(restored.Community.treated);
        Assert.AreEqual(.95f, restored.Community.ProductionMultiplier, .001f);
        Assert.IsFalse(restored.Community.sourceRemoved);
    }

    [Test]
    public void Community_SourceRequiresTreatmentAndCompletesFinalFloorOnlyOnce()
    {
        var config = Config(SmallMap);
        var so = new SerializedObject(config);
        so.FindProperty("_gasSource").boolValue = true;
        so.FindProperty("_gateHits").intValue = 2;
        so.FindProperty("_gatePickaxeTier").intValue = 0;
        so.FindProperty("_gateKingdomLevel").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        var model = Model(config);
        model.Enter(T0);
        TravelTo(model, model.Map.Stairs);
        Assert.IsFalse(model.HitGate(out _));
        model.Community.Discover(3, true);
        model.Community.concernAccepted = model.Community.treated = true;
        while (!model.FloorCleared) Assert.IsTrue(model.HitGate(out _));
        Assert.IsTrue(model.Community.sourceRemoved);
        Assert.IsTrue(model.IsFloorCleared(0));
        Assert.IsFalse(model.HitGate(out _));
        Assert.AreEqual(1f, model.Community.ProductionMultiplier);
        var restored = Model(config, model.ToSave());
        Assert.IsTrue(restored.FloorCleared);
        Assert.IsTrue(restored.Community.sourceRemoved);
    }

    [Test]
    public void Community_RealDeepFloorsHaveGuaranteedFinalRecipeChests()
    {
        var root = AssetDatabase.LoadAssetAtPath<MineMazeConfig>("Assets/Scriptable Obejects/MineMaze/MineMazeConfig.asset");
        var fourth = root.AdditionalFloors[2];
        var fifth = root.AdditionalFloors[3];
        Assert.AreEqual(4, fourth.GatePickaxeTier, "Entering floor 5 must not require the final pickaxe.");
        Assert.AreEqual(5, fifth.GatePickaxeTier);
        Assert.IsTrue(fifth.GasSource);
        foreach (var ingredient in root.Rework.Pickaxes[5].Recipe)
        {
            int guaranteed = 0;
            foreach (var loot in fifth.ChestLoot)
                if (loot.Ore == ingredient.Ore && loot.Chance >= 1f)
                    guaranteed += loot.Min * MazeMap.Parse(fifth.MapRows).Chests.Count;
            Assert.GreaterOrEqual(guaranteed, ingredient.Amount, ingredient.Ore.OreId);
        }
    }
}
