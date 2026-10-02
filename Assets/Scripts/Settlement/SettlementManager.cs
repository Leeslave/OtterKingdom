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
}

/// <summary>
/// 정착 진행(Settlement)의 주인. 전역 UI(GlobalUI) 루트에 붙어 씬을 넘어 유지된다.
/// 게시판 부탁의 건설 비용(골드·재료)을 받고, 시간이 지나면 완료 처리하고, 장소 해금(밭 등)을 알려 준다.
/// 기존 시스템(밭·가방·재화·퀘스트) 앞에서 진행만 제어하는 상위 레이어 — 그 내부 로직은 건드리지 않는다.
/// - 세이브: GameManager가 LoadFromSave / WriteToSave를 호출. 부탁을 끝내면 SaveRequested로 바로 저장을 부탁한다
/// </summary>
// 매니저들(-100)이 준비된 뒤, GameManager가 세이브를 불러오기 전에 준비
[DefaultExecutionOrder(-80)]
public class SettlementManager : MonoBehaviour
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

    public static long NowTicks => DateTime.UtcNow.Ticks;

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

    // 끝난 건설 (부탁, 끝난 시각). 돌아옴 팝업이 자리를 비운 동안 끝난 것만 골라 알리고 비운다
    private readonly List<(BoardRequestDefinition request, long endTicks)> _finishedJobs = new List<(BoardRequestDefinition, long)>();

    private void Awake()
    {
        // 중복은 GlobalUIRoot가 먼저 꺼서 여기까지 오지 않지만, 혹시 모를 경우를 대비
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        Settlement = new Settlement();
        Settlement.OnChanged += () => OnChanged?.Invoke();
        Settlement.OnDevelopmentUnlocked += id => OnDevelopmentUnlocked?.Invoke(id);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (IsLoaded && Settlement.Job != null && Settlement.Job.IsDue(NowTicks))
            FinishJob();
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

    /// <summary>시작할 수 있는데 아직 손대지 않은 부탁이 있는지 (게시판 빨간 점)</summary>
    public bool HasActionableRequest
    {
        get
        {
            foreach (var request in _config.Requests)
            {
                if (request != null && GetStatus(request) == RequestStatus.Available)
                    return true;
            }
            return false;
        }
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

    public bool HasEnoughItems(ConstructionDefinition construction)
    {
        foreach (var cost in construction.RequiredItems)
        {
            if (cost != null && cost.Item != null && ItemCount(cost.Item) < cost.Amount)
                return false;
        }
        return true;
    }

    #endregion

    #region 게시판

    /// <summary>게시판을 연다 (화면이 열고, 처음 본 것으로 표시)</summary>
    public void RequestBoard(bool openRequests)
    {
        Settlement.MarkBoardVisited();
        OnBoardRequested?.Invoke(openRequests);
    }

    /// <summary>부탁의 건설을 시작한다. 비용을 내고, 시간이 0이면 바로 끝낸다</summary>
    public ConstructionStartResult TryStart(BoardRequestDefinition request)
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
        if (construction.NeedsBuilder && !HasBuilder)
            return ConstructionStartResult.NoBuilder;
        if (!HasEnoughGold(construction))
            return ConstructionStartResult.NotEnoughGold;
        if (!HasEnoughItems(construction))
            return ConstructionStartResult.NotEnoughItems;

        PayCost(construction);

        if (construction.IsInstant)
        {
            Complete(request);
            return ConstructionStartResult.Completed;
        }

        long now = NowTicks;
        long end = now + TimeSpan.FromSeconds(construction.DurationSeconds).Ticks;
        Settlement.StartJob(request.RequestId, construction.ConstructionId, now, end);
        OnConstructionStarted?.Invoke(request);
        SaveRequested?.Invoke();
        return ConstructionStartResult.Started;
    }

    // 확인을 다 한 뒤에만 부름: 골드 → 재료 순서로 낸다
    private void PayCost(ConstructionDefinition construction)
    {
        if (construction.RequiredGold > 0)
            CurrencyManager.Instance.TrySpend(_config.GoldCurrency, construction.RequiredGold, TransactionSource.Construction);

        var bag = InventoryManager.Instance.Inventory;
        foreach (var cost in construction.RequiredItems)
        {
            if (cost != null && cost.Item != null && cost.Amount > 0)
                bag.TryRemove(cost.Item, cost.Amount, ItemChangeReason.Construction);
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
        SaveRequested?.Invoke();
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

    #region 줍기 (나뭇가지 → 목재, 돌무더기 → 돌)

    public bool IsGatherReady(string pointId) => Settlement.IsGatherReady(pointId, NowTicks);

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

        int regrown = Settlement.CountGatherRegrown(leftAt, now);
        if (regrown > 0)
            lines.Add($"광장에 주울 나뭇가지·돌이 {regrown}곳 다시 생겼어요");
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
    public void LoadFromSave(SettlementSaveData saved)
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

        IsLoaded = true;
        // 꺼 둔 동안 끝난 건설
        if (Settlement.Job != null && Settlement.Job.IsDue(NowTicks))
            FinishJob();

        OnLoaded?.Invoke();
        OnChanged?.Invoke();
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

    #endregion
}
