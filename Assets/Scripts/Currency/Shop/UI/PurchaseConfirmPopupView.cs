using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 구매 확인 팝업: "조개 50개로 / 골드 5,500을 살까요?", 가격 재화 잔액이 어떻게 바뀌는지(120 → 70), [취소] [가격].
/// 구매 자체는 모르고, 확인을 알리기만 한다.
/// </summary>
public class PurchaseConfirmPopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("내용")]
    [SerializeField] private Image _icon;
    [Tooltip("\"조개 50개로\"")]
    [SerializeField] private TextMeshProUGUI _priceLine;
    [Tooltip("\"골드 5,500을 살까요?\"")]
    [SerializeField] private TextMeshProUGUI _questionLine;

    [Header("남는 재화")]
    [SerializeField] private Image _balanceIcon;
    [Tooltip("\"120  →  70\"")]
    [SerializeField] private TextMeshProUGUI _balanceText;
    [Tooltip("\"남는 조개\"")]
    [SerializeField] private TextMeshProUGUI _balanceLabel;

    [Header("버튼")]
    [SerializeField] private Button _cancelButton;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Image _confirmIcon;
    [SerializeField] private TextMeshProUGUI _confirmText;

    public CurrencyPack Pack { get; private set; }

    public event Action<CurrencyPack> OnConfirmed;

    private void Awake()
    {
        _cancelButton.onClick.AddListener(Hide);
        _confirmButton.onClick.AddListener(() => OnConfirmed?.Invoke(Pack));
    }

    /// <param name="balance">지금 가진 가격 재화 (살 수 있을 때만 연다)</param>
    public void Show(CurrencyPack pack, int balance)
    {
        if (pack == null)
            throw new ArgumentNullException(nameof(pack));
        if (pack.IsCashPack)
            throw new ArgumentException("현금 상품은 스토어 결제 화면을 쓴다.", nameof(pack));

        Pack = pack;
        var price = pack.PriceCurrency;

        _icon.sprite = pack.Icon;
        _priceLine.text = $"{price.DisplayName} {pack.Price:N0}개로";
        _questionLine.text = $"{pack.Reward.DisplayName} {pack.TotalAmount:N0}{KoreanParticle.ObjectParticle(pack.TotalAmount)} 살까요?";

        _balanceIcon.sprite = price.Icon;
        _balanceText.text = $"{balance:N0}  →  {balance - pack.Price:N0}";
        _balanceLabel.text = $"남는 {price.DisplayName}";

        _confirmIcon.sprite = price.Icon;
        _confirmText.text = pack.Price.ToString("N0");

        _animator.Show();
    }

    public void Hide() => _animator.Hide();

    /// <summary>그사이 잔액이 부족해져 실패 → 흔들기</summary>
    public void ShowFailed() => _animator.Shake();
}
