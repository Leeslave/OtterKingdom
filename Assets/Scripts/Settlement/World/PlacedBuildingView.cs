using System.Collections;
using UnityEngine;

/// <summary>
/// 광장에 놓인 건물 하나의 모습 (DecorBoardView가 만들고 치움). 공사 중에는 진행 비율에 따라 공사 단계 그림을 바꿔 끼우고
/// 머리 위에 진행 막대를 보이며, 다 지으면 완성 그림이 통 튀어나온다. 공사·완성은 SettlementManager의 기록을 본다.
/// 해달이 가지고 놀지 않는다 (HasRoom = false)
/// </summary>
public class PlacedBuildingView : PlacedDecorView
{
    // 단계 그림이 바뀌는 진행 비율 (그림 수만큼 앞에서부터 씀: 터 → 골조 → 벽 → 마무리)
    private static readonly float[] StageFrom = { 0f, 0.25f, 0.5f, 0.8f };
    private const float BounceSeconds = 0.3f;
    private const float BarWidthFraction = 0.6f;
    private const float BarHeight = 0.18f;
    private const float BarGap = 0.4f;
    private static readonly Color BarBack = new Color(0.29f, 0.18f, 0.13f, 0.85f);
    private static readonly Color BarFill = new Color(0.55f, 0.85f, 0.42f, 1f);

    private static Sprite _whiteSprite;

    private BuildingDefinition _building;
    private Transform _body;
    private SpriteRenderer _renderer;
    private Transform _bar;
    private SpriteRenderer _barBack;
    private SpriteRenderer _barFill;
    private int _shownStage = int.MinValue; // -1 = 완성 그림
    private Coroutine _bounce;
    private FairyNpcView _fairy;
    private Vector2 _fairyPlacedAt = new Vector2(float.NaN, float.NaN);

    public BuildingDefinition Building => _building;

    internal override void Init(PlacedDecor placed, Rect worldRect, float cellSize, float fill, int sortingOrder)
    {
        Placed = placed;
        _building = (BuildingDefinition)placed.Decor;
        if (_body == null)
        {
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            _renderer = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            _renderer.transform.SetParent(_body, false);
            CreateBar();
        }
        Place(worldRect, fill);
        Refresh(false);
    }

    internal override void Place(Rect worldRect, float fill)
    {
        WorldRect = worldRect;
        transform.position = new Vector3(worldRect.center.x, worldRect.yMin, 0f);
        BuildingVisual.Fit(_body, _renderer, _building, SpriteFor(_shownStage), worldRect);
        PlaceBar();
        SetSortingOrder(0);
    }

    // 큰 건물은 앞 모서리 대신 차지한 칸 가운데 높이로 앞뒤를 정함 (집 옆을 지나는 해달이 지붕에 가려지지 않게. PlazaProp의 집과 같은 규칙)
    internal override void SetSortingOrder(int order)
    {
        int own = PlazaDepth.SortingOrderFor(WorldRect.center.y);
        _renderer.sortingOrder = own;
        if (_barBack != null)
        {
            _barBack.sortingOrder = own + 1;
            _barFill.sortingOrder = own + 2;
        }
    }

    private void Update() => Refresh(true);

    private void Refresh(bool animate)
    {
        var manager = SettlementManager.Instance;
        var record = manager != null && manager.IsLoaded ? manager.BuildingRecordOf(Placed.InstanceId) : null;
        bool built = record == null || record.Built;
        float progress = built ? 1f : record.Progress(SettlementManager.NowTicks);

        int stage = built ? -1 : StageIndexFor(progress);
        if (stage != _shownStage)
        {
            bool finishing = animate && stage == -1 && _shownStage != int.MinValue;
            _shownStage = stage;
            BuildingVisual.Fit(_body, _renderer, _building, SpriteFor(stage), WorldRect);
            if (animate)
                Bounce(finishing ? 0.25f : 0.08f);
        }

        _bar.gameObject.SetActive(!built);
        if (!built)
            _barFill.transform.localScale = new Vector3(Mathf.Max(0.001f, progress) * BarLength, BarHeight * 0.6f, 1f);

        if (built && _building.HostsFairyShop)
            HostFairy();
    }

    // 요정 상점이 이 건물 앞(오른쪽 앞 모서리 쪽)으로 이사. 건물을 옮기면 따라옴
    private void HostFairy()
    {
        Vector2 foot = new Vector2(WorldRect.center.x + WorldRect.width * 0.32f, WorldRect.yMin - 0.45f);
        if (foot == _fairyPlacedAt)
            return;
        if (_fairy == null)
            _fairy = FindAnyObjectByType<FairyNpcView>();
        if (_fairy == null)
            return;
        _fairy.MoveTo(foot);
        _fairyPlacedAt = foot;
    }

    private int StageIndexFor(float progress)
    {
        var stages = _building.StageSprites;
        int index = -1;
        for (int i = 0; i < Mathf.Min(stages.Count, StageFrom.Length); i++)
        {
            if (progress >= StageFrom[i] && stages[i] != null)
                index = i;
        }
        // 단계 그림이 없으면 공사 중에도 완성 그림을 흐리게 대신 씀 (SpriteFor)
        return index < 0 ? 0 : index;
    }

    private Sprite SpriteFor(int stage)
    {
        var stages = _building.StageSprites;
        bool underConstruction = stage >= 0;
        _renderer.color = underConstruction && (stage >= stages.Count || stages[stage] == null)
            ? new Color(1f, 1f, 1f, 0.45f)
            : Color.white;
        if (underConstruction && stage < stages.Count && stages[stage] != null)
            return stages[stage];
        return _building.WorldSprite;
    }

    private void Bounce(float strength)
    {
        if (_bounce != null)
            StopCoroutine(_bounce);
        _bounce = StartCoroutine(BounceRoutine(strength));
    }

    private IEnumerator BounceRoutine(float strength)
    {
        for (float t = 0f; t < BounceSeconds; t += Time.deltaTime)
        {
            float k = t / BounceSeconds;
            float s = 1f + Mathf.Sin(k * Mathf.PI) * strength;
            _body.localScale = new Vector3(1f / Mathf.Sqrt(s), s, 1f);
            yield return null;
        }
        _body.localScale = Vector3.one;
        _bounce = null;
    }

    #region 진행 막대

    private float BarLength => Mathf.Max(0.5f, WorldRect.width * BarWidthFraction);

    private void CreateBar()
    {
        if (_whiteSprite == null)
            _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);

        _bar = new GameObject("ProgressBar").transform;
        _bar.SetParent(transform, false);
        _barBack = new GameObject("Back").AddComponent<SpriteRenderer>();
        _barBack.transform.SetParent(_bar, false);
        _barBack.sprite = _whiteSprite;
        _barBack.color = BarBack;
        _barFill = new GameObject("Fill").AddComponent<SpriteRenderer>();
        _barFill.transform.SetParent(_bar, false);
        _barFill.sprite = _whiteSprite;
        _barFill.color = BarFill;
    }

    // 완성될 그림의 지붕 위
    private void PlaceBar()
    {
        var reference = _building.WorldSprite;
        float height = reference != null
            ? reference.bounds.size.y * (WorldRect.width * _building.WidthFill / Mathf.Max(0.01f, reference.bounds.size.x))
            : WorldRect.height;
        _bar.localPosition = new Vector3(-BarLength * 0.5f, height + BarGap, 0f);
        _barBack.transform.localScale = new Vector3(BarLength, BarHeight, 1f);
        _barFill.transform.localPosition = new Vector3(0f, 0f, 0f);
    }

    #endregion
}
