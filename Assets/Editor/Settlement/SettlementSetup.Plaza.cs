using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static GlobalUISetup;

/// <summary>
/// 광장 씬에 정착 진행을 배치한다: 게시판, 나뭇가지, 공사 현장, 밭 가는 길의 잡목, 건설 해달,
/// 기존 소품(집·벤치·가로등·울타리·피크닉·요정)에 발전 조건(DevelopmentGate). 여러 번 실행해도 결과가 같음.
/// 좌표는 바닥 그림(Plaza_Ground.png) 픽셀 = plaza_layout.json과 같은 기준.
/// </summary>
public static partial class SettlementSetup
{
    private const string PlazaScenePath = "Assets/Scenes/Plaza.unity";
    private const string PlazaSettingsPath = "Assets/Data/Plaza/PlazaSettings.asset";
    private const string MinerSpriteFolder = "Assets/Sprites/Characters/MinerOtter";
    private const string TitleFontAssetPath = "Assets/Fonts/Cafe24Ssurround-v2.0 SDF.asset";
    private const float GroundPixelsPerUnit = 40f;
    private const string RootName = "Settlement";

    // 말풍선·글자는 해달·소품 위에
    private const int OverlayOrder = 32000;

    // 기존 소품의 발전 조건: (소품, 바닥 픽셀 x, y) → 발전. 지금 광장 씬의 실제 위치 (plaza_layout.json 이후 손으로 옮긴 배치)
    // 목록에 없는 소품(나무·덤불·바위·통나무·그루터기)은 처음부터 있음
    // 첫 화면(초점 512,640 주변 약 x 312~712, y 280~1000)에 게시판·나뭇가지를 둔다
    private static readonly (string prop, float x, float y, string development)[] PropGates =
    {
        ("House_Blue", 965, 400, "house_1"),
        ("Bench", 240, 320, "chair"), // 의자 부탁으로 지음 (Site_Chair)
        ("Lamp", 990, 520, "house_1"),
        ("House_Red", 905, 240, "house_2"),
        ("Fence_Rising", 40, 690, "house_2"),
        ("Picnic", 140, 1000, "farmland"),
        ("Fence_Falling", 180, 1220, "farmland"),
        ("Fence_Falling", 253, 1256, "farmland"),
    };

    private static readonly Vector2 BoardPixel = new Vector2(400, 525);
    // 바위: 여러 번 쳐서 깨면 돌 3개, 150초 뒤 다시 솟음 (옛 돌무더기 자리)
    private static readonly Vector2[] RockPixels = { new Vector2(575, 770), new Vector2(360, 880) };

    // 흔드는 나무: 3번 흔들면 목재 4개(+가끔 사과), 120초 쉼. 첫 화면 왼쪽 아래·오른쪽 위 (게시판·요정과 안 겹침)
    private static readonly Vector2[] TreePixels = { new Vector2(385, 700), new Vector2(650, 548) };

    // 바닥 나뭇가지: 한 번 눌러 줍기 (목재 2개, 60초)
    private static readonly Vector2[] BranchPixels = { new Vector2(430, 800), new Vector2(690, 860) };
    private static readonly Vector2 GatherPixel = new Vector2(512, 640);   // 처음 카메라 화면 가운데
    private static readonly Vector2 ArrivalPixel = new Vector2(600, 1000); // 아래쪽 바닷가 길

    // 집 터 = 그 집 소품 자리 (위치·크기는 소품에서 읽음): (현장, 건설, 소품)
    private static readonly (string site, string construction, string prop)[] HouseSites =
    {
        ("Site_House1", "con_house_1", "House_Blue"),
        ("Site_House2", "con_house_2", "House_Red"),
    };

    // 밭 가는 길 (위쪽 길): 잡목·돌이 막고 있다가 개간하면 치워지고 밭 표지판이 열림
    private static readonly Vector2 FarmlandPixel = new Vector2(545, 150);
    private static readonly Vector2 FarmlandStandPixel = new Vector2(545, 290);
    private static readonly Vector2 FarmlandBubblePixel = new Vector2(545, 470);
    private static readonly Vector2 FarmSignPixel = new Vector2(665, 245);
    private static readonly (string prop, Vector2 pixel, float scale)[] BlockerProps =
    {
        ("Bush", new Vector2(545, 118), 1.1f),
        ("Rock", new Vector2(478, 150), 0.7f),
        ("Rock", new Vector2(612, 160), 0.55f),
        ("Log", new Vector2(548, 192), 0.6f),
    };

    [MenuItem("Tools/Settlement/Setup Plaza")]
    public static void PlacePlaza()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ImportArt();
        var scene = EditorSceneManager.OpenScene(PlazaScenePath, OpenSceneMode.Single);

        var plazaRoot = GameObject.Find("PlazaRoot").transform;
        var props = plazaRoot.Find("Props");
        var walkable = Object.FindAnyObjectByType<PlazaWalkableArea>();
        var controller = Object.FindAnyObjectByType<PlazaController>();
        var background = plazaRoot.Find("Background").GetComponent<SpriteRenderer>();
        var bounds = background.bounds;
        Vector3 ToWorld(Vector2 px) => new Vector3(bounds.min.x + px.x / GroundPixelsPerUnit, bounds.max.y - px.y / GroundPixelsPerUnit, 0f);
        Vector2 ToPixel(Vector3 world) => new Vector2((world.x - bounds.min.x) * GroundPixelsPerUnit, (bounds.max.y - world.y) * GroundPixelsPerUnit);

        var old = plazaRoot.Find(RootName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(RootName).transform;
        root.SetParent(plazaRoot, false);

        GateExistingProps(props, ToPixel);

        var board = BuildBoardProp(root, ToWorld(BoardPixel));
        for (int i = 0; i < BranchPixels.Length; i++)
            BuildGatherPoint(root, $"branch_{i + 1:00}", ToWorld(BranchPixels[i]), "Prop_Branches", null, 0, 0f);
        var stone = AssetDatabase.LoadAssetAtPath<ItemDefinition>(StoneItemPath);
        var gem = AssetDatabase.LoadAssetAtPath<Currency>(GemPath);
        for (int i = 0; i < RockPixels.Length; i++)
            BuildRockNode(root, $"rock_{i + 1:00}", ToWorld(RockPixels[i]), stone, gem, i == 0);
        var apple = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AppleItemPath);
        for (int i = 0; i < TreePixels.Length; i++)
            BuildTreeNode(root, $"tree_{i + 1:00}", ToWorld(TreePixels[i]), apple, i == 0);

        var siteViews = new List<ConstructionSiteView>();
        foreach (var h in HouseSites)
        {
            var house = props.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith(h.prop + "_"));
            if (house == null)
            {
                Debug.LogError($"[SettlementSetup] 광장에 {h.prop} 소품이 없어 {h.site}를 만들지 못했습니다.");
                continue;
            }
            siteViews.Add(BuildHouseSite(root, h.site, h.construction, house.GetComponent<SpriteRenderer>(), FootprintDepthOffset(house)));
        }
        var bench = props.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Bench_"));
        if (bench != null)
            siteViews.Add(BuildChairSite(root, bench.position));
        else
            Debug.LogError("[SettlementSetup] 광장에 Bench 소품이 없어 의자 공사 현장을 만들지 못했습니다.");
        var clearTargets = BuildFarmPath(root, ToWorld);
        siteViews.Add(BuildClearingSite(root, ToWorld(FarmlandPixel), ToWorld(FarmlandStandPixel), ToWorld(FarmlandBubblePixel), clearTargets));

        var builder = BuildBuilder(root, ToWorld(ArrivalPixel));

        var gatherPoint = new GameObject("GatherPoint").transform;
        gatherPoint.SetParent(root, false);
        gatherPoint.position = ToWorld(GatherPixel);
        var arrivalPoint = new GameObject("ArrivalPoint").transform;
        arrivalPoint.SetParent(root, false);
        arrivalPoint.position = ToWorld(ArrivalPixel);

        // 요정 상점은 밭이 열린 뒤 (모종을 사러)
        var fairy = Object.FindAnyObjectByType<FairyNpcView>(FindObjectsInactive.Include);
        if (fairy != null)
            AddGate(fairy.gameObject, "farmland", true);

        // 걷기 영역이 게시판·잡목 발자국도 보게
        AddPolygonRoot(walkable, root);
        BuildAmbience(root, PlazaDepth.FlatPropOrder + 1, 3, OverlayOrder - 10, 2, OverlayOrder - 20, 6);
        BuildNightLights(root, props);

        // 광장 해달은 정착 진행이 내보냄 (아무나 5마리 → 첫 해달 한 마리부터)
        var settings = AssetDatabase.LoadAssetAtPath<PlazaSettings>(PlazaSettingsPath);
        settings.otterCount = 0;
        EditorUtility.SetDirty(settings);

        var controllerSo = new SerializedObject(controller);
        var view = root.gameObject.AddComponent<SettlementPlazaView>();
        var viewSo = new SerializedObject(view);
        viewSo.FindProperty("_walkableArea").objectReferenceValue = walkable;
        viewSo.FindProperty("_camera").objectReferenceValue = Object.FindAnyObjectByType<PlazaCameraController>();
        viewSo.FindProperty("_plazaSettings").objectReferenceValue = settings;
        viewSo.FindProperty("_otterRoot").objectReferenceValue = controllerSo.FindProperty("otterRoot").objectReferenceValue;
        viewSo.FindProperty("_gatherPoint").objectReferenceValue = gatherPoint;
        viewSo.FindProperty("_arrivalPoint").objectReferenceValue = arrivalPoint;
        var gateRoots = new List<Transform> { props, root };
        if (fairy != null)
            gateRoots.Add(fairy.transform);
        SetTransforms(viewSo.FindProperty("_gateRoots"), gateRoots);
        var sites = viewSo.FindProperty("_sites");
        sites.arraySize = siteViews.Count;
        for (int i = 0; i < siteViews.Count; i++)
            sites.GetArrayElementAtIndex(i).objectReferenceValue = siteViews[i];
        viewSo.FindProperty("_builder").objectReferenceValue = builder;
        viewSo.FindProperty("_speechBubble").objectReferenceValue = LoadPropArt("UI_Bubble_Speech");
        viewSo.FindProperty("_alertBubble").objectReferenceValue = LoadPropArt("UI_Bubble_Alert");
        viewSo.FindProperty("_hammerBubble").objectReferenceValue = LoadPropArt("UI_Bubble_Hammer");
        viewSo.FindProperty("_speechFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontAssetPath);
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[SettlementSetup] 광장 배치 완료");
    }

    private static void SetTransforms(SerializedProperty list, List<Transform> values)
    {
        list.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void AddGate(GameObject target, string development, bool showWhenUnlocked, bool focus = false)
    {
        var gate = target.GetComponent<DevelopmentGate>();
        if (gate == null)
            gate = target.AddComponent<DevelopmentGate>();
        var so = new SerializedObject(gate);
        so.FindProperty("_developmentId").stringValue = development;
        so.FindProperty("_showWhenUnlocked").boolValue = showWhenUnlocked;
        so.FindProperty("_focusOnUnlock").boolValue = focus;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 소품 이름은 "{소품}_{번호}" (PlazaSceneSetup). 같은 소품끼리는 위치로 구분
    private static void GateExistingProps(Transform props, System.Func<Vector3, Vector2> toPixel)
    {
        var matched = new HashSet<int>();
        foreach (Transform prop in props)
        {
            var old = prop.GetComponent<DevelopmentGate>();
            if (old != null)
                Object.DestroyImmediate(old);
            prop.gameObject.SetActive(true);

            string kind = prop.name.Contains("_") ? prop.name.Substring(0, prop.name.LastIndexOf('_')) : prop.name;
            Vector2 pixel = toPixel(prop.position);
            for (int i = 0; i < PropGates.Length; i++)
            {
                var g = PropGates[i];
                if (g.prop == kind && !matched.Contains(i) && Vector2.Distance(pixel, new Vector2(g.x, g.y)) < 6f)
                {
                    AddGate(prop.gameObject, g.development, true, kind.StartsWith("House"));
                    matched.Add(i);
                    break;
                }
            }
            // 비스듬한 큰 건물: 앞 모서리가 아니라 발자국 가운데를 기준으로 정렬 (벽 앞 해달이 가려지지 않게)
            if (kind.StartsWith("House"))
            {
                var propSo = new SerializedObject(prop.GetComponent<PlazaProp>());
                propSo.FindProperty("depthOffset").floatValue = FootprintDepthOffset(prop);
                propSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        for (int i = 0; i < PropGates.Length; i++)
        {
            if (!matched.Contains(i))
                Debug.LogWarning($"[SettlementSetup] 발전 조건을 붙일 소품을 못 찾음: {PropGates[i].prop} ({PropGates[i].x}, {PropGates[i].y})");
        }
    }

    /// <summary>발자국(Blocked 다각형) 꼭짓점의 가운데 높이 − 발밑 높이 (월드 단위). 발자국이 없으면 0</summary>
    private static float FootprintDepthOffset(Transform prop)
    {
        var footprint = prop.GetComponentInChildren<PlazaAreaPolygon>(true);
        if (footprint == null)
            return 0f;
        var points = new List<Vector2>();
        footprint.GetWorldPoints(points);
        if (points.Count == 0)
            return 0f;
        float sum = 0f;
        foreach (var p in points)
            sum += p.y;
        return sum / points.Count - prop.position.y;
    }

    private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Vector3 position, bool sorted)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (sorted)
            go.AddComponent<PlazaProp>();
        return renderer;
    }

    /// <summary>걷기 영역이 이 루트 아래의 막힌 발자국도 보게 (지난 배치에서 지운 루트가 남긴 빈 칸은 정리)</summary>
    private static void AddPolygonRoot(PlazaWalkableArea walkable, Transform root)
    {
        var areaSo = new SerializedObject(walkable);
        var roots = areaSo.FindProperty("polygonRoots");
        for (int i = roots.arraySize - 1; i >= 0; i--)
        {
            if (roots.GetArrayElementAtIndex(i).objectReferenceValue == null)
                roots.DeleteArrayElementAtIndex(i);
        }
        bool hasRoot = false;
        for (int i = 0; i < roots.arraySize; i++)
            hasRoot |= roots.GetArrayElementAtIndex(i).objectReferenceValue == root;
        if (!hasRoot)
        {
            roots.arraySize++;
            roots.GetArrayElementAtIndex(roots.arraySize - 1).objectReferenceValue = root;
        }
        areaSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject AddFootprint(Transform parent, Rect localRect)
    {
        var go = new GameObject("Footprint");
        go.transform.SetParent(parent, false);
        var polygon = go.AddComponent<PlazaAreaPolygon>();
        var so = new SerializedObject(polygon);
        so.FindProperty("kind").enumValueIndex = (int)PlazaAreaKind.Blocked;
        var points = so.FindProperty("points");
        var corners = new[] { new Vector2(localRect.xMin, localRect.yMin), new Vector2(localRect.xMax, localRect.yMin),
            new Vector2(localRect.xMax, localRect.yMax), new Vector2(localRect.xMin, localRect.yMax) };
        points.arraySize = corners.Length;
        for (int i = 0; i < corners.Length; i++)
            points.GetArrayElementAtIndex(i).vector2Value = corners[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    private static void AddDecorBlock(Transform parent, Vector2 localCenter, Vector2 size)
    {
        var block = new GameObject("DecorBlock").AddComponent<DecorBlockArea>();
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localCenter;
        var so = new SerializedObject(block);
        so.FindProperty("_size").vector2Value = size;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #region 게시판 · 나뭇가지

    private static SettlementBoardPropView BuildBoardProp(Transform root, Vector3 position)
    {
        var board = CreateSprite("Board", root, LoadPropArt("Prop_Board"), position, true);
        var tap = board.gameObject.AddComponent<BoxCollider2D>();
        tap.size = new Vector2(2.6f, 2.8f);
        tap.offset = new Vector2(0f, 1.35f);
        AddFootprint(board.transform, new Rect(-1.1f, -0.2f, 2.2f, 0.5f));
        AddDecorBlock(board.transform, new Vector2(0f, 0.4f), new Vector2(2.6f, 1.2f));

        var attention = CreateSprite("Attention", board.transform, LoadPropArt("UI_Bubble_Alert"), position + new Vector3(0.95f, 2.6f, 0f), false);
        attention.sortingOrder = OverlayOrder;

        var view = board.gameObject.AddComponent<SettlementBoardPropView>();
        Set(view, "_tapArea", tap);
        Set(view, "_attention", attention);
        return view;
    }

    /// <param name="item">null이면 설정의 기본값(목재 2개, 60초)</param>
    private static void BuildGatherPoint(Transform root, string pointId, Vector3 position, string art,
        ItemDefinition item, int amount, float cooldownSeconds)
    {
        var point = new GameObject($"Gather_{pointId}").transform;
        point.SetParent(root, false);
        point.position = position;

        var visual = CreateSprite("Visual", point, LoadPropArt(art), position, true);
        var tap = point.gameObject.AddComponent<CircleCollider2D>();
        tap.radius = 0.8f;
        tap.offset = new Vector2(0f, 0.3f);

        var popup = CreateWorldText("Popup", point, new Vector3(0f, 1.1f, 0f), "+2 목재");

        var view = point.gameObject.AddComponent<GatherPointView>();
        var so = new SerializedObject(view);
        so.FindProperty("_pointId").stringValue = pointId;
        so.FindProperty("_visual").objectReferenceValue = visual.gameObject;
        so.FindProperty("_tapArea").objectReferenceValue = tap;
        so.FindProperty("_popup").objectReferenceValue = popup;
        so.FindProperty("_item").objectReferenceValue = item;
        so.FindProperty("_amount").intValue = amount;
        so.FindProperty("_cooldownSeconds").floatValue = cooldownSeconds;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 여러 번 쳐서 깨는 바위 (금 간 그림 3단계 + 자갈, 약점 반짝이, 드물게 조개)
    private static void BuildRockNode(Transform root, string pointId, Vector3 position, ItemDefinition stone, Currency gem, bool hint)
    {
        var node = new GameObject($"Rock_{pointId}").transform;
        node.SetParent(root, false);
        node.position = position;

        var visual = CreateSprite("Visual", node, LoadPropArt("Prop_Rock_0"), position, true);
        // 바위가 바닥을 차지하는 만큼 (해달이 올라서지 않게). 깨져 자갈만 남아도 그대로 (다시 솟을 자리)
        AddFootprint(node, RockFootprint);
        var timer = CreateRegrowTimer(node, position + new Vector3(0f, 0.75f, 0f));

        var tap = node.gameObject.AddComponent<CircleCollider2D>();
        tap.radius = 0.85f;
        tap.offset = new Vector2(0f, 0.45f);

        var weakSpot = CreateSprite("WeakSpot", node, LoadPropArt("FX_Sparkle"), position + new Vector3(0f, 0.6f, 0f), false);

        var view = node.gameObject.AddComponent<RockNodeView>();
        var so = new SerializedObject(view);
        so.FindProperty("_pointId").stringValue = pointId;
        so.FindProperty("_item").objectReferenceValue = stone;
        so.FindProperty("_rareCurrency").objectReferenceValue = gem;
        var cracks = so.FindProperty("_crackSprites");
        cracks.arraySize = 3;
        for (int i = 0; i < 3; i++)
            cracks.GetArrayElementAtIndex(i).objectReferenceValue = LoadPropArt($"Prop_Rock_{i}");
        so.FindProperty("_rubbleSprite").objectReferenceValue = LoadPropArt("Prop_Rock_Rubble");
        so.FindProperty("_chipSprite").objectReferenceValue = LoadPropArt("FX_StoneChip");
        so.FindProperty("_timer").objectReferenceValue = timer;
        SetRegrowSprites(so.FindProperty("_timerSprites"));
        so.FindProperty("_renderer").objectReferenceValue = visual;
        so.FindProperty("_weakSpot").objectReferenceValue = weakSpot;
        so.FindProperty("_tapArea").objectReferenceValue = tap;
        so.FindProperty("_fx").objectReferenceValue = CreateNodeFx(node);
        if (hint)
            so.FindProperty("_hint").objectReferenceValue = CreateTapHint(node, "hint_rock", "톡톡 쳐서 돌을 캐요!", new Vector3(0f, 1.35f, 0f));
        so.FindProperty("_weakSpotHint").objectReferenceValue =
            CreateTapHint(node, "hint_weak_spot", "반짝이는 곳을 치면 3배!", new Vector3(0f, 0.45f, 0f), weakSpot.transform);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 흔들면 나뭇가지·사과가 떨어지는 사과나무 (다 흔들면 잎이 성긴 그림)
    private static void BuildTreeNode(Transform root, string pointId, Vector3 position, ItemDefinition fruit, bool hint)
    {
        var node = new GameObject($"Tree_{pointId}").transform;
        node.SetParent(root, false);
        node.position = position;

        var visual = CreateSprite("Visual", node, LoadPropArt("Prop_AppleTree_0"), position, true);
        AddFootprint(node, TreeFootprint);
        var timer = CreateRegrowTimer(node, position + new Vector3(0.85f, 0.45f, 0f));

        var tap = node.gameObject.AddComponent<CircleCollider2D>();
        tap.radius = 1.4f;
        tap.offset = new Vector2(0f, 1.8f);

        var view = node.gameObject.AddComponent<TreeShakeView>();
        var so = new SerializedObject(view);
        so.FindProperty("_pointId").stringValue = pointId;
        so.FindProperty("_fruit").objectReferenceValue = fruit;
        so.FindProperty("_readySprite").objectReferenceValue = LoadPropArt("Prop_AppleTree_0");
        so.FindProperty("_restSprite").objectReferenceValue = LoadPropArt("Prop_AppleTree_1");
        so.FindProperty("_branchSprite").objectReferenceValue = LoadPropArt("Prop_Branches");
        so.FindProperty("_leafSprite").objectReferenceValue = LoadPropArt("FX_Leaf");
        so.FindProperty("_timer").objectReferenceValue = timer;
        SetRegrowSprites(so.FindProperty("_timerSprites"));
        so.FindProperty("_renderer").objectReferenceValue = visual;
        so.FindProperty("_tapArea").objectReferenceValue = tap;
        so.FindProperty("_fx").objectReferenceValue = CreateNodeFx(node);
        if (hint)
            so.FindProperty("_hint").objectReferenceValue = CreateTapHint(node, "hint_tree", "흔들면 나뭇가지가 떨어져요!", new Vector3(0f, 2.5f, 0f));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private const string WindShaderPath = "Assets/Art/Shaders/SpriteWindSway.shader";
    private const string WindMaterialPath = "Assets/Art/Shaders/SpriteWindSway.mat";

    // 바람 셰이더 재질 (없으면 만듦)
    private static Material WindMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath);
        if (material != null)
            return material;
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(WindShaderPath);
        if (shader == null)
        {
            Debug.LogError($"[SettlementSetup] 바람 셰이더가 없습니다: {WindShaderPath}");
            return null;
        }
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, WindMaterialPath);
        return material;
    }

    // 분위기 연출: 발밑 그림자·살랑임·나비·구름 그림자 (AmbienceDirector가 실행 중에 붙이고 만듦)
    private static void BuildAmbience(Transform root, int shadowOrder, int butterflies, int butterflyOrder, int clouds, int cloudOrder, int fireflies)
    {
        var go = new GameObject("Ambience");
        go.transform.SetParent(root, false);
        var view = go.AddComponent<AmbienceDirector>();
        var so = new SerializedObject(view);
        so.FindProperty("_shadowSprite").objectReferenceValue = LoadPropArt("FX_Shadow");
        so.FindProperty("_shadowOrder").intValue = shadowOrder;
        so.FindProperty("_windMaterial").objectReferenceValue = WindMaterial();
        var frames = so.FindProperty("_butterflyFrames");
        frames.arraySize = 2;
        frames.GetArrayElementAtIndex(0).objectReferenceValue = LoadPropArt("FX_Butterfly_0");
        frames.GetArrayElementAtIndex(1).objectReferenceValue = LoadPropArt("FX_Butterfly_1");
        so.FindProperty("_butterflyCount").intValue = butterflies;
        so.FindProperty("_butterflyOrder").intValue = butterflyOrder;
        so.FindProperty("_fireflySprite").objectReferenceValue = LoadPropArt("FX_Firefly");
        so.FindProperty("_glowMaterial").objectReferenceValue = GlowMaterial();
        so.FindProperty("_fireflyCount").intValue = fireflies;
        so.FindProperty("_fireflyOrder").intValue = butterflyOrder + 1;
        so.FindProperty("_cloudSprite").objectReferenceValue = LoadPropArt("FX_CloudShadow");
        so.FindProperty("_cloudCount").intValue = clouds;
        so.FindProperty("_cloudOrder").intValue = cloudOrder;
        so.ApplyModifiedPropertiesWithoutUndo();
        var lighting = go.AddComponent<TimeOfDayLighting>();
        var lightingSo = new SerializedObject(lighting);
        lightingSo.FindProperty("_litMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
        lightingSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // 조명을 받지 않는 기본 스프라이트 재질 (밤 불빛·반딧불이 어둠 속에서도 빛나게)
    private static Material GlowMaterial() =>
        AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");

    // 밤 불빛: 집 창문·가로등 유리 그림(night_glow.py) + 주변을 비추는 점 조명. 소품이 보일 때만 켜짐
    private static readonly (string prop, string art, float lightX, float lightY, float radius)[] NightLights =
    {
        // 조명 위치는 소품 그림 영역의 비율 (왼쪽 아래 0,0)
        ("House_Blue", "Night_House_Blue", 0.55f, 0.35f, 3.2f),
        ("House_Red", "Night_House_Red", 0.55f, 0.35f, 3.2f),
        ("Lamp", "Night_Lamp", 0.2f, 0.55f, 2.6f),
    };

    private static void BuildNightLights(Transform root, Transform props)
    {
        var parent = new GameObject("NightLights").transform;
        parent.SetParent(root, false);
        foreach (var n in NightLights)
        {
            foreach (Transform prop in props)
            {
                if (!prop.name.StartsWith(n.prop + "_"))
                    continue;
                var propRenderer = prop.GetComponent<SpriteRenderer>();
                var sprite = ImportStageSprite(n.art, propRenderer.sprite);
                if (sprite == null)
                    continue;

                var go = new GameObject($"NightGlow_{prop.name}");
                go.transform.SetParent(parent, false);
                go.transform.position = prop.position;
                go.transform.localScale = prop.lossyScale;
                var glow = go.AddComponent<SpriteRenderer>();
                glow.sprite = sprite;
                glow.sharedMaterial = GlowMaterial();
                glow.sortingOrder = propRenderer.sortingOrder + 1;
                glow.color = new Color(1f, 1f, 1f, 0f);

                var bounds = propRenderer.bounds;
                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(go.transform, false);
                lightGo.transform.position = new Vector3(bounds.min.x + bounds.size.x * n.lightX, bounds.min.y + bounds.size.y * n.lightY, 0f);
                var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
                light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
                light.pointLightOuterRadius = n.radius;
                light.pointLightInnerRadius = n.radius * 0.15f;
                light.color = new Color(1f, 0.8f, 0.48f);
                light.intensity = 0f;
                light.targetSortingLayers = SortingLayer.layers.Select(l => l.id).ToArray();

                var view = go.AddComponent<NightGlow>();
                var so = new SerializedObject(view);
                so.FindProperty("_glow").objectReferenceValue = glow;
                so.FindProperty("_light").objectReferenceValue = light;
                so.FindProperty("_followActive").objectReferenceValue = prop.gameObject;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    // 바위·나무가 바닥을 차지하는 만큼 (발 위치 기준, 해달이 올라서거나 그늘 밑을 지나가지 않게)
    private static readonly Rect RockFootprint = new Rect(-0.9f, -0.15f, 1.8f, 0.8f);
    private static readonly Rect TreeFootprint = new Rect(-0.8f, -0.15f, 1.6f, 0.7f);

    // 다시 생기기까지 작은 시계 (쉬는 동안만 켜짐)
    private static SpriteRenderer CreateRegrowTimer(Transform node, Vector3 position)
    {
        var timer = CreateSprite("RegrowTimer", node, LoadPropArt("FX_Regrow_0"), position, false);
        timer.sortingOrder = OverlayOrder - 3;
        timer.gameObject.SetActive(false);
        return timer;
    }

    private static void SetRegrowSprites(SerializedProperty list)
    {
        list.arraySize = 8;
        for (int i = 0; i < 8; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = LoadPropArt($"FX_Regrow_{i}");
    }

    // 처음 한 번 보이는 안내 말풍선 (해달 말풍선 그림 + 글자). follow가 있으면 그 대상을 따라다님 (약점 반짝이)
    private static TapHintView CreateTapHint(Transform parent, string flag, string text, Vector3 offset, Transform follow = null)
    {
        var go = new GameObject("TapHint");
        go.transform.SetParent(parent, false);

        var bubble = new GameObject("Bubble");
        bubble.transform.SetParent(go.transform, false);
        bubble.transform.localPosition = offset;
        bubble.transform.localScale = Vector3.one * 0.85f;
        var sprite = bubble.AddComponent<SpriteRenderer>();
        sprite.sprite = LoadPropArt("UI_Bubble_Speech");
        sprite.sortingOrder = OverlayOrder;

        var label = CreateWorldText("Text", bubble.transform, new Vector3(0f, 1.05f, 0f), text);
        label.outlineWidth = 0f;
        label.fontSize = 3f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 2f;
        label.fontSizeMax = 3f;
        label.rectTransform.sizeDelta = new Vector2(3.4f, 1.1f);
        label.sortingOrder = OverlayOrder + 1;

        var view = go.AddComponent<TapHintView>();
        var so = new SerializedObject(view);
        so.FindProperty("_flag").stringValue = flag;
        so.FindProperty("_bubble").objectReferenceValue = bubble;
        so.FindProperty("_follow").objectReferenceValue = follow;
        so.FindProperty("_offset").vector3Value = offset;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // 튀는 조각·떠오르는 글자 (바위·나무 공용)
    private static PlazaNodeFx CreateNodeFx(Transform node)
    {
        var go = new GameObject("Fx");
        go.transform.SetParent(node, false);
        var text = CreateWorldText("Text", go.transform, Vector3.zero, "+3 돌");
        var fx = go.AddComponent<PlazaNodeFx>();
        var so = new SerializedObject(fx);
        so.FindProperty("_textTemplate").objectReferenceValue = text;
        so.FindProperty("_sortingOrder").intValue = OverlayOrder - 2;
        so.ApplyModifiedPropertiesWithoutUndo();
        return fx;
    }

    private static TextMeshPro CreateWorldText(string name, Transform parent, Vector3 localPosition, string sample)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var text = go.AddComponent<TextMeshPro>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontAssetPath);
        text.text = sample;
        text.fontSize = 5f;
        text.color = Cocoa;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(0xFF, 0xF4, 0xE6, 0xFF);
        text.sortingOrder = OverlayOrder;
        return text;
    }

    #endregion

    #region 공사 현장 · 밭 가는 길

    private static (ConstructionSiteView view, SerializedObject so) CreateSite(Transform root, string name, string constructionId,
        Vector3 position, Vector3 stand, Vector3 bubble, float dustWidth)
    {
        var site = new GameObject(name).transform;
        site.SetParent(root, false);
        site.position = position;

        var standPoint = new GameObject("StandPoint").transform;
        standPoint.SetParent(site, false);
        standPoint.position = stand;
        var bubbleAnchor = new GameObject("BubbleAnchor").transform;
        bubbleAnchor.SetParent(site, false);
        bubbleAnchor.position = bubble;

        var view = site.gameObject.AddComponent<ConstructionSiteView>();
        var so = new SerializedObject(view);
        so.FindProperty("_constructionId").stringValue = constructionId;
        so.FindProperty("_standPoint").objectReferenceValue = standPoint;
        so.FindProperty("_bubbleAnchor").objectReferenceValue = bubbleAnchor;
        so.FindProperty("_dustSprite").objectReferenceValue = LoadPropArt("FX_Dust");
        so.FindProperty("_dustWidth").floatValue = dustWidth;
        so.FindProperty("_twinkleSprite").objectReferenceValue = LoadPropArt("FX_Twinkle");
        so.FindProperty("_glowMaterial").objectReferenceValue = GlowMaterial();
        return (view, so);
    }

    // 집 터: 주춧돌 + 단계 그림(골조 → 벽 → 마무리, Tools/UIGen/house_stages.py가 완성 집 그림으로 만듦)
    // 단계 그림은 완성 집과 같은 자리·크기·정렬로 그려서, 완성되는 순간 진짜 집으로 자연스럽게 바뀜
    private static ConstructionSiteView BuildHouseSite(Transform root, string name, string constructionId, SpriteRenderer house, float depthOffset)
    {
        // 소품 이름(House_Blue_01) → 단계 그림 이름(Stage_House_Blue_1~3)
        string houseName = house.name.Substring(0, house.name.LastIndexOf('_'));
        var bounds = house.bounds;
        Vector3 basePoint = house.transform.position;
        // 해달은 집 왼쪽 앞에서 일하고, 진행 말풍선은 집 아래 (말풍선이 집을 가리지 않게)
        var stand = new Vector3(bounds.min.x + bounds.size.x * 0.12f, basePoint.y - 0.7f, 0f);
        var bubble = new Vector3(bounds.center.x, basePoint.y - 2.9f, 0f);
        var (view, so) = CreateSite(root, name, constructionId, basePoint, stand, bubble, bounds.size.x * 0.8f);
        var site = view.transform;

        // 건설 예정지 (0단계): 단계 그림과 같은 자리·크기로 바닥에 깖. 그림이 아직 없으면 예전 주춧돌 그림을 집 그림 가운데에
        var siteSprite = ImportStageSprite($"Stage_{houseName}_0", house.sprite);
        SpriteRenderer foundation;
        if (siteSprite != null)
        {
            foundation = CreateSprite("Scaffold", site, siteSprite, basePoint, true);
            foundation.transform.localScale = house.transform.lossyScale;
            foundation.flipX = house.flipX;
        }
        else
        {
            var foundationSprite = LoadPropArt("Prop_Foundation");
            foundation = CreateSprite("Scaffold", site, foundationSprite, new Vector3(bounds.center.x, basePoint.y, 0f), true);
            foundation.transform.localScale = Vector3.one * (bounds.size.x * 1.15f / foundationSprite.bounds.size.x);
        }
        var propSo = new SerializedObject(foundation.GetComponent<PlazaProp>());
        propSo.FindProperty("flat").boolValue = true; // 바닥에 깔린 터: 해달이 위로 지나감
        propSo.ApplyModifiedPropertiesWithoutUndo();
        foundation.gameObject.SetActive(false);

        var stages = new List<Sprite>();
        for (int i = 1; i <= 3; i++)
        {
            var sprite = ImportStageSprite($"Stage_{houseName}_{i}", house.sprite);
            if (sprite != null)
                stages.Add(sprite);
        }

        var stage = CreateSprite("Stage", site, stages.Count > 0 ? stages[0] : house.sprite, basePoint, true);
        stage.transform.localScale = house.transform.lossyScale;
        stage.flipX = house.flipX;
        SetDepthOffset(stage, depthOffset);
        stage.gameObject.SetActive(false);

        // 집이 생길 자리에는 장난감을 못 놓게
        AddDecorBlock(site, new Vector2(bounds.center.x - basePoint.x, bounds.size.y * 0.25f), new Vector2(bounds.size.x, bounds.size.y * 0.5f));

        so.FindProperty("_scaffold").objectReferenceValue = foundation.gameObject;
        so.FindProperty("_stage").objectReferenceValue = stage;
        var stageList = so.FindProperty("_stageSprites");
        stageList.arraySize = stages.Count;
        for (int i = 0; i < stages.Count; i++)
            stageList.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // 단계 그림은 완성 집 그림 캔버스에 사방 같은 여백을 더한 크기(fit_stage_images.py의 PAD) →
    // 완성 집의 PPU를 쓰고, 피벗은 완성 집 피벗을 여백만큼 옮겨서 겹쳐 보이게 함
    private static Sprite ImportStageSprite(string name, Sprite house)
    {
        string path = $"{ArtFolder}/{name}.png";
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"[SettlementSetup] 공사 단계 그림이 없습니다: {path} (python Tools/UIGen/house_stages.py)");
            return null;
        }
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = house.pixelsPerUnit;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        var pad = new Vector2((width - house.rect.width) * 0.5f, (height - house.rect.height) * 0.5f);
        settings.spritePivot = new Vector2((house.pivot.x + pad.x) / width, (house.pivot.y + pad.y) / height);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void SetDepthOffset(SpriteRenderer renderer, float offset)
    {
        var propSo = new SerializedObject(renderer.GetComponent<PlazaProp>());
        propSo.FindProperty("depthOffset").floatValue = offset;
        propSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // 개간 현장: 진행에 따라 덤불 → 돌 → 통나무가 하나씩 치워짐
    // 의자: 단계 그림 없이 먼지만 (완성되면 DevelopmentGate가 벤치 소품을 켬). 첫 해달이 옆에서 일함
    private static ConstructionSiteView BuildChairSite(Transform root, Vector3 bench)
    {
        var (view, so) = CreateSite(root, "Site_Chair", "con_chair", bench,
            bench + new Vector3(1.4f, -0.2f, 0f), bench + new Vector3(0f, 2.2f, 0f), 2.4f);
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static ConstructionSiteView BuildClearingSite(Transform root, Vector3 position, Vector3 stand, Vector3 bubble, List<GameObject> clearTargets)
    {
        var (view, so) = CreateSite(root, "Site_Farmland", "con_farmland", position, stand, bubble, 3.5f);
        var targets = so.FindProperty("_clearTargets");
        targets.arraySize = clearTargets.Count;
        for (int i = 0; i < clearTargets.Count; i++)
            targets.GetArrayElementAtIndex(i).objectReferenceValue = clearTargets[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    /// <returns>치울 순서대로의 잡목·돌·통나무</returns>
    private static List<GameObject> BuildFarmPath(Transform root, System.Func<Vector2, Vector3> toWorld)
    {
        // 개간 전: 잡목·돌·통나무 + 잠긴 밭 표지판 (개간하면 사라짐)
        var blocker = new GameObject("FarmPathBlocker").transform;
        blocker.SetParent(root, false);
        var clearTargets = new List<GameObject>();
        foreach (var b in BlockerProps)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlazaPrefabFolder}/Prop_{b.prop}.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, blocker);
            instance.transform.position = toWorld(b.pixel);
            instance.transform.localScale = Vector3.one * b.scale;
            clearTargets.Add(instance);
        }
        CreateSprite("FarmSign_Locked", blocker, LoadPropArt("Prop_FarmSign_Locked"), toWorld(FarmSignPixel), true);
        AddGate(blocker.gameObject, "farmland", false);

        // 개간 후: 밭 표지판
        var sign = CreateSprite("FarmSign", root, LoadPropArt("Prop_FarmSign"), toWorld(FarmSignPixel), true);
        AddGate(sign.gameObject, "farmland", true, true);
        return clearTargets;
    }

    #endregion

    #region 건설 해달

    // 광부 해달 시트: 걷기 4줄(오른쪽·왼쪽·앞·뒤) × 8칸 212×272, 곡괭이질 1줄 × 8칸 308×276 (MineSceneSetup이 잘라 둠)
    private static BuilderOtterController BuildBuilder(Transform root, Vector3 position)
    {
        var go = new GameObject("BuilderOtter");
        go.transform.SetParent(root, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 1.1f; // 광장 해달(1.67)과 키를 맞춤
        var renderer = go.AddComponent<SpriteRenderer>();
        var animator = go.AddComponent<SpriteFrameAnimator>();

        var walk = SliceGrid($"{MinerSpriteFolder}/MinerOtter_Walk.png", 212, 272);
        var mine = SliceGrid($"{MinerSpriteFolder}/MinerOtter_Mine.png", 308, 276);
        renderer.sprite = mine[0][0];

        var clips = new (string name, Sprite[] frames, float fps)[]
        {
            (BuilderOtterController.ClipIdle, new[] { mine[0][0] }, 1f),
            (BuilderOtterController.ClipWalkRight, walk[0], 10f),
            (BuilderOtterController.ClipWalkLeft, walk[1], 10f),
            (BuilderOtterController.ClipWalkDown, walk[2], 10f),
            (BuilderOtterController.ClipWalkUp, walk[3], 10f),
            (BuilderOtterController.ClipWork, mine[0], 8f),
        };
        var animSo = new SerializedObject(animator);
        var clipsProp = animSo.FindProperty("clips");
        clipsProp.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            var element = clipsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("name").stringValue = clips[i].name;
            element.FindPropertyRelative("fps").floatValue = clips[i].fps;
            element.FindPropertyRelative("loop").boolValue = true;
            var frames = element.FindPropertyRelative("frames");
            frames.arraySize = clips[i].frames.Length;
            for (int f = 0; f < clips[i].frames.Length; f++)
                frames.GetArrayElementAtIndex(f).objectReferenceValue = clips[i].frames[f];
        }
        animSo.ApplyModifiedPropertiesWithoutUndo();

        var bubble = CreateSprite("WorkBubble", go.transform, LoadPropArt("UI_Bubble_Hammer"), position, false);
        bubble.transform.localPosition = new Vector3(0.55f, 1.55f, 0f);
        bubble.transform.localScale = Vector3.one / 1.1f;

        var controller = go.AddComponent<BuilderOtterController>();
        Set(controller, "_workBubble", bubble);
        return controller;
    }

    // grid[행][열] (행은 위에서부터)
    private static Sprite[][] SliceGrid(string path, int cellW, int cellH)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        int rows = texture.height / cellH;
        int cols = texture.width / cellW;
        var grid = new Sprite[rows][];
        for (int r = 0; r < rows; r++)
            grid[r] = new Sprite[cols];
        foreach (var sprite in sprites)
        {
            int col = Mathf.RoundToInt(sprite.rect.x / cellW);
            int row = Mathf.RoundToInt((texture.height - sprite.rect.yMax) / cellH);
            if (row >= 0 && row < rows && col >= 0 && col < cols)
                grid[row][col] = sprite;
        }
        for (int r = 0; r < rows; r++)
        {
            if (grid[r].Any(s => s == null))
                Debug.LogError($"[SettlementSetup] {path} {r}행이 다 잘려 있지 않습니다 (Tools/Mine 설정을 먼저 실행).");
        }
        return grid;
    }

    #endregion

    /// <summary>배치 모드 한 번에: 전역 UI(정착 데이터·화면 포함) + 광장 배치</summary>
    public static void Run()
    {
        GlobalUISetup.Run();
        PlacePlaza();
        PlaceMine();
        PlaceZoneAmbience();
    }
}
