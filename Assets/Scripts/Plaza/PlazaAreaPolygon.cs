using System.Collections.Generic;
using UnityEngine;

public enum PlazaAreaKind
{
    Walkable,
    Blocked
}

// One hand-edited polygon of the plaza's movement data. Points are in this
// transform's local space so a whole area can be nudged by moving the
// GameObject. PlazaWalkableArea combines every polygon into its grid:
// a feet position is walkable when it is inside some Walkable polygon and
// outside every Blocked polygon.
//
// The background is one flat painting, so these polygons are the only thing
// that keeps otters off the houses, trees, fences and sea — nothing is read
// from the image at runtime. Edit vertices in the Scene view (see
// PlazaAreaPolygonEditor) and select WalkableArea to preview the grid.
public class PlazaAreaPolygon : MonoBehaviour
{
    [SerializeField] private PlazaAreaKind kind = PlazaAreaKind.Walkable;
    [SerializeField] private List<Vector2> points = new List<Vector2>();

    public PlazaAreaKind Kind => kind;
    public int PointCount => points.Count;

    public void GetWorldPoints(List<Vector2> result)
    {
        result.Clear();
        foreach (var p in points)
        {
            result.Add(transform.TransformPoint(p));
        }
    }

    // Even-odd rule, so self-intersecting shapes behave predictably instead
    // of silently flooding the grid.
    public static bool Contains(List<Vector2> polygon, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            if ((a.y > p.y) != (b.y > p.y) &&
                p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private void OnDrawGizmos()
    {
        if (points.Count < 2) return;

        Gizmos.color = kind == PlazaAreaKind.Walkable
            ? new Color(0.2f, 1f, 0.3f, 0.9f)
            : new Color(1f, 0.25f, 0.2f, 0.9f);

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 a = transform.TransformPoint(points[i]);
            Vector3 b = transform.TransformPoint(points[(i + 1) % points.Count]);
            Gizmos.DrawLine(a, b);
        }
    }
}
