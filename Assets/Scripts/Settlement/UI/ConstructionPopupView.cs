using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 건설 팝업 (시안: "건설 해달 — 집을 짓고 길을 열어요 / 새 이웃의 집 / 150 · 12 · 8 / [건설 시작]").
/// 말하는 해달(건설 해달 또는 부탁한 해달) + 건설 이름·그림 + 비용 칸 + 시작 버튼. [시작]을 알리기만 한다.
/// </summary>
public class ConstructionPopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("말하는 해달")]
    [SerializeField] private Image _speakerPortrait;
    [SerializeField] private TextMeshProUGUI _speakerName;
    [SerializeField] private TextMeshProUGUI _speakerLine;

    [Header("건설")]
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [Tooltip("비용 칸 (골드, 재료1, 재료2 순서). 남는 칸은 숨김")]
    [SerializeField] private List<CostChipView> _costChips = new List<CostChipView>();

    [Tooltip("걸리는 시간·안내 (예: 30초 걸려요)")]
    [SerializeField] private TextMeshProUGUI _noteText;

    [Header("버튼")]
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _startLabel;
    [SerializeField] private Button _closeButton;

    public bool IsOpen => _animator.IsOpen;
    public BoardRequestDefinition Request { get; private set; }

    public event Action OnStartClicked;

    private void Awake()
    {
        _startButton.onClick.AddListener(() => OnStartClicked?.Invoke());
        _closeButton.onClick.AddListener(() => _animator.Hide());
    }

    /// <param name="costs">(아이콘, 필요, 보유) — 골드부터</param>
    public void Show(BoardRequestDefinition request, SettlementOtterDefinition speaker, string speakerLine,
        IReadOnlyList<(Sprite icon, int need, int have)> costs, string note, string startLabel, bool canStart)
    {
        Request = request;
        _speakerPortrait.sprite = speaker != null ? speaker.Portrait : null;
        _speakerPortrait.enabled = _speakerPortrait.sprite != null;
        _speakerName.text = speaker != null ? speaker.DisplayName : string.Empty;
        _speakerLine.text = speakerLine;

        // 배치 부탁처럼 건설이 없는 부탁은 부탁 그림·제목
        var construction = request.Construction;
        _icon.sprite = construction != null && construction.Icon != null ? construction.Icon : request.Icon;
        _icon.enabled = _icon.sprite != null;
        _nameText.text = construction != null ? construction.DisplayName : request.Title;
        BindCosts(costs);

        _noteText.text = note;
        _startLabel.text = startLabel;
        _startButton.interactable = canStart;

        if (!_animator.IsOpen)
            _animator.Show();
    }

    private void BindCosts(IReadOnlyList<(Sprite icon, int need, int have)> costs)
    {
        for (int i = 0; i < _costChips.Count; i++)
        {
            if (i < costs.Count)
                _costChips[i].BindCost(costs[i].icon, costs[i].need, costs[i].have);
            else
                _costChips[i].Hide();
        }
    }

    /// <summary>시작하지 못했을 때 흔들고 안내를 바꿈</summary>
    public void ShowFailed(string note)
    {
        _noteText.text = note;
        _animator.Shake();
    }

    public void Hide() => _animator.Hide();
}
