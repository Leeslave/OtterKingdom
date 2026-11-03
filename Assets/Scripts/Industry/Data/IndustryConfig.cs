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

    [Header("수레")]
    [Tooltip("수레가 시설을 한 번 돌아 창고로 옮기는 간격 (초)")]
    [Min(1f)]
    [SerializeField]
    private float _cartSeconds = 30f;

    [Tooltip("빈 수레 (오른쪽으로 감)")]
    [SerializeField]
    private Sprite _emptyCartSprite;

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

    [Header("마을 정비 (C01)")]
    [Tooltip("창고 자재로 만드는 꾸미기 (만들면 가방으로 → 꾸미기에서 놓음)")]
    [SerializeField]
    private List<CraftRecipe> _crafts = new List<CraftRecipe>();

    public IReadOnlyList<ProcessingRecipeDefinition> Recipes => _recipes;
    public int BaseCapacity => _baseCapacity;
    public int WarehouseCapacity => _warehouseCapacity;
    public float CartSeconds => _cartSeconds;
    public Sprite EmptyCartSprite => _emptyCartSprite;
    public Sprite WarehouseSprite => _warehouseSprite;
    public string HouseBuildingId => _houseBuildingId;
    public int HousePlanks => _housePlanks;
    public int HouseGold => _houseGold;
    public IReadOnlyList<CraftRecipe> Crafts => _crafts;

    /// <summary>이 집의 개량 그림 (집마다 번갈아, 없으면 null)</summary>
    public Sprite ImprovedHouseSprite(int instanceId)
    {
        if (_improvedHouseSprites == null || _improvedHouseSprites.Length == 0)
            return null;
        return _improvedHouseSprites[Mathf.Abs(instanceId) % _improvedHouseSprites.Length];
    }

    /// <summary>이 시설의 레시피 (없으면 null)</summary>
    public ProcessingRecipeDefinition RecipeFor(string buildingId)
    {
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

    [Tooltip("드는 골드")]
    [Min(0)]
    [SerializeField]
    private int _gold;

    public ItemDefinition Item => _item;
    public string TechId => _techId;
    public int Planks => _planks;
    public int StoneBlocks => _stoneBlocks;
    public int Gold => _gold;

    public CraftRecipe() { }

#if UNITY_EDITOR
    public CraftRecipe(ItemDefinition item, string techId, int planks, int stoneBlocks, int gold)
    {
        _item = item;
        _techId = techId;
        _planks = planks;
        _stoneBlocks = stoneBlocks;
        _gold = gold;
    }
#endif
}
