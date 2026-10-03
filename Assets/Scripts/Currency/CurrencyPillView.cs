using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단바의 재화 칸 하나 (아이콘 + 보유량 + [+]). 보유량이 바뀌면 숫자가 굴러가듯 따라가고(늘면 아이콘이 통 튐),
/// [+]는 클릭을 알리기만 한다.
/// </summary>
public class CurrencyPillView : MonoBehaviour
{
    // 이보다 작으면 쉼표만 (12,480), 크면 짧게 (1.2M)
    private const int ShortFormatFrom = 1_000_000;
    // 숫자가 새 값까지 굴러가는 시간, 아이콘이 통 튀는 시간·크기
    private const float CountSeconds = 0.6f;
    private const float PopSeconds = 0.3f;
    private const float PopScale = 0.25f;

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
    private int _shown;
    private int _countFrom;
    private int _countTo;
    private float _countTime = -1f;
    private float _popTime = -1f;

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

        // 꺼져 있던 동안 바뀐 값은 바로 반영
        _countTime = -1f;
        _popTime = -1f;
        _icon.rectTransform.localScale = Vector3.one;
        _shown = _manager.GetCurrency(_currency);
        Show(_shown);
    }

    private void OnDisable()
    {
        if (_manager != null)
            _manager.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    private void HandleCurrencyChanged(Currency currency, int balance)
    {
        if (currency != _currency || balance == _shown)
            return;
        if (balance > _shown)
            _popTime = 0f;
        _countFrom = _shown;
        _countTo = balance;
        _countTime = 0f;
    }

    private void Update()
    {
        if (_countTime >= 0f)
        {
            _countTime += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_countTime / CountSeconds);
            float eased = 1f - (1f - k) * (1f - k) * (1f - k);
            _shown = Mathf.RoundToInt(Mathf.Lerp(_countFrom, _countTo, eased));
            Show(_shown);
            if (k >= 1f)
                _countTime = -1f;
        }
        if (_popTime >= 0f)
        {
            _popTime += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_popTime / PopSeconds);
            _icon.rectTransform.localScale = Vector3.one * (1f + PopScale * Mathf.Sin(Mathf.PI * k));
            if (k >= 1f)
                _popTime = -1f;
        }
    }

    private void Show(int balance)
    {
        _amountText.text = balance < ShortFormatFrom ? balance.ToString("N0") : NumberFormatter.Short(balance);
    }
}
