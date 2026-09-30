using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 요정 상점 구매 팝업: 그림·이름·설명·보유 수, 수량 [−][n][+], 합계, [취소] [가격].
/// 구매 자체는 모르고, 고른 수량으로 확인을 알리기만 한다.
/// </summary>
public class ShopPurchasePopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("상품")]
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [Tooltip("\"보유: 2개\"")]
    [SerializeField] private TextMeshProUGUI _ownedText;
    [Tooltip("살 수 없을 때 이유 (가방이 가득 찼어요)")]
    [SerializeField] private TextMeshProUGUI _warningText;

    [Header("수량")]
    [SerializeField] private TextMeshProUGUI _quantityText;
    [SerializeField] private Button _minusButton;
    [SerializeField] private Button _plusButton;

    [Header("합계")]
    [SerializeField] private Image _totalIcon;
    [SerializeField] private TextMeshProUGUI _totalText;

    [Header("버튼")]
    [SerializeField] private Button _cancelButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Image _confirmImage;
    [SerializeField] private Image _confirmIcon;
    [SerializeField] private TextMeshProUGUI _confirmText;
    [SerializeField] private CurrencySpriteMap _confirmSprites = new CurrencySpriteMap();

    public ShopProduct Product { get; private set; }

    /// <summary>[가격] 버튼 (고른 묶음 수)</summary>
    public event Action<int> OnConfirmed;

    private readonly SellQuantity _quantity = new SellQuantity();

    private void Awake()
    {
        _minusButton.onClick.AddListener(() => { _quantity.Decrease(); Refresh(); });
        _plusButton.onClick.AddListener(() => { _quantity.Increase(); Refresh(); });
        _cancelButton.onClick.AddListener(Hide);
        _closeButton.onClick.AddListener(Hide);
        _confirmButton.onClick.AddListener(() => OnConfirmed?.Invoke(_quantity.Value));
    }

    /// <param name="owned">지금 가방에 있는 개수</param>
    /// <param name="maxQuantity">고를 수 있는 최대 묶음 수 (1 이상. 가방이 가득 차도 1로 열고, 사려 할 때 막음)</param>
    public void Show(ShopProduct product, int owned, int maxQuantity)
    {
        if (product == null)
            throw new ArgumentNullException(nameof(product));

        Product = product;
        _quantity.Reset(Mathf.Max(1, maxQuantity));

        _icon.sprite = product.Item.Icon;
        _icon.enabled = _icon.sprite != null;
        _nameText.text = product.DisplayName;
        _descriptionText.text = product.Item.Description;
        _ownedText.text = $"보유: {owned:N0}개";
        _warningText.gameObject.SetActive(false);

        var currency = product.PriceCurrency;
        _totalIcon.sprite = currency.Icon;
        _confirmIcon.sprite = currency.Icon;
        _confirmImage.sprite = _confirmSprites.Get(currency);

        Refresh();
        _animator.Show();
    }

    public void Hide() => _animator.Hide();

    /// <summary>살 수 없음 (가방이 가득 참) → 이유를 보여주고 흔들기</summary>
    public void ShowFailed(string reason)
    {
        _warningText.text = reason;
        _warningText.gameObject.SetActive(true);
        _animator.Shake();
    }

    private void Refresh()
    {
        _quantityText.text = _quantity.Value.ToString();
        _minusButton.interactable = _quantity.CanDecrease;
        _plusButton.interactable = _quantity.CanIncrease;

        string total = ShopPurchaseRules.TotalPrice(Product, _quantity.Value).ToString("N0");
        _totalText.text = total;
        _confirmText.text = total;
    }
}
