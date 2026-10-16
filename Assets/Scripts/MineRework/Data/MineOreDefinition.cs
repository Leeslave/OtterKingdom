using UnityEngine;

/// <summary>광석 등급 (도감 · 연출 크기). 에셋에 숫자로 저장되므로 뒤에만 추가</summary>
public enum MineOreRarity
{
    Common,     // 흔함
    Uncommon,   // 보통
    Rare,       // 드묾
    Epic,       // 귀함
    Legendary,  // 전설
    Relic,      // 화석 · 유물 (한 번만, 이야기)
}

/// <summary>
/// 광산 개편(테스트 씬)의 광석 하나. 이름 · 그림 · 등급 · 판매가와 바위에 박힌 광맥 그림.
/// Docs/광산_개편_기획.md
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Mine Rework/Ore", fileName = "Ore_")]
public class MineOreDefinition : ScriptableObject
{
    [Header("이름")]
    [Tooltip("세이브 키 (예: ore_shell_copper). 저장 데이터가 생긴 뒤에는 바꾸지 않는다")]
    [SerializeField] private string _oreId;
    [Tooltip("보이는 이름 (예: 조개 구리)")]
    [SerializeField] private string _displayName;
    [Tooltip("도감 설명")]
    [SerializeField, TextArea] private string _description;
    [Tooltip("도감 쓰임 안내 (예: 구리 곡괭이 · 마을회관)")]
    [SerializeField] private string _useText;

    [Header("그림")]
    [Tooltip("아이콘")]
    [SerializeField] private Sprite _icon;
    [Tooltip("이 광석이 박힌 바위 위에 겹칠 광맥 그림 (없으면 그냥 바위)")]
    [SerializeField] private Sprite _veinSprite;
    [Tooltip("파편 · 빛 색")]
    [SerializeField] private Color _color = Color.white;

    [Header("값")]
    [Tooltip("등급")]
    [SerializeField] private MineOreRarity _rarity;
    [Tooltip("하나 판매가 (0 = 팔 수 없음)")]
    [SerializeField] private int _sellPrice;

    public string OreId => _oreId;
    public string DisplayName => _displayName;
    public string Description => _description;
    public string UseText => _useText;
    public Sprite Icon => _icon;
    public Sprite VeinSprite => _veinSprite;
    public Color Color => _color;
    public MineOreRarity Rarity => _rarity;
    public int SellPrice => Mathf.Max(0, _sellPrice);
    public bool CanSell => _sellPrice > 0;
    /// <summary>드묾 이상 (나올 때 크게 축하)</summary>
    public bool IsRare => _rarity >= MineOreRarity.Rare;
}
