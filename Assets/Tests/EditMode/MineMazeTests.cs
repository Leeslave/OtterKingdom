using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>광산 미로 규칙 (MineMazeModel · MazeMap): 지도 읽기 · 앞뒤좌우 가기 · 길을 막는 돌 · 1분 뒤 다시 생김(뚫은 길은 지나감) · 상자 · 나가기 · 지도 해달 · 저장</summary>
public partial class MineMazeTests
{
    private static readonly DateTime T0 = new DateTime(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc);

    // 입구에서 동쪽: 길 · 돌 · 길 · 상자 / 입구 북쪽 위로 길, 꼭대기 오른쪽 끝에 계단
    private static readonly string[] SmallMap =
    {
        "#######",
        "#....E#",
        "#.#####",
        "#S.R.T#",
        "#######",
    };

    private readonly List<Object> _created = new List<Object>();
    private MineOreDefinition _stone, _copper, _iron, _fossil;
    private MineLayerDefinition _layer;
    private MineReworkConfig _rework;

    [SetUp]
    public void SetUp()
    {
        _stone = Ore("ore_stone", MineOreRarity.Common);
        _copper = Ore("ore_copper", MineOreRarity.Common);
        _iron = Ore("ore_iron", MineOreRarity.Uncommon);
        _fossil = Ore("relic_fossil", MineOreRarity.Relic);
        _layer = Layer(4, new[] { (_copper, 1) });
        _rework = Rework(new[] { Pickaxe(1), Pickaxe(2) });
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in _created)
            Object.DestroyImmediate(o);
        _created.Clear();
    }

    #region 준비

    private T Track<T>(T o) where T : Object
    {
        _created.Add(o);
        return o;
    }

    private MineOreDefinition Ore(string id, MineOreRarity rarity)
    {
        var ore = Track(ScriptableObject.CreateInstance<MineOreDefinition>());
        var so = new SerializedObject(ore);
        so.FindProperty("_oreId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.FindProperty("_rarity").enumValueIndex = (int)rarity;
        so.ApplyModifiedPropertiesWithoutUndo();
        return ore;
    }

    private MineLayerDefinition Layer(int hp, (MineOreDefinition, int)[] veins)
    {
        var layer = Track(ScriptableObject.CreateInstance<MineLayerDefinition>());
        var so = new SerializedObject(layer);
        so.FindProperty("_rockHp").intValue = hp;
        var list = so.FindProperty("_veins");
        list.arraySize = veins.Length;
        for (int i = 0; i < veins.Length; i++)
        {
            list.GetArrayElementAtIndex(i).FindPropertyRelative("_ore").objectReferenceValue = veins[i].Item1;
            list.GetArrayElementAtIndex(i).FindPropertyRelative("_weight").intValue = veins[i].Item2;
        }
        so.FindProperty("_rareChance").floatValue = 0f;
        so.FindProperty("_relic").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        return layer;
    }

    private MinePickaxeDefinition Pickaxe(int power)
    {
        var pickaxe = Track(ScriptableObject.CreateInstance<MinePickaxeDefinition>());
        var so = new SerializedObject(pickaxe);
        so.FindProperty("_power").intValue = power;
        so.ApplyModifiedPropertiesWithoutUndo();
        return pickaxe;
    }

    private MineReworkConfig Rework(MinePickaxeDefinition[] pickaxes)
    {
        var config = Track(ScriptableObject.CreateInstance<MineReworkConfig>());
        var so = new SerializedObject(config);
        Set(so.FindProperty("_ores"), new Object[] { _stone, _copper, _iron, _fossil });
        Set(so.FindProperty("_layers"), new Object[] { _layer });
        Set(so.FindProperty("_pickaxes"), pickaxes);
        so.FindProperty("_stone").objectReferenceValue = _stone;
        so.FindProperty("_veinYield").vector2IntValue = new Vector2Int(2, 2);
        so.FindProperty("_stoneYield").vector2IntValue = new Vector2Int(3, 3);
        so.FindProperty("_shinyChance").floatValue = 0f;
        so.FindProperty("_comboWindow").floatValue = 1f;
        so.FindProperty("_comboBonusEvery").intValue = 100;
        so.FindProperty("_baseCritChance").floatValue = 0f;
        so.FindProperty("_critPerFiveCombo").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return config;
    }

    private MineMazeConfig Config(string[] map, int otterRuns = 2, int earlyCells = 99)
    {
        var config = Track(ScriptableObject.CreateInstance<MineMazeConfig>());
        var so = new SerializedObject(config);
        so.FindProperty("_rework").objectReferenceValue = _rework;
        so.FindProperty("_layer").objectReferenceValue = _layer;
        var rows = so.FindProperty("_map");
        rows.arraySize = map.Length;
        for (int i = 0; i < map.Length; i++)
            rows.GetArrayElementAtIndex(i).stringValue = map[i];
        so.FindProperty("_rockRespawnSeconds").floatValue = 60f;
        var loot = so.FindProperty("_chestLoot");
        loot.arraySize = 1;
        loot.GetArrayElementAtIndex(0).FindPropertyRelative("_ore").objectReferenceValue = _iron;
        loot.GetArrayElementAtIndex(0).FindPropertyRelative("_min").intValue = 3;
        loot.GetArrayElementAtIndex(0).FindPropertyRelative("_max").intValue = 3;
        loot.GetArrayElementAtIndex(0).FindPropertyRelative("_chance").floatValue = 1f;
        so.FindProperty("_mapOtterRuns").intValue = otterRuns;
        so.FindProperty("_mapOtterEarlyCells").intValue = earlyCells;
        so.ApplyModifiedPropertiesWithoutUndo();
        return config;
    }

    private static void Set(SerializedProperty list, Object[] items)
    {
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private MineMazeModel Model(MineMazeConfig config = null, MineMazeSave save = null)
    {
        var model = new MineMazeModel(config ?? Config(SmallMap), save ?? new MineMazeSave(), new System.Random(5), T0);
        return model;
    }

    // 앞 돌을 다 깰 때까지 친다 (콤보 안 이어지게 시간 간격을 둠)
    private static MineHit BreakAhead(MineMazeModel model, DateTime now)
    {
        MineHit hit = null;
        for (int i = 0; i < 50 && (hit == null || !hit.Broken); i++)
            hit = model.HitAhead(i * 10f, now);
        return hit;
    }

    #endregion

    #region 지도

    [Test]
    public void Map_ReadsCellsStartAndFacing()
    {
        var map = MazeMap.Parse(SmallMap);
        Assert.AreEqual(7, map.Width);
        Assert.AreEqual(5, map.Height);
        Assert.AreEqual(new Vector2Int(1, 3), map.Start);
        Assert.AreEqual(new Vector2Int(5, 1), map.Stairs);
        Assert.AreEqual(1, map.Rocks.Count);
        Assert.AreEqual(1, map.Chests.Count);
        Assert.AreEqual(MazeDir.East, map.StartFacing);
    }

    [Test]
    public void Map_RejectsOpenEdgeAndMissingStart()
    {
        Assert.Throws<ArgumentException>(() => MazeMap.Parse(new[] { "###", "#S.", "###" }));
        Assert.Throws<ArgumentException>(() => MazeMap.Parse(new[] { "###", "#.#", "###" }));
    }

    [Test]
    public void Floor1Map_EveryCellReachable_AndStairsNeedRocks()
    {
        var asset = AssetDatabase.LoadAssetAtPath<MineMazeConfig>("Assets/Scriptable Obejects/MineMaze/MineMazeConfig.asset");
        if (asset == null)
            Assert.Ignore("지하 1층 설정이 아직 없음 (Tools/Mine Maze/Build Test Scene)");
        var map = MazeMap.Parse(asset.MapRows);
        int floors = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
                floors += map.IsWall(new Vector2Int(x, y)) ? 0 : 1;
        }
        Assert.AreEqual(floors, map.Reachable().Count, "모든 길이 입구와 이어져야 함");
        Assert.GreaterOrEqual(map.Stairs.x, 0, "계단이 있어야 함");
        Assert.Greater(map.Rocks.Count, 0);
        Assert.Greater(map.Chests.Count, 0);
    }

    #endregion

    #region 가기

    [Test]
    public void Enter_StartsAtEntranceFacingOpenWay()
    {
        var model = Model();
        model.Enter(T0);
        Assert.IsTrue(model.Inside);
        Assert.AreEqual(new Vector2Int(1, 3), model.Position);
        Assert.AreEqual(MazeDir.East, model.Facing);
        Assert.IsTrue(model.OnEntrance);
        Assert.IsTrue(model.IsVisited(model.Position));
    }

    [Test]
    public void Go_LeftTurnsThenSteps_BackTurnsAround()
    {
        var model = Model();
        model.Enter(T0);
        // 동쪽을 보고 왼쪽 = 북쪽 (1,2)
        Assert.AreEqual(MazeMove.Moved, model.Go(MazeTurn.Left));
        Assert.AreEqual(new Vector2Int(1, 2), model.Position);
        Assert.AreEqual(MazeDir.North, model.Facing);
        Assert.AreEqual(MazeMove.Moved, model.Go(MazeTurn.Back));
        Assert.AreEqual(new Vector2Int(1, 3), model.Position);
        Assert.AreEqual(MazeDir.South, model.Facing);
    }

    [Test]
    public void Go_IntoWall_DoesNothing()
    {
        var model = Model();
        model.Enter(T0);
        // 동쪽을 보고 오른쪽 = 남쪽 벽
        Assert.AreEqual(MazeMove.Wall, model.Go(MazeTurn.Right));
        Assert.AreEqual(new Vector2Int(1, 3), model.Position);
        Assert.AreEqual(MazeDir.East, model.Facing);
    }

    [Test]
    public void Rock_BlocksUntilBroken()
    {
        var model = Model();
        model.Enter(T0);
        Assert.AreEqual(MazeMove.Moved, model.Go(MazeTurn.Forward)); // (2,3)
        Assert.AreEqual(MazeAhead.Rock, model.Look(MazeTurn.Forward));
        Assert.IsTrue(model.RockAhead.Blocking);
        Assert.AreEqual(MazeMove.Blocked, model.Go(MazeTurn.Forward));
        Assert.AreEqual(new Vector2Int(2, 3), model.Position);
    }

    [Test]
    public void Blocked_SideStep_TurnsToFaceIt()
    {
        var model = Model();
        model.Enter(T0);
        model.Go(MazeTurn.Forward); // (2,3) 동쪽
        model.Go(MazeTurn.Back);    // (1,3) 서쪽
        model.Go(MazeTurn.Back);    // (2,3) 동쪽
        model.Face(MazeTurn.Left);  // 북쪽 벽
        Assert.AreEqual(MazeDir.North, model.Facing);
        // 북쪽을 보고 오른쪽 = 동쪽 돌 → 그쪽을 보고 멈춤
        Assert.AreEqual(MazeMove.Blocked, model.Go(MazeTurn.Right));
        Assert.AreEqual(MazeDir.East, model.Facing);
        Assert.AreEqual(new Vector2Int(2, 3), model.Position);
    }

    #endregion

    #region 돌

    [Test]
    public void Rock_HitsToBreak_FollowReworkRule_AndGiveVeinOre()
    {
        var model = Model();
        model.Enter(T0);
        model.Go(MazeTurn.Forward);
        // 체력 4 · 힘 1 → 네 번
        for (int i = 0; i < 3; i++)
            Assert.IsFalse(model.HitAhead(i * 10f, T0).Broken);
        var hit = model.HitAhead(30f, T0);
        Assert.IsTrue(hit.Broken);
        Assert.AreEqual(2, model.Count(_copper));
        Assert.IsTrue(model.IsDiscovered(_copper));
        Assert.IsNull(model.RockAhead);
        Assert.AreEqual(MazeMove.Moved, model.Go(MazeTurn.Forward));
    }

    [Test]
    public void StrongerPickaxe_BreaksFaster()
    {
        var model = Model();
        model.CheatPickaxe(1);
        model.Enter(T0);
        model.Go(MazeTurn.Forward);
        Assert.IsFalse(model.HitAhead(0f, T0).Broken);
        Assert.IsTrue(model.HitAhead(10f, T0).Broken);
    }

    [Test]
    public void BrokenRock_RespawnsAfterAMinute_ButNoLongerBlocks()
    {
        var model = Model();
        model.Enter(T0);
        model.Go(MazeTurn.Forward);
        BreakAhead(model, T0);
        var cell = model.CellToward(MazeTurn.Forward);
        var respawned = new List<Vector2Int>();
        model.RockRespawned += c => respawned.Add(c);

        model.Tick(T0.AddSeconds(59));
        Assert.IsNull(model.RockAhead);
        Assert.AreEqual(1, model.NextRespawnIn(T0.AddSeconds(59)), 0.01);

        model.Tick(T0.AddSeconds(60));
        CollectionAssert.AreEqual(new[] { cell }, respawned);
        var rock = model.RockAhead;
        Assert.IsNotNull(rock);
        Assert.IsTrue(rock.Opened);
        Assert.IsFalse(rock.Blocking);
        Assert.AreEqual(MazeAhead.Rock, model.Look(MazeTurn.Forward));
        // 지나갈 수 있음
        Assert.AreEqual(MazeMove.Moved, model.Go(MazeTurn.Forward));
        Assert.AreEqual(cell, model.Position);
    }

    [Test]
    public void RespawnedRock_CanBeMinedAgain()
    {
        var model = Model();
        model.Enter(T0);
        model.Go(MazeTurn.Forward);
        BreakAhead(model, T0);
        model.Tick(T0.AddSeconds(60));
        Assert.IsTrue(BreakAhead(model, T0.AddSeconds(60)).Broken);
        Assert.AreEqual(4, model.Count(_copper));
    }

    [Test]
    public void Combo_ContinuesWithinWindow()
    {
        var model = Model();
        model.Enter(T0);
        model.Go(MazeTurn.Forward);
        Assert.AreEqual(1, model.HitAhead(0f, T0).Combo);
        Assert.AreEqual(2, model.HitAhead(0.5f, T0).Combo);
        Assert.AreEqual(1, model.HitAhead(5f, T0).Combo);
    }

    #endregion

    #region 상자

    [Test]
    public void Chest_OpensOnce_ThenPassable()
    {
        var model = Model();
        model.Enter(T0);
        model.Go(MazeTurn.Forward);
        BreakAhead(model, T0);
        model.Go(MazeTurn.Forward); // (3,3)
        model.Go(MazeTurn.Forward); // (4,3)
        Assert.AreEqual(MazeAhead.Chest, model.Look(MazeTurn.Forward));
        Assert.AreEqual(MazeMove.Blocked, model.Go(MazeTurn.Forward));
        var gains = model.OpenChestAhead();
        Assert.AreEqual(1, gains.Count);
        Assert.AreEqual(3, model.Count(_iron));
        Assert.AreEqual(MazeAhead.OpenedChest, model.Look(MazeTurn.Forward));
        Assert.IsNull(model.OpenChestAhead());
        Assert.AreEqual(MazeMove.Moved, model.Go(MazeTurn.Forward));
    }

    #endregion

    #region 나가기 · 지도 해달

    [Test]
    public void Exit_FromAnywhere_ReportsRun_AndNextEnterStartsAtEntrance()
    {
        var model = Model();
        model.Enter(T0);
        model.Go(MazeTurn.Forward);
        BreakAhead(model, T0);
        model.Go(MazeTurn.Forward);
        var result = model.Exit(T0.AddSeconds(125));
        Assert.IsFalse(model.Inside);
        Assert.AreEqual(125, result.Seconds, 0.01);
        Assert.AreEqual(1, result.RocksBroken);
        Assert.AreEqual(1, result.Gains.Count);
        Assert.AreEqual(_copper, result.Gains[0].Ore);
        Assert.AreEqual(2, result.Gains[0].Amount);
        Assert.AreEqual(1, model.Runs);

        model.Enter(T0.AddSeconds(200));
        Assert.AreEqual(new Vector2Int(1, 3), model.Position);
        Assert.AreEqual(MazeDir.East, model.Facing);
        // 뚫은 길은 그대로 (돌이 다시 생겼어도 지나감)
        model.Go(MazeTurn.Forward);
        Assert.AreEqual(MazeMove.Moved, model.Go(MazeTurn.Forward));
    }

    [Test]
    public void MapOtter_ArrivesAfterTwoRuns()
    {
        var model = Model();
        model.Enter(T0);
        Assert.IsFalse(model.Exit(T0).MapOtterArrived);
        Assert.IsFalse(model.MapOtter);
        model.Enter(T0);
        Assert.IsTrue(model.Exit(T0).MapOtterArrived);
        Assert.IsTrue(model.MapOtter);
        model.Enter(T0);
        Assert.IsFalse(model.Exit(T0).MapOtterArrived);
    }

    [Test]
    public void MapOtter_ArrivesOnFirstExit_WhenExploredALot()
    {
        var model = Model(Config(SmallMap, 2, 3));
        model.Enter(T0);
        model.Go(MazeTurn.Left);    // (1,2)
        model.Go(MazeTurn.Forward); // (1,1)
        Assert.IsTrue(model.Exit(T0).MapOtterArrived);
    }

    [Test]
    public void Visited_RemembersCellsEvenBeforeMapOtter()
    {
        var model = Model();
        model.Enter(T0);
        model.Go(MazeTurn.Left);
        model.Go(MazeTurn.Forward);
        Assert.IsFalse(model.MapOtter);
        Assert.IsTrue(model.IsVisited(new Vector2Int(1, 1)));
        Assert.AreEqual(3, model.VisitedCount);
    }

    #endregion

    #region 저장

    [Test]
    public void Save_RoundTrips_RocksChestsVisitedOresOtter()
    {
        var config = Config(SmallMap);
        var model = Model(config);
        model.Enter(T0);
        model.Go(MazeTurn.Forward);
        BreakAhead(model, T0);
        model.Go(MazeTurn.Forward);
        model.Go(MazeTurn.Forward);
        model.OpenChestAhead();
        model.Exit(T0.AddSeconds(10));
        model.Enter(T0.AddSeconds(10));
        model.Exit(T0.AddSeconds(20));
        var json = JsonUtility.ToJson(model.ToSave());

        var loaded = new MineMazeModel(config, JsonUtility.FromJson<MineMazeSave>(json), new System.Random(1), T0.AddSeconds(30));
        Assert.AreEqual(2, loaded.Runs);
        Assert.IsTrue(loaded.MapOtter);
        Assert.AreEqual(2, loaded.Count(_copper));
        Assert.AreEqual(3, loaded.Count(_iron));
        Assert.IsTrue(loaded.IsChestOpened(new Vector2Int(5, 3)));
        Assert.IsTrue(loaded.IsVisited(new Vector2Int(4, 3)));
        var rock = loaded.RockAt(new Vector2Int(3, 3));
        Assert.IsTrue(rock.Opened);
        Assert.IsFalse(rock.Present, "아직 1분이 안 됨");
        loaded.Tick(T0.AddSeconds(60));
        Assert.IsTrue(rock.Present);
    }

    [Test]
    public void Load_RockRespawnsIfTimePassedWhileClosed()
    {
        var config = Config(SmallMap);
        var model = Model(config);
        model.Enter(T0);
        model.Go(MazeTurn.Forward);
        BreakAhead(model, T0);
        var json = JsonUtility.ToJson(model.ToSave());
        var loaded = new MineMazeModel(config, JsonUtility.FromJson<MineMazeSave>(json), new System.Random(1), T0.AddHours(3));
        var rock = loaded.RockAt(new Vector2Int(3, 3));
        Assert.IsTrue(rock.Present);
        Assert.IsFalse(rock.Blocking);
    }

    #endregion
}
