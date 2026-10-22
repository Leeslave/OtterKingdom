using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 새 뽑기 보상 설정 (Docs/가챠개편_특수해달조각_장신구_기획.md 2장): 재료 꾸러미 · 장신구 · 특수 해달 조각의 확률과 목록.
/// 배너마다 천장 · 픽업 해달만 다르고, 나머지는 이 한 곳에서 같이 씀 (GachaRewardTable을 만듦)
/// </summary>
[CreateAssetMenu(fileName = "GachaRewardConfig", menuName = "Game Data/Gacha/Reward Config")]
public class GachaRewardConfig : ScriptableObject
{
    [Serializable]
    public class MaterialBundle
    {
        [Min(0)] public int wood;
        [Min(0)] public int stone;
        [Min(1)] public int weight = 1;
    }

    [Header("확률 (합 1)")]
    [Range(0f, 1f)] [SerializeField] private float _materialRate = 0.45f;
    [Tooltip("장신구 일반 · 레어 · 에픽")]
    [SerializeField] private float[] _accessoryRates = { 0.20f, 0.07f, 0.01f };
    [Range(0f, 1f)] [SerializeField] private float _rareFragmentRate = 0.22f;
    [Range(0f, 1f)] [SerializeField] private float _epicFragmentRate = 0.05f;

    [Header("재료 꾸러미")]
    [SerializeField] private ItemDefinition _wood;
    [SerializeField] private ItemDefinition _stone;
    [SerializeField] private List<MaterialBundle> _bundles = new List<MaterialBundle>();

    [Header("특수 해달 조각 (한 번에 나오는 수)")]
    [SerializeField] private Vector2Int _rareFragments = new Vector2Int(2, 4);
    [SerializeField] private Vector2Int _epicFragments = new Vector2Int(2, 3);
    [Tooltip("에픽 천장에서 확정으로 주는 에픽 해달 조각")]
    [Min(1)] [SerializeField] private int _pityFragments = 12;

    [Header("별빛 포인트 · 교환소")]
    [Tooltip("픽업 별빛 포인트를 다 모으면 주는 픽업 해달 조각")]
    [Min(1)] [SerializeField] private int _pointsFragments = 20;
    [Tooltip("교환소에서 한 번에 고르는 해달 조각 (값 = 조각 하나 값 × 이 수)")]
    [Min(1)] [SerializeField] private int _exchangeFragments = 5;

    [Header("처음 선물")]
    [Tooltip("뽑기가 처음 열리면 해금해 첫 응원 칸에 넣어 주는 특수 해달 (편성 효과를 바로 보게)")]
    [SerializeField] private SpecialOtterDefinition _starterOtter;

    [Header("그림이 아직 없을 때")]
    [SerializeField] private Sprite _otterFallbackIcon;
    [SerializeField] private Sprite _accessoryFallbackIcon;

    [Header("목록")]
    [SerializeField] private List<SpecialOtterDefinition> _otters = new List<SpecialOtterDefinition>();
    [SerializeField] private List<AccessoryDefinition> _accessories = new List<AccessoryDefinition>();

    public ItemDefinition Wood => _wood;
    public ItemDefinition Stone => _stone;
    public int PointsFragments => Mathf.Max(1, _pointsFragments);
    public SpecialOtterDefinition StarterOtter => _starterOtter;
    public int ExchangeFragments => Mathf.Max(1, _exchangeFragments);
    public Sprite IconOf(SpecialOtterDefinition otter) => otter != null && otter.Portrait != null ? otter.Portrait : _otterFallbackIcon;
    public Sprite IconOf(AccessoryDefinition accessory) => accessory != null && accessory.Icon != null ? accessory.Icon : _accessoryFallbackIcon;
    public IReadOnlyList<SpecialOtterDefinition> Otters => _otters;
    public IReadOnlyList<AccessoryDefinition> Accessories => _accessories;

    public SpecialOtterDefinition FindOtter(string otterId) => _otters.Find(o => o != null && o.OtterId == otterId);
    public AccessoryDefinition FindAccessory(string accessoryId) => _accessories.Find(a => a != null && a.AccessoryId == accessoryId);

    /// <summary>배너 하나의 보상 표 (픽업 해달은 그 배너의 것, 표에 없는 해달이어도 넣음)</summary>
    public GachaRewardTable BuildTable(GachaBannerKind kind, int epicPity, int rarePity, float featuredShare,
        IEnumerable<SpecialOtterDefinition> featured = null)
    {
        var rates = new GachaRewardRates(_materialRate, Rate(0), Rate(1), Rate(2), _rareFragmentRate, _epicFragmentRate);
        var table = new GachaRewardTable(kind, rates, epicPity, rarePity, featuredShare,
            (_rareFragments.x, _rareFragments.y), (_epicFragments.x, _epicFragments.y), _pityFragments);
        foreach (var bundle in _bundles)
        {
            if (bundle != null)
                table.AddBundle(bundle.wood, bundle.stone, bundle.weight);
        }
        foreach (var accessory in _accessories)
        {
            if (accessory != null)
                table.AddAccessory(accessory);
        }
        var featuredSet = new HashSet<SpecialOtterDefinition>();
        if (featured != null)
        {
            foreach (var otter in featured)
            {
                if (otter != null && featuredSet.Add(otter))
                    table.AddOtter(otter, true);
            }
        }
        foreach (var otter in _otters)
        {
            if (otter != null && !featuredSet.Contains(otter))
                table.AddOtter(otter, false);
        }
        return table;
    }

    private float Rate(int tier) => _accessoryRates != null && tier < _accessoryRates.Length ? Mathf.Max(0f, _accessoryRates[tier]) : 0f;

    /// <summary>설정 도구 · 테스트용 (bundles가 null이면 재료 꾸러미는 그대로)</summary>
    public void Setup(ItemDefinition wood, ItemDefinition stone, IEnumerable<MaterialBundle> bundles,
        IEnumerable<SpecialOtterDefinition> otters, IEnumerable<AccessoryDefinition> accessories)
    {
        _wood = wood;
        _stone = stone;
        if (bundles != null)
            _bundles = new List<MaterialBundle>(bundles);
        _otters = new List<SpecialOtterDefinition>(otters);
        _accessories = new List<AccessoryDefinition>(accessories);
    }

    /// <summary>설정 도구 · 테스트용: 그림이 아직 없을 때 쓰는 그림</summary>
    public void SetupFallbackIcons(Sprite otter, Sprite accessory)
    {
        _otterFallbackIcon = otter;
        _accessoryFallbackIcon = accessory;
    }
}
