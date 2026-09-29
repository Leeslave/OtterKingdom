using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재화 부족 팝업: "조개가 부족해요", "필요 200 · 보유 120", "80개 부족", [닫기] [조개 충전하기].
/// </summary>
public class CurrencyShortagePopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("내용")]
    [SerializeField] private Image _icon;
    [Tooltip("\"조개가 부족해요\"")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [Tooltip("\"필요 200  ·  보유 120\"")]
    [SerializeField] private TextMeshProUGUI _detailText;
    [Tooltip("\"80개 부족\"")]
    [SerializeField] private TextMeshProUGUI _lackText;

    [Header("버튼")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _chargeButton;
    [Tooltip("\"조개 충전하기\"")]
    [SerializeField] private TextMeshProUGUI _chargeText;

    public Currency Currency { get; private set; }

    /// <summary>[충전하기]를 눌렀을 때 (부족한 재화)</summary>
    public event Action<Currency> OnChargeClicked;

    private void Awake()
    {
        _closeButton.onClick.AddListener(Hide);
        _chargeButton.onClick.AddListener(() => OnChargeClicked?.Invoke(Currency));
    }

    public void Show(Currency currency, int need, int have)
    {
        if (currency == null)
            throw new ArgumentNullException(nameof(currency));

        Currency = currency;
        _icon.sprite = currency.Icon;
        _titleText.text = $"{currency.DisplayName}{KoreanParticle.SubjectParticle(currency.DisplayName)} 부족해요";
        _detailText.text = $"필요 {need:N0}  ·  보유 {have:N0}";
        _lackText.text = $"{Math.Max(0, need - have):N0}개 부족";
        _chargeText.text = $"{currency.DisplayName} 충전하기";

        _animator.Show();
    }

    public void Hide() => _animator.Hide();
}
