using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보관함 탭 버튼 하나 (전체 / 장난감 …). 선택되면 금색.
/// </summary>
public class DecorStorageTabView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _background;
    [SerializeField] private TextMeshProUGUI _label;

    [Header("배경 스프라이트")]
    [SerializeField] private Sprite _normalSprite;
    [SerializeField] private Sprite _selectedSprite;

    /// <summary>이 탭이 보여줄 분류. null이면 "전체" (건물 탭이 아니면)</summary>
    public ItemCategory Category { get; private set; }

    /// <summary>건물 탭 (자리를 골라 짓는 건물 목록)</summary>
    public bool IsBuildings { get; private set; }

    public event Action<DecorStorageTabView> OnClicked;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    public void Bind(ItemCategory category, string label)
    {
        Category = category;
        IsBuildings = false;
        _label.text = label;
    }

    public void BindBuildings(string label)
    {
        Category = null;
        IsBuildings = true;
        _label.text = label;
    }

    public void SetSelected(bool selected)
    {
        _background.sprite = selected ? _selectedSprite : _normalSprite;
    }
}
