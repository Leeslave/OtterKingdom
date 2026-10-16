using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 칸 확장 구매 확인 팝업. 받은 값을 그리고 확인 클릭을 알리기만 한다 (결제는 모름).
/// </summary>
public class ExpandPopupView : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private UIPopupAnimator _animator;

    [Header("구성 요소")]
    [Tooltip("\"16칸 → 20칸\"")]
    [SerializeField] private TextMeshProUGUI _capacityText;
    [SerializeField] private Image _currencyIcon;
    [SerializeField] private TextMeshProUGUI _costText;
    [Tooltip("재화 부족 안내")]
    [SerializeField] private TextMeshProUGUI _warningText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;

    public event Action OnConfirmClicked;

    private void Awake()
    {
        _confirmButton.onClick.AddListener(() => OnConfirmClicked?.Invoke());
        _cancelButton.onClick.AddListener(Hide);
    }

    public void Show(int currentCapacity, int newCapacity, Currency currency, int cost, bool canAfford)
    {
        if (currency == null) throw new ArgumentNullException(nameof(currency));

        _capacityText.text = $"{currentCapacity}칸 → {newCapacity}칸";

        // 아이콘이 없으면 이름으로 대신 표시 (예: "조개 100")
        bool hasIcon = currency.Icon != null;
        _currencyIcon.sprite = currency.Icon;
        _currencyIcon.gameObject.SetActive(hasIcon);
        string amount = NumberFormatter.Short(cost);
        _costText.text = hasIcon ? amount : $"{currency.DisplayName} {amount}";

        SetAffordable(canAfford, currency);
        _animator.Show();
    }

    /// <summary>결제 실패 (연출 + 안내)</summary>
    public void ShowFailed(Currency currency)
    {
        SetAffordable(false, currency);
        _animator.Shake();
    }

    public void Hide()
    {
        _animator.Hide();
    }

    private void SetAffordable(bool canAfford, Currency currency)
    {
        _warningText.gameObject.SetActive(!canAfford);
        _warningText.text = $"{currency.DisplayName}{SubjectParticle(currency.DisplayName)} 부족해요";
        _confirmButton.interactable = canAfford;
    }

    // 받침이 있으면 "이", 없으면 "가" (조개가 / 보석이)
    private static string SubjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "가";

        char last = word[word.Length - 1];
        bool isHangul = last >= '가' && last <= '힣';
        bool hasFinalConsonant = isHangul && (last - '가') % 28 != 0;
        return hasFinalConsonant ? "이" : "가";
    }
}
