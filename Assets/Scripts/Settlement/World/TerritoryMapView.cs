using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광장 카메라가 갈 수 있는 범위를 영토에 맞춘다 (광장 씬에 하나). 지도는 한 장의 큰 그림이고,
/// 열린 땅 = 중앙 공터 + 넓힌 단계(방향마다 구역 블록이 커지는 땅) + 바닷가(낚시터를 찾으면). 그 바깥 숲이 조금 보이도록 여유를 둔다.
/// 땅을 넓히면 범위가 바로 넓어지고 카메라가 새 땅을 비춘다. 걷기 영역은 땅마다 다각형의 DevelopmentGate가 맡는다.
/// </summary>
public class TerritoryMapView : MonoBehaviour
{
    /// <summary>발전이 열리면 카메라 범위에 들어오는 땅 하나</summary>
    [Serializable]
    public class Region
    {
        [Tooltip("이 땅이 열리는 발전 (영토 확장 단계 ID, 바닷가는 fishing_found)")]
        public string development;

        [Tooltip("땅 (월드)")]
        public Rect rect;
    }

    [SerializeField] private PlazaCameraController _camera;

    [Header("지도 (월드)")]
    [Tooltip("지도 그림 전체")]
    [SerializeField] private Rect _map;

    [Tooltip("처음 땅 (중앙 공터)")]
    [SerializeField] private Rect _start;

    [Tooltip("넓히면 열리는 땅들")]
    [SerializeField] private List<Region> _regions = new List<Region>();

    [Tooltip("열린 땅 밖으로 보이는 숲 (월드 단위)")]
    [Min(0f)]
    [SerializeField] private float _forestMargin = 4.5f;

    private SettlementManager _manager;
    private int _seenVersion = -1;

    /// <summary>지금 카메라 범위 (확인용)</summary>
    public Rect Bounds { get; private set; }

    public Rect StartRect => _start;

    private void Start()
    {
        if (GetComponent<DistrictMapView>() == null) gameObject.AddComponent<DistrictMapView>();
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

    /// <summary>열린 땅을 모두 감싸는 범위 + 숲 여유 (지도 밖으로는 안 나감)</summary>
    public static Rect ComputeBounds(Rect map, Rect start, IReadOnlyList<Region> regions, Func<string, bool> has, float margin)
    {
        float xMin = start.xMin, yMin = start.yMin, xMax = start.xMax, yMax = start.yMax;
        foreach (var region in regions)
        {
            if (region == null || string.IsNullOrEmpty(region.development) || has == null || !has(region.development))
                continue;
            xMin = Mathf.Min(xMin, region.rect.xMin);
            yMin = Mathf.Min(yMin, region.rect.yMin);
            xMax = Mathf.Max(xMax, region.rect.xMax);
            yMax = Mathf.Max(yMax, region.rect.yMax);
        }
        return Rect.MinMaxRect(Mathf.Max(map.xMin, xMin - margin), Mathf.Max(map.yMin, yMin - margin),
            Mathf.Min(map.xMax, xMax + margin), Mathf.Min(map.yMax, yMax + margin));
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
        SetBounds(ComputeBounds(_map, _start, _regions, _manager.Settlement.HasDevelopment, _forestMargin));
    }

    private void SetBounds(Rect rect)
    {
        Bounds = rect;
        if (_camera != null)
            _camera.SetBoundsOverride(rect);
    }

    // 새 땅: 범위를 먼저 넓히고 그 땅 가운데를 비춤
    private void HandleExpanded(TerritoryExpansionDefinition territory)
    {
        Apply();
        if (_camera == null || territory == null)
            return;
        foreach (var region in _regions)
        {
            if (region != null && region.development == territory.ExpandedDevelopment)
            {
                _camera.PanTo(region.rect.center);
                return;
            }
        }
    }
}
