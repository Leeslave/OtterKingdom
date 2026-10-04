using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광장 카메라가 갈 수 있는 범위를 영토에 맞춘다 (광장 씬에 하나). 지도는 한 장의 큰 그림이고,
/// 열린 땅 = 처음 광장 + 서쪽 i단계 · 북쪽 j단계까지 (직사각형). 그 바깥 숲이 조금 보이도록 여유를 둔다.
/// 땅을 넓히면 범위가 바로 넓어지고 카메라가 새 땅을 비춘다. 걷기 영역은 칸별 다각형의 DevelopmentGate가 맡는다.
/// </summary>
public class TerritoryMapView : MonoBehaviour
{
    [SerializeField] private PlazaCameraController _camera;

    [Header("지도 (월드)")]
    [Tooltip("지도 그림 전체")]
    [SerializeField] private Rect _map;

    [Tooltip("처음 광장 (넓히기 전 범위)")]
    [SerializeField] private Rect _start;

    [Tooltip("서쪽 단계별 왼쪽 끝 x (0 = 처음 광장의 왼쪽 끝, 1 = 서쪽 1단계의 왼쪽 끝 …)")]
    [SerializeField] private List<float> _westEdges = new List<float>();

    [Tooltip("북쪽 단계별 위쪽 끝 y (0 = 처음 광장의 위쪽 끝, 1 = 북쪽 1단계의 위쪽 끝 …)")]
    [SerializeField] private List<float> _northEdges = new List<float>();

    [Tooltip("열린 땅 밖으로 보이는 숲 (월드 단위)")]
    [Min(0f)]
    [SerializeField] private float _forestMargin = 4.5f;

    private SettlementManager _manager;
    private int _seenVersion = -1;

    /// <summary>지금 카메라 범위 (확인용)</summary>
    public Rect Bounds { get; private set; }

    private void Start()
    {
        _manager = SettlementManager.Instance;
        if (_manager != null)
            _manager.OnTerritoryExpanded += HandleExpanded;
        Apply();
    }

    private void OnDestroy()
    {
        if (_manager != null)
            _manager.OnTerritoryExpanded -= HandleExpanded;
    }

    private void Update()
    {
        if (_manager != null && _manager.IsLoaded && _manager.TerritoryVersion != _seenVersion)
            Apply();
    }

    private void Apply()
    {
        // 정착 매니저가 없는 테스트 씬: 지도 전체
        if (_manager == null)
        {
            SetBounds(_map);
            return;
        }
        _seenVersion = _manager.TerritoryVersion;
        int west = TerritoryRules.ExpandedTier(_manager.Config, _manager.Settlement, TerritoryDirection.West);
        int north = TerritoryRules.ExpandedTier(_manager.Config, _manager.Settlement, TerritoryDirection.North);
        float left = Edge(_westEdges, west, _start.xMin);
        float top = Edge(_northEdges, north, _start.yMax);
        // 더 넓힐 쪽이 남았으면 숲이 조금 보이게
        if (west < _westEdges.Count - 1)
            left -= _forestMargin;
        if (north < _northEdges.Count - 1)
            top += _forestMargin;
        left = Mathf.Max(left, _map.xMin);
        top = Mathf.Min(top, _map.yMax);
        SetBounds(Rect.MinMaxRect(left, _start.yMin, _start.xMax, top));
    }

    private void SetBounds(Rect rect)
    {
        Bounds = rect;
        if (_camera != null)
            _camera.SetBoundsOverride(rect);
    }

    private static float Edge(List<float> edges, int tier, float fallback) =>
        edges.Count == 0 ? fallback : edges[Mathf.Clamp(tier, 0, edges.Count - 1)];

    // 새 땅: 범위를 먼저 넓히고 그 땅 가운데를 비춤
    private void HandleExpanded(TerritoryExpansionDefinition territory)
    {
        Apply();
        if (_camera == null)
            return;
        int tier = territory.Tier;
        Vector2 target = territory.Direction == TerritoryDirection.West
            ? new Vector2((Edge(_westEdges, tier, _start.xMin) + Edge(_westEdges, tier - 1, _start.xMin)) * 0.5f, _start.center.y)
            : new Vector2(_start.center.x, (Edge(_northEdges, tier, _start.yMax) + Edge(_northEdges, tier - 1, _start.yMax)) * 0.5f);
        _camera.PanTo(target);
    }
}
