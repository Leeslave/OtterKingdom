using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 정착 진행에 나오는 해달 한 마리 (첫 해달, 방문 해달, 정착 후보, 건설 해달).
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

    [Header("역할")]
    [Tooltip("건설·개간 작업을 하는 해달")]
    [SerializeField] private bool _isBuilder;

    [Tooltip("처음 왔을 때 방명록에 남는 기록 (비워도 됨)")]
    [SerializeField] private GuestbookEntryDefinition _arrivalEntry;

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
    public bool IsBuilder => _isBuilder;
    public GuestbookEntryDefinition ArrivalEntry => _arrivalEntry;
    public IReadOnlyList<string> Lines => _lines;
    public string IntroLine => _introLine;
    public string IntroDevelopment => _introDevelopment;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_otterId))
            Debug.LogWarning($"[{name}] OtterId가 비어 있습니다.", this);
    }
}
