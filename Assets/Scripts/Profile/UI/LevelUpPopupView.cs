using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 팝업: "레벨 업!", 새 레벨, 받은 보상(조개 등), 안내 한 줄, [확인]. 확인을 알리기만 한다.
/// </summary>
public class LevelUpPopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("내용")]
    [Tooltip("\"Lv.5\"")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [Tooltip("보상 줄 (보상이 없는 레벨이면 숨김)")]
    [SerializeField] private GameObject _reward;
    [SerializeField] private Image _rewardIcon;
    [SerializeField] private TextMeshProUGUI _rewardText;
    [Tooltip("안내 한 줄 (\"새 퀘스트가 열렸어요!\" 또는 새로 열린 장소)")]
    [SerializeField] private TextMeshProUGUI _noteText;

    [Header("버튼")]
    [SerializeField] private Button _confirmButton;

    public bool IsOpen => _animator.IsOpen;

    /// <summary>[확인]을 눌렀거나 배경을 눌러 닫혔을 때</summary>
    public event Action OnClosed;

    private void Awake()
    {
        _confirmButton.onClick.AddListener(() => _animator.Hide());
        _animator.OnHidden += () => OnClosed?.Invoke();
    }

    public void Show(int level, Currency rewardCurrency, int rewardAmount, string note)
    {
        SettingsManager.VibrateMoment();
        _levelText.text = $"Lv.{level}";
        _noteText.text = note;

        bool hasReward = rewardCurrency != null && rewardAmount > 0;
        _reward.SetActive(hasReward);
        if (hasReward)
        {
            _rewardIcon.sprite = rewardCurrency.Icon;
            _rewardText.text = $"+{rewardAmount:N0}";
        }

        _animator.Show();
        PopupGlow.Play(_animator, _levelText.rectTransform);
    }
}
