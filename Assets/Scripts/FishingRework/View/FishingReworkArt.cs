using UnityEngine;

/// <summary>
/// 낚시 개편 테스트 씬의 그림 묶음 (바다 · 선착장 · 바늘 · 강화 아이콘). UI 부품 · 효과는 광산 개편 것을 같이 씀(Ui).
/// 만들기: Tools/Fishing Rework/Build Test Scene
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Fishing Rework/Art", fileName = "FishingReworkArt")]
public class FishingReworkArt : ScriptableObject
{
    [Tooltip("UI 부품 · 효과 (광산 개편과 같은 스티커 부품)")]
    [SerializeField] private MineReworkArt _ui;

    [Header("바다")]
    [SerializeField] private Sprite _sky;
    [SerializeField] private Sprite _water;
    [SerializeField] private Sprite _wave;
    [SerializeField] private Sprite _ray;
    [SerializeField] private Sprite _island;
    [SerializeField] private Sprite _cloud;
    [SerializeField] private Sprite _pier;
    [SerializeField] private Sprite _buoy;
    [SerializeField] private Sprite[] _seaweed = new Sprite[0];
    [SerializeField] private Sprite[] _coral = new Sprite[0];
    [SerializeField] private Sprite[] _rocks = new Sprite[0];

    [Header("낚시 도구")]
    [SerializeField] private Sprite _hook;
    [SerializeField] private Sprite _sinker;

    [Header("아이콘")]
    [SerializeField] private Sprite _iconUpgrade;
    [SerializeField] private Sprite _iconBook;
    [SerializeField] private Sprite _iconAnyFish;
    [SerializeField] private Sprite _iconBig;
    [SerializeField] private Sprite _iconBag;
    [Tooltip("본 게임 [나가기] (선착장으로)")]
    [SerializeField] private Sprite _iconPier;

    public MineReworkArt Ui => _ui;
    public Sprite Sky => _sky;
    public Sprite Water => _water;
    public Sprite Wave => _wave;
    public Sprite Ray => _ray;
    public Sprite Island => _island;
    public Sprite Cloud => _cloud;
    public Sprite Pier => _pier;
    public Sprite Buoy => _buoy;
    public Sprite[] Seaweed => _seaweed;
    public Sprite[] Coral => _coral;
    public Sprite[] Rocks => _rocks;
    public Sprite Hook => _hook;
    public Sprite Sinker => _sinker;
    public Sprite IconUpgrade => _iconUpgrade;
    public Sprite IconBook => _iconBook;
    public Sprite IconAnyFish => _iconAnyFish;
    public Sprite IconBig => _iconBig;
    public Sprite IconBag => _iconBag;
    public Sprite IconPier => _iconPier != null ? _iconPier : _iconUpgrade;

    public static Color RarityColor(FishRarity rarity)
    {
        switch (rarity)
        {
            case FishRarity.Uncommon: return new Color(0.45f, 0.78f, 0.55f);
            case FishRarity.Rare: return new Color(0.4f, 0.65f, 1f);
            case FishRarity.Epic: return new Color(0.78f, 0.5f, 1f);
            case FishRarity.Legendary: return new Color(1f, 0.78f, 0.25f);
            default: return new Color(0.75f, 0.72f, 0.68f);
        }
    }

    public static string RarityName(FishRarity rarity)
    {
        switch (rarity)
        {
            case FishRarity.Uncommon: return "보통";
            case FishRarity.Rare: return "희귀";
            case FishRarity.Epic: return "귀함";
            case FishRarity.Legendary: return "전설";
            default: return "흔함";
        }
    }

#if UNITY_EDITOR
    public void SetIconPier(Sprite icon) => _iconPier = icon;

    public void Set(MineReworkArt ui, Sprite sky, Sprite water, Sprite wave, Sprite ray, Sprite island, Sprite cloud, Sprite pier,
        Sprite buoy, Sprite[] seaweed, Sprite[] coral, Sprite[] rocks, Sprite hook, Sprite sinker,
        Sprite iconUpgrade, Sprite iconBook, Sprite iconAnyFish, Sprite iconBig, Sprite iconBag)
    {
        _ui = ui;
        _sky = sky;
        _water = water;
        _wave = wave;
        _ray = ray;
        _island = island;
        _cloud = cloud;
        _pier = pier;
        _buoy = buoy;
        _seaweed = seaweed;
        _coral = coral;
        _rocks = rocks;
        _hook = hook;
        _sinker = sinker;
        _iconUpgrade = iconUpgrade;
        _iconBook = iconBook;
        _iconAnyFish = iconAnyFish;
        _iconBig = iconBig;
        _iconBag = iconBag;
    }
#endif
}
