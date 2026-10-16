using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>10회 결과의 한 칸: 등급 색 테두리, 장난감 그림, NEW · 픽업 표시, 이름</summary>
public class GachaResultCellView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("등급 색을 곱하는 칸 바탕")]
    [SerializeField] private Image _frame;

    [SerializeField] private Image _icon;

    [SerializeField] private GameObject _newBadge;

    [SerializeField] private GameObject _featuredBadge;

    [SerializeField] private TextMeshProUGUI _nameText;

    public void Show(GachaPull pull, Color frameColor)
    {
        _frame.color = frameColor;
        _icon.sprite = pull.Item.Icon;
        _newBadge.SetActive(pull.IsNew);
        _featuredBadge.SetActive(pull.Featured);
        _nameText.text = pull.Item.DisplayName;
    }
}
