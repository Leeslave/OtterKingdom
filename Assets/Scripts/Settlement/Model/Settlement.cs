using System;
using System.Collections.Generic;

/// <summary>
/// 진행 중인 건설. 일할 해달이 현장에 도착하기 전(WaitingForWorker)에는 시간이 흐르지 않는다:
/// 시작·끝 시각은 걸리는 시간만 담고, 도착하면 그때부터 다시 잰다 (Settlement.BeginJobWork).
/// </summary>
public class ConstructionJob
{
    public string RequestId { get; }
    public string ConstructionId { get; }
    public long StartUtcTicks { get; }
    public long EndUtcTicks { get; }

    /// <summary>일할 해달이 아직 현장에 가는 중 (시간이 흐르지 않음)</summary>
    public bool WaitingForWorker { get; }

    public ConstructionJob(string requestId, string constructionId, long startUtcTicks, long endUtcTicks, bool waitingForWorker = false)
    {
        RequestId = requestId;
        ConstructionId = constructionId;
        StartUtcTicks = startUtcTicks;
        EndUtcTicks = endUtcTicks;
        WaitingForWorker = waitingForWorker;
    }

    /// <summary>걸리는 시간 (도착 전에도 같음)</summary>
    public TimeSpan Duration => TimeSpan.FromTicks(Math.Max(0, EndUtcTicks - StartUtcTicks));

    /// <summary>0~1 진행 비율</summary>
    public float Progress(long nowUtcTicks)
    {
        if (WaitingForWorker)
            return 0f;
        long total = EndUtcTicks - StartUtcTicks;
        if (total <= 0)
            return 1f;
        double ratio = (double)(nowUtcTicks - StartUtcTicks) / total;
        return (float)Math.Max(0d, Math.Min(1d, ratio));
    }

    public TimeSpan Remaining(long nowUtcTicks) =>
        WaitingForWorker ? Duration : TimeSpan.FromTicks(Math.Max(0, EndUtcTicks - nowUtcTicks));

    public bool IsDue(long nowUtcTicks) => !WaitingForWorker && nowUtcTicks >= EndUtcTicks;
}

/// <summary>전문 해달 한 마리의 기록 (처지, 배치한 지역, 일하기 시작한 시각)</summary>
public readonly struct SpecialistRecord
{
    public readonly SpecialistState State;
    public readonly string RegionId;
    public readonly long WorkingSinceUtcTicks;

    public SpecialistRecord(SpecialistState state, string regionId, long workingSinceUtcTicks)
    {
        State = state;
        RegionId = regionId;
        WorkingSinceUtcTicks = workingSinceUtcTicks;
    }
}

/// <summary>관리 역할을 맡은 해달 하나 (역할, 해달, 근무 자리)</summary>
public readonly struct RoleAssignment
{
    public readonly string RoleId;
    public readonly string OtterId;
    public readonly string StationId;

    public RoleAssignment(string roleId, string otterId, string stationId)
    {
        RoleId = roleId;
        OtterId = otterId;
        StationId = stationId;
    }
}

/// <summary>
/// 정착 진행 상태 (순수 C#): 왕국 단계, 해달별 처지, 끝낸 부탁, 열린 발전, 방명록, 진행 중인 건설, 주민 작업, 전문 해달, 만난 해달, 관리 역할.
/// 규칙(언제 무엇이 열리는지)은 SettlementRules, 비용·시간은 SettlementManager가 다룬다.
/// </summary>
public class Settlement
{
    private readonly Dictionary<string, ResidentState> _residents = new Dictionary<string, ResidentState>();
    // 온 순서대로 (광장에 나오는 순서, 세이브 순서를 지키기 위해)
    private readonly List<string> _residentOrder = new List<string>();
    private readonly HashSet<string> _completedRequests = new HashSet<string>();
    private readonly HashSet<string> _developments = new HashSet<string>();
    private readonly List<string> _guestbook = new List<string>();
    private readonly Dictionary<string, long> _gatherReady = new Dictionary<string, long>();
    private readonly HashSet<string> _flags = new HashSet<string>();
    private readonly Dictionary<string, SettlementTaskJob> _tasks = new Dictionary<string, SettlementTaskJob>();
    private readonly HashSet<string> _completedTasks = new HashSet<string>();
    private readonly Dictionary<string, SpecialistRecord> _specialists = new Dictionary<string, SpecialistRecord>();
    private readonly HashSet<string> _metOtters = new HashSet<string>();
    // 역할 ID → 맡은 해달
    private readonly Dictionary<string, RoleAssignment> _roles = new Dictionary<string, RoleAssignment>();

    /// <summary>세이브 버전 (SettlementSaveData.CurrentVersion까지 옮겼는지)</summary>
    public int Version { get; private set; }
    public int Stage { get; private set; }
    public bool Initialized { get; private set; }
    public bool LegacyComplete { get; private set; }
    public bool BoardVisited { get; private set; }
    public ConstructionJob Job { get; private set; }

    public IReadOnlyList<string> ResidentOrder => _residentOrder;
    public IReadOnlyList<string> Guestbook => _guestbook;
    public IReadOnlyCollection<string> Developments => _developments;
    /// <summary>진행 중인 주민 작업</summary>
    public IReadOnlyCollection<SettlementTaskJob> TaskJobs => _tasks.Values;
    /// <summary>광장에서 처음 만난 해달</summary>
    public IReadOnlyCollection<string> MetOtters => _metOtters;
    /// <summary>끝낸 부탁</summary>
    public IReadOnlyCollection<string> CompletedRequests => _completedRequests;
    /// <summary>관리 역할을 맡은 해달</summary>
    public IReadOnlyCollection<RoleAssignment> Roles => _roles.Values;

    /// <summary>주민 수 (Resident만)</summary>
    public int ResidentCount
    {
        get
        {
            int count = 0;
            foreach (var state in _residents.Values)
            {
                if (state == ResidentState.Resident)
                    count++;
            }
            return count;
        }
    }

    public event Action<int> OnStageChanged;
    public event Action<string, ResidentState> OnResidentChanged;
    public event Action<string> OnRequestCompleted;
    public event Action<string> OnDevelopmentUnlocked;
    public event Action<string> OnGuestbookAdded;
    public event Action<ConstructionJob> OnConstructionStarted;
    public event Action<ConstructionJob> OnConstructionFinished;
    public event Action<SettlementTaskJob> OnTaskStarted;
    public event Action<SettlementTaskJob> OnTaskFinished;
    /// <summary>무엇이든 바뀌었을 때 (화면 갱신용)</summary>
    public event Action OnChanged;

    #region 조회

    public bool TryGetResidentState(string otterId, out ResidentState state) => _residents.TryGetValue(otterId, out state);

    public bool IsCompleted(string requestId) => _completedRequests.Contains(requestId);

    /// <summary>비어 있으면 늘 열림</summary>
    public bool HasDevelopment(string developmentId) => string.IsNullOrEmpty(developmentId) || _developments.Contains(developmentId);

    public bool HasGuestbook(string entryId) => _guestbook.Contains(entryId);

    /// <summary>한 번만 보이는 안내 등을 이미 봤는지</summary>
    public bool HasFlag(string flag) => _flags.Contains(flag);

    /// <summary>이 자리의 나뭇가지가 있는지</summary>
    public bool IsGatherReady(string pointId, long nowUtcTicks) =>
        !_gatherReady.TryGetValue(pointId, out long ready) || nowUtcTicks >= ready;

    /// <summary>이 자리가 다시 생기기까지 남은 시간 (이미 있으면 0)</summary>
    public TimeSpan GatherRemaining(string pointId, long nowUtcTicks) =>
        _gatherReady.TryGetValue(pointId, out long ready) && ready > nowUtcTicks
            ? TimeSpan.FromTicks(ready - nowUtcTicks)
            : TimeSpan.Zero;

    public bool IsTaskCompleted(string taskId) => _completedTasks.Contains(taskId);

    /// <summary>전문 해달의 기록 (아직 없으면 false)</summary>
    public bool TryGetSpecialist(string otterId, out SpecialistRecord record)
    {
        record = default;
        return !string.IsNullOrEmpty(otterId) && _specialists.TryGetValue(otterId, out record);
    }

    /// <summary>전문 해달의 처지 (기록이 없으면 NotArrived)</summary>
    public SpecialistState GetSpecialistState(string otterId) =>
        TryGetSpecialist(otterId, out var record) ? record.State : SpecialistState.NotArrived;

    public bool HasMet(string otterId) => !string.IsNullOrEmpty(otterId) && _metOtters.Contains(otterId);

    /// <summary>진행 중인 작업 (없으면 false)</summary>
    public bool TryGetTaskJob(string taskId, out SettlementTaskJob job)
    {
        job = null;
        return !string.IsNullOrEmpty(taskId) && _tasks.TryGetValue(taskId, out job);
    }

    /// <summary>이 해달이 지금 작업 중인지</summary>
    public ResidentWorkState GetWorkState(string otterId) =>
        FindTaskJobOf(otterId) != null ? ResidentWorkState.Working : ResidentWorkState.Idle;

    /// <summary>이 해달이 하고 있는 작업 (없으면 null)</summary>
    public SettlementTaskJob FindTaskJobOf(string otterId)
    {
        foreach (var job in _tasks.Values)
        {
            if (job.HasOtter(otterId))
                return job;
        }
        return null;
    }

    /// <summary>이 역할을 맡은 해달 (아직 아무도 없으면 false)</summary>
    public bool TryGetRole(string roleId, out RoleAssignment assignment)
    {
        assignment = default;
        return !string.IsNullOrEmpty(roleId) && _roles.TryGetValue(roleId, out assignment);
    }

    /// <summary>이 해달이 맡은 역할 (없으면 false)</summary>
    public bool TryGetRoleOf(string otterId, out RoleAssignment assignment)
    {
        foreach (var role in _roles.Values)
        {
            if (role.OtterId == otterId)
            {
                assignment = role;
                return true;
            }
        }
        assignment = default;
        return false;
    }

    /// <summary>관리 역할을 맡은 해달인지</summary>
    public bool HasRole(string otterId) => !string.IsNullOrEmpty(otterId) && TryGetRoleOf(otterId, out _);

    /// <summary>from 뒤로 to까지 사이에 다시 생긴 줍기 자리 수 (자리를 비운 동안의 소식)</summary>
    public int CountGatherRegrown(long fromUtcTicks, long toUtcTicks)
    {
        int count = 0;
        foreach (long ready in _gatherReady.Values)
        {
            if (ready > fromUtcTicks && ready <= toUtcTicks)
                count++;
        }
        return count;
    }

    #endregion

    #region 바꾸기

    public void MarkInitialized()
    {
        Initialized = true;
        OnChanged?.Invoke();
    }

    public void MarkBoardVisited()
    {
        if (BoardVisited)
            return;
        BoardVisited = true;
        OnChanged?.Invoke();
    }

    public void ClearLegacyFlag() => LegacyComplete = false;

    public void SetStage(int stage)
    {
        if (stage < 0)
            throw new ArgumentOutOfRangeException(nameof(stage));
        if (Stage == stage)
            return;
        Stage = stage;
        OnStageChanged?.Invoke(stage);
        OnChanged?.Invoke();
    }

    /// <summary>해달의 처지를 정한다 (처음 오는 해달이면 온 순서 끝에 붙음)</summary>
    public void SetResident(string otterId, ResidentState state)
    {
        if (string.IsNullOrEmpty(otterId))
            throw new ArgumentNullException(nameof(otterId));

        if (_residents.TryGetValue(otterId, out var current) && current == state)
            return;
        if (!_residents.ContainsKey(otterId))
            _residentOrder.Add(otterId);

        _residents[otterId] = state;
        OnResidentChanged?.Invoke(otterId, state);
        OnChanged?.Invoke();
    }

    /// <returns>새로 남겼으면 true (같은 기록은 한 번만)</returns>
    public bool AddGuestbook(string entryId)
    {
        if (string.IsNullOrEmpty(entryId) || _guestbook.Contains(entryId))
            return false;
        _guestbook.Add(entryId);
        OnGuestbookAdded?.Invoke(entryId);
        OnChanged?.Invoke();
        return true;
    }

    /// <param name="waitingForWorker">true면 일할 해달이 도착할 때까지 시간이 흐르지 않음 (BeginJobWork)</param>
    public void StartJob(string requestId, string constructionId, long startUtcTicks, long endUtcTicks, bool waitingForWorker = false)
    {
        if (Job != null)
            throw new InvalidOperationException("이미 진행 중인 건설이 있습니다.");
        if (string.IsNullOrEmpty(requestId))
            throw new ArgumentNullException(nameof(requestId));

        Job = new ConstructionJob(requestId, constructionId, startUtcTicks, endUtcTicks, waitingForWorker);
        OnConstructionStarted?.Invoke(Job);
        OnChanged?.Invoke();
    }

    /// <summary>일할 해달이 현장에 도착: 지금부터 걸리는 시간만큼 잰다</summary>
    /// <returns>기다리던 건설이 있어서 시작했으면 true</returns>
    public bool BeginJobWork(long nowUtcTicks)
    {
        if (Job == null || !Job.WaitingForWorker)
            return false;
        Job = new ConstructionJob(Job.RequestId, Job.ConstructionId, nowUtcTicks, nowUtcTicks + Job.Duration.Ticks);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>진행 중인 건설을 끝낸 것으로 치우고 알린다 (부탁 완료 처리는 따로)</summary>
    public ConstructionJob FinishJob()
    {
        var job = Job;
        if (job == null)
            return null;
        Job = null;
        OnConstructionFinished?.Invoke(job);
        OnChanged?.Invoke();
        return job;
    }

    /// <summary>부탁을 끝낸 것으로 표시하고 발전을 연다 (중복 호출은 무시)</summary>
    /// <returns>새로 끝냈으면 true</returns>
    public bool CompleteRequest(string requestId, string developmentId)
    {
        if (string.IsNullOrEmpty(requestId))
            throw new ArgumentNullException(nameof(requestId));
        if (!_completedRequests.Add(requestId))
            return false;

        if (!string.IsNullOrEmpty(developmentId) && _developments.Add(developmentId))
            OnDevelopmentUnlocked?.Invoke(developmentId);
        OnRequestCompleted?.Invoke(requestId);
        OnChanged?.Invoke();
        return true;
    }

    /// <returns>새로 세웠으면 true</returns>
    public bool SetFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag))
            throw new ArgumentNullException(nameof(flag));
        if (!_flags.Add(flag))
            return false;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>부탁 없이 발전만 연다 (옛 세이브 처리 등)</summary>
    public void UnlockDevelopment(string developmentId)
    {
        if (string.IsNullOrEmpty(developmentId))
            throw new ArgumentNullException(nameof(developmentId));
        if (!_developments.Add(developmentId))
            return;
        OnDevelopmentUnlocked?.Invoke(developmentId);
        OnChanged?.Invoke();
    }

    /// <summary>주민 작업을 시작한다 (보낼 수 있는지는 SettlementRegionRules·SettlementManager가 먼저 확인)</summary>
    public void StartTask(string taskId, IReadOnlyList<string> otterIds, long startUtcTicks, long endUtcTicks)
    {
        if (string.IsNullOrEmpty(taskId))
            throw new ArgumentNullException(nameof(taskId));
        if (otterIds == null || otterIds.Count == 0)
            throw new ArgumentException("보낼 해달이 없습니다.", nameof(otterIds));
        if (_tasks.ContainsKey(taskId) || _completedTasks.Contains(taskId))
            throw new InvalidOperationException($"이미 시작했거나 끝낸 작업입니다: {taskId}");
        foreach (var otterId in otterIds)
        {
            if (GetWorkState(otterId) == ResidentWorkState.Working)
                throw new InvalidOperationException($"이미 작업 중인 해달입니다: {otterId}");
        }

        var job = new SettlementTaskJob(taskId, otterIds, startUtcTicks, endUtcTicks);
        _tasks[taskId] = job;
        OnTaskStarted?.Invoke(job);
        OnChanged?.Invoke();
    }

    /// <summary>작업을 끝낸 것으로 표시하고 결과 발전을 연다. 해달은 다시 쉬는 상태가 됨</summary>
    /// <returns>끝낸 작업 (진행 중이 아니었으면 null)</returns>
    public SettlementTaskJob FinishTask(string taskId, string resultDevelopment)
    {
        if (!TryGetTaskJob(taskId, out var job))
            return null;
        _tasks.Remove(taskId);
        _completedTasks.Add(taskId);
        if (!string.IsNullOrEmpty(resultDevelopment) && _developments.Add(resultDevelopment))
            OnDevelopmentUnlocked?.Invoke(resultDevelopment);
        OnTaskFinished?.Invoke(job);
        OnChanged?.Invoke();
        return job;
    }

    public void SetGatherReady(string pointId, long readyUtcTicks)
    {
        if (string.IsNullOrEmpty(pointId))
            throw new ArgumentNullException(nameof(pointId));
        _gatherReady[pointId] = readyUtcTicks;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 전문 해달의 처지를 앞으로만 옮긴다 (광장 → 배치 → 일함). 같거나 뒤로 가는 호출은 무시해서
    /// 배치 버튼을 여러 번 눌러도 한 번만 바뀐다. 지역·시작 시각은 비어 있으면 원래 값을 지킨다
    /// </summary>
    /// <returns>바뀌었으면 true</returns>
    public bool AdvanceSpecialist(string otterId, SpecialistState state, string regionId = null, long workingSinceUtcTicks = 0)
    {
        if (string.IsNullOrEmpty(otterId))
            throw new ArgumentNullException(nameof(otterId));
        TryGetSpecialist(otterId, out var current);
        if (state <= current.State && _specialists.ContainsKey(otterId))
            return false;

        string region = string.IsNullOrEmpty(regionId) ? current.RegionId : regionId;
        long since = workingSinceUtcTicks > 0 ? workingSinceUtcTicks : current.WorkingSinceUtcTicks;
        _specialists[otterId] = new SpecialistRecord(state, region, since);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>광장에서 이 해달을 처음 만났음</summary>
    /// <returns>처음이면 true (같은 해달은 한 번만)</returns>
    public bool MarkMet(string otterId)
    {
        if (string.IsNullOrEmpty(otterId))
            throw new ArgumentNullException(nameof(otterId));
        if (!_metOtters.Add(otterId))
            return false;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>관리 역할을 해달에게 맡긴다. 이미 누가 맡은 역할이거나 이미 다른 역할을 맡은 해달이면 그대로 (연타해도 한 번만)</summary>
    /// <returns>이번에 맡겼으면 true</returns>
    public bool AssignRole(string roleId, string otterId, string stationId)
    {
        if (string.IsNullOrEmpty(roleId))
            throw new ArgumentNullException(nameof(roleId));
        if (string.IsNullOrEmpty(otterId))
            throw new ArgumentNullException(nameof(otterId));
        if (_roles.ContainsKey(roleId) || HasRole(otterId))
            return false;
        _roles[roleId] = new RoleAssignment(roleId, otterId, stationId);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>세이브를 이 버전까지 옮겼음 (낮추지 않음)</summary>
    public void SetVersion(int version)
    {
        if (version <= Version)
            return;
        Version = version;
        OnChanged?.Invoke();
    }

    #endregion

    #region 세이브

    public void Load(SettlementSaveData saved)
    {
        if (saved == null)
            throw new ArgumentNullException(nameof(saved));

        _residents.Clear();
        _residentOrder.Clear();
        _completedRequests.Clear();
        _developments.Clear();
        _guestbook.Clear();
        _gatherReady.Clear();
        _flags.Clear();
        _tasks.Clear();
        _completedTasks.Clear();
        _specialists.Clear();
        _metOtters.Clear();
        _roles.Clear();
        Job = null;

        Version = Math.Max(0, saved.version);
        Initialized = saved.initialized;
        LegacyComplete = saved.legacyComplete;
        BoardVisited = saved.boardVisited;
        Stage = Math.Max(0, saved.kingdomStage);

        if (saved.completedRequests != null)
        {
            foreach (var id in saved.completedRequests)
            {
                if (!string.IsNullOrEmpty(id))
                    _completedRequests.Add(id);
            }
        }
        if (saved.unlockedDevelopments != null)
        {
            foreach (var id in saved.unlockedDevelopments)
            {
                if (!string.IsNullOrEmpty(id))
                    _developments.Add(id);
            }
        }
        if (saved.residents != null)
        {
            foreach (var r in saved.residents)
            {
                if (r == null || string.IsNullOrEmpty(r.otterId))
                    continue;
                if (!_residents.ContainsKey(r.otterId))
                    _residentOrder.Add(r.otterId);
                _residents[r.otterId] = r.state;
            }
        }
        if (saved.guestbookEntries != null)
        {
            foreach (var id in saved.guestbookEntries)
            {
                if (!string.IsNullOrEmpty(id) && !_guestbook.Contains(id))
                    _guestbook.Add(id);
            }
        }
        var job = saved.construction;
        if (job != null && !string.IsNullOrEmpty(job.requestId))
            Job = new ConstructionJob(job.requestId, job.constructionId, job.startUtcTicks, job.endUtcTicks, job.waitingForWorker);
        if (saved.gatherCooldowns != null)
        {
            foreach (var g in saved.gatherCooldowns)
            {
                if (g != null && !string.IsNullOrEmpty(g.pointId))
                    _gatherReady[g.pointId] = g.readyUtcTicks;
            }
        }
        if (saved.flags != null)
        {
            foreach (var flag in saved.flags)
            {
                if (!string.IsNullOrEmpty(flag))
                    _flags.Add(flag);
            }
        }
        if (saved.completedTasks != null)
        {
            foreach (var id in saved.completedTasks)
            {
                if (!string.IsNullOrEmpty(id))
                    _completedTasks.Add(id);
            }
        }
        if (saved.tasks != null)
        {
            foreach (var t in saved.tasks)
            {
                // 같은 해달이 두 작업에 들어 있는 등 깨진 줄은 버림 (해달이 묶여 버리지 않게)
                if (t == null || string.IsNullOrEmpty(t.taskId) || t.assignedOtterIds == null || t.assignedOtterIds.Count == 0
                    || _tasks.ContainsKey(t.taskId) || _completedTasks.Contains(t.taskId) || t.assignedOtterIds.Exists(id => GetWorkState(id) == ResidentWorkState.Working))
                    continue;
                _tasks[t.taskId] = new SettlementTaskJob(t.taskId, t.assignedOtterIds, t.startUtcTicks, t.endUtcTicks);
            }
        }
        if (saved.specialists != null)
        {
            foreach (var s in saved.specialists)
            {
                // 같은 해달이 두 번 있으면 앞으로 더 간 쪽을 남김
                if (s == null || string.IsNullOrEmpty(s.otterId) || s.state == SpecialistState.NotArrived && string.IsNullOrEmpty(s.regionId))
                    continue;
                if (_specialists.TryGetValue(s.otterId, out var existing) && existing.State >= s.state)
                    continue;
                _specialists[s.otterId] = new SpecialistRecord(s.state, s.regionId, s.workingSinceUtcTicks);
            }
        }
        if (saved.metOtters != null)
        {
            foreach (var id in saved.metOtters)
            {
                if (!string.IsNullOrEmpty(id))
                    _metOtters.Add(id);
            }
        }
        if (saved.roles != null)
        {
            foreach (var r in saved.roles)
            {
                // 같은 역할이 두 번이거나 한 해달이 두 역할이면 앞의 것만 (해달이 두 자리에 묶이지 않게)
                if (r == null || string.IsNullOrEmpty(r.roleId) || string.IsNullOrEmpty(r.otterId)
                    || _roles.ContainsKey(r.roleId) || HasRole(r.otterId))
                    continue;
                _roles[r.roleId] = new RoleAssignment(r.roleId, r.otterId, r.stationId);
            }
        }

        OnChanged?.Invoke();
    }

    public void Write(SettlementSaveData result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        result.version = Version;
        result.initialized = Initialized;
        result.legacyComplete = LegacyComplete;
        result.boardVisited = BoardVisited;
        result.kingdomStage = Stage;

        result.completedRequests = new List<string>(_completedRequests);
        result.completedRequests.Sort(StringComparer.Ordinal);
        result.unlockedDevelopments = new List<string>(_developments);
        result.unlockedDevelopments.Sort(StringComparer.Ordinal);

        result.residents = new List<ResidentSaveData>();
        foreach (var id in _residentOrder)
            result.residents.Add(new ResidentSaveData { otterId = id, state = _residents[id] });

        result.guestbookEntries = new List<string>(_guestbook);

        result.construction = Job == null
            ? new ConstructionJobSaveData()
            : new ConstructionJobSaveData
            {
                requestId = Job.RequestId,
                constructionId = Job.ConstructionId,
                startUtcTicks = Job.StartUtcTicks,
                endUtcTicks = Job.EndUtcTicks,
                waitingForWorker = Job.WaitingForWorker,
            };

        result.gatherCooldowns = new List<GatherCooldownSaveData>();
        foreach (var pair in _gatherReady)
            result.gatherCooldowns.Add(new GatherCooldownSaveData { pointId = pair.Key, readyUtcTicks = pair.Value });
        result.gatherCooldowns.Sort((a, b) => string.CompareOrdinal(a.pointId, b.pointId));

        result.flags = new List<string>(_flags);
        result.flags.Sort(StringComparer.Ordinal);

        result.tasks = new List<SettlementTaskSaveData>();
        foreach (var job in _tasks.Values)
        {
            result.tasks.Add(new SettlementTaskSaveData
            {
                taskId = job.TaskId,
                startUtcTicks = job.StartUtcTicks,
                endUtcTicks = job.EndUtcTicks,
                assignedOtterIds = new List<string>(job.OtterIds),
            });
        }
        result.tasks.Sort((a, b) => string.CompareOrdinal(a.taskId, b.taskId));
        result.completedTasks = new List<string>(_completedTasks);
        result.completedTasks.Sort(StringComparer.Ordinal);

        result.specialists = new List<SpecialistSaveData>();
        foreach (var pair in _specialists)
        {
            result.specialists.Add(new SpecialistSaveData
            {
                otterId = pair.Key,
                state = pair.Value.State,
                regionId = pair.Value.RegionId,
                workingSinceUtcTicks = pair.Value.WorkingSinceUtcTicks,
            });
        }
        result.specialists.Sort((a, b) => string.CompareOrdinal(a.otterId, b.otterId));
        result.metOtters = new List<string>(_metOtters);
        result.metOtters.Sort(StringComparer.Ordinal);

        result.roles = new List<RoleAssignmentSaveData>();
        foreach (var role in _roles.Values)
            result.roles.Add(new RoleAssignmentSaveData { roleId = role.RoleId, otterId = role.OtterId, stationId = role.StationId });
        result.roles.Sort((a, b) => string.CompareOrdinal(a.roleId, b.roleId));
    }

    #endregion
}
