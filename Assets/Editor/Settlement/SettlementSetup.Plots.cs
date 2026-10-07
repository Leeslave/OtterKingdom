using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static CollectionSetup;

/// <summary>
/// 게시판 부탁으로 짓는 광장 건물(첫 집 · 두 번째 집 · 나무 그늘 의자 · 바닷가 벤치 · 길드 접수소)을 건설 모드에서 자리를 골라 짓게 한다
/// (Docs/건설모드_전환_작업기록.md).
/// - 건설 자리 에셋(ConstructionPlotDefinition)을 만들어 꾸미기 카탈로그의 _plots에 넣음. 미리보기 그림·크기는 씬의 건물에서 읽음
/// - 광장 씬의 공사 현장에 ConstructionPlotAnchor를 붙여 함께 옮길 것(완성된 건물 · 밤 불빛 · 같은 자리의 마을회관 등)을 연결
/// - 원래 씬 자리 = 기본 칸: 공사 현장이 기본 칸 아래 가운데에서 떨어진 거리를 계산해 넣음 (옛 세이브의 건물이 제자리에 놓임)
/// - 나중에 고정 자리에 나타나는 소품(가로등 · 요정 · 공동 식탁 · 비축 상자)은 꾸미기 막음으로 처음부터 비워 둠 (그 위에 집을 짓지 않게)
/// Setup Plaza · Setup P2 Plaza · Setup P3 Plaza 끝에서도 부른다. 여러 번 실행해도 결과가 같다
/// </summary>
public static partial class SettlementSetup
{
    private const string PlotFolder = "Assets/Scriptable Obejects/Decor/Plots";
    private const string ConstructionFolder = "Assets/Scriptable Obejects/Settlement/Constructions";
    private const string PlotReservationsName = "PlotReservations";

    // 에셋, 건설, 공사 현장 · 미리보기 그림 · 함께 옮길 것 · 원래 자리를 비워 두던 막음 (PlazaRoot 아래 경로), 기본 칸(세이브 좌표), 칸 수
    // 기본 칸 = 씬의 원래 건물 발자국을 덮는 칸 (광장 격자: 칸 1, 왼쪽 아래 (-89, -17.4), 세이브 기준 칸 (77, 3))
    private static readonly (string asset, string construction, string site, string sprite, string[] followers, string[] reservations,
        Vector2Int cell, Vector2Int size)[] PlotTable =
    {
        ("Plot_House1", "con_house_1", "Settlement/Site_House1", "Props/House_Blue_01",
            new[] { "Props/House_Blue_01", "Settlement/NightLights/NightGlow_House_Blue_01" }, new string[0],
            new Vector2Int(20, 23), new Vector2Int(6, 4)),
        ("Plot_House2", "con_house_2", "Settlement/Site_House2", "Props/House_Red_01",
            new[] { "Props/House_Red_01", "Settlement/NightLights/NightGlow_House_Red_01" }, new string[0],
            new Vector2Int(18, 27), new Vector2Int(7, 4)),
        ("Plot_Chair", "con_chair", "Settlement/Site_Chair", "Props/Bench_01",
            new[] { "Props/Bench_01" }, new string[0],
            new Vector2Int(4, 25), new Vector2Int(3, 2)),
        ("Plot_RestCorner", "con_rest_corner", "Settlement/P2/Site_RestCorner", "Settlement/P2/RestCorner_Bench",
            new[] { "Settlement/P2/RestCorner_Bench" }, new string[0],
            new Vector2Int(18, 8), new Vector2Int(3, 2)),
        // 마을회관은 접수소 자리에 올라감 (같은 묶음). 회관 정리 작업 자리 · 회관 앞 화분도 따라감
        ("Plot_GuildOffice", "con_guild_office", "Settlement/P2/Site_GuildOffice", "Settlement/P2/GuildOffice",
            new[] { "Settlement/P2/GuildOffice", "Settlement/P2/Site_TownHall", "Settlement/P2/TownHall", "Settlement/P3/TaskSite_HallTidy",
                "Settlement/P3/HallDecor" },
            new[] { "Settlement/P2/DecorBlock" },
            new Vector2Int(17, 15), new Vector2Int(6, 3)),
    };

    // 나중에 고정 자리에 나타나는 소품 (PlazaRoot 아래 경로, 요정은 씬에서 찾음): 그 소품의 꾸미기 막음(없으면 발자국)을 처음부터 켜 둠
    private static readonly string[] PlotReservedProps =
    {
        "Props/Lamp_01",
        "Settlement/P3/CommunityTable",
        "Settlement/P3/SupplyBox",
    };

    [MenuItem("Tools/Settlement/Apply Building Plots")]
    public static void ApplyBuildingPlots()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);
        var plazaRoot = GameObject.Find("PlazaRoot").transform;
        if (!ApplyPlotAnchors(plazaRoot))
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SettlementSetup] 건설 자리 적용 완료: {PlotTable.Length}곳");
    }

    /// <summary>건설 자리 에셋 · 카탈로그 · 광장 씬의 묶음과 미리 비워 둘 자리를 맞춘다 (씬 저장은 부른 쪽)</summary>
    private static bool ApplyPlotAnchors(Transform plazaRoot)
    {
        var board = Object.FindAnyObjectByType<DecorBoardView>(FindObjectsInactive.Include);
        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>(DecorCatalogPath);
        if (board == null || board.Board == null || catalog == null)
        {
            Debug.LogError("[SettlementSetup] 광장 꾸미기 격자나 카탈로그가 없어 건설 자리를 만들지 못했습니다.");
            return false;
        }
        if (!AssetDatabase.IsValidFolder(PlotFolder))
            AssetDatabase.CreateFolder("Assets/Scriptable Obejects/Decor", "Plots");

        var plots = new List<ConstructionPlotDefinition>();
        foreach (var row in PlotTable)
        {
            var site = plazaRoot.Find(row.site);
            var art = plazaRoot.Find(row.sprite);
            var construction = AssetDatabase.LoadAssetAtPath<ConstructionDefinition>($"{ConstructionFolder}/{row.construction}.asset");
            if (site == null || art == null || construction == null)
            {
                Debug.LogError($"[SettlementSetup] {row.asset}: 공사 현장 '{row.site}' · 그림 '{row.sprite}' · 건설 '{row.construction}' 중 없는 것이 있습니다.");
                continue;
            }

            // 기본 칸 아래 가운데 → 공사 현장(건물 발밑)
            var cell = board.Board.SaveOrigin + row.cell;
            var area = new Rect((Vector2)board.transform.position + (Vector2)cell * board.CellSize, (Vector2)row.size * board.CellSize);
            var pivotOffset = (Vector2)site.position - new Vector2(area.center.x, area.yMin);

            var plot = LoadOrCreate<ConstructionPlotDefinition>($"{PlotFolder}/{row.asset}.asset").asset;
            var renderer = art.GetComponent<SpriteRenderer>();
            plot.SetupPlacement(row.size, false, renderer != null ? renderer.sprite : null);
            plot.SetupPlot(construction, row.cell, pivotOffset, art.lossyScale.x);
            EditorUtility.SetDirty(plot);
            plots.Add(plot);

            var anchor = site.GetComponent<ConstructionPlotAnchor>();
            if (anchor == null)
                anchor = site.gameObject.AddComponent<ConstructionPlotAnchor>();
            var so = new SerializedObject(anchor);
            so.FindProperty("_plot").objectReferenceValue = plot;
            var followers = new List<Transform>();
            foreach (var path in row.followers)
            {
                var follower = plazaRoot.Find(path);
                if (follower != null)
                    followers.Add(follower);
                else
                    Debug.LogWarning($"[SettlementSetup] {row.asset}: 함께 옮길 '{path}'이(가) 없습니다.");
            }
            SetTransforms(so.FindProperty("_followers"), followers);
            var reservations = so.FindProperty("_reservations");
            var found = new List<GameObject>();
            foreach (var path in row.reservations)
            {
                var reservation = plazaRoot.Find(path);
                if (reservation != null)
                    found.Add(reservation.gameObject);
            }
            reservations.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
                reservations.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var catalogSo = new SerializedObject(catalog);
        var list = catalogSo.FindProperty("_plots");
        foreach (var plot in plots)
            AddUnique(list, plot);
        catalogSo.ApplyModifiedPropertiesWithoutUndo();

        BuildPlotReservations(plazaRoot);
        return true;
    }

    // 나중에 고정 자리에 나타나는 소품 자리를 처음부터 비워 둠 (소품이 꺼져 있는 동안에도 켜진 막음)
    private static void BuildPlotReservations(Transform plazaRoot)
    {
        var root = plazaRoot.Find(RootName);
        var old = root.Find(PlotReservationsName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject(PlotReservationsName).transform;
        holder.SetParent(root, false);

        var targets = new List<Transform>();
        foreach (var path in PlotReservedProps)
        {
            var target = plazaRoot.Find(path);
            if (target != null)
                targets.Add(target);
            else
                Debug.LogWarning($"[SettlementSetup] 미리 비워 둘 '{path}'이(가) 없습니다.");
        }
        var fairy = Object.FindAnyObjectByType<FairyNpcView>(FindObjectsInactive.Include);
        if (fairy != null)
            targets.Add(fairy.transform);

        foreach (var target in targets)
        {
            if (!TryGetReservedRect(target, out var rect))
            {
                Debug.LogWarning($"[SettlementSetup] '{target.name}'에 막음·발자국이 없어 미리 비워 두지 못했습니다.");
                continue;
            }
            var go = new GameObject($"Reserve_{target.name}");
            go.transform.SetParent(holder, false);
            go.transform.position = rect.center;
            var area = go.AddComponent<DecorBlockArea>();
            var so = new SerializedObject(area);
            so.FindProperty("_size").vector2Value = rect.size;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // 소품의 꾸미기 막음 영역, 없으면 Blocked 발자국을 감싸는 영역 (월드)
    private static bool TryGetReservedRect(Transform target, out Rect rect)
    {
        var blocks = target.GetComponentsInChildren<DecorBlockArea>(true);
        if (blocks.Length > 0)
        {
            rect = blocks[0].WorldRect;
            for (int i = 1; i < blocks.Length; i++)
                rect = Rect.MinMaxRect(Mathf.Min(rect.xMin, blocks[i].WorldRect.xMin), Mathf.Min(rect.yMin, blocks[i].WorldRect.yMin),
                    Mathf.Max(rect.xMax, blocks[i].WorldRect.xMax), Mathf.Max(rect.yMax, blocks[i].WorldRect.yMax));
            return true;
        }

        var points = new List<Vector2>();
        bool any = false;
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var polygon in target.GetComponentsInChildren<PlazaAreaPolygon>(true))
        {
            if (polygon.Kind != PlazaAreaKind.Blocked)
                continue;
            polygon.GetWorldPoints(points);
            foreach (var p in points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
                any = true;
            }
        }
        rect = any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
        return any;
    }
}
