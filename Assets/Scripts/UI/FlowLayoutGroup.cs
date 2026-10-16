using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자식을 왼쪽부터 가로로 놓고, 넘치면 다음 줄로 (칩 · 태그 목록). 자식 크기는 각자의 선호 크기(텍스트 길이)를 쓴다.
/// 높이는 줄 수만큼 선호 높이로 알려서, 위의 VerticalLayoutGroup · ContentSizeFitter가 따라 늘어난다
/// </summary>
[AddComponentMenu("Layout/Flow Layout Group")]
public class FlowLayoutGroup : LayoutGroup
{
    [Header("간격")]
    [Tooltip("같은 줄 칩 사이")]
    [SerializeField] private float _spacingX = 10f;

    [Tooltip("줄 사이")]
    [SerializeField] private float _spacingY = 10f;

    public void SetSpacing(float x, float y)
    {
        _spacingX = x;
        _spacingY = y;
        SetDirty();
    }

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        SetLayoutInputForAxis(padding.horizontal, padding.horizontal, -1, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        float height = Arrange(false);
        SetLayoutInputForAxis(height, height, -1, 1);
    }

    public override void SetLayoutHorizontal() => Arrange(true);

    public override void SetLayoutVertical() => Arrange(true);

    // 줄을 나눠 놓고(apply) 전체 높이를 돌려줌
    private float Arrange(bool apply)
    {
        float width = rectTransform.rect.width - padding.horizontal;
        float x = 0f, y = 0f, rowHeight = 0f;
        foreach (var child in rectChildren)
        {
            float w = Mathf.Min(LayoutUtility.GetPreferredWidth(child), width);
            float h = LayoutUtility.GetPreferredHeight(child);
            if (x > 0f && x + w > width)
            {
                x = 0f;
                y += rowHeight + _spacingY;
                rowHeight = 0f;
            }
            if (apply)
            {
                SetChildAlongAxis(child, 0, padding.left + x, w);
                SetChildAlongAxis(child, 1, padding.top + y, h);
            }
            x += w + _spacingX;
            rowHeight = Mathf.Max(rowHeight, h);
        }
        return padding.vertical + y + rowHeight;
    }
}
