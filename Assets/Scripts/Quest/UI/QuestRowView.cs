using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 목록 한 줄: 그림, 제목, 조건, 진행 바, 보상, 오른쪽 버튼.
/// 버튼: 달성 → 금색 [보상 받기], 진행 중 → 회색 [진행 중], 받음 → 회색 [완료] + 체크.
/// </summary>
public class QuestRowView : MonoBehaviour
{
    [Header("내용")]
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Header("진행")]
    [SerializeField] private ProgressBarView _progressBar;
    [Tooltip("\"1 / 3\"")]
    [SerializeField] private TextMeshProUGUI _progressText;

    [Header("보상")]
    [SerializeField] private Image _rewardIcon;
    [SerializeField] private TextMeshProUGUI _rewardText;
    [Tooltip("\"경험치 50\" (없으면 숨김)")]
    [SerializeField] private GameObject _expTag;
    [SerializeField] private TextMeshProUGUI _expText;

    [Header("종류")]
    [Tooltip("일일 퀘스트 표시")]
    [SerializeField] private GameObject _dailyTag;

    [Header("버튼")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _buttonImage;
    [SerializeField] private TextMeshProUGUI _buttonLabel;

    [Header("받음 표시")]
    [Tooltip("그림 칸 모서리의 초록 체크")]
    [SerializeField] private GameObject _claimedCheck;
    [Tooltip("받은 퀘스트는 흐리게")]
    [SerializeField] private CanvasGroup _group;
    [Range(0f, 1f)]
    [SerializeField] private float _claimedAlpha = 0.6f;

    [Header("버튼 스프라이트")]
    [SerializeField] private Sprite _claimableSprite;
    [SerializeField] private Sprite _idleSprite;

    [Header("버튼 글자색")]
    [SerializeField] private Color _claimableTextColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    [SerializeField] private Color _idleTextColor = new Color32(0x9C, 0x7C, 0x66, 0xFF);

    public QuestDefinition Quest { get; private set; }

    /// <summary>[보상 받기]를 눌렀을 때 (달성한 퀘스트만 버튼이 눌림)</summary>
    public event Action<QuestRowView> OnClaimClicked;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClaimClicked?.Invoke(this));
    }

    /// <param name="exp">받으면 얻는 경험치 (지금 레벨 기준)</param>
    public void Bind(QuestDefinition quest, int progress, QuestStatus status, int exp)
    {
        if (quest == null)
            throw new ArgumentNullException(nameof(quest));

        Quest = quest;

        _icon.sprite = quest.Icon;
        _icon.enabled = quest.Icon != null;
        _titleText.text = quest.Title;
        _descriptionText.text = quest.Description;

        _progressBar.SetProgress(progress, quest.Goal);
        _progressText.text = $"{NumberFormatter.Short(progress)} / {NumberFormatter.Short(quest.Goal)}";

        var currency = quest.RewardCurrency;
        _rewardIcon.sprite = currency != null ? currency.Icon : null;
        _rewardIcon.enabled = _rewardIcon.sprite != null;
        _rewardText.text = NumberFormatter.Short(quest.RewardAmount);
        _expTag.SetActive(exp > 0);
        _expText.text = $"경험치 {NumberFormatter.Short(exp)}";
        _dailyTag.SetActive(quest.Kind == QuestKind.Daily);

        bool claimable = status == QuestStatus.Claimable;
        bool claimed = status == QuestStatus.Claimed;
        _button.interactable = claimable;
        _buttonImage.sprite = claimable ? _claimableSprite : _idleSprite;
        _buttonLabel.text = claimable ? "보상 받기" : claimed ? "완료" : "진행 중";
        _buttonLabel.color = claimable ? _claimableTextColor : _idleTextColor;
        _claimedCheck.SetActive(claimed);
        _group.alpha = claimed ? _claimedAlpha : 1f;
    }
}
