using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 영토 확장 (작업기록: Docs/영토확장_작업기록.md).
/// - 그림: Tools/MapExpansion/build_territory_art.py가 만든 전체 지도(Plaza_WorldMap.jpg) · 숲(우리 나무 그림체) · 점선 원 · 도끼 말풍선
///   과 배치 데이터(territory_layout.json: 칸 경계 · 칸별 걷기 다각형 · 숲 묶음 · 개간 자리 · 이웃집 자리)
/// - 데이터: 서쪽 3단계 · 북쪽 2단계. 단계마다 숲 개간 3번(주민 1명, 목재) + 마을회관 영토 확장 미션(납품)
/// - 광장: 전체 지도 한 장(기존 바닥은 픽셀 기준으로만 남기고 그리지 않음), 숲 묶음(개간하면 조각씩 사라지고 그루터기),
///   칸별 걷기 영역, 서쪽·북쪽 개간 자리, 카메라 범위(TerritoryMapView)
/// 한 번에 적용: Tools/Settlement/Apply Map Expansion. Setup P3 Plaza · Apply P3 Projects도 끝에서 같이 만듦
/// </summary>
public static partial class SettlementSetup
{
    private const string TerritoryArtFolder = "Assets/Art/Plaza/Territory";
    private const string WorldMapPath = "Assets/Art/Plaza/Plaza_WorldMap.jpg";
    private const string TerritoryLayoutPath = "Assets/Art/Plaza/territory_layout.json";
    private const string TerritoryFolder = DataFolder + "/Territory";
    private const string TerritoryRootName = "Territory";
    private const int TerritoryStartLevel = 6;

#pragma warning disable 0649 // JsonUtility가 채움
    [System.Serializable] private class TerritoryLayout
    {
        public float groundPixelsPerUnit;
        public PxRect map;
        public PxRect start;
        public int[] columns;
        public int[] rows;
        public TerritorySpriteInfo[] sprites;
        public TerritoryCell[] cells;
        public ForestGroup[] groups;
        public TerritorySite[] sites;
        public TerritoryHomes homes;
    }

    [System.Serializable] private class PxRect { public float x0, y0, x1, y1; }

    [System.Serializable] private class TerritorySpriteInfo { public string name; public int width, height; public float pivotX, pivotY; }

    [System.Serializable] private class TerritoryCell { public int col, row; public TerritoryPolygon[] polygons; }

    [System.Serializable] private class TerritoryPolygon { public float[] points; }

    [System.Serializable] private class ForestGroup
    {
        public string id;
        public ForestItem[] trees;
        public ForestItem[] stumps;
        public ForestShade shade;
    }

    [System.Serializable] private class ForestItem { public string sprite; public float x, y, scale, tint; public bool flip; }

    [System.Serializable] private class ForestShade { public string sprite; public float x0, y0, x1, y1; }

    [System.Serializable] private class TerritorySite { public string direction; public int tier; public float x, y; public float[] stands; public float[] look; }

    [System.Serializable] private class TerritoryHomes { public float[] west, north, potOffset, lineOffset; }
#pragma warning restore 0649

    // 단계 표: (방향, 단계, 이름, 개간 시간(초) 3개, 목재 3개, 미션 골드, 목재, 돌, 경험치)
    private static readonly (TerritoryDirection dir, int tier, string name, int[] seconds, int[] wood, int gold, int missionWood, int stone, int xp)[] TerritoryTable =
    {
        (TerritoryDirection.West, 1, "서쪽 숲", new[] { 180, 240, 300 }, new[] { 30, 35, 40 }, 500, 40, 15, 600),
        (TerritoryDirection.West, 2, "서쪽 깊은 숲", new[] { 360, 480, 600 }, new[] { 45, 50, 60 }, 1200, 70, 30, 1000),
        (TerritoryDirection.West, 3, "서쪽 끝 숲", new[] { 600, 780, 900 }, new[] { 60, 70, 80 }, 2400, 110, 50, 1500),
        (TerritoryDirection.North, 1, "북쪽 숲", new[] { 180, 240, 300 }, new[] { 30, 35, 40 }, 500, 40, 15, 600),
        (TerritoryDirection.North, 2, "북쪽 깊은 숲", new[] { 360, 480, 600 }, new[] { 45, 50, 60 }, 1200, 70, 30, 1000),
    };

    [MenuItem("Tools/Settlement/Apply Map Expansion")]
    public static void ApplyMapExpansion()
    {
        // 그림 → 데이터(P3 포함: 광장 첫 확장 대신 영토) → 광장 P3 · 영토 배치
        ApplyP3Projects();
        Debug.Log("[SettlementSetup] 영토 확장 적용 완료: 지도·숲 그림, 서쪽 3 · 북쪽 2단계 데이터, 광장 배치");
    }

    /// <summary>기존 바닥(Plaza_Ground) 범위. 바닥을 그리지 않아도(꺼 둬도) 픽셀 기준으로 씀</summary>
    internal static Bounds GroundBounds(SpriteRenderer background)
    {
        if (background.sprite == null)
            return background.bounds;
        var local = background.sprite.bounds;
        var t = background.transform;
        var min = t.TransformPoint(local.min);
        var max = t.TransformPoint(local.max);
        var bounds = new Bounds((min + max) * 0.5f, Vector3.zero);
        bounds.Encapsulate(min);
        bounds.Encapsulate(max);
        return bounds;
    }

    private static TerritoryLayout LoadTerritoryLayout()
    {
        if (!File.Exists(TerritoryLayoutPath))
        {
            Debug.LogWarning($"[SettlementSetup] 영토 배치가 없습니다: {TerritoryLayoutPath} (python Tools/MapExpansion/build_territory_art.py)");
            return null;
        }
        return JsonUtility.FromJson<TerritoryLayout>(File.ReadAllText(TerritoryLayoutPath));
    }

    #region 그림

    private static void ImportTerritoryArt(TerritoryLayout layout)
    {
        if (layout == null)
            return;
        ImportTerritorySprite(WorldMapPath, layout.groundPixelsPerUnit, new Vector2(0.5f, 0.5f), 4096, false);
        foreach (var info in layout.sprites)
        {
            string path = $"{TerritoryArtFolder}/{info.name}.png";
            // 말풍선은 망치 말풍선과 같은 크기 (PPU 150.6, 아래 가운데)
            float ppu = info.name.StartsWith("UI_Bubble") ? 150.58823f : layout.groundPixelsPerUnit;
            ImportTerritorySprite(path, ppu, new Vector2(info.pivotX, info.pivotY), 2048, true);
        }
        foreach (var group in layout.groups)
        {
            if (group.shade != null && !string.IsNullOrEmpty(group.shade.sprite))
                ImportTerritorySprite($"{TerritoryArtFolder}/{group.shade.sprite}.png", layout.groundPixelsPerUnit / 5f, new Vector2(0.5f, 0.5f), 1024, true);
        }
    }

    private static Sprite ImportTerritorySprite(string path, float pixelsPerUnit, Vector2 pivot, int maxSize, bool alpha)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SettlementSetup] 그림이 없습니다: {path} (python Tools/MapExpansion/build_territory_art.py)");
            return null;
        }
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = alpha;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.maxTextureSize = maxSize;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite TerritorySprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{TerritoryArtFolder}/{name}.png");

    #endregion

    #region 데이터

    // CreateP3Data에서 부름: 단계마다 개간 작업 3개 · 미션(단계 없는 공동사업) · 영토 정의
    private static List<TerritoryExpansionDefinition> CreateTerritoryData(SerializedObject configSo, SettlementOtterDefinition requester,
        GuestbookEntryDefinition firstEntry)
    {
        EnsureFolder(TerritoryFolder);
        EnsureFolder($"{DataFolder}/Tasks");
        EnsureFolder(ProjectFolder);
        var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodItemPath);
        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var territories = new List<TerritoryExpansionDefinition>();
        foreach (var row in TerritoryTable)
        {
            string dirId = row.dir == TerritoryDirection.West ? "west" : "north";
            string id = $"territory_{dirId}_{row.tier}";
            var territory = LoadOrCreate<TerritoryExpansionDefinition>($"{TerritoryFolder}/Territory_{dirId}_{row.tier}.asset").asset;

            var steps = new List<TerritoryClearingStep>();
            for (int k = 0; k < 3; k++)
            {
                var task = CreateP3Task($"task_{id}_{k + 1}", $"{row.name} 개간",
                    "숲의 나무를 베어 넓힐 땅을 마련해요.\n끝나면 목재를 잔뜩 얻어요.", $"{row.name} 개간 {k + 1}/3 끝!",
                    1, row.seconds[k], "", $"{id}_c{k + 1}");
                AddUnique(configSo.FindProperty("_tasks"), task);
                steps.Add(new TerritoryClearingStep(task, wood != null ? new[] { new ItemAmount(wood, row.wood[k]) } : null));
            }

            var items = new List<(ItemDefinition, int)>();
            if (wood != null)
                items.Add((wood, row.missionWood));
            if (stone != null)
                items.Add((stone, row.stone));
            var mission = CreateProject($"{id}_mission", 100 + territories.Count, $"영토 확장 · {row.name}",
                "숲을 걷어 낸 자리를 다듬으면 우리 땅이 될 거야!", requester, LoadArt("ICON_Clearing"),
                $"{row.name}이 광장이 되고 카메라가 더 멀리 가요", $"{row.name}이 열렸어요!\n광장이 넓어졌어요.",
                1, $"{id}_cleared", row.gold, items.ToArray(), System.Array.Empty<ProjectStageDefinition>(), id, row.xp,
                row.tier == 1 ? firstEntry : null);

            territory.Setup(id, row.dir, row.tier, row.name, steps, mission);
            EditorUtility.SetDirty(territory);
            territories.Add(territory);
        }
        SetList(configSo.FindProperty("_territories"), territories);
        configSo.FindProperty("_territoryStartLevel").intValue = TerritoryStartLevel;
        return territories;
    }

    #endregion

    #region 광장

    // BuildP3 끝에서 부름: Settlement 아래 Territory를 다시 만들고 광장 뷰·카메라와 잇는다
    private static void BuildTerritory(Transform root, SettlementPlazaView view, SpriteRenderer background, System.Func<Vector2, Vector3> toWorld)
    {
        var old = root.Find(TerritoryRootName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var layout = LoadTerritoryLayout();
        if (layout == null)
            return;
        var territory = new GameObject(TerritoryRootName).transform;
        territory.SetParent(root, false);
        var camera = Object.FindAnyObjectByType<PlazaCameraController>();
        float ppu = layout.groundPixelsPerUnit;
        Vector3 W(float x, float y) => toWorld(new Vector2(x, y));

        // 1) 전체 지도 한 장 (기존 바닥은 픽셀 기준으로만 남기고 그리지 않음)
        var mapSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WorldMapPath);
        var map = new GameObject("WorldMap").AddComponent<SpriteRenderer>();
        map.transform.SetParent(territory, false);
        map.sprite = mapSprite;
        map.sortingLayerID = background.sortingLayerID;
        map.sortingOrder = PlazaDepth.BackgroundOrder;
        map.sharedMaterial = background.sharedMaterial;
        var mapCenter = W((layout.map.x0 + layout.map.x1) * 0.5f, (layout.map.y0 + layout.map.y1) * 0.5f);
        map.transform.position = new Vector3(mapCenter.x, mapCenter.y, background.transform.position.z);
        background.enabled = mapSprite == null;

        // 2) 숲: 묶음마다 그늘 + 나무·덤불 (개간한 조각 / 열린 칸에서 사라짐)
        var forest = new GameObject("Forest").transform;
        forest.SetParent(territory, false);
        var stumps = new GameObject("Stumps").transform;
        stumps.SetParent(territory, false);
        var stumpParents = new Dictionary<string, Transform>();
        foreach (var group in layout.groups)
        {
            var node = new GameObject($"Forest_{group.id}").transform;
            node.SetParent(forest, false);
            AddGate(node.gameObject, ForestDevelopment(group.id), false);
            if (group.shade != null && TerritorySprite(group.shade.sprite) != null)
            {
                var shade = CreateSprite("Shade", node, TerritorySprite(group.shade.sprite),
                    W((group.shade.x0 + group.shade.x1) * 0.5f, (group.shade.y0 + group.shade.y1) * 0.5f), false);
                shade.sortingLayerID = background.sortingLayerID;
                shade.sortingOrder = PlazaDepth.BackgroundOrder + 5;
            }
            foreach (var item in group.trees)
                ForestSprite(node, item, W(item.x, item.y), background);

            // 개간한 조각에 남는 그루터기: 그 칸이 열리기 전까지만
            if (group.stumps == null || group.stumps.Length == 0 || group.id[0] == 'C')
                continue;
            string cellDev = group.id[0] == 'W'
                ? TerritoryRules.CellDevelopment(group.id[1] - '0', 0)
                : TerritoryRules.CellDevelopment(0, group.id[1] - '0');
            if (!stumpParents.TryGetValue(cellDev, out var cellNode))
            {
                cellNode = new GameObject($"Stumps_{cellDev}").transform;
                cellNode.SetParent(stumps, false);
                AddGate(cellNode.gameObject, cellDev, false);
                stumpParents[cellDev] = cellNode;
            }
            var stumpNode = new GameObject($"Stumps_{group.id}").transform;
            stumpNode.SetParent(cellNode, false);
            AddGate(stumpNode.gameObject, ForestDevelopment(group.id), true);
            foreach (var item in group.stumps)
                ForestSprite(stumpNode, item, W(item.x, item.y), background);
        }

        // 3) 칸별 걷기 영역 (그 칸이 열리면)
        var walk = new GameObject("Walkable").transform;
        walk.SetParent(territory, false);
        foreach (var cell in layout.cells)
        {
            var cellNode = new GameObject($"Walkable_{cell.col}_{cell.row}").transform;
            cellNode.SetParent(walk, false);
            for (int p = 0; p < cell.polygons.Length; p++)
            {
                var area = new GameObject($"Area{p}").AddComponent<PlazaAreaPolygon>();
                area.transform.SetParent(cellNode, false);
                var so = new SerializedObject(area);
                so.FindProperty("kind").enumValueIndex = (int)PlazaAreaKind.Walkable;
                var points = so.FindProperty("points");
                var flat = cell.polygons[p].points;
                points.arraySize = flat.Length / 2;
                for (int i = 0; i < flat.Length / 2; i++)
                    points.GetArrayElementAtIndex(i).vector2Value = area.transform.InverseTransformPoint(W(flat[i * 2], flat[i * 2 + 1]));
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AddGate(cellNode.gameObject, TerritoryRules.CellDevelopment(cell.col, cell.row), true);
            var gate = new SerializedObject(cellNode.GetComponent<DevelopmentGate>());
            gate.FindProperty("_noPop").boolValue = true;
            gate.ApplyModifiedPropertiesWithoutUndo();
        }

        // 4) 개간 자리 (방향마다 하나, 단계마다 자리를 옮김)
        var sites = new List<TerritorySiteView>();
        foreach (var direction in new[] { TerritoryDirection.West, TerritoryDirection.North })
        {
            var list = new List<TerritorySite>();
            foreach (var site in layout.sites)
            {
                if (site.direction == direction.ToString())
                    list.Add(site);
            }
            list.Sort((a, b) => a.tier.CompareTo(b.tier));
            if (list.Count > 0)
                sites.Add(BuildTerritorySite(territory, direction, list, W, ppu));
        }

        // 5) 카메라 범위
        var mapView = territory.gameObject.AddComponent<TerritoryMapView>();
        var mapSo = new SerializedObject(mapView);
        mapSo.FindProperty("_camera").objectReferenceValue = camera;
        mapSo.FindProperty("_map").rectValue = PxToRect(layout.map, W);
        mapSo.FindProperty("_start").rectValue = PxToRect(layout.start, W);
        var westEdges = mapSo.FindProperty("_westEdges");
        westEdges.arraySize = layout.columns.Length;
        for (int i = 0; i < layout.columns.Length; i++)
            westEdges.GetArrayElementAtIndex(i).floatValue = W(i == layout.columns.Length - 1 ? layout.map.x0 : layout.columns[i], 0f).x;
        var northEdges = mapSo.FindProperty("_northEdges");
        northEdges.arraySize = layout.rows.Length;
        for (int i = 0; i < layout.rows.Length; i++)
            northEdges.GetArrayElementAtIndex(i).floatValue = W(0f, i == layout.rows.Length - 1 ? layout.map.y0 : layout.rows[i]).y;
        mapSo.ApplyModifiedPropertiesWithoutUndo();

        if (camera != null)
        {
            // 확장 바닥 조각(옛 동쪽 땅)은 없음: 지도 범위는 TerritoryMapView가 정함
            var cameraSo = new SerializedObject(camera);
            cameraSo.FindProperty("extraBoundsSources").arraySize = 0;
            cameraSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var viewSo = new SerializedObject(view);
        SetList(viewSo.FindProperty("_territorySites"), sites);
        viewSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // 숲 묶음이 사라지는 발전: 서쪽 조각 = 그 개간, 북쪽 조각 = 그 개간, 모서리 칸 = 그 칸이 열림
    private static string ForestDevelopment(string groupId)
    {
        int a = groupId[1] - '0';
        int b = groupId[3] - '0';
        switch (groupId[0])
        {
            case 'W': return $"territory_west_{a}_c{b}";
            case 'N': return $"territory_north_{a}_c{b}";
            default: return TerritoryRules.CellDevelopment(a, b);
        }
    }

    private static void ForestSprite(Transform parent, ForestItem item, Vector3 position, SpriteRenderer background)
    {
        var sprite = TerritorySprite(item.sprite);
        if (sprite == null)
            return;
        var renderer = CreateSprite(item.sprite, parent, sprite, position, false);
        renderer.transform.localScale = Vector3.one * item.scale;
        renderer.flipX = item.flip;
        renderer.color = new Color(item.tint, item.tint, item.tint, 1f);
        renderer.sortingLayerID = background.sortingLayerID;
        // 움직이지 않으므로 앞뒤 정렬을 한 번만 (나무가 수백 그루라 PlazaProp을 붙이지 않음)
        renderer.sortingOrder = PlazaDepth.SortingOrderFor(position.y);
    }

    private static Rect PxToRect(PxRect px, System.Func<float, float, Vector3> w)
    {
        var a = w(px.x0, px.y1); // 왼쪽 아래
        var b = w(px.x1, px.y0); // 오른쪽 위
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }

    // 점선 원 + 도끼 말풍선 + 이름 + 남은 시간 말풍선 + 처음 안내
    private static TerritorySiteView BuildTerritorySite(Transform parent, TerritoryDirection direction, List<TerritorySite> tiers,
        System.Func<float, float, Vector3> w, float ppu)
    {
        var first = tiers[0];
        var site = new GameObject($"Site_{direction}").transform;
        site.SetParent(parent, false);
        site.position = w(first.x, first.y);

        // 바닥에 누운 점선 원: 바깥은 납작하게, 안쪽 원이 돌아감
        var ringRoot = new GameObject("RingRoot").transform;
        ringRoot.SetParent(site, false);
        ringRoot.localScale = new Vector3(0.44f, 0.3f, 1f);
        var ring = CreateSprite("Ring", ringRoot, TerritorySprite("Territory_Ring"), site.position, false);
        ring.sortingOrder = PlazaDepth.BackgroundOrder + 30;
        ring.color = new Color(1f, 1f, 1f, 0.95f);

        var markerPoint = site.position + new Vector3(0f, 1.7f, 0f);
        var marker = CreateSprite("Marker", site, TerritorySprite("UI_Bubble_Axe"), markerPoint, false);
        marker.sortingOrder = OverlayOrder - 5;
        var label = CreateWorldText("Label", site, new Vector3(0f, 0.95f, 0f), "서쪽 숲 개간 1/3");
        label.fontSize = 2.6f;
        label.rectTransform.sizeDelta = new Vector2(6f, 1f);
        label.sortingOrder = OverlayOrder - 4;

        var tap = site.gameObject.AddComponent<CircleCollider2D>();
        tap.radius = 1.6f;
        tap.offset = new Vector2(0f, 0.8f);
        var hint = CreateTapHint(site, $"hint_territory_{direction.ToString().ToLowerInvariant()}", "눌러서 숲을 개간해요!", new Vector3(0f, 3.1f, 0f));

        var bubble = CreateSprite("ProgressBubble", site, LoadPropArt("UI_Bubble_Speech"), markerPoint, false);
        bubble.transform.localScale = Vector3.one * 0.85f;
        bubble.sortingOrder = OverlayOrder - 4;
        var progress = CreateWorldText("Text", bubble.transform, new Vector3(0f, 1.05f, 0f), "서쪽 숲 개간\n0:30");
        progress.outlineWidth = 0f;
        progress.fontSize = 2.6f;
        progress.enableAutoSizing = true;
        progress.fontSizeMin = 1.8f;
        progress.fontSizeMax = 2.6f;
        progress.rectTransform.sizeDelta = new Vector2(3.4f, 1.2f);
        progress.sortingOrder = OverlayOrder - 3;

        var view = site.gameObject.AddComponent<TerritorySiteView>();
        var so = new SerializedObject(view);
        so.FindProperty("_direction").enumValueIndex = (int)direction;
        var positions = so.FindProperty("_tierPositions");
        positions.arraySize = tiers.Count;
        for (int i = 0; i < tiers.Count; i++)
            positions.GetArrayElementAtIndex(i).vector3Value = w(tiers[i].x, tiers[i].y);
        var stands = so.FindProperty("_standOffsets");
        stands.arraySize = first.stands.Length / 2;
        for (int i = 0; i < first.stands.Length / 2; i++)
            stands.GetArrayElementAtIndex(i).vector2Value = new Vector2(first.stands[i * 2] / ppu, -first.stands[i * 2 + 1] / ppu);
        so.FindProperty("_lookOffset").vector2Value = new Vector2(first.look[0] / ppu, -first.look[1] / ppu);
        so.FindProperty("_ring").objectReferenceValue = ring;
        so.FindProperty("_marker").objectReferenceValue = marker;
        so.FindProperty("_clearSprite").objectReferenceValue = TerritorySprite("UI_Bubble_Axe");
        so.FindProperty("_missionSprite").objectReferenceValue = LoadPropArt("UI_Bubble_Hammer");
        so.FindProperty("_tapArea").objectReferenceValue = tap;
        so.FindProperty("_label").objectReferenceValue = label;
        so.FindProperty("_hint").objectReferenceValue = hint;
        so.FindProperty("_progressBubble").objectReferenceValue = bubble.gameObject;
        so.FindProperty("_progressText").objectReferenceValue = progress;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    #endregion
}
