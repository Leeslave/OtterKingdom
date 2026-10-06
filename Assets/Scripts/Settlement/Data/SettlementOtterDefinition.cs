using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정착 진행에 나오는 해달 한 마리 (첫 해달, 방문 해달, 정착 후보, 건설 해달, 전문 해달, 장난감 방문 해달).
/// 광장의 랜덤 해달과 달리 정해진 캐릭터라 재접속해도 같은 해달이 한 마리만 나온다.
/// 도감 등록 상태와는 별개다 (도감 = 만난 적 있음, 여기 = 왕국에 사는지).
/// </summary>
[CreateAssetMenu(fileName = "SettlementOtter", menuName = "Game Data/Settlement/Otter")]
public class SettlementOtterDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브에 저장되는 ID (예: otter_first)")]
    [SerializeField] private string _otterId;

    [Tooltip("게시판·팝업에 보일 이름")]
    [SerializeField] private string _displayName;

    [Header("모습")]
    [Tooltip("게시판·팝업의 얼굴 그림")]
    [SerializeField] private Sprite _portrait;

    [Tooltip("광장에 나올 해달 프리팹 (OtterWanderAgent). 건설 해달은 비우고 광장 씬의 건설 해달을 씀")]
    [SerializeField] private GameObject _plazaPrefab;

    [Tooltip("광장 해달 그림에 곱할 색 (같은 프리팹을 쓰는 해달 구분용 임시 색. 흰색 = 그대로)")]
    [SerializeField] private Color _plazaTint = Color.white;

    [Header("특성")]
    [Tooltip("이 해달의 특성 (바뀌지 않음). 왕국에 사는 해달의 특성 수가 특성 건물의 해금 조건")]
    [SerializeField] private OtterTrait _trait;

    [Header("장난감 방문")]
    [Tooltip("광장에 놓인 장난감을 보고 찾아오는 해달 (부탁·사업이 부르지 않음)")]
    [SerializeField] private bool _toyVisitor;

    [Tooltip("이 해달을 부르는 장난감의 최소 등급 (ItemRarity.Tier: 0 흔함, 1 레어, 2 에픽)")]
    [Min(0)]
    [SerializeField] private int _visitTier;

    [Tooltip("빈 집이 없을 때 말을 걸면 하는 말 (비우면 기본 말)")]
    [TextArea]
    [SerializeField] private string _homelessLine;

    [Header("역할")]
    [Tooltip("건설·개간 작업을 하는 해달")]
    [SerializeField] private bool _isBuilder;

    [Tooltip("처음 왔을 때 방명록에 남는 기록 (비워도 됨)")]
    [SerializeField] private GuestbookEntryDefinition _arrivalEntry;

    [Header("전문 해달 (광부·농부)")]
    [Tooltip("이 해달이 일할 지역 (비우면 전문 해달이 아님). 광장에서 만나 배치하면 그 지역에서만 일하고 다른 작업에는 가지 않음")]
    [SerializeField] private DevelopableRegionDefinition _workRegion;

    [Tooltip("배치하면 등록되는 도감 항목 (비워도 됨)")]
    [SerializeField] private CollectionEntry _collectionEntry;

    [Tooltip("광장에서 배치 전에 눌렀을 때 하는 말 (예: 광산에서 일하고 싶어요!)")]
    [TextArea]
    [SerializeField] private string _assignLine;

    [Tooltip("배치(파견)가 저장되면 열리는 발전 (예: 농부 → fairy_invited = 요정 방문 예약). 비우면 없음")]
    [SerializeField] private string _assignDevelopment;

    [Header("입주 (P3 새 이웃)")]
    [Tooltip("광장에서 만난 뒤 눌렀을 때 입주를 묻는 말 (예: 저 집에서 살아도 될까요?)")]
    [TextArea]
    [SerializeField] private string _moveInLine;

    [Header("말")]
    [Tooltip("광장에서 눌렀을 때 하는 말 (하나를 골라 말함)")]
    [TextArea]
    [SerializeField] private List<string> _lines = new List<string>();

    [Tooltip("정착 후보일 때 처음 눌렀을 때 하는 말 (예: 저도 여기 살고 싶어요!)")]
    [TextArea]
    [SerializeField] private string _introLine;

    [Tooltip("그 말을 들으면 열리는 발전 (이 해달의 집 부탁이 이걸 조건으로 둠)")]
    [SerializeField] private string _introDevelopment;

    public string OtterId => _otterId;
    public string DisplayName => _displayName;
    public Sprite Portrait => _portrait;
    public GameObject PlazaPrefab => _plazaPrefab;
    public Color PlazaTint => _plazaTint;
    public OtterTrait Trait => _trait;
    public bool IsToyVisitor => _toyVisitor;
    public int VisitTier => _visitTier;
    public string HomelessLine => _homelessLine;
    public bool IsBuilder => _isBuilder;
    public GuestbookEntryDefinition ArrivalEntry => _arrivalEntry;
    public DevelopableRegionDefinition WorkRegion => _workRegion;
    public bool IsSpecialist => _workRegion != null;
    public CollectionEntry CollectionEntry => _collectionEntry;
    public string AssignLine => _assignLine;
    public string AssignDevelopment => _assignDevelopment;
    public string MoveInLine => _moveInLine;
    public IReadOnlyList<string> Lines => _lines;
    public string IntroLine => _introLine;
    public string IntroDevelopment => _introDevelopment;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_otterId))
            Debug.LogWarning($"[{name}] OtterId가 비어 있습니다.", this);
    }
}
