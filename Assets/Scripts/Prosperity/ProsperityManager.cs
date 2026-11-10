using System;
using UnityEngine;

/// <summary>ProfileManager와 함께 씬을 넘어 유지. GameManager가 한 번 로드하고 매번 저장한다.</summary>
public sealed partial class ProsperityManager : MonoBehaviour
{
    public static ProsperityManager Instance { get; private set; }
    public static event Action SaveRequested;
    public ProsperityResearch Research { get; private set; }
    private ProfileManager _profile;
    private Func<string, bool> _provider;
    private ProsperityScreen _screen;
    private SettlementManager _settlement;
    private CommunityProjectDefinition _plan;
    private InventoryManager _inventory;
    private Inventory _bag;
    private bool _syncing;
    private bool _reportingActivity;
    private bool _savePending;

    public CommunityProjectDefinition Plan => _plan;
    public SettlementManager SettlementOwner => _settlement;

    public static void EnsureLoaded(ProsperitySaveData saved, ProfileManager profile)
    {
        if (Instance != null || profile == null) return;
        var manager = profile.gameObject.AddComponent<ProsperityManager>();
        manager.Initialize(saved, profile);
    }

    private void Initialize(ProsperitySaveData saved, ProfileManager profile)
    {
        Instance = this;
        _profile = profile;
        Research = new ProsperityResearch(saved ?? new ProsperitySaveData());
        _provider = Research.HasTech;
        TechGate.Provider = _provider;
        // 로드 중에는 전체 진행 데이터가 준비되기 전 저장 요청을 보내지 않는다.
        Research.SyncLevel(_profile.Level);
        EnsureBindings();
        Research.Learned += TechGate.NotifyLearned;
        Research.Changed += HandleResearchChanged;
        _profile.OnLevelUp += HandleLevelUp;
    }

    private static void RequestSave() => SaveRequested?.Invoke();
    private void HandleLevelUp(int level) => Research.SyncLevel(level);

    private void Update()
    {
        EnsureBindings();
        TickOnboarding();
    }

    private void LateUpdate()
    {
        if (!_savePending) return;
        _savePending = false;
        RequestSave();
    }

    private void EnsureBindings()
    {
        if (Research == null) return;
        var inventory = InventoryManager.Instance;
        if (inventory != null && inventory.Inventory != _bag)
        {
            if (_bag != null) _bag.OnItemChanged -= HandleItemChanged;
            if (_inventory != null) _inventory.OnItemSold -= HandleSold;
            _inventory = inventory;
            _bag = inventory.Inventory;
            _bag.OnItemChanged += HandleItemChanged;
            _inventory.OnItemSold += HandleSold;
        }
        var settlement = SettlementManager.Instance;
        if (settlement == null || !settlement.IsLoaded) return;
        if (_settlement != settlement)
        {
            if (_settlement != null) _settlement.OnProjectCompleted -= HandleProjectCompleted;
            _settlement = settlement;
            _settlement.OnProjectCompleted += HandleProjectCompleted;
        }
        if (_plan == null && inventory != null && inventory.Config != null && inventory.Config.ItemDatabase != null
            && inventory.Config.ItemDatabase.TryGet("material_wood", out var wood)
            && inventory.Config.ItemDatabase.TryGet("ore_stone", out var stone))
        {
            _plan = ScriptableObject.CreateInstance<CommunityProjectDefinition>();
            _plan.name = "번영 계획 수립";
            _plan.Setup(ProsperityResearch.PlanProjectId, 0, "번영 계획 수립", 20, ProsperityResearch.EraDevelopment,
                0, new[] { new ItemAmount(wood, 12), new ItemAmount(stone, 12) },
                new[] { new ProjectStageDefinition("confirm_plan", ProjectActionKind.ConfirmPlan, "번영 계획 확정")
                    .With(hint: "준비한 재료를 모아 왕국의 번영 계획을 함께 확정해요.") },
                ProsperityResearch.PlanDevelopment, 0);
            _plan.SetupText("또박이: 왕국의 다음 걸음을 함께 정해요.", "연구 포인트 3 RP를 받아요",
                "번영 계획을 세웠어요! 연구 포인트 3 RP를 받았어요.", null, null, null);
        }
        if (_plan != null && _settlement.ProsperityPlanProject != _plan) _settlement.RegisterProsperityPlan(_plan);
        SyncSettlement();
    }

    private void SyncSettlement()
    {
        if (_syncing || _settlement == null || !_settlement.IsLoaded || !Research.Data.introCompleted) return;
        _syncing = true;
        try
        {
            _settlement.BeginProsperity();
            if (_plan != null) Research.SyncPlanReward(CommunityProjectRules.IsCompleted(_plan, _settlement.Settlement));
        }
        finally { _syncing = false; }
    }

    private void HandleResearchChanged()
    {
        if (_syncing) { _savePending = true; return; }
        SyncSettlement();
        if (_reportingActivity) _savePending = true;
        else RequestSave();
    }

    private void HandleProjectCompleted(CommunityProjectDefinition project, int xp)
    {
        if (project.ProjectId == ProsperityResearch.PlanProjectId) SyncSettlement();
    }

    private void HandleItemChanged(ItemChangedEvent change)
    {
        if (change.Reason != ItemChangeReason.Harvest || change.Delta <= 0) return;
        ReportActivity(ProsperityResearch.HarvestAdvance, change.Delta);
    }

    private void HandleSold(ItemSoldEvent sale) => ReportActivity(ProsperityResearch.SalesAdvance, sale.TotalPrice);

    private void ReportActivity(string id, int amount)
    {
        // 가방·수확 처리가 끝난 프레임 말에 저장해 생산 도중 상태를 저장하지 않는다.
        _reportingActivity = true;
        try { Research.ReportAdvance(id, amount); }
        finally { _reportingActivity = false; }
    }

    public void OpenPlan()
    {
        EnsureBindings();
        if (_plan != null && _settlement != null && Research.Data.introCompleted) _settlement.RequestProject(_plan);
    }

    public void Open()
    {
        EnsureBindings();
        if (_screen == null) _screen = gameObject.AddComponent<ProsperityScreen>();
        Research.SyncLevel(_profile.Level);
        _screen.Open(this);
    }

    private void OnDestroy()
    {
        if (_profile != null) _profile.OnLevelUp -= HandleLevelUp;
        if (Research != null)
        {
            Research.Learned -= TechGate.NotifyLearned;
            Research.Changed -= HandleResearchChanged;
        }
        if (_bag != null) _bag.OnItemChanged -= HandleItemChanged;
        if (_inventory != null) _inventory.OnItemSold -= HandleSold;
        if (_settlement != null) _settlement.OnProjectCompleted -= HandleProjectCompleted;
        if (_plan != null) Destroy(_plan);
        if (TechGate.Provider == _provider) TechGate.Provider = null;
        if (Instance == this) Instance = null;
    }
}
