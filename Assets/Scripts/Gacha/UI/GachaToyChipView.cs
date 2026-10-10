using UnityEngine;
using UnityEngine.UI;

/// <summary>배너 아래 눈여겨볼 장난감 한 칸 (등급 색 테두리 + 그림 + "UP")</summary>
public class GachaToyChipView : MonoBehaviour
{
    [Header("구성 요소")]
    [Tooltip("등급 색을 곱하는 칸 바탕")]
    [SerializeField] private Image _frame;

    [SerializeField] private Image _icon;

    [Tooltip("픽업 장난감 표시")]
    [SerializeField] private GameObject _upTag;

    [Header("등급 색 (흔함 · 레어 · 에픽)")]
    [SerializeField] private Color[] _frameColors =
    {
        new Color(1f, 0.96f, 0.88f, 1f),
        new Color(0.68f, 0.85f, 1f, 1f),
        new Color(1f, 0.86f, 0.45f, 1f),
    };

    public void Show(ItemDefinition item, int tier, bool featured)
    {
        gameObject.SetActive(item != null);
        if (item == null)
            return;
        _frame.color = _frameColors[Mathf.Clamp(tier, 0, _frameColors.Length - 1)];
        _icon.sprite = item.Icon;
        _upTag.SetActive(featured);
    }
}
