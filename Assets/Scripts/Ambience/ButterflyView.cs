using UnityEngine;

/// <summary>
/// 화면 안을 이리저리 날아다니는 나비 한 마리. 날개를 펴고 접으며(그림 두 장), 목표 지점을 향해 흔들흔들 날다가
/// 도착하면 잠깐 머문 뒤 새 목표를 고른다. 목표는 지금 카메라에 보이는 범위 안에서 고른다.
/// </summary>
public class ButterflyView : MonoBehaviour
{
    private const float FlapSeconds = 0.12f;
    private const float Speed = 1.1f;
    private const float Margin = 0.8f;

    private SpriteRenderer _renderer;
    private Sprite[] _frames;
    private Vector3 _target;
    private float _flapTimer;
    private int _frame;
    private float _rest;
    private float _wobblePhase;

    public void Init(Sprite[] frames, Color tint, int sortingOrder, float size)
    {
        _frames = frames;
        _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = frames[0];
        _renderer.color = tint;
        _renderer.sortingOrder = sortingOrder;
        transform.localScale = Vector3.one * (size / Mathf.Max(0.0001f, frames[0].bounds.size.x));
        _wobblePhase = Random.value * 10f;
        transform.position = RandomPointInView();
        PickTarget();
    }

    private void Update()
    {
        if (_renderer == null)
            return;

        // 밤에는 쉬러 감 (반딧불이 대신 나옴)
        var color = _renderer.color;
        color.a = 1f - TimeOfDayLighting.Night;
        _renderer.color = color;
        _renderer.enabled = color.a > 0.01f;

        _flapTimer -= Time.deltaTime;
        if (_flapTimer <= 0f)
        {
            // 쉬는 동안은 천천히 날갯짓
            _flapTimer = _rest > 0f ? FlapSeconds * 4f : FlapSeconds;
            _frame = 1 - _frame;
            _renderer.sprite = _frames[_frame];
        }

        if (_rest > 0f)
        {
            _rest -= Time.deltaTime;
            if (_rest <= 0f)
                PickTarget();
            return;
        }

        var position = transform.position;
        var toTarget = _target - position;
        if (toTarget.sqrMagnitude < 0.04f)
        {
            _rest = Random.Range(0.6f, 2.5f);
            return;
        }

        var step = toTarget.normalized * (Speed * Time.deltaTime);
        // 위아래로 팔랑팔랑
        step.y += Mathf.Sin(Time.time * 7f + _wobblePhase) * 0.9f * Time.deltaTime;
        transform.position = position + step;
        _renderer.flipX = toTarget.x < 0f;
    }

    private void PickTarget()
    {
        _target = RandomPointInView();
    }

    private Vector3 RandomPointInView()
    {
        var cam = Camera.main;
        if (cam == null)
            return transform.position;
        float h = cam.orthographicSize - Margin;
        float w = cam.orthographicSize * cam.aspect - Margin;
        var c = cam.transform.position;
        return new Vector3(c.x + Random.Range(-w, w), c.y + Random.Range(-h * 0.6f, h * 0.7f), 0f);
    }
}
