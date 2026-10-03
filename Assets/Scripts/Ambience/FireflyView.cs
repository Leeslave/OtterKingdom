using UnityEngine;

/// <summary>
/// 밤에만 나오는 반딧불 하나: 화면 안을 천천히 떠다니며 깜빡인다 (조명을 받지 않는 재질이라 어둠 속에서 빛남).
/// 낮에는 보이지 않는다 (TimeOfDayLighting.Night).
/// </summary>
public class FireflyView : MonoBehaviour
{
    private const float Speed = 0.35f;
    private const float Margin = 0.6f;

    private SpriteRenderer _renderer;
    private Vector3 _target;
    private float _phase;

    public void Init(Sprite sprite, Material material, int sortingOrder, float size)
    {
        _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = sprite;
        if (material != null)
            _renderer.sharedMaterial = material;
        _renderer.sortingOrder = sortingOrder;
        transform.localScale = Vector3.one * (size / Mathf.Max(0.0001f, sprite.bounds.size.x));
        _phase = Random.value * 10f;
        transform.position = RandomPointInView();
        _target = RandomPointInView();
    }

    private void Update()
    {
        if (_renderer == null)
            return;

        float night = TimeOfDayLighting.Night;
        float blink = 0.55f + 0.45f * Mathf.Sin(Time.time * 2.6f + _phase);
        var color = _renderer.color;
        color.a = night * blink;
        _renderer.color = color;
        _renderer.enabled = night > 0.01f;
        if (!_renderer.enabled)
            return;

        var position = transform.position;
        var toTarget = _target - position;
        if (toTarget.sqrMagnitude < 0.05f)
        {
            _target = RandomPointInView();
            return;
        }
        var step = toTarget.normalized * (Speed * Time.deltaTime);
        step.x += Mathf.Sin(Time.time * 1.3f + _phase) * 0.2f * Time.deltaTime;
        transform.position = position + step;
    }

    private Vector3 RandomPointInView()
    {
        var cam = Camera.main;
        if (cam == null)
            return transform.position;
        float h = cam.orthographicSize - Margin;
        float w = cam.orthographicSize * cam.aspect - Margin;
        var c = cam.transform.position;
        return new Vector3(c.x + Random.Range(-w, w), c.y + Random.Range(-h * 0.7f, h * 0.6f), 0f);
    }
}
