using UnityEngine;

/// <summary>
/// 광장의 관리 해달 근무 자리 (예: 게시판 옆). 역할을 맡은 해달이 이 자리에 서서 시설을 바라본다.
/// 역할 데이터(ManagementRoleDefinition)의 근무 자리 ID와 같은 ID를 쓴다. 그리기·판단은 SettlementPlazaView가 한다.
/// </summary>
public class ManagementStationView : MonoBehaviour
{
    [Tooltip("근무 자리 ID (예: station_board)")]
    [SerializeField] private string _stationId;

    [Tooltip("해달이 서는 곳 (걷기 영역 안)")]
    [SerializeField] private Transform _standPoint;

    [Tooltip("일하며 바라볼 곳 (게시판)")]
    [SerializeField] private Transform _lookPoint;

    public string StationId => _stationId;
    public Vector2 StandPoint => _standPoint != null ? (Vector2)_standPoint.position : (Vector2)transform.position;
    public Vector2 LookPoint => _lookPoint != null ? (Vector2)_lookPoint.position : StandPoint;
}
