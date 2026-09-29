using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 탭 버튼 하나 (채소 / 어류 / 해달). 선택되면 금색.
/// </summary>
public class CollectionTabView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private TextMeshProUGUI _label;

    [Header("배경 스프라이트")]
    [SerializeField] private Sprite _normalSprite;
    [SerializeField] private Sprite _selectedSprite;

    public CollectionTab Tab { get; private set; }
    public event Action<CollectionTabView> OnClicked;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void Bind(CollectionTab tab)
    {
        if (tab == null)
            throw new ArgumentNullException(nameof(tab));

        Tab = tab;
        _label.text = tab.DisplayName;
    }

    public void SetSelected(bool selected)
    {
        _background.sprite = selected ? _selectedSprite : _normalSprite;
    }
}
