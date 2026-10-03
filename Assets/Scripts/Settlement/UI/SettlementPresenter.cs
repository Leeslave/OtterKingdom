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
/// - 공사 현장 위 진행 말풍선 (광장에 있을 때)
/// </summary>
public class SettlementPresenter : MonoBehaviour
{
    private const string GoalVisitedFlag = "visited_goal_zone";
    private const float GuideRefreshSeconds = 0.25f;
    // 집이 완성되는 순간(먼지 → 카메라가 다가간 뒤 별빛)을 보여 준 뒤 완료 팝업
    private const float CelebrationSeconds = 1.9f;

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

    private SettlementManager _manager;
    private readonly Queue<BoardRequestDefinition> _completed = new Queue<BoardRequestDefinition>();
    private readonly List<(BoardRequestDefinition, RequestStatus, string)> _rows = new List<(BoardRequestDefinition, RequestStatus, string)>();
    private readonly List<GuestbookEntryDefinition> _entries = new List<GuestbookEntryDefinition>();
    private readonly List<(Sprite, int, int)> _costs = new List<(Sprite, int, int)>();
    private readonly List<(Sprite, int)> _paid = new List<(Sprite, int)>();
    private float _guideTimer;
    private bool _glyphsReady;

    private static SettlementPresenter _active;

    /// <summary>완료 팝업이 떠 있거나 차례를 기다리는 중 (레벨업 팝업·장소 튜토리얼은 그 뒤에)</summary>
    public static bool IsCelebrating => _active != null && (_active._complete.IsOpen || _active._completed.Count > 0);

    private void OnEnable()
    {
        _active = this;
        _guide.OnClicked += HandleGuideClicked;
        _board.OnRequestClicked += OpenConstruction;
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
        Refresh();
    }

    private void OnDisable()
    {
        if (_active == this)
            _active = null;
        _guide.OnClicked -= HandleGuideClicked;
        _board.OnRequestClicked -= OpenConstruction;
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

        _guide.Hide();
    }

    // 길을 다 치운 지역: 주민 해달을 보내야 함 (그 장소에서만) / 정비 중 남은 시간
    private bool TryShowPreparationGuide(BoardRequestDefinition request)
    {
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

    private void FillRequests()
    {
        _rows.Clear();
        long now = SettlementManager.NowTicks;
        foreach (var request in _manager.Config.Requests)
        {
            if (request == null)
                continue;
            var status = _manager.GetStatus(request);
            if (status == RequestStatus.Locked)
                continue;
            string time = status == RequestStatus.Building ? JobTime(_manager.Settlement.Job) : null;
            // 주민 해달이 정비하는 중이면 카드도 "진행 중" + 남은 시간
            var task = PreparationOf(request, out var regionState);
            if (status == RequestStatus.Available && task != null && regionState == RegionProgressState.WorkerPreparing)
            {
                status = RequestStatus.Building;
                time = FormatTime(_manager.GetTaskJob(task).Remaining(now));
            }
            _rows.Add((request, status, time));
        }
        _rows.Sort((a, b) => a.Item1.Order.CompareTo(b.Item1.Order));
        _board.BindRequests(_rows);
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
        if (request.AssignSpecialist != null)
        {
            _construction.Hide();
            if (_board.IsOpen)
                _board.Hide();
            var plaza = SettlementPlazaView.Active;
            if (plaza != null)
                plaza.FocusOtter(request.AssignSpecialist.OtterId);
            else if (PlazaZone != null && _navigator != null)
                _navigator.TryGo(PlazaZone);
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

        // 배치 부탁처럼 건설이 없는 부탁은 부탁 제목·그림 (배치한 해달 얼굴)
        string title = construction != null ? construction.DisplayName : request.Title;
        Sprite icon = construction != null && construction.Icon != null ? construction.Icon
            : request.AssignSpecialist != null && request.AssignSpecialist.Portrait != null ? request.AssignSpecialist.Portrait
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
        for (int i = 0; i < config.StageCount; i++)
            text.Append(config.StageName(i));
        if (config.GatherItem != null)
            text.Append(config.GatherItem.DisplayName);
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
