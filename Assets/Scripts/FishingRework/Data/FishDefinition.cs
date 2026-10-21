using UnityEngine;

public enum FishRarity { Common, Uncommon, Rare, Epic, Legendary }

/// <summary>
/// 낚시 개편: 물고기 한 종. 사는 곳(거리 · 깊이), 무게, 값, 얼마나 자주 나오는지.
/// 멀고 깊을수록 좋은 물고기, 무거운 물고기는 릴이 튼튼해야 낚임 (Docs/낚시_개편_기획.md)
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Fishing Rework/Fish", fileName = "Fish")]
public class FishDefinition : ScriptableObject
{
    [SerializeField] private string _fishId = "fish";
    [SerializeField] private string _displayName = "물고기";
    [TextArea] [SerializeField] private string _description = "";
    [Tooltip("옆모습, 오른쪽을 봄")]
    [SerializeField] private Sprite _icon;
    [SerializeField] private FishRarity _rarity;
    [Tooltip("본 게임 가방 아이템 (낚으면 이 아이템이 가방에, 도감도 이 아이템으로). 비우면 테스트 씬에서만")]
    [SerializeField] private ItemDefinition _item;
    [Tooltip("그림이 왼쪽을 봄 (본 게임 물고기 아이콘)")]
    [SerializeField] private bool _facesLeft;

    [Header("값 · 무게")]
    [Tooltip("보통 크기일 때 판매가 (작으면 덜, 크면 더)")]
    [Min(1)] [SerializeField] private int _price = 10;
    [Min(0.01f)] [SerializeField] private float _minWeight = 0.2f;
    [Min(0.01f)] [SerializeField] private float _maxWeight = 1f;

    [Header("사는 곳 (m)")]
    [Tooltip("선착장에서 떨어진 거리")]
    [SerializeField] private Vector2 _distance = new Vector2(3f, 20f);
    [Tooltip("수면에서 깊이 (바닥에 사는 물고기는 바닥에서 높이)")]
    [SerializeField] private Vector2 _depth = new Vector2(0.5f, 5f);
    [SerializeField] private bool _nearBottom;

    [Header("나오기 · 헤엄")]
    [Tooltip("같은 자리 물고기들 사이에서 뽑히는 무게 (흔함 100 기준)")]
    [Min(0f)] [SerializeField] private float _spawnWeight = 100f;
    [Min(0.05f)] [SerializeField] private float _swimSpeed = 0.8f;
    [Tooltip("보통 크기일 때 화면 길이 (m)")]
    [Min(0.1f)] [SerializeField] private float _length = 0.6f;

    public string FishId => _fishId;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public FishRarity Rarity => _rarity;
    public ItemDefinition Item => _item;
    public bool FacesLeft => _facesLeft;
    public int Price => _price;
    public float MinWeight => _minWeight;
    public float MaxWeight => Mathf.Max(_minWeight, _maxWeight);
    public float MinDistance => _distance.x;
    public float MaxDistance => Mathf.Max(_distance.x, _distance.y);
    public float MinDepth => _depth.x;
    public float MaxDepth => Mathf.Max(_depth.x, _depth.y);
    public bool NearBottom => _nearBottom;
    public float SpawnWeight => _spawnWeight;
    public float SwimSpeed => _swimSpeed;
    public float Length => _length;

    /// <summary>크기(0 작음 ~ 1 큼) → 무게</summary>
    public float WeightAt(float size) => Mathf.Lerp(MinWeight, MaxWeight, Mathf.Clamp01(size));

    /// <summary>크기 → 판매가 (작으면 70%, 크면 130%)</summary>
    public int PriceAt(float size) => Mathf.Max(1, Mathf.RoundToInt(_price * (0.7f + 0.6f * Mathf.Clamp01(size))));

    /// <summary>크기 → 화면 길이 (작으면 80%, 크면 125%)</summary>
    public float LengthAt(float size) => _length * (0.8f + 0.45f * Mathf.Clamp01(size));

#if UNITY_EDITOR
    public void SetItem(ItemDefinition item, bool facesLeft)
    {
        _item = item;
        _facesLeft = facesLeft;
    }

    public void Set(string id, string name, string description, Sprite icon, FishRarity rarity, int price,
        float minWeight, float maxWeight, Vector2 distance, Vector2 depth, bool nearBottom, float spawnWeight, float swimSpeed, float length)
    {
        _fishId = id;
        _displayName = name;
        _description = description;
        _icon = icon;
        _rarity = rarity;
        _price = price;
        _minWeight = minWeight;
        _maxWeight = maxWeight;
        _distance = distance;
        _depth = depth;
        _nearBottom = nearBottom;
        _spawnWeight = spawnWeight;
        _swimSpeed = swimSpeed;
        _length = length;
    }
#endif
}
