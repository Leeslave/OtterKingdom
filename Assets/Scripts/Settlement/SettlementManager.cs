using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>건설을 시작하지 못한 이유</summary>
public enum ConstructionStartResult
{
    Started,
    Completed,      // 시간이 0이라 바로 끝남
    NotAvailable,   // 잠김 / 이미 끝남 / 건설 중
    Busy,           // 다른 건설이 진행 중
    NoBuilder,      // 건설 해달이 아직 없음
    NotEnoughGold,
    NotEnoughItems,
    WorkerBusy,     // 일할 해달이 주민 작업에 가 있음
    NeedsPlace,     // 자리를 골라 짓는 건물인데 아직 자리를 고르지 않음 (건설 모드에서 TryStartAt)
}

/// <summary>주민 작업을 시작하지 못한 이유</summary>
public enum TaskStartResult
{
    Started,
    NotAvailable,      // 잠김 / 이미 하는 중 / 끝남
    WrongWorkerCount,  // 필요한 수만큼 고르지 않음
    WorkerBusy,        // 주민이 아니거나 다른 일을 하는 중
    NotEnoughGold,     // 작업 비용 (농경지 개간)
    NotEnoughItems,
}

/// <summary>마을회관의 주민 현황: 정착 주민 수, 작업에 보낼 수 있는 주민 중 지금 쉬는 수 · 작업 중인 수</summary>
public readonly struct LaborSummary
{
    /// <summary>정착 주민 (Resident 전부: 건설·전문 해달 포함)</summary>
    public readonly int Settled;
    /// <summary>작업에 보낼 수 있는 주민 (건설·전문·관리 해달 제외)</summary>
    public readonly int Workforce;
    /// <summary>지금 바로 보낼 수 있음 (작업 화면과 같은 판정)</summary>
    public readonly int Available;
    /// <summary>주민 작업 중</summary>
    public readonly int Working;

    public LaborSummary(int settled, int workforce, int available, int working)
    {
        Settled = settled;
        Workforce = workforce;
        Available = available;
        Working = working;
    }
}

/// <summary>
/// 정착 진행(Settlement)의 주인. 전역 UI(GlobalUI) 루트에 붙어 씬을 넘어 유지된다.
/// 게시판 부탁의 건설 비용(골드·재료)을 받고, 시간이 지나면 완료 처리하고, 장소 해금(밭 등)을 알려 준다.
/// 개간 지역(P1): 플레이어가 길을 다 치우면 주민 해달을 작업에 보내고, 작업이 끝나면 지역을 운영(부탁 완료·꾸미기 구역 해금)한다.
/// 전문 해달: 운영되면 그 지역의 전문 해달(광부·농부)이 광장에 찾아오고, 플레이어가 배치한 뒤 그 장소의 안내를 끝내면 생산이 시작된다.
/// 게시판 성장(P2): 게시판 보강 → 관리 해달 방문·역할 맡김 → 주민 부탁(광장 주민 작업) → 접수소(큰 부탁 묶음) → 마을회관(발전 현황).
/// 공동사업·생활 의뢰·요정 방문(P3): SettlementManager.P3.cs.
/// 기존 시스템(밭·가방·재화·퀘스트) 앞에서 진행만 제어하는 상위 레이어 — 그 내부 로직은 건드리지 않는다.
/// - 세이브: GameManager가 LoadFromSave / WriteToSave를 호출. 부탁을 끝내면 SaveRequested로 바로 저장을 부탁한다
/// </summary>
// 매니저들(-100)이 준비된 뒤, GameManager가 세이브를 불러오기 전에 준비
[DefaultExecutionOrder(-80)]
public partial class SettlementManager : MonoBehaviour
{
    public static SettlementManager Instance { get; private set; }

    /// <summary>바로 저장해 달라는 부탁 (부탁 완료·건설 시작·나뭇가지 줍기). GameManager가 듣는다</summary>
    public static event Action SaveRequested;

    [Header("데이터")]
    [SerializeField] private SettlementConfig _config;

    public SettlementConfig Config => _config;
    public Settlement Settlement { get; private set; }

    /// <summary>세이브를 불러왔는지 (불러오기 전에는 새 게임 상태로 보임)</summary>
    public bool IsLoaded { get; private set; }

    public static long NowTicks => GameClock.UtcNow.Ticks;

    /// <summary>세이브를 불러왔을 때 (광장이 집·해달을 맞춰 그림)</summary>
    public event Action OnLoaded;

    /// <summary>무엇이든 바뀌었을 때 (화면 갱신용)</summary>
    public event Action OnChanged;

    /// <summary>부탁을 끝냈을 때 (완료 팝업)</summary>
    public event Action<BoardRequestDefinition> OnRequestCompleted;

    /// <summary>시간이 걸리는 건설을 시작했을 때 (건설 해달이 일하러 감)</summary>
    public event Action<BoardRequestDefinition> OnConstructionStarted;

    /// <summary>발전이 열렸을 때 (광장 오브젝트, 장소 해금)</summary>
    public event Action<string> OnDevelopmentUnlocked;

    /// <summary>게시판을 열어 달라는 부탁 (광장 게시판·안내 띠). 게시판 화면이 듣는다. 인자: 부탁 탭으로 열지</summary>
    public event Action<bool> OnBoardRequested;

    /// <summary>주민 작업을 시작했을 때 (광장의 해달이 일하러 떠남)</summary>
    public event Action<SettlementTaskDefinition> OnTaskStarted;

    /// <summary>주민 작업이 끝났을 때. 인자: 이 작업으로 지역이 운영되었거나 부탁이 끝났는지 (그러면 부탁 완료 팝업이 대신 알림)</summary>
    public event Action<SettlementTaskDefinition, bool> OnTaskFinished;

    /// <summary>작업 화면을 열어 달라는 부탁 (광산의 정비 표지판, 게시판). 작업 화면이 듣는다</summary>
    public event Action<SettlementTaskDefinition> OnTaskRequested;

    /// <summary>광장에서 해달을 처음 만났을 때 (새 해달 만나기 퀘스트). 인자: 해달 ID</summary>
    public event Action<string> OnOtterMet;

    /// <summary>전문 해달을 일할 곳에 배치했을 때 (광장의 그 해달이 길 끝으로 걸어 나감)</summary>
    public event Action<SettlementOtterDefinition> OnSpecialistAssigned;

    /// <summary>관리 역할을 맡겼을 때 (광장의 그 해달이 근무 자리로 걸어감)</summary>
    public event Action<ManagementRoleDefinition> OnRoleAssigned;

    /// <summary>이 부탁을 하러 가 달라는 부탁 (마을회관·큰 부탁의 [가 보기]). 게시판 화면 쪽이 듣고 알맞은 화면을 연다</summary>
    public event Action<BoardRequestDefinition> OnRequestOpenRequested;

    /// <summary>큰 부탁 화면을 열어 달라는 부탁 (게시판 카드, 접수소)</summary>
    public event Action<MilestoneGroupDefinition> OnMilestoneRequested;

    /// <summary>마을회관 발전 현황 화면을 열어 달라는 부탁 (게시판 카드, 마을회관)</summary>
    public event Action OnTownHallRequested;

    // 일할 해달이 도착하지 못해도 이만큼 지나면 공사를 시작함 (길이 막히는 등)
    private const float WorkerWaitLimitSeconds = 25f;
    private float _workerWaitSeconds;

    // 끝난 건설 (부탁, 끝난 시각). 돌아옴 팝업이 자리를 비운 동안 끝난 것만 골라 알리고 비운다
    private readonly List<(BoardRequestDefinition request, long endTicks)> _finishedJobs = new List<(BoardRequestDefinition, long)>();
    // 끝난 주민 작업 (작업, 끝난 시각). 돌아옴 팝업용
    private readonly List<(SettlementTaskDefinition task, long endTicks)> _finishedTasks = new List<(SettlementTaskDefinition, long)>();
    private readonly List<SettlementTaskJob> _dueTasks = new List<SettlementTaskJob>();
    private readonly List<BoardRequestRow> _boardRows = new List<BoardRequestRow>();
    private readonly List<SettlementOtterDefinition> _laborCandidates = new List<SettlementOtterDefinition>();

    private void Awake()
    {
        // 중복은 GlobalUIRoot가 먼저 꺼서 여기까지 오지 않지만, 혹시 모를 경우를 대비
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        Settlement = new Settlement();
        Settlement.OnChanged += () =>
        {
            // 공동사업의 단계·완료는 기록에서 계산하므로, 무엇이 바뀌든 다음 프레임에 다시 봄 (바꾸는 도중에 다시 바꾸지 않게)
            _projectsDirty = true;
            _territoryVersion++;
            OnChanged?.Invoke();
        };
        Settlement.OnDevelopmentUnlocked += id => OnDevelopmentUnlocked?.Invoke(id);
        // 왕국 보유 효과(특성 건물): 판매·개간·채굴·수확이 여기서 합계를 물음
        KingdomBonus.Source = BonusPercent;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            KingdomBonus.Source = null;
        }
    }

    private void Update()
    {
        if (!IsLoaded)
            return;
        FinishDueTasks();
        // 레벨이 오르면 다음 사업·생활 의뢰가 열릴 수 있음
        int level = PlayerLevel;
        if (level != _seenLevel)
        {
            // 처음 보는 레벨(씬을 연 직후)은 이미 맞춰 둔 것이라 알리지 않음
            bool levelUp = _seenLevel > 0;
            _seenLevel = level;
            _projectsDirty = true;
            // 레벨이 오르면 숲 개간 기회가 생김
            _territoryVersion++;
            // 레벨로 열리는 발전 (Lv.15 낚시터 발견 → 선착장 부탁)
            SyncLevelDevelopments(levelUp);
        }
        if (_projectsDirty)
        {
            _projectsDirty = false;
            UpdateProjects(false);
        }
        TickToyVisits();
        FinishDueBuildings();
        CompleteRecordRequests();
        if (Settlement.Job == null)
            return;

        if (Settlement.Job.WaitingForWorker)
            WaitForWorker();
        else if (Settlement.Job.IsDue(NowTicks))
            FinishJob();
    }

    // 일할 해달이 현장에 가는 중: 광장의 SettlementPlazaView가 도착을 알려 줌(BeginJobWork).
    // 광장에 없거나(다른 장소·꺼 둔 동안) 너무 오래 못 오면 그냥 시작 (공사가 멈춰 있지 않게)
    private void WaitForWorker()
    {
        _workerWaitSeconds += Time.unscaledDeltaTime;
        if (SettlementPlazaView.Active == null || _workerWaitSeconds >= WorkerWaitLimitSeconds)
            BeginJobWork();
    }

    /// <summary>일할 해달이 현장에 도착: 지금부터 공사 시간이 흐른다</summary>
    public void BeginJobWork()
    {
        _workerWaitSeconds = 0f;
        if (Settlement.BeginJobWork(NowTicks))
            SaveRequested?.Invoke();
    }

    #region 조회

    public RequestStatus GetStatus(BoardRequestDefinition request) => SettlementRules.GetStatus(request, Settlement);

    public bool HasDevelopment(string developmentId) => Settlement.HasDevelopment(developmentId);

    public bool HasBuilder => SettlementRules.HasBuilder(_config, Settlement);

    public string StageName => _config.StageName(Settlement.Stage);

    /// <summary>지금 할 부탁 (건설 중 우선). 없으면 null</summary>
    public BoardRequestDefinition CurrentRequest => SettlementRules.FindCurrent(_config, Settlement);

    /// <summary>진행 중인 건설의 부탁 (없으면 null)</summary>
    public BoardRequestDefinition JobRequest => Settlement.Job != null ? _config.FindRequest(Settlement.Job.RequestId) : null;

    /// <summary>게시판에 보이는 부탁 중 시작할 수 있는데 아직 손대지 않은 것이 있는지 (게시판 빨간 점)</summary>
    public bool HasActionableRequest
    {
        get
        {
            CollectBoardRows(_boardRows);
            foreach (var row in _boardRows)
            {
                if (row.Status == RequestStatus.Available && !IsBeingPrepared(row.Request))
                    return true;
            }
            return false;
        }
    }

    /// <summary>게시판 "해달의 부탁"에 보일 부탁과 구역 (등급·주민 부탁 칸 규칙: SettlementBoardRules)</summary>
    public void CollectBoardRows(List<BoardRequestRow> rows)
    {
        // 데이터는 실행 중에 바뀌지 않으므로 정렬은 한 번만 (게시판 "!"가 매 프레임 물어봄)
        _sortedRequests ??= SettlementBoardRules.SortRequests(_config);
        SettlementBoardRules.CollectRows(_config, _sortedRequests, Settlement, rows);
    }

    private List<BoardRequestDefinition> _sortedRequests;

    /// <summary>게시판 등급 (발전 기록에서 계산)</summary>
    public BoardTier BoardTier => SettlementBoardRules.GetTier(_config, Settlement);

    // 직접 치우는 부탁의 지역을 주민 해달이 정비하는 중 (플레이어가 할 일이 없음)
    private bool IsBeingPrepared(BoardRequestDefinition request)
    {
        var region = request.ClearZone != null ? _config.FindRegion(request.ClearZone) : null;
        return region != null && GetRegionState(region) == RegionProgressState.WorkerPreparing;
    }

    /// <summary>이 장소에 갈 수 있는지 (정착 조건만. 씬 준비 여부는 ZoneDefinition.IsAvailable)</summary>
    public bool IsZoneOpen(ZoneDefinition zone)
    {
        if (zone == null)
            throw new ArgumentNullException(nameof(zone));
        return Settlement.HasDevelopment(zone.RequiredDevelopment);
    }

    public int GoldBalance => CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCurrency(_config.GoldCurrency) : 0;

    public int ItemCount(ItemDefinition item) =>
        InventoryManager.Instance != null && item != null ? InventoryManager.Instance.Inventory.GetCount(item) : 0;

    public bool HasEnoughGold(ConstructionDefinition construction) =>
        construction.RequiredGold <= 0 || GoldBalance >= construction.RequiredGold;

    public bool HasEnoughItems(ConstructionDefinition construction) => HasEnoughItems(construction.RequiredItems);

    public bool HasEnoughItems(IReadOnlyList<ItemAmount> items)
    {
        foreach (var cost in items)
        {
            if (cost != null && cost.Item != null && ItemCount(cost.Item) < cost.Amount)
                return false;
        }
        return true;
    }

    public bool HasEnoughGold(int gold) => gold <= 0 || GoldBalance >= gold;

    #endregion

    #region 게시판

    /// <summary>게시판을 연다 (화면이 열고, 처음 본 것으로 표시)</summary>
    public void RequestBoard(bool openRequests)
    {
        Settlement.MarkBoardVisited();
        OnBoardRequested?.Invoke(openRequests);
    }

    /// <summary>부탁의 건설을 시작한다. 비용을 내고, 시간이 0이면 바로 끝낸다.
    /// 자리를 골라 짓는 건물(ConstructionPlotDefinition)은 자리가 놓인 뒤에만 시작한다 (건설 모드의 TryStartAt)</summary>
    public ConstructionStartResult TryStart(BoardRequestDefinition request)
    {
        var check = CheckStart(request);
        if (check != ConstructionStartResult.Started)
            return check;
        var plot = PlotOf(request);
        if (plot != null && !IsPlotPlaced(plot))
            return ConstructionStartResult.NeedsPlace;

        var construction = request.Construction;
        PayCost(construction);

        if (construction.IsInstant)
        {
            Complete(request);
            return ConstructionStartResult.Completed;
        }

        long now = NowTicks;
        long end = now + TimeSpan.FromSeconds(DevTimers.Duration(construction.DurationSeconds)).Ticks;
        _workerWaitSeconds = 0f;
        Settlement.StartJob(request.RequestId, construction.ConstructionId, now, end, waitingForWorker: true);
        OnConstructionStarted?.Invoke(request);
        SaveRequested?.Invoke();
        return ConstructionStartResult.Started;
    }

    /// <summary>지금 시작할 수 있는지만 본다 (아무것도 바꾸지 않음). 시작할 수 있으면 Started, 아니면 첫 이유</summary>
    public ConstructionStartResult CheckStart(BoardRequestDefinition request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        var construction = request.Construction;
        if (construction == null)
            throw new InvalidOperationException($"[{request.name}] 건설이 비어 있습니다.");

        if (GetStatus(request) != RequestStatus.Available)
            return ConstructionStartResult.NotAvailable;
        if (Settlement.Job != null && !construction.IsInstant)
            return ConstructionStartResult.Busy;
        if (!construction.IsInstant && !construction.NeedsBuilder && request.Requester != null
            && Settlement.GetWorkState(request.Requester.OtterId) == ResidentWorkState.Working)
            return ConstructionStartResult.WorkerBusy;
        if (construction.NeedsBuilder && !HasBuilder)
            return ConstructionStartResult.NoBuilder;
        if (!HasEnoughGold(construction))
            return ConstructionStartResult.NotEnoughGold;
        if (!HasEnoughItems(construction))
            return ConstructionStartResult.NotEnoughItems;
        return ConstructionStartResult.Started;
    }

    // 확인을 다 한 뒤에만 부름: 골드 → 재료 순서로 낸다
    private void PayCost(ConstructionDefinition construction) => PayCost(construction.RequiredGold, construction.RequiredItems);

    private void PayCost(int gold, IReadOnlyList<ItemAmount> items)
    {
        if (gold > 0)
            CurrencyManager.Instance.TrySpend(_config.GoldCurrency, gold, TransactionSource.Construction);

        foreach (var cost in items)
        {
            // 비용이 없는 공사(공동사업 단계: 재료는 납품으로 이미 냄)는 가방을 건드리지 않음
            if (cost != null && cost.Item != null && cost.Amount > 0)
                InventoryManager.Instance.Inventory.TryRemove(cost.Item, cost.Amount, ItemChangeReason.Construction);
        }
    }

    private void FinishJob()
    {
        var job = Settlement.FinishJob();
        var request = job != null ? _config.FindRequest(job.RequestId) : null;
        if (request == null)
        {
            if (job != null)
                Debug.LogWarning($"[SettlementManager] 진행 중이던 부탁 '{job.RequestId}'을(를) 찾을 수 없어 건설을 취소합니다.");
            SaveRequested?.Invoke();
            return;
        }
        _finishedJobs.Add((request, job.EndUtcTicks));
        Complete(request);
    }

    private void Complete(BoardRequestDefinition request)
    {
        if (!SettlementRules.ApplyCompletion(request, Settlement))
            return;
        OnRequestCompleted?.Invoke(request);

        // 큰 발전: 왕국 레벨이 바로 오르고, 다음 발전까지의 제한이 풀림
        var profile = ProfileManager.Instance;
        if (profile != null && request.KingdomLevel > 0)
            profile.ReachLevel(request.KingdomLevel);
        ApplyLevelCap();
        SaveRequested?.Invoke();
    }

    // 아직 안 끝낸 큰 발전 아래로 왕국 레벨을 묶음 (경험치는 계속 쌓임)
    private void ApplyLevelCap()
    {
        var profile = ProfileManager.Instance;
        if (profile != null)
            profile.SetLevelCap(SettlementRules.LevelCap(_config, Settlement, profile.Level));
    }

    /// <summary>왕국 레벨을 묶고 있는 부탁 (끝내면 레벨이 오름). 묶여 있지 않으면 null</summary>
    public BoardRequestDefinition LevelCapRequest
    {
        get
        {
            var profile = ProfileManager.Instance;
            return IsLoaded ? SettlementRules.LevelCapRequest(_config, Settlement, profile != null ? profile.Level : 1) : null;
        }
    }

    #endregion

    #region 해달과 대화

    /// <summary>이 해달이 아직 하지 않은 첫 이야기가 있는지 (정착 후보의 "살고 싶어요" 등)</summary>
    public bool HasPendingIntro(SettlementOtterDefinition otter)
    {
        if (otter == null || string.IsNullOrEmpty(otter.IntroDevelopment) || Settlement.HasDevelopment(otter.IntroDevelopment))
            return false;
        return Settlement.TryGetResidentState(otter.OtterId, out var state) && state == ResidentState.SettlementCandidate;
    }

    /// <summary>첫 이야기를 들음 → 그 해달의 부탁이 열림</summary>
    /// <returns>새로 들었으면 true</returns>
    public bool TryHearIntro(SettlementOtterDefinition otter)
    {
        if (!HasPendingIntro(otter))
            return false;
        Settlement.UnlockDevelopment(otter.IntroDevelopment);
        SaveRequested?.Invoke();
        return true;
    }

    /// <summary>첫 이야기를 기다리는 정착 후보 (없으면 null)</summary>
    public SettlementOtterDefinition PendingIntroOtter
    {
        get
        {
            foreach (var otter in _config.Otters)
            {
                if (HasPendingIntro(otter))
                    return otter;
            }
            return null;
        }
    }

    #endregion

    #region 줍기 (나뭇가지·나무 → 목재, 바위 → 돌)

    public bool IsGatherReady(string pointId) => Settlement.IsGatherReady(pointId, NowTicks);

    /// <summary>이 자리가 다시 생기기까지 남은 시간 (이미 있으면 0)</summary>
    public TimeSpan GatherRemaining(string pointId) => Settlement.GatherRemaining(pointId, NowTicks);

    /// <summary>"1:23" 처럼 짧게 (한 시간이 넘으면 "1시간 5분")</summary>
    public static string FormatShort(TimeSpan time)
    {
        int seconds = (int)Math.Ceiling(time.TotalSeconds);
        return seconds >= 3600 ? $"{seconds / 3600}시간 {seconds / 60 % 60}분" : $"{seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>설정의 기본값(나뭇가지: 목재 2개, 60초)으로 줍는다</summary>
    public int TryGather(string pointId) =>
        TryGather(pointId, _config.GatherItem, _config.GatherAmount, _config.GatherCooldownSeconds);

    /// <summary>광장에서 주운 재료를 가방에 넣고, 그 자리는 cooldownSeconds 뒤에 다시 생긴다</summary>
    /// <returns>넣은 개수 (아직 안 생겼거나 가방이 꽉 찼으면 0)</returns>
    public int TryGather(string pointId, ItemDefinition item, int amount, float cooldownSeconds)
    {
        if (string.IsNullOrEmpty(pointId))
            throw new ArgumentNullException(nameof(pointId));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (item == null || !IsGatherReady(pointId))
            return 0;

        int added = InventoryManager.Instance.Inventory.Add(item, amount, ItemChangeReason.Gather);
        if (added <= 0)
            return 0;

        Settlement.SetGatherReady(pointId, NowTicks + TimeSpan.FromSeconds(cooldownSeconds).Ticks);
        SaveRequested?.Invoke();
        return added;
    }

    /// <summary>자리의 쉬는 시간과 상관없이 덤으로 얻은 것 (나무에서 떨어진 열매)</summary>
    /// <returns>넣은 개수 (가방이 꽉 찼으면 0)</returns>
    public int GatherExtra(ItemDefinition item, int amount)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        int added = InventoryManager.Instance.Inventory.Add(item, amount, ItemChangeReason.Gather);
        if (added > 0)
            SaveRequested?.Invoke();
        return added;
    }

    /// <summary>장소(광산·밭)의 길을 막은 장애물을 치우고 얻은 재료. 광장 줍기 퀘스트에는 세지 않음</summary>
    /// <returns>넣은 개수 (가방이 꽉 찼으면 0)</returns>
    public int GrantClearingReward(ItemDefinition item, int amount)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        int added = InventoryManager.Instance.Inventory.Add(item, amount, ItemChangeReason.Clearing);
        if (added > 0)
            SaveRequested?.Invoke();
        return added;
    }

    /// <summary>광장에서 드물게 찾은 재화 (바위 속 조개)</summary>
    public void GrantPlazaFind(Currency currency, int amount)
    {
        if (currency == null)
            throw new ArgumentNullException(nameof(currency));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(currency, amount, TransactionSource.PlazaFind));
        SaveRequested?.Invoke();
    }

    #endregion

    #region 장소 개척 (광산 길 열기)

    /// <summary>이 장소를 직접 치우는 부탁을 끝냈는지 (그런 부탁이 없으면 처음부터 열린 곳)</summary>
    public bool IsZoneCleared(ZoneDefinition zone)
    {
        var request = SettlementRules.FindClearing(_config, zone);
        return request == null || Settlement.IsCompleted(request.RequestId);
    }

    /// <summary>지금 이 장소를 치울 수 있는지 (부탁이 열려 있음)</summary>
    public bool CanClearZone(ZoneDefinition zone)
    {
        var request = SettlementRules.FindClearing(_config, zone);
        return request != null && GetStatus(request) == RequestStatus.Available;
    }

    public bool IsObstacleCleared(string obstacleId) => Settlement.HasFlag(ObstacleFlag(obstacleId));

    /// <summary>장애물 하나를 치웠음 (세이브에 남아 다시 오지 않음)</summary>
    public void MarkObstacleCleared(string obstacleId)
    {
        if (string.IsNullOrEmpty(obstacleId))
            throw new ArgumentNullException(nameof(obstacleId));
        if (Settlement.SetFlag(ObstacleFlag(obstacleId)))
            SaveRequested?.Invoke();
    }

    /// <summary>장애물을 다 치움 → 그 장소의 부탁을 끝냄</summary>
    /// <returns>이번에 끝냈으면 true</returns>
    public bool TryClearZone(ZoneDefinition zone)
    {
        var request = SettlementRules.FindClearing(_config, zone);
        return request != null && TryStart(request) == ConstructionStartResult.Completed;
    }

    private static string ObstacleFlag(string obstacleId) => "cleared_" + obstacleId;

    /// <summary>처음 한 번 보여 주는 안내(TapHintView)를 이미 봤는지</summary>
    public bool HasSeen(string hintFlag) => Settlement.HasFlag(hintFlag);

    /// <summary>안내한 조작을 해 봤음 (세이브에 남아 다시 안 보임)</summary>
    public void MarkSeen(string hintFlag)
    {
        if (string.IsNullOrEmpty(hintFlag))
            throw new ArgumentNullException(nameof(hintFlag));
        if (Settlement.SetFlag(hintFlag))
            SaveRequested?.Invoke();
    }

    #endregion

    #region 개간 지역 · 주민 작업

    public RegionProgressState GetRegionState(DevelopableRegionDefinition region) =>
        SettlementRegionRules.GetRegionState(region, Settlement);

    /// <summary>이 장소의 개간 지역 (없으면 null)</summary>
    public DevelopableRegionDefinition FindRegion(ZoneDefinition zone) => _config.FindRegion(zone);

    /// <summary>작업 상태. 영토의 숲 개간은 기회가 있고 다른 개간이 없을 때만 Available (TerritoryRules)</summary>
    public SettlementTaskState GetTaskState(SettlementTaskDefinition task) =>
        TerritoryTaskState(task, SettlementRegionRules.GetTaskState(task, Settlement));

    /// <summary>진행 중인 작업 (없으면 null)</summary>
    public SettlementTaskJob GetTaskJob(SettlementTaskDefinition task) =>
        task != null && Settlement.TryGetTaskJob(task.TaskId, out var job) ? job : null;

    /// <summary>이 지역에서 지금 손댈 후속 정비 (없으면 null)</summary>
    public SettlementTaskDefinition CurrentPreparation(DevelopableRegionDefinition region) =>
        SettlementRegionRules.CurrentPreparation(region, Settlement);

    /// <summary>작업 화면을 연다 (작업 화면이 듣고 염)</summary>
    public void RequestTask(SettlementTaskDefinition task)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));
        OnTaskRequested?.Invoke(task);
    }

    /// <summary>작업에 보낼 수 있는 주민 해달 후보 (온 순서대로, 건설·전문·관리 해달 제외). 지금 바쁜 해달도 들어 있음 → CanAssign으로 확인</summary>
    public void CollectResidents(List<SettlementOtterDefinition> result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        result.Clear();
        foreach (var otterId in Settlement.ResidentOrder)
        {
            var otter = _config.FindOtter(otterId);
            if (otter != null && !otter.IsBuilder && !otter.IsSpecialist && !Settlement.HasRole(otterId)
                && Settlement.TryGetResidentState(otterId, out var state) && state == ResidentState.Resident)
                result.Add(otter);
        }
    }

    /// <summary>
    /// 마을회관의 주민 현황. 보낼 수 있는 수는 작업 화면과 같은 판정(CanAssign)으로 세므로 실제로 보낼 수 있는 인원과 같다
    /// </summary>
    public LaborSummary GetLaborSummary()
    {
        CollectResidents(_laborCandidates);
        int available = 0;
        int working = 0;
        foreach (var otter in _laborCandidates)
        {
            if (CanAssign(otter))
                available++;
            else if (Settlement.GetWorkState(otter.OtterId) == ResidentWorkState.Working)
                working++;
        }
        return new LaborSummary(Settlement.ResidentCount, _laborCandidates.Count, available, working);
    }

    /// <summary>광장에서 하는 주민 작업인지 (개간 지역의 후속 정비가 아님 → 그 장소로 떠나지 않고 광장 현장에서 일함)</summary>
    public bool IsPlazaTask(SettlementTaskDefinition task) => task != null && _config.FindRegionOf(task) == null;

    /// <summary>이 해달이 광장 현장에서 주민 작업 중이면 그 작업 (아니면 null)</summary>
    public SettlementTaskDefinition PlazaTaskOf(string otterId)
    {
        var job = Settlement.FindTaskJobOf(otterId);
        var task = job != null ? _config.FindTask(job.TaskId) : null;
        return IsPlazaTask(task) ? task : null;
    }

    /// <summary>지금 작업에 보낼 수 있는지 (주민 · 다른 작업 없음 · 공사하러 가 있지 않음)</summary>
    public bool CanAssign(SettlementOtterDefinition otter) =>
        SettlementRegionRules.CanWork(otter, Settlement) && !IsConstructionWorker(otter);

    /// <summary>진행 중인 공사를 직접 하는 해달인지 (건설 해달이 필요 없는 공사는 부탁한 해달이 일함)</summary>
    public bool IsConstructionWorker(SettlementOtterDefinition otter)
    {
        var request = JobRequest;
        return otter != null && request != null && !request.Construction.NeedsBuilder
            && request.Requester != null && request.Requester.OtterId == otter.OtterId;
    }

    /// <summary>주민 해달을 보내 작업을 시작한다. 비용이 있으면 이때 내고, 끝나는 시각을 저장해 게임을 꺼 둔 동안에도 진행된다</summary>
    public TaskStartResult TryStartTask(SettlementTaskDefinition task, IReadOnlyList<SettlementOtterDefinition> workers)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));
        if (workers == null)
            throw new ArgumentNullException(nameof(workers));

        if (GetTaskState(task) != SettlementTaskState.Available)
            return TaskStartResult.NotAvailable;
        if (workers.Count != task.RequiredWorkers)
            return TaskStartResult.WrongWorkerCount;

        var ids = new List<string>(workers.Count);
        foreach (var worker in workers)
        {
            if (worker == null || ids.Contains(worker.OtterId) || !CanAssign(worker))
                return TaskStartResult.WorkerBusy;
            ids.Add(worker.OtterId);
        }
        if (!HasEnoughGold(task.RequiredGold))
            return TaskStartResult.NotEnoughGold;
        if (!HasEnoughItems(task.RequiredItems))
            return TaskStartResult.NotEnoughItems;

        PayCost(task.RequiredGold, task.RequiredItems);
        long now = NowTicks;
        Settlement.StartTask(task.TaskId, ids, now, now + TimeSpan.FromSeconds(DevTimers.Duration(task.DurationSeconds)).Ticks);
        OnTaskStarted?.Invoke(task);
        SaveRequested?.Invoke();
        return TaskStartResult.Started;
    }

    /// <summary>플레이어가 지역의 길을 막은 것을 다 치움 → 후속 정비를 기다림 (정비할 작업이 없으면 바로 운영)</summary>
    /// <returns>이번에 바뀌었으면 true</returns>
    public bool MarkRegionPlayerCleared(DevelopableRegionDefinition region)
    {
        if (region == null)
            throw new ArgumentNullException(nameof(region));
        if (GetRegionState(region) != RegionProgressState.PlayerClearing)
            return false;

        Settlement.UnlockDevelopment(region.PlayerClearDevelopment);
        TryOperate(region);
        SaveRequested?.Invoke();
        return true;
    }

    private void FinishDueTasks()
    {
        long now = NowTicks;
        _dueTasks.Clear();
        foreach (var job in Settlement.TaskJobs)
        {
            if (job.IsDue(now))
                _dueTasks.Add(job);
        }
        foreach (var job in _dueTasks)
            FinishTask(job);
    }

    private void FinishTask(SettlementTaskJob job)
    {
        // 생활 의뢰의 주민 작업 (틀ID#회차): 결과 발전 없이 의뢰 보상 (SettlementManager.P3)
        if (SettlementConfig.IsTaskInstance(job.TaskId))
        {
            FinishLifeWork(job);
            return;
        }
        var task = _config.FindTask(job.TaskId);
        if (task == null)
            Debug.LogWarning($"[SettlementManager] 진행 중이던 작업 '{job.TaskId}'을(를) 찾을 수 없어 해달을 돌려보냅니다.");
        Settlement.FinishTask(job.TaskId, task != null ? task.ResultDevelopment : null);
        if (task != null)
        {
            _finishedTasks.Add((task, job.EndUtcTicks));
            // 영토의 숲 개간: 목재를 주고, 3번 다 끝냈으면 영토 확장 미션이 열림 (SettlementManager.Territory)
            FinishTerritoryClearing(task);
            var region = _config.FindRegionOf(task);
            bool operated = region != null && TryOperate(region);
            // 주민 작업 부탁: 실제 작업 완료 기록으로 끝냄 (완료 팝업이 알림)
            var request = _config.FindRequestByTask(task);
            bool completed = request != null && !Settlement.IsCompleted(request.RequestId);
            if (completed)
                Complete(request);
            OnTaskFinished?.Invoke(task, operated || completed);
        }
        SaveRequested?.Invoke();
    }

    // 길을 다 치웠고 후속 정비도 다 끝남 → 운영. 직접 치우는 부탁이 있으면 그 부탁을 끝내서(왕국 레벨·새 해달) 연다
    private bool TryOperate(DevelopableRegionDefinition region)
    {
        if (GetRegionState(region) == RegionProgressState.Operational
            || !Settlement.HasDevelopment(region.PlayerClearDevelopment)
            || !SettlementRegionRules.ArePreparationsDone(region, Settlement))
            return false;

        var request = SettlementRules.FindClearing(_config, region.Zone);
        if (request != null && GetStatus(request) == RequestStatus.Available)
            TryStart(request);
        if (!Settlement.HasDevelopment(region.OperationalDevelopment))
            Settlement.UnlockDevelopment(region.OperationalDevelopment);
        UnlockRegionDecor(region);
        return true;
    }

    // 운영되는 지역의 꾸미기 구역을 연다 (불러온 세이브에도 맞춤. 이미 열려 있으면 그대로)
    private void SyncRegionDecor()
    {
        foreach (var region in _config.Regions)
        {
            if (region != null && GetRegionState(region) == RegionProgressState.Operational)
                UnlockRegionDecor(region);
        }
    }

    private static void UnlockRegionDecor(DevelopableRegionDefinition region)
    {
        var decor = DecorManager.Instance;
        if (decor == null || region.DecorBoard == null || region.DecorRegion == null)
            return;
        decor.UnlockDirectly(region.DecorBoard, region.DecorRegion.RegionId);
    }

    /// <summary>작업 비용 중 모자란 것 한 줄 (넉넉하거나 비용이 없으면 null)</summary>
    public string TaskMissingText(SettlementTaskDefinition task)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));
        int goldShort = task.RequiredGold - GoldBalance;
        if (task.RequiredGold > 0 && goldShort > 0)
            return $"골드가 {goldShort:N0} 모자라요.";
        foreach (var cost in task.RequiredItems)
        {
            if (cost == null || cost.Item == null)
                continue;
            int itemShort = cost.Amount - ItemCount(cost.Item);
            if (itemShort > 0)
                return $"{cost.Item.DisplayName} {itemShort}개가 더 필요해요.";
        }
        return null;
    }

    #endregion

    #region 전문 해달 (광부·농부)

    public SettlementOtterDefinition FindSpecialist(DevelopableRegionDefinition region) => _config.FindSpecialist(region);

    /// <summary>찾아와서 배치를 기다리는 전문 해달 (온 순서대로 첫 번째. 없으면 null)</summary>
    public SettlementOtterDefinition VisitingSpecialist
    {
        get
        {
            foreach (var otterId in Settlement.ResidentOrder)
            {
                var otter = _config.FindOtter(otterId);
                if (otter != null && IsVisitingPlaza(otter))
                    return otter;
            }
            return null;
        }
    }

    public SpecialistState GetSpecialistState(SettlementOtterDefinition otter) =>
        otter != null ? Settlement.GetSpecialistState(otter.OtterId) : SpecialistState.NotArrived;

    /// <summary>광장에 나와야 하는 전문 해달인지 (찾아왔고 아직 배치 전)</summary>
    public bool IsVisitingPlaza(SettlementOtterDefinition otter) =>
        otter != null && SettlementRegionRules.IsVisitingPlaza(otter, Settlement);

    /// <summary>광장에서 말을 걸면 배치할 수 있는지</summary>
    public bool CanAssignSpecialist(SettlementOtterDefinition otter) =>
        otter != null && SettlementRegionRules.CanAssign(otter, Settlement);

    /// <summary>
    /// 이 장소(ZoneId, 예: Mine)에서 생산할 수 있는지. 개간 지역이 없는 장소는 늘 됨.
    /// 정착 진행을 아직 불러오지 않았으면 false (불러온 뒤 다시 봄)
    /// </summary>
    public bool CanProduceIn(string zoneId)
    {
        if (!IsLoaded)
            return false;
        var region = _config.FindRegionByZoneId(zoneId);
        return region == null || SettlementRegionRules.CanProduce(region, _config.FindSpecialist(region), Settlement);
    }

    /// <summary>이 장소의 지역은 운영 중인데 전문 해달을 아직 배치하지 않았으면 그 해달 (아니면 null)</summary>
    public SettlementOtterDefinition WaitingSpecialist(ZoneDefinition zone)
    {
        var region = zone != null ? _config.FindRegion(zone) : null;
        if (region == null)
            return null;
        var specialist = _config.FindSpecialist(region);
        return SettlementRegionRules.IsWaitingForSpecialist(region, specialist, Settlement) ? specialist : null;
    }

    /// <summary>배치했지만 아직 일하는 곳에 가 보지 않은 전문 해달 (안내 띠 "광산에 가서 일을 시작해요". 없으면 null)</summary>
    public SettlementOtterDefinition AssignedNotWorking
    {
        get
        {
            foreach (var otter in _config.Otters)
            {
                if (otter != null && otter.IsSpecialist && GetSpecialistState(otter) == SpecialistState.Assigned)
                    return otter;
            }
            return null;
        }
    }

    /// <summary>이 장소에 배치되었지만 아직 일을 시작하지 않은 전문 해달 (생산 안내를 기다림. 없으면 null)</summary>
    public SettlementOtterDefinition AssignedSpecialist(string zoneId)
    {
        var region = _config.FindRegionByZoneId(zoneId);
        var specialist = region != null ? _config.FindSpecialist(region) : null;
        return specialist != null && GetSpecialistState(specialist) == SpecialistState.Assigned ? specialist : null;
    }

    /// <summary>
    /// 광장에서 해달을 처음 만남 (광장이 그 해달을 처음 내보낼 때). 전문 해달은 광장에 와 있는 처지가 된다.
    /// 같은 해달은 한 번만 센다 (재방문·씬 재진입)
    /// </summary>
    public void Meet(SettlementOtterDefinition otter)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        var before = Settlement.GetSpecialistState(otter.OtterId);
        bool first = SettlementRegionRules.Meet(otter, Settlement);
        if (!first && Settlement.GetSpecialistState(otter.OtterId) == before)
            return;
        if (first)
            OnOtterMet?.Invoke(otter.OtterId);
        SaveRequested?.Invoke();
    }

    /// <summary>새 해달 만나기 퀘스트가 세는 수: 광장에서 만난 해달 (처음부터 함께한 첫 해달은 뺌)</summary>
    public int MetOtterCount
    {
        get
        {
            int count = 0;
            string first = _config.FirstOtter != null ? _config.FirstOtter.OtterId : null;
            foreach (var otterId in Settlement.MetOtters)
            {
                if (otterId != first)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// 광장에 와 있는 전문 해달을 일할 지역에 배치 (광장에서 해달과 대화). 배치는 바로 저장되고,
    /// 그 해달의 배치 부탁이 끝나며, 도감에 등록된다. 여러 번 눌러도 한 번만 된다
    /// </summary>
    /// <returns>이번에 배치했으면 true</returns>
    public bool TryAssignSpecialist(SettlementOtterDefinition otter)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        if (!SettlementRegionRules.Assign(otter, Settlement))
            return false;

        // 파견이 저장되는 순간 열리는 발전 (농부 → 요정 방문 예약). 배치와 같은 저장에 남음
        if (!string.IsNullOrEmpty(otter.AssignDevelopment))
            Settlement.UnlockDevelopment(otter.AssignDevelopment);
        if (otter.CollectionEntry != null && CollectionManager.Instance != null)
            CollectionManager.Instance.Register(otter.CollectionEntry.EntryId);
        OnSpecialistAssigned?.Invoke(otter);

        bool completed = false;
        foreach (var request in _config.Requests)
        {
            if (request != null && request.AssignSpecialist == otter && !Settlement.IsCompleted(request.RequestId))
            {
                // Complete가 저장까지 부탁함
                Complete(request);
                completed = true;
            }
        }
        if (!completed)
            SaveRequested?.Invoke();
        return true;
    }

    /// <summary>이 장소(ZoneId)에 배치된 전문 해달이 일하기 시작함 (그 장소의 생산 안내를 끝냈을 때)</summary>
    /// <returns>이번에 시작했으면 true</returns>
    public bool StartSpecialistWork(string zoneId)
    {
        var specialist = AssignedSpecialist(zoneId);
        if (specialist == null || !SettlementRegionRules.StartWork(specialist, Settlement, NowTicks))
            return false;
        SaveRequested?.Invoke();
        return true;
    }

    /// <summary>
    /// 배치한 전문 해달을 도감에 등록 (옛 세이브를 옮긴 경우 등). 도감은 정착보다 늦게 불러오므로
    /// GameManager가 도감을 불러온 뒤에 부른다. 이미 등록했으면 그대로
    /// </summary>
    public void SyncSpecialistCollection()
    {
        var collection = CollectionManager.Instance;
        if (collection == null || !IsLoaded)
            return;
        foreach (var otter in _config.Otters)
        {
            if (otter != null && otter.CollectionEntry != null && GetSpecialistState(otter) >= SpecialistState.Assigned
                && collection.Collection.GetState(otter.CollectionEntry) != CollectionState.Collected)
                collection.Register(otter.CollectionEntry.EntryId);
        }
    }

    // 전문 해달이 생기기 전 세이브: 이미 운영 중인 지역은 그 해달이 일하는 중으로 (부탁은 조용히 끝냄, 팝업·보상 없음)
    private void MigrateSpecialists()
    {
        foreach (var otter in _config.Otters)
        {
            if (otter == null || !otter.IsSpecialist)
                continue;
            bool assignDone = false;
            foreach (var request in _config.Requests)
            {
                if (request != null && request.AssignSpecialist == otter && Settlement.IsCompleted(request.RequestId))
                    assignDone = true;
            }
            if (!SettlementRegionRules.MigrateLegacy(otter, assignDone, Settlement, NowTicks))
                continue;
            foreach (var request in _config.Requests)
            {
                if (request != null && request.AssignSpecialist == otter)
                    SettlementRules.ApplyCompletion(request, Settlement);
            }
            Debug.Log($"[SettlementManager] 옛 세이브: '{otter.DisplayName}'을(를) 이미 운영 중인 {otter.WorkRegion.DisplayName}에서 일하는 중으로 옮겼습니다.");
        }
    }

    #endregion

    #region 관리 해달 (게시판 관리)

    /// <summary>이 해달이 맡는 관리 역할 (관리 해달이 아니면 null)</summary>
    public ManagementRoleDefinition FindRole(SettlementOtterDefinition otter) => _config.FindRoleFor(otter);

    /// <summary>광장에서 말을 걸면 역할을 맡길 수 있는지 (만났고, 앞선 발전이 열렸고, 아직 아무도 안 맡음)</summary>
    public bool CanAssignRole(SettlementOtterDefinition otter)
    {
        var role = FindRole(otter);
        return role != null && SettlementRoleRules.CanAssign(role, Settlement);
    }

    /// <summary>이 해달이 역할을 맡아 근무 중이면 그 역할 (아니면 null)</summary>
    public ManagementRoleDefinition AssignedRoleOf(SettlementOtterDefinition otter)
    {
        var role = FindRole(otter);
        return role != null && SettlementRoleRules.IsAssigned(role, Settlement) ? role : null;
    }

    /// <summary>
    /// 광장에서 만난 관리 해달에게 역할을 맡긴다 (대화의 확인 버튼). 맡기면 바로 저장되고, 그 역할 부탁이 끝나며,
    /// 도감에 등록된다. 여러 번 눌러도 한 번만 된다. 근무 자리로 걸어가는 것은 광장이 하고, 도착하지 못해도 맡긴 것은 그대로다
    /// </summary>
    /// <returns>이번에 맡겼으면 true</returns>
    public bool TryAssignRole(ManagementRoleDefinition role)
    {
        if (role == null)
            throw new ArgumentNullException(nameof(role));
        if (!SettlementRoleRules.Assign(role, Settlement))
            return false;

        if (role.Otter.CollectionEntry != null && CollectionManager.Instance != null)
            CollectionManager.Instance.Register(role.Otter.CollectionEntry.EntryId);
        OnRoleAssigned?.Invoke(role);

        bool completed = false;
        foreach (var request in _config.Requests)
        {
            if (request != null && request.AssignRole == role && !Settlement.IsCompleted(request.RequestId))
            {
                // Complete가 저장까지 부탁함
                Complete(request);
                completed = true;
            }
        }
        if (!completed)
            SaveRequested?.Invoke();
        return true;
    }

    #endregion

    #region 게시판 성장 · 큰 부탁 · 마을회관

    /// <summary>이 부탁을 하러 가는 화면을 열어 달라고 부탁 (마을회관·큰 부탁의 [가 보기])</summary>
    public void RequestOpen(BoardRequestDefinition request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        OnRequestOpenRequested?.Invoke(request);
    }

    /// <summary>큰 부탁 화면을 연다 (보이는 큰 부탁일 때만)</summary>
    public void RequestMilestone(MilestoneGroupDefinition group)
    {
        if (group == null)
            throw new ArgumentNullException(nameof(group));
        if (SettlementBoardRules.IsGroupVisible(group, Settlement))
            OnMilestoneRequested?.Invoke(group);
    }

    /// <summary>마을회관이 생긴 뒤 발전 현황 화면을 연다</summary>
    public void RequestTownHall()
    {
        if (IsTownHallOpen)
            OnTownHallRequested?.Invoke();
    }

    /// <summary>마을회관이 있어 발전 현황 화면을 볼 수 있는지</summary>
    public bool IsTownHallOpen => !string.IsNullOrEmpty(_config.TownHallDevelopment) && Settlement.HasDevelopment(_config.TownHallDevelopment);

    /// <summary>지금 보이는 큰 부탁 (없으면 null)</summary>
    public MilestoneGroupDefinition VisibleMilestone
    {
        get
        {
            foreach (var group in _config.MilestoneGroups)
            {
                if (SettlementBoardRules.IsGroupVisible(group, Settlement))
                    return group;
            }
            return null;
        }
    }

    /// <summary>준비된 메인 발전을 모두 끝냈는지</summary>
    public bool AreAllMainDone => SettlementBoardRules.AreAllMainDone(_config, Settlement);

    /// <summary>왕국 레벨이 모자라 아직 안 보이는 다음 메인 부탁 (없으면 null). level = 열리는 레벨</summary>
    public BoardRequestDefinition FindLevelLockedRequest(out int level) => SettlementRules.FindLevelLocked(_config, Settlement, out level);

    #endregion

    #region 건설 기록 (퀘스트)

    /// <summary>
    /// 다 지은 건물 (끝낸 부탁의 건설 중 개간이 아닌 것). 퀘스트가 이 기록으로 다시 센다.
    /// 끝낸 부탁 = 저장된 고유 기록이라 같은 건물을 두 번 세지 않는다
    /// </summary>
    public void CollectCompletedBuildings(List<ConstructionDefinition> result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        result.Clear();
        foreach (var request in _config.Requests)
        {
            var construction = request != null ? request.Construction : null;
            if (construction != null && construction.TargetType != ConstructionTarget.Clearing
                && Settlement.IsCompleted(request.RequestId) && !result.Contains(construction))
                result.Add(construction);
        }
    }

    #endregion

    #region 자리를 비운 동안

    /// <summary>
    /// 돌아옴 팝업의 마을 소식: 비운 동안 다 지은 건설, 아직 짓는 중인 건설, 다시 생긴 줍기 자리.
    /// 다 지은 건설은 한 번만 알린다. 소식이 없으면 아무것도 넣지 않는다
    /// </summary>
    public void CollectAbsenceNews(double absenceSeconds, List<string> lines)
    {
        if (lines == null)
            throw new ArgumentNullException(nameof(lines));
        if (!IsLoaded)
            return;

        long now = NowTicks;
        long leftAt = now - TimeSpan.FromSeconds(Math.Max(0, absenceSeconds)).Ticks;

        // 백그라운드에서 돌아오면 Update보다 먼저 불려 아직 안 끝났을 수 있음
        if (Settlement.Job != null && Settlement.Job.IsDue(now))
            FinishJob();

        foreach (var (request, endTicks) in _finishedJobs)
        {
            if (endTicks < leftAt)
                continue;
            // 완료 문구는 첫 줄만 (팝업 한 줄에 들어가게)
            string done = $"{request.Construction.DisplayName} 완성!";
            string message = string.IsNullOrEmpty(request.CompletionMessage) ? null : request.CompletionMessage.Split('\n')[0];
            lines.Add(message == null ? done : $"{done} {message}");
        }
        _finishedJobs.Clear();

        var building = JobRequest;
        if (building != null)
            lines.Add($"{building.Construction.ProgressLabel} · {FormatRemaining(Settlement.Job.Remaining(now))} 남았어요");

        FinishDueTasks();
        foreach (var (task, endTicks) in _finishedTasks)
        {
            if (endTicks >= leftAt)
                lines.Add(string.IsNullOrEmpty(task.CompletionMessage) ? $"{task.Title} 끝!" : task.CompletionMessage);
        }
        _finishedTasks.Clear();
        foreach (var job in Settlement.TaskJobs)
        {
            var task = _config.FindTask(job.TaskId);
            if (task != null)
                lines.Add($"{task.Title} · {FormatRemaining(job.Remaining(now))} 남았어요");
        }

        int regrown = Settlement.CountGatherRegrown(leftAt, now);
        if (regrown > 0)
            lines.Add($"광장에 주울 나뭇가지·돌이 {regrown}곳 다시 생겼어요");

        CollectBuildingNews(leftAt, now, lines);
        CollectVisitNews(lines);
    }

    /// <summary>돌아옴 팝업 맨 아래의 다음 할 일 (지금 할 부탁 또는 정착 후보의 이야기. 짓는 중이거나 다 끝냈으면 null)</summary>
    public string NextGoalTitle
    {
        get
        {
            if (!IsLoaded || Settlement.Job != null)
                return null;
            var current = CurrentRequest;
            if (current != null)
                return current.Title;
            var visiting = VisitingSpecialist;
            if (visiting != null)
                return $"광장에서 {visiting.DisplayName} 만나기";
            var intro = PendingIntroOtter;
            return intro != null ? $"{intro.DisplayName}의 이야기 듣기" : null;
        }
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        if (seconds >= 3600)
            return $"{seconds / 3600}시간 {seconds / 60 % 60}분";
        return seconds >= 60 ? $"{(seconds + 59) / 60}분" : $"{seconds}초";
    }

    #endregion

    #region 세이브

    /// <summary>게임 시작 시 한 번. 새 게임이면 첫 해달이 오고 시작 재료를 받는다</summary>
    /// <param name="fairyShopSeen">요정 상점 안내를 이미 봤는지 (P3 전 세이브에서 요정을 그대로 두기 위해. GameManager의 튜토리얼 기록)</param>
    public void LoadFromSave(SettlementSaveData saved, bool fairyShopSeen = false)
    {
        if (saved == null)
            throw new ArgumentNullException(nameof(saved));

        Settlement.Load(saved);

        if (Settlement.LegacyComplete)
        {
            SettlementRules.CompleteAll(_config, Settlement);
        }
        else if (!Settlement.Initialized)
        {
            SettlementRules.InitializeNewGame(_config, Settlement);
            GrantStartingItems();
        }

        // 지역·작업·전문 해달을 먼저 맞춘 뒤 (옛 세이브 옮기기) 꺼 둔 동안 끝난 건설·주민 작업을 처리
        MigrateSpecialists();
        MigrateVersion(fairyShopSeen);
        MigrateTerritory();
        ReconcileRecords();
        ReconcileBuildings();
        ReconcilePlots();
        SyncLevelDevelopments(false);
        IsLoaded = true;
        // 개발 메뉴 Fast Timers가 켜져 있으면 세이브에 남은 긴 건설·작업도 5초 안으로
        if (DevTimers.Fast)
            Settlement.ShortenTimers(NowTicks, TimeSpan.FromSeconds(DevTimers.FastSeconds));
        if (Settlement.Job != null && Settlement.Job.IsDue(NowTicks))
            FinishJob();
        FinishDueTasks();
        FinishDueBuildings(false);
        SyncRegionDecor();
        // 공동사업: 기록에서 단계·완료를 다시 계산 (꺼진 사이 끝난 건설·정비, 주다 만 보상). 알림 없이
        UpdateProjects(true);

        ApplyLevelCap();
        OnLoaded?.Invoke();
        OnChanged?.Invoke();
    }

    // 세이브 버전을 한 번 옮김 (P2·P3 콘텐츠는 자동으로 주지 않음. P3 전 세이브에서 이미 와 있던 요정은 그대로)
    private void MigrateVersion(bool fairyShopSeen)
    {
        int from = Settlement.Version;
        if (SettlementMigration.MigrateFairy(_config, Settlement, from, fairyShopSeen))
            Debug.Log("[SettlementManager] P3 전 세이브: 이미 광장에 있던 요정을 그대로 둡니다.");
        if (SettlementMigration.MigrateToCurrent(Settlement) && from > 0)
            Debug.Log($"[SettlementManager] 정착 세이브를 버전 {from} → {SettlementSaveData.CurrentVersion}으로 옮겼습니다.");
    }

    // 끝낸 주민 작업·맡긴 역할은 있는데 부탁이 안 끝난 세이브 (끝내는 도중에 꺼짐): 조용히 끝냄 (팝업·보상 없음)
    private void ReconcileRecords()
    {
        var completed = new List<BoardRequestDefinition>();
        SettlementMigration.ReconcileRecords(_config, Settlement, completed);
        foreach (var request in completed)
            Debug.Log($"[SettlementManager] 기록에 맞춰 부탁 '{request.RequestId}'을(를) 끝낸 것으로 맞췄습니다.");
        // 파견은 했는데 파견 발전(요정 방문 예약)이 없는 세이브: 한 번 맞춤
        foreach (var otter in SettlementMigration.ReconcileAssignDevelopments(_config, Settlement))
            Debug.Log($"[SettlementManager] 기록에 맞춰 '{otter.DisplayName}' 파견 발전 '{otter.AssignDevelopment}'을(를) 열었습니다.");
        // 옛 세이브에 따로 열어 준 장소(낚시터)를 짓는 부탁: 이미 열려 있으니 조용히 끝냄 (선착장을 다시 짓지 않게)
        completed.Clear();
        SettlementMigration.ReconcileLegacyConstructions(_config, Settlement, completed);
        foreach (var request in completed)
            Debug.Log($"[SettlementManager] 옛 세이브: 이미 열린 '{request.Construction.UnlockResultId}'의 부탁 '{request.RequestId}'을(를) 끝낸 것으로 맞췄습니다.");
    }

    private readonly List<LevelDevelopment> _openedByLevel = new List<LevelDevelopment>();

    // 왕국 레벨에 닿은 발전을 연다. notify: 플레이 중 레벨이 올라 열렸으면 화면 위쪽 알림
    private void SyncLevelDevelopments(bool notify)
    {
        SettlementRules.UnlockLevelDevelopments(_config, Settlement, PlayerLevel, _openedByLevel);
        if (_openedByLevel.Count == 0)
            return;
        if (notify)
        {
            foreach (var entry in _openedByLevel)
                GameNotices.Post(new GameNotice(entry.Notice));
        }
        SaveRequested?.Invoke();
    }

    private void GrantStartingItems()
    {
        if (_config.StartingGold > 0 && _config.GoldCurrency != null && CurrencyManager.Instance != null)
            CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(_config.GoldCurrency, _config.StartingGold, TransactionSource.StartingGrant));

        if (InventoryManager.Instance == null)
            return;
        var bag = InventoryManager.Instance.Inventory;
        foreach (var grant in _config.StartingItems)
        {
            if (grant != null && grant.Item != null && grant.Amount > 0)
                bag.Add(grant.Item, grant.Amount, ItemChangeReason.Grant);
        }
    }

    public void WriteToSave(SettlementSaveData result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        Settlement.Write(result);
    }

    #endregion

    #region 개발용

    /// <summary>진행 중인 건설을 바로 끝낸다 (테스트용)</summary>
    public void DevFinishJob()
    {
        if (Settlement.Job != null)
            FinishJob();
    }

    /// <summary>진행 중인 건설·주민 작업이 길어도 DevTimers.FastSeconds 안에 끝나게 줄인다 (개발 메뉴 Fast Timers를 켤 때)</summary>
    public void DevShortenTimers()
    {
        if (IsLoaded && Settlement.ShortenTimers(NowTicks, TimeSpan.FromSeconds(DevTimers.FastSeconds)))
            SaveRequested?.Invoke();
    }

    /// <summary>진행 중인 주민 작업을 모두 바로 끝낸다 (테스트용)</summary>
    public void DevFinishTasks()
    {
        _dueTasks.Clear();
        _dueTasks.AddRange(Settlement.TaskJobs);
        foreach (var job in _dueTasks)
            FinishTask(job);
    }

    #endregion
}
