using System.Collections;
using UnityEngine;

/// <summary>
/// 가끔 물고기가 물 위로 톡 뛰어올랐다 첨벙 들어간다 (낚시터). 뛰는 자리·끝 자리에 물결 고리와 물방울.
/// 밤에는 드물게. SettlementSetup.Life가 붙인다.
/// </summary>
public class FishJumpView : MonoBehaviour
{
    private const int DropletCount = 5;

    [Header("곳")]
    [SerializeField] private WorldArea _water = new WorldArea();

    [Header("그림")]
    [SerializeField] private Sprite _fishSprite;
    [SerializeField] private Sprite _rippleSprite;
    [SerializeField] private Sprite _dropletSprite;

    [Header("뛰기")]
    [Tooltip("다음 점프까지 (초, 최소~최대)")]
    [SerializeField] private Vector2 _interval = new Vector2(6f, 13f);

    [Tooltip("물고기 크기 (월드 단위, 가로)")]
    [SerializeField] private float _fishSize = 0.55f;

    [Tooltip("뛰는 높이 · 가로로 나아가는 거리 (월드 단위)")]
    [SerializeField] private Vector2 _arc = new Vector2(0.9f, 1.1f);

    [Tooltip("물 위에 떠 있는 시간 (초)")]
    [SerializeField] private float _airSeconds = 0.75f;

    [SerializeField] private int _sortingOrder = 1;

    private float _timer;

    private void OnEnable() => _timer = Random.Range(2f, _interval.y);

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f)
            return;
        // 밤에는 두 배 드물게
        _timer = Random.Range(_interval.x, _interval.y) * (1f + TimeOfDay.Night(TimeOfDay.CurrentHour));
        if (_water.TryPick(out Vector2 start))
            StartCoroutine(Jump(start));
    }

    private IEnumerator Jump(Vector2 start)
    {
        float direction = Random.value < 0.5f ? -1f : 1f;
        var end = start + new Vector2(direction * _arc.y, 0f);
        StartCoroutine(Splash(start, 0.8f));

        var fish = CreateSprite("Fish", _fishSprite, _sortingOrder + 1);
        float scale = _fishSize / Mathf.Max(0.001f, _fishSprite.bounds.size.x);
        fish.flipX = direction < 0f;
        for (float t = 0f; t < _airSeconds; t += Time.deltaTime)
        {
            float k = t / _airSeconds;
            var position = Vector2.Lerp(start, end, k) + Vector2.up * (4f * _arc.x * k * (1f - k));
            fish.transform.position = position;
            // 올라갈 땐 머리를 들고 내려갈 땐 숙임
            float slope = 4f * _arc.x * (1f - 2f * k) / _arc.y;
            fish.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan(slope) * Mathf.Rad2Deg * direction * 0.8f);
            // 물에서 나올 때·들어갈 때 살짝 작게
            fish.transform.localScale = Vector3.one * (scale * Mathf.Lerp(0.6f, 1f, Mathf.Sin(Mathf.PI * k)));
            yield return null;
        }
        Destroy(fish.gameObject);
        StartCoroutine(Splash(end, 1f));
    }

    // 물결 고리 하나 + 물방울 몇 개
    private IEnumerator Splash(Vector2 center, float strength)
    {
        var ring = CreateSprite("Splash", _rippleSprite, _sortingOrder);
        ring.transform.position = center;
        float ringWidth = Mathf.Max(0.001f, _rippleSprite.bounds.size.x);
        var droplets = new SpriteRenderer[DropletCount];
        var velocities = new Vector2[DropletCount];
        float dropletScale = 0.12f / Mathf.Max(0.001f, _dropletSprite.bounds.size.x);
        for (int i = 0; i < DropletCount; i++)
        {
            droplets[i] = CreateSprite("Droplet", _dropletSprite, _sortingOrder + 2);
            droplets[i].transform.position = center;
            droplets[i].transform.localScale = Vector3.one * dropletScale * Random.Range(0.7f, 1.2f);
            velocities[i] = new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(1.8f, 3f)) * strength;
        }

        const float seconds = 0.9f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            float width = Mathf.Lerp(0.3f, 1.1f, 1f - (1f - k) * (1f - k)) * strength;
            ring.transform.localScale = Vector3.one * (width / ringWidth);
            ring.color = new Color(1f, 1f, 1f, 0.75f * (1f - k));
            for (int i = 0; i < DropletCount; i++)
            {
                velocities[i].y -= 9f * Time.deltaTime;
                var p = (Vector2)droplets[i].transform.position + velocities[i] * Time.deltaTime;
                droplets[i].transform.position = p;
                droplets[i].color = new Color(1f, 1f, 1f, p.y < center.y ? 0f : 0.9f * (1f - k));
            }
            yield return null;
        }
        Destroy(ring.gameObject);
        foreach (var droplet in droplets)
            Destroy(droplet.gameObject);
    }

    private SpriteRenderer CreateSprite(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }
}
