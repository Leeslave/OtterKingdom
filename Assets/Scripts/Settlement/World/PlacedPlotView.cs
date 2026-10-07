using UnityEngine;

/// <summary>
/// 꾸미기 격자에 놓인 건설 자리 하나 (DecorBoardView가 만들고 치움). 스스로 그리지 않고,
/// 광장 씬의 그 건물 묶음(ConstructionPlotAnchor: 공사 현장 · 완성된 집 · 밤 불빛)을 이 칸으로 옮긴다.
/// 해달 길은 묶음의 발자국이 막으므로 걷기 장애물로 알리지 않는다. 해달이 가지고 놀지 않는다 (HasRoom = false)
/// </summary>
public class PlacedPlotView : PlacedDecorView
{
    /// <summary>마지막 Place에서 묶음이 옮겨졌는지 (걷기 영역을 다시 계산해야 함)</summary>
    public bool MovedAnchor { get; private set; }

    internal override void Init(PlacedDecor placed, Rect worldRect, float cellSize, float fill, int sortingOrder)
    {
        Placed = placed;
        Place(worldRect, fill);
    }

    internal override void Place(Rect worldRect, float fill)
    {
        WorldRect = worldRect;
        transform.position = new Vector3(worldRect.center.x, worldRect.yMin, 0f);
        var anchor = ConstructionPlotAnchor.Find(Placed.Decor.SaveId);
        MovedAnchor = anchor != null && anchor.Apply(worldRect);
    }

    internal override void SetSortingOrder(int order) { }
}
