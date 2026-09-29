using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 판매 팝업: 수량을 고르고 받을 금액을 보여준 뒤, 확인하면 고른 수량을 알린다 (판매 자체는 모름).
/// </summary>
public class SellPopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("아이템")]
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;

    [Header("수량")]
    [SerializeField] private TextMeshProUGUI _quantityText;
    [SerializeField] private Button _minusButton;
    [SerializeField] private Button _plusButton;
    [SerializeField] private Button _maxButton;

    [Header("받을 금액")]
    [SerializeField] private Image _currencyIcon;
    [SerializeField] private TextMeshProUGUI _totalText;

    [Header("확인")]
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;

    /// <summary>판매 확인 (고른 수량)</summary>
    public event Action<int> OnConfirmed;

    private readonly SellQuantity _quantity = new SellQuantity();
    private ItemDefinition _item;
    private Currency _currency;

    public bool IsOpen => _animator.IsOpen;

    private void Awake()
    {
        _minusButton.onClick.AddListener(() => { _quantity.Decrease(); Refresh(); });
        _plusButton.onClick.AddListener(() => { _quantity.Increase(); Refresh(); });
        _maxButton.onClick.AddListener(() => { _quantity.SetToMax(); Refresh(); });
        _confirmButton.onClick.AddListener(() => OnConfirmed?.Invoke(_quantity.Value));
        _cancelButton.onClick.AddListener(Hide);
    }

    public void Show(ItemDefinition item, int owned, Currency currency)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (currency == null) throw new ArgumentNullException(nameof(currency));

        _item = item;
        _currency = currency;
        _quantity.Reset(owned);

        _icon.sprite = item.Icon;
        _icon.enabled = item.Icon != null;
        _nameText.text = item.DisplayName;

        _currencyIcon.sprite = currency.Icon;
        _currencyIcon.gameObject.SetActive(currency.Icon != null);

        Refresh();
        _animator.Show();
    }

    public void Hide() => _animator.Hide();

    /// <summary>판매 실패 (그사이 개수가 줄었거나 판매 불가) → 흔들기</summary>
    public void ShowFailed() => _animator.Shake();

    private void Refresh()
    {
        _quantityText.text = NumberFormatter.Short(_quantity.Value);
        _minusButton.interactable = _quantity.CanDecrease;
        _plusButton.interactable = _quantity.CanIncrease;
        _maxButton.interactable = _quantity.CanIncrease;

        // 아이콘이 없으면 재화 이름으로 대신 표시 (예: "골드 150")
        string amount = NumberFormatter.Short(_quantity.Total(_item.SellPrice));
        _totalText.text = _currency.Icon != null ? amount : $"{_currency.DisplayName} {amount}";
    }
}
