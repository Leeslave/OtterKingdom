using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게시판 부탁으로 짓는 광장 건물 한 채의 묶음 (공사 현장 + 완성된 집·의자 + 밤 불빛 등)을 건설 모드에서 고른 자리로 옮긴다.
/// 공사 현장(ConstructionSiteView) 오브젝트에 붙고, 이 오브젝트의 씬 자리 = 건설 자리의 기본 칸(ConstructionPlotDefinition.DefaultCell).
/// 꾸미기 격자에 자리가 놓이면 DecorBoardView(PlacedPlotView)가 Apply를 불러 묶음 전체를 같은 만큼 옮긴다 (놓이지 않았으면 씬 자리 그대로).
/// - 묶음의 꾸미기 막음(DecorBlockArea)은 끔: 자리는 꾸미기 격자가 차지한 칸으로 막는다 (원래 자리를 미리 비워 둘 필요가 없음)
/// - 묶음의 발자국(Blocked 다각형)은 해달 길 막기에는 그대로 쓰되, 꾸미기 칸 막기에서는 뺀다 (자기 발자국 때문에 옆 칸으로 못 옮기는 일이 없게)
/// </summary>
// 걷기 영역(-100)이 처음 계산되기 전에 발자국·막음을 정리
[DefaultExecutionOrder(-110)]
public class ConstructionPlotAnchor : MonoBehaviour
{
    private static readonly Dictionary<string, ConstructionPlotAnchor> Anchors = new Dictionary<string, ConstructionPlotAnchor>();
    // 이 순서 이상은 말풍선·글자 (늘 위에 그림)
    private const int OverlayOrderFrom = 30000;

    [SerializeField] private ConstructionPlotDefinition _plot;

    [Tooltip("함께 옮길 것 (완성된 건물, 같은 자리의 다음 단계 건물·현장, 밤 불빛, 건물 앞 작업 자리 등). 이 오브젝트는 따로 넣지 않아도 옮겨짐")]
    [SerializeField] private List<Transform> _followers = new List<Transform>();

    [Tooltip("원래 자리를 꾸미기에서 비워 두던 막음 (묶음 밖에 있는 것). 자리를 고르게 되었으니 끔")]
    [SerializeField] private List<GameObject> _reservations = new List<GameObject>();

    private readonly List<(Transform target, Vector3 position)> _authored = new List<(Transform, Vector3)>();
    // PlazaProp이 아닌 그림 (밤 불빛 등): 씬에 정해 둔 정렬 순서를 옮긴 높이만큼 바꿔 줌
    private readonly List<(SpriteRenderer renderer, int order)> _fixedOrders = new List<(SpriteRenderer, int)>();
    private Vector2 _authoredPivot;
    private Vector2 _currentPivot;
    private ConstructionSiteView _site;

    public ConstructionPlotDefinition Plot => _plot;

    /// <summary>지금 오브젝트 발밑 (공사 현장 자리)</summary>
    public Vector2 Pivot => _currentPivot;

    /// <summary>이 건설의 묶음 (광장에 없으면 null)</summary>
    public static ConstructionPlotAnchor Find(string constructionId) =>
        constructionId != null && Anchors.TryGetValue(constructionId, out var anchor) && anchor != null ? anchor : null;

    private void Awake()
    {
        _site = GetComponent<ConstructionSiteView>();
        _authoredPivot = _currentPivot = transform.position;
        if (_plot != null && !string.IsNullOrEmpty(_plot.SaveId))
            Anchors[_plot.SaveId] = this;

        var roots = new List<Transform> { transform };
        foreach (var follower in _followers)
        {
            if (follower != null && !roots.Contains(follower))
                roots.Add(follower);
        }
        foreach (var root in roots)
        {
            _authored.Add((root, root.position));
            foreach (var area in root.GetComponentsInChildren<DecorBlockArea>(true))
                area.gameObject.SetActive(false);
            foreach (var polygon in root.GetComponentsInChildren<PlazaAreaPolygon>(true))
            {
                if (polygon.Kind == PlazaAreaKind.Blocked)
                    polygon.IgnoredByDecor = true;
            }
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                // 땅 높이로 정한 순서만 (말풍선처럼 늘 위에 그리는 것은 그대로)
                if (renderer.GetComponent<PlazaProp>() == null && renderer.GetComponentInParent<ConstructionSiteView>(true) == null
                    && renderer.sortingOrder > PlazaDepth.FlatPropOrder && renderer.sortingOrder < OverlayOrderFrom)
                    _fixedOrders.Add((renderer, renderer.sortingOrder));
            }
        }
        foreach (var reservation in _reservations)
        {
            if (reservation != null)
                reservation.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_plot != null && !string.IsNullOrEmpty(_plot.SaveId) && Anchors.TryGetValue(_plot.SaveId, out var anchor) && anchor == this)
            Anchors.Remove(_plot.SaveId);
    }

    /// <summary>이 칸에 놓이면 공사 해달이 설 곳 (공사 현장이 없으면 발밑)</summary>
    public Vector2 StandPointFor(Rect area)
    {
        var delta = _plot.PivotFor(area) - _currentPivot;
        return (_site != null ? _site.StandPoint : _currentPivot) + delta;
    }

    /// <summary>묶음을 이 칸(월드)에 맞춰 옮긴다</summary>
    /// <returns>자리가 바뀌었으면 true (걷기 영역을 다시 계산해야 함)</returns>
    public bool Apply(Rect area)
    {
        if (_plot == null)
            return false;
        var pivot = _plot.PivotFor(area);
        if ((pivot - _currentPivot).sqrMagnitude < 0.0001f)
            return false;

        _currentPivot = pivot;
        Vector3 delta = pivot - _authoredPivot;
        foreach (var (target, position) in _authored)
        {
            if (target != null)
                target.position = position + delta;
        }

        // 앞뒤 정렬을 새 높이로 (PlazaProp은 스스로, 그 밖의 그림은 씬 순서 + 옮긴 만큼)
        int shift = PlazaDepth.SortingOrderFor(pivot.y) - PlazaDepth.SortingOrderFor(_authoredPivot.y);
        foreach (var (renderer, order) in _fixedOrders)
        {
            if (renderer != null)
                renderer.sortingOrder = Mathf.Clamp(order + shift, PlazaDepth.FlatPropOrder + 1, OverlayOrderFrom - 1);
        }
        foreach (var (target, _) in _authored)
        {
            if (target == null)
                continue;
            foreach (var prop in target.GetComponentsInChildren<PlazaProp>(true))
                prop.SetDepthOffset(prop.DepthOffset);
            foreach (var site in target.GetComponentsInChildren<ConstructionSiteView>(true))
                site.RefreshDepth();
        }
        return true;
    }
}
