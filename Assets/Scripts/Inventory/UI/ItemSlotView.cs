using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 슬롯 하나. 받은 값을 그리고 클릭을 알리기만 한다 (Inventory를 모름).
/// </summary>
public class ItemSlotView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField]
    private Button _button;

    [SerializeField]
    private Image _background;

    [SerializeField]
    private Image _icon;

    [SerializeField]
    private TextMeshProUGUI _countText;

    [SerializeField]
    private GameObject _lockIcon;

    [SerializeField]
    private GameObject _selectRing;

    [Tooltip("오른쪽 위 희귀도 점 (흰 원 스프라이트에 ItemRarity.Color를 곱함)")]
    [SerializeField]
    private Image _rarityDot;

    [Header("배경 스프라이트")]
    [SerializeField]
    private Sprite _filledSprite;

    [SerializeField]
    private Sprite _emptySprite;

    [Tooltip("잠긴 칸 (확장 구매 전) 배경")]
    [SerializeField]
    private Sprite _lockedSprite;

    public event Action<ItemSlotView> OnClicked;

    public SlotState State { get; private set; }
    public ItemDefinition Item { get; private set; }

    private void Awake()
    {
        // 리스너는 여기서 딱 한 번만 등록
        _button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void ShowItem(ItemDefinition item, int count)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        State = SlotState.Item;
        Item = item;

        _background.sprite = _filledSprite;
        _background.color = Color.white;
        _icon.sprite = item.Icon;
        _icon.enabled = item.Icon != null;
        _countText.text = $"x{count}";
        _countText.enabled = true;
        _lockIcon.SetActive(false);

        bool hasRarity = item.Rarity != null;
        _rarityDot.enabled = hasRarity;
        if (hasRarity)
            _rarityDot.color = item.Rarity.Color;
    }

    public void ShowEmpty()
    {
        State = SlotState.Empty;
        Item = null;

        _background.sprite = _emptySprite;
        _background.color = Color.white;
        _icon.enabled = false;
        _countText.enabled = false;
        _lockIcon.SetActive(false);
        _rarityDot.enabled = false;
        SetSelected(false);
    }

    public void ShowLocked()
    {
        State = SlotState.Locked;
        Item = null;

        _background.sprite = _lockedSprite;
        _background.color = Color.white;
        _icon.enabled = false;
        _countText.enabled = false;
        _lockIcon.SetActive(true);
        _rarityDot.enabled = false;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        _selectRing.SetActive(selected);
    }
}
