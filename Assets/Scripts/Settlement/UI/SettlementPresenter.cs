using System;
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

    private void OnEnable()
    {
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

        // 남은 시간이 매초 바뀌므로 안내 띠·열린 팝업을 짧은 간격으로 다시 그림
        if (_manager.Settlement.Job != null)
        {
            RefreshGuide();
            if (_board.IsOpen && _board.IsRequestTab)
                FillRequests();
            if (_construction.IsOpen)
                OpenConstruction(_construction.Request);
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
            _guide.Show($"{label}  {FormatTime(job.Remaining(SettlementManager.NowTicks))}", null);
            return;
        }

        var current = _manager.CurrentRequest;
        if (current != null)
        {
            if (!settlement.BoardVisited)
                _guide.Show("게시판을 확인해요", "확인하기");
            else
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

    private bool IsInGoalZone => _navigator != null && _navigator.CurrentZone == _goalZone;

    private void HandleGuideClicked()
    {
        var current = _manager.CurrentRequest;
        if (current != null)
        {
            if (!_manager.Settlement.BoardVisited)
                _manager.RequestBoard(false);
            else
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
            string time = status == RequestStatus.Building ? FormatTime(_manager.Settlement.Job.Remaining(now)) : null;
            _rows.Add((request, status, time));
        }
        _rows.Sort((a, b) => a.Item1.Order.CompareTo(b.Item1.Order));
        _board.BindRequests(_rows);
    }

    #endregion

    #region 건설

    private void OpenConstruction(BoardRequestDefinition request)
    {
        if (request == null || request.Construction == null)
            return;
        if (!_glyphsReady)
            PrepareGlyphs();

        var construction = request.Construction;
        var status = _manager.GetStatus(request);
        if (status == RequestStatus.Completed)
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
            note = $"{construction.ProgressLabel}  {FormatTime(_manager.Settlement.Job.Remaining(SettlementManager.NowTicks))}";
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
        else
        {
            note = MissingText(construction);
            canStart = note == null;
            if (canStart)
                note = construction.IsInstant ? "바로 지을 수 있어요!" : $"{FormatDuration(construction.DurationSeconds)} 걸려요";
        }

        _construction.Show(request, speaker, line, _costs, note, startLabel, canStart);
    }

    // 장소를 직접 치우는 부탁 (광산 길 열기): 비용 없이 [가 보기]로 그 장소에 감
    private void OpenClearing(BoardRequestDefinition request, RequestStatus status)
    {
        var zone = request.ClearZone;
        bool here = _navigator != null && _navigator.CurrentZone == zone;
        string note = status == RequestStatus.Locked ? "아직 할 수 없어요."
            : here ? "길을 막은 나무와 돌을 톡톡 눌러 치워요!"
            : $"{zone.DisplayName}에 가서 길을 막은 나무와 돌을 치워요.";
        _costs.Clear();
        _construction.Show(request, request.Requester, request.Description, _costs, note, "가 보기",
            status == RequestStatus.Available && !here);
    }

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
        if (!_complete.IsOpen)
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
        if (construction.RequiredGold > 0)
            _paid.Add((gold != null ? gold.Icon : null, construction.RequiredGold));
        foreach (var cost in construction.RequiredItems)
        {
            if (cost != null && cost.Item != null && cost.Amount > 0)
                _paid.Add((cost.Item.Icon, cost.Amount));
        }

        int stage = _manager.Settlement.Stage;
        _complete.Show($"{stage + 1:00} · {_manager.StageName}", construction.DisplayName, _paid, request.CompletionMessage);
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
        _progress.SetProgress(job.Progress(now), FormatTime(job.Remaining(now)));
    }

    #endregion

    #region 글자

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
        var text = new StringBuilder("0123456789:/ ,.!?%·시간분초 보기진행중완료확인하기짓기건설개간시작해달게시판방명록의부탁방문기록새로운이도착했어요골드가모자라요개더필요해요아직할수없어요다른공사끝나면있어요바로지을걸려요주민가열렸어요집을짓고길을열어요");
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
                text.Append(otter.DisplayName);
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
