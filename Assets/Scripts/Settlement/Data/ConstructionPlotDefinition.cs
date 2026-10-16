using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>발전에 따라 바뀌는 기본 칸 하나 (처음 넓힌 영토 방향마다 새 이웃의 집 자리가 다름)</summary>
[Serializable]
public class PlotAlternateCell
{
    [Tooltip("이 발전이 열려 있으면 (예: territory_home_ne)")]
    [SerializeField] private string _development;
    [SerializeField] private Vector2Int _cell;

    public string Development => _development;
    public Vector2Int Cell => _cell;

    public PlotAlternateCell() { }

    public PlotAlternateCell(string development, Vector2Int cell)
    {
        _development = development;
        _cell = cell;
    }
}

/// <summary>
/// 부탁으로 짓는 광장 건물(첫 집 · 두 번째 집 · 의자 · 벤치 · 가로등 · 공동사업의 식탁 · 비축 상자 · 이웃집 · 환영 소품)의 자리. 꾸미기 격자에 건물처럼 놓인다.
/// 건설 모드에서 플레이어가 자리를 고르면 그 칸에 놓이고, 같은 순간 부탁의 건설이 시작된다 (SettlementManager.TryStartAt).
/// 그림은 광장 씬의 원래 오브젝트(집·공사 현장)가 그대로 그리고, ConstructionPlotAnchor가 그 묶음을 놓인 자리로 옮긴다.
/// 세이브: 꾸미기 세이브에 건설 ID(SaveId)로 남는다. 공사·완료 기록은 정착 세이브(부탁) 그대로
/// </summary>
[CreateAssetMenu(fileName = "Plot", menuName = "Game Data/Settlement/Construction Plot")]
public class ConstructionPlotDefinition : DecorDefinition, IDistrictBuilding
{
    [Header("건설")]
    [Tooltip("이 자리에 짓는 건설 (게시판 부탁의 건설)")]
    [SerializeField] private ConstructionDefinition _construction;

    [Header("씬과 맞추기")]
    [Tooltip("원래 씬 자리의 왼쪽 아래 칸 (세이브 좌표 = 칸 − 격자 기준 칸). 옛 세이브의 다 지은 건물이 이 자리에 놓이고, 처음 자리 고르기도 여기서 시작")]
    [SerializeField] private Vector2Int _defaultCell;

    [Tooltip("발전이 열려 있으면 기본 칸 대신 쓰는 칸 (처음 넓힌 영토 방향의 땅에 놓이는 새 이웃의 집 등, 앞의 것부터 봄). 비우면 없음")]
    [SerializeField] private List<PlotAlternateCell> _alternates = new List<PlotAlternateCell>();

    [Tooltip("공사 현장(오브젝트 발밑)이 차지한 칸 아래 가운데에서 떨어진 거리 (월드). 씬의 원래 자리와 기본 칸에서 계산됨")]
    [SerializeField] private Vector2 _pivotOffset;

    [Tooltip("미리보기 그림 크기 (씬 오브젝트의 크기, 뒤집힌 그림은 x가 음수)")]
    [SerializeField] private Vector2 _worldScale = Vector2.one;

    [Header("구역")]
    [Tooltip("이 건물이 있어야 할 구역 (구역·이사, 예: 집 = 주거). None이면 어디에 놓아도 됨")]
    [SerializeField] private DistrictType _district;

    public ConstructionDefinition Construction => _construction;
    public string ConstructionId => _construction != null ? _construction.ConstructionId : null;
    public override string SaveId => ConstructionId;
    public override string DisplayName => _construction != null ? _construction.DisplayName : name;
    public override bool IsBuilding => true;
    public Vector2Int DefaultCell => _defaultCell;
    public Vector2 PivotOffset => _pivotOffset;
    public Vector2 WorldScale => _worldScale;
    public DistrictType District => _district;

    /// <summary>테스트·설정 도구용: 구역</summary>
    public void SetupDistrict(DistrictType district) => _district = district;

    /// <summary>씬의 원래 자리 칸 (세이브 좌표). 다른 기본 칸의 발전이 열려 있으면 그 칸</summary>
    public Vector2Int DefaultCellFor(Func<string, bool> hasDevelopment)
    {
        if (hasDevelopment != null)
        {
            foreach (var alternate in _alternates)
            {
                if (alternate != null && !string.IsNullOrEmpty(alternate.Development) && hasDevelopment(alternate.Development))
                    return alternate.Cell;
            }
        }
        return _defaultCell;
    }

    public IReadOnlyList<PlotAlternateCell> Alternates => _alternates;

    /// <summary>오브젝트 발밑이 올 자리 (차지한 칸 기준)</summary>
    public Vector2 PivotFor(Rect area) => new Vector2(area.center.x, area.yMin) + _pivotOffset;

    /// <summary>테스트·설정 도구용</summary>
    public void SetupPlot(ConstructionDefinition construction, Vector2Int defaultCell, Vector2 pivotOffset, Vector2 worldScale,
        IEnumerable<PlotAlternateCell> alternates = null)
    {
        _construction = construction;
        _defaultCell = defaultCell;
        _pivotOffset = pivotOffset;
        _worldScale = worldScale;
        _alternates = alternates != null ? new List<PlotAlternateCell>(alternates) : new List<PlotAlternateCell>();
    }

    // 아이템이 없음 (장난감 검사 대신 건설 검사)
    protected override void OnValidate()
    {
        if (_construction == null)
            Debug.LogWarning($"[{name}] 건설이 비어 있습니다.", this);
        if (CanRotate)
            Debug.LogWarning($"[{name}] 건물 자리는 돌리지 않습니다 (회전을 꺼 주세요).", this);
    }
}
