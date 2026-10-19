using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public partial class MineMazeTests
{
    private MineMazeConfig ThreeFloors()
    {
        var root = Config(SmallMap);
        var second = Config(new[] { "#########", "#S.R...E#", "#.#.###.#", "#T......#", "#########" });
        var third = Config(new[] { "#######", "#S.R.E#", "#.#.#.#", "#T....#", "#######" });
        var so = new SerializedObject(root);
        Set(so.FindProperty("_additionalFloors"), new UnityEngine.Object[] { second, third });
        so.FindProperty("_testKingdomLevel").intValue = 13;
        so.ApplyModifiedPropertiesWithoutUndo();
        var secondSo = new SerializedObject(second);
        secondSo.FindProperty("_layer").objectReferenceValue = Layer(6, new[] { (_iron, 1) });
        secondSo.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    private static void TravelTo(MineMazeModel model, Vector2Int target)
    {
        var previous = new Dictionary<Vector2Int, Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(model.Position); previous[model.Position] = model.Position;
        while (queue.Count > 0 && !previous.ContainsKey(target))
        {
            var cell = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                var next = cell + MazeDirs.Step((MazeDir)d);
                if (model.Map.IsWall(next) || previous.ContainsKey(next)) continue;
                previous[next] = cell; queue.Enqueue(next);
            }
        }
        Assert.IsTrue(previous.ContainsKey(target));
        var path = new Stack<Vector2Int>();
        for (var cell = target; cell != model.Position; cell = previous[cell]) path.Push(cell);
        while (path.Count > 0)
        {
            var next = path.Pop();
            var turn = MazeTurn.Forward;
            foreach (MazeTurn candidate in Enum.GetValues(typeof(MazeTurn)))
                if (model.CellToward(candidate) == next) { turn = candidate; break; }
            var result = model.Go(turn);
            if (result == MazeMove.Blocked)
            {
                if (model.RockAhead != null) BreakAhead(model, T0);
                else model.OpenChestAhead();
                result = model.Go(MazeTurn.Forward);
            }
            Assert.AreEqual(MazeMove.Moved, result);
            Assert.AreEqual(next, model.Position);
        }
    }

    [Test]
    public void Floors_LegacyClearUnlocksSecondAndKeepsInventoryAndGold()
    {
        var config = ThreeFloors();
        var old = Stock(12, 55); old.floorCleared = true; old.pickaxeTier = 1;
        var model = Model(config, old);
        Assert.AreEqual(1, model.UnlockedFloor);
        Assert.AreEqual(13, model.KingdomLevel);
        Assert.IsFalse(model.SelectFloor(2, T0));
        Assert.IsTrue(model.SelectFloor(1, T0));
        Assert.IsFalse(model.FloorCleared);
        Assert.AreEqual(0, model.GateDamage);
        Assert.AreEqual(12, model.Count(_copper));
        Assert.AreEqual(55, model.Gold);
    }

    [Test]
    public void Floors_SaveRestoresIndependentRocksChestsMapsAndCurrentFloor()
    {
        var config = ThreeFloors();
        var old = Stock(0); old.floorCleared = true; old.pickaxeTier = 1;
        var model = Model(config, old);
        model.Enter(T0);
        TravelTo(model, model.Map.Chests[0]);
        Assert.IsTrue(model.IsChestOpened(model.Map.Chests[0]));
        model.Exit(T0);
        int firstVisited = model.VisitedCount;
        Assert.IsTrue(model.SelectFloor(1, T0));
        model.Enter(T0);
        TravelTo(model, model.Map.Stairs);
        Assert.IsTrue(model.HitGate(out _));
        int secondVisited = model.VisitedCount;
        // Serialize, rather than sharing in-memory lists with the loaded model.
        var save = JsonUtility.FromJson<MineMazeSave>(JsonUtility.ToJson(model.ToSave()));
        model = Model(config, save);
        Assert.AreEqual(1, model.CurrentFloor);
        Assert.AreEqual(1, model.GateDamage);
        Assert.AreEqual(secondVisited, model.VisitedCount);
        Assert.IsTrue(model.OnEntrance);
        Assert.IsTrue(model.SelectFloor(0, T0));
        Assert.IsTrue(model.IsChestOpened(model.Map.Chests[0]));
        Assert.AreEqual(firstVisited, model.VisitedCount);
        Assert.IsTrue(model.FloorCleared);
    }

    [Test]
    public void Floors_DescentPreservesRunAndProductionUsesOnlyClearedFloors()
    {
        var config = ThreeFloors();
        var old = Stock(0); old.pickaxeTier = 1;
        var model = Model(config, old);
        model.Enter(T0);
        TravelTo(model, model.Map.Stairs);
        while (!model.FloorCleared) Assert.IsTrue(model.HitGate(out _));
        Assert.IsTrue(model.SelectFloor(1, T0));
        Assert.IsTrue(model.Inside);
        Assert.AreEqual(0, model.Runs);
        Assert.IsFalse(model.SelectFloor(0, T0)); // No teleport from within an active run.
        int ironBefore = model.Count(_iron);
        model.AdvanceMiner(model.MinerInterval * 10);
        Assert.AreEqual(ironBefore, model.Count(_iron), "Uncleared floor 2 must not auto-produce iron.");
        Assert.GreaterOrEqual(model.Count(_copper), 10);
        TravelTo(model, model.Map.Stairs);
        while (!model.FloorCleared) Assert.IsTrue(model.HitGate(out _));
        Assert.IsTrue(model.SelectFloor(2, T0));
        TravelTo(model, model.Map.Stairs);
        Assert.IsFalse(model.HitGate(out _), "No fourth-floor or gas event in this milestone.");
        Assert.IsFalse(model.FloorCleared);
        Assert.AreEqual(2, model.UnlockedFloor);
        model.AdvanceMiner(model.MinerInterval * 100);
        Assert.Greater(model.Count(_iron), ironBefore);
        Assert.AreEqual(1, model.Exit(T0.AddMinutes(3)).Seconds / 180);
    }

    [Test]
    public void Floors_RealAssetsHaveConnectedDistinctMapsAndAffordableMaterialPaths()
    {
        var root = AssetDatabase.LoadAssetAtPath<MineMazeConfig>("Assets/Scriptable Obejects/MineMaze/MineMazeConfig.asset");
        Assert.NotNull(root);
        Assert.AreEqual(2, root.AdditionalFloors.Count);
        var model = Model(root);
        var mapStrings = new HashSet<string>();
        for (int i = 0; i < 3; i++)
        {
            var floor = model.FloorConfig(i);
            var map = MazeMap.Parse(floor.MapRows);
            int walkable = 0;
            for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
                if (!map.IsWall(new Vector2Int(x, y))) walkable++;
            Assert.AreEqual(walkable, map.Reachable().Count);
            Assert.IsTrue(mapStrings.Add(string.Join("\n", floor.MapRows)));
            Assert.GreaterOrEqual(map.Rocks.Count, 6);
            Assert.GreaterOrEqual(map.Chests.Count, 4);
            foreach (var cost in root.Rework.Pickaxes[i + 1].Recipe)
            {
                bool obtainable = false;
                for (int source = 0; source <= i; source++)
                {
                    foreach (var vein in model.FloorConfig(source).Layer.Veins) obtainable |= vein.Ore == cost.Ore && vein.Weight > 0;
                    foreach (var drop in model.FloorConfig(source).Layer.RareDrops) obtainable |= drop.Ore == cost.Ore && drop.Weight > 0;
                }
                Assert.IsTrue(obtainable, $"Floor {i + 1}: recipe ingredient {cost.Ore.OreId} is inaccessible.");
            }
        }
    }
}
