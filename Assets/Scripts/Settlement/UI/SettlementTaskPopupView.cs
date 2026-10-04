using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주민 작업 화면 (시안: [광산 주변 정리] 필요 인원 1명 · 작업 시간 30초 / 보낼 해달: [몽실] / [작업 시작]).
/// 세 가지 모습: 보낼 해달 고르기 → 작업 중(진행 막대·남은 시간·일하는 해달) → 끝남(안내 + 확인).
/// 버튼·해달 칸 클릭을 알리기만 한다.
/// </summary>
public class SettlementTaskPopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("내용")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Tooltip("필요 인원 (예: 1명)")]
    [SerializeField] private TextMeshProUGUI _workersText;

    [Tooltip("작업 시간 (예: 30초)")]
    [SerializeField] private TextMeshProUGUI _durationText;

    [Tooltip("필요 인원·작업 시간 줄 (끝난 모습에서는 숨김)")]
    [SerializeField] private GameObject _infoRow;

    [Header("보낼 해달")]
    [SerializeField] private GameObject _assignGroup;

    [Tooltip("주민 해달 칸 (남는 칸은 숨김)")]
    [SerializeField] private List<WorkerChipView> _workerChips = new List<WorkerChipView>();

    [Header("필요 재료")]
    [Tooltip("\"필요 재료\" 제목 + 재료 칸 줄 (비용이 없는 작업·작업 중·끝난 모습에서는 숨김)")]
    [SerializeField] private GameObject _costGroup;

    [Tooltip("재료 칸 (골드부터. 아이콘 + 가진 / 필요, 넉넉하면 체크·모자라면 빨강). 남는 칸은 숨김")]
    [SerializeField] private List<CostChipView> _costChips = new List<CostChipView>();

    [Header("작업 중")]
    [SerializeField] private GameObject _progressGroup;
    [SerializeField] private ProgressBarView _bar;

    [Tooltip("남은 시간 (예: 남은 시간 00:12)")]
    [SerializeField] private TextMeshProUGUI _timeText;

    [Tooltip("일하는 해달 (예: 몽실 작업 중)")]
    [SerializeField] private TextMeshProUGUI _crewText;

    [Header("안내")]
    [SerializeField] private TextMeshProUGUI _noteText;

    [Header("버튼")]
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _startLabel;
    [SerializeField] private Button _closeButton;

    private bool _done;

    public bool IsOpen => _animator.IsOpen;
    public SettlementTaskDefinition Task { get; private set; }

    /// <summary>[작업 시작]을 눌렀을 때 (끝난 모습의 [확인]은 여기서 바로 닫음)</summary>
    public event Action OnStartClicked;
    public event Action<SettlementOtterDefinition> OnWorkerClicked;

    private void Awake()
    {
        _startButton.onClick.AddListener(HandleStart);
        _closeButton.onClick.AddListener(() => _animator.Hide());
        foreach (var chip in _workerChips)
            chip.OnClicked += c => OnWorkerClicked?.Invoke(c.Otter);
    }

    private void HandleStart()
    {
        if (_done)
            _animator.Hide();
        else
            OnStartClicked?.Invoke();
    }

    /// <summary>보낼 해달 고르기</summary>
    /// <param name="workers">(해달, 골랐는지, 보낼 수 없는 이유 — 보낼 수 있으면 null)</param>
    /// <param name="costs">(아이콘, 필요, 가진 것) — 골드부터. 비어 있으면 재료 줄을 숨김</param>
    public void ShowAssign(SettlementTaskDefinition task, IReadOnlyList<(SettlementOtterDefinition otter, bool selected, string busy)> workers,
        IReadOnlyList<(Sprite icon, int need, int have)> costs, string note, bool canStart)
    {
        BindHeader(task, task.Description, true);
        _assignGroup.SetActive(true);
        _progressGroup.SetActive(false);
        for (int i = 0; i < _workerChips.Count; i++)
        {
            if (i < workers.Count)
                _workerChips[i].Bind(workers[i].otter, workers[i].selected, workers[i].busy);
            else
                _workerChips[i].Hide();
        }
        BindCosts(costs);
        _noteText.text = note;
        SetButton("작업 시작", canStart);
        Open();
    }

    /// <summary>작업 중: 진행 막대·남은 시간·일하는 해달</summary>
    public void ShowWorking(SettlementTaskDefinition task, float progress, string remaining, string crew)
    {
        BindHeader(task, task.Description, true);
        _assignGroup.SetActive(false);
        _progressGroup.SetActive(true);
        BindCosts(null);
        _bar.SetRatio(progress);
        _timeText.text = $"남은 시간 {remaining}";
        _crewText.text = crew;
        _noteText.text = "끝나면 해달이 돌아와요.";
        SetButton("작업 중", false);
        Open();
    }

    /// <summary>끝남: 안내 + [확인]</summary>
    public void ShowDone(SettlementTaskDefinition task, string message)
    {
        BindHeader(task, message, false);
        _assignGroup.SetActive(false);
        _progressGroup.SetActive(false);
        BindCosts(null);
        _noteText.text = string.Empty;
        SetButton("확인", true);
        _done = true;
        Open();
    }

    /// <summary>시작하지 못했을 때 흔들고 안내를 바꿈</summary>
    public void ShowFailed(string note)
    {
        _noteText.text = note;
        _animator.Shake();
    }

    public void Hide() => _animator.Hide();

    private void BindHeader(SettlementTaskDefinition task, string description, bool showInfo)
    {
        Task = task;
        _done = false;
        _titleText.text = task.Title;
        _descriptionText.text = description;
        _infoRow.SetActive(showInfo);
        _workersText.text = $"{task.RequiredWorkers}명";
        _durationText.text = FormatDuration(task.DurationSeconds);
    }

    // 가진 / 필요 (넉넉하면 체크, 모자라면 빨강). 비용이 없으면 줄째 숨김
    private void BindCosts(IReadOnlyList<(Sprite icon, int need, int have)> costs)
    {
        int count = costs != null ? costs.Count : 0;
        // 화면을 새로 만들기 전의 프리팹(재료 줄 없음)에서도 동작하게
        if (_costGroup != null)
            _costGroup.SetActive(count > 0);
        for (int i = 0; i < _costChips.Count; i++)
        {
            if (i < count)
                _costChips[i].BindProgress(costs[i].icon, costs[i].have, costs[i].need);
            else
                _costChips[i].Hide();
        }
    }

    private void SetButton(string label, bool interactable)
    {
        _startLabel.text = label;
        _startButton.interactable = interactable;
    }

    private void Open()
    {
        if (!_animator.IsOpen)
            _animator.Show();
    }

    private static string FormatDuration(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        if (total < 60)
            return $"{total}초";
        return total % 60 == 0 ? $"{total / 60}분" : $"{total / 60}분 {total % 60}초";
    }
}
