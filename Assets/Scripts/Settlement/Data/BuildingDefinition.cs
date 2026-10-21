using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>무엇을 하는 건물인지</summary>
public enum BuildingKind
{
    Home,     // 집: 다 지으면 빈 집 하나 (장난감 해달이 입주)
    Facility, // 시설 (특성 건물 등): 다 지으면 왕국 보유 효과
}

/// <summary>다 지은 시설이 주는 왕국 보유 효과 하나 (예: 판매가 +10%)</summary>
[Serializable]
public class BuildingBonus
{
    [SerializeField] private KingdomBonusKind _kind;
    [SerializeField] private int _percent;

    public KingdomBonusKind Kind => _kind;
    public int Percent => _percent;

    public BuildingBonus() { }

    public BuildingBonus(KingdomBonusKind kind, int percent)
    {
        _kind = kind;
        _percent = percent;
    }
}

/// <summary>건물 해금에 필요한 특성 하나 (예: 나무캐기 2명)</summary>
[Serializable]
public class TraitRequirement
{
    [SerializeField] private OtterTrait _trait;
    [Min(1)]
    [SerializeField] private int _count = 1;

    public OtterTrait Trait => _trait;
    public int Count => Mathf.Max(1, _count);

    public TraitRequirement() { }

    public TraitRequirement(OtterTrait trait, int count)
    {
        _trait = trait;
        _count = count;
    }
}

/// <summary>
/// 플레이어가 자리를 골라 짓는 건물 (P4: 작은 집, 특성 건물). 꾸미기 격자에 장난감과 같은 규칙으로 놓이고(칸 크기·겹침·옮기기),
/// 놓는 순간 비용을 내고 공사가 시작된다. 보관함·가방에는 들어가지 않고 치울 수 없다 (옮기기만).
/// 놓인 자리는 꾸미기 세이브, 공사·완성 기록은 정착 세이브(SettlementSaveData.buildings)가 원본이다
/// </summary>
[CreateAssetMenu(fileName = "Building", menuName = "Game Data/Settlement/Building")]
public class BuildingDefinition : DecorDefinition, IDistrictBuilding
{
    [Header("건물")]
    [Tooltip("세이브에 저장되는 ID (예: bld_small_house). 저장 데이터가 생긴 뒤에는 바꾸지 않는다")]
    [SerializeField] private string _buildingId;

    [SerializeField] private string _buildingName;

    [TextArea]
    [SerializeField] private string _description;

    [Tooltip("건물 목록 칸의 그림 (비우면 완성 그림)")]
    [SerializeField] private Sprite _icon;

    [SerializeField] private BuildingKind _kind;

    [Header("해금")]
    [Tooltip("이 왕국 레벨부터 (0 = 조건 없음)")]
    [Min(0)]
    [SerializeField] private int _requiredLevel;

    [Tooltip("이 발전이 열려 있어야 함 (비우면 조건 없음, 예: 마을회관)")]
    [SerializeField] private string _requiredDevelopment;

    [Tooltip("왕국에 사는 해달의 특성 수 (모두 채워야 함)")]
    [SerializeField] private List<TraitRequirement> _traits = new List<TraitRequirement>();

    [Tooltip("지을 수 있는 최대 수 (0 = 제한 없음)")]
    [Min(0)]
    [SerializeField] private int _maxCount;

    [Header("비용")]
    [Min(0)]
    [SerializeField] private int _gold;

    [SerializeField] private List<ItemAmount> _items = new List<ItemAmount>();

    [Tooltip("이미 지은(짓는 중 포함) 같은 건물 하나마다 비용이 늘어나는 비율 (0.3 = 30%씩)")]
    [Min(0f)]
    [SerializeField] private float _costGrowth;

    [Header("공사")]
    [Tooltip("걸리는 시간 (초)")]
    [Min(1f)]
    [SerializeField] private float _buildSeconds = 60f;

    [Tooltip("공사 단계 그림 (터 → 골조 → 벽 → 마무리). 완성 그림과 같은 피벗(발밑)으로 그림")]
    [SerializeField] private List<Sprite> _stageSprites = new List<Sprite>();

    [Header("효과 (시설)")]
    [Tooltip("다 지으면 왕국에 주는 영구 효과 (같은 건물을 여러 채 지어도 한 번만)")]
    [SerializeField] private List<BuildingBonus> _bonuses = new List<BuildingBonus>();

    [Tooltip("다 지으면 요정 상점이 이 건물 앞으로 이사함")]
    [SerializeField] private bool _hostsFairyShop;

    [Header("생산 (시설)")]
    [Tooltip("다 지으면 이 특성의 해달들이 일해 꾸준히 모아 가방에 넣는 재료 (예: 목공소 = 목재). 비우면 생산 없음")]
    [SerializeField] private ItemDefinition _productionItem;
    [Tooltip("일하는 해달 한 마리가 1시간에 모으는 개수")]
    [Min(0)]
    [SerializeField] private int _productionPerHour;
    [Tooltip("여기서 일하는 해달의 특성 (마을에 사는 그 특성 해달 수만큼 일함)")]
    [SerializeField] private OtterTrait _productionTrait;

    [Header("구역")]
    [Tooltip("이 건물이 있어야 할 구역 (구역·이사). None이면 어디에 놓아도 됨")]
    [SerializeField] private DistrictType _district;

    [Header("농장")]
    [Tooltip("광장 농장 부품이면 그 쓰임 (밭 칸 · 상자 · 길 · 스프링클러). Docs/농장_설계형_기획.md")]
    [SerializeField] private FarmPartKind _farmPart;
    [Tooltip("공사 없이 바로 놓임 (길처럼 많이 까는 것). 다른 공사와 상관없이 놓을 수 있음")]
    [SerializeField] private bool _instant;

    [Header("그림")]
    [Tooltip("완성 그림의 가로가 차지한 칸 가로의 몇 배인지 (지붕 처마가 칸보다 조금 넓어도 됨)")]
    [Range(0.5f, 1.5f)]
    [SerializeField] private float _widthFill = 1f;

    public string BuildingId => _buildingId;
    public override string DisplayName => string.IsNullOrEmpty(_buildingName) ? name : _buildingName;
    public override string SaveId => _buildingId;
    public override bool IsBuilding => true;
    // 농장 부품(밭 · 상자 · 길 · 스프링클러)은 문이 없고, 길은 문 앞에 깔 수 있음
    public override bool KeepsDoorstepClear => _farmPart == FarmPartKind.None;
    public override bool AllowedOnDoorstep => _farmPart == FarmPartKind.Path;
    public string Description => _description;
    public Sprite Icon => _icon != null ? _icon : WorldSprite;
    public BuildingKind Kind => _kind;
    public int RequiredLevel => _requiredLevel;
    public string RequiredDevelopment => _requiredDevelopment;
    public IReadOnlyList<TraitRequirement> Traits => _traits;
    public int MaxCount => _maxCount;
    public int Gold => _gold;
    public IReadOnlyList<ItemAmount> Items => _items;
    public float CostGrowth => _costGrowth;
    public float BuildSeconds => Mathf.Max(1f, _buildSeconds);
    public IReadOnlyList<Sprite> StageSprites => _stageSprites;
    public float WidthFill => _widthFill;
    public IReadOnlyList<BuildingBonus> Bonuses => _bonuses;
    public bool HostsFairyShop => _hostsFairyShop;
    public ItemDefinition ProductionItem => _productionItem;
    public int ProductionPerHour => Mathf.Max(0, _productionPerHour);
    public OtterTrait ProductionTrait => _productionTrait;
    /// <summary>다 지으면 해달들이 일해 재료를 모으는 시설인지</summary>
    public bool Produces => _productionItem != null && _productionPerHour > 0 && _productionTrait != OtterTrait.None;
    public DistrictType District => _district;
    public FarmPartKind FarmPart => _farmPart;
    public bool IsFarmPart => _farmPart != FarmPartKind.None;
    public bool Instant => _instant;

    /// <summary>테스트·설정 도구용: 구역</summary>
    public void SetupDistrict(DistrictType district) => _district = district;

    /// <summary>테스트·설정 도구용: 효과</summary>
    public void SetupEffects(IEnumerable<BuildingBonus> bonuses, bool hostsFairyShop)
    {
        _bonuses = bonuses != null ? new List<BuildingBonus>(bonuses) : new List<BuildingBonus>();
        _hostsFairyShop = hostsFairyShop;
    }

    /// <summary>테스트·설정 도구용: 생산</summary>
    public void SetupProduction(ItemDefinition item, int perHour, OtterTrait trait)
    {
        _productionItem = item;
        _productionPerHour = perHour;
        _productionTrait = trait;
    }

    /// <summary>테스트·설정 도구용</summary>
    public void Setup(string buildingId, string buildingName, BuildingKind kind, int requiredLevel, string requiredDevelopment,
        IEnumerable<TraitRequirement> traits, int maxCount, int gold, IEnumerable<ItemAmount> items, float costGrowth, float buildSeconds)
    {
        _buildingId = buildingId;
        _buildingName = buildingName;
        _kind = kind;
        _requiredLevel = requiredLevel;
        _requiredDevelopment = requiredDevelopment;
        _traits = traits != null ? new List<TraitRequirement>(traits) : new List<TraitRequirement>();
        _maxCount = maxCount;
        _gold = gold;
        _items = items != null ? new List<ItemAmount>(items) : new List<ItemAmount>();
        _costGrowth = costGrowth;
        _buildSeconds = buildSeconds;
    }

    // 건물은 아이템이 없음 (장난감 검사 대신 건물 ID 검사)
    protected override void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_buildingId))
            Debug.LogWarning($"[{name}] BuildingId가 비어 있습니다.", this);
        if (CanRotate)
            Debug.LogWarning($"[{name}] 건물은 돌리지 않습니다 (회전을 꺼 주세요).", this);
    }
}
