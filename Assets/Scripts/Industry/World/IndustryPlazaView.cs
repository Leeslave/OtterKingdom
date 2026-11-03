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
        _buildings.Clear();
        _buildings.AddRange(FindObjectsByType<PlacedBuildingView>(FindObjectsSortMode.None));
        bool visible = IndustrySection.IsVisible(_manager);
        PlaceWarehouse(visible);

        var seen = new HashSet<string>();
        foreach (var recipe in _manager.IndustryConfig.Recipes)
        {
            if (recipe == null || !recipe.IsValid)
                continue;
            string id = recipe.Building.BuildingId;
            var view = visible ? FindBuilt(id) : null;
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
            site.Bench = CreateRenderer("Bench_" + recipe.Building.BuildingId);
        site.Bench.gameObject.SetActive(learned && recipe.BenchSprite != null);
        site.Bench.sprite = recipe.BenchSprite;
        PlaceOnGround(site.Bench, new Vector2(rect.xMin + rect.width * 0.16f, rect.yMin - 0.08f), rect.width * BenchWidth);

        var processor = _manager.ProcessorOf(recipe.Building);
        var pile = learned ? recipe.PileSprite(processor.tray, recipe.ToRecipe().TrayCapacity) : null;
        if (site.Pile == null)
            site.Pile = CreateRenderer("Pile_" + recipe.Building.BuildingId);
        bool grew = pile != null && site.Pile.sprite != pile && site.Pile.gameObject.activeSelf;
        site.Pile.gameObject.SetActive(pile != null);
        site.Pile.sprite = pile;
        PlaceOnGround(site.Pile, new Vector2(rect.xMax - rect.width * 0.16f, rect.yMin - 0.12f), rect.width * PileWidth);
        if (grew)
            Fx().Puff(site.Pile.bounds.center, 0.5f, Vector2.up * 0.2f);

        UpdateCartAssignment(site);
    }

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
        var building = site.Recipe.Building;
        int before = _manager.ProcessorOf(building).tray;
        int moved = _manager.CollectProcessed(building);
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
        bool wants = _hasWarehouse && _manager.HasCartRoute(building) && TechGate.Has(site.Recipe.TechId);
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
            var renderer = CreateRenderer("Cart_" + building.BuildingId);
            renderer.sprite = _manager.IndustryConfig.EmptyCartSprite;
            site.Cart = new Cart { OtterId = workers[i], Agent = agent, Renderer = renderer };
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
        var stand = new Vector2(rect.xMax - rect.width * 0.16f - CartGap, rect.yMin - 0.55f);
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
    }

    #endregion
}
