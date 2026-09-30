using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보관함 칸 하나: 물건 그림, 남은 개수(x1), 이름. 남은 게 없으면 흐리게 하고 이름 대신 "배치됨".
/// </summary>
public class DecorStorageSlotView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _countText;
    [SerializeField] private TextMeshProUGUI _nameText;
    [Tooltip("지금 들고 있는 물건이면 금색 테두리")]
    [SerializeField] private GameObject _selectRing;

    [Header("배경")]
    [SerializeField] private Sprite _filledSprite;
    [SerializeField] private Sprite _emptySprite;

    [Header("글자색")]
    [SerializeField] private Color _nameColor = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    [SerializeField] private Color _mutedColor = new Color32(0x9C, 0x7C, 0x66, 0xFF);
    [Range(0f, 1f)]
    [SerializeField] private float _emptyIconAlpha = 0.4f;

    public DecorDefinition Decor { get; private set; }

    public event Action<DecorStorageSlotView> OnClicked;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void Bind(DecorDefinition decor, int storageCount, bool selected)
    {
        if (decor == null)
            throw new ArgumentNullException(nameof(decor));

        Decor = decor;
        bool has = storageCount > 0;

        _background.sprite = has ? _filledSprite : _emptySprite;
        _icon.sprite = decor.Item.Icon;
        _icon.enabled = _icon.sprite != null;
        _icon.color = new Color(1f, 1f, 1f, has ? 1f : _emptyIconAlpha);
        _countText.text = $"x{storageCount}";
        _nameText.text = has ? decor.DisplayName : "배치됨";
        _nameText.color = has ? _nameColor : _mutedColor;
        _selectRing.SetActive(selected);
    }
}
