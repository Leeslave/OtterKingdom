using UnityEngine;

/// <summary>지금 카메라에 보이는 월드 범위 (화면을 덮는 연출용: 햇살, 가장자리 어둡게, 날리는 꽃잎)</summary>
public static class CameraView
{
    /// <summary>카메라 가운데와 보이는 가로·세로 (월드 단위). 정사영 카메라가 없으면 false</summary>
    public static bool TryGet(out Vector2 center, out Vector2 size)
    {
        var camera = Camera.main;
        if (camera == null || !camera.orthographic)
        {
            center = default;
            size = default;
            return false;
        }
        float height = camera.orthographicSize * 2f;
        center = camera.transform.position;
        size = new Vector2(height * camera.aspect, height);
        return true;
    }

    /// <summary>그림이 화면을 꼭 덮도록 자리·크기를 맞춤 (margin 1.1이면 10% 넉넉히)</summary>
    public static void Cover(SpriteRenderer renderer, float margin)
    {
        if (!TryGet(out var center, out var size) || renderer.sprite == null)
            return;
        var spriteSize = renderer.sprite.bounds.size;
        var t = renderer.transform;
        t.position = new Vector3(center.x, center.y, 0f);
        t.localScale = new Vector3(size.x * margin / Mathf.Max(0.001f, spriteSize.x), size.y * margin / Mathf.Max(0.001f, spriteSize.y), 1f);
    }
}
