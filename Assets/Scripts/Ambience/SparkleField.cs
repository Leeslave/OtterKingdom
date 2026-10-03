using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 반짝임: 정해진 곳(광산 수정, 물결)에서 작은 별빛이 하나씩 톡 켜졌다 돌며 꺼진다.
/// 조명을 받지 않는 재질이라 밤에도 빛나고, 밤에는 따로 정한 만큼만 (물결은 해가 없으면 흐리게, 수정은 그대로).
/// SettlementSetup.Life가 장소마다 붙인다.
/// </summary>
public class SparkleField : MonoBehaviour
{
    private const int PoolSize = 12;

    [Header("곳")]
    [SerializeField] private WorldArea _area = new WorldArea();

    [Header("반짝임")]
    [SerializeField] private Sprite _sprite;

    [Tooltip("조명을 받지 않는 재질 (밤에도 밝게)")]
    [SerializeField] private Material _material;

    [SerializeField] private Color _color = new Color(1f, 1f, 0.95f, 1f);

    [Tooltip("크기 (월드 단위, 최소~최대)")]
    [SerializeField] private Vector2 _size = new Vector2(0.25f, 0.45f);

    [Tooltip("하나가 켜졌다 꺼지는 시간 (초)")]
    [SerializeField] private float _lifeSeconds = 0.7f;

    [Tooltip("다음 반짝임까지 (초, 최소~최대)")]
    [SerializeField] private Vector2 _interval = new Vector2(0.2f, 0.6f);

    [Tooltip("밤에 밝기 배율 (0이면 밤에는 안 보임)")]
    [Range(0f, 1f)]
    [SerializeField] private float _nightAlpha = 1f;

    [SerializeField] private int _sortingOrder = 5;

    [Header("빛무리 (선택)")]
    [Tooltip("반짝일 때 뒤에 번지는 은은한 빛 (비우면 없음). 수정처럼 스스로 빛나는 것에")]
    [SerializeField] private Sprite _haloSprite;

    [Tooltip("빛무리 크기 (반짝임 대비)")]
    [SerializeField] private float _haloScale = 2.6f;

    [SerializeField] private Color _haloColor = new Color(0.8f, 0.7f, 1f, 0.6f);

    private class Glint
    {
        public SpriteRenderer Renderer;
        public SpriteRenderer Halo;
        public float Age;
        public float Size;
        public float Spin;
    }

    private readonly List<Glint> _glints = new List<Glint>();
    private float _timer;
    private float _spriteWidth = 1f;

    private void Awake()
    {
        _spriteWidth = Mathf.Max(0.001f, _sprite.bounds.size.x);
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("Glint");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sharedMaterial = _material;
            renderer.sortingOrder = _sortingOrder;
            SpriteRenderer halo = null;
            if (_haloSprite != null)
            {
                var haloGo = new GameObject("Halo");
                haloGo.transform.SetParent(go.transform, false);
                halo = haloGo.AddComponent<SpriteRenderer>();
                halo.sprite = _haloSprite;
                halo.sharedMaterial = _material;
                halo.sortingOrder = _sortingOrder - 1;
            }
            go.SetActive(false);
            _glints.Add(new Glint { Renderer = renderer, Halo = halo, Age = _lifeSeconds });
        }
    }

    private void Update()
    {
        float alpha = Mathf.Lerp(1f, _nightAlpha, TimeOfDay.Night(TimeOfDay.CurrentHour));
        _timer -= Time.deltaTime;
        if (_timer <= 0f && alpha > 0.01f)
        {
            _timer = Random.Range(_interval.x, _interval.y);
            Spawn();
        }

        foreach (var glint in _glints)
        {
            if (glint.Age >= _lifeSeconds)
                continue;
            glint.Age += Time.deltaTime;
            float k = Mathf.Clamp01(glint.Age / _lifeSeconds);
            // 끝(k=1)에서 sin이 아주 작은 음수가 되어 제곱근이 NaN이 되지 않게
            float shine = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * k)), 1.5f);
            var t = glint.Renderer.transform;
            t.localScale = Vector3.one * (glint.Size / _spriteWidth * shine);
            t.localRotation = Quaternion.Euler(0f, 0f, glint.Spin * k);
            var color = _color;
            color.a *= shine * alpha;
            glint.Renderer.color = color;
            if (glint.Halo != null)
            {
                // 별빛은 돌지만 빛무리는 그대로, 크기는 별빛보다 크고 천천히 사라짐
                var halo = glint.Halo.transform;
                halo.localRotation = Quaternion.Inverse(t.localRotation);
                float haloSize = glint.Size * _haloScale / Mathf.Max(0.001f, _haloSprite.bounds.size.x);
                halo.localScale = Vector3.one * (haloSize / Mathf.Max(0.001f, t.localScale.x) * Mathf.Lerp(0.6f, 1f, shine));
                var haloColor = _haloColor;
                haloColor.a *= Mathf.Sin(Mathf.PI * k) * alpha;
                glint.Halo.color = haloColor;
            }
            if (glint.Age >= _lifeSeconds)
                glint.Renderer.gameObject.SetActive(false);
        }
    }

    private void Spawn()
    {
        Glint free = null;
        foreach (var glint in _glints)
        {
            if (glint.Age >= _lifeSeconds)
            {
                free = glint;
                break;
            }
        }
        if (free == null || !_area.TryPick(out Vector2 point))
            return;
        free.Age = 0f;
        free.Size = Random.Range(_size.x, _size.y);
        free.Spin = Random.Range(-60f, 60f);
        free.Renderer.transform.position = new Vector3(point.x, point.y, 0f);
        free.Renderer.transform.localScale = Vector3.zero;
        free.Renderer.gameObject.SetActive(true);
    }
}
