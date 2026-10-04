using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 관리 역할 하나 (예: 게시판 관리). 생산 지역을 맡는 전문 해달(광부·농부)과 달리 광장의 시설 옆에서 일한다.
/// 해달이 광장에 찾아와 처음 만난 뒤, 플레이어가 대화로 역할을 맡기면 정해진 근무 자리(stationId)로 간다.
/// 세이브에는 역할 ID · 해달 ID · 근무 자리 ID만 남는다 (SettlementSaveData.roles).
/// </summary>
[CreateAssetMenu(fileName = "Role", menuName = "Game Data/Settlement/Management Role")]
public class ManagementRoleDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브에 저장되는 ID (예: role_board_manager)")]
    [SerializeField] private string _roleId;

    [Tooltip("역할 이름 (예: 게시판 관리)")]
    [SerializeField] private string _displayName;

    [Header("누가 · 어디서")]
    [Tooltip("이 역할을 맡을 해달")]
    [SerializeField] private SettlementOtterDefinition _otter;

    [Tooltip("근무 자리 ID (광장의 ManagementStationView와 같게. 예: station_board)")]
    [SerializeField] private string _stationId;

    [Header("조건과 결과")]
    [Tooltip("이 발전이 열려야 맡길 수 있음 (예: board_upgraded)")]
    [SerializeField] private string _requiredDevelopment;

    [Tooltip("맡기면 열리는 발전 (예: receptionist_assigned)")]
    [SerializeField] private string _resultDevelopment;

    [Header("말")]
    [Tooltip("맡기기 전에 눌렀을 때 하는 말 (예: 게시판 일을 도와도 될까요?)")]
    [TextArea]
    [SerializeField] private string _askLine;

    [Tooltip("맡기기 확인 창 제목 (예: 게시판 관리 맡기기)")]
    [SerializeField] private string _confirmLabel;

    [Tooltip("근무 중에 눌렀을 때 하는 말 (하나를 골라 말함)")]
    [TextArea]
    [SerializeField] private List<string> _workingLines = new List<string>();

    public string RoleId => _roleId;
    public string DisplayName => _displayName;
    public SettlementOtterDefinition Otter => _otter;
    public string StationId => _stationId;
    public string RequiredDevelopment => _requiredDevelopment;
    public string ResultDevelopment => _resultDevelopment;
    public string AskLine => _askLine;
    public string ConfirmLabel => _confirmLabel;
    public IReadOnlyList<string> WorkingLines => _workingLines;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_roleId))
            Debug.LogWarning($"[{name}] RoleId가 비어 있습니다.", this);
        if (_otter == null)
            Debug.LogWarning($"[{name}] 역할을 맡을 해달이 비어 있습니다.", this);
        if (_otter != null && (_otter.IsSpecialist || _otter.IsBuilder))
            Debug.LogWarning($"[{name}] 전문 해달·건설 해달에게는 관리 역할을 맡기지 않습니다.", this);
    }
}
