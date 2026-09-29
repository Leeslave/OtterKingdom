using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단바의 재화 칸 하나 (아이콘 + 보유량 + [+]). 보유량이 바뀌면 바로 반영하고, [+]는 클릭을 알리기만 한다.
/// </summary>
public class CurrencyPillView : MonoBehaviour
{
    // 이보다 작으면 쉼표만 (12,480), 크면 짧게 (1.2M)
    private const int ShortFormatFrom = 1_000_000;

    [Header("데이터")]
    [Tooltip("표시할 재화 (Gold, Gem)")]
    [SerializeField] private Currency _currency;

    [Header("구성 요소")]
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _amountText;
    [Tooltip("충전·상점으로 가는 [+]")]
    [SerializeField] private Button _plusButton;

    public Currency Currency => _currency;

    /// <summary>[+]를 눌렀을 때 (상점이 생기면 연결)</summary>
    public event Action<Currency> OnPlusClicked;

    private CurrencyManager _manager;

    private void Awake()
    {
        _plusButton.onClick.AddListener(() => OnPlusClicked?.Invoke(_currency));

        if (_currency.Icon != null)
            _icon.sprite = _currency.Icon;
    }

    private void OnEnable()
    {
        _manager = CurrencyManager.Instance;
        _manager.OnCurrencyChanged += HandleCurrencyChanged;

        // 꺼져 있던 동안 바뀐 값 반영
        Show(_manager.GetCurrency(_currency));
    }

    private void OnDisable()
    {
        if (_manager != null)
            _manager.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    private void HandleCurrencyChanged(Currency currency, int balance)
    {
        if (currency == _currency)
            Show(balance);
    }

    private void Show(int balance)
    {
        _amountText.text = balance < ShortFormatFrom ? balance.ToString("N0") : NumberFormatter.Short(balance);
    }
}
