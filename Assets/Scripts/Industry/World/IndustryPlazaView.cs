using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 번영의 시대 광장 (개발자 A, 기획서 6.3 · 7.2 · 9):
/// - 가공을 배운 시설 앞 작업대 (제재 작업대 · 돌 재단대)
/// - 시설에 쌓인 가공품 더미 — 누르면 창고로 ("+3 판재")
/// - 왕국 창고 자리 (상자 더미: 농업 창고 앞, 없으면 게시판 옆)
/// - 수레: 그 시설에서 일하는 해달 하나가 수레를 밀고 시설 ↔ 창고 자리를 오감.
///   연출일 뿐 장부는 SettlementManager가 2초마다 옮김 (길이 막혀도 자재는 그대로)
/// - 건물 누르기: 작은 집 → 주택 개량 창, 목공소 · 채석 작업소 → 일터 창 가공 탭
/// - 왕국 공사: 돌길 · 큰길(지도 위 그림) · 왕립 산업단지(가공 시설 깃발) · 학술원(기록관 그림, PlacedBuildingView) ·
///   수도 광장(중앙 광장 둘레 깃대) · 왕궁(성 그림). 이번에 마쳤으면 창을 닫은 뒤 카메라 · 먼지와 함께 나타남
/// - 마을 주택 (주택 개량 2단계): 집 앞 현관 등불 · 꽃 화분
/// SettlementPlazaView가 붙인다
/// </summary>
public class IndustryPlazaView : MonoBehaviour
{
    private const float ScanSeconds = 0.5f;
    private const float BenchWidth = 0.42f;   // 시설 폭 대비
    private const float PileWidth = 0.32f;
    private const float CartWidth = 1.9f;     // 월드 단위 (광장 해달 키 ≈ 1.8)
    private const float CrateWidth = 2.6f;
    private const float CartGap = 1.05f;      // 해달 발 → 수레 가운데
    private const float PorchLampWidth = 0.13f;   // 집 폭 대비
    private const float PorchFlowersWidth = 0.12f;
    private const float LoadSeconds = 1.2f;
    private const float WalkLimitSeconds = 25f;
    private static readonly Color GoodText = new Color(0.98f, 0.86f, 0.45f);
    private static readonly Color WarnText = new Color(1f, 0.55f, 0.5f);

    private static readonly HashSet<string> Carters = new HashSet<string>();

    /// <summary>수레를 미는 해달 (SettlementPlazaView가 이 해달에게는 일터 일을 주지 않음)</summary>
    public static bool IsCarter(string otterId) => otterId != null && Carters.Contains(otterId);

    private enum CartPhase { ToSource, Loading, ToWarehouse, Unloading }

    private class Cart
    {
        public string OtterId;
        public OtterWanderAgent Agent;
        public SpriteRenderer Renderer;
        public SpriteRenderer Pennant;
        public CartPhase Phase;
        public float Timer;
        public int Load;
        public float Facing = 1f;
    }

    private class Site
    {
        public ProcessingRecipeDefinition Recipe;
        public PlacedBuildingView View;
        public SpriteRenderer Bench;
        public SpriteRenderer Pile;
        public Cart Cart;
    }

    private SettlementPlazaView _plaza;
    private SettlementManager _manager;
    private PlazaNodeFx _fx;
    private readonly Dictionary<string, Site> _sites = new Dictionary<string, Site>();
    private readonly List<PlacedBuildingView> _buildings = new List<PlacedBuildingView>();
    // 수레가 창고로 옮긴 몫 (가공품 ID → 개수). 다음에 싣는 수레가 그만큼 싣고 감
    private readonly Dictionary<string, int> _delivered = new Dictionary<string, int>();
    private SpriteRenderer _crates;
    private Vector2 _warehouseFoot;
    private bool _hasWarehouse;
    private float _scan;
    private HouseUpgradeSheet _houseSheet;
    // 마을 주택 현관 (놓인 건물 번호 → 등불 · 화분)
    private readonly Dictionary<int, (SpriteRenderer lamp, SpriteRenderer flowers)> _porches = new Dictionary<int, (SpriteRenderer, SpriteRenderer)>();
    // 광장 돌길
    private SpriteRenderer _map;
    // 왕국 공사 (공사 ID → 광장에 놓은 그림들)
    private readonly Dictionary<string, List<SpriteRenderer>> _projects = new Dictionary<string, List<SpriteRenderer>>();
    private bool _projectsShown;

    public void Init(SettlementPlazaView plaza) => _plaza = plaza;

    private void OnEnable() => SettlementManager.WarehouseChanged += HandleWarehouseChanged;

    private void OnDisable()
    {
        SettlementManager.WarehouseChanged -= HandleWarehouseChanged;
        foreach (var site in _sites.Values)
            ReleaseCart(site);
    }

    private void HandleWarehouseChanged(string itemId, int delta, string reason)
    {
        if (reason != "cart" || delta <= 0)
            return;
        _delivered.TryGetValue(itemId, out int sum);
        _delivered[itemId] = sum + delta;
    }

    private void Update()
    {
        _manager = SettlementManager.Instance;
        if (_plaza == null || _manager == null || !_manager.IsLoaded || _manager.IndustryConfig == null)
            return;
        _scan -= Time.deltaTime;
        if (_scan <= 0f)
        {
            _scan = ScanSeconds;
            Scan();
        }
        HandleTap();
        foreach (var site in _sites.Values)
        {
            if (site.Cart != null)
                UpdateCart(site);
        }
    }

    private void LateUpdate()
    {
        foreach (var site in _sites.Values)
        {
            if (site.Cart != null)
                FollowAgent(site.Cart);
        }
    }

    #region 찾기 · 놓기

    private void Scan()
    {
        ScanProjects();
        _buildings.Clear();
        _buildings.AddRange(FindObjectsByType<PlacedBuildingView>(FindObjectsSortMode.None));
        bool visible = IndustrySection.IsVisible(_manager);
        PlaceWarehouse(visible);

        var seen = new HashSet<string>();
        foreach (var recipe in _manager.IndustryConfig.Recipes)
        {
            if (recipe == null || !recipe.IsValid)
                continue;
            string id = recipe.ProcessorId;
            var view = visible ? FindBuilt(recipe.Building.BuildingId) : null;
            if (view == null)
                continue;
            seen.Add(id);
            if (!_sites.TryGetValue(id, out var site))
                _sites[id] = site = new Site { Recipe = recipe };
            site.View = view;
            PlaceSite(site);
        }
        foreach (var id in new List<string>(_sites.Keys))
        {
            if (seen.Contains(id))
                continue;
            RemoveSite(_sites[id]);
            _sites.Remove(id);
        }
        PlacePorches();
    }

    // 마을 주택: 현관 왼쪽 등불 · 오른쪽 화분 (집을 옮기면 따라감, 없어지면 치움)
    private void PlacePorches()
    {
        var config = _manager.IndustryConfig;
        var seen = new HashSet<int>();
        foreach (var view in _buildings)
        {
            if (view == null || view.Placed == null || !_manager.IsTownHouse(view.Placed.InstanceId))
                continue;
            int id = view.Placed.InstanceId;
            seen.Add(id);
            if (!_porches.TryGetValue(id, out var porch))
            {
                porch = (CreateRenderer("PorchLamp_" + id), CreateRenderer("PorchFlowers_" + id));
                porch.lamp.sprite = config.PorchLamp;
                porch.flowers.sprite = config.PorchFlowers;
                _porches[id] = porch;
            }
            var rect = view.WorldRect;
            PlaceOnGround(porch.lamp, new Vector2(rect.xMin + rect.width * 0.12f, rect.yMin - 0.06f), rect.width * PorchLampWidth);
            PlaceOnGround(porch.flowers, new Vector2(rect.xMax - rect.width * 0.14f, rect.yMin - 0.1f), rect.width * PorchFlowersWidth);
        }
        foreach (var id in new List<int>(_porches.Keys))
        {
            if (seen.Contains(id))
                continue;
            var porch = _porches[id];
            if (porch.lamp != null)
                Destroy(porch.lamp.gameObject);
            if (porch.flowers != null)
                Destroy(porch.flowers.gameObject);
            _porches.Remove(id);
        }
    }

    private PlacedBuildingView FindBuilt(string buildingId)
    {
        foreach (var view in _buildings)
        {
            if (view == null || view.Building == null || view.Building.BuildingId != buildingId)
                continue;
            var record = _manager.BuildingRecordOf(view.Placed.InstanceId);
            if (record == null || record.Built)
                return view;
        }
        return null;
    }

    // 왕국 창고 자리: 농업 창고 앞 (없으면 게시판 옆)
    private void PlaceWarehouse(bool visible)
    {
        _hasWarehouse = false;
        var sprite = _manager.IndustryConfig.WarehouseSprite;
        if (visible && sprite != null)
        {
            var granary = FindBuilt("bld_granary");
            if (granary != null)
            {
                var rect = granary.WorldRect;
                _warehouseFoot = new Vector2(rect.xMax - CrateWidth * 0.3f, rect.yMin - 0.35f);
                _hasWarehouse = true;
            }
            else
            {
                var board = FindAnyObjectByType<SettlementBoardPropView>();
                if (board != null)
                {
                    _warehouseFoot = (Vector2)board.transform.position + new Vector2(1.7f, -0.3f);
                    _hasWarehouse = true;
                }
            }
        }
        if (!_hasWarehouse)
        {
            if (_crates != null)
                _crates.gameObject.SetActive(false);
            return;
        }
        if (_crates == null)
            _crates = CreateRenderer("WarehouseCrates");
        _crates.gameObject.SetActive(true);
        _crates.sprite = sprite;
        PlaceOnGround(_crates, _warehouseFoot, CrateWidth);
    }

    private void PlaceSite(Site site)
    {
        var rect = site.View.WorldRect;
        var recipe = site.Recipe;
        bool learned = TechGate.Has(recipe.TechId);

        if (site.Bench == null)
            site.Bench = CreateRenderer("Bench_" + recipe.ProcessorId);
        site.Bench.gameObject.SetActive(learned && recipe.BenchSprite != null);
        site.Bench.sprite = recipe.BenchSprite;
        PlaceOnGround(site.Bench, new Vector2(rect.xMin + rect.width * 0.16f, rect.yMin - 0.08f), rect.width * BenchWidth);

        var processor = _manager.ProcessorOf(recipe);
        var pile = learned ? recipe.PileSprite(processor.tray, recipe.ToRecipe().TrayCapacity) : null;
        if (site.Pile == null)
            site.Pile = CreateRenderer("Pile_" + recipe.ProcessorId);
        bool grew = pile != null && site.Pile.sprite != pile && site.Pile.gameObject.activeSelf;
        site.Pile.gameObject.SetActive(pile != null);
        site.Pile.sprite = pile;
        PlaceOnGround(site.Pile, PileFoot(recipe, rect), rect.width * PileWidth);
        if (grew && !IndustryFx.Reduced)
            Fx().Puff(site.Pile.bounds.center, 0.5f, Vector2.up * 0.2f);

        UpdateCartAssignment(site);
    }

    // 소품 자리: 첫 줄은 오른쪽 앞, 둘째 줄(정밀 공방)은 가운데 앞
    private static Vector2 PileFoot(ProcessingRecipeDefinition recipe, Rect rect) => recipe.PropSlot == 1
        ? new Vector2(rect.center.x, rect.yMin - 0.22f)
        : new Vector2(rect.xMax - rect.width * 0.16f, rect.yMin - 0.12f);

    private void RemoveSite(Site site)
    {
        ReleaseCart(site);
        if (site.Bench != null)
            Destroy(site.Bench.gameObject);
        if (site.Pile != null)
            Destroy(site.Pile.gameObject);
    }

    private SpriteRenderer CreateRenderer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.AddComponent<SpriteRenderer>();
    }

    // 발(아랫변 가운데)을 foot에, 가로를 width에 맞춤
    private static void PlaceOnGround(SpriteRenderer renderer, Vector2 foot, float width)
    {
        var sprite = renderer.sprite;
        if (sprite == null)
            return;
        float scale = width / Mathf.Max(0.01f, sprite.bounds.size.x);
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
        renderer.transform.position = new Vector3(foot.x - sprite.bounds.center.x * scale, foot.y - sprite.bounds.min.y * scale, 0f);
        renderer.sortingOrder = PlazaDepth.SortingOrderFor(foot.y);
    }

    private PlazaNodeFx Fx()
    {
        if (_fx == null)
            _fx = FindAnyObjectByType<PlazaNodeFx>();
        return _fx;
    }

    #endregion

    #region 왕국 공사

    private const float PavingRevealSeconds = 1.8f;
    private const int PavingOrderAboveMap = 2;
    private const float ParkBannerWidth = 0.16f;   // 시설 폭 대비
    private const float CapitalFlagWidth = 0.9f;   // 월드 단위
    // 수도 광장 깃대: 중앙 광장 그림 가운데에서 (각도 °, 반지름 = 그림 폭 비율) — 북쪽 성 자리는 비움
    private static readonly (float angle, float radius)[] CapitalFlags =
    {
        (-20f, 0.3f), (20f, 0.3f), (160f, 0.3f), (200f, 0.3f), (240f, 0.27f), (300f, 0.27f),
    };

    // 마친 공사를 광장에 (처음 열 때 이미 마친 것은 바로, 이번에 마친 것은 창이 닫힌 뒤 연출과 함께)
    private void ScanProjects()
    {
        var projects = _manager.IndustryConfig.Projects;
        if (projects.Count == 0)
            return;
        if (_map == null)
        {
            var mapObject = GameObject.Find("WorldMap");
            _map = mapObject != null ? mapObject.GetComponent<SpriteRenderer>() : null;
            if (_map == null || _map.sprite == null)
                return;
        }
        foreach (var project in projects)
        {
            if (project == null || _projects.ContainsKey(project.Id) || !_manager.IsProjectDone(project))
                continue;
            bool reveal = _projectsShown;
            if (reveal && RuntimeModalSurface.AnyVisible)
                continue;
            var renderers = CreateProject(project);
            if (renderers == null)
                continue;
            _projects[project.Id] = renderers;
            if (reveal)
                StartCoroutine(RevealProject(renderers, project));
        }
        _projectsShown = true;
        PlaceParkBanners();
    }

    // 공사 종류마다 광장에 놓는 것 (학술원은 PlacedBuildingView가 그림을 바꿈 → 여기서는 연출 자리만)
    private List<SpriteRenderer> CreateProject(TownProject project)
    {
        switch (project.Kind)
        {
            case TownProjectKind.Paving:
                return CreatePaving(project);
            case TownProjectKind.CapitalPlaza:
                return CreateCapitalFlags(project);
            case TownProjectKind.Palace:
                return SwapSceneSprite(project);
            case TownProjectKind.Academy:
                var academy = FindBuilt(project.Target);
                return academy != null ? new List<SpriteRenderer> { academy.GetComponentInChildren<SpriteRenderer>() } : null;
            default:
                // 산업단지: 깃발은 PlaceParkBanners가 시설을 따라 놓음
                return new List<SpriteRenderer>();
        }
    }

    // 돌길 · 큰길: 조각마다 지도 비율 자리에 (지도 Sprite 범위 기준, HD 조각과 같은 방식)
    private List<SpriteRenderer> CreatePaving(TownProject paving)
    {
        var renderers = new List<SpriteRenderer>();
        Vector2 total = _map.sprite.bounds.size;
        for (int i = 0; i < paving.Pieces.Count; i++)
        {
            var piece = paving.Pieces[i];
            if (piece == null || piece.Sprite == null)
                continue;
            var renderer = new GameObject($"Paving_{paving.Id}_{i}").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(_map.transform, false);
            renderer.sprite = piece.Sprite;
            renderer.sortingLayerID = _map.sortingLayerID;
            renderer.sortingOrder = _map.sortingOrder + PavingOrderAboveMap;
            renderer.sharedMaterial = _map.sharedMaterial;
            renderer.transform.localPosition = MapLocal(piece.MapCenter);
            Vector2 native = piece.Sprite.bounds.size;
            renderer.transform.localScale = new Vector3(piece.MapSize.x * total.x / native.x, piece.MapSize.y * total.y / native.y, 1f);
            renderers.Add(renderer);
        }
        return renderers;
    }

    private Vector3 MapLocal(Vector2 mapCenter)
    {
        Vector2 total = _map.sprite.bounds.size;
        return new Vector3(-total.x * 0.5f + mapCenter.x * total.x, total.y * 0.5f - mapCenter.y * total.y, 0f);
    }

    // 수도 광장: 중앙 광장(첫 돌길 공사의 첫 조각) 둘레에 깃대
    private List<SpriteRenderer> CreateCapitalFlags(TownProject project)
    {
        var center = _manager.IndustryConfig.FindProject(TownProjectKind.Paving);
        if (center == null || center.Pieces.Count == 0 || project.Sprite == null)
            return null;
        var piece = center.Pieces[0];
        Vector2 middle = _map.transform.TransformPoint(MapLocal(piece.MapCenter));
        float width = piece.MapSize.x * _map.sprite.bounds.size.x * _map.transform.lossyScale.x;
        var renderers = new List<SpriteRenderer>();
        foreach (var (angle, radius) in CapitalFlags)
        {
            float rad = angle * Mathf.Deg2Rad;
            var flag = CreateRenderer("CapitalFlag");
            flag.sprite = project.Sprite;
            PlaceOnGround(flag, middle + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad) * 0.8f) * width * radius, CapitalFlagWidth);
            renderers.Add(flag);
        }
        return renderers;
    }

    // 왕궁: 광장 씬의 성 그림을 바꿈
    private List<SpriteRenderer> SwapSceneSprite(TownProject project)
    {
        var target = string.IsNullOrEmpty(project.Target) ? null : GameObject.Find(project.Target);
        var renderer = target != null ? target.GetComponent<SpriteRenderer>() : null;
        if (renderer == null || project.Sprite == null || !renderer.gameObject.activeInHierarchy)
            return null;
        renderer.sprite = project.Sprite;
        return new List<SpriteRenderer> { renderer };
    }

    // 왕립 산업단지: 가공 시설마다 왼쪽 앞에 왕실 깃발 (시설을 옮기면 따라감)
    private readonly Dictionary<string, SpriteRenderer> _parkBanners = new Dictionary<string, SpriteRenderer>();

    private void PlaceParkBanners()
    {
        var park = _manager.IndustryConfig.FindProject(TownProjectKind.IndustrialPark);
        bool show = park != null && park.Sprite != null && _projects.ContainsKey(park.Id);
        var seen = new HashSet<string>();
        if (show)
        {
            foreach (var site in _sites.Values)
            {
                string buildingId = site.Recipe.Building.BuildingId;
                if (site.View == null || !seen.Add(buildingId))
                    continue;
                if (!_parkBanners.TryGetValue(buildingId, out var banner))
                    _parkBanners[buildingId] = banner = CreateRenderer("ParkBanner_" + buildingId);
                banner.sprite = park.Sprite;
                var rect = site.View.WorldRect;
                PlaceOnGround(banner, new Vector2(rect.xMin + rect.width * 0.04f, rect.yMin + rect.height * 0.25f), rect.width * ParkBannerWidth);
            }
        }
        foreach (var id in new List<string>(_parkBanners.Keys))
        {
            if (seen.Contains(id))
                continue;
            Destroy(_parkBanners[id].gameObject);
            _parkBanners.Remove(id);
        }
    }

    private System.Collections.IEnumerator RevealProject(List<SpriteRenderer> renderers, TownProject project)
    {
        PlaceParkBanners();
        var shown = new List<SpriteRenderer>(renderers);
        if (project.Kind == TownProjectKind.IndustrialPark)
            shown.AddRange(_parkBanners.Values);
        if (shown.Count == 0)
            yield break;
        var focus = shown[0].bounds.center;
        var camera = FindAnyObjectByType<PlazaCameraController>();
        if (camera != null)
            camera.Celebrate(focus, 1f, PavingRevealSeconds + 1.2f);
        // 돌길 · 깃발은 서서히, 그림이 바뀌는 학술원 · 왕궁은 통 튀어오르듯
        bool fade = project.Kind == TownProjectKind.Paving || project.Kind == TownProjectKind.CapitalPlaza || project.Kind == TownProjectKind.IndustrialPark;
        var size = shown[0].bounds.size;
        if (!IndustryFx.Reduced)
        {
            for (int i = 0; i < 12; i++)
            {
                var at = focus + new Vector3(Random.Range(-0.3f, 0.3f) * size.x, Random.Range(-0.3f, 0.3f) * size.y, 0f);
                Fx().Puff(at, 1.4f, Vector2.up * 0.2f);
            }
        }
        var scales = new List<Vector3>();
        foreach (var renderer in shown)
        {
            scales.Add(renderer.transform.localScale);
            if (fade)
                renderer.color = new Color(1f, 1f, 1f, 0f);
        }
        for (float t = 0f; t < PavingRevealSeconds; t += Time.deltaTime)
        {
            float k = t / PavingRevealSeconds;
            for (int i = 0; i < shown.Count; i++)
            {
                var renderer = shown[i];
                if (renderer == null)
                    continue;
                if (fade)
                    renderer.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, k));
                else
                    renderer.transform.localScale = scales[i] * (1f + Mathf.Sin(Mathf.Clamp01(k * 2f) * Mathf.PI) * 0.12f);
            }
            yield return null;
        }
        for (int i = 0; i < shown.Count; i++)
        {
            if (shown[i] == null)
                continue;
            shown[i].color = Color.white;
            shown[i].transform.localScale = scales[i];
        }
        Fx().ShowText($"{project.DisplayName} 완성!", focus + Vector3.up * 1.5f, GoodText);
    }

    #endregion

    #region 누르기

    private void HandleTap()
    {
        if (DecorModePresenter.IsActive || RuntimeModalSurface.AnyVisible || !PlazaTapInput.TryGetTap(out Vector2 world))
            return;
        // 더미 (작으니 먼저)
        foreach (var site in _sites.Values)
        {
            if (site.Pile == null || !site.Pile.gameObject.activeSelf)
                continue;
            var bounds = site.Pile.bounds;
            bounds.Expand(0.3f);
            if (bounds.Contains(new Vector3(world.x, world.y, bounds.center.z)))
            {
                Collect(site);
                return;
            }
        }
        if (OtterAt(world))
            return;
        foreach (var view in _buildings)
        {
            if (view == null || !Contains(view, world))
                continue;
            if (_manager.IsUpgradableHouse(view.Placed) && (TechGate.Has(TechIds.BetterHouses) || _manager.IsImprovedHouse(view.Placed.InstanceId)))
            {
                PlazaTapInput.BlockCurrentGesture();
                OpenHouse(view);
                return;
            }
            if (_manager.RecipeFor(view.Building) != null && IndustrySection.IsVisible(_manager))
            {
                PlazaTapInput.BlockCurrentGesture();
                _manager.OpenProduction(ProductionSheet.Tab.Processing);
                return;
            }
        }
    }

    // 건물 그림 (지붕까지)
    private static bool Contains(PlacedBuildingView view, Vector2 world)
    {
        var body = view.GetComponentInChildren<SpriteRenderer>();
        if (body == null)
            return view.WorldRect.Contains(world);
        var bounds = body.bounds;
        return world.x >= bounds.min.x && world.x <= bounds.max.x && world.y >= bounds.min.y && world.y <= bounds.max.y;
    }

    // 해달을 누른 거면 건물은 열지 않음 (해달 창이 열림)
    private bool OtterAt(Vector2 world)
    {
        foreach (var pair in _plaza.SpawnedOtters)
        {
            var agent = pair.Value;
            if (agent == null || !agent.isActiveAndEnabled)
                continue;
            Vector2 delta = world - agent.Position;
            if (Mathf.Abs(delta.x) < 0.45f && delta.y > -0.15f && delta.y < 1.3f)
                return true;
        }
        return false;
    }

    private void Collect(Site site)
    {
        PlazaTapInput.BlockCurrentGesture();
        int before = _manager.ProcessorOf(site.Recipe).tray;
        int moved = _manager.CollectProcessed(site.Recipe);
        Vector3 at = site.Pile.bounds.center + Vector3.up * 0.3f;
        if (moved > 0)
        {
            Fx().ShowText($"+{moved} {site.Recipe.Output.DisplayName}", at, GoodText);
            Fx().Puff(site.Pile.bounds.center, 0.6f, Vector2.up * 0.3f);
        }
        else if (before > 0)
        {
            Fx().ShowText("창고가 꽉 찼어요", at, WarnText);
        }
        Scan();
    }

    private void OpenHouse(PlacedBuildingView view)
    {
        if (_houseSheet == null)
            _houseSheet = gameObject.AddComponent<HouseUpgradeSheet>();
        _houseSheet.Open(_manager, view, () =>
        {
            if (view == null)
                return;
            var body = view.GetComponentInChildren<SpriteRenderer>();
            var center = body != null ? body.bounds.center : view.transform.position;
            Fx().Puff(center, 1.2f, Vector2.up * 0.3f);
            Fx().ShowText("개량 완료!", center + Vector3.up * 0.8f, GoodText);
        });
    }

    #endregion

    #region 수레

    private void UpdateCartAssignment(Site site)
    {
        var building = site.Recipe.Building;
        bool wants = _hasWarehouse && !IndustryFx.Reduced && _manager.HasCartRoute(site.Recipe) && TechGate.Has(site.Recipe.TechId);
        var workers = wants ? ProductionRules.WorkerIds(_manager.Config, _manager.Settlement, building) : null;
        if (site.Cart != null)
        {
            bool stillWorks = wants && workers.Contains(site.Cart.OtterId) && site.Cart.Agent != null && site.Cart.Agent.isActiveAndEnabled;
            if (!stillWorks)
                ReleaseCart(site);
            return;
        }
        if (!wants)
            return;
        // 일하는 해달 중 마지막 (첫째는 일터에 남음)
        for (int i = workers.Count - 1; i >= 0; i--)
        {
            var agent = _plaza.FindAgent(workers[i]);
            if (agent == null || !agent.isActiveAndEnabled || IsCarter(workers[i]))
                continue;
            Carters.Add(workers[i]);
            var renderer = CreateRenderer("Cart_" + site.Recipe.ProcessorId);
            renderer.sprite = _manager.IndustryConfig.EmptyCartSprite;
            site.Cart = new Cart { OtterId = workers[i], Agent = agent, Renderer = renderer };
            // 개량 수레 (L06): 손잡이 쪽에 깃발
            if (TechGate.Has(TechIds.BetterCart) && _manager.IndustryConfig.CartPennant != null)
            {
                site.Cart.Pennant = CreateRenderer("CartPennant");
                site.Cart.Pennant.transform.SetParent(renderer.transform, false);
                site.Cart.Pennant.sprite = _manager.IndustryConfig.CartPennant;
            }
            GoToSource(site);
            return;
        }
    }

    private void ReleaseCart(Site site)
    {
        var cart = site.Cart;
        if (cart == null)
            return;
        site.Cart = null;
        Carters.Remove(cart.OtterId);
        if (cart.Agent != null)
            cart.Agent.ClearTask();
        if (cart.Renderer != null)
            Destroy(cart.Renderer.gameObject);
    }

    private void GoToSource(Site site)
    {
        var rect = site.View.WorldRect;
        site.Cart.Phase = CartPhase.ToSource;
        site.Cart.Timer = WalkLimitSeconds;
        var foot = PileFoot(site.Recipe, rect);
        var stand = new Vector2(foot.x - CartGap, rect.yMin - 0.55f);
        site.Cart.Agent.AssignTask(stand, stand + Vector2.right);
    }

    private void GoToWarehouse(Site site)
    {
        site.Cart.Phase = CartPhase.ToWarehouse;
        site.Cart.Timer = WalkLimitSeconds;
        var stand = _warehouseFoot + new Vector2(-CrateWidth * 0.55f - CartGap, -0.35f);
        site.Cart.Agent.AssignTask(stand, stand + Vector2.right);
    }

    private void UpdateCart(Site site)
    {
        var cart = site.Cart;
        if (cart.Agent == null)
        {
            ReleaseCart(site);
            return;
        }
        cart.Timer -= Time.deltaTime;
        switch (cart.Phase)
        {
            case CartPhase.ToSource:
            case CartPhase.ToWarehouse:
                // 길이 막혀 너무 오래 걸리면 그 자리로 옮김 (연출이 장부를 막지 않게)
                if (cart.Timer <= 0f && !cart.Agent.IsOnTask)
                    cart.Agent.WarpToTask();
                if (!cart.Agent.IsOnTask)
                    break;
                cart.Phase = cart.Phase == CartPhase.ToSource ? CartPhase.Loading : CartPhase.Unloading;
                cart.Timer = LoadSeconds;
                if (cart.Phase == CartPhase.Unloading)
                    Unload(site);
                break;

            case CartPhase.Loading:
                if (cart.Timer > 0f)
                    break;
                string output = site.Recipe.Output.ItemId;
                _delivered.TryGetValue(output, out cart.Load);
                _delivered[output] = 0;
                cart.Renderer.sprite = cart.Load > 0 && site.Recipe.CartSprite != null ? site.Recipe.CartSprite : _manager.IndustryConfig.EmptyCartSprite;
                GoToWarehouse(site);
                break;

            case CartPhase.Unloading:
                if (cart.Timer <= 0f)
                    GoToSource(site);
                break;
        }
    }

    private void Unload(Site site)
    {
        var cart = site.Cart;
        if (cart.Load > 0 && _crates != null)
        {
            Fx().ShowText($"+{cart.Load} {site.Recipe.Output.DisplayName}", _crates.bounds.center + Vector3.up * 0.6f, GoodText);
            Fx().Puff(_crates.bounds.center, 0.7f, Vector2.up * 0.2f);
        }
        cart.Load = 0;
        cart.Renderer.sprite = _manager.IndustryConfig.EmptyCartSprite;
    }

    // 수레는 해달 앞 (가는 쪽), 손잡이가 해달 쪽
    private void FollowAgent(Cart cart)
    {
        if (cart.Agent == null || cart.Renderer == null || cart.Renderer.sprite == null)
            return;
        bool walking = cart.Agent.CurrentState == OtterWanderAgent.State.Walk;
        if (walking && Mathf.Abs(cart.Agent.MoveDirection.x) > 0.15f)
            cart.Facing = Mathf.Sign(cart.Agent.MoveDirection.x);
        else if (!walking)
            cart.Facing = Mathf.Sign(cart.Agent.LookTarget.x - cart.Agent.Position.x + 0.0001f);
        Vector2 foot = cart.Agent.Position + new Vector2(cart.Facing * CartGap, -0.02f);
        cart.Renderer.flipX = cart.Facing < 0f;
        // 바퀴가 구르듯 살짝 들썩
        if (walking)
            foot.y += Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.03f;
        PlaceOnGround(cart.Renderer, foot, CartWidth);
        if (cart.Pennant != null)
        {
            // 수레 그림 안 좌표: 손잡이 쪽(뒤) 위. 수레 크기에 맞춰 둠
            var bounds = cart.Renderer.sprite.bounds;
            float side = cart.Facing < 0f ? 1f : -1f;
            cart.Pennant.flipX = cart.Facing < 0f;
            cart.Pennant.transform.localPosition = new Vector3(side * bounds.extents.x * 0.45f, bounds.max.y * 0.9f, 0f);
            cart.Pennant.transform.localScale = Vector3.one * 1.1f;
            cart.Pennant.sortingOrder = cart.Renderer.sortingOrder - 1;
        }
    }

    #endregion
}
