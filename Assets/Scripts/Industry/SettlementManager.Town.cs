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
    public const string FirstTownHouseDevelopment = "first_town_house";
    public const string IngotItemId = "ingot";

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

    public bool IsTownHouse(int instanceId) => IsLoaded && TownRules.IsTownHouse(Settlement.Industry, instanceId);

    /// <summary>마을 주택(2단계)으로 바꿀 수 없는 까닭 (할 수 있으면 null)</summary>
    public string TownHouseHint(int instanceId)
    {
        var config = IndustryConfig;
        if (config == null || !IsLoaded || Warehouse == null)
            return "불러오는 중이에요";
        if (!IsImprovedHouse(instanceId))
            return "먼저 개량 주택으로 바꿔요";
        if (IsTownHouse(instanceId))
            return "이미 마을 주택이에요";
        if (!TechGate.Has(TechIds.Administration))
            return $"연구에서 '{TechName(TechIds.Administration)}'을 배우면 열려요";
        if (Warehouse.Available(StoneBlockItemId) < config.TownHouseStoneBlocks || Warehouse.Available(IngotItemId) < config.TownHouseIngots)
            return $"왕국 창고에 석재 블록 {config.TownHouseStoneBlocks}개 · 금속 주괴 {config.TownHouseIngots}개가 필요해요";
        if (!HasEnoughGold(config.TownHouseGold))
            return $"골드가 {config.TownHouseGold:N0} 필요해요";
        return null;
    }

    /// <returns>마을 주택으로 바꿨나</returns>
    public bool TryMakeTownHouse(int instanceId)
    {
        if (TownHouseHint(instanceId) != null)
            return false;
        var config = IndustryConfig;
        if (!Warehouse.TryConsume(TownRules.Costs(StoneBlockItemId, config.TownHouseStoneBlocks, IngotItemId, config.TownHouseIngots), "house"))
            return false;
        PayCost(config.TownHouseGold, new List<ItemAmount>());
        TownRules.MakeTownHouse(Settlement.Industry, instanceId);
        Settlement.UnlockDevelopment(FirstTownHouseDevelopment);
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
        if (Warehouse.Available(PlankItemId) < recipe.Planks || Warehouse.Available(StoneBlockItemId) < recipe.StoneBlocks
            || Warehouse.Available(IngotItemId) < recipe.Ingots)
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
        var costs = TownRules.Costs(PlankItemId, recipe.Planks, StoneBlockItemId, recipe.StoneBlocks);
        if (recipe.Ingots > 0)
            costs.Add((IngotItemId, recipe.Ingots));
        if (!Warehouse.TryConsume(costs, "craft"))
            return false;
        PayCost(recipe.Gold, new List<ItemAmount>());
        InventoryManager.Instance.Inventory.Add(recipe.Item, 1, ItemChangeReason.Crafting);
        Settlement.UnlockDevelopment(FirstCraftDevelopment);
        RequestSave();
        IndustryChanged?.Invoke();
        return true;
    }

    #endregion

    #region 광장 돌길 (바닥 공사)

    public bool IsPaved(PavingProject project) => project != null && IsLoaded && HasDevelopment(project.Id);

    /// <summary>깔 수 없는 까닭 (깔 수 있으면 null)</summary>
    public string PavingHint(PavingProject project)
    {
        if (project == null || string.IsNullOrEmpty(project.Id) || !IsLoaded || Warehouse == null)
            return "깔 수 없어요";
        if (IsPaved(project))
            return "이미 깔았어요";
        if (!string.IsNullOrEmpty(project.TechId) && !TechGate.Has(project.TechId))
            return $"연구에서 '{TechName(project.TechId)}'을 배우면 열려요";
        if (!string.IsNullOrEmpty(project.Requires) && !HasDevelopment(project.Requires))
            return "먼저 중앙 광장 돌길을 깔아요";
        if (Warehouse.Available(StoneBlockItemId) < project.StoneBlocks || Warehouse.Available(PlankItemId) < project.Planks)
            return "왕국 창고 자재가 모자라요";
        if (!HasEnoughGold(project.Gold))
            return "골드가 모자라요";
        return null;
    }

    /// <returns>깔았나 (광장에 돌길이 깔림 — IndustryPlazaView)</returns>
    public bool TryPave(PavingProject project)
    {
        if (PavingHint(project) != null)
            return false;
        if (!Warehouse.TryConsume(TownRules.Costs(StoneBlockItemId, project.StoneBlocks, PlankItemId, project.Planks), "paving"))
            return false;
        PayCost(project.Gold, new List<ItemAmount>());
        Settlement.UnlockDevelopment(project.Id);
        RequestSave();
        IndustryChanged?.Invoke();
        OnChanged?.Invoke();
        return true;
    }

    #endregion

    private static string TechName(string techId) => IndustrySection.TechName(techId);
}
