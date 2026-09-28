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

    [Header("배경 스프라이트")]
    [SerializeField]
    private Sprite _filledSprite;

    [SerializeField]
    private Sprite _emptySprite;

    [Header("잠긴 칸 색 (전용 스프라이트가 생기기 전 임시)")]
    [SerializeField]
    private Color _lockedTint = new Color(0.75f, 0.7f, 0.65f);

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
        SetSelected(false);
    }

    public void ShowLocked()
    {
        State = SlotState.Locked;
        Item = null;

        _background.sprite = _emptySprite;
        _background.color = _lockedTint;
        _icon.enabled = false;
        _countText.enabled = false;
        _lockIcon.SetActive(true);
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        _selectRing.SetActive(selected);
    }
}
