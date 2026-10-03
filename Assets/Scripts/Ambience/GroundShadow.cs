using UnityEngine;

/// <summary>
/// 발밑의 흐린 타원 그림자. 해달·소품이 바닥에 붙어 보이게 한다.
/// 대상 그림(가장 큰 SpriteRenderer)의 폭에 맞춰 크기를 정하고, 그림이 꺼지거나 바뀌면 따라간다
/// (바위가 깨져 자갈이 되면 그림자도 작아짐). AmbienceDirector가 붙인다.
/// </summary>
public class GroundShadow : MonoBehaviour
{
    // 말풍선·안내처럼 맨 위에 그리는 것은 몸통이 아님
    private const int OverlayOrderFrom = 30000;

    private SpriteRenderer _source;
    private SpriteRenderer _shadow;
    private float _alpha;
    private Sprite _sizedFor;
    private float _widthFactor;
    private float _yOffset;

    /// <summary>target 발밑에 그림자를 붙인다 (이미 있으면 그대로)</summary>
    /// <param name="yOffset">그림 아래 끝에서 그림자 가운데까지 (집처럼 앞 모서리가 아래 끝이면 위로 조금)</param>
    public static GroundShadow Attach(GameObject target, Sprite sprite, int sortingOrder, Color color, float widthFactor, float yOffset)
    {
        var existing = target.GetComponent<GroundShadow>();
        if (existing != null)
            return existing;

        var source = LargestRenderer(target);
        if (source == null)
            return null;

        var view = target.AddComponent<GroundShadow>();
        var go = new GameObject("GroundShadow");
        go.transform.SetParent(target.transform, false);
        view._shadow = go.AddComponent<SpriteRenderer>();
        view._shadow.sprite = sprite;
        view._shadow.color = color;
        view._alpha = color.a;
        view._shadow.sortingOrder = sortingOrder;
        view._source = source;
        view._widthFactor = widthFactor;
        view._yOffset = yOffset;
        view.Fit();
        return view;
    }

    // 말풍선·아이콘처럼 작은 것 말고 몸통 그림
    private static SpriteRenderer LargestRenderer(GameObject target)
    {
        SpriteRenderer best = null;
        float bestArea = 0f;
        foreach (var renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null || renderer.sortingOrder >= OverlayOrderFrom)
                continue;
            var size = renderer.sprite.bounds.size;
            float area = size.x * size.y * Mathf.Abs(renderer.transform.lossyScale.x * renderer.transform.lossyScale.y);
            if (area > bestArea)
            {
                best = renderer;
                bestArea = area;
            }
        }
        return best;
    }

    private void LateUpdate()
    {
        bool visible = _source.enabled && _source.gameObject.activeInHierarchy && _source.sprite != null;
        if (_shadow.enabled != visible)
            _shadow.enabled = visible;
        if (!visible)
            return;
        if (_source.sprite != _sizedFor)
            Fit();
        // 대상이 흐려지면(광부가 굴로 들어감) 그림자도
        var color = _shadow.color;
        color.a = _alpha * _source.color.a;
        _shadow.color = color;
    }

    // 그림 폭의 일정 비율, 높이는 폭의 0.3. 부모 크기·뒤집기와 상관없이 같은 크기로
    private void Fit()
    {
        _sizedFor = _source.sprite;
        if (_sizedFor == null)
            return;

        var spriteBounds = _sizedFor.bounds;
        float sourceScale = Mathf.Abs(_source.transform.lossyScale.x);
        float width = spriteBounds.size.x * sourceScale * _widthFactor;
        float centerX = _source.transform.position.x + spriteBounds.center.x * _source.transform.lossyScale.x;
        // 그림 아래 끝(발·바닥 닿는 곳)에 그림자 가운데를 둬서 절반이 그림 밑으로 보이게
        float bottomY = _source.transform.position.y + spriteBounds.min.y * Mathf.Abs(_source.transform.lossyScale.y);

        var parentScale = transform.lossyScale;
        var shadowSize = _shadow.sprite.bounds.size;
        var t = _shadow.transform;
        t.position = new Vector3(centerX, bottomY + _yOffset, transform.position.z);
        t.localScale = new Vector3(
            width / shadowSize.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            width * 0.3f / shadowSize.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            1f);
    }
}
