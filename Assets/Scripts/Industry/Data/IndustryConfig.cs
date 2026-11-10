using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 번영의 시대 산업 · 물류 설정 (Resources/IndustryConfig — 전역 UI 프리팹을 건드리지 않게 불러 씀).
/// 만들기: Tools/Industry/Create Data (Assets/Editor/Industry/IndustrySetup.cs)
/// </summary>
[CreateAssetMenu(fileName = "IndustryConfig", menuName = "Game Data/Industry/Config")]
public class IndustryConfig : ScriptableObject
{
    public const string ResourcePath = "IndustryConfig";

    [Header("가공")]
    [Tooltip("시설별 가공 레시피")]
    [SerializeField]
    private List<ProcessingRecipeDefinition> _recipes = new List<ProcessingRecipeDefinition>();

    [Header("왕국 창고")]
    [Tooltip("공동 창고(L01) 전 임시 보관 칸 (가공품만)")]
    [Min(0)]
    [SerializeField]
    private int _baseCapacity = 60;

    [Tooltip("공동 창고(L01)를 배운 뒤 칸")]
    [Min(0)]
    [SerializeField]
    private int _warehouseCapacity = 200;

    [Tooltip("집산소(L05)를 배운 뒤 칸")]
    [Min(0)]
    [SerializeField]
    private int _depotCapacity = 400;

    [Header("수레")]
    [Tooltip("수레가 시설을 한 번 돌아 창고로 옮기는 간격 (초)")]
    [Min(1f)]
    [SerializeField]
    private float _cartSeconds = 30f;

    [Tooltip("빈 수레 (오른쪽으로 감)")]
    [SerializeField]
    private Sprite _emptyCartSprite;

    [Tooltip("개량 수레(L06)에 다는 깃발 (수레 뒤 손잡이 쪽 위)")]
    [SerializeField]
    private Sprite _cartPennant;

    [Tooltip("광장의 왕국 창고 자리 (상자 더미 + 표지). 농업 창고 옆, 없으면 게시판 옆")]
    [SerializeField]
    private Sprite _warehouseSprite;

    [Header("주택 개량 (C02)")]
    [Tooltip("개량할 수 있는 집 (건물 ID)")]
    [SerializeField]
    private string _houseBuildingId = "bld_small_house";

    [Tooltip("개량 주택 그림 (집마다 번갈아 씀)")]
    [SerializeField]
    private Sprite[] _improvedHouseSprites = new Sprite[0];

    [Tooltip("한 채 개량에 드는 판재 (왕국 창고에서)")]
    [Min(0)]
    [SerializeField]
    private int _housePlanks = 6;

    [Tooltip("한 채 개량에 드는 골드")]
    [Min(0)]
    [SerializeField]
    private int _houseGold = 800;

    [Header("주택 개량 2단계 · 마을 주택 (C05)")]
    [Tooltip("마을 주택 미리보기 (개량 주택 그림과 같은 순서)")]
    [SerializeField]
    private Sprite[] _townHouseIcons = new Sprite[0];

    [Tooltip("현관 왼쪽 등불")]
    [SerializeField]
    private Sprite _porchLamp;

    [Tooltip("현관 오른쪽 화분")]
    [SerializeField]
    private Sprite _porchFlowers;

    [Tooltip("한 채에 드는 석재 블록")]
    [Min(0)]
    [SerializeField]
    private int _townHouseStoneBlocks = 4;

    [Tooltip("한 채에 드는 금속 주괴")]
    [Min(0)]
    [SerializeField]
    private int _townHouseIngots = 2;

    [Tooltip("한 채에 드는 골드")]
    [Min(0)]
    [SerializeField]
    private int _townHouseGold = 2000;

    [Header("마을 정비 (C01)")]
    [Tooltip("창고 자재로 만드는 꾸미기 (만들면 가방으로 → 꾸미기에서 놓음)")]
    [SerializeField]
    private List<CraftRecipe> _crafts = new List<CraftRecipe>();

    [Tooltip("광장 바닥 공사 (흙 마당 → 돌길). 한 번 하면 광장 지도 위에 돌길 그림이 깔림")]
    [SerializeField]
    private List<PavingProject> _pavings = new List<PavingProject>();

    public IReadOnlyList<ProcessingRecipeDefinition> Recipes => _recipes;
    public int BaseCapacity => _baseCapacity;
    public int WarehouseCapacity => _warehouseCapacity;
    public int DepotCapacity => _depotCapacity;
    public float CartSeconds => _cartSeconds;
    public Sprite EmptyCartSprite => _emptyCartSprite;
    public Sprite CartPennant => _cartPennant;
    public Sprite WarehouseSprite => _warehouseSprite;
    public string HouseBuildingId => _houseBuildingId;
    public int HousePlanks => _housePlanks;
    public int HouseGold => _houseGold;
    public Sprite PorchLamp => _porchLamp;
    public Sprite PorchFlowers => _porchFlowers;
    public int TownHouseStoneBlocks => _townHouseStoneBlocks;
    public int TownHouseIngots => _townHouseIngots;
    public int TownHouseGold => _townHouseGold;

    /// <summary>이 집의 마을 주택 미리보기 (개량 주택 그림과 같은 번갈아 규칙)</summary>
    public Sprite TownHouseIcon(int instanceId)
    {
        if (_townHouseIcons == null || _townHouseIcons.Length == 0)
            return ImprovedHouseSprite(instanceId);
        return _townHouseIcons[Mathf.Abs(instanceId) % _townHouseIcons.Length];
    }
    public IReadOnlyList<CraftRecipe> Crafts => _crafts;
    public IReadOnlyList<PavingProject> Pavings => _pavings;

    /// <summary>이 집의 개량 그림 (집마다 번갈아, 없으면 null)</summary>
    public Sprite ImprovedHouseSprite(int instanceId)
    {
        if (_improvedHouseSprites == null || _improvedHouseSprites.Length == 0)
            return null;
        return _improvedHouseSprites[Mathf.Abs(instanceId) % _improvedHouseSprites.Length];
    }

    /// <summary>이 시설의 첫 레시피 (시설이 모은 원재료를 받는 줄, 없으면 null)</summary>
    public ProcessingRecipeDefinition RecipeFor(string buildingId)
    {
        foreach (var recipe in _recipes)
        {
            if (recipe != null && recipe.IsValid && recipe.Building.BuildingId == buildingId && !recipe.FromWarehouse)
                return recipe;
        }
        foreach (var recipe in _recipes)
        {
            if (recipe != null && recipe.IsValid && recipe.Building.BuildingId == buildingId)
                return recipe;
        }
        return null;
    }

    /// <summary>창고 자재 아이템 (레시피의 가공품들)</summary>
    public ItemDefinition FindOutput(string itemId)
    {
        foreach (var recipe in _recipes)
        {
            if (recipe != null && recipe.IsValid && recipe.Output.ItemId == itemId)
                return recipe.Output;
        }
        return null;
    }

#if UNITY_EDITOR
    public void EditorSet(List<ProcessingRecipeDefinition> recipes) => _recipes = recipes;
#endif
}

/// <summary>광장 바닥 공사 그림 한 조각: 지도 비율 자리 (큰 공사는 4096 넘지 않게 여러 조각)</summary>
[System.Serializable]
public class PavingPiece
{
    [Tooltip("광장 지도 위에 까는 돌길 그림")]
    [SerializeField]
    private Sprite _sprite;

    [Tooltip("그림 가운데 (지도 비율, 왼쪽 위 0,0 ~ 오른쪽 아래 1,1)")]
    [SerializeField]
    private Vector2 _mapCenter;

    [Tooltip("그림 크기 (지도 비율)")]
    [SerializeField]
    private Vector2 _mapSize;

    public Sprite Sprite => _sprite;
    public Vector2 MapCenter => _mapCenter;
    public Vector2 MapSize => _mapSize;
}

/// <summary>
/// 광장 바닥 공사 하나 (마을 정비: 가운데 흙 마당 C01 · 큰길 C03). 한 번 하면 발전 기록(Id)이 남고,
/// 광장 지도(WorldMap) 위 지도 비율 자리에 돌길 그림 조각들이 깔림 (그림 · 자리는 Tools/UIGen/plaza_paving.py)
/// </summary>
[System.Serializable]
public class PavingProject
{
    [Tooltip("발전 기록 ID (깔면 남음, 예: town_paving_plaza_center)")]
    [SerializeField]
    private string _id;

    [SerializeField]
    private string _displayName;

    [TextArea]
    [SerializeField]
    private string _description;

    [Tooltip("이 기술을 배워야 깔 수 있음 (TechIds)")]
    [SerializeField]
    private string _techId;

    [Tooltip("드는 석재 블록 (창고)")]
    [Min(0)]
    [SerializeField]
    private int _stoneBlocks;

    [Tooltip("드는 판재 (창고)")]
    [Min(0)]
    [SerializeField]
    private int _planks;

    [Tooltip("드는 골드")]
    [Min(0)]
    [SerializeField]
    private int _gold;

    [Tooltip("먼저 깔아야 하는 공사 ID (없으면 비움)")]
    [SerializeField]
    private string _requires;

    [Tooltip("창 카드 아이콘")]
    [SerializeField]
    private Sprite _icon;

    [Tooltip("광장 지도 위에 까는 그림 조각들")]
    [SerializeField]
    private List<PavingPiece> _pieces = new List<PavingPiece>();

    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public string TechId => _techId;
    public int StoneBlocks => _stoneBlocks;
    public int Planks => _planks;
    public int Gold => _gold;
    public string Requires => _requires;
    public Sprite Icon => _icon;
    public IReadOnlyList<PavingPiece> Pieces => _pieces;
}

/// <summary>창고 자재로 만드는 꾸미기 하나 (마을 정비 C01: 왕국 표지판 · 가로등)</summary>
[System.Serializable]
public class CraftRecipe
{
    [Tooltip("만들어 가방에 넣는 꾸미기 아이템")]
    [SerializeField]
    private ItemDefinition _item;

    [Tooltip("이 기술을 배워야 만들 수 있음 (TechIds)")]
    [SerializeField]
    private string _techId;

    [Tooltip("드는 판재 (창고)")]
    [Min(0)]
    [SerializeField]
    private int _planks;

    [Tooltip("드는 석재 블록 (창고)")]
    [Min(0)]
    [SerializeField]
    private int _stoneBlocks;

    [Tooltip("드는 금속 주괴 (창고)")]
    [Min(0)]
    [SerializeField]
    private int _ingots;

    [Tooltip("드는 골드")]
    [Min(0)]
    [SerializeField]
    private int _gold;

    public ItemDefinition Item => _item;
    public string TechId => _techId;
    public int Planks => _planks;
    public int StoneBlocks => _stoneBlocks;
    public int Ingots => _ingots;
    public int Gold => _gold;

    public CraftRecipe() { }

#if UNITY_EDITOR
    public CraftRecipe(ItemDefinition item, string techId, int planks, int stoneBlocks, int gold, int ingots = 0)
    {
        _item = item;
        _techId = techId;
        _planks = planks;
        _stoneBlocks = stoneBlocks;
        _ingots = ingots;
        _gold = gold;
    }
#endif
}
