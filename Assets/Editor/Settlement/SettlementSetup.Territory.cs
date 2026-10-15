using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 영토 확장 (작업기록: Docs/영토확장_작업기록.md · Docs/구역이사_작업기록.md).
/// - 그림: Tools/MapExpansion/build_territory_art.py가 만든 전체 지도(Plaza_WorldMap.jpg) · 숲(우리 나무 그림체) · 점선 원 · 도끼 말풍선
///   과 배치 데이터(territory_layout.json: 땅별 걷기 다각형 · 숲 묶음 · 개간 자리 · 이웃집 자리 · 구역 블록 단계와 부지 칸)
/// - 데이터: 중앙 공터 둘레 4방향(구역 블록) × 3단계. 단계마다 숲 개간 N번(주민 1명, 목재) + 마을회관 영토 확장 미션(납품)
/// - 광장: 전체 지도 한 장(기존 바닥은 픽셀 기준으로만 남기고 그리지 않음), 숲 묶음(개간하면 조각씩 사라지고 그루터기, 블록 사이 숲은 늘 남음),
///   땅별 걷기 영역(공터는 MainPlaza), 방향마다 개간 자리, 카메라 범위(TerritoryMapView)
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
        public TerritorySpriteInfo[] sprites;
        public TerritoryRegion[] regions;
        public ForestGroup[] groups;
        public TerritorySite[] sites;
        public TerritoryHomes homes;
        public DistrictLayout[] districts;
        public TierInfo[] tiers;
    }

    [System.Serializable] private class PxRect { public float x0, y0, x1, y1; }

    [System.Serializable] private class TerritorySpriteInfo { public string name; public int width, height; public float pivotX, pivotY; }

    // 걷기 · 카메라 땅 하나: 공터(발전 없음) · 단계(영토 확장 ID) · 바닷가(fishing_found)
    [System.Serializable] private class TerritoryRegion
    {
        public string id, direction, development;
        public int tier;
        public PxRect rect;
        public TerritoryPolygon[] polygons;
    }

    [System.Serializable] private class TerritoryPolygon { public float[] points; }

    // 숲 묶음: development가 열리면 사라짐 (비면 늘 남는 숲), 그루터기는 그 뒤 stumpsUntil이 열리기 전까지
    [System.Serializable] private class ForestGroup
    {
        public string id, development, stumpsUntil;
        public ForestItem[] trees;
        public ForestItem[] stumps;
        public ForestShade shade;
    }

    [System.Serializable] private class ForestItem { public string sprite; public float x, y, scale, tint; public bool flip; }

    [System.Serializable] private class ForestShade { public string sprite; public float x0, y0, x1, y1; }

    [System.Serializable] private class TerritorySite { public string direction; public int tier; public float x, y; public float[] stands; public float[] look; }

    [System.Serializable] private class TerritoryHomes { public HomeSpot[] list; public float[] potOffset, lineOffset; }

    [System.Serializable] private class HomeSpot { public string direction, development; public float x, y; }

    // 구역 블록: 단계(칸 영역 · 열리는 발전) · 부지 칸 (꾸미기 격자 세이브 좌표)
    [System.Serializable] private class DistrictLayout
    {
        public string block, direction;
        public CellArea[] stages;
        public CellArea[] slots;
    }

    [System.Serializable] private class CellArea { public string development; public int x, y, w, h, stage; }

    [System.Serializable] private class TierInfo { public string direction, key; public int tier, clearings; }
#pragma warning restore 0649

    // 단계 표: (방향, 단계, 이름, 개간 시간(초) — 개수 = 개간 수, 목재(개간마다), 미션 골드, 목재, 돌, 경험치). 수치는 임시
    private static readonly (TerritoryDirection dir, int tier, string name, int[] seconds, int[] wood, int gold, int missionWood, int stone, int xp)[] TerritoryTable =
    {
        (TerritoryDirection.NorthEast, 1, "북동쪽 숲", new[] { 180, 240, 300 }, new[] { 30, 35, 40 }, 500, 40, 15, 600),
        (TerritoryDirection.NorthEast, 2, "북동쪽 깊은 숲", new[] { 360, 480 }, new[] { 45, 55 }, 1200, 70, 30, 1000),
        (TerritoryDirection.NorthEast, 3, "북동쪽 언덕 숲", new[] { 600, 780 }, new[] { 60, 75 }, 2400, 110, 50, 1500),
        (TerritoryDirection.South, 1, "남쪽 숲", new[] { 180, 240, 300 }, new[] { 30, 35, 40 }, 500, 40, 15, 600),
        (TerritoryDirection.South, 2, "남쪽 깊은 숲", new[] { 360, 480 }, new[] { 45, 55 }, 1200, 70, 30, 1000),
        (TerritoryDirection.South, 3, "바닷가 숲", new[] { 600, 780 }, new[] { 60, 75 }, 2400, 110, 50, 1500),
        (TerritoryDirection.NorthWest, 1, "북서쪽 숲", new[] { 180, 240, 300 }, new[] { 30, 35, 40 }, 500, 40, 15, 600),
        (TerritoryDirection.NorthWest, 2, "북서쪽 깊은 숲", new[] { 360, 480 }, new[] { 45, 55 }, 1200, 70, 30, 1000),
        (TerritoryDirection.NorthWest, 3, "북서쪽 끝 숲", new[] { 600, 780 }, new[] { 60, 75 }, 2400, 110, 50, 1500),
        (TerritoryDirection.West, 1, "서쪽 숲", new[] { 180, 240, 300 }, new[] { 30, 35, 40 }, 500, 40, 15, 600),
        (TerritoryDirection.West, 2, "서쪽 깊은 숲", new[] { 360, 480 }, new[] { 45, 55 }, 1200, 70, 30, 1000),
        (TerritoryDirection.West, 3, "서쪽 끝 숲", new[] { 600, 780 }, new[] { 60, 75 }, 2400, 110, 50, 1500),
    };

    /// <summary>영토 확장 단계 ID (= 그 땅이 열린 발전, 구역 블록 단계의 발전)</summary>
    internal static string TerritoryId(TerritoryDirection direction, int tier) => $"territory_{TerritoryRules.DirectionKey(direction)}_{tier}";

    [MenuItem("Tools/Settlement/Apply Map Expansion")]
    public static void ApplyMapExpansion()
    {
        // 그림 → 데이터(P3 포함: 광장 첫 확장 대신 영토) → 광장 P3 · 영토 배치
        ApplyP3Projects();
        Debug.Log("[SettlementSetup] 영토 확장 적용 완료: 숲 그늘 그림, 4방향 × 3단계 데이터, 광장 배치");
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

    // CreateP3Data에서 부름: 단계마다 개간 작업 N개 · 미션(단계 없는 공동사업) · 영토 정의. 옛 구조(남동쪽 광장에서 서쪽 · 북쪽)의 데이터는 지움
    private static List<TerritoryExpansionDefinition> CreateTerritoryData(SerializedObject configSo, SettlementOtterDefinition requester,
        GuestbookEntryDefinition firstEntry)
    {
        EnsureFolder(TerritoryFolder);
        EnsureFolder($"{DataFolder}/Tasks");
        EnsureFolder(ProjectFolder);
        RemoveOldTerritoryData(configSo);
        var layout = LoadTerritoryLayout();
        var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(WoodItemPath);
        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var territories = new List<TerritoryExpansionDefinition>();
        foreach (var row in TerritoryTable)
        {
            string key = TerritoryRules.DirectionKey(row.dir);
            string id = TerritoryId(row.dir, row.tier);
            int clearings = row.seconds.Length;
            var tierInfo = layout != null && layout.tiers != null
                ? System.Array.Find(layout.tiers, t => t.key == key && t.tier == row.tier) : null;
            if (tierInfo != null && tierInfo.clearings != clearings)
                Debug.LogWarning($"[SettlementSetup] {id}: 개간 {clearings}번인데 지도 숲 조각은 {tierInfo.clearings}개입니다 (build_territory_art.py DIRECTIONS).");
            var territory = LoadOrCreate<TerritoryExpansionDefinition>($"{TerritoryFolder}/Territory_{key}_{row.tier}.asset").asset;

            var steps = new List<TerritoryClearingStep>();
            for (int k = 0; k < clearings; k++)
            {
                var task = CreateP3Task($"task_{id}_{k + 1}", $"{row.name} 개간",
                    "숲의 나무를 베어 넓힐 땅을 마련해요.\n끝나면 목재를 잔뜩 얻어요.", $"{row.name} 개간 {k + 1}/{clearings} 끝!",
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
                $"{row.name}이 우리 땅이 되고 카메라가 더 멀리 가요", $"{row.name}이 열렸어요!\n구역으로 쓸 땅이 넓어졌어요.",
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

    // 옛 구조의 영토 · 개간 작업 · 미션 (territory_west_1 … territory_north_2): 설정 목록에서 빼고 에셋을 지움 (세이브에는 ID 문자열만 남음)
    private static void RemoveOldTerritoryData(SerializedObject configSo)
    {
        var oldId = new System.Text.RegularExpressions.Regex(@"territory_(west|north)_\d");
        void Strip(string property, System.Func<Object, string> idOf)
        {
            var list = configSo.FindProperty(property);
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var item = list.GetArrayElementAtIndex(i).objectReferenceValue;
                string id = item != null ? idOf(item) : null;
                if (id == null || !oldId.IsMatch(id))
                    continue;
                list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
            }
        }
        Strip("_tasks", o => (o as SettlementTaskDefinition)?.TaskId);
        Strip("_projects", o => (o as CommunityProjectDefinition)?.ProjectId);
        Strip("_territories", o => (o as TerritoryExpansionDefinition)?.ExpansionId);
        configSo.ApplyModifiedPropertiesWithoutUndo();
        var oldFile = new System.Text.RegularExpressions.Regex(
            @"^(Territory_(west|north)_\d|task_territory_(west|north)_\d_\d|Project_territory_(west|north)_\d_mission)$");
        foreach (var folder in new[] { TerritoryFolder, $"{DataFolder}/Tasks", ProjectFolder })
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (oldFile.IsMatch(Path.GetFileNameWithoutExtension(path)))
                    AssetDatabase.DeleteAsset(path);
            }
        }
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

        // 2) 숲: 묶음마다 그늘 + 나무·덤불. 개간 조각은 그 개간을 끝내면 사라지고, 그 단계가 열리기 전까지 그루터기. 블록 사이 숲은 늘 남음
        var forest = new GameObject("Forest").transform;
        forest.SetParent(territory, false);
        var stumps = new GameObject("Stumps").transform;
        stumps.SetParent(territory, false);
        var stumpParents = new Dictionary<string, Transform>();
        foreach (var group in layout.groups)
        {
            var node = new GameObject($"Forest_{group.id}").transform;
            node.SetParent(forest, false);
            if (!string.IsNullOrEmpty(group.development))
                AddGate(node.gameObject, group.development, false);
            if (group.shade != null && !string.IsNullOrEmpty(group.shade.sprite) && TerritorySprite(group.shade.sprite) != null)
            {
                var shade = CreateSprite("Shade", node, TerritorySprite(group.shade.sprite),
                    W((group.shade.x0 + group.shade.x1) * 0.5f, (group.shade.y0 + group.shade.y1) * 0.5f), false);
                shade.sortingLayerID = background.sortingLayerID;
                shade.sortingOrder = PlazaDepth.BackgroundOrder + 5;
            }
            foreach (var item in group.trees)
                ForestSprite(node, item, W(item.x, item.y), background);

            if (string.IsNullOrEmpty(group.development) || group.stumps == null || group.stumps.Length == 0)
                continue;
            string until = group.stumpsUntil ?? "";
            if (!stumpParents.TryGetValue(until, out var tierNode))
            {
                tierNode = new GameObject($"Stumps_{until}").transform;
                tierNode.SetParent(stumps, false);
                if (until.Length > 0)
                    AddGate(tierNode.gameObject, until, false);
                stumpParents[until] = tierNode;
            }
            var stumpNode = new GameObject($"Stumps_{group.id}").transform;
            stumpNode.SetParent(tierNode, false);
            AddGate(stumpNode.gameObject, group.development, true);
            // 개간 뒤에만 보이는 것은 꺼 둔 채 저장 (켜기는 DevelopmentGate가 함)
            stumpNode.gameObject.SetActive(false);
            foreach (var item in group.stumps)
                ForestSprite(stumpNode, item, W(item.x, item.y), background);
        }

        // 3) 걷기 영역: 공터 = 광장 기본 걷기 영역(MainPlaza), 단계 · 바닷가 = 그 발전이 열리면
        var walk = new GameObject("Walkable").transform;
        walk.SetParent(territory, false);
        foreach (var region in layout.regions)
        {
            if (string.IsNullOrEmpty(region.development))
            {
                SetStartWalkable(root.parent, region, W);
                continue;
            }
            var regionNode = new GameObject($"Walkable_{region.id}").transform;
            regionNode.SetParent(walk, false);
            for (int p = 0; p < region.polygons.Length; p++)
                CreateWalkablePolygon(regionNode, $"Area{p}", region.polygons[p].points, W);
            AddGate(regionNode.gameObject, region.development, true);
            var gate = new SerializedObject(regionNode.GetComponent<DevelopmentGate>());
            gate.FindProperty("_noPop").boolValue = true;
            gate.ApplyModifiedPropertiesWithoutUndo();
            // 꺼 둔 채 저장: 켜져 있으면 PlazaWalkableArea.Awake가 아직 안 열린 땅까지 걷기 영역을 계산함
            regionNode.gameObject.SetActive(false);
        }

        // 4) 개간 자리 (방향마다 하나, 단계마다 자리를 옮김)
        var sites = new List<TerritorySiteView>();
        foreach (TerritoryDirection direction in System.Enum.GetValues(typeof(TerritoryDirection)))
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

        // 5) 카메라 범위: 공터 + 열린 땅
        var mapView = territory.gameObject.AddComponent<TerritoryMapView>();
        var mapSo = new SerializedObject(mapView);
        mapSo.FindProperty("_camera").objectReferenceValue = camera;
        mapSo.FindProperty("_map").rectValue = PxToRect(layout.map, W);
        mapSo.FindProperty("_start").rectValue = PxToRect(layout.start, W);
        var regions = mapSo.FindProperty("_regions");
        var opened = System.Array.FindAll(layout.regions, r => !string.IsNullOrEmpty(r.development));
        regions.arraySize = opened.Length;
        for (int i = 0; i < opened.Length; i++)
        {
            var element = regions.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("development").stringValue = opened[i].development;
            element.FindPropertyRelative("rect").rectValue = PxToRect(opened[i].rect, W);
        }
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

    // 걷기 다각형 하나 (점은 그 오브젝트 기준)
    private static void CreateWalkablePolygon(Transform parent, string name, float[] flat, System.Func<float, float, Vector3> w)
    {
        var area = new GameObject(name).AddComponent<PlazaAreaPolygon>();
        area.transform.SetParent(parent, false);
        SetPolygonPoints(area, flat, w);
    }

    private static void SetPolygonPoints(PlazaAreaPolygon area, float[] flat, System.Func<float, float, Vector3> w)
    {
        var so = new SerializedObject(area);
        so.FindProperty("kind").enumValueIndex = (int)PlazaAreaKind.Walkable;
        var points = so.FindProperty("points");
        points.arraySize = flat.Length / 2;
        for (int i = 0; i < flat.Length / 2; i++)
            points.GetArrayElementAtIndex(i).vector2Value = area.transform.InverseTransformPoint(w(flat[i * 2], flat[i * 2 + 1]));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 처음 땅(중앙 공터)의 걷기 다각형 = 광장 기본 걷기 영역 (WalkableArea/MainPlaza). 다각형이 더 있으면 옆에 더 만듦
    private static void SetStartWalkable(Transform plazaRoot, TerritoryRegion region, System.Func<float, float, Vector3> w)
    {
        var walkable = plazaRoot != null ? plazaRoot.Find("WalkableArea") : null;
        var main = walkable != null ? walkable.Find("MainPlaza") : null;
        var mainArea = main != null ? main.GetComponent<PlazaAreaPolygon>() : null;
        if (mainArea == null || region.polygons.Length == 0)
        {
            Debug.LogError("[SettlementSetup] 광장 기본 걷기 영역(WalkableArea/MainPlaza)이 없거나 공터 다각형이 없습니다.");
            return;
        }
        SetPolygonPoints(mainArea, region.polygons[0].points, w);
        for (int i = walkable.childCount - 1; i >= 0; i--)
        {
            var child = walkable.GetChild(i);
            if (child.name.StartsWith("StartArea"))
                Object.DestroyImmediate(child.gameObject);
        }
        for (int p = 1; p < region.polygons.Length; p++)
            CreateWalkablePolygon(walkable, $"StartArea{p}", region.polygons[p].points, w);
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
