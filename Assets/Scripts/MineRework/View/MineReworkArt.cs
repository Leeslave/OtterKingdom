using UnityEngine;

/// <summary>
/// 광산 개편 테스트 씬의 화면 그림 묶음 (바위 단계 · 효과 · UI 부품). 규칙 데이터(MineReworkConfig)와 따로.
/// 만들기: Tools/Mine Rework/Build Test Scene
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Mine Rework/Art", fileName = "MineReworkArt")]
public class MineReworkArt : ScriptableObject
{
    [Header("바위")]
    [Tooltip("바위 단계 (멀쩡 → 금 → 많이 금)")]
    [SerializeField] private Sprite[] _rockStages = new Sprite[3];
    [Tooltip("깨진 잔해")]
    [SerializeField] private Sprite _rubble;
    [Tooltip("돌 파편")]
    [SerializeField] private Sprite _chip;

    [Header("효과")]
    [SerializeField] private Sprite _glow;
    [SerializeField] private Sprite _spark;
    [SerializeField] private Sprite _ring;
    [SerializeField] private Sprite _mote;
    [SerializeField] private Sprite _vignette;
    [SerializeField] private Sprite _rays;

    [Header("UI 부품")]
    [SerializeField] private Sprite _roundCream;
    [SerializeField] private Sprite _roundFeatured;
    [SerializeField] private Sprite _roundPeach;
    [SerializeField] private Sprite _chipNormal;
    [SerializeField] private Sprite _badge;
    [SerializeField] private Sprite _boxInset;
    [SerializeField] private Sprite _barFill;
    [SerializeField] private Sprite _ribbonPeach;
    [SerializeField] private Sprite _ribbonLav;
    [SerializeField] private Sprite _tagCommon;
    [SerializeField] private Sprite _tagRare;
    [SerializeField] private Sprite _tagEpic;
    [SerializeField] private Sprite _paper;

    [Header("아이콘")]
    [SerializeField] private Sprite _gold;
    [SerializeField] private Sprite _iconWorkbench;
    [SerializeField] private Sprite _iconLayers;
    [SerializeField] private Sprite _iconCollection;

    public Sprite RockStage(int stage) => _rockStages[Mathf.Clamp(stage, 0, _rockStages.Length - 1)];
    public Sprite Rubble => _rubble;
    public Sprite Chip => _chip;
    public Sprite Glow => _glow;
    public Sprite Spark => _spark;
    public Sprite Ring => _ring;
    public Sprite Mote => _mote;
    public Sprite Vignette => _vignette;
    public Sprite Rays => _rays;
    public Sprite RoundCream => _roundCream;
    public Sprite RoundFeatured => _roundFeatured;
    public Sprite RoundPeach => _roundPeach;
    public Sprite ChipNormal => _chipNormal;
    public Sprite Badge => _badge;
    public Sprite BoxInset => _boxInset;
    public Sprite BarFill => _barFill;
    public Sprite RibbonPeach => _ribbonPeach;
    public Sprite RibbonLav => _ribbonLav;
    public Sprite Paper => _paper;
    public Sprite Gold => _gold;
    public Sprite IconWorkbench => _iconWorkbench;
    public Sprite IconLayers => _iconLayers;
    public Sprite IconCollection => _iconCollection;

    /// <summary>등급 태그 바탕</summary>
    public Sprite RarityTag(MineOreRarity rarity) =>
        rarity >= MineOreRarity.Epic ? _tagEpic : rarity >= MineOreRarity.Rare ? _tagRare : _tagCommon;
}
