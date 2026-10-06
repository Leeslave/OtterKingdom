using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 꾸미기 격자 하나 (장소마다 하나: 광장, 밭, 낚시터): 크기와 구역. 놓을 수 있는 물건은 모든 격자가 같다 (DecorCatalog).
/// 격자가 월드의 어디에 얼마나 큰 칸으로 놓이는지, 어느 칸이 막혔는지(밭고랑, 낚시 영역, 나무 등)는 씬 쪽이 정한다.
/// </summary>
[CreateAssetMenu(fileName = "DecorBoard", menuName = "Game Data/Decor/Decor Board")]
public class DecorBoardDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("Key로 사용될 ID (세이브에 저장됨, 예: plaza, farm, fishing). 저장 데이터가 생긴 뒤에는 바꾸지 않는다")]
    [SerializeField]
    private string _boardId;

    [Tooltip("이 격자가 있는 장소 (꾸미기 모드를 열 때 지금 장소의 격자를 찾음)")]
    [SerializeField]
    private ZoneDefinition _zone;

    [Header("격자")]
    [Tooltip("칸 수 (가로 × 세로)")]
    [SerializeField]
    private Vector2Int _size = new Vector2Int(10, 10);

    [Tooltip("구역. 어느 구역에도 속하지 않는 칸에는 놓을 수 없다")]
    [SerializeField]
    private List<DecorRegionDefinition> _regions = new List<DecorRegionDefinition>();

    [Tooltip("세이브 좌표의 기준 칸: 세이브에는 (칸 − 이 값)으로 저장한다. 격자를 왼쪽·아래로 넓히면 넓힌 칸 수만큼 올려서 옛 세이브의 자리가 그대로 맞게 한다")]
    [SerializeField]
    private Vector2Int _saveOrigin;

    public string BoardId => _boardId;
    public ZoneDefinition Zone => _zone;
    public Vector2Int Size => new Vector2Int(Mathf.Max(1, _size.x), Mathf.Max(1, _size.y));
    public IReadOnlyList<DecorRegionDefinition> Regions => _regions;
    public Vector2Int SaveOrigin => _saveOrigin;

    /// <summary>테스트·설정 도구용</summary>
    public void Setup(string boardId, Vector2Int size, IEnumerable<DecorRegionDefinition> regions, Vector2Int saveOrigin)
    {
        _boardId = boardId;
        _size = size;
        _regions = regions != null ? new List<DecorRegionDefinition>(regions) : new List<DecorRegionDefinition>();
        _saveOrigin = saveOrigin;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_boardId))
            Debug.LogWarning($"[{name}] BoardId가 비어 있습니다.", this);
    }
}
