using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상세 패널. 받은 아이템 정보를 그리고 닫기 클릭을 알리기만 한다 (Inventory를 모름).
/// </summary>
public class ItemDetailView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField]
    private Image _icon;

    [SerializeField]
    private Image _background;

    [SerializeField]
    private TextMeshProUGUI _nameText;

    [Tooltip("\"채소 · 흔함\" 형식")]
    [SerializeField]
    private TextMeshProUGUI _subText;

    [SerializeField]
    private TextMeshProUGUI _countText;

    [SerializeField]
    private TextMeshProUGUI _priceText;

    [SerializeField]
    private TextMeshProUGUI _descriptionText;

    [SerializeField]
    private Button _closeButton;

    [Header("배경 스프라이트")]
    [SerializeField]
    private Sprite _filledSprite;

    [SerializeField]
    private Sprite _emptySprite;

    [Header("빈 상태")]
    [SerializeField]
    private string _emptyMessage = "아이템을 선택하세요.";

    public event Action OnCloseClicked;

    private void Awake()
    {
        _closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
    }

    public void Show(ItemDefinition item, int count)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        // 이전 상태(ShowNothing 등)가 남아 있지 않도록 모든 요소를 다시 설정
        _icon.sprite = item.Icon;
        _icon.enabled = item.Icon != null;
        _nameText.text = item.DisplayName;
        _subText.text = BuildSubText(item);
        _countText.text = NumberFormatter.Short(count);
        _priceText.text = NumberFormatter.Short(item.SellPrice);
        _descriptionText.text = item.Description;
        _background.sprite = _filledSprite;
    }

    public void ShowNothing()
    {
        _icon.sprite = null;
        _icon.enabled = false;
        _nameText.text = "";
        _subText.text = "";
        _countText.text = "";
        _priceText.text = "";
        _descriptionText.text = _emptyMessage;
        _background.sprite = _emptySprite;
    }

    // 카테고리/희귀도 중 있는 것만 " · "로 연결 (SO는 == null로 검사)
    private static string BuildSubText(ItemDefinition item)
    {
        bool hasCategory = item.Category != null;
        bool hasRarity = item.Rarity != null;

        if (hasCategory && hasRarity)
            return $"{item.Category.DisplayName} · {item.Rarity.DisplayName}";
        if (hasCategory)
            return item.Category.DisplayName;
        if (hasRarity)
            return item.Rarity.DisplayName;
        return "";
    }
}
