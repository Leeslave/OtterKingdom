using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업 화면의 주민 해달 한 칸: 얼굴, 이름, 고르면 체크. 보낼 수 없으면 흐리게 + 이유("작업 중" 등).
/// 클릭을 알리기만 한다.
/// </summary>
public class WorkerChipView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Image _portrait;
    [SerializeField] private TextMeshProUGUI _nameText;

    [Tooltip("골랐을 때 체크")]
    [SerializeField] private GameObject _check;

    [Tooltip("보낼 수 없을 때만 보이는 이유 띠 (얼굴 아래)")]
    [SerializeField] private GameObject _busyTag;

    [Tooltip("이유 글자 (예: 작업 중)")]
    [SerializeField] private TextMeshProUGUI _busyText;

    [Tooltip("보낼 수 없으면 흐리게")]
    [SerializeField] private CanvasGroup _group;

    [SerializeField] private Button _button;

    public SettlementOtterDefinition Otter { get; private set; }

    public event Action<WorkerChipView> OnClicked;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    /// <param name="busyLabel">보낼 수 없는 이유 (보낼 수 있으면 null)</param>
    public void Bind(SettlementOtterDefinition otter, bool selected, string busyLabel)
    {
        Otter = otter;
        gameObject.SetActive(true);
        _portrait.sprite = otter.Portrait;
        _portrait.enabled = otter.Portrait != null;
        _nameText.text = otter.DisplayName;
        _check.SetActive(selected);

        bool busy = busyLabel != null;
        _busyTag.SetActive(busy);
        if (busy)
            _busyText.text = busyLabel;
        _group.alpha = busy ? 0.5f : 1f;
        _button.interactable = !busy;
    }

    public void Hide() => gameObject.SetActive(false);
}
