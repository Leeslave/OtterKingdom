using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 요정 상점 상품 카드: 분류 칩(씨앗), 희귀도 칩(흔함/레어), 그림, 이름, 가격 버튼(재화 색).
/// </summary>
public class ShopProductCardView : MonoBehaviour
{
    [Serializable]
    public class RarityTag
    {
        [SerializeField] private string _rarityId;
        [SerializeField] private Sprite _sprite;

        public string RarityId => _rarityId;
        public Sprite Sprite => _sprite;
    }

    [Header("내용")]
    [SerializeField] private Button _cardButton;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;

    [Header("칩")]
    [SerializeField] private GameObject _categoryChip;
    [SerializeField] private TextMeshProUGUI _categoryText;
    [SerializeField] private Image _rarityChip;
    [SerializeField] private TextMeshProUGUI _rarityText;
    [Tooltip("희귀도 ID별 칩 배경 (Common / Rare / Epic)")]
    [SerializeField] private List<RarityTag> _rarityTags = new List<RarityTag>();

    [Header("가격 버튼")]
    [SerializeField] private Button _priceButton;
    [SerializeField] private Image _priceButtonImage;
    [SerializeField] private Image _priceIcon;
    [SerializeField] private TextMeshProUGUI _priceText;
    [SerializeField] private CurrencySpriteMap _priceButtonSprites = new CurrencySpriteMap();

    public ShopProduct Product { get; private set; }

    public event Action<ShopProductCardView> OnClicked;

    private void Awake()
    {
        _cardButton.onClick.AddListener(() => OnClicked?.Invoke(this));
        _priceButton.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    private static readonly Color LockedIconColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);

    /// <param name="categoryLabel">분류 칩 글자 (null이면 칩 숨김)</param>
    /// <param name="locked">아직 레벨이 안 됨: 가격 대신 "Lv.N", 그림은 흐리게</param>
    public void Bind(ShopProduct product, string categoryLabel, bool locked = false)
    {
        if (product == null)
            throw new ArgumentNullException(nameof(product));

        Product = product;
        var item = product.Item;

        _icon.sprite = item.Icon;
        _icon.enabled = item.Icon != null;
        _nameText.text = product.DisplayName;

        _categoryChip.SetActive(!string.IsNullOrEmpty(categoryLabel));
        _categoryText.text = categoryLabel;

        var rarity = item.Rarity;
        _rarityChip.gameObject.SetActive(rarity != null);
        if (rarity != null)
        {
            _rarityText.text = rarity.DisplayName;
            var tag = _rarityTags.Find(t => t != null && t.RarityId == rarity.RarityId);
            if (tag != null && tag.Sprite != null)
                _rarityChip.sprite = tag.Sprite;
        }

        _priceButtonImage.sprite = _priceButtonSprites.Get(product.PriceCurrency);
        _priceIcon.sprite = product.PriceCurrency.Icon;
        _priceIcon.enabled = _priceIcon.sprite != null && !locked;
        _priceText.text = locked ? $"Lv.{product.RequiredLevel}" : product.Price.ToString("N0");
        _icon.color = locked ? LockedIconColor : Color.white;
    }
}
