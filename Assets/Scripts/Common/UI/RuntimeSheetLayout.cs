using UnityEngine;

/// <summary>창 크기·회전·안전 영역이 바뀌면 스크롤 시트의 폭과 위치를 다시 맞춘다.</summary>
public class RuntimeSheetLayout : MonoBehaviour
{
    private int _width, _height;
    private Rect _safe;
    private float _parentWidth;
    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        var frame = (RectTransform)transform;
        var parent = (RectTransform)frame.parent;
        var safe = Screen.safeArea;
        if (_width == Screen.width && _height == Screen.height && _safe == safe && Mathf.Approximately(_parentWidth, parent.rect.width)) return;
        _width = Screen.width; _height = Screen.height; _safe = safe; _parentWidth = parent.rect.width;
        float width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        float center = safe.center.x / width;
        frame.anchorMin = new Vector2(center, safe.yMin / height + .04f);
        frame.anchorMax = new Vector2(center, safe.yMax / height - .04f);
        float fitWidth = Mathf.Min(1020f, parent.rect.width * Mathf.Max(.1f, safe.width / width - .08f));
        frame.offsetMin = new Vector2(-fitWidth * .5f, 0f);
        frame.offsetMax = new Vector2(fitWidth * .5f, 0f);
    }
}
