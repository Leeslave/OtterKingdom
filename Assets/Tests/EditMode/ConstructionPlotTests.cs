using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 게시판 부탁 건물의 건설 자리 (첫 집 · 의자 · 길드 등을 건설 모드에서 자리를 골라 지음):
/// 카탈로그에서 건설 ID로 찾기, 꾸미기 세이브에 건설 ID로 남음(세이브 기준 칸), 돌리지 않음, 옛 세이브용 막힘 무시 놓기(물건 위는 안 됨),
/// 칸 → 건물 발밑, 영토 방향에 따른 기본 칸, 옛 세이브의 가로등, 움직이는 건물의 발자국은 해달 길만 막고 꾸미기 칸은 막지 않음.
/// </summary>
public class ConstructionPlotTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    [Test]
    public void Catalog_FindsPlotByConstruction()
    {
        var plot = Plot("con_house_1", "작은 집", new Vector2Int(20, 23), new Vector2Int(6, 4));
        var catalog = Create<DecorCatalog>();
        catalog.Setup(null, null, new[] { plot });

        Assert.AreEqual(plot, catalog.FindPlot("con_house_1"));
        Assert.IsNull(catalog.FindPlot("con_chair"));
        Assert.IsTrue(catalog.TryGetDecor("con_house_1", out var decor), "세이브 복원은 건설 ID로 찾음");
        Assert.AreEqual(plot, decor);
        Assert.AreEqual("작은 집", plot.DisplayName, "이름은 건설 이름");
        Assert.IsTrue(plot.IsBuilding, "보관함·놀이에서 빠지고 치울 수 없음");
    }

    [Test]
    public void DecorSave_WritesConstructionIdAndRestores()
    {
        var plot = Plot("con_house_1", "작은 집", new Vector2Int(20, 23), new Vector2Int(6, 4));
        var catalog = Create<DecorCatalog>();
        catalog.Setup(null, null, new[] { plot });
        var saveOrigin = new Vector2Int(77, 3);

        var layout = PlazaLayout();
        Assert.AreEqual(DecorPlacementResult.Ok, layout.TryPlace(plot, saveOrigin + plot.DefaultCell, DecorRotation.R90, out var placed));
        Assert.AreEqual(DecorRotation.R0, placed.Rotation, "건물 자리는 돌리지 않음");

        var saved = new DecorBoardSaveData { boardId = "plaza" };
        DecorSaveConverter.Write(layout, saved, saveOrigin);
        Assert.AreEqual("con_house_1", saved.placed[0].itemId);
        Assert.AreEqual(20, saved.placed[0].x);
        Assert.AreEqual(23, saved.placed[0].y);

        var loaded = PlazaLayout();
        Assert.AreEqual(0, DecorSaveConverter.Read(saved, loaded, catalog, saveOrigin));
        Assert.IsTrue(loaded.TryGet(placed.InstanceId, out var back));
        Assert.AreEqual(plot, back.Decor);
        Assert.AreEqual(new Vector2Int(97, 26), back.Origin);
    }

    [Test]
    public void IgnoreBlocked_PlacesOverWorldBlockButNotOverToys()
    {
        var plot = Plot("con_chair", "의자", new Vector2Int(4, 25), new Vector2Int(3, 2));
        var toy = Create<DecorDefinition>();
        var layout = PlazaLayout();
        var origin = new Vector2Int(10, 10);
        layout.SetBlocked(new Vector2Int(11, 10), true);

        Assert.AreEqual(DecorPlacementResult.Unavailable, layout.Check(plot, origin, DecorRotation.R0));
        Assert.AreEqual(DecorPlacementResult.Ok, layout.Check(plot, origin, DecorRotation.R0, 0, ignoreBlocked: true),
            "옛 세이브의 다 지은 건물은 씬이 비워 두던 원래 자리에 놓음");

        Assert.AreEqual(DecorPlacementResult.Ok, layout.TryPlace(toy, new Vector2Int(12, 11), DecorRotation.R0, out _));
        Assert.AreEqual(DecorPlacementResult.Occupied, layout.TryPlace(plot, origin, DecorRotation.R0, out _, ignoreBlocked: true),
            "다른 물건 위에는 놓지 않음");
    }

    [Test]
    public void PivotFor_PutsBuildingFootAtSceneSpot()
    {
        var plot = Plot("con_house_1", "작은 집", new Vector2Int(20, 23), new Vector2Int(6, 4));
        plot.SetupPlot(plot.Construction, plot.DefaultCell, new Vector2(0.325f, 0.6f), Vector2.one);

        var pivot = plot.PivotFor(new Rect(8f, 8.6f, 6f, 4f));
        Assert.AreEqual(11.325f, pivot.x, 0.0001f);
        Assert.AreEqual(9.2f, pivot.y, 0.0001f);
        var moved = plot.PivotFor(new Rect(-2f, 0.6f, 6f, 4f));
        Assert.AreEqual(1.325f, moved.x, 0.0001f, "옮긴 칸만큼 그대로 따라감");
        Assert.AreEqual(1.2f, moved.y, 0.0001f);
    }

    [Test]
    public void DefaultCell_FollowsTerritoryDirection()
    {
        var plot = Plot("con_p3_house", "새 이웃의 집", new Vector2Int(-16, 19), new Vector2Int(6, 4));
        plot.SetupPlot(plot.Construction, plot.DefaultCell, Vector2.zero, Vector2.one, "territory_home_north", new Vector2Int(11, 47));

        Assert.AreEqual(new Vector2Int(-16, 19), plot.DefaultCellFor(_ => false), "서쪽을 먼저 넓혔거나 아직 넓히기 전");
        Assert.AreEqual(new Vector2Int(11, 47), plot.DefaultCellFor(id => id == "territory_home_north"), "북쪽을 먼저 넓힘");
        var plain = Plot("con_house_1", "작은 집", new Vector2Int(20, 23), new Vector2Int(6, 4));
        Assert.AreEqual(new Vector2Int(20, 23), plain.DefaultCellFor(_ => true), "다른 칸이 없는 자리");
    }

    [Test]
    public void LampMigration_OldSaveWithHouseKeepsLamp()
    {
        var construction = Create<ConstructionDefinition>();
        var cso = new SerializedObject(construction);
        cso.FindProperty("_constructionId").stringValue = "con_lamp";
        cso.FindProperty("_unlockResultId").stringValue = "lamp_built";
        cso.ApplyModifiedPropertiesWithoutUndo();
        var lamp = Create<BoardRequestDefinition>();
        var rso = new SerializedObject(lamp);
        rso.FindProperty("_requestId").stringValue = SettlementMigration.LampRequestId;
        rso.FindProperty("_requiredDevelopment").stringValue = "house_1";
        rso.FindProperty("_construction").objectReferenceValue = construction;
        rso.ApplyModifiedPropertiesWithoutUndo();
        var config = Create<SettlementConfig>();
        var so = new SerializedObject(config);
        var requests = so.FindProperty("_requests");
        requests.arraySize = 1;
        requests.GetArrayElementAtIndex(0).objectReferenceValue = lamp;
        so.ApplyModifiedPropertiesWithoutUndo();

        var noHouse = new Settlement();
        Assert.IsFalse(SettlementMigration.MigrateLamp(config, noHouse, 3), "첫 집 전이면 직접 세움");

        var old = new Settlement();
        old.UnlockDevelopment("house_1");
        Assert.IsFalse(SettlementMigration.MigrateLamp(config, old, 4), "이미 버전 4 (새 게임에서 첫 집을 지은 뒤)");
        Assert.IsTrue(SettlementMigration.MigrateLamp(config, old, 3), "첫 집과 함께 있던 가로등은 그대로");
        Assert.IsTrue(old.IsCompleted(SettlementMigration.LampRequestId));
        Assert.IsTrue(old.HasDevelopment("lamp_built"));
        Assert.IsFalse(SettlementMigration.MigrateLamp(config, old, 3), "다시 해도 그대로");
    }

    [Test]
    public void Walkable_MovableFootprintBlocksOttersButNotDecor()
    {
        var root = new GameObject("Areas");
        _created.Add(root);
        Polygon(root, PlazaAreaKind.Walkable, new Rect(0f, 0f, 20f, 20f));
        var house = Polygon(root, PlazaAreaKind.Blocked, new Rect(8f, 8f, 4f, 4f));
        var area = root.AddComponent<PlazaWalkableArea>();
        var so = new SerializedObject(area);
        var roots = so.FindProperty("polygonRoots");
        roots.arraySize = 1;
        roots.GetArrayElementAtIndex(0).objectReferenceValue = root.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        var inside = new Vector2(10f, 10f);
        area.Rebuild();
        Assert.IsFalse(area.IsStaticWalkable(inside));
        Assert.IsFalse(area.IsDecorWalkable(inside), "고정 발자국은 꾸미기 칸도 막음");

        house.IgnoredByDecor = true;
        area.Rebuild();
        Assert.IsFalse(area.IsWalkable(inside), "해달 길은 그대로 막힘");
        Assert.IsTrue(area.IsDecorWalkable(inside), "자리를 고르는 건물의 발자국은 꾸미기 칸을 막지 않음 (자기 자리 옆으로 옮길 수 있게)");
        Assert.IsTrue(area.IsDecorWalkable(new Vector2(3f, 3f)));
        Assert.IsFalse(area.IsDecorWalkable(new Vector2(25f, 25f)), "걷기 영역 밖은 그대로 막힘");
    }

    #region 만들기

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    private ConstructionPlotDefinition Plot(string constructionId, string name, Vector2Int cell, Vector2Int size)
    {
        var construction = Create<ConstructionDefinition>();
        var so = new SerializedObject(construction);
        so.FindProperty("_constructionId").stringValue = constructionId;
        so.FindProperty("_displayName").stringValue = name;
        so.ApplyModifiedPropertiesWithoutUndo();

        var plot = Create<ConstructionPlotDefinition>();
        plot.SetupPlacement(size, false, null);
        plot.SetupPlot(construction, cell, Vector2.zero, Vector2.one);
        return plot;
    }

    private static DecorLayout PlazaLayout()
    {
        var layout = new DecorLayout(new Vector2Int(103, 90));
        layout.AddRegion("plaza", new[] { new RectInt(0, 0, 103, 90) }, true);
        return layout;
    }

    private static PlazaAreaPolygon Polygon(GameObject root, PlazaAreaKind kind, Rect rect)
    {
        var go = new GameObject(kind.ToString());
        go.transform.SetParent(root.transform, false);
        var polygon = go.AddComponent<PlazaAreaPolygon>();
        var so = new SerializedObject(polygon);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        var points = so.FindProperty("points");
        var corners = new[] { rect.min, new Vector2(rect.xMax, rect.yMin), rect.max, new Vector2(rect.xMin, rect.yMax) };
        points.arraySize = corners.Length;
        for (int i = 0; i < corners.Length; i++)
            points.GetArrayElementAtIndex(i).vector2Value = corners[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return polygon;
    }

    #endregion
}
