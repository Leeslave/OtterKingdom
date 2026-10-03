using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공사 현장 한 곳 (집 터, 농경지 개간 자리). 진행 비율에 따라 공사 과정을 단계로 보여 준다.
/// - 집: 주춧돌 → 단계 그림(골조 → 벽 → 마무리, 완성될 집과 같은 크기)을 차례로 바꿔 끼움 → 완성되면 먼지가 펑,
///   집 위로 별빛이 차례로 반짝 (진짜 집은 DevelopmentGate가 띄움). 단계가 바뀔 때마다 통 튀고 먼지가 남
/// - 개간: 치울 것(덤불·돌·통나무)이 순서대로 먼지와 함께 사라짐
/// 건설 해달이 설 자리와 진행 말풍선 위치도 알려 준다.
/// </summary>
public class ConstructionSiteView : MonoBehaviour
{
    // 단계 그림이 바뀌는 진행 비율 (그 전까지는 주춧돌만). 그림이 적으면 앞에서부터 씀
    private static readonly float[] StageFrom = { 0.15f, 0.45f, 0.8f };
    private const float BounceSeconds = 0.3f;
    private const float DustInterval = 0.45f;
    private const float DustSeconds = 0.7f;
    private const int DustPool = 8;
    private const int BurstCount = 7;
    private const int GlintCount = 6;
    private const float GlintInterval = 0.13f;
    private const float GlintSeconds = 0.7f;
    private static readonly Color GlintColor = new Color(1f, 0.98f, 0.86f, 1f);

    [Header("건설")]
    [Tooltip("ConstructionDefinition의 ID (예: con_house_2)")]
    [SerializeField] private string _constructionId;

    [Header("자리")]
    [Tooltip("건설 해달이 서서 일할 곳 (걷기 영역 안)")]
    [SerializeField] private Transform _standPoint;

    [Tooltip("진행 말풍선이 뜰 곳")]
    [SerializeField] private Transform _bubbleAnchor;

    [Header("집 짓기")]
    [Tooltip("건설 예정지 (바닥에 깔린 터·말뚝). 공사를 시작해 첫 단계 그림이 나오기 전까지만 보임. 없어도 됨")]
    [SerializeField] private GameObject _scaffold;

    [Tooltip("단계 그림을 그릴 곳 (완성될 집과 같은 자리·크기·정렬). 없으면 터만")]
    [SerializeField] private SpriteRenderer _stage;

    [Tooltip("공사 단계 그림 (골조 → 벽 → 마무리)")]
    [SerializeField] private List<Sprite> _stageSprites = new List<Sprite>();

    [Header("개간")]
    [Tooltip("진행에 따라 순서대로 치워질 것 (덤불 → 돌 → 통나무)")]
    [SerializeField] private List<GameObject> _clearTargets = new List<GameObject>();

    [Header("먼지")]
    [SerializeField] private Sprite _dustSprite;
    [Tooltip("먼지가 피어오를 가로 폭 (월드 단위)")]
    [SerializeField] private float _dustWidth = 4f;

    [Header("완성 빛")]
    [Tooltip("완성될 때 집 위에서 차례로 반짝이는 별빛")]
    [SerializeField] private Sprite _twinkleSprite;

    [Tooltip("빛 재질 (조명을 받지 않아 밤에도 밝음)")]
    [SerializeField] private Material _glowMaterial;

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
    private Vector3 _stageScale;
    private int _stageIndex = -1;

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
        if (_stage != null)
        {
            _stageScale = _stage.transform.localScale;
            _stage.gameObject.SetActive(false);
        }
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
        if (!building && _stage != null)
        {
            _stage.gameObject.SetActive(false);
            _stageIndex = -1;
        }
        UpdateSite();
    }

    // 건설 예정지는 단계 그림이 나오면 숨김 (단계 그림에 돌 기초가 들어 있음)
    private void UpdateSite()
    {
        bool visible = _building && _stageIndex < 0;
        if (_scaffold != null && _scaffold.activeSelf != visible)
            _scaffold.SetActive(visible);
    }

    /// <summary>진행 비율(0~1)에 맞춰 공사 단계를 보여 주고 치울 것을 치움</summary>
    /// <param name="animate">false면 먼지·튀기 없이 바로 맞춤 (씬을 열었을 때)</param>
    public void SetProgress(float progress, bool animate)
    {
        if (_stage != null)
            ShowStage(StageIndexFor(progress), animate);

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

    /// <summary>완성 순간: 먼지가 펑. lightDelay초 뒤(카메라가 다가온 뒤) 집 위로 별빛이 차례로 반짝</summary>
    public void PlayCompleteBurst(float lightDelay)
    {
        Burst(transform.position, BurstCount);
        var area = BuiltArea();
        for (int i = 0; i < GlintCount; i++)
            StartCoroutine(Glint(area, lightDelay + 0.15f + i * GlintInterval));
    }

    // 이 진행 비율에서 보일 단계 그림 번호 (-1이면 주춧돌만)
    private int StageIndexFor(float progress)
    {
        int index = -1;
        int count = Mathf.Min(_stageSprites.Count, StageFrom.Length);
        for (int i = 0; i < count; i++)
        {
            if (progress >= StageFrom[i])
                index = i;
        }
        return index;
    }

    // 단계가 바뀌면 그림을 바꿔 끼우고, 통 튀며 먼지가 남
    private void ShowStage(int index, bool animate)
    {
        if (index == _stageIndex)
            return;
        _stageIndex = index;
        UpdateSite();

        bool visible = index >= 0 && _stageSprites[index] != null;
        _stage.gameObject.SetActive(visible);
        if (!visible)
            return;
        _stage.sprite = _stageSprites[index];
        if (!animate)
            return;
        StartCoroutine(Bounce(_stage.transform, _stageScale));
        Burst(transform.position, 5);
    }

    // 바닥에 닿은 채로 0.85 → 1.08 → 1 (통 세워지는 느낌)
    private IEnumerator Bounce(Transform target, Vector3 baseScale)
    {
        for (float t = 0f; t < BounceSeconds; t += Time.deltaTime)
        {
            float k = t / BounceSeconds;
            float s = k < 0.6f ? Mathf.Lerp(0.85f, 1.08f, k / 0.6f) : Mathf.Lerp(1.08f, 1f, (k - 0.6f) / 0.4f);
            target.localScale = new Vector3(baseScale.x, baseScale.y * s, baseScale.z);
            yield return null;
        }
        target.localScale = baseScale;
    }

    #region 완성 빛

    // 다 지은 것이 차지하는 곳: 집은 마지막 단계 그림(완성될 집과 같은 크기), 아니면 먼지 폭만큼 위로 (별빛이 뜰 곳)
    private Rect BuiltArea()
    {
        var p = transform.position;
        if (_stage == null || _stageSprites.Count == 0 || _stageSprites[_stageSprites.Count - 1] == null)
            return new Rect(p.x - _dustWidth * 0.5f, p.y, _dustWidth, _dustWidth * 0.6f);

        var bounds = _stageSprites[_stageSprites.Count - 1].bounds;
        var stage = _stage.transform;
        Vector2 a = stage.TransformPoint(bounds.min);
        Vector2 b = stage.TransformPoint(bounds.max);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    private SpriteRenderer CreateLight(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = _glowMaterial;
        renderer.sortingOrder = order;
        return renderer;
    }

    // 집 윗부분 아무 곳에서 별빛이 톡 커졌다 돌며 사라짐
    private IEnumerator Glint(Rect area, float delay)
    {
        for (float t = 0f; t < delay; t += Time.deltaTime)
            yield return null;

        var glint = CreateLight("Glint", _twinkleSprite, _baseOrder + 6);
        glint.transform.position = new Vector3(
            Random.Range(area.xMin + area.width * 0.15f, area.xMax - area.width * 0.15f),
            Random.Range(area.yMin + area.height * 0.4f, area.yMax - area.height * 0.05f),
            transform.position.z);
        float size = Random.Range(0.6f, 1f) / _twinkleSprite.bounds.size.x;
        for (float t = 0f; t < GlintSeconds; t += Time.deltaTime)
        {
            float k = t / GlintSeconds;
            float shine = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * k)), 1.5f);
            glint.transform.localScale = Vector3.one * (size * shine);
            glint.transform.localRotation = Quaternion.Euler(0f, 0f, 45f * k);
            var color = GlintColor;
            color.a = shine;
            glint.color = color;
            yield return null;
        }
        Destroy(glint.gameObject);
    }

    #endregion

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
