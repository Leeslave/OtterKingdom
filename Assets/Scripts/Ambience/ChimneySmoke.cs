using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 굴뚝에서 몽글몽글 연기가 피어올라 바람에 살짝 밀리며 흩어진다 (광장 집). 집 오브젝트 아래에 붙어 집이 생겨야 보인다.
/// 조명을 받는 재질이라 밤에는 어둑하게. SettlementSetup.Life가 붙인다.
/// </summary>
public class ChimneySmoke : MonoBehaviour
{
    private const int PoolSize = 6;

    [Header("연기")]
    [SerializeField] private Sprite _sprite;
    [SerializeField] private Color _color = new Color(1f, 1f, 1f, 0.6f);

    [Tooltip("다 퍼졌을 때 크기 (월드 단위)")]
    [SerializeField] private float _size = 0.9f;

    [Tooltip("올라가는 높이 (월드 단위)")]
    [SerializeField] private float _rise = 1.6f;

    [Tooltip("바람에 밀리는 거리 (월드 단위, + 오른쪽)")]
    [SerializeField] private float _drift = 0.7f;

    [Tooltip("한 뭉치가 사라지기까지 (초)")]
    [SerializeField] private float _lifeSeconds = 3.2f;

    [Tooltip("다음 뭉치까지 (초)")]
    [SerializeField] private float _interval = 0.7f;

    [SerializeField] private int _sortingOrder = 31850;

    private class Puff
    {
        public SpriteRenderer Renderer;
        public float Age;
        public float Spin;
        public float Wobble;
    }

    private readonly List<Puff> _puffs = new List<Puff>();
    private float _timer;
    private float _spriteWidth = 1f;

    private void Awake()
    {
        _spriteWidth = Mathf.Max(0.001f, _sprite.bounds.size.x);
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("Puff");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sortingOrder = _sortingOrder;
            go.SetActive(false);
            _puffs.Add(new Puff { Renderer = renderer, Age = _lifeSeconds });
        }
    }

    // 다시 켜지면(집이 다시 보이면) 처음부터
    private void OnEnable()
    {
        foreach (var puff in _puffs)
        {
            puff.Age = _lifeSeconds;
            puff.Renderer.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = _interval * Random.Range(0.8f, 1.25f);
            Emit();
        }

        foreach (var puff in _puffs)
        {
            if (puff.Age >= _lifeSeconds)
                continue;
            puff.Age += Time.deltaTime;
            float k = Mathf.Clamp01(puff.Age / _lifeSeconds);
            var t = puff.Renderer.transform;
            float rise = 1f - (1f - k) * (1f - k);
            t.position = transform.position + new Vector3(_drift * k * k + Mathf.Sin(k * 6f + puff.Wobble) * 0.06f, _rise * rise, 0f);
            t.localScale = Vector3.one * (_size / _spriteWidth * Mathf.Lerp(0.3f, 1f, rise) / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x)));
            t.localRotation = Quaternion.Euler(0f, 0f, puff.Spin * k);
            var color = _color;
            color.a *= Mathf.Clamp01(k / 0.15f) * (1f - k);
            puff.Renderer.color = color;
            if (puff.Age >= _lifeSeconds)
                puff.Renderer.gameObject.SetActive(false);
        }
    }

    private void Emit()
    {
        foreach (var puff in _puffs)
        {
            if (puff.Age < _lifeSeconds)
                continue;
            puff.Age = 0f;
            puff.Spin = Random.Range(-40f, 40f);
            puff.Wobble = Random.value * 6f;
            puff.Renderer.transform.position = transform.position;
            puff.Renderer.transform.localScale = Vector3.zero;
            puff.Renderer.gameObject.SetActive(true);
            return;
        }
    }
}
