using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 주민 작업 화면을 SettlementManager와 잇는다 (전역 UI 루트에 붙음).
/// 작업 화면은 광산의 망치 표지판·게시판에서 열리고(SettlementManager.RequestTask), 보낼 해달을 골라 시작한다.
/// 작업이 끝나면 알려 준다 (그 작업으로 지역이 운영되면 게시판 부탁 완료 팝업이 대신 알림).
/// </summary>
public class SettlementTaskPresenter : MonoBehaviour
{
    private const float RefreshSeconds = 0.25f;

    [Header("화면")]
    [SerializeField] private SettlementTaskPopupView _popup;

    private SettlementManager _manager;
    private readonly List<SettlementOtterDefinition> _residents = new List<SettlementOtterDefinition>();
    private readonly List<SettlementOtterDefinition> _selected = new List<SettlementOtterDefinition>();
    private readonly List<(SettlementOtterDefinition, bool, string)> _rows = new List<(SettlementOtterDefinition, bool, string)>();
    private readonly List<(Sprite, int, int)> _costs = new List<(Sprite, int, int)>();
    private float _refreshTimer;

    private void OnEnable()
    {
        _popup.OnStartClicked += HandleStartClicked;
        _popup.OnWorkerClicked += HandleWorkerClicked;
    }

    // SettlementManager가 같은 오브젝트에 붙어 있어 깨어나는 순서가 같을 수 있으므로 Start에서 연결
    private void Start()
    {
        _manager = SettlementManager.Instance;
        if (_manager == null)
            return;
        _manager.OnTaskRequested += Open;
        _manager.OnTaskFinished += HandleTaskFinished;
    }

    private void OnDisable()
    {
        _popup.OnStartClicked -= HandleStartClicked;
        _popup.OnWorkerClicked -= HandleWorkerClicked;
        if (_manager != null)
        {
            _manager.OnTaskRequested -= Open;
            _manager.OnTaskFinished -= HandleTaskFinished;
        }
    }

    // 작업 중이면 남은 시간을 짧은 간격으로 다시 그림
    private void Update()
    {
        if (_manager == null || !_popup.IsOpen || _popup.Task == null || _manager.GetTaskState(_popup.Task) != SettlementTaskState.Working)
            return;
        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer > 0f)
            return;
        _refreshTimer = RefreshSeconds;
        Refresh(_popup.Task);
    }

    /// <summary>작업 화면을 연다. 보낼 수 있는 해달을 필요한 수만큼 미리 골라 둠 (한 번에 시작할 수 있게)</summary>
    public void Open(SettlementTaskDefinition task)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));
        _selected.Clear();
        _manager.CollectResidents(_residents);
        foreach (var otter in _residents)
        {
            if (_selected.Count < task.RequiredWorkers && _manager.CanAssign(otter))
                _selected.Add(otter);
        }
        Refresh(task);
    }

    private void Refresh(SettlementTaskDefinition task)
    {
        switch (_manager.GetTaskState(task))
        {
            case SettlementTaskState.Working:
                ShowWorking(task);
                break;
            case SettlementTaskState.Available:
                ShowAssign(task);
                break;
            default:
                if (_popup.IsOpen)
                    _popup.Hide();
                break;
        }
    }

    private void ShowAssign(SettlementTaskDefinition task)
    {
        _manager.CollectResidents(_residents);
        _rows.Clear();
        int available = 0;
        foreach (var otter in _residents)
        {
            string busy = BusyLabel(otter);
            if (busy == null)
                available++;
            else
                _selected.Remove(otter);
            _rows.Add((otter, _selected.Contains(otter), busy));
        }

        int need = task.RequiredWorkers;
        string missing = _manager.TaskMissingText(task);
        string note;
        if (available < need)
            note = _residents.Count < need
                ? $"주민이 {need}명 있어야 해요. 집을 지어 주민을 늘려요."
                : $"쉬고 있는 주민이 {need}명 있어야 해요. 다른 작업이 끝나면 보낼 수 있어요.";
        else if (_selected.Count < need)
            note = $"보낼 해달을 {need - _selected.Count}명 더 골라요.";
        else if (missing != null)
            note = missing;
        else
            note = "해달이 걸어가서 일을 시작해요.";
        _popup.ShowAssign(task, _rows, CollectCosts(task), note, _selected.Count == need && missing == null);
    }

    // 필요 재료 칸: (아이콘, 필요, 가진 것) — 골드부터, 비용이 없으면 비어 있음
    private List<(Sprite, int, int)> CollectCosts(SettlementTaskDefinition task)
    {
        _costs.Clear();
        var gold = _manager.Config.GoldCurrency;
        if (task.RequiredGold > 0)
            _costs.Add((gold != null ? gold.Icon : null, task.RequiredGold, _manager.GoldBalance));
        foreach (var item in task.RequiredItems)
        {
            if (item != null && item.Item != null && item.Amount > 0)
                _costs.Add((item.Item.Icon, item.Amount, _manager.ItemCount(item.Item)));
        }
        return _costs;
    }

    private void ShowWorking(SettlementTaskDefinition task)
    {
        var job = _manager.GetTaskJob(task);
        long now = SettlementManager.NowTicks;
        var names = new List<string>();
        foreach (var otterId in job.OtterIds)
        {
            var otter = _manager.Config.FindOtter(otterId);
            names.Add(otter != null ? otter.DisplayName : otterId);
        }
        _popup.ShowWorking(task, job.Progress(now), FormatTime(job.Remaining(now)), $"{string.Join(" / ", names)} 작업 중");
    }

    // 보낼 수 없는 이유 (보낼 수 있으면 null)
    private string BusyLabel(SettlementOtterDefinition otter)
    {
        if (_manager.Settlement.GetWorkState(otter.OtterId) == ResidentWorkState.Working)
            return "작업 중";
        if (_manager.IsConstructionWorker(otter))
            return "공사 중";
        return _manager.CanAssign(otter) ? null : "쉬는 중";
    }

    private void HandleWorkerClicked(SettlementOtterDefinition otter)
    {
        var task = _popup.Task;
        if (task == null || otter == null || !_manager.CanAssign(otter))
            return;
        if (!_selected.Remove(otter))
        {
            // 이미 다 골랐으면 먼저 고른 해달을 빼고 새로 고른 해달을 넣음
            if (_selected.Count >= task.RequiredWorkers)
                _selected.RemoveAt(0);
            _selected.Add(otter);
        }
        ShowAssign(task);
    }

    private void HandleStartClicked()
    {
        var task = _popup.Task;
        switch (_manager.TryStartTask(task, _selected))
        {
            case TaskStartResult.Started:
                ShowWorking(task);
                break;
            case TaskStartResult.WrongWorkerCount:
                _popup.ShowFailed($"해달 {task.RequiredWorkers}명을 골라요.");
                break;
            case TaskStartResult.WorkerBusy:
                ShowAssign(task);
                _popup.ShowFailed("다른 일을 하는 해달이 있어요.");
                break;
            case TaskStartResult.NotEnoughGold:
            case TaskStartResult.NotEnoughItems:
                ShowAssign(task);
                _popup.ShowFailed(_manager.TaskMissingText(task) ?? "비용이 모자라요.");
                break;
            default:
                Refresh(task);
                break;
        }
    }

    private void HandleTaskFinished(SettlementTaskDefinition task, bool operatedRegion)
    {
        // 지역이 운영되면 게시판 부탁 완료 팝업(왕국 레벨·새 해달)이 알림
        if (operatedRegion)
        {
            if (_popup.IsOpen)
                _popup.Hide();
            return;
        }
        _popup.ShowDone(task, string.IsNullOrEmpty(task.CompletionMessage)
            ? $"{task.Title}{KoreanParticle.SubjectParticle(task.Title)} 끝났어요!"
            : task.CompletionMessage);
    }

    private static string FormatTime(TimeSpan remaining)
    {
        int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        return seconds >= 3600
            ? $"{seconds / 3600}시간 {seconds / 60 % 60:00}분"
            : $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
