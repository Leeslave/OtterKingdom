using UnityEngine;

/// <summary>
/// 광장 위를 천천히 지나가는 구름 그림자. 아주 옅게 모든 것을 살짝 어둡게 하며 오른쪽으로 흘러가고,
/// 카메라 화면을 벗어나면 왼쪽 밖에서 새 높이로 다시 들어온다.
/// </summary>
public class CloudShadowView : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private float _speed;

    public void Init(Sprite sprite, Color color, int sortingOrder, float width, float speed, bool startInView)
    {
        _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = sprite;
        _renderer.color = color;
        _renderer.sortingOrder = sortingOrder;
        transform.localScale = Vector3.one * (width / Mathf.Max(0.0001f, sprite.bounds.size.x));
        _speed = speed;
        Respawn(startInView);
    }

    private void Update()
    {
        if (_renderer == null)
            return;
        var cam = Camera.main;
        if (cam == null)
            return;

        transform.position += Vector3.right * (_speed * Time.deltaTime);
        float halfWidth = _renderer.bounds.extents.x;
        float viewRight = cam.transform.position.x + cam.orthographicSize * cam.aspect;
        if (transform.position.x - halfWidth > viewRight)
            Respawn(false);
    }

    // 카메라 왼쪽 밖(또는 처음엔 화면 안 아무 데나)에서 시작
    private void Respawn(bool inView)
    {
        var cam = Camera.main;
        if (cam == null)
            return;
        float halfW = cam.orthographicSize * cam.aspect;
        float halfH = cam.orthographicSize;
        var c = cam.transform.position;
        float x = inView ? c.x + Random.Range(-halfW, halfW) : c.x - halfW - _renderer.bounds.extents.x;
        float y = c.y + Random.Range(-halfH * 0.7f, halfH * 0.7f);
        transform.position = new Vector3(x, y, 0f);
    }
}
