using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 정착 진행 화면들을 SettlementManager와 잇는다 (전역 UI 루트에 붙음).
/// - 상단바 아래 칩(단계 · 주민 수), 오른쪽 위 안내 띠(지금 할 일)
/// - 게시판 팝업(방명록 / 해달의 부탁) → 건설 팝업 → 완료 팝업
///   부탁 탭은 게시판 등급에 따라: 기본은 한 목록, 관리 해달이 맡은 뒤 메인 / 주민 부탁 / 완료 구역, 접수소가 생기면 큰 부탁·발전 현황 카드
/// - 공사 현장 위 진행 말풍선 (광장에 있을 때)
/// </summary>
public class SettlementPresenter : MonoBehaviour
{
    private const string GoalVisitedFlag = "visited_goal_zone";
    private const float GuideRefreshSeconds = 0.25f;
    // 집이 완성되는 순간(먼지 → 카메라가 다가간 뒤 별빛)을 보여 준 뒤 완료 팝업
    private const float CelebrationSeconds = 1.9f;
    // 게시판 화면 카드 키
    private const string MilestoneEntryPrefix = "milestone:";
    private const string TownHallEntry = "townhall";
    // P3: 공동사업 카드 · 첫 모임 기념 · 생활 의뢰 (회차)
    private const string ProjectEntry = "p3project";
    private const string MemoryEntry = "p3memory";
    private const string LifeEntryPrefix = "life:";
    private const string MainSectionTitle = "지금 할 부탁";
    private const string ResidentSectionTitle = "주민들의 부탁";
    private const string CompletedSectionTitle = "완료한 부탁";
    private const string TownSectionTitle = "마을 발전";

    [Header("화면")]
    [SerializeField] private SettlementStatusView _status;
    [SerializeField] private SettlementGuideView _guide;
    [SerializeField] private SettlementBoardView _board;
    [SerializeField] private ConstructionPopupView _construction;
    [SerializeField] private ConstructionCompletePopupView _complete;
    [SerializeField] private ConstructionProgressView _progress;

    [Header("목표 장소")]
    [Tooltip("모든 부탁을 끝내면 안내 띠가 가자고 하는 장소 (밭)")]
    [SerializeField] private ZoneDefinition _goalZone;
    [SerializeField] private SceneNavigator _navigator;

    [Header("마을 발전 카드")]
    [Tooltip("게시판의 \"마을 발전 현황\" 카드 그림")]
    [SerializeField] private Sprite _townHallIcon;

    private SettlementManager _manager;
    private readonly Queue<BoardRequestDefinition> _completed = new Queue<BoardRequestDefinition>();
    private readonly List<BoardRow> _rows = new List<BoardRow>();
    private readonly List<BoardRequestRow> _requestRows = new List<BoardRequestRow>();
    private readonly List<GuestbookEntryDefinition> _entries = new List<GuestbookEntryDefinition>();
    private readonly List<(Sprite, int, int)> _costs = new List<(Sprite, int, int)>();
    private readonly List<(Sprite, int)> _paid = new List<(Sprite, int)>();
    private float _guideTimer;
    private bool _glyphsReady;

    private static SettlementPresenter _active;

    /// <summary>완료 팝업이 떠 있거나 차례를 기다리는 중 (레벨업 팝업·장소 튜토리얼은 그 뒤에)</summary>
    public static bool IsCelebrating => _active != null && (_active._complete.IsOpen || _active._completed.Count > 0);

    /// <summary>게시판·건설 팝업이 열려 있음 (새 해달 방문은 닫힌 뒤에)</summary>
    public static bool IsPopupOpen => _active != null && (_active._board.IsOpen || _active._construction.IsOpen);

    private void OnEnable()
    {
        _active = this;
        _guide.OnClicked += HandleGuideClicked;
        _board.OnRequestClicked += OpenConstruction;
        _board.OnEntryClicked += HandleEntryClicked;
        _board.OnTabChanged += HandleTabChanged;
        _construction.OnStartClicked += HandleStartClicked;
        _complete.OnClosed += ShowNextCompleted;
        SceneNavigator.Arrived += HandleArrived;
    }

    // SettlementManager가 같은 오브젝트에 붙어 있어 깨어나는 순서가 같을 수 있으므로 Start에서 연결
    private void Start()
    {
        _manager = SettlementManager.Instance;
        if (_manager == null)
        {
            _status.gameObject.SetActive(false);
            _guide.Hide();
            _progress.Hide();
            return;
        }

        _manager.OnChanged += Refresh;
        _manager.OnBoardRequested += OpenBoard;
        _manager.OnRequestCompleted += HandleRequestCompleted;
        _manager.OnConstructionStarted += HandleConstructionStarted;
        _manager.OnRequestOpenRequested += OpenConstruction;
        Refresh();
    }

    private void OnDisable()
    {
        if (_active == this)
            _active = null;
        _guide.OnClicked -= HandleGuideClicked;
        _board.OnRequestClicked -= OpenConstruction;
        _board.OnEntryClicked -= HandleEntryClicked;
        _board.OnTabChanged -= HandleTabChanged;
        _construction.OnStartClicked -= HandleStartClicked;
        _complete.OnClosed -= ShowNextCompleted;
        SceneNavigator.Arrived -= HandleArrived;
        if (_manager != null)
        {
            _manager.OnChanged -= Refresh;
            _manager.OnBoardRequested -= OpenBoard;
            _manager.OnRequestCompleted -= HandleRequestCompleted;
            _manager.OnConstructionStarted -= HandleConstructionStarted;
            _manager.OnRequestOpenRequested -= OpenConstruction;
        }
    }

    private void Update()
    {
        if (_manager == null)
            return;

        UpdateProgressBubble();

        _guideTimer -= Time.unscaledDeltaTime;
        if (_guideTimer > 0f)
            return;
        _guideTimer = GuideRefreshSeconds;

        // 남은 시간이 매초 바뀌므로 안내 띠·열린 팝업을 짧은 간격으로 다시 그림 (공사·주민 작업)
        if (_manager.Settlement.Job != null)
        {
            RefreshGuide();
            if (_board.IsOpen && _board.IsRequestTab)
                FillRequests();
            if (_construction.IsOpen)
                OpenConstruction(_construction.Request);
        }
        else if (_manager.Settlement.TaskJobs.Count > 0)
        {
            RefreshGuide();
            if (_board.IsOpen && _board.IsRequestTab)
                FillRequests();
        }
    }

    #region 갱신

    private void Refresh()
    {
        if (_manager == null)
            return;

        _status.gameObject.SetActive(_manager.IsLoaded);
        if (_manager.IsLoaded)
            _status.Bind(_manager.StageName, _manager.Settlement.ResidentCount);

        RefreshGuide();
        _board.SetRequestDot(_manager.HasActionableRequest);
        if (_board.IsOpen)
            HandleTabChanged(_board.IsRequestTab);
    }

    private void RefreshGuide()
    {
        if (!_manager.IsLoaded)
        {
            _guide.Hide();
            return;
        }

        var settlement = _manager.Settlement;
        var job = settlement.Job;
        if (job != null)
        {
            var building = _manager.JobRequest;
            string label = building != null && !string.IsNullOrEmpty(building.Construction.ProgressLabel)
                ? building.Construction.ProgressLabel
                : "공사 중";
            _guide.Show($"{label}  {JobTime(job)}", null);
            return;
        }

        // 배치한 전문 해달: 일하는 곳에 가면 생산 안내 뒤 일을 시작함
        var assigned = _manager.AssignedNotWorking;
        if (assigned != null && assigned.WorkRegion.Zone != null && !IsIn(assigned.WorkRegion.Zone))
        {
            string place = assigned.WorkRegion.DisplayName;
            _guide.Show($"{place}에 가서 {assigned.DisplayName}의 일을 시작해요", "가 보기");
            return;
        }

        var current = _manager.CurrentRequest;
        if (current != null)
        {
            if (!settlement.BoardVisited)
                _guide.Show("게시판을 확인해요", "확인하기");
            else if (!TryShowPreparationGuide(current))
                _guide.Show(current.Title, "보기");
            return;
        }

        // 정착 후보가 할 말이 있음 → 들으면 그 해달의 집 부탁이 열림
        var intro = _manager.PendingIntroOtter;
        if (intro != null)
        {
            string name = intro.DisplayName;
            _guide.Show($"{name}{KoreanParticle.SubjectParticle(name)} 할 말이 있대요", SettlementPlazaView.Active != null ? "보기" : null);
            return;
        }

        bool goalOpen = _goalZone != null && ZoneAccess.IsOpen(_goalZone);
        if (goalOpen && !settlement.HasFlag(GoalVisitedFlag) && !IsInGoalZone)
        {
            _guide.Show($"{_goalZone.DisplayName}이 열렸어요!", "가 보기");
            return;
        }

        // P3: 마을회관 뒤 지금 공동사업 (반복 사업은 띠에 띄우지 않음 — 회관·게시판에서 고름)
        var project = _manager.ActiveProject;
        if (project != null && !project.Repeatable)
        {
            _guide.Show(ProjectGuideText(project), "보기");
            return;
        }

        _guide.Hide();
    }

    // "우리 마을의 첫 비축: 재료 모으기" / "광장 첫 확장: 주민 정비 01:20"
    private string ProjectGuideText(CommunityProjectDefinition project) => $"{project.Title}: {ProjectStepText(project)}";

    // 지금 단계 한 줄 (재료 모으기 / 상자 설치 / 주민 정비 01:20)
    private string ProjectStepText(CommunityProjectDefinition project)
    {
        var status = _manager.GetProjectStatus(project);
        string step;
        switch (status.Phase)
        {
            case ProjectPhase.NeedsLevel:
                step = $"Lv.{project.RequiredLevel}부터";
                break;
            case ProjectPhase.Delivering:
                step = "재료 모으기";
                break;
            default:
                var stage = status.StageIndex < project.Stages.Count ? project.Stages[status.StageIndex] : null;
                step = stage != null ? stage.Label : "마무리";
                if (status.StageState == ProjectStageState.Working && stage != null)
                {
                    var taskJob = stage.Task != null ? _manager.GetTaskJob(stage.Task) : null;
                    if (taskJob != null)
                        step += "  " + FormatTime(taskJob.Remaining(SettlementManager.NowTicks));
                }
                break;
        }
        return step;
    }

    // 길을 다 치운 지역: 주민 해달을 보내야 함 (그 장소에서만) / 정비 중 남은 시간. 광장 주민 작업 부탁은 작업 중 남은 시간
    private bool TryShowPreparationGuide(BoardRequestDefinition request)
    {
        var plazaJob = request.CompletionTask != null ? _manager.GetTaskJob(request.CompletionTask) : null;
        if (plazaJob != null)
        {
            _guide.Show($"{request.CompletionTask.Title}  {FormatTime(plazaJob.Remaining(SettlementManager.NowTicks))}", "보기");
            return true;
        }

        var task = PreparationOf(request, out var state);
        if (task == null)
            return false;
        if (state == RegionProgressState.WorkerPreparing)
            _guide.Show($"{task.Title}  {FormatTime(_manager.GetTaskJob(task).Remaining(SettlementManager.NowTicks))}", "보기");
        else if (IsIn(request.ClearZone))
            _guide.Show($"{task.Title}: 주민을 보내요", "보내기");
        else
            _guide.Show($"{task.Title}: {request.ClearZone.DisplayName}에서 주민을 보내요", "가 보기");
        return true;
    }

    // 직접 치우는 부탁의 지역이 후속 정비 단계면 지금 손댈 작업 (아니면 null)
    private SettlementTaskDefinition PreparationOf(BoardRequestDefinition request, out RegionProgressState state)
    {
        state = RegionProgressState.Locked;
        var region = request.ClearZone != null ? _manager.FindRegion(request.ClearZone) : null;
        if (region == null)
            return null;
        state = _manager.GetRegionState(region);
        if (state != RegionProgressState.AwaitingWorkers && state != RegionProgressState.WorkerPreparing)
            return null;
        return _manager.CurrentPreparation(region);
    }

    private bool IsInGoalZone => IsIn(_goalZone);

    private bool IsIn(ZoneDefinition zone) => _navigator != null && zone != null && _navigator.CurrentZone == zone;

    private void HandleGuideClicked()
    {
        var assigned = _manager.AssignedNotWorking;
        if (assigned != null && assigned.WorkRegion.Zone != null && !IsIn(assigned.WorkRegion.Zone))
        {
            if (_navigator != null)
                _navigator.TryGo(assigned.WorkRegion.Zone);
            return;
        }

        var current = _manager.CurrentRequest;
        if (current != null)
        {
            if (!_manager.Settlement.BoardVisited)
            {
                _manager.RequestBoard(false);
                return;
            }
            // 주민을 보낼 차례인데 그 장소에 있지 않음 → 바로 그 장소로 (작업은 현장의 망치 표지판에서)
            if (PreparationOf(current, out var regionState) != null && regionState == RegionProgressState.AwaitingWorkers
                && !IsIn(current.ClearZone) && _navigator != null)
            {
                _navigator.TryGo(current.ClearZone);
                return;
            }
            OpenConstruction(current);
            return;
        }

        var intro = _manager.PendingIntroOtter;
        if (intro != null)
        {
            if (SettlementPlazaView.Active != null)
                SettlementPlazaView.Active.FocusOtter(intro.OtterId);
            return;
        }

        bool goalOpen = _goalZone != null && ZoneAccess.IsOpen(_goalZone);
        bool goalPending = goalOpen && !_manager.Settlement.HasFlag(GoalVisitedFlag) && !IsInGoalZone;
        var project = _manager.ActiveProject;
        if (!goalPending && project != null)
        {
            _manager.RequestProject(project);
            return;
        }

        if (_goalZone != null && _navigator != null)
            _navigator.TryGo(_goalZone);
    }

    private void HandleArrived(ZoneDefinition zone)
    {
        if (_manager != null && zone != null && zone == _goalZone && _manager.IsLoaded)
            _manager.Settlement.SetFlag(GoalVisitedFlag);
        if (_manager != null)
            RefreshGuide();
    }

    #endregion

    #region 게시판

    private void OpenBoard(bool requestTab)
    {
        if (!_glyphsReady)
            PrepareGlyphs();
        _manager.EnsureLifeRequests();
        _board.SetRequestDot(_manager.HasActionableRequest);
        _board.Show(requestTab);
    }

    private void HandleTabChanged(bool requestTab)
    {
        if (_manager == null)
            return;
        if (requestTab)
            FillRequests();
        else
            FillGuestbook();
    }

    private void FillGuestbook()
    {
        var config = _manager.Config;
        _entries.Clear();
        foreach (var id in _manager.Settlement.Guestbook)
        {
            var entry = config.FindEntry(id);
            if (entry != null)
                _entries.Add(entry);
        }
        _board.BindGuestbook(_entries);

        var current = _manager.CurrentRequest;
        bool actionable = current != null && _manager.GetStatus(current) == RequestStatus.Available;
        _board.SetNotice(actionable ? "새로운 부탁이 도착했어요." : null,
            actionable && current.Requester != null ? current.Requester.Portrait : null);
    }

    // 부탁 탭: 게시판 등급에 따라 한 목록 또는 구역(메인 / 주민 / 완료). 접수소·마을회관이 생기면 맨 위에 화면 카드
    private void FillRequests()
    {
        _rows.Clear();
        long now = SettlementManager.NowTicks;
        bool sections = SettlementBoardRules.ShowsSections(_manager.BoardTier);

        AddTownEntries(sections);

        _manager.CollectBoardRows(_requestRows);
        BoardSection? section = null;
        foreach (var row in _requestRows)
        {
            if (sections && row.Section != section)
            {
                section = row.Section;
                _rows.Add(BoardRow.Header(SectionTitle(row.Section)));
            }
            var request = row.Request;
            var status = row.Status;
            string time = null;
            if (status == RequestStatus.Building)
            {
                var taskJob = request.CompletionTask != null ? _manager.GetTaskJob(request.CompletionTask) : null;
                time = taskJob != null ? FormatTime(taskJob.Remaining(now)) : JobTime(_manager.Settlement.Job);
            }
            // 주민 해달이 정비하는 중이면 카드도 "진행 중" + 남은 시간
            var task = PreparationOf(request, out var regionState);
            if (status == RequestStatus.Available && task != null && regionState == RegionProgressState.WorkerPreparing)
            {
                status = RequestStatus.Building;
                time = FormatTime(_manager.GetTaskJob(task).Remaining(now));
            }
            _rows.Add(BoardRow.ForRequest(request, status, time));
        }
        if (sections)
            AddLifeRequests();
        _board.BindRows(_rows);
    }

    // 생활 의뢰(P3)를 "주민들의 부탁" 구역에 (기존 주민 부탁이 쓰고 남은 칸). 구역 제목이 없으면 완료 구역 앞에 만듦.
    // 빈 칸 채우기는 게시판을 열기 전·기록이 바뀐 다음 프레임에 매니저가 함 (여기서 바꾸면 그리는 도중 다시 그려짐)
    private void AddLifeRequests()
    {
        var requests = _manager.Settlement.LifeRequests;
        if (requests.Count == 0)
            return;

        int insertAt = _rows.Count;
        bool hasHeader = false;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Kind != BoardRow.RowKind.Header)
                continue;
            if (_rows[i].Text == ResidentSectionTitle)
                hasHeader = true;
            else if (_rows[i].Text == CompletedSectionTitle)
            {
                insertAt = i;
                break;
            }
        }
        var entries = new List<BoardRow>();
        if (!hasHeader)
            entries.Add(BoardRow.Header(ResidentSectionTitle));
        long now = SettlementManager.NowTicks;
        foreach (var record in requests)
        {
            var template = _manager.FindLifeTemplate(record);
            if (template == null)
                continue;
            var item = _manager.LifeRequestItem(record);
            var job = _manager.LifeWorkJob(record);
            string description = job != null
                ? $"{template.Task.Title} 중 · {FormatTime(job.Remaining(now))}"
                : LifeRequestRules.Line(template, item, record.Amount);
            var icon = template.Icon != null ? template.Icon : item != null ? item.Icon : null;
            entries.Add(BoardRow.Entry(LifeEntryPrefix + record.Serial, icon, template.Title, description, job != null ? "보기" : "하기"));
        }
        _rows.InsertRange(insertAt, entries);
    }

    // 큰 부탁 "마을 회의소 마련하기 3/5", 마을 발전 현황 (마을회관이 생긴 뒤), 공동사업 · 첫 모임 기념 (P3)
    private void AddTownEntries(bool sections)
    {
        var group = _manager.VisibleMilestone;
        bool hall = _manager.IsTownHallOpen;
        if (group == null && !hall)
            return;
        if (sections)
            _rows.Add(BoardRow.Header(TownSectionTitle));
        var project = _manager.ActiveProject;
        if (project != null)
            _rows.Add(BoardRow.Entry(ProjectEntry, project.Icon != null ? project.Icon : _townHallIcon, _manager.ProjectTitle(project),
                ProjectStepText(project), "보기"));
        if (_manager.HasHeldGathering)
            _rows.Add(BoardRow.Entry(MemoryEntry, _townHallIcon, "첫 마을 모임 기념", "광장에서 모임을 다시 볼 수 있어요.", "다시 보기"));
        if (group != null)
        {
            var settlement = _manager.Settlement;
            int done = SettlementBoardRules.CountDone(group, settlement);
            var step = SettlementBoardRules.CurrentStep(group, settlement);
            string description = step != null
                ? $"{done} / {group.Steps.Count} 단계 · 다음: {step.Title}"
                : $"{done} / {group.Steps.Count} 단계 · 모두 마쳤어요!";
            _rows.Add(BoardRow.Entry(MilestoneEntryPrefix + group.GroupId, group.Icon, group.Title, description, "보기"));
        }
        if (hall)
            _rows.Add(BoardRow.Entry(TownHallEntry, _townHallIcon, "마을 발전 현황", "발전 · 주민 · 지금 하는 일을 한눈에 봐요.", "보기"));
    }

    private static string SectionTitle(BoardSection section)
    {
        switch (section)
        {
            case BoardSection.Resident: return ResidentSectionTitle;
            case BoardSection.Completed: return CompletedSectionTitle;
            default: return MainSectionTitle;
        }
    }

    private void HandleEntryClicked(string key)
    {
        if (key == TownHallEntry)
        {
            _manager.RequestTownHall();
            return;
        }
        if (key == ProjectEntry)
        {
            var project = _manager.ActiveProject;
            if (project != null)
            {
                _board.Hide();
                _manager.RequestProject(project);
            }
            return;
        }
        if (key == MemoryEntry)
        {
            _board.Hide();
            if (SettlementPlazaView.Active != null)
                _manager.RequestGatheringReplay();
            else
                GameNotices.Post(new GameNotice("첫 모임은 광장에서 다시 볼 수 있어요."));
            return;
        }
        if (key.StartsWith(LifeEntryPrefix, StringComparison.Ordinal)
            && int.TryParse(key.Substring(LifeEntryPrefix.Length), out int serial))
        {
            _board.Hide();
            _manager.RequestLifeRequest(serial);
            return;
        }
        if (!key.StartsWith(MilestoneEntryPrefix, StringComparison.Ordinal))
            return;
        string groupId = key.Substring(MilestoneEntryPrefix.Length);
        foreach (var group in _manager.Config.MilestoneGroups)
        {
            if (group != null && group.GroupId == groupId)
            {
                _manager.RequestMilestone(group);
                return;
            }
        }
    }

    #endregion

    #region 건설

    private void OpenConstruction(BoardRequestDefinition request)
    {
        if (request == null)
            return;
        if (!_glyphsReady)
            PrepareGlyphs();

        var status = _manager.GetStatus(request);
        if (status == RequestStatus.Completed)
            return;
        if (request.AssignSpecialist != null)
        {
            OpenAssignment(request, status);
            return;
        }
        if (request.AssignRole != null)
        {
            OpenRoleAssignment(request, status);
            return;
        }
        if (request.CompletionTask != null)
        {
            OpenResidentTask(request, status);
            return;
        }
        var construction = request.Construction;
        if (construction == null)
            return;
        if (request.ClearZone != null)
        {
            OpenClearing(request, status);
            return;
        }

        var builder = _manager.Config.FindBuilder();
        bool byBuilder = construction.NeedsBuilder && builder != null;
        var speaker = byBuilder ? builder : request.Requester;
        string line = byBuilder ? "집을 짓고 길을 열어요." : request.Description;

        _costs.Clear();
        var gold = _manager.Config.GoldCurrency;
        if (construction.RequiredGold > 0)
            _costs.Add((gold != null ? gold.Icon : null, construction.RequiredGold, _manager.GoldBalance));
        foreach (var cost in construction.RequiredItems)
        {
            if (cost != null && cost.Item != null && cost.Amount > 0)
                _costs.Add((cost.Item.Icon, cost.Amount, _manager.ItemCount(cost.Item)));
        }

        string note;
        string startLabel = construction.IsInstant ? "짓기" : construction.TargetType == ConstructionTarget.Clearing ? "개간 시작" : "건설 시작";
        bool canStart = false;
        if (status == RequestStatus.Building)
        {
            note = $"{construction.ProgressLabel}  {JobTime(_manager.Settlement.Job)}";
            startLabel = "진행 중";
        }
        else if (status == RequestStatus.Locked)
        {
            note = "아직 할 수 없어요.";
        }
        else if (construction.NeedsBuilder && !_manager.HasBuilder)
        {
            note = "건설 해달이 필요해요.";
        }
        else if (!construction.IsInstant && _manager.Settlement.Job != null)
        {
            note = "다른 공사가 끝나면 시작할 수 있어요.";
        }
        else if (IsRequesterAtWork(request))
        {
            note = $"{request.Requester.DisplayName}{KoreanParticle.SubjectParticle(request.Requester.DisplayName)} 작업하러 가 있어요. 끝나면 시작할 수 있어요.";
        }
        else
        {
            note = MissingText(construction);
            canStart = note == null;
            if (canStart)
                note = construction.IsInstant ? "바로 지을 수 있어요!" : $"{FormatDuration(construction.DurationSeconds)} 걸려요";
        }

        _construction.Show(request, speaker, line, _costs, note, startLabel, canStart);
    }

    // 장소를 직접 치우는 부탁 (광산 길 열기, 농경지 개간): 비용 없이 [가 보기]로 그 장소에 감.
    // 다 치운 뒤 주민 해달을 보내는 작업도 그 장소(망치 표지판)에서 시작한다. 그 장소에 있으면 바로 작업 화면, 아니면 [가 보기]
    private void OpenClearing(BoardRequestDefinition request, RequestStatus status)
    {
        var zone = request.ClearZone;
        bool here = IsIn(zone);
        var task = PreparationOf(request, out var regionState);
        if (task != null && here)
        {
            if (_board.IsOpen)
                _board.Hide();
            if (_construction.IsOpen)
                _construction.Hide();
            _manager.RequestTask(task);
            return;
        }

        string note;
        if (status == RequestStatus.Locked)
            note = "아직 할 수 없어요.";
        else if (task != null && regionState == RegionProgressState.WorkerPreparing)
            note = $"주민 해달이 {zone.DisplayName}에서 일하고 있어요 · {FormatTime(_manager.GetTaskJob(task).Remaining(SettlementManager.NowTicks))}";
        else if (task != null)
            note = $"{zone.DisplayName}에 가서 망치 표지판을 눌러 주민 해달을 보내요.";
        else if (here)
            note = "길을 막은 나무와 돌을 톡톡 눌러 치워요!";
        else
            note = $"{zone.DisplayName}에 가서 길을 막은 나무와 돌을 치워요.";
        _costs.Clear();
        _construction.Show(request, request.Requester, request.Description, _costs, note, "가 보기",
            status == RequestStatus.Available && !here);
    }

    // 배치 부탁 (광산에서 일할 친구): 광장으로 돌아가 새로 찾아온 전문 해달을 만나 대화로 배치. 부탁 화면에서는 배치하지 않음
    private void OpenAssignment(BoardRequestDefinition request, RequestStatus status)
    {
        var otter = request.AssignSpecialist;
        string name = otter.DisplayName;
        string place = otter.WorkRegion != null ? otter.WorkRegion.DisplayName : string.Empty;
        bool inPlaza = SettlementPlazaView.Active != null;
        string note;
        string button;
        bool canPress;
        if (status == RequestStatus.Locked)
        {
            note = "아직 할 수 없어요.";
            button = "보기";
            canPress = false;
        }
        else if (!inPlaza)
        {
            note = $"광장으로 돌아가 새로 찾아온 {name}{KoreanParticle.ObjectParticle(name)} 만나 보세요.";
            button = "광장으로";
            canPress = PlazaZone != null;
        }
        else if (_manager.CanAssignSpecialist(otter))
        {
            note = $"{name}에게 말을 걸어 {place}에 배치해 주세요.";
            button = "만나러 가기";
            canPress = true;
        }
        else
        {
            note = $"{name}{KoreanParticle.SubjectParticle(name)} 광장으로 오고 있어요.";
            button = "만나러 가기";
            canPress = true;
        }
        _costs.Clear();
        _construction.Show(request, otter, request.Description, _costs, note, button, canPress);
    }

    // 역할 부탁 (게시판을 맡아 줄 친구): 광장에서 만난 관리 해달에게 대화로 맡김. 부탁 화면에서는 맡기지 않음
    private void OpenRoleAssignment(BoardRequestDefinition request, RequestStatus status)
    {
        var role = request.AssignRole;
        var otter = role.Otter;
        string name = otter != null ? otter.DisplayName : string.Empty;
        bool inPlaza = SettlementPlazaView.Active != null;
        string note;
        string button = "만나러 가기";
        bool canPress = true;
        if (status == RequestStatus.Locked || otter == null)
        {
            note = "아직 할 수 없어요.";
            button = "보기";
            canPress = false;
        }
        else if (!inPlaza)
        {
            note = $"광장으로 돌아가 새로 찾아온 {name}{KoreanParticle.ObjectParticle(name)} 만나 보세요.";
            button = "광장으로";
            canPress = PlazaZone != null;
        }
        else if (_manager.CanAssignRole(otter))
            note = $"{name}에게 말을 걸어 {role.DisplayName}{KoreanParticle.ObjectParticle(role.DisplayName)} 맡겨 주세요.";
        else
            note = $"{name}{KoreanParticle.SubjectParticle(name)} 광장으로 오고 있어요.";
        _costs.Clear();
        _construction.Show(request, otter, request.Description, _costs, note, button, canPress);
    }

    // 주민 작업 부탁 (공동 공간 정비 등): 광장에 있으면 바로 작업 화면(주민 고르기 · 남은 시간), 아니면 [광장으로]
    private void OpenResidentTask(BoardRequestDefinition request, RequestStatus status)
    {
        var task = request.CompletionTask;
        var taskState = _manager.GetTaskState(task);
        bool inPlaza = SettlementPlazaView.Active != null;
        if (inPlaza && (taskState == SettlementTaskState.Available || taskState == SettlementTaskState.Working))
        {
            if (_board.IsOpen)
                _board.Hide();
            if (_construction.IsOpen)
                _construction.Hide();
            _manager.RequestTask(task);
            return;
        }

        _costs.Clear();
        var gold = _manager.Config.GoldCurrency;
        if (task.RequiredGold > 0)
            _costs.Add((gold != null ? gold.Icon : null, task.RequiredGold, _manager.GoldBalance));
        foreach (var cost in task.RequiredItems)
        {
            if (cost != null && cost.Item != null && cost.Amount > 0)
                _costs.Add((cost.Item.Icon, cost.Amount, _manager.ItemCount(cost.Item)));
        }

        string note;
        string button = "광장으로";
        bool canPress = PlazaZone != null;
        if (status == RequestStatus.Locked || taskState == SettlementTaskState.Locked)
        {
            note = "아직 할 수 없어요.";
            button = "보기";
            canPress = false;
        }
        else if (taskState == SettlementTaskState.Working)
            note = $"주민 해달이 광장에서 일하고 있어요 · {FormatTime(_manager.GetTaskJob(task).Remaining(SettlementManager.NowTicks))}";
        else
            note = $"광장의 망치 표지판에서 주민 해달 {task.RequiredWorkers}명을 보내요.";
        _construction.Show(request, request.Requester, request.Description, _costs, note, button, canPress);
    }

    // 광장 (배치 부탁이 데려가는 곳)
    private ZoneDefinition PlazaZone =>
        GlobalUIRoot.Instance != null ? ZoneLookup.FindByScene(GlobalUIRoot.Instance.Zones, ZoneTutorials.Plaza) : null;

    // 공사를 직접 할 해달(부탁한 해달)이 주민 작업에 가 있음
    private bool IsRequesterAtWork(BoardRequestDefinition request) =>
        !request.Construction.IsInstant && !request.Construction.NeedsBuilder && request.Requester != null
        && _manager.Settlement.GetWorkState(request.Requester.OtterId) == ResidentWorkState.Working;

    // 모자란 것 한 줄 (넉넉하면 null)
    private string MissingText(ConstructionDefinition construction)
    {
        int goldShort = construction.RequiredGold - _manager.GoldBalance;
        if (construction.RequiredGold > 0 && goldShort > 0)
            return $"골드가 {goldShort:N0} 모자라요.";
        foreach (var cost in construction.RequiredItems)
        {
            if (cost == null || cost.Item == null)
                continue;
            int itemShort = cost.Amount - _manager.ItemCount(cost.Item);
            if (itemShort > 0)
                return $"{cost.Item.DisplayName} {itemShort}개가 더 필요해요.";
        }
        return null;
    }

    private void HandleStartClicked()
    {
        var request = _construction.Request;
        var meet = request.AssignSpecialist != null ? request.AssignSpecialist
            : request.AssignRole != null ? request.AssignRole.Otter
            : null;
        if (meet != null || request.CompletionTask != null)
        {
            _construction.Hide();
            if (_board.IsOpen)
                _board.Hide();
            var plaza = SettlementPlazaView.Active;
            if (plaza == null)
            {
                if (PlazaZone != null && _navigator != null)
                    _navigator.TryGo(PlazaZone);
            }
            else if (meet != null)
                plaza.FocusOtter(meet.OtterId);
            else
                _manager.RequestTask(request.CompletionTask);
            return;
        }
        if (request.ClearZone != null)
        {
            _construction.Hide();
            if (_board.IsOpen)
                _board.Hide();
            _navigator.TryGo(request.ClearZone);
            return;
        }

        switch (_manager.TryStart(request))
        {
            case ConstructionStartResult.Started:
            case ConstructionStartResult.Completed:
                // 건설 해달이 일하러 가는 모습(또는 완료 팝업)이 보이도록 둘 다 닫음
                _construction.Hide();
                if (_board.IsOpen)
                    _board.Hide();
                break;

            default:
                OpenConstruction(request);
                _construction.ShowFailed(MissingText(request.Construction) ?? "지금은 시작할 수 없어요.");
                break;
        }
    }

    private void HandleConstructionStarted(BoardRequestDefinition request)
    {
        RefreshGuide();
    }

    private void HandleRequestCompleted(BoardRequestDefinition request)
    {
        _completed.Enqueue(request);
        if (_construction.IsOpen)
            _construction.Hide();
        if (_board.IsOpen)
            _board.Hide();
        if (_complete.IsOpen)
            return;
        // 광장에서 완성되면 카메라가 다가가 집이 생기는 순간을 보여 준 뒤에 팝업
        var plaza = SettlementPlazaView.Active;
        if (plaza != null && plaza.CelebratesHere(request))
            StartCoroutine(ShowCompletedAfter(CelebrationSeconds));
        else
            ShowNextCompleted();
    }

    private IEnumerator ShowCompletedAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        ShowNextCompleted();
    }

    private void ShowNextCompleted()
    {
        if (_completed.Count == 0 || _complete.IsOpen)
            return;
        if (!_glyphsReady)
            PrepareGlyphs();

        var request = _completed.Dequeue();
        var construction = request.Construction;
        _paid.Clear();
        var gold = _manager.Config.GoldCurrency;
        if (construction != null && construction.RequiredGold > 0)
            _paid.Add((gold != null ? gold.Icon : null, construction.RequiredGold));
        if (construction != null)
        {
            foreach (var cost in construction.RequiredItems)
            {
                if (cost != null && cost.Item != null && cost.Amount > 0)
                    _paid.Add((cost.Item.Icon, cost.Amount));
            }
        }

        // 주민 작업 부탁은 작업을 시작할 때 낸 비용
        var task = request.CompletionTask;
        if (task != null)
        {
            if (task.RequiredGold > 0)
                _paid.Add((gold != null ? gold.Icon : null, task.RequiredGold));
            foreach (var cost in task.RequiredItems)
            {
                if (cost != null && cost.Item != null && cost.Amount > 0)
                    _paid.Add((cost.Item.Icon, cost.Amount));
            }
        }
        // 배치·역할 부탁처럼 건설이 없는 부탁은 부탁 제목·그림 (배치한·맡긴 해달 얼굴)
        var otter = request.AssignSpecialist != null ? request.AssignSpecialist
            : request.AssignRole != null ? request.AssignRole.Otter
            : null;
        string title = construction != null ? construction.DisplayName : request.Title;
        Sprite icon = construction != null && construction.Icon != null ? construction.Icon
            : otter != null && otter.Portrait != null ? otter.Portrait
            : request.Icon;
        int stage = _manager.Settlement.Stage;
        _complete.Show($"{stage + 1:00} · {_manager.StageName}", title, _paid, request.CompletionMessage, icon);
    }

    #endregion

    #region 진행 말풍선

    private void UpdateProgressBubble()
    {
        var job = _manager.Settlement.Job;
        var plaza = SettlementPlazaView.Active;
        var camera = Camera.main;
        if (job == null || plaza == null || camera == null || !plaza.TryGetSiteAnchor(job.ConstructionId, out Vector3 world))
        {
            if (_progress.Root.gameObject.activeSelf)
                _progress.Hide();
            return;
        }

        var request = _manager.JobRequest;
        if (!_progress.Root.gameObject.activeSelf && request != null)
            _progress.Show(request.Construction.ProgressLabel, request.Construction.ProgressHint);

        var parent = (RectTransform)_progress.Root.parent;
        Vector2 screen = camera.WorldToScreenPoint(world);
        var canvas = parent.GetComponentInParent<Canvas>().rootCanvas;
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local))
            _progress.Root.anchoredPosition = local;

        long now = SettlementManager.NowTicks;
        _progress.SetProgress(job.Progress(now), JobTime(job));
    }

    #endregion

    #region 글자

    // 남은 시간 (일할 해달이 아직 가는 중이면 그 안내)
    private static string JobTime(ConstructionJob job) =>
        job.WaitingForWorker ? "가는 중" : FormatTime(job.Remaining(SettlementManager.NowTicks));

    private static string FormatTime(TimeSpan remaining)
    {
        int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        return seconds >= 3600
            ? $"{seconds / 3600}시간 {seconds / 60 % 60:00}분"
            : $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private static string FormatDuration(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        if (total < 60)
            return $"{total}초";
        return total % 60 == 0 ? $"{total / 60}분" : $"{total / 60}분 {total % 60}초";
    }

    // 처음 보는 한글을 폰트 아틀라스에 미리 넣는다 (스크롤 목록 갱신 중 글자가 추가되면 D3D12 에디터가 멈춤)
    private void PrepareGlyphs()
    {
        var config = _manager.Config;
        var text = new StringBuilder("0123456789:/ ,.!?%·가는 중시간분초 보기진행중완료확인하기짓기건설개간시작해달게시판방명록의부탁방문기록새로운이도착했어요골드가모자라요개더필요해요아직할수없어요다른공사끝나면있어요바로지을걸려요주민가열렸어요집을짓고길을열어요광장으로돌아가찾아온만나보세요에게말을걸어배치해주세요오고있어요러가기을를서일의망치표지판눌러보내요하고");
        foreach (var request in config.Requests)
        {
            if (request == null)
                continue;
            text.Append(request.Title).Append(request.Description).Append(request.CompletionMessage);
            if (request.Construction != null)
                text.Append(request.Construction.DisplayName).Append(request.Construction.ProgressLabel).Append(request.Construction.ProgressHint);
        }
        foreach (var entry in config.GuestbookEntries)
        {
            if (entry != null)
                text.Append(entry.Tag).Append(entry.Message);
        }
        foreach (var otter in config.Otters)
        {
            if (otter != null)
                text.Append(otter.DisplayName).Append(otter.WorkRegion != null ? otter.WorkRegion.DisplayName : string.Empty);
        }
        // 게시판 성장: 구역 제목, 화면 카드, 역할·주민 작업·큰 부탁 문구
        text.Append(MainSectionTitle).Append(ResidentSectionTitle).Append(CompletedSectionTitle).Append(TownSectionTitle)
            .Append("마을 발전 현황 · 지금 하는 일을 한눈에 봐요 단계 다음: 모두 마쳤어요 광장의 망치 표지판에서 명을 맡겨 주세요 광장으로 오고");
        foreach (var task in config.Tasks)
        {
            if (task != null)
                text.Append(task.Title).Append(task.Description).Append(task.CompletionMessage);
        }
        foreach (var role in config.Roles)
        {
            if (role != null)
                text.Append(role.DisplayName).Append(role.AskLine).Append(role.ConfirmLabel);
        }
        foreach (var group in config.MilestoneGroups)
        {
            if (group != null)
                text.Append(group.Title).Append(group.Description);
        }
        for (int i = 0; i < config.StageCount; i++)
            text.Append(config.StageName(i));
        if (config.GatherItem != null)
            text.Append(config.GatherItem.DisplayName);
        // P3: 공동사업 카드 · 생활 의뢰 · 기념 기록
        text.Append("재료 모으기마무리부터첫 마을 모임 기념광장에서 모임을 다시 볼 수 있어요하기중Lv");
        foreach (var project in config.Projects)
        {
            if (project == null)
                continue;
            text.Append(project.Title);
            foreach (var title in project.CycleTitles)
                text.Append(title);
            foreach (var stage in project.Stages)
                text.Append(stage.Label);
        }
        foreach (var template in config.LifeRequests)
        {
            if (template == null)
                continue;
            text.Append(template.Title).Append(template.Line);
            if (template.Task != null)
                text.Append(template.Task.Title);
            foreach (var item in template.Items)
            {
                if (item != null)
                    text.Append(item.DisplayName);
            }
        }
        if (_goalZone != null)
            text.Append(_goalZone.DisplayName);

        var fonts = new HashSet<TMP_FontAsset>();
        foreach (var root in new Component[] { _board, _construction, _complete, _guide, _progress })
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                fonts.Add(label.font);
        }

        string characters = text.ToString();
        foreach (var font in fonts)
        {
            if (font != null)
                font.TryAddCharacters(characters, out _);
        }
        _glyphsReady = true;
    }

    #endregion
}
