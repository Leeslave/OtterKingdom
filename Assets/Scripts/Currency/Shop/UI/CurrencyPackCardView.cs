using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 충전 상품 카드 하나: 재화 더미 그림, "골드 5,500", 보너스 설명, 뱃지, 가격 버튼.
/// </summary>
public class CurrencyPackCardView : MonoBehaviour
{
    [Header("내용")]
    [SerializeField] private Image _icon;
    [Tooltip("\"골드 5,500\"")]
    [SerializeField] private TextMeshProUGUI _nameText;
    [Tooltip("\"기본 5,000 + 500\" (보너스가 있을 때만)")]
    [SerializeField] private TextMeshProUGUI _detailText;
    [Tooltip("카드가 낮아 설명 줄을 쓰지 않는 상점이면 끔 (보너스는 +% 뱃지로만)")]
    [SerializeField] private bool _showDetail = true;

    [Header("뱃지")]
    [Tooltip("오른쪽 위 산호색 \"+10%\"")]
    [SerializeField] private GameObject _bonusTag;
    [SerializeField] private TextMeshProUGUI _bonusText;
    [Tooltip("왼쪽 위 금색 \"최고 효율\" 등")]
    [SerializeField] private GameObject _highlightTag;
    [SerializeField] private TextMeshProUGUI _highlightText;

    [Header("가격 버튼")]
    [SerializeField] private Button _priceButton;
    [Tooltip("가격 재화 아이콘 (현금 상품이면 숨김)")]
    [SerializeField] private Image _priceIcon;
    [SerializeField] private TextMeshProUGUI _priceText;

    public CurrencyPack Pack { get; private set; }

    public event Action<CurrencyPackCardView> OnBuyClicked;

    private void Awake()
    {
        _priceButton.onClick.AddListener(() => OnBuyClicked?.Invoke(this));
    }

    public void Bind(CurrencyPack pack)
    {
        if (pack == null)
            throw new ArgumentNullException(nameof(pack));

        Pack = pack;

        _icon.sprite = pack.Icon;
        _icon.enabled = pack.Icon != null;
        _nameText.text = $"{pack.Reward.DisplayName} {pack.TotalAmount:N0}";

        bool hasBonus = pack.BonusAmount > 0;
        _detailText.gameObject.SetActive(_showDetail && hasBonus);
        _detailText.text = $"기본 {pack.BaseAmount:N0} + {pack.BonusAmount:N0}";
        _bonusTag.SetActive(hasBonus);
        _bonusText.text = $"+{pack.BonusPercent}%";

        bool highlighted = !string.IsNullOrWhiteSpace(pack.Highlight);
        _highlightTag.SetActive(highlighted);
        _highlightText.text = pack.Highlight;

        _priceIcon.gameObject.SetActive(!pack.IsCashPack);
        if (!pack.IsCashPack)
            _priceIcon.sprite = pack.PriceCurrency.Icon;
        // 제목 폰트(Cafe24)에 ₩ 기호가 없어 "원"으로 표기 (스토어 결제가 붙으면 스토어가 주는 가격 문자열로 바꿈)
        _priceText.text = pack.IsCashPack ? $"{pack.Price:N0}원" : pack.Price.ToString("N0");
    }
}
