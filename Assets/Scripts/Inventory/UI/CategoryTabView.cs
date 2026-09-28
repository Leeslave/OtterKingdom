using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카테고리 탭(또는 소분류 칩) 하나. 받은 카테고리를 그리고 클릭을 알리기만 한다.
/// 탭과 칩은 같은 스크립트를 쓰고 프리팹(스프라이트, 크기)만 다르다.
/// </summary>
public class CategoryTabView : MonoBehaviour
{
    [Header("구성 요소")]
    [SerializeField]
    private Button _button;

    [SerializeField]
    private Image _background;

    [SerializeField]
    private TextMeshProUGUI _label;

    [Header("배경 스프라이트")]
    [SerializeField]
    private Sprite _normalSprite;

    [SerializeField]
    private Sprite _selectedSprite;

    [Header("글자색")]
    [SerializeField]
    private Color _normalTextColor = new Color32(0xA0, 0x86, 0x72, 0xFF); // 연한 갈색

    [SerializeField]
    private Color _selectedTextColor = new Color32(0x4B, 0x2E, 0x22, 0xFF); // 진한 코코아

    public ItemCategory Category { get; private set; }
    public event Action<CategoryTabView> OnClicked;

    private void Awake()
    {
        _button.onClick.AddListener(() => OnClicked?.Invoke(this));
    }

    /// <param name="label">비우면 카테고리 이름. 소분류 칩의 "전체"처럼 탭 자신을 다른 이름으로 보일 때 사용</param>
    public void Bind(ItemCategory category, string label = null)
    {
        if (category == null)
            throw new ArgumentNullException(nameof(category));

        Category = category;
        _label.text = label ?? category.DisplayName;
    }

    public void SetSelected(bool selected)
    {
        _background.sprite = selected ? _selectedSprite : _normalSprite;
        _label.color = selected ? _selectedTextColor : _normalTextColor;
    }
}
