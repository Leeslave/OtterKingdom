using UnityEngine;

/// <summary>
/// 튜토리얼이 강조할 대상의 화면 영역(픽셀, 왼쪽 아래 원점) 구하기.
/// 대상이 없거나 꺼져 있으면 null → 그 장은 건너뛰거나 강조 없이 보여 준다.
/// </summary>
public static class TutorialTargets
{
    private static readonly Vector3[] Corners = new Vector3[4];

    /// <summary>UI 요소 (어느 캔버스 모드든)</summary>
    public static Rect? Ui(RectTransform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return null;

        var canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        var root = canvas.rootCanvas;
        var cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;

        target.GetWorldCorners(Corners);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, Corners[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    /// <summary>월드 오브젝트: 자신과 자식의 켜진 SpriteRenderer 전부를 감싸는 영역</summary>
    public static Rect? World(Component target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return null;

        var cam = Camera.main;
        if (cam == null) return null;

        Bounds? bounds = null;
        foreach (var renderer in target.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!renderer.enabled || renderer.sprite == null || renderer.color.a <= 0.01f) continue;

            if (bounds == null)
            {
                bounds = renderer.bounds;
            }
            else
            {
                var b = bounds.Value;
                b.Encapsulate(renderer.bounds);
                bounds = b;
            }
        }
        if (bounds == null) return null;

        Vector2 min = cam.WorldToScreenPoint(bounds.Value.min);
        Vector2 max = cam.WorldToScreenPoint(bounds.Value.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
