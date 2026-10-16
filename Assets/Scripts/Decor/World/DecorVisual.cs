using UnityEngine;

/// <summary>
/// 물건 그림을 차지한 칸에 맞춰 놓는 공통 규칙 (놓인 물건, 미리보기가 함께 씀).
/// 부모(body)는 칸 가운데에서 방향만큼 돌고, 그 아래 그림(sprite)은 그림의 가운데가 body에 오도록 옮긴다.
/// 그래서 방향을 바꾸거나 흔들어도 칸 가운데를 중심으로 돈다.
/// </summary>
public static class DecorVisual
{
    /// <param name="body">칸 가운데에 둘 부모 (회전·튀기 반응을 받음)</param>
    /// <param name="sprite">그림을 그리는 SpriteRenderer (body의 자식)</param>
    /// <param name="area">차지한 칸 (월드). body의 부모 원점은 칸 아래 가운데에 있다고 본다</param>
    /// <param name="fill">칸을 채우는 비율</param>
    /// <returns>body의 기본 회전 (반응이 끝나면 여기로 돌아옴)</returns>
    public static Quaternion Fit(Transform body, SpriteRenderer sprite, DecorDefinition decor, DecorRotation rotation, Rect area, float fill)
    {
        sprite.sprite = decor.SpriteFor(rotation, out float angle);
        var baseRotation = Quaternion.Euler(0f, 0f, angle);

        body.localPosition = new Vector3(0f, area.height * 0.5f, 0f);
        body.localRotation = baseRotation;
        if (sprite.sprite == null)
            return baseRotation;

        // 옆으로 누운 방향(90·270도 돌린 그림)이면 가로·세로를 바꿔 칸에 맞춤
        Vector2 size = sprite.sprite.bounds.size;
        bool sideways = Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)) > 0.5f;
        if (sideways)
            size = new Vector2(size.y, size.x);

        float scale = Mathf.Min(area.width * fill / size.x, area.height * fill / size.y);
        sprite.transform.localScale = Vector3.one * scale;
        sprite.transform.localPosition = -sprite.sprite.bounds.center * scale;
        return baseRotation;
    }
}
