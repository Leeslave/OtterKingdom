using UnityEngine;

/// <summary>
/// 재화 충전 상품 하나 (예: 조개 50개 → 골드 5,500). 가격 재화가 비어 있으면 현금 상품(스토어 결제).
/// </summary>
[CreateAssetMenu(fileName = "CurrencyPack", menuName = "Game Data/Currency/Currency Pack")]
public class CurrencyPack : ScriptableObject
{
    [Header("식별 및 기본정보")]
    [Tooltip("Key로 사용될 ID (스토어 상품 ID와 맞출 예정, 예: gold_5500)")]
    [SerializeField]
    private string _packId;

    [Tooltip("상점 안 정렬 순서 (작을수록 앞)")]
    [SerializeField]
    private int _sortOrder;

    [Tooltip("카드에 보일 그림 (재화 더미)")]
    [SerializeField]
    private Sprite _icon;

    [Header("받는 것")]
    [Tooltip("받는 재화")]
    [SerializeField]
    private Currency _reward;

    [Tooltip("기본 수량")]
    [Min(1)]
    [SerializeField]
    private int _baseAmount = 1;

    [Tooltip("보너스 수량 (카드에 \"+10%\"로 표시)")]
    [Min(0)]
    [SerializeField]
    private int _bonusAmount;

    [Header("가격")]
    [Tooltip("내는 재화 (조개). 비우면 현금 상품")]
    [SerializeField]
    private Currency _priceCurrency;

    [Tooltip("가격. 재화 상품은 재화 개수, 현금 상품은 원")]
    [Min(1)]
    [SerializeField]
    private int _price = 1;

    [Header("표시")]
    [Tooltip("카드 위쪽 금색 뱃지 (예: 최고 효율, 첫 구매 2배). 비우면 없음")]
    [SerializeField]
    private string _highlight;

    public string PackId => _packId;
    public int SortOrder => _sortOrder;
    public Sprite Icon => _icon;
    public Currency Reward => _reward;
    public int BaseAmount => _baseAmount;
    public int BonusAmount => _bonusAmount;
    public Currency PriceCurrency => _priceCurrency;
    public int Price => _price;
    public string Highlight => _highlight;

    public bool IsCashPack => _priceCurrency == null;

    /// <summary>실제로 받는 수량 (기본 + 보너스)</summary>
    public int TotalAmount => (int)System.Math.Min((long)_baseAmount + _bonusAmount, int.MaxValue);

    /// <summary>보너스 비율 (반올림한 %). 보너스가 없으면 0</summary>
    public int BonusPercent => _bonusAmount <= 0 ? 0 : Mathf.RoundToInt(_bonusAmount * 100f / Mathf.Max(1, _baseAmount));

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_packId))
            Debug.LogWarning($"[{name}] PackId가 비어 있습니다.", this);

        if (_reward == null)
            Debug.LogWarning($"[{name}] 받는 재화가 비어 있습니다.", this);
    }
}
