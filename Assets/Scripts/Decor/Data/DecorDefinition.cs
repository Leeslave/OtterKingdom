using UnityEngine;

/// <summary>해달이 가지고 놀 때 물건이 보이는 반응</summary>
public enum DecorPlayStyle
{
    Bounce, // 통통 튐 (공)
    Wiggle, // 좌우로 흔들림 (퍼즐)
    Spin,   // 빙글 돎
}

/// <summary>
/// 광장에 놓을 수 있는 물건 하나 (축구공, 퍼즐 등). 가방 아이템(ItemDefinition)에 배치 정보를 덧붙인다.
/// 가방에 있는 개수 = 가진 개수, 그중 광장에 놓지 않은 것만 보관함에 보인다.
/// </summary>
[CreateAssetMenu(fileName = "Decor", menuName = "Game Data/Decor/Decor Definition")]
public class DecorDefinition : ScriptableObject
{
    [Header("아이템")]
    [Tooltip("가방에 들어가는 아이템 (개수·이름·아이콘은 여기서 가져옴)")]
    [SerializeField]
    private ItemDefinition _item;

    [Header("배치")]
    [Tooltip("차지하는 칸 수 (가로 × 세로, 회전 전 기준)")]
    [SerializeField]
    private Vector2Int _footprint = Vector2Int.one;

    [Tooltip("회전할 수 있는지 (정사각형도 그림은 돌아감)")]
    [SerializeField]
    private bool _canRotate = true;

    [Header("놀이")]
    [Tooltip("해달이 가지고 놀 때 물건의 반응")]
    [SerializeField]
    private DecorPlayStyle _playStyle = DecorPlayStyle.Bounce;

    [Tooltip("한 번 놀 때 걸리는 시간 (초, 최소~최대)")]
    [SerializeField]
    private Vector2 _playSeconds = new Vector2(4f, 7f);

    [Tooltip("동시에 가지고 놀 수 있는 해달 수")]
    [Min(1)]
    [SerializeField]
    private int _maxPlayers = 1;

    [Header("시각 요소")]
    [Tooltip("광장에 놓였을 때의 그림. 비우면 아이템 아이콘을 쓴다")]
    [SerializeField]
    private Sprite _worldSprite;

    [Tooltip("방향별 그림 (0°, 90°, 180°, 270° 순, 선택). 비어 있는 방향은 기본 그림을 그만큼 시계 방향으로 돌려서 보여준다")]
    [SerializeField]
    private Sprite[] _rotationSprites = new Sprite[4];

    public ItemDefinition Item => _item;
    public string ItemId => _item != null ? _item.ItemId : null;
    public virtual string DisplayName => _item != null ? _item.DisplayName : name;

    /// <summary>세이브에 남는 종류 ID (장난감 = 아이템 ID, 건물 = 건물 ID)</summary>
    public virtual string SaveId => ItemId;

    /// <summary>건물인지 (가방·보관함이 아니라 건설로 놓이고, 해달이 가지고 놀지 않음)</summary>
    public virtual bool IsBuilding => false;

    public Vector2Int Footprint => new Vector2Int(Mathf.Max(1, _footprint.x), Mathf.Max(1, _footprint.y));
    public bool CanRotate => _canRotate;
    public DecorPlayStyle PlayStyle => _playStyle;
    public int MaxPlayers => Mathf.Max(1, _maxPlayers);
    public float RollPlaySeconds() => Random.Range(Mathf.Min(_playSeconds.x, _playSeconds.y), Mathf.Max(_playSeconds.x, _playSeconds.y));
    public Sprite WorldSprite => _worldSprite != null ? _worldSprite : _item != null ? _item.Icon : null;

    /// <summary>이 방향일 때 보여줄 그림과, 그 그림을 돌릴 각도 (방향별 그림이 있으면 0)</summary>
    public Sprite SpriteFor(DecorRotation rotation, out float zAngle)
    {
        int index = (int)rotation;
        if (_rotationSprites != null && index < _rotationSprites.Length && _rotationSprites[index] != null)
        {
            zAngle = 0f;
            return _rotationSprites[index];
        }

        zAngle = -90f * index; // 시계 방향
        return WorldSprite;
    }

    /// <summary>테스트·설정 도구용: 놓는 모습</summary>
    public void SetupPlacement(Vector2Int footprint, bool canRotate, Sprite worldSprite)
    {
        _footprint = footprint;
        _canRotate = canRotate;
        _worldSprite = worldSprite;
    }

    protected virtual void OnValidate()
    {
        if (_item == null)
            Debug.LogWarning($"[{name}] 아이템이 비어 있습니다.", this);
    }
}
