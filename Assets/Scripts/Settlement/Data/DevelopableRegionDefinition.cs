using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 개간 지역 하나의 진행 규칙: 발견 → 플레이어가 길을 막은 것을 직접 치움 → 주민 해달이 후속 정비(작업) → 운영.
/// 운영되면 그 장소의 기능(광산 채굴 등)과 꾸미기 구역이 열린다.
/// 지도상의 위치·그림·연결선은 넣지 않는다 (지도는 나중에 따로 이 지역을 regionId로 가리킴).
/// 단계는 발전(development)으로 정해지므로 세이브는 SettlementSaveData의 발전·작업에 들어 있다.
/// </summary>
[CreateAssetMenu(fileName = "Region", menuName = "Game Data/Settlement/Developable Region")]
public class DevelopableRegionDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("지역 ID (예: region_mine). 나중에 지도 데이터가 이 ID로 가리킴")]
    [SerializeField] private string _regionId;

    [SerializeField] private string _displayName;

    [Tooltip("이 지역이 있는 장소 (직접 치우는 부탁의 '직접 치우러 갈 장소'와 같음)")]
    [SerializeField] private ZoneDefinition _zone;

    [Header("단계 (발전 ID)")]
    [Tooltip("이 발전이 열리면 지역을 발견함 (예: chair)")]
    [SerializeField] private string _discoverDevelopment;

    [Tooltip("플레이어가 길을 막은 것을 다 치우면 열리는 발전 (예: mine_path_open)")]
    [SerializeField] private string _playerClearDevelopment;

    [Tooltip("운영 = 이 발전이 열림 (예: mine_cleared). 직접 치우는 부탁이 있으면 그 부탁을 끝내서 열림")]
    [SerializeField] private string _operationalDevelopment;

    [Header("후속 정비")]
    [Tooltip("플레이어가 다 치운 뒤 주민 해달이 할 작업 (모두 끝내면 운영)")]
    [SerializeField] private List<SettlementTaskDefinition> _preparationTasks = new List<SettlementTaskDefinition>();

    [Header("꾸미기")]
    [Tooltip("운영되면 열 꾸미기 격자 (비우면 없음)")]
    [SerializeField] private DecorBoardDefinition _decorBoard;

    [Tooltip("운영되면 열 꾸미기 구역")]
    [SerializeField] private DecorRegionDefinition _decorRegion;

    public string RegionId => _regionId;
    public string DisplayName => _displayName;
    public ZoneDefinition Zone => _zone;
    public string DiscoverDevelopment => _discoverDevelopment;
    public string PlayerClearDevelopment => _playerClearDevelopment;
    public string OperationalDevelopment => _operationalDevelopment;
    public IReadOnlyList<SettlementTaskDefinition> PreparationTasks => _preparationTasks;
    public DecorBoardDefinition DecorBoard => _decorBoard;
    public DecorRegionDefinition DecorRegion => _decorRegion;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_regionId))
            Debug.LogWarning($"[{name}] RegionId가 비어 있습니다.", this);
        if (string.IsNullOrWhiteSpace(_playerClearDevelopment) || string.IsNullOrWhiteSpace(_operationalDevelopment))
            Debug.LogWarning($"[{name}] 직접 개척·운영 발전 ID가 비어 있습니다.", this);
    }
}
