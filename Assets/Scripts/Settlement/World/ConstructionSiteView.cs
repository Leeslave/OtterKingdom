using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공사 현장 한 곳 (집 터, 농경지 개간 자리). 진행 비율에 따라 공사 과정을 보여 준다.
/// - 집: 주춧돌 → 집이 아래에서부터 올라옴(마스크) + 공사 먼지 → 완성되면 먼지가 펑 (진짜 집은 DevelopmentGate가 띄움)
/// - 개간: 치울 것(덤불·돌·통나무)이 순서대로 먼지와 함께 사라짐
/// 건설 해달이 설 자리와 진행 말풍선 위치도 알려 준다.
/// </summary>
public class ConstructionSiteView : MonoBehaviour
{
    // 이 비율까지는 주춧돌만, 그 뒤로 집이 올라옴
    private const float FoundationOnlyUntil = 0.12f;
    private const float DustInterval = 0.45f;
    private const float DustSeconds = 0.7f;
    private const int DustPool = 8;
    private const int BurstCount = 7;

    [Header("건설")]
    [Tooltip("ConstructionDefinition의 ID (예: con_house_2)")]
    [SerializeField] private string _constructionId;

    [Header("자리")]
    [Tooltip("건설 해달이 서서 일할 곳 (걷기 영역 안)")]
    [SerializeField] private Transform _standPoint;

    [Tooltip("진행 말풍선이 뜰 곳")]
    [SerializeField] private Transform _bubbleAnchor;

    [Header("집 짓기")]
    [Tooltip("건설 중에만 보이는 터 (주춧돌). 없어도 됨")]
    [SerializeField] private GameObject _scaffold;

    [Tooltip("올라오는 집 그림 (완성될 집과 같은 그림). 없으면 터만")]
    [SerializeField] private SpriteRenderer _rising;

    [Tooltip("올라오는 집을 아래부터 보여 줄 마스크 (발밑 피벗, 세로 크기 = 보이는 높이)")]
    [SerializeField] private SpriteMask _revealMask;

    [Tooltip("집 전체 높이 (월드 단위)")]
    [SerializeField] private float _riseHeight = 4f;

    [Header("개간")]
    [Tooltip("진행에 따라 순서대로 치워질 것 (덤불 → 돌 → 통나무)")]
    [SerializeField] private List<GameObject> _clearTargets = new List<GameObject>();

    [Header("먼지")]
    [SerializeField] private Sprite _dustSprite;
    [Tooltip("먼지가 피어오를 가로 폭 (월드 단위)")]
    [SerializeField] private float _dustWidth = 4f;

    public string ConstructionId => _constructionId;
    public Vector2 StandPoint => _standPoint.position;
    public Vector3 BubbleAnchor => _bubbleAnchor.position;
    /// <summary>건설 해달이 바라볼 곳 (현장 가운데)</summary>
    public Vector2 LookPoint => transform.position;

    private class Dust
    {
        public SpriteRenderer Renderer;
        public float Age;
        public float Size;
        public Vector3 Drift;
    }

    private readonly List<Dust> _dust = new List<Dust>();
    private bool _building;
    private float _dustTimer;
    private int _baseOrder;

    private void Awake()
    {
        for (int i = 0; i < DustPool; i++)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _dustSprite;
            go.SetActive(false);
            _dust.Add(new Dust { Renderer = renderer, Age = DustSeconds });
        }
        if (_rising != null)
            _rising.gameObject.SetActive(false);
        _baseOrder = PlazaDepth.SortingOrderFor(transform.position.y);
    }

    private void Update()
    {
        if (_building && _dustSprite != null)
        {
            _dustTimer -= Time.deltaTime;
            if (_dustTimer <= 0f)
            {
                _dustTimer = DustInterval * Random.Range(0.7f, 1.3f);
                SpawnDust(RandomBasePoint(), Random.Range(0.6f, 0.9f));
            }
        }
        AnimateDust();
    }

    /// <summary>건설 중인지 (터·올라오는 집을 켜고 끔)</summary>
    public void SetBuilding(bool building)
    {
        _building = building;
        if (_scaffold != null && _scaffold.activeSelf != building)
            _scaffold.SetActive(building);
        if (_rising != null && !building && _rising.gameObject.activeSelf)
            _rising.gameObject.SetActive(false);
    }

    /// <summary>진행 비율(0~1)에 맞춰 집을 올리고 치울 것을 치움</summary>
    /// <param name="animate">false면 먼지 없이 바로 맞춤 (씬을 열었을 때)</param>
    public void SetProgress(float progress, bool animate)
    {
        if (_rising != null)
        {
            float rise = Mathf.InverseLerp(FoundationOnlyUntil, 1f, progress);
            bool visible = rise > 0f;
            if (_rising.gameObject.activeSelf != visible)
                _rising.gameObject.SetActive(visible);
            if (visible)
            {
                var scale = _revealMask.transform.localScale;
                scale.y = _riseHeight * rise;
                _revealMask.transform.localScale = scale;
                // 마스크가 올라오는 집에만 걸리도록 그 집의 그리는 순서 앞뒤로 범위를 둠
                _revealMask.isCustomRangeActive = true;
                _revealMask.frontSortingOrder = _rising.sortingOrder + 1;
                _revealMask.backSortingOrder = _rising.sortingOrder - 1;
            }
        }

        for (int i = 0; i < _clearTargets.Count; i++)
        {
            var target = _clearTargets[i];
            if (target == null)
                continue;
            bool cleared = progress >= (i + 1f) / (_clearTargets.Count + 1f);
            if (target.activeSelf == !cleared)
                continue;
            target.SetActive(!cleared);
            if (cleared && animate)
                Burst(target.transform.position, 4);
        }
    }

    /// <summary>완성 순간 먼지가 펑</summary>
    public void PlayCompleteBurst() => Burst(transform.position, BurstCount);

    #region 먼지

    private Vector3 RandomBasePoint()
    {
        var p = transform.position;
        return new Vector3(p.x + Random.Range(-0.5f, 0.5f) * _dustWidth, p.y + Random.Range(-0.2f, 0.4f), p.z);
    }

    private void Burst(Vector3 center, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
            var offset = new Vector3(Mathf.Cos(angle) * _dustWidth * 0.35f, Mathf.Sin(angle) * 0.5f + 0.3f, 0f);
            SpawnDust(center + offset, Random.Range(0.9f, 1.3f));
        }
    }

    private void SpawnDust(Vector3 position, float size)
    {
        if (_dustSprite == null)
            return;
        Dust free = null;
        foreach (var d in _dust)
        {
            if (d.Age >= DustSeconds)
            {
                free = d;
                break;
            }
        }
        if (free == null)
            return;

        free.Age = 0f;
        free.Size = size;
        free.Drift = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.4f, 0.8f), 0f);
        free.Renderer.transform.position = position;
        free.Renderer.gameObject.SetActive(true);
        // 집·소품보다 앞에
        free.Renderer.sortingOrder = Mathf.Max(_baseOrder, PlazaDepth.SortingOrderFor(position.y)) + 5;
    }

    private void AnimateDust()
    {
        foreach (var d in _dust)
        {
            if (d.Age >= DustSeconds)
                continue;
            d.Age += Time.deltaTime;
            float k = Mathf.Clamp01(d.Age / DustSeconds);
            var t = d.Renderer.transform;
            t.position += d.Drift * Time.deltaTime;
            t.localScale = Vector3.one * (d.Size * Mathf.Lerp(0.5f, 1.15f, k));
            var color = d.Renderer.color;
            color.a = k < 0.3f ? k / 0.3f : 1f - (k - 0.3f) / 0.7f;
            d.Renderer.color = color;
            if (d.Age >= DustSeconds)
                d.Renderer.gameObject.SetActive(false);
        }
    }

    #endregion
}
