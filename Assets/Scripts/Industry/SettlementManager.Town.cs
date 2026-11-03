using System.Collections.Generic;

/// <summary>
/// 번영의 시대 도시 (개발자 A): 주택 개량 1단계(C02) · 마을 정비 만들기(C01).
/// 개량은 같은 집 · 같은 입주민 그대로 그림만 개량 주택 (기획서 9.3). 자재는 왕국 창고 판재 · 석재 블록 + 골드
/// </summary>
public partial class SettlementManager
{
    public const string PlankItemId = "plank";
    public const string StoneBlockItemId = "stone_block";
    public const string FirstImprovedHouseDevelopment = "first_improved_house";
    public const string FirstCraftDevelopment = "first_town_craft";

    #region 주택 개량

    /// <summary>광장에 놓인 이 건물이 개량할 수 있는 집인가 (작은 집 · 다 지음)</summary>
    public bool IsUpgradableHouse(PlacedDecor placed)
    {
        var config = IndustryConfig;
        if (config == null || placed == null || !IsLoaded || !(placed.Decor is BuildingDefinition building))
            return false;
        if (building.BuildingId != config.HouseBuildingId)
            return false;
        var record = BuildingRecordOf(placed.InstanceId);
        return record == null || record.Built;
    }

    public bool IsImprovedHouse(int instanceId) => IsLoaded && TownRules.IsImproved(Settlement.Industry, instanceId);

    /// <summary>이 집을 개량할 수 없는 까닭 (할 수 있으면 null)</summary>
    public string HouseUpgradeHint(int instanceId)
    {
        var config = IndustryConfig;
        if (config == null || !IsLoaded)
            return "불러오는 중이에요";
        if (IsImprovedHouse(instanceId))
            return "이미 개량한 집이에요";
        if (!TechGate.Has(TechIds.BetterHouses))
            return "연구에서 '주거 개량'을 배우면 열려요";
        if (Warehouse == null || Warehouse.Available(PlankItemId) < config.HousePlanks)
            return $"왕국 창고에 판재가 {config.HousePlanks}개 필요해요";
        if (!HasEnoughGold(config.HouseGold))
            return $"골드가 {config.HouseGold:N0} 필요해요";
        return null;
    }

    /// <returns>개량했나</returns>
    public bool TryImproveHouse(int instanceId)
    {
        if (HouseUpgradeHint(instanceId) != null)
            return false;
        var config = IndustryConfig;
        if (!Warehouse.TryConsume(new[] { (PlankItemId, config.HousePlanks) }, "house"))
            return false;
        PayCost(config.HouseGold, new List<ItemAmount>());
        TownRules.Improve(Settlement.Industry, instanceId);
        Settlement.UnlockDevelopment(FirstImprovedHouseDevelopment);
        RequestSave();
        IndustryChanged?.Invoke();
        OnChanged?.Invoke();
        return true;
    }

    #endregion

    #region 마을 정비 (창고 자재로 꾸미기 만들기)

    /// <summary>만들 수 없는 까닭 (만들 수 있으면 null)</summary>
    public string CraftHint(CraftRecipe recipe)
    {
        if (recipe == null || recipe.Item == null || !IsLoaded || Warehouse == null)
            return "만들 수 없어요";
        if (!string.IsNullOrEmpty(recipe.TechId) && !TechGate.Has(recipe.TechId))
            return $"연구에서 '{TechName(recipe.TechId)}'을 배우면 열려요";
        if (Warehouse.Available(PlankItemId) < recipe.Planks || Warehouse.Available(StoneBlockItemId) < recipe.StoneBlocks)
            return "왕국 창고 자재가 모자라요";
        if (!HasEnoughGold(recipe.Gold))
            return "골드가 모자라요";
        var inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        if (inventory == null || (inventory.GetCount(recipe.Item) == 0 && recipe.Item.UsesBagCapacity && inventory.FreeSlots <= 0))
            return "가방에 자리가 없어요";
        return null;
    }

    /// <returns>만들었나 (가방으로 → 꾸미기에서 놓음)</returns>
    public bool TryCraft(CraftRecipe recipe)
    {
        if (CraftHint(recipe) != null)
            return false;
        if (!Warehouse.TryConsume(TownRules.Costs(PlankItemId, recipe.Planks, StoneBlockItemId, recipe.StoneBlocks), "craft"))
            return false;
        PayCost(recipe.Gold, new List<ItemAmount>());
        InventoryManager.Instance.Inventory.Add(recipe.Item, 1, ItemChangeReason.Crafting);
        Settlement.UnlockDevelopment(FirstCraftDevelopment);
        RequestSave();
        IndustryChanged?.Invoke();
        return true;
    }

    #endregion

    private static string TechName(string techId) => IndustrySection.TechName(techId);
}
