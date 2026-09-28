using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GridLayoutGroup의 열 수를 가용 폭에 맞춰 조절한다.
/// 칸 크기는 유지하고 넓은 화면(태블릿/폴드)에서는 열을 늘린다.
/// 폭이 좁아 최소 열 수가 안 들어가면 칸을 줄여서라도 최소 열 수를 지킨다.
/// </summary>
[RequireComponent(typeof(GridLayoutGroup))]
public class AdaptiveGridColumns : MonoBehaviour
{
    [Header("칸")]
    [Tooltip("원하는 칸 크기 (시안 190)")]
    [SerializeField] private float _cellSize = 190f;

    [Tooltip("최소 열 수 (폭이 좁으면 칸을 줄여서라도 유지)")]
    [SerializeField] private int _minColumns = 4;

    [Header("여백")]
    [Tooltip("좌우 최소 여백 (남는 폭은 Child Alignment에 따라 가운데 정렬)")]
    [SerializeField] private float _minSidePadding = 40f;

    private GridLayoutGroup _grid;
    private RectTransform _rectTransform;
    private float _lastWidth = -1f;

    private void Awake()
    {
        _grid = GetComponent<GridLayoutGroup>();
        _rectTransform = (RectTransform)transform;
    }

    private void OnEnable()
    {
        Apply();
    }

    // 회전·해상도·Safe Area 변화로 폭이 바뀌면 호출됨 (Awake 전에 불릴 수도 있어서 null 확인)
    private void OnRectTransformDimensionsChange()
    {
        if (_grid != null && isActiveAndEnabled)
            Apply();
    }

    private void Apply()
    {
        float width = _rectTransform.rect.width;

        // 폭이 그대로면 무시: ContentSizeFitter가 높이만 바꿀 때도 이 콜백이 오는데,
        // 그때 그리드 값을 건드리면 레이아웃 재계산이 반복된다
        if (width <= 0f || Mathf.Approximately(width, _lastWidth))
            return;
        _lastWidth = width;

        float spacing = _grid.spacing.x;
        float usable = width - _minSidePadding * 2f;

        int columns = Mathf.FloorToInt((usable + spacing) / (_cellSize + spacing));
        float cell = _cellSize;

        if (columns < _minColumns)
        {
            columns = _minColumns;
            cell = (usable - spacing * (columns - 1)) / columns;
        }

        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columns;
        _grid.cellSize = new Vector2(cell, cell);
    }
}
