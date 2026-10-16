using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>농장 부품 (광장 농업 지역에 놓는 건물의 농장 쓰임). 에셋에 숫자로 저장되므로 뒤에만 추가</summary>
public enum FarmPartKind
{
    None,       // 농장 부품 아님
    Field,      // 밭 칸: 작물 하나를 심는 곳
    Crate,      // 수확 상자: 농부가 수확물을 넣고 플레이어가 거둠
    Path,       // 길: 농부가 길 위에서 빨리 걸음
    Sprinkler,  // 스프링클러: 둘레 밭이 빨리 자람
}

/// <summary>밭 흙 한 단계 (보통 흙 → 기름진 흙 → 황금 흙). 마을이 커지면(건물을 지으면) 업그레이드가 열림</summary>
[Serializable]
public class FarmSoilTier
{
    [Tooltip("이름 (예: 기름진 흙)")]
    [SerializeField] private string _name;
    [Tooltip("자라는 시간 배수 (0.8 = 20% 빨리)")]
    [SerializeField, Range(0.1f, 1f)] private float _durationMultiplier = 1f;
    [Tooltip("수확할 때 하나 더 나올 확률")]
    [SerializeField, Range(0f, 1f)] private float _bonusChance;
    [Tooltip("이 흙으로 올리려면 다 지어야 하는 건물 ID (비우면 처음부터)")]
    [SerializeField] private string _requiredBuilding;
    [Tooltip("또는 열린 발전 ID (성처럼 건물 기록이 아닌 것)")]
    [SerializeField] private string _requiredDevelopment;
    [Tooltip("올리는 골드")]
    [SerializeField] private int _gold;
    [Tooltip("올리는 재료")]
    [SerializeField] private List<ItemAmount> _items = new List<ItemAmount>();
    [Tooltip("밭 칸 그림")]
    [SerializeField] private Sprite _sprite;

    public string Name => _name;
    public float DurationMultiplier => Mathf.Clamp(_durationMultiplier, 0.1f, 1f);
    public float BonusChance => _bonusChance;
    public string RequiredBuilding => _requiredBuilding;
    public string RequiredDevelopment => _requiredDevelopment;
    public int Gold => Mathf.Max(0, _gold);
    public IReadOnlyList<ItemAmount> Items => _items;
    public Sprite Sprite => _sprite;
}

/// <summary>건물을 지으면 열리는 농장 기능 하나 (상자 용량 · 길 속도 · 밭 빨리 짓기 · 수레)</summary>
[Serializable]
public class FarmUnlock
{
    [Tooltip("다 지어야 하는 건물 ID")]
    [SerializeField] private string _requiredBuilding;
    [Tooltip("보이는 이름 (예: 농업 창고)")]
    [SerializeField] private string _label;

    public string RequiredBuilding => _requiredBuilding;
    public string Label => _label;
}

/// <summary>
/// 광장 농장 설정 (Docs/농장_설계형_기획.md): 흙 단계, 상자 용량, 길 속도, 스프링클러, 레벨별 밭 칸 수, 수레, 농부.
/// 농장 부품 자체(밭 칸 · 상자 · 길 · 스프링클러)는 BuildingDefinition(_farmPart)으로 짓고 놓는다
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Settlement/Plaza Farm Config", fileName = "PlazaFarmConfig")]
public class PlazaFarmConfig : ScriptableObject
{
    [Header("밭 칸")]
    [Tooltip("흙 단계 (처음 = 0번)")]
    [SerializeField] private List<FarmSoilTier> _soils = new List<FarmSoilTier>();
    [Tooltip("왕국 레벨마다 지을 수 있는 밭 칸 수 (x = 레벨, y = 칸 수). 레벨 순서대로, 마지막 뒤로는 레벨마다 +1")]
    [SerializeField] private List<Vector2Int> _fieldsByLevel = new List<Vector2Int> { new Vector2Int(10, 3), new Vector2Int(11, 5), new Vector2Int(12, 7), new Vector2Int(13, 9) };
    [Tooltip("이 건물을 지으면 밭 칸 짓는 시간이 절반")]
    [SerializeField] private FarmUnlock _fastBuild;

    [Header("수확 상자")]
    [Tooltip("상자 하나에 들어가는 수")]
    [SerializeField] private int _crateCapacity = 20;
    [Tooltip("이 건물을 지으면 상자 용량 두 배")]
    [SerializeField] private FarmUnlock _bigCrate;
    [Tooltip("이 건물을 지으면 수레가 상자를 자동으로 비움")]
    [SerializeField] private FarmUnlock _cart;
    [Tooltip("수레가 상자를 비우는 간격 (초)")]
    [SerializeField] private float _cartSeconds = 60f;

    [Header("길 · 스프링클러")]
    [Tooltip("이 건물을 지으면 흙길을 깔 수 있음")]
    [SerializeField] private FarmUnlock _pathUnlock;
    [Tooltip("이 건물을 지으면 스프링클러를 놓을 수 있음")]
    [SerializeField] private FarmUnlock _sprinklerUnlock;
    [Tooltip("돌길 그림 (돌길이 열리면 흙길 그림을 이것으로)")]
    [SerializeField] private Sprite _stonePathSprite;
    [Tooltip("길 위 농부 걷기 배수")]
    [SerializeField] private float _pathSpeed = 1.6f;
    [Tooltip("이 건물을 지으면 길이 돌길이 되어 더 빨라짐")]
    [SerializeField] private FarmUnlock _stonePath;
    [Tooltip("돌길 걷기 배수")]
    [SerializeField] private float _stonePathSpeed = 2f;
    [Tooltip("스프링클러가 닿는 거리 (격자 칸, 스프링클러 둘레 이만큼 안의 밭 칸)")]
    [SerializeField] private int _sprinklerRange = 3;
    [Tooltip("스프링클러 하나의 자람 속도 + (0.25 = 25% 빨리)")]
    [SerializeField] private float _sprinklerBonus = 0.25f;
    [Tooltip("스프링클러가 겹칠 때 최대")]
    [SerializeField] private float _sprinklerMax = 0.5f;

    [Header("농부")]
    [Tooltip("광장 밭에서 일하는 해달 ID")]
    [SerializeField] private string _farmerOtterId = "otter_farmer";
    [Tooltip("수확 동작 시간 (초)")]
    [SerializeField] private float _harvestSeconds = 1.6f;
    [Tooltip("꺼 둔 동안 농부가 일하는 효율 (걷는 시간을 뺀 몫)")]
    [SerializeField, Range(0.1f, 1f)] private float _offlineEfficiency = 0.6f;

    public IReadOnlyList<FarmSoilTier> Soils => _soils;
    public FarmUnlock FastBuild => _fastBuild;
    public int CrateCapacity => Mathf.Max(1, _crateCapacity);
    public FarmUnlock BigCrate => _bigCrate;
    public FarmUnlock Cart => _cart;
    public float CartSeconds => Mathf.Max(5f, _cartSeconds);
    public FarmUnlock PathUnlock => _pathUnlock;
    public FarmUnlock SprinklerUnlock => _sprinklerUnlock;
    public Sprite StonePathSprite => _stonePathSprite;
    public float PathSpeed => Mathf.Max(1f, _pathSpeed);
    public FarmUnlock StonePath => _stonePath;
    public float StonePathSpeed => Mathf.Max(1f, _stonePathSpeed);
    public int SprinklerRange => Mathf.Max(1, _sprinklerRange);
    public float SprinklerBonus => _sprinklerBonus;
    public float SprinklerMax => _sprinklerMax;
    public string FarmerOtterId => _farmerOtterId;
    public float HarvestSeconds => Mathf.Max(0.2f, _harvestSeconds);
    public float OfflineEfficiency => _offlineEfficiency;

    /// <summary>이 레벨에 지을 수 있는 밭 칸 수 (첫 레벨 전엔 0)</summary>
    public int FieldsAt(int level)
    {
        int count = 0;
        int lastLevel = 0;
        foreach (var step in _fieldsByLevel)
        {
            if (level < step.x)
                return count;
            count = step.y;
            lastLevel = step.x;
        }
        return count + Mathf.Max(0, level - lastLevel);
    }
}
