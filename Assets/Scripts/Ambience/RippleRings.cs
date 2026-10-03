using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 물에 잠긴 기둥 둘레로 잔물결 고리가 천천히 번졌다 사라진다 (낚시터 부두). 물이 고여 있지 않고 흐르는 느낌.
/// SettlementSetup.Life가 붙인다.
/// </summary>
public class RippleRings : MonoBehaviour
{
    private const int RingsPerPoint = 2;

    [Header("자리")]
    [Tooltip("물에 잠긴 곳 (월드)")]
    [SerializeField] private List<Vector2> _points = new List<Vector2>();

    [Header("고리")]
    [SerializeField] private Sprite _sprite;
    [SerializeField] private Color _color = new Color(1f, 1f, 1f, 0.55f);

    [Tooltip("다 퍼졌을 때 가로 크기 (월드 단위)")]
    [SerializeField] private float _width = 1.1f;

    [Tooltip("한 번 퍼지는 시간 (초)")]
    [SerializeField] private float _seconds = 2.4f;

    [SerializeField] private int _sortingOrder = 1;

    private readonly List<(SpriteRenderer renderer, Vector2 center, float offset)> _rings = new List<(SpriteRenderer, Vector2, float)>();
    private float _spriteWidth = 1f;

    private void Awake()
    {
        _spriteWidth = Mathf.Max(0.001f, _sprite.bounds.size.x);
        foreach (var point in _points)
        {
            float start = Random.value;
            for (int i = 0; i < RingsPerPoint; i++)
            {
                var go = new GameObject("Ripple");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(point.x, point.y, 0f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _sprite;
                renderer.sortingOrder = _sortingOrder;
                // 같은 자리의 고리 둘은 반 박자 엇갈려 끊김 없이 이어짐
                _rings.Add((renderer, point, start + i / (float)RingsPerPoint));
            }
        }
    }

    private void Update()
    {
        foreach (var (renderer, center, offset) in _rings)
        {
            float k = Mathf.Repeat(Time.time / _seconds + offset, 1f);
            float width = Mathf.Lerp(0.35f, 1f, 1f - (1f - k) * (1f - k)) * _width;
            renderer.transform.localScale = new Vector3(width / _spriteWidth, width / _spriteWidth, 1f);
            var color = _color;
            color.a *= Mathf.Clamp01(k / 0.15f) * (1f - k);
            renderer.color = color;
        }
    }
}
