using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 등불 둘레의 따뜻한 빛무리가 촛불처럼 살짝 일렁인다 (광산 입구 등불, 낚시터 등불·등대, 밭 등불).
/// 낮에는 은은하게, 밤에는 또렷하게. 조명을 받지 않는 재질. SettlementSetup.Life가 장소마다 붙인다.
/// </summary>
public class GlowFlicker : MonoBehaviour
{
    [Header("자리")]
    [Tooltip("등불 자리 (월드)")]
    [SerializeField] private List<Vector2> _points = new List<Vector2>();

    [Header("빛")]
    [SerializeField] private Sprite _sprite;

    [Tooltip("조명을 받지 않는 재질 (밤에도 밝게)")]
    [SerializeField] private Material _material;

    [SerializeField] private Color _color = new Color(1f, 0.78f, 0.4f, 1f);

    [Tooltip("빛무리 지름 (월드 단위)")]
    [SerializeField] private float _size = 1.2f;

    [Tooltip("낮 밝기")]
    [Range(0f, 1f)]
    [SerializeField] private float _dayAlpha = 0.3f;

    [Tooltip("밤 밝기")]
    [Range(0f, 1f)]
    [SerializeField] private float _nightAlpha = 0.85f;

    [Tooltip("일렁이는 빠르기")]
    [SerializeField] private float _flickerSpeed = 3f;

    [SerializeField] private int _sortingOrder = 31900;

    private readonly List<(SpriteRenderer renderer, float seed)> _glows = new List<(SpriteRenderer, float)>();

    private void Awake()
    {
        float scale = _size / Mathf.Max(0.001f, _sprite.bounds.size.x);
        foreach (var point in _points)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(point.x, point.y, 0f);
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sharedMaterial = _material;
            renderer.sortingOrder = _sortingOrder;
            _glows.Add((renderer, Random.value * 100f));
        }
    }

    private void Update()
    {
        float baseAlpha = Mathf.Lerp(_dayAlpha, _nightAlpha, TimeOfDay.Night(TimeOfDay.CurrentHour));
        foreach (var (renderer, seed) in _glows)
        {
            // 느린 숨쉬기 + 빠른 떨림 (촛불처럼)
            float slow = Mathf.PerlinNoise(seed, Time.time * _flickerSpeed * 0.3f);
            float fast = Mathf.PerlinNoise(seed + 50f, Time.time * _flickerSpeed * 2f);
            float k = 0.75f + 0.18f * slow + 0.07f * fast;
            var color = _color;
            color.a = baseAlpha * k;
            renderer.color = color;
            renderer.transform.localScale = Vector3.one * (_size / Mathf.Max(0.001f, _sprite.bounds.size.x) * (0.94f + 0.08f * slow));
        }
    }
}
