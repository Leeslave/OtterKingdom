using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
public class CurrencyUI : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private Currency _targetCurrency;
    [SerializeField] private TextMeshProUGUI _uIText;
    [SerializeField] private Image _currencyIcon;

    [Header("애니메이션 설정")]
    [SerializeField] private float _animationDuration = 0.5f; // 효과 지속 시간

    private int _currentDisplayValue = 0; 
    private Coroutine _iconCoroutine;
    private Coroutine _textCoroutine;

    private void Awake()
    {
        if (_targetCurrency != null)
            _currencyIcon.sprite = _targetCurrency.Icon;
    }

    private void Start()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCurrencyChanged += UpdateUI;
            RefreshUI();
        }
    }

    private void OnDisable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged -= UpdateUI;
    }

    private void UpdateUI(Currency changedCurrency, int currentAmount)
    {
        if (changedCurrency == _targetCurrency)
        {
           PlayAnimations(currentAmount);
        }
    }
    private void RefreshUI()
    {
        if (_targetCurrency == null) return;
        int amount = CurrencyManager.Instance.GetCurrency(_targetCurrency);
        _currentDisplayValue = amount;
        _uIText.text = amount.ToString();
    }

    #region UI Animation
    private void PlayAnimations(int targetValue)
    {
        if (_textCoroutine != null) StopCoroutine(_textCoroutine);

        _textCoroutine = StartCoroutine(AnimateText(targetValue));
    }

    // 텍스트 카운트 업 효과
    private IEnumerator AnimateText(int targetValue)
    {
        int startValue = _currentDisplayValue;
        float elapsed = 0f;

        while (elapsed < _animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _animationDuration;

            // 숫자가 서서히 증가하도록 보간
            int val = Mathf.RoundToInt(Mathf.Lerp(startValue, targetValue, t));
            _uIText.text = val.ToString();
            yield return null;
        }

        _uIText.text = targetValue.ToString();
        _currentDisplayValue = targetValue;
    }

    #endregion
}
