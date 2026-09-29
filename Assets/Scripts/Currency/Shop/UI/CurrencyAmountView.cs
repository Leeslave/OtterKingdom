using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재화 아이콘 + 보유량 (충전 화면의 "보유" 줄). 보유량이 바뀌면 바로 반영한다.
/// </summary>
public class CurrencyAmountView : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private Currency _currency;

    [Header("구성 요소")]
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _amountText;

    private CurrencyManager _manager;

    private void Awake()
    {
        if (_currency.Icon != null)
            _icon.sprite = _currency.Icon;
    }

    private void OnEnable()
    {
        _manager = CurrencyManager.Instance;
        _manager.OnCurrencyChanged += HandleCurrencyChanged;

        // 꺼져 있던 동안 바뀐 값 반영
        _amountText.text = _manager.GetCurrency(_currency).ToString("N0");
    }

    private void OnDisable()
    {
        if (_manager != null)
            _manager.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    private void HandleCurrencyChanged(Currency currency, int balance)
    {
        if (currency == _currency)
            _amountText.text = balance.ToString("N0");
    }
}
