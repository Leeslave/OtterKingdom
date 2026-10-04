using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 공동사업·생활 의뢰가 재료를 내는 곳 (골드 잔액 · 가방). 기본은 CurrencyManager · InventoryManager, 규칙 테스트는 가짜를 끼운다
/// </summary>
public interface IProjectWallet
{
    int Gold { get; }
    int Count(ItemDefinition item);
    bool TrySpendGold(int amount);
    bool TryRemove(ItemDefinition item, int amount);
}

/// <summary>공동사업 단계를 진행하지 못한 이유</summary>
public enum ProjectActionResult
{
    Done,           // 했음 (건설 시작 · 입주 · 모임 열기 등)
    OpenTask,       // 주민 고르기 화면을 열었음 (정비)
    NotNow,         // 지금 할 단계가 아님 / 이미 함
    Busy,           // 건설 큐를 다른 공사가 쓰는 중 (납품은 그대로 보존)
    NeedChoice,     // 소품을 먼저 골라야 함
    NothingToGive,  // 넣을 수 있는 재료가 없음 (가진 것이 없거나 이미 다 냄)
}

/// <summary>
/// P3: 마을 공동사업 · 집과 입주 · 첫 마을 모임 · 생활 의뢰 · 요정 방문 순서.
/// - 사업 진행의 원본은 기존 기록 (건설 완료 부탁, 장애물 기록, 끝낸 작업, 집의 입주민, 모임 기록) — 여기서는 납품·보상만 따로 저장
/// - 납품: 재료 차감과 납품량 증가를 같은 프레임에 하고 한 번 저장 (중간에 꺼져도 어긋나지 않음). 남은 양과 가진 양 중 작은 만큼만
/// - 보상: 사업 ID#회차 기록과 경험치를 같은 프레임에 (다시 불러와도 두 번 주지 않고, 주다 만 것은 불러올 때 마저 줌)
/// - 요정: 농부 파견이 저장되면 방문 예약(농부의 파견 발전), 광장의 안전한 때에 SettlementPlazaView가 도착시킴
/// </summary>
public partial class SettlementManager
{
    /// <summary>모임 연출을 다 봤거나 건너뛴 기록 (다시 보기는 언제든)</summary>
    public const string GatheringViewedFlag = "gathering_viewed";

    private bool _projectsDirty;
    private int _seenLevel;
    private readonly List<ProjectMaterial> _materials = new List<ProjectMaterial>();
    private readonly List<(ItemDefinition item, int amount)> _deliverItems = new List<(ItemDefinition, int)>();
    private IProjectWallet _defaultWallet;

    /// <summary>재료를 내는 곳 (비우면 재화·가방 매니저). 테스트용</summary>
    public IProjectWallet WalletOverride { get; set; }

    private IProjectWallet Wallet => WalletOverride ?? (_defaultWallet ??= new ManagerWallet(this));

    // 골드 = 정착 설정의 골드, 가방 = InventoryManager
    private sealed class ManagerWallet : IProjectWallet
    {
        private readonly SettlementManager _owner;

        public ManagerWallet(SettlementManager owner) => _owner = owner;

        public int Gold => _owner.GoldBalance;

        public int Count(ItemDefinition item) => _owner.ItemCount(item);

        public bool TrySpendGold(int amount) =>
            amount <= 0 || CurrencyManager.Instance != null
            && CurrencyManager.Instance.TrySpend(_owner._config.GoldCurrency, amount, TransactionSource.CommunityProject);

        public bool TryRemove(ItemDefinition item, int amount) =>
            amount <= 0 || InventoryManager.Instance != null && InventoryManager.Instance.Inventory.TryRemove(item, amount, ItemChangeReason.Delivery);
    }
    private readonly List<BoardRequestRow> _lifeRows = new List<BoardRequestRow>();

    /// <summary>공동사업 화면을 열어 달라는 부탁 (회관 · 게시판 카드 · 안내 띠)</summary>
    public event Action<CommunityProjectDefinition> OnProjectRequested;

    /// <summary>공동사업을 끝냈을 때 (결과 발전·경험치를 준 뒤). 인자: 사업, 받은 경험치</summary>
    public event Action<CommunityProjectDefinition, int> OnProjectCompleted;

    /// <summary>[모임 열기]를 눌렀을 때 (광장이 연출을 보여 줌)</summary>
    public event Action<CommunityProjectDefinition> OnGatheringHeld;

    /// <summary>기념 기록에서 모임을 다시 보고 싶을 때</summary>
    public event Action OnGatheringReplayRequested;

    /// <summary>생활 의뢰 화면을 열어 달라는 부탁 (인자: 회차)</summary>
    public event Action<int> OnLifeRequestRequested;

    /// <summary>생활 의뢰를 끝냈을 때 (틀, 골드, 경험치)</summary>
    public event Action<LifeRequestTemplate, int, int> OnLifeRequestCompleted;

    /// <summary>이 해달이 입주했을 때</summary>
    public event Action<SettlementOtterDefinition> OnResidentMovedIn;

    /// <summary>지금 왕국 레벨 (레벨 매니저가 없으면 1)</summary>
    public int PlayerLevel => ProfileManager.Instance != null ? ProfileManager.Instance.Level : 1;

    #region 공동사업 — 조회

    /// <summary>지금 사업 (마을회관 전이면 null)</summary>
    public CommunityProjectDefinition ActiveProject => CommunityProjectRules.FindActive(_config, Settlement);

    public ProjectStatus GetProjectStatus(CommunityProjectDefinition project) =>
        CommunityProjectRules.GetStatus(project, Settlement, PlayerLevel);

    /// <summary>필요한 재료 (골드 먼저) + 넣은 양</summary>
    public void CollectProjectMaterials(CommunityProjectDefinition project, List<ProjectMaterial> result) =>
        CommunityProjectRules.CollectMaterials(project, Settlement, result);

    /// <summary>이 재료를 지금 가진 양 (골드는 잔액)</summary>
    public int OwnedAmount(ProjectMaterial material) => material.IsGold ? Wallet.Gold : Wallet.Count(material.Item);

    /// <summary>이 회차의 제목</summary>
    public string ProjectTitle(CommunityProjectDefinition project) => CommunityProjectRules.Title(project, Settlement);

    /// <summary>지금 단계 (재료를 넣는 중이거나 끝났으면 null)</summary>
    public ProjectStageDefinition CurrentStage(CommunityProjectDefinition project)
    {
        var status = GetProjectStatus(project);
        return status.Phase == ProjectPhase.Stage && status.StageIndex < project.Stages.Count ? project.Stages[status.StageIndex] : null;
    }

    /// <summary>공동사업 화면을 연다</summary>
    public void RequestProject(CommunityProjectDefinition project)
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        OnProjectRequested?.Invoke(project);
    }

    #endregion

    #region 공동사업 — 납품

    /// <summary>재료 한 가지를 넣을 수 있는 만큼 넣는다 (남은 양과 가진 양 중 작은 것)</summary>
    /// <returns>넣은 양 (없으면 0)</returns>
    public int TryDeliver(CommunityProjectDefinition project, int materialIndex)
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        if (GetProjectStatus(project).Phase != ProjectPhase.Delivering)
            return 0;
        CollectProjectMaterials(project, _materials);
        if (materialIndex < 0 || materialIndex >= _materials.Count)
            return 0;
        var material = _materials[materialIndex];
        int amount = material.Deliverable(OwnedAmount(material));
        if (amount <= 0)
            return 0;

        _deliverItems.Clear();
        int gold = 0;
        if (material.IsGold)
            gold = amount;
        else
            _deliverItems.Add((material.Item, amount));
        return Pay(project, gold, _deliverItems) ? amount : 0;
    }

    /// <summary>[가능한 만큼 넣기]에서 실제로 쓸 것 (골드 포함 여부를 보여 주려고). 넣을 것이 없으면 null</summary>
    public string DeliverAllPreview(CommunityProjectDefinition project)
    {
        if (GetProjectStatus(project).Phase != ProjectPhase.Delivering)
            return null;
        CollectProjectMaterials(project, _materials);
        var text = new StringBuilder();
        foreach (var material in _materials)
        {
            int amount = material.Deliverable(OwnedAmount(material));
            if (amount <= 0)
                continue;
            if (text.Length > 0)
                text.Append(" · ");
            text.Append(material.IsGold ? "골드" : material.Item.DisplayName).Append(' ').Append(amount.ToString("N0"));
        }
        return text.Length > 0 ? text.ToString() : null;
    }

    /// <summary>모든 재료를 넣을 수 있는 만큼 넣는다 (골드 포함). 한 번에 결제·기록·저장</summary>
    /// <returns>무엇이든 넣었으면 true</returns>
    public bool TryDeliverAll(CommunityProjectDefinition project)
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        if (GetProjectStatus(project).Phase != ProjectPhase.Delivering)
            return false;
        CollectProjectMaterials(project, _materials);
        _deliverItems.Clear();
        int gold = 0;
        foreach (var material in _materials)
        {
            int amount = material.Deliverable(OwnedAmount(material));
            if (amount <= 0)
                continue;
            if (material.IsGold)
                gold = amount;
            else
                _deliverItems.Add((material.Item, amount));
        }
        if (gold <= 0 && _deliverItems.Count == 0)
            return false;
        return Pay(project, gold, _deliverItems);
    }

    // 확인을 다 한 뒤: 골드 → 아이템을 내고, 같은 프레임에 납품량을 올리고 한 번 저장 (차감과 기록이 어긋나지 않게)
    private bool Pay(CommunityProjectDefinition project, int gold, List<(ItemDefinition item, int amount)> items)
    {
        var wallet = Wallet;
        if (gold > 0 && wallet.Gold < gold)
            return false;
        foreach (var (item, amount) in items)
        {
            if (item == null || wallet.Count(item) < amount)
                return false;
        }

        if (gold > 0 && !wallet.TrySpendGold(gold))
            return false;
        var delivered = new List<(string itemId, int amount)>(items.Count);
        foreach (var (item, amount) in items)
        {
            if (wallet.TryRemove(item, amount))
                delivered.Add((item.ItemId, amount));
        }
        Settlement.Deliver(project.ProjectId, gold, delivered);

        // 다 냈으면 단계 건설·정비가 쓰는 "납품 완료" 발전 (반복 사업은 단계가 없어 바로 끝남)
        if (CommunityProjectRules.IsDelivered(project, Settlement))
            Settlement.UnlockDevelopment(project.PaidDevelopment);
        UpdateProjects(false);
        SaveRequested?.Invoke();
        return true;
    }

    private bool TryFindItem(string itemId, out ItemDefinition item)
    {
        item = null;
        var inventory = InventoryManager.Instance;
        return inventory != null && inventory.Config != null && inventory.Config.ItemDatabase != null
            && inventory.Config.ItemDatabase.TryGet(itemId, out item);
    }

    #endregion

    #region 공동사업 — 단계

    /// <summary>
    /// 지금 단계를 진행한다: 짓기 = 건설 시작(비용 없음), 정비 = 주민 고르기 화면, 입주·장애물 = 광장에서 (여기서는 NotNow),
    /// 소품 = 고른 것을 지음 (TryChooseWelcomeProp 먼저), 모임 = TryHoldGathering
    /// </summary>
    public ProjectActionResult TryAdvanceProject(CommunityProjectDefinition project)
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        var status = GetProjectStatus(project);
        if (status.Phase != ProjectPhase.Stage || status.StageIndex >= project.Stages.Count)
            return ProjectActionResult.NotNow;
        var stage = project.Stages[status.StageIndex];
        if (status.StageState == ProjectStageState.Busy)
            return ProjectActionResult.Busy;
        if (status.StageState != ProjectStageState.Ready)
            return ProjectActionResult.NotNow;

        switch (stage.Action)
        {
            case ProjectActionKind.CompleteConstruction:
                return StartProjectConstruction(stage.Construction);

            case ProjectActionKind.PlaceWelcomeProp:
            {
                var chosen = CommunityProjectRules.StageConstruction(project, stage, Settlement);
                return chosen == null ? ProjectActionResult.NeedChoice : StartProjectConstruction(chosen);
            }

            case ProjectActionKind.CompleteRegionTask:
                if (stage.Task == null)
                    return ProjectActionResult.NotNow;
                RequestTask(stage.Task);
                return ProjectActionResult.OpenTask;

            case ProjectActionKind.HoldGathering:
                return TryHoldGathering(project) ? ProjectActionResult.Done : ProjectActionResult.NotNow;

            default:
                return ProjectActionResult.NotNow;
        }
    }

    // 사업 단계의 건설: 비용 0 (재료는 납품으로 이미 냄) → 기존 건설 큐로 시작
    private ProjectActionResult StartProjectConstruction(BoardRequestDefinition request)
    {
        if (request == null || request.Construction == null)
            return ProjectActionResult.NotNow;
        switch (TryStart(request))
        {
            case ConstructionStartResult.Started:
            case ConstructionStartResult.Completed:
                return ProjectActionResult.Done;
            case ConstructionStartResult.Busy:
            case ConstructionStartResult.WorkerBusy:
                return ProjectActionResult.Busy;
            default:
                return ProjectActionResult.NotNow;
        }
    }

    /// <summary>환영 소품을 고르고 바로 짓기 시작한다 (한 번 고르면 바꾸지 않음. 큐가 차 있으면 고른 것만 남고 나중에 시작)</summary>
    public ProjectActionResult TryChooseWelcomeProp(CommunityProjectDefinition project, BoardRequestDefinition option)
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        if (option == null)
            throw new ArgumentNullException(nameof(option));
        var stage = CurrentStage(project);
        if (stage == null || stage.Action != ProjectActionKind.PlaceWelcomeProp || !Contains(stage.Options, option))
            return ProjectActionResult.NotNow;
        var progress = Settlement.GetProject(project.ProjectId);
        if (progress == null || string.IsNullOrEmpty(progress.Choice))
        {
            Settlement.SetProjectChoice(project.ProjectId, option.RequestId);
            SaveRequested?.Invoke();
        }
        return TryAdvanceProject(project);
    }

    private static bool ContainsId(IReadOnlyList<string> list, string id)
    {
        foreach (var x in list)
        {
            if (x == id)
                return true;
        }
        return false;
    }

    private static bool Contains(IReadOnlyList<BoardRequestDefinition> list, BoardRequestDefinition item)
    {
        foreach (var x in list)
        {
            if (x == item)
                return true;
        }
        return false;
    }

    /// <summary>광장의 사업 장애물을 하나 치움 (지금 사업의 장애물 단계일 때만). 다 치우면 정비가 열림</summary>
    /// <returns>이번에 치웠으면 true</returns>
    public bool TryClearProjectObstacle(string obstacleId)
    {
        if (string.IsNullOrEmpty(obstacleId))
            return false;
        var project = ActiveProject;
        var stage = project != null ? CurrentStage(project) : null;
        if (stage == null || stage.Action != ProjectActionKind.ClearObstacles || !ContainsId(stage.ObstacleIds, obstacleId)
            || Settlement.HasFlag(CommunityProjectRules.ObstacleFlag(obstacleId)))
            return false;

        Settlement.SetFlag(CommunityProjectRules.ObstacleFlag(obstacleId));
        if (CommunityProjectRules.AreObstaclesCleared(stage, Settlement) && !string.IsNullOrEmpty(stage.ClearedDevelopment))
            Settlement.UnlockDevelopment(stage.ClearedDevelopment);
        SaveRequested?.Invoke();
        return true;
    }

    /// <summary>이 장애물이 지금 치울 차례인지 (광장의 장애물이 통통 뜀)</summary>
    public bool IsProjectObstacleActive(string obstacleId)
    {
        var project = ActiveProject;
        var stage = project != null ? CurrentStage(project) : null;
        return stage != null && stage.Action == ProjectActionKind.ClearObstacles && ContainsId(stage.ObstacleIds, obstacleId)
            && !Settlement.HasFlag(CommunityProjectRules.ObstacleFlag(obstacleId));
    }

    #endregion

    #region 공동사업 — 완료 · 보상

    /// <summary>
    /// 기록에서 사업을 다시 본다: 다 낸 사업은 납품 완료 발전, 집이 다 지어진 자리는 집 기록, 모든 단계를 끝낸 사업은 결과·보상.
    /// 불러올 때(silent)는 알림 없이 맞추기만 한다 (주다 만 경험치는 이때 마저 줌)
    /// </summary>
    private void UpdateProjects(bool silent)
    {
        if (!IsLoaded && !silent)
            return;
        // 한 번에 여러 사업을 끝낼 수 있음 (불러올 때 소급). 무한 반복은 막음
        for (int guard = 0; guard < 16; guard++)
        {
            SyncHouses();
            var project = ActiveProject;
            if (project == null)
                break;
            if (CommunityProjectRules.IsDelivered(project, Settlement) && !Settlement.HasDevelopment(project.PaidDevelopment)
                && GetProjectStatus(project).Phase != ProjectPhase.NeedsLevel)
                Settlement.UnlockDevelopment(project.PaidDevelopment);
            if (!CommunityProjectRules.IsReadyToComplete(project, Settlement, PlayerLevel))
                break;
            CompleteProject(project, silent);
        }
        EnsureLifeRequests();
    }

    // 사업 끝: 결과 발전 · 방명록 · 경험치(한 번) · 반복 사업은 다음 회차
    private void CompleteProject(CommunityProjectDefinition project, bool silent)
    {
        int cycle = Settlement.ProjectCycle(project.ProjectId);
        int xp;
        if (project.Repeatable)
        {
            // 회차 자체가 기록: 같은 회차는 한 번만 넘어감
            if (!Settlement.AdvanceProjectCycle(project.ProjectId, cycle))
                return;
            xp = RepeatXp(project);
            GrantXp(xp);
            if (project.MilestoneCycles > 0 && cycle + 1 >= project.MilestoneCycles && !string.IsNullOrEmpty(project.MilestoneDevelopment))
                Settlement.UnlockDevelopment(project.MilestoneDevelopment);
        }
        else
        {
            Settlement.MarkProjectCompleted(project.ProjectId);
            if (!string.IsNullOrEmpty(project.ResultDevelopment))
                Settlement.UnlockDevelopment(project.ResultDevelopment);
            if (project.CompletionEntry != null)
                Settlement.AddGuestbook(project.CompletionEntry.EntryId);
            // 보상 기록과 경험치를 같은 프레임에 (기록이 이미 있으면 주지 않음)
            xp = Settlement.AddReward(CommunityProjectRules.RewardKey(project, cycle)) ? project.XpReward : 0;
            GrantXp(xp);
        }
        SaveRequested?.Invoke();
        if (!silent)
            OnProjectCompleted?.Invoke(project, xp);
        else if (xp > 0)
            Debug.Log($"[SettlementManager] 기록에 맞춰 공동사업 '{project.ProjectId}'을(를) 끝내고 경험치 {xp}을(를) 마저 줬습니다.");
    }

    private int RepeatXp(CommunityProjectDefinition project)
    {
        var profile = ProfileManager.Instance;
        if (project.XpPercentOfLevel <= 0f || profile == null || profile.LevelTable == null)
            return project.XpReward;
        int need = profile.LevelTable.ExpToNext(profile.Level);
        return need <= 0 ? project.XpReward : Math.Max(1, Mathf.RoundToInt(need * project.XpPercentOfLevel / 100f));
    }

    private static void GrantXp(int xp)
    {
        if (xp > 0 && ProfileManager.Instance != null)
            ProfileManager.Instance.AddExp(xp);
    }

    // 사업의 집 짓기가 끝난 자리에 집 기록을 만든다 (자리마다 한 채. 다시 해도 같음)
    private void SyncHouses()
    {
        foreach (var project in _config.Projects)
        {
            if (project == null)
                continue;
            BoardRequestDefinition lastBuild = null;
            foreach (var stage in project.Stages)
            {
                if (stage.Action == ProjectActionKind.CompleteConstruction)
                    lastBuild = stage.Construction;
                if (stage.Action != ProjectActionKind.SettleResident || string.IsNullOrEmpty(stage.HouseSlotId) || lastBuild == null)
                    continue;
                if (Settlement.IsCompleted(lastBuild.RequestId) && Settlement.FindHouseBySlot(stage.HouseSlotId) == null)
                    Settlement.AddHouse(CommunityProjectRules.HouseInstanceId(stage.HouseSlotId),
                        lastBuild.Construction != null ? lastBuild.Construction.ConstructionId : lastBuild.RequestId, stage.HouseSlotId);
            }
        }
    }

    #endregion

    #region 입주

    // 이 해달이 입주할 사업 단계 (지금 사업의 지금 단계일 때만)
    private ProjectStageDefinition MoveInStage(SettlementOtterDefinition otter)
    {
        if (otter == null)
            return null;
        var project = ActiveProject;
        var stage = project != null ? CurrentStage(project) : null;
        return stage != null && stage.Action == ProjectActionKind.SettleResident && stage.Resident == otter ? stage : null;
    }

    /// <summary>광장에서 말을 걸면 입주를 물을 수 있는지 (집이 있고, 만났고, 아직 입주 전)</summary>
    public bool CanMoveIn(SettlementOtterDefinition otter)
    {
        var stage = MoveInStage(otter);
        return stage != null && Settlement.FindHouseBySlot(stage.HouseSlotId) != null && Settlement.HasMet(otter.OtterId)
            && !CommunityProjectRules.IsSettled(stage, Settlement);
    }

    /// <summary>새 이웃이 그 집에 입주 (집의 입주민 + 주민 처지를 같은 저장에). 연타해도 한 번</summary>
    /// <returns>이번에 입주했으면 true</returns>
    public bool TryMoveIn(SettlementOtterDefinition otter)
    {
        if (!CanMoveIn(otter))
            return false;
        var stage = MoveInStage(otter);
        var house = Settlement.FindHouseBySlot(stage.HouseSlotId);
        if (!Settlement.SetHouseResident(house.InstanceId, otter.OtterId) && house.ResidentId != otter.OtterId)
            return false;
        Settlement.SetResident(otter.OtterId, ResidentState.Resident);
        if (otter.CollectionEntry != null && CollectionManager.Instance != null)
            CollectionManager.Instance.Register(otter.CollectionEntry.EntryId);
        OnResidentMovedIn?.Invoke(otter);
        UpdateProjects(false);
        SaveRequested?.Invoke();
        return true;
    }

    /// <summary>이 해달이 사는 집 (없으면 null)</summary>
    public HouseRecord HouseOf(SettlementOtterDefinition otter) => otter != null ? Settlement.FindHouseOf(otter.OtterId) : null;

    #endregion

    #region 첫 마을 모임

    /// <summary>[모임 열기]: 모임 기록을 남기고(= 사업 완료·보상) 광장에 연출을 부탁. 건너뛰어도 끝난 것은 같다</summary>
    public bool TryHoldGathering(CommunityProjectDefinition project)
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        var stage = CurrentStage(project);
        if (stage == null || stage.Action != ProjectActionKind.HoldGathering)
            return false;
        if (!Settlement.SetFlag(CommunityProjectRules.GatheringFlag(project)))
            return false;
        UpdateProjects(false);
        SaveRequested?.Invoke();
        OnGatheringHeld?.Invoke(project);
        return true;
    }

    /// <summary>첫 모임을 열었는지 (기념 기록이 있음)</summary>
    public bool HasHeldGathering
    {
        get
        {
            foreach (var project in _config.Projects)
            {
                if (project == null)
                    continue;
                foreach (var stage in project.Stages)
                {
                    if (stage.Action == ProjectActionKind.HoldGathering && Settlement.HasFlag(CommunityProjectRules.GatheringFlag(project)))
                        return true;
                }
            }
            return false;
        }
    }

    /// <summary>모임 연출을 다 봤거나 건너뜀</summary>
    public void MarkGatheringViewed()
    {
        if (Settlement.SetFlag(GatheringViewedFlag))
            SaveRequested?.Invoke();
    }

    /// <summary>기념 기록에서 모임 다시 보기 (광장에서만 보임)</summary>
    public void RequestGatheringReplay()
    {
        if (HasHeldGathering)
            OnGatheringReplayRequested?.Invoke();
    }

    #endregion

    #region 생활 의뢰

    /// <summary>생활 의뢰가 열렸는지 (마을회관 + 레벨)</summary>
    public bool IsLifeRequestOpen => LifeRequestRules.IsOpen(_config, Settlement, PlayerLevel);

    /// <summary>
    /// 게시판 주민 부탁 칸(최대 2)에서 기존 주민 부탁이 쓰고 남은 칸만큼 생활 의뢰를 건다. 회차 순서대로라 다시 뽑지 않는다
    /// </summary>
    public void EnsureLifeRequests()
    {
        if (!IsLoaded || !IsLifeRequestOpen)
            return;
        int slots = LifeRequestSlots();
        bool added = false;
        for (int guard = 0; guard < 4 && Settlement.LifeRequests.Count < slots; guard++)
        {
            string exclude = Settlement.LifeRequests.Count > 0 ? Settlement.LifeRequests[0].TemplateId : null;
            if (!LifeRequestRules.TryPick(_config, Settlement.LifeRequestSerial, IsObtainable, exclude, out var template, out var item, out int amount))
                break;
            Settlement.AddLifeRequest(template.TemplateId, item != null ? item.ItemId : null, amount);
            added = true;
        }
        if (added)
            SaveRequested?.Invoke();
    }

    // 주민 부탁 칸 - 지금 보이는 기존 주민 부탁 (진행 중인 생활 의뢰는 칸과 상관없이 남음)
    private int LifeRequestSlots()
    {
        int slots = SettlementBoardRules.ResidentSlots(_config, BoardTier);
        CollectBoardRows(_lifeRows);
        foreach (var row in _lifeRows)
        {
            if (row.Section == BoardSection.Resident)
                slots--;
        }
        return Math.Max(0, slots);
    }

    // 지금 얻을 수 있는 재료인지: 작물은 그 장소에서 생산할 수 있을 때, 나머지(목재·돌)는 광장에서 늘 주울 수 있음
    private bool IsObtainable(ItemDefinition item)
    {
        if (item == null)
            return false;
        foreach (var template in _config.LifeRequests)
        {
            if (template == null || string.IsNullOrEmpty(template.ProductionZone))
                continue;
            foreach (var candidate in template.Items)
            {
                if (candidate == item)
                    return CanProduceIn(template.ProductionZone);
            }
        }
        return true;
    }

    public LifeRequestTemplate FindLifeTemplate(LifeRequestRecord record) => _config.FindLifeRequest(record.TemplateId);

    /// <summary>건네줄 아이템 (의뢰 틀의 후보에서 찾고, 없으면 아이템 목록에서)</summary>
    public ItemDefinition LifeRequestItem(LifeRequestRecord record)
    {
        if (string.IsNullOrEmpty(record.ItemId))
            return null;
        var template = FindLifeTemplate(record);
        if (template != null)
        {
            foreach (var candidate in template.Items)
            {
                if (candidate != null && candidate.ItemId == record.ItemId)
                    return candidate;
            }
        }
        return TryFindItem(record.ItemId, out var item) ? item : null;
    }

    /// <summary>주민 작업 의뢰가 진행 중이면 그 작업 (아니면 null)</summary>
    public SettlementTaskJob LifeWorkJob(LifeRequestRecord record)
    {
        var template = FindLifeTemplate(record);
        string taskId = template != null ? record.TaskInstanceId(template.Task) : null;
        return taskId != null && Settlement.TryGetTaskJob(taskId, out var job) ? job : null;
    }

    /// <summary>같은 작업 틀로 하는 회차 작업 중 진행 중인 것 (광장 생활 의뢰 현장. 없으면 null)</summary>
    public SettlementTaskJob FindRepeatedTaskJob(SettlementTaskDefinition template)
    {
        if (template == null)
            return null;
        foreach (var job in Settlement.TaskJobs)
        {
            if (SettlementConfig.IsTaskInstance(job.TaskId) && SettlementConfig.TaskTemplateId(job.TaskId) == template.TaskId)
                return job;
        }
        return null;
    }

    /// <summary>생활 의뢰 화면을 연다</summary>
    public void RequestLifeRequest(int serial) => OnLifeRequestRequested?.Invoke(serial);

    /// <summary>건네주기 의뢰를 끝낸다 (가방에 다 있어야 함). 아이템을 내고 보상을 받고 다음 의뢰가 걸림</summary>
    public bool TryCompleteLifeDelivery(int serial)
    {
        if (!Settlement.TryGetLifeRequest(serial, out var record))
            return false;
        var template = FindLifeTemplate(record);
        var item = LifeRequestItem(record);
        if (template == null || template.Kind != LifeRequestKind.Deliver || item == null)
            return false;
        var wallet = Wallet;
        if (wallet.Count(item) < record.Amount || !wallet.TryRemove(item, record.Amount))
            return false;
        FinishLifeRequest(record, template);
        return true;
    }

    /// <summary>주민 작업 의뢰에 주민 한 명을 보낸다 (짧은 광장 작업. 다른 작업과 같은 노동력 규칙)</summary>
    public TaskStartResult TryStartLifeWork(int serial, SettlementOtterDefinition worker)
    {
        if (!Settlement.TryGetLifeRequest(serial, out var record))
            return TaskStartResult.NotAvailable;
        var template = FindLifeTemplate(record);
        if (template == null || template.Kind != LifeRequestKind.ResidentWork || template.Task == null || LifeWorkJob(record) != null)
            return TaskStartResult.NotAvailable;
        if (worker == null || !CanAssign(worker))
            return TaskStartResult.WorkerBusy;

        var task = template.Task;
        if (!HasEnoughGold(task.RequiredGold))
            return TaskStartResult.NotEnoughGold;
        if (!HasEnoughItems(task.RequiredItems))
            return TaskStartResult.NotEnoughItems;
        PayCost(task.RequiredGold, task.RequiredItems);
        long now = NowTicks;
        Settlement.StartTask(record.TaskInstanceId(task), new[] { worker.OtterId }, now, now + TimeSpan.FromSeconds(task.DurationSeconds).Ticks);
        OnTaskStarted?.Invoke(task);
        SaveRequested?.Invoke();
        return TaskStartResult.Started;
    }

    // 생활 의뢰의 주민 작업이 끝남: 해달은 돌아오고 의뢰 보상 (작업 완료 팝업 대신 알림 띠)
    private void FinishLifeWork(SettlementTaskJob job)
    {
        var task = _config.FindTask(job.TaskId);
        Settlement.FinishTask(job.TaskId, null);
        Settlement.ForgetCompletedTask(job.TaskId);
        foreach (var record in Settlement.LifeRequests)
        {
            var template = FindLifeTemplate(record);
            if (template != null && record.TaskInstanceId(template.Task) == job.TaskId)
            {
                FinishLifeRequest(record, template);
                break;
            }
        }
        if (task != null)
            OnTaskFinished?.Invoke(task, true);
        SaveRequested?.Invoke();
    }

    // 보상(골드·경험치)과 의뢰 내림을 같은 프레임에 → 다음 의뢰
    private void FinishLifeRequest(LifeRequestRecord record, LifeRequestTemplate template)
    {
        if (!Settlement.RemoveLifeRequest(record.Serial, true))
            return;
        int gold = template.Gold;
        if (gold > 0 && CurrencyManager.Instance != null && _config.GoldCurrency != null)
            CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(_config.GoldCurrency, gold, TransactionSource.RequestReward));
        var profile = ProfileManager.Instance;
        int xp = profile != null ? LifeRequestRules.XpFor(template, profile.Level, profile.LevelTable) : 0;
        GrantXp(xp);
        EnsureLifeRequests();
        OnLifeRequestCompleted?.Invoke(template, gold, xp);
        SaveRequested?.Invoke();
    }

    /// <summary>아직 손대지 않은 의뢰를 다음 의뢰로 바꾼다 (보상 없음). 작업 중인 의뢰는 바꿀 수 없음</summary>
    public bool TrySwapLifeRequest(int serial)
    {
        if (!Settlement.TryGetLifeRequest(serial, out var record) || LifeWorkJob(record) != null)
            return false;
        Settlement.RemoveLifeRequest(serial, false);
        // 같은 의뢰가 바로 다시 나오지 않게 지금 의뢰의 틀은 빼고 고름
        if (LifeRequestRules.TryPick(_config, Settlement.LifeRequestSerial, IsObtainable, record.TemplateId, out var template, out var item, out int amount))
            Settlement.AddLifeRequest(template.TemplateId, item != null ? item.ItemId : null, amount);
        EnsureLifeRequests();
        SaveRequested?.Invoke();
        return true;
    }

    #endregion

    #region 요정 방문

    /// <summary>요정이 광장에 와 있는지 (요정 NPC·상점의 조건). 요정 발전이 없는 데이터는 늘 와 있음</summary>
    public bool IsFairyArrived =>
        string.IsNullOrEmpty(_config.FairyArrivedDevelopment) || Settlement.HasDevelopment(_config.FairyArrivedDevelopment);

    /// <summary>방문이 예약되어 광장의 안전한 때를 기다리는 중 (농부 파견 뒤, 아직 나타나기 전)</summary>
    public bool IsFairyComing => !IsFairyArrived && !string.IsNullOrEmpty(_config.FairyInvitedDevelopment)
        && Settlement.HasDevelopment(_config.FairyInvitedDevelopment);

    /// <summary>광장이 요정을 실제로 나타나게 한 순간 (한 번. 이 뒤로 상점이 열림)</summary>
    /// <returns>이번에 도착했으면 true</returns>
    public bool TryMarkFairyArrived()
    {
        if (!IsFairyComing)
            return false;
        Settlement.UnlockDevelopment(_config.FairyArrivedDevelopment);
        SaveRequested?.Invoke();
        return true;
    }

    #endregion
}
