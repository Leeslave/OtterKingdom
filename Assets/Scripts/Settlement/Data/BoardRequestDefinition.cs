using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>부탁을 끝냈을 때 새로 오는 해달과 그 해달의 상태</summary>
[Serializable]
public class OtterArrival
{
    [SerializeField] private SettlementOtterDefinition _otter;
    [SerializeField] private ResidentState _state;

    public SettlementOtterDefinition Otter => _otter;
    public ResidentState State => _state;

    public OtterArrival() { }

    public OtterArrival(SettlementOtterDefinition otter, ResidentState state)
    {
        _otter = otter;
        _state = state;
    }
}

/// <summary>게시판에서 부탁이 놓이는 곳. 세이브에 저장하지 않지만 에셋에 숫자로 들어가므로 순서를 바꾸지 않는다</summary>
public enum RequestCategory
{
    Main = 0,     // 메인 발전 (늘 보임)
    Resident = 1, // 주민 부탁 (선택. 게시판을 맡은 해달이 생긴 뒤 정해진 수만큼 보임)
}

/// <summary>부탁을 끝내는 행동. 따로 저장하지 않고 어떤 칸을 채웠는지로 정해진다 (한 부탁에 하나만)</summary>
public enum RequestAction
{
    None,             // 아무 행동도 없음 (데이터 실수)
    Construction,     // 건설을 끝냄 (직접 치우는 장소 포함)
    AssignSpecialist, // 전문 해달을 일할 곳에 배치
    AssignRole,       // 관리 역할을 해달에게 맡김
    ResidentTask,     // 주민 작업을 끝냄
    BuildBuilding,    // 자리를 골라 짓는 건물을 다 지음 (꾸미기 모드 건물 탭, P4)
    SettleOtters,     // 해달들이 빈 집에 입주해 주민이 됨 (P4)
}

/// <summary>
/// 게시판 "해달의 부탁" 하나 = 세계를 바꾸는 진행 (집 짓기, 개간, 전문 해달 배치, 관리 역할, 주민 작업). 반복 보상인 퀘스트와는 따로 간다.
/// 조건(앞선 발전, 주민 수)이 되면 나타나고, 건설을 끝내거나 해달을 배치하거나 역할을 맡기거나 주민 작업을 끝내면 완료된다.
/// </summary>
[CreateAssetMenu(fileName = "BoardRequest", menuName = "Game Data/Settlement/Board Request")]
public class BoardRequestDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브에 저장되는 ID (예: req_first_house)")]
    [SerializeField] private string _requestId;

    [Tooltip("게시판 정렬 순서 (작을수록 위)")]
    [SerializeField] private int _order;

    [Tooltip("메인 발전 / 주민 부탁 (주민 부탁은 선택이라 메인 진행의 조건이 되지 않음)")]
    [SerializeField] private RequestCategory _category;

    [Tooltip("이 부탁이 들어온 콘텐츠 버전 (0 = P0·P1, 2 = P2). 정착 진행 전 세이브는 SettlementRules.LegacyContentVersion까지만 완료로 옮김")]
    [SerializeField] private int _contentVersion;

    [Header("내용")]
    [SerializeField] private string _title;

    [TextArea]
    [SerializeField] private string _description;

    [SerializeField] private Sprite _icon;

    [Tooltip("부탁한 해달 (팝업 얼굴 그림)")]
    [SerializeField] private SettlementOtterDefinition _requester;

    [Header("나타나는 조건")]
    [Tooltip("이 발전이 끝나야 나타남 (비우면 처음부터)")]
    [SerializeField] private string _requiredDevelopment;

    [Tooltip("주민이 이만큼 있어야 나타남")]
    [SerializeField] private int _minResidents;

    [Header("할 일")]
    [SerializeField] private ConstructionDefinition _construction;

    [Tooltip("직접 치우러 갈 장소 (설정하면 [가 보기]로 그 장소에 가고, 그곳의 장애물을 다 치우면 완료. 건설은 비용 0·바로 끝남으로)")]
    [SerializeField] private ZoneDefinition _clearZone;

    [Tooltip("배치 부탁: 이 전문 해달을 일할 곳에 배치하면 완료 (광장에서 해달과 대화해 배치). 설정하면 건설은 비움")]
    [SerializeField] private SettlementOtterDefinition _assignSpecialist;

    [Tooltip("역할 부탁: 이 관리 역할을 해달에게 맡기면 완료 (광장에서 해달과 대화). 설정하면 건설은 비움")]
    [SerializeField] private ManagementRoleDefinition _assignRole;

    [Tooltip("주민 작업 부탁: 이 주민 작업이 끝나면 완료 (주민 해달을 보내 시작). 설정하면 건설은 비움")]
    [SerializeField] private SettlementTaskDefinition _completionTask;

    [Tooltip("건물 부탁(P4): 이 건물을 다 지은 수가 _buildingCount 이상이면 완료 (꾸미기 모드에서 자리를 골라 지음). 기록으로 셈 — 먼저 지어 두었으면 열리자마자 끝남")]
    [SerializeField] private BuildingDefinition _building;

    [Min(1)]
    [SerializeField] private int _buildingCount = 1;

    [Tooltip("입주 부탁(P4): 이 해달들이 모두 주민이 되면 완료 (빈 집이 있으면 광장에서 말을 걸어 입주). 기록으로 셈")]
    [SerializeField] private List<SettlementOtterDefinition> _settleTargets = new List<SettlementOtterDefinition>();

    [Tooltip("건물·입주 부탁을 끝내면 열리는 발전 (다음 부탁의 조건)")]
    [SerializeField] private string _resultDevelopment;

    [Header("완료하면")]
    [Tooltip("주민이 되는 해달")]
    [SerializeField] private List<SettlementOtterDefinition> _settles = new List<SettlementOtterDefinition>();

    [Tooltip("새로 찾아오는 해달")]
    [SerializeField] private List<OtterArrival> _arrivals = new List<OtterArrival>();

    [Tooltip("왕국 단계 (-1이면 그대로)")]
    [SerializeField] private int _stageOnComplete = -1;

    [Tooltip("방명록에 남는 기록 (비워도 됨)")]
    [SerializeField] private GuestbookEntryDefinition _completionEntry;

    [Tooltip("완료 팝업 문구 (예: 첫 주민이 정착했어요!)")]
    [SerializeField] private string _completionMessage;

    [Tooltip("끝내면 왕국 레벨이 여기까지 오름 (0이면 그대로). 끝내기 전에는 경험치가 차도 이 레벨 아래에 머묾")]
    [SerializeField] private int _kingdomLevel;

    public string RequestId => _requestId;
    public int Order => _order;
    public RequestCategory Category => _category;
    public int ContentVersion => _contentVersion;
    public string Title => _title;
    public string Description => _description;
    public Sprite Icon => _icon;
    public SettlementOtterDefinition Requester => _requester;
    public string RequiredDevelopment => _requiredDevelopment;
    public int MinResidents => _minResidents;
    public ConstructionDefinition Construction => _construction;
    public ZoneDefinition ClearZone => _clearZone;
    public SettlementOtterDefinition AssignSpecialist => _assignSpecialist;
    public ManagementRoleDefinition AssignRole => _assignRole;
    public SettlementTaskDefinition CompletionTask => _completionTask;
    public BuildingDefinition Building => _building;
    public int BuildingCount => Mathf.Max(1, _buildingCount);
    public IReadOnlyList<SettlementOtterDefinition> SettleTargets => _settleTargets;
    public string ResultDevelopment => _resultDevelopment;
    public IReadOnlyList<SettlementOtterDefinition> Settles => _settles;
    public IReadOnlyList<OtterArrival> Arrivals => _arrivals;
    public int StageOnComplete => _stageOnComplete;
    public GuestbookEntryDefinition CompletionEntry => _completionEntry;
    public string CompletionMessage => _completionMessage;
    public int KingdomLevel => _kingdomLevel;

    /// <summary>이 부탁을 끝내는 행동 (행동 칸이 둘 이상 채워져 있으면 None — IsValidAction으로 확인)</summary>
    public RequestAction Action
    {
        get
        {
            if (ActionCount != 1)
                return RequestAction.None;
            if (_assignSpecialist != null)
                return RequestAction.AssignSpecialist;
            if (_assignRole != null)
                return RequestAction.AssignRole;
            if (_completionTask != null)
                return RequestAction.ResidentTask;
            if (_building != null)
                return RequestAction.BuildBuilding;
            if (HasSettleTargets)
                return RequestAction.SettleOtters;
            return RequestAction.Construction;
        }
    }

    /// <summary>행동 칸(건설·전문 해달 배치·역할·주민 작업)이 정확히 하나만 채워져 있는지</summary>
    public bool IsValidAction => ActionCount == 1;

    /// <summary>기록(지은 건물 · 주민 처지)만 보고 끝나는 부탁인지 (건물 · 입주)</summary>
    public bool CompletesByRecord => _building != null || HasSettleTargets;

    private bool HasSettleTargets => _settleTargets != null && _settleTargets.Exists(o => o != null);

    private int ActionCount =>
        (_construction != null ? 1 : 0) + (_assignSpecialist != null ? 1 : 0) + (_assignRole != null ? 1 : 0) + (_completionTask != null ? 1 : 0)
        + (_building != null ? 1 : 0) + (HasSettleTargets ? 1 : 0);

    /// <summary>테스트·설정 도구용: 건물 · 입주 부탁</summary>
    public void SetupRecordAction(BuildingDefinition building, int buildingCount, IEnumerable<SettlementOtterDefinition> settleTargets,
        string resultDevelopment)
    {
        _building = building;
        _buildingCount = buildingCount;
        _settleTargets = settleTargets != null ? new List<SettlementOtterDefinition>(settleTargets) : new List<SettlementOtterDefinition>();
        _resultDevelopment = resultDevelopment;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_requestId))
            Debug.LogWarning($"[{name}] RequestId가 비어 있습니다.", this);
        if (ActionCount == 0)
            Debug.LogWarning($"[{name}] 건설·배치할 전문 해달·역할·주민 작업·건물·입주가 모두 비어 있습니다.", this);
        else if (ActionCount > 1)
            Debug.LogWarning($"[{name}] 건설·배치할 전문 해달·역할·주민 작업·건물·입주 중 하나만 채워야 합니다.", this);
        if (_assignSpecialist != null && !_assignSpecialist.IsSpecialist)
            Debug.LogWarning($"[{name}] '{_assignSpecialist.name}'에 일할 지역이 없어 배치할 수 없습니다.", this);
    }
}
