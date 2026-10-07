using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 장소 씬(광장·밭·낚시터) 하나의 꾸미기 격자를 월드에 올린다.
/// - 이 오브젝트의 위치 = 격자 왼쪽 아래 모서리, 칸 크기는 _cellSize
/// - 씬이 열릴 때 막힌 칸을 정한다: 걷기 영역 밖(광장·낚시터), DecorBlockArea(밭고랑·낚시 자리), 비워 둘 지점(웨이포인트 등)
/// - 놓인 물건을 그리고, 걷기 영역에 장애물로 알려 해달이 피해 가게 한다
/// - 해달에게 가지고 놀 물건과 설 자리를 나눠 준다 (DecorPlaySession)
/// </summary>
// 걷기 영역(-100)이 만들어진 뒤, 해달들이 움직이기 전에 준비
[DefaultExecutionOrder(-40)]
public class DecorBoardView : MonoBehaviour
{
    /// <summary>지금 씬의 꾸미기 격자 (없으면 null). 해달 컨트롤러가 놀 물건을 찾을 때 쓴다</summary>
    public static DecorBoardView Active { get; private set; }

    [Header("데이터")]
    [SerializeField] private DecorBoardDefinition _board;

    [Header("월드 배치")]
    [Tooltip("칸 한 변의 길이 (월드 단위). 이 오브젝트 위치가 격자 왼쪽 아래 모서리")]
    [Min(0.05f)]
    [SerializeField] private float _cellSize = 1f;

    [Header("막힌 칸")]
    [Tooltip("있으면: 걷기 영역 밖의 칸을 막고, 놓인 물건을 장애물로 알린다 (광장·낚시터)")]
    [SerializeField] private PlazaWalkableArea _walkableArea;
    [Tooltip("걷기 영역 밖에도 놓게 할지 (낚시터: 해달은 부두 앞쪽만 걷고 물건은 뒤쪽에 둠)")]
    [SerializeField] private bool _allowOutsideWalkable;
    [Tooltip("놓인 물건을 걷기 영역의 장애물로 알릴지 (해달이 물건을 피해 감)")]
    [SerializeField] private bool _toysBlockWalking = true;
    [Tooltip("물건을 놓지 못하게 비워 둘 지점 (해달의 웨이포인트, 수확 자리 등). 그 지점이 든 칸이 막힌다")]
    [SerializeField] private List<Transform> _reservedPoints = new List<Transform>();
    [Tooltip("비워 둘 지점 둘레로 함께 비울 거리 (월드 단위). 지점이 칸 경계에 걸려도 옆 칸까지 비워 둠")]
    [Min(0f)]
    [SerializeField] private float _reservedRadius = 0.35f;

    [Header("그리기")]
    [Tooltip("켜면 땅에 닿는 높이로 앞뒤를 정함 (광장, PlazaDepth). 끄면 아래 고정 순서")]
    [SerializeField] private bool _depthSorted = true;
    [Tooltip("_depthSorted가 꺼져 있을 때의 정렬 순서 (밭: 밭고랑 1 < 물건 < 해달 2)")]
    [SerializeField] private int _sortingOrder = 1;
    [Tooltip("물건 그림이 차지한 칸을 채우는 비율")]
    [Range(0.3f, 1f)]
    [SerializeField] private float _fill = 0.85f;

    [Header("놀이")]
    [Tooltip("해달이 다음 행동을 고를 때 물건을 가지고 놀러 갈 확률")]
    [Range(0f, 1f)]
    [SerializeField] private float _playChance = 0.35f;
    [Tooltip("물건과 해달 발 사이 간격 (월드 단위)")]
    [Min(0f)]
    [SerializeField] private float _standGap = 0.35f;

    [Header("디버그")]
    [SerializeField] private bool _drawGrid = true;

    public DecorBoardDefinition Board => _board;
    public DecorLayout Layout { get; private set; }
    public float CellSize => _cellSize;
    public Vector2 Origin => transform.position;
    public float Fill => _fill;

    /// <summary>꾸미기 모드 격자 표시의 정렬 순서 (땅 위, 물건·해달 아래)</summary>
    public int OverlaySortingOrder => _depthSorted ? PlazaDepth.FlatPropOrder + 1 : _sortingOrder;

    /// <summary>꾸미기 모드 미리보기의 정렬 순서 (모든 것 위)</summary>
    public int GhostSortingOrder => _depthSorted ? short.MaxValue - 10 : _sortingOrder + 50;

    // 길이 끊겼다고 보는 걷기 영역 크기 (걷기 칸 수). 이보다 작은 틈은 세지 않음
    private const int MinRegionCells = 12;

    private readonly Dictionary<int, PlacedDecorView> _views = new Dictionary<int, PlacedDecorView>();
    private readonly List<Rect> _obstacles = new List<Rect>();
    private int _appliedStaticVersion = -1;
    private readonly List<Vector2Int> _cornerBuffer = new List<Vector2Int>();

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    private void Start()
    {
        if (DecorManager.Instance == null)
        {
            Debug.LogWarning($"[DecorBoardView] DecorManager가 없어 '{name}' 격자를 쓰지 않습니다 (GlobalUI 확인).", this);
            enabled = false;
            return;
        }

        Layout = DecorManager.Instance.GetLayout(_board);
        ApplySceneBlockers();
        if (_walkableArea != null)
            _appliedStaticVersion = _walkableArea.StaticVersion;

        Layout.OnPlaced += HandlePlaced;
        Layout.OnMoved += HandleMoved;
        Layout.OnRemoved += HandleRemoved;

        foreach (var placed in Layout.Placed)
            CreateView(placed);
        UpdateObstacles();
    }

    private void OnDestroy()
    {
        if (Layout == null)
            return;

        Layout.OnPlaced -= HandlePlaced;
        Layout.OnMoved -= HandleMoved;
        Layout.OnRemoved -= HandleRemoved;
    }

    // 걷기 영역이 다시 만들어지면(영토가 넓어짐 · 집이 생김) 놓을 수 있는 칸도 다시 정함
    private void Update()
    {
        if (Layout == null || _walkableArea == null || _walkableArea.StaticVersion == _appliedStaticVersion)
            return;
        _appliedStaticVersion = _walkableArea.StaticVersion;
        ApplySceneBlockers();
    }

    #region 좌표

    public Vector2 CellCenter(Vector2Int cell) => Origin + ((Vector2)cell + Vector2.one * 0.5f) * _cellSize;

    public Vector2Int WorldToCell(Vector2 world)
    {
        Vector2 local = (world - Origin) / _cellSize;
        return new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
    }

    public Rect AreaWorldRect(RectInt area) => new Rect(Origin + (Vector2)area.position * _cellSize, (Vector2)area.size * _cellSize);

    public bool ContainsCell(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Layout.Size.x && cell.y < Layout.Size.y;

    #endregion

    #region 막힌 칸

    // 격자는 장소를 넘어 유지되지만 막힌 칸은 씬의 모습이라, 씬이 열릴 때마다 다시 정한다
    private void ApplySceneBlockers()
    {
        Layout.ClearBlocked();
        var blockAreas = FindObjectsByType<DecorBlockArea>(FindObjectsSortMode.None);

        for (int y = 0; y < Layout.Size.y; y++)
        {
            for (int x = 0; x < Layout.Size.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (IsBlockedInScene(cell, blockAreas))
                    Layout.SetBlocked(cell, true);
            }
        }

        // 채집 나무·바위, 해달 모임 자리처럼 스스로 비워 둘 영역을 알려 주는 것
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour is IDecorBlocker blocker && blocker.TryGetDecorBlock(out var area))
                BlockOverlapping(area);
        }

        foreach (var point in _reservedPoints)
        {
            if (point == null)
                continue;

            var min = WorldToCell((Vector2)point.position - Vector2.one * _reservedRadius);
            var max = WorldToCell((Vector2)point.position + Vector2.one * _reservedRadius);
            for (int y = min.y; y <= max.y; y++)
            {
                for (int x = min.x; x <= max.x; x++)
                    Layout.SetBlocked(new Vector2Int(x, y), true);
            }
        }
    }

    private void BlockOverlapping(Rect area)
    {
        var min = WorldToCell(area.min);
        var max = WorldToCell(area.max);
        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (ContainsCell(cell) && AreaWorldRect(new RectInt(cell, Vector2Int.one)).Overlaps(area))
                    Layout.SetBlocked(cell, true);
            }
        }
    }

    private bool IsBlockedInScene(Vector2Int cell, DecorBlockArea[] blockAreas)
    {
        var rect = AreaWorldRect(new RectInt(cell, Vector2Int.one));
        foreach (var area in blockAreas)
        {
            if (area.WorldRect.Overlaps(rect))
                return true;
        }

        return _walkableArea != null && !_allowOutsideWalkable && !_walkableArea.IsStaticWalkable(rect.center);
    }

    #endregion

    #region 물건 그리기

    private void HandlePlaced(PlacedDecor placed)
    {
        CreateView(placed);
        UpdateObstacles();
    }

    private void HandleMoved(PlacedDecor placed)
    {
        if (_views.TryGetValue(placed.InstanceId, out var view))
        {
            var rect = AreaWorldRect(placed.Area);
            view.Place(rect, _fill);
            view.SetSortingOrder(SortingOrderFor(rect));
        }
        UpdateObstacles();
    }

    private void HandleRemoved(PlacedDecor placed)
    {
        if (_views.TryGetValue(placed.InstanceId, out var view))
        {
            _views.Remove(placed.InstanceId);
            view.gameObject.SetActive(false); // 노는 중인 해달이 IsValid로 알아채도록 먼저 끔
            Destroy(view.gameObject);
        }
        UpdateObstacles();
    }

    private void CreateView(PlacedDecor placed)
    {
        var go = new GameObject($"Decor_{placed.Decor.SaveId}_{placed.InstanceId}");
        go.transform.SetParent(transform, true);
        // 건물은 공사 단계·완성 그림을 그리는 뷰 (해달이 가지고 놀지 않음)
        var view = placed.Decor.IsBuilding ? go.AddComponent<PlacedBuildingView>() : go.AddComponent<PlacedDecorView>();
        var rect = AreaWorldRect(placed.Area);
        view.Init(placed, rect, _cellSize, _fill, SortingOrderFor(rect));
        _views[placed.InstanceId] = view;
    }

    /// <summary>
    /// 이 영역을 막으면 해달이 걷는 길이 둘로 끊기는지 (큰 건물을 놓기 전 확인). 지금 놓인 물건(숨긴 것 제외)에 영역 하나를 더해 본다.
    /// 걷기 영역이 없는 장소는 늘 false
    /// </summary>
    public bool WouldCutPath(RectInt area)
    {
        if (_walkableArea == null)
            return false;
        int before = _walkableArea.CountRegions(_obstacles, MinRegionCells);
        var withArea = new List<Rect>(_obstacles) { AreaWorldRect(area) };
        return _walkableArea.CountRegions(withArea, MinRegionCells) > before;
    }

    /// <summary>꾸미기 모드에서 들어 올린 물건을 잠시 숨김 (미리보기가 대신 보임). 숨긴 물건은 놀이·장애물에서 빠진다</summary>
    public void SetHidden(int instanceId, bool hidden)
    {
        if (!_views.TryGetValue(instanceId, out var view))
            return;

        view.gameObject.SetActive(!hidden);
        UpdateObstacles();
    }

    private int SortingOrderFor(Rect rect) => _depthSorted ? PlazaDepth.SortingOrderFor(rect.yMin) : _sortingOrder;

    private void UpdateObstacles()
    {
        if (_walkableArea == null || !_toysBlockWalking)
            return;

        _obstacles.Clear();
        foreach (var view in _views.Values)
        {
            if (view.gameObject.activeSelf)
                _obstacles.Add(view.WorldRect);
        }
        _walkableArea.SetObstacles(_obstacles);
    }

    #endregion

    #region 놀이

    /// <summary>해달이 다음 행동으로 놀러 갈지 (확률 + 자리가 남은 물건이 있는지)</summary>
    public bool RollWantsToPlay()
    {
        if (_views.Count == 0 || Random.value >= _playChance)
            return false;

        foreach (var view in _views.Values)
        {
            if (view.gameObject.activeSelf && view.HasRoom)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 자리가 남은 물건 중 하나(가까운 것일수록 잘 고름)와 설 자리를 예약한다.
    /// canStand: 해달이 그 자리에 설 수 있는지 (광장·낚시터는 걷기 영역, 밭은 CanStandOnGrid)
    /// </summary>
    public bool TryReservePlay(Vector2 from, Func<Vector2, bool> canStand, out DecorPlaySession session)
    {
        if (canStand == null)
            throw new ArgumentNullException(nameof(canStand));

        session = null;
        var candidates = new List<PlacedDecorView>();
        foreach (var view in _views.Values)
        {
            if (view.gameObject.activeSelf && view.HasRoom)
                candidates.Add(view);
        }
        candidates.Sort((a, b) => Vector2.Distance(from, a.WorldRect.center).CompareTo(Vector2.Distance(from, b.WorldRect.center)));

        // 가까운 물건부터 보되, 늘 같은 것만 고르지 않게 앞쪽 몇 개 중에서 무작위로 시작
        int start = candidates.Count > 0 ? Random.Range(0, Mathf.Min(3, candidates.Count)) : 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            var toy = candidates[(start + i) % candidates.Count];
            if (!TryFindStandPoint(toy, from, canStand, out var standPoint))
                continue;

            toy.Players++;
            session = new DecorPlaySession(toy, standPoint, toy.Placed.Decor.RollPlaySeconds());
            return true;
        }
        return false;
    }

    // 물건의 왼쪽·오른쪽·앞(아래)·뒤(위) 중 설 수 있는 곳. 해달이 오는 쪽에 가까운 자리를 먼저 본다
    private bool TryFindStandPoint(PlacedDecorView toy, Vector2 from, Func<Vector2, bool> canStand, out Vector2 point)
    {
        var rect = toy.WorldRect;
        var sides = new List<Vector2>
        {
            new Vector2(rect.xMin - _standGap, rect.yMin + rect.height * 0.3f),
            new Vector2(rect.xMax + _standGap, rect.yMin + rect.height * 0.3f),
            new Vector2(rect.center.x, rect.yMin - _standGap),
            new Vector2(rect.center.x, rect.yMax + _standGap),
        };
        sides.Sort((a, b) => Vector2.Distance(from, a).CompareTo(Vector2.Distance(from, b)));

        foreach (var side in sides)
        {
            if (canStand(side))
            {
                point = side;
                return true;
            }
        }

        point = default;
        return false;
    }

    #endregion

    #region 격자 이동 (걷기 영역이 없는 밭)

    /// <summary>격자 안이고 물건이 없는 자리면 설 수 있음 (밭 해달용)</summary>
    public bool CanStandOnGrid(Vector2 world)
    {
        var cell = WorldToCell(world);
        return ContainsCell(cell) && Layout.GetAt(cell) == null;
    }

    /// <summary>
    /// 밭 해달의 "세로 먼저, 가로 나중" 이동 길이 물건에 막히면, 물건을 돌아가는 꺾인 길을 만든다.
    /// 막히지 않았거나 격자 밖이면 false (원래 방식대로 걸으면 됨)
    /// </summary>
    /// <param name="points">돌아가는 길의 꺾이는 지점들 (마지막은 to)</param>
    public bool TryFindDetour(Vector2 from, Vector2 to, List<Vector2> points)
    {
        points.Clear();
        if (Layout == null || _views.Count == 0)
            return false;

        var corner = new Vector2(from.x, to.y);
        if (!SegmentHitsToy(from, corner) && !SegmentHitsToy(corner, to))
            return false;

        var start = WorldToCell(from);
        var goal = WorldToCell(to);
        if (!ContainsCell(start) || !ContainsCell(goal))
            return false;

        if (!DecorGridPath.FindPath(Layout.Size, cell => Layout.GetAt(cell) == null, start, goal, _cornerBuffer))
            return false;

        foreach (var cell in _cornerBuffer)
            points.Add(CellCenter(cell));
        if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], to) > 0.001f)
            points.Add(to);
        return true;
    }

    /// <summary>
    /// 밭 해달이 물건을 밟지 않고 갈 수 있는지 (곧은 길이 비었거나 돌아가는 길이 있음).
    /// 격자 밖이 끼면 알 수 없으므로 true
    /// </summary>
    public bool CanReachWithoutToys(Vector2 from, Vector2 to)
    {
        if (Layout == null || _views.Count == 0)
            return true;

        var corner = new Vector2(from.x, to.y);
        if (!SegmentHitsToy(from, corner) && !SegmentHitsToy(corner, to))
            return true;

        var start = WorldToCell(from);
        var goal = WorldToCell(to);
        if (!ContainsCell(start) || !ContainsCell(goal))
            return true;

        return DecorGridPath.FindPath(Layout.Size, cell => Layout.GetAt(cell) == null, start, goal, _cornerBuffer);
    }

    // 곧은 선분(가로 또는 세로)이 물건이 놓인 칸을 지나는지. 칸 크기의 절반 간격으로 확인
    private bool SegmentHitsToy(Vector2 a, Vector2 b)
    {
        float length = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / (_cellSize * 0.5f)));
        for (int i = 0; i <= steps; i++)
        {
            var cell = WorldToCell(Vector2.Lerp(a, b, (float)i / steps));
            if (ContainsCell(cell) && Layout.GetAt(cell) != null)
                return true;
        }
        return false;
    }

    #endregion

    #region 디버그

    [ContextMenu("테스트: 장난감 지급 후 빈 칸에 놓기")]
    private void DebugPlaceToys()
    {
        if (!Application.isPlaying || Layout == null)
        {
            Debug.LogWarning("[DecorBoardView] Play 모드에서만 쓸 수 있습니다.");
            return;
        }

        var manager = DecorManager.Instance;
        var inventory = InventoryManager.Instance.Inventory;
        foreach (var decor in manager.Catalog.Decors)
        {
            if (decor == null)
                continue;

            inventory.Add(decor.Item, 2, ItemChangeReason.Test);
            for (int attempt = 0; attempt < 200 && manager.StorageCount(decor) > 0; attempt++)
            {
                var cell = new Vector2Int(Random.Range(0, Layout.Size.x), Random.Range(0, Layout.Size.y));
                manager.TryPlaceFromStorage(_board, decor, cell, DecorRotation.R0, out _);
            }
        }
    }

    [ContextMenu("테스트: 이 장소의 물건 모두 치우기")]
    private void DebugClearToys()
    {
        if (Layout == null)
            return;

        foreach (var placed in new List<PlacedDecor>(Layout.Placed))
            Layout.Remove(placed.InstanceId);
    }

    private void OnDrawGizmosSelected()
    {
        if (!_drawGrid || _board == null)
            return;

        var size = _board.Size;
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var rect = new Rect(Origin + (Vector2)cell * _cellSize, Vector2.one * _cellSize);
                Color color = new Color(1f, 1f, 1f, 0.35f);
                if (Layout != null)
                {
                    switch (Layout.GetCellState(cell))
                    {
                        case DecorCellState.Unavailable: color = new Color(0.9f, 0.3f, 0.3f, 0.35f); break;
                        case DecorCellState.Locked: color = new Color(0.5f, 0.5f, 0.5f, 0.35f); break;
                        case DecorCellState.Occupied: color = new Color(0.95f, 0.8f, 0.3f, 0.5f); break;
                        default: color = new Color(0.55f, 0.8f, 0.5f, 0.35f); break;
                    }
                }
                Gizmos.color = color;
                Gizmos.DrawWireCube(rect.center, rect.size * 0.96f);
            }
        }
    }

    #endregion
}
