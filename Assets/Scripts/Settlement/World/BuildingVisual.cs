using UnityEngine;

/// <summary>
/// 건물 그림을 차지한 칸에 맞춰 놓는 규칙 (놓인 건물, 꾸미기 미리보기가 함께 씀).
/// 완성 그림의 가로를 칸 가로(× WidthFill)에 맞추고 아래를 칸 아랫변에 붙인다. 공사 단계 그림도 같은 배율·같은 자리에
/// 피벗(발밑)을 맞춰 그리므로 단계가 바뀌어도 집이 제자리에 있다 (단계 그림은 완성 그림과 같은 피벗으로 만들어 둠)
/// </summary>
public static class BuildingVisual
{
    /// <param name="body">칸 아래 가운데에 둘 부모</param>
    /// <param name="sprite">body의 자식 SpriteRenderer</param>
    /// <param name="shown">지금 보여 줄 그림 (완성 그림 또는 공사 단계 그림)</param>
    /// <param name="area">차지한 칸 (월드). body의 부모 원점은 칸 아래 가운데에 있다고 본다</param>
    public static void Fit(Transform body, SpriteRenderer sprite, BuildingDefinition building, Sprite shown, Rect area)
    {
        sprite.sprite = shown;
        body.localPosition = Vector3.zero;
        body.localRotation = Quaternion.identity;

        var reference = building.WorldSprite != null ? building.WorldSprite : shown;
        if (reference == null)
            return;

        var bounds = reference.bounds;
        float scale = area.width * building.WidthFill / Mathf.Max(0.01f, bounds.size.x);
        sprite.transform.localScale = Vector3.one * scale;
        // 완성 그림 기준으로 가로 가운데·아래를 맞춘 자리 (단계 그림도 같은 자리 = 같은 피벗)
        sprite.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, 0f);
    }
}
