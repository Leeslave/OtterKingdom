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

/// <summary>
/// 게시판 "해달의 부탁" 하나 = 세계를 바꾸는 진행 (집 짓기, 개간, 전문 해달 배치). 반복 보상인 퀘스트와는 따로 간다.
/// 조건(앞선 발전, 주민 수)이 되면 나타나고, 건설을 끝내거나 전문 해달을 배치하면 완료된다.
/// </summary>
[CreateAssetMenu(fileName = "BoardRequest", menuName = "Game Data/Settlement/Board Request")]
public class BoardRequestDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브에 저장되는 ID (예: req_first_house)")]
    [SerializeField] private string _requestId;

    [Tooltip("게시판 정렬 순서 (작을수록 위)")]
    [SerializeField] private int _order;

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
    public string Title => _title;
    public string Description => _description;
    public Sprite Icon => _icon;
    public SettlementOtterDefinition Requester => _requester;
    public string RequiredDevelopment => _requiredDevelopment;
    public int MinResidents => _minResidents;
    public ConstructionDefinition Construction => _construction;
    public ZoneDefinition ClearZone => _clearZone;
    public SettlementOtterDefinition AssignSpecialist => _assignSpecialist;
    public IReadOnlyList<SettlementOtterDefinition> Settles => _settles;
    public IReadOnlyList<OtterArrival> Arrivals => _arrivals;
    public int StageOnComplete => _stageOnComplete;
    public GuestbookEntryDefinition CompletionEntry => _completionEntry;
    public string CompletionMessage => _completionMessage;
    public int KingdomLevel => _kingdomLevel;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_requestId))
            Debug.LogWarning($"[{name}] RequestId가 비어 있습니다.", this);
        if (_construction == null && _assignSpecialist == null)
            Debug.LogWarning($"[{name}] 건설과 배치할 전문 해달이 모두 비어 있습니다.", this);
        if (_assignSpecialist != null && !_assignSpecialist.IsSpecialist)
            Debug.LogWarning($"[{name}] '{_assignSpecialist.name}'에 일할 지역이 없어 배치할 수 없습니다.", this);
    }
}
