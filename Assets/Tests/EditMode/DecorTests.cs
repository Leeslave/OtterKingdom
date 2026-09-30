using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DecorTests
{
    private readonly List<Object> _created = new List<Object>();

    private DecorDefinition _ball;  // 1×1
    private DecorDefinition _bench; // 2×1, 회전 가능
    private DecorLayout _layout;

    [SetUp]
    public void SetUp()
    {
        _ball = CreateDecor("toy_ball", new Vector2Int(1, 1), true);
        _bench = CreateDecor("toy_bench", new Vector2Int(2, 1), true);

        // 6×4 격자: 왼쪽 4열은 기본 구역(열림), 오른쪽 2열은 잠긴 구역
        _layout = new DecorLayout(new Vector2Int(6, 4));
        _layout.AddRegion("base", new[] { new RectInt(0, 0, 4, 4) }, true);
        _layout.AddRegion("east", new[] { new RectInt(4, 0, 2, 4) }, false);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    private T Track<T>(T obj) where T : Object
    {
        _created.Add(obj);
        return obj;
    }

    private DecorDefinition CreateDecor(string itemId, Vector2Int footprint, bool canRotate)
    {
        var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
        var itemSo = new SerializedObject(item);
        itemSo.FindProperty("_itemId").stringValue = itemId;
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        var decor = Track(ScriptableObject.CreateInstance<DecorDefinition>());
        var so = new SerializedObject(decor);
        so.FindProperty("_item").objectReferenceValue = item;
        so.FindProperty("_footprint").vector2IntValue = footprint;
        so.FindProperty("_canRotate").boolValue = canRotate;
        so.ApplyModifiedPropertiesWithoutUndo();
        return decor;
    }

    #region 놓기 검사

    [Test]
    public void Check_ReportsReasonPerCell()
    {
        Assert.AreEqual(DecorPlacementResult.Ok, _layout.Check(_ball, new Vector2Int(0, 0), DecorRotation.R0));
        Assert.AreEqual(DecorPlacementResult.OutOfBounds, _layout.Check(_ball, new Vector2Int(6, 0), DecorRotation.R0));
        Assert.AreEqual(DecorPlacementResult.Locked, _layout.Check(_ball, new Vector2Int(4, 0), DecorRotation.R0));
        Assert.AreEqual(DecorPlacementResult.Locked, _layout.Check(_bench, new Vector2Int(3, 0), DecorRotation.R0), "한 칸이라도 잠겼으면 놓을 수 없습니다.");

        _layout.SetBlocked(new Vector2Int(1, 1), true);
        Assert.AreEqual(DecorPlacementResult.Unavailable, _layout.Check(_ball, new Vector2Int(1, 1), DecorRotation.R0));
    }

    [Test]
    public void CellsOutsideAnyRegion_AreUnavailable()
    {
        var layout = new DecorLayout(new Vector2Int(3, 3));
        layout.AddRegion("small", new[] { new RectInt(0, 0, 1, 1) }, true);

        Assert.AreEqual(DecorCellState.Free, layout.GetCellState(new Vector2Int(0, 0)));
        Assert.AreEqual(DecorCellState.Unavailable, layout.GetCellState(new Vector2Int(2, 2)));
    }

    [Test]
    public void Rotation_SwapsFootprint()
    {
        // 2×1 벤치를 세우면(90도) 1×2 → 맨 오른쪽 열(x=3)에도 들어감
        Assert.AreEqual(DecorPlacementResult.Locked, _layout.Check(_bench, new Vector2Int(3, 0), DecorRotation.R0));
        Assert.AreEqual(DecorPlacementResult.Ok, _layout.Check(_bench, new Vector2Int(3, 0), DecorRotation.R90));
    }

    [Test]
    public void NonRotatable_IgnoresRotation()
    {
        var sign = CreateDecor("sign", new Vector2Int(2, 1), false);

        Assert.AreEqual(DecorPlacementResult.Ok, _layout.TryPlace(sign, new Vector2Int(0, 0), DecorRotation.R90, out var placed));
        Assert.AreEqual(DecorRotation.R0, placed.Rotation);
        Assert.AreEqual(new Vector2Int(2, 1), placed.Size);
    }

    #endregion

    #region 놓기 / 옮기기 / 치우기

    [Test]
    public void Place_OccupiesAllCellsAndBlocksOverlap()
    {
        Assert.AreEqual(DecorPlacementResult.Ok, _layout.TryPlace(_bench, new Vector2Int(0, 0), DecorRotation.R0, out var bench));

        Assert.AreEqual(bench, _layout.GetAt(new Vector2Int(1, 0)));
        Assert.AreEqual(DecorCellState.Occupied, _layout.GetCellState(new Vector2Int(0, 0)));
        Assert.AreEqual(DecorPlacementResult.Occupied, _layout.TryPlace(_ball, new Vector2Int(1, 0), DecorRotation.R0, out _));
        Assert.AreEqual(1, _layout.PlacedCount(_bench));
    }

    [Test]
    public void Move_CanOverlapItsOwnCells_AndFailureKeepsPosition()
    {
        _layout.TryPlace(_bench, new Vector2Int(0, 0), DecorRotation.R0, out var bench);
        _layout.TryPlace(_ball, new Vector2Int(0, 2), DecorRotation.R0, out _);

        // 한 칸 오른쪽으로 (자기 칸과 겹쳐도 됨)
        Assert.AreEqual(DecorPlacementResult.Ok, _layout.TryMove(bench.InstanceId, new Vector2Int(1, 0), DecorRotation.R0));
        Assert.IsNull(_layout.GetAt(new Vector2Int(0, 0)), "옮긴 뒤 원래 칸은 비어야 합니다.");

        // 공이 있는 자리로는 못 감 → 그대로
        Assert.AreEqual(DecorPlacementResult.Occupied, _layout.TryMove(bench.InstanceId, new Vector2Int(0, 2), DecorRotation.R0));
        Assert.AreEqual(new Vector2Int(1, 0), bench.Origin);
    }

    [Test]
    public void Remove_FreesCellsAndNotifies()
    {
        _layout.TryPlace(_ball, new Vector2Int(2, 2), DecorRotation.R0, out var ball);
        PlacedDecor removed = null;
        _layout.OnRemoved += p => removed = p;

        Assert.IsTrue(_layout.Remove(ball.InstanceId));
        Assert.AreEqual(ball, removed);
        Assert.AreEqual(DecorCellState.Free, _layout.GetCellState(new Vector2Int(2, 2)));
        Assert.IsFalse(_layout.Remove(ball.InstanceId), "두 번 치울 수 없습니다.");
    }

    [Test]
    public void SameDecor_GetsDistinctInstanceIds()
    {
        _layout.TryPlace(_ball, new Vector2Int(0, 0), DecorRotation.R0, out var a);
        _layout.TryPlace(_ball, new Vector2Int(1, 0), DecorRotation.R0, out var b);

        Assert.AreNotEqual(a.InstanceId, b.InstanceId);
        Assert.AreEqual(2, _layout.PlacedCount(_ball));
    }

    #endregion

    #region 구역 해금

    [Test]
    public void UnlockRegion_MakesCellsPlaceable_Once()
    {
        string unlocked = null;
        _layout.OnRegionUnlocked += id => unlocked = id;

        Assert.IsTrue(_layout.UnlockRegion("east"));
        Assert.AreEqual("east", unlocked);
        Assert.AreEqual(DecorPlacementResult.Ok, _layout.Check(_ball, new Vector2Int(5, 3), DecorRotation.R0));
        Assert.IsFalse(_layout.UnlockRegion("east"), "이미 열린 구역");
        Assert.IsFalse(_layout.UnlockRegion("nowhere"), "없는 구역");
    }

    [Test]
    public void LevelRequirement_ChecksPlayerLevel()
    {
        var requirement = Track(ScriptableObject.CreateInstance<LevelUnlockRequirement>());
        var so = new SerializedObject(requirement);
        so.FindProperty("_minLevel").intValue = 5;
        so.ApplyModifiedPropertiesWithoutUndo();

        Assert.IsFalse(requirement.IsMet(new UnlockContext(4, null)));
        Assert.IsTrue(requirement.IsMet(new UnlockContext(5, null)));
        Assert.AreEqual("Lv.5 이상", requirement.Describe());
    }

    [Test]
    public void UnlockRules_AllMet_FailsWhenAnyMissing()
    {
        var lv3 = Track(ScriptableObject.CreateInstance<LevelUnlockRequirement>());
        var lv7 = Track(ScriptableObject.CreateInstance<LevelUnlockRequirement>());
        var so3 = new SerializedObject(lv3);
        so3.FindProperty("_minLevel").intValue = 3;
        so3.ApplyModifiedPropertiesWithoutUndo();
        var so7 = new SerializedObject(lv7);
        so7.FindProperty("_minLevel").intValue = 7;
        so7.ApplyModifiedPropertiesWithoutUndo();

        var context = new UnlockContext(5, null);
        Assert.IsTrue(UnlockRules.AllMet(new UnlockRequirement[] { lv3 }, context));
        Assert.IsFalse(UnlockRules.TryFulfillAll(new UnlockRequirement[] { lv3, lv7 }, context));
        Assert.IsFalse(UnlockRules.AllMet(new UnlockRequirement[] { lv3, null }, context), "빈 칸은 만족하지 않은 것으로 봅니다.");
    }

    [Test]
    public void CurrencyRequirement_WithoutManager_IsNotMet()
    {
        var requirement = Track(ScriptableObject.CreateInstance<CurrencyUnlockRequirement>());
        Assert.IsFalse(requirement.IsMet(new UnlockContext(99, null)));
    }

    #endregion

    #region 보관함 (여러 장소)

    [Test]
    public void Storage_CountsPlacementsInEveryPlace()
    {
        var farm = new DecorLayout(new Vector2Int(3, 3));
        farm.AddRegion("farm", new[] { new RectInt(0, 0, 3, 3) }, true);
        _layout.TryPlace(_ball, new Vector2Int(0, 0), DecorRotation.R0, out _);
        farm.TryPlace(_ball, new Vector2Int(1, 1), DecorRotation.R0, out _);

        var layouts = new[] { _layout, farm };
        Assert.AreEqual(1, DecorStorage.Available(3, layouts, _ball), "광장 1 + 밭 1 → 3개 중 1개 남음");
        Assert.AreEqual(0, DecorStorage.Available(1, layouts, _ball), "0 아래로 내려가지 않습니다.");
    }

    [Test]
    public void RemoveExcess_TakesBackNewestFirst()
    {
        _layout.TryPlace(_ball, new Vector2Int(0, 0), DecorRotation.R0, out var older);
        _layout.TryPlace(_ball, new Vector2Int(1, 0), DecorRotation.R0, out var newer);

        // 가방에서 1개가 빠짐 → 2개 놓였는데 1개만 가짐
        Assert.AreEqual(1, DecorStorage.RemoveExcess(1, new[] { _layout }, _ball));
        Assert.IsTrue(_layout.TryGet(older.InstanceId, out _));
        Assert.IsFalse(_layout.TryGet(newer.InstanceId, out _));
    }

    #endregion

    #region 꾸미기 모드 (들고 있는 물건)

    [Test]
    public void FindFreeNear_PrefersCenterThenNearestFree()
    {
        Assert.IsTrue(DecorEditSession.TryFindFreeNear(_layout, _ball, new Vector2Int(1, 1), DecorRotation.R0, out var origin));
        Assert.AreEqual(new Vector2Int(1, 1), origin);

        _layout.TryPlace(_ball, new Vector2Int(1, 1), DecorRotation.R0, out _);
        Assert.IsTrue(DecorEditSession.TryFindFreeNear(_layout, _ball, new Vector2Int(1, 1), DecorRotation.R0, out origin));
        Assert.AreNotEqual(new Vector2Int(1, 1), origin);
        Assert.LessOrEqual(Mathf.Max(Mathf.Abs(origin.x - 1), Mathf.Abs(origin.y - 1)), 1, "바로 옆 고리에서 찾아야 합니다.");
    }

    [Test]
    public void FindFreeNear_NoRoom_ReturnsFalse()
    {
        var tiny = new DecorLayout(new Vector2Int(1, 1));
        tiny.AddRegion("r", new[] { new RectInt(0, 0, 1, 1) }, true);
        tiny.TryPlace(_ball, Vector2Int.zero, DecorRotation.R0, out _);

        Assert.IsFalse(DecorEditSession.TryFindFreeNear(tiny, _ball, Vector2Int.zero, DecorRotation.R0, out _));
    }

    [Test]
    public void EditPlaced_ChecksIgnoringItself_AndRotates()
    {
        _layout.TryPlace(_bench, new Vector2Int(0, 0), DecorRotation.R0, out var bench);
        var session = DecorEditSession.ForPlaced(bench);

        Assert.AreEqual(DecorPlacementResult.Ok, session.Check(_layout), "제자리는 놓을 수 있어야 합니다.");
        Assert.IsTrue(session.CanRotate);
        session.Rotate();
        Assert.AreEqual(new Vector2Int(1, 2), session.Size);
        Assert.AreEqual(DecorRotation.R0, bench.Rotation, "확인하기 전에는 격자가 바뀌면 안 됩니다.");
    }

    [Test]
    public void SquareDecor_RotatesPictureButKeepsCells()
    {
        var session = DecorEditSession.ForNew(_ball, Vector2Int.zero);
        Assert.IsTrue(session.CanRotate, "정사각형도 그림은 돌아가므로 회전 버튼이 보여야 합니다.");

        session.Rotate();
        Assert.AreEqual(DecorRotation.R90, session.Rotation);
        Assert.AreEqual(Vector2Int.one, session.Size);

        _ball.SpriteFor(DecorRotation.R90, out float angle);
        Assert.AreEqual(-90f, angle, "방향별 그림이 없으면 기본 그림을 시계 방향으로 돌립니다.");
    }

    [Test]
    public void SquareDecor_RotationIsSavedAfterPlacing()
    {
        _layout.TryPlace(_ball, new Vector2Int(1, 1), DecorRotation.R270, out var ball);
        Assert.AreEqual(DecorRotation.R270, ball.Rotation);
    }

    #endregion

    #region 격자 길찾기 (밭)

    [Test]
    public void GridPath_StraightLine_HasOnlyGoal()
    {
        var corners = new List<Vector2Int>();
        Assert.IsTrue(DecorGridPath.FindPath(new Vector2Int(5, 5), _ => true, new Vector2Int(0, 2), new Vector2Int(4, 2), corners));
        CollectionAssert.AreEqual(new[] { new Vector2Int(4, 2) }, corners);
    }

    [Test]
    public void GridPath_GoesAroundWall()
    {
        // x=2 열이 y=0~3 막힘 → 위쪽(y=4)으로 돌아감
        var wall = new HashSet<Vector2Int> { new Vector2Int(2, 0), new Vector2Int(2, 1), new Vector2Int(2, 2), new Vector2Int(2, 3) };
        var corners = new List<Vector2Int>();

        Assert.IsTrue(DecorGridPath.FindPath(new Vector2Int(5, 5), c => !wall.Contains(c), new Vector2Int(0, 0), new Vector2Int(4, 0), corners));
        Assert.AreEqual(new Vector2Int(4, 0), corners[corners.Count - 1]);
        Assert.IsTrue(corners.Exists(c => c.y == 4), "벽 위로 돌아가야 합니다.");
        foreach (var c in corners)
            Assert.IsFalse(wall.Contains(c));
    }

    [Test]
    public void GridPath_NoWay_ReturnsFalse()
    {
        var corners = new List<Vector2Int>();
        Assert.IsFalse(DecorGridPath.FindPath(new Vector2Int(3, 1), c => c.x != 1, new Vector2Int(0, 0), new Vector2Int(2, 0), corners));
    }

    [Test]
    public void GridPath_PrefersFewerTurns()
    {
        // 같은 거리라면 한 번만 꺾는 길 (계단 모양 아님)
        var corners = new List<Vector2Int>();
        DecorGridPath.FindPath(new Vector2Int(6, 6), _ => true, new Vector2Int(0, 0), new Vector2Int(4, 4), corners);
        Assert.AreEqual(2, corners.Count);
    }

    #endregion

    #region 세이브

    [Test]
    public void SaveRoundTrip_RestoresRegionsAndPlacements()
    {
        var board = CreateBoard();
        var layout = DecorManager.CreateLayout(board);
        layout.UnlockRegion("east");
        layout.TryPlace(_bench, new Vector2Int(4, 1), DecorRotation.R90, out var bench);
        layout.TryPlace(_ball, new Vector2Int(0, 0), DecorRotation.R0, out _);

        var saved = new DecorBoardSaveData { boardId = "plaza" };
        DecorSaveConverter.Write(layout, saved);
        var json = JsonUtility.ToJson(saved);

        var restored = DecorManager.CreateLayout(board);
        int skipped = DecorSaveConverter.Read(JsonUtility.FromJson<DecorBoardSaveData>(json), restored, CreateCatalog());

        Assert.AreEqual(0, skipped);
        Assert.IsTrue(restored.IsRegionUnlocked("east"), "구역을 먼저 열어야 그 구역의 물건이 복원됩니다.");
        Assert.AreEqual(bench.InstanceId, restored.GetAt(new Vector2Int(4, 2)).InstanceId);
        Assert.AreEqual(DecorRotation.R90, restored.GetAt(new Vector2Int(4, 2)).Rotation);

        // 복원 뒤 새로 놓는 물건은 ID가 겹치지 않아야 함
        restored.TryPlace(_ball, new Vector2Int(2, 2), DecorRotation.R0, out var fresh);
        Assert.Greater(fresh.InstanceId, bench.InstanceId);
    }

    [Test]
    public void Read_SkipsUnknownAndInvalidEntries()
    {
        var board = CreateBoard();
        var layout = DecorManager.CreateLayout(board);
        var saved = new DecorBoardSaveData { boardId = "plaza" };
        saved.placed.Add(new PlacedDecorSaveEntry(1, "toy_removed", 0, 0, DecorRotation.R0)); // 없는 물건
        saved.placed.Add(new PlacedDecorSaveEntry(2, "toy_ball", 5, 0, DecorRotation.R0));    // 잠긴 구역
        saved.placed.Add(new PlacedDecorSaveEntry(3, "toy_ball", 1, 1, DecorRotation.R0));    // 정상

        Assert.AreEqual(2, DecorSaveConverter.Read(saved, layout, CreateCatalog()));
        Assert.AreEqual(1, layout.PlacedCount(_ball));
    }

    private DecorBoardDefinition CreateBoard()
    {
        var baseRegion = Track(ScriptableObject.CreateInstance<DecorRegionDefinition>());
        var baseSo = new SerializedObject(baseRegion);
        baseSo.FindProperty("_regionId").stringValue = "base";
        baseSo.FindProperty("_unlockedByDefault").boolValue = true;
        var baseAreas = baseSo.FindProperty("_areas");
        baseAreas.arraySize = 1;
        baseAreas.GetArrayElementAtIndex(0).rectIntValue = new RectInt(0, 0, 4, 4);
        baseSo.ApplyModifiedPropertiesWithoutUndo();

        var east = Track(ScriptableObject.CreateInstance<DecorRegionDefinition>());
        var eastSo = new SerializedObject(east);
        eastSo.FindProperty("_regionId").stringValue = "east";
        var eastAreas = eastSo.FindProperty("_areas");
        eastAreas.arraySize = 1;
        eastAreas.GetArrayElementAtIndex(0).rectIntValue = new RectInt(4, 0, 2, 4);
        eastSo.ApplyModifiedPropertiesWithoutUndo();

        var board = Track(ScriptableObject.CreateInstance<DecorBoardDefinition>());
        var so = new SerializedObject(board);
        so.FindProperty("_size").vector2IntValue = new Vector2Int(6, 4);
        var regions = so.FindProperty("_regions");
        regions.arraySize = 2;
        regions.GetArrayElementAtIndex(0).objectReferenceValue = baseRegion;
        regions.GetArrayElementAtIndex(1).objectReferenceValue = east;
        so.ApplyModifiedPropertiesWithoutUndo();
        return board;
    }

    private DecorCatalog CreateCatalog()
    {
        var catalog = Track(ScriptableObject.CreateInstance<DecorCatalog>());
        var so = new SerializedObject(catalog);
        var decors = so.FindProperty("_decors");
        decors.arraySize = 2;
        decors.GetArrayElementAtIndex(0).objectReferenceValue = _ball;
        decors.GetArrayElementAtIndex(1).objectReferenceValue = _bench;
        so.ApplyModifiedPropertiesWithoutUndo();
        return catalog;
    }

    #endregion
}
