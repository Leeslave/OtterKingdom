using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 연구 트리 노드 그림: 그 기술로 열리는 것의 그림을 이미 연결된 데이터에서 찾는다 (따로 그림 표를 두지 않음).
/// 가공 줄의 가공품 · 왕국 공사 아이콘 · 만들기 꾸미기 → 기술 ID가 적혀 있으니 그대로, 나머지(창고 · 수레 · 주택 · 공구 등)는 같은 설정의 그림.
/// 못 찾으면 마을회관 그림
/// </summary>
public static class ResearchTreeIcons
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static IndustryConfig _cachedFor;

    public static Sprite For(string techId)
    {
        var settlement = SettlementManager.Instance;
        var config = settlement != null ? settlement.IndustryConfig : null;
        if (config != _cachedFor)
        {
            Cache.Clear();
            _cachedFor = config;
        }
        if (Cache.TryGetValue(techId, out var cached))
            return cached;
        var sprite = Find(techId, config) ?? Fallback(techId, settlement);
        Cache[techId] = sprite;
        return sprite;
    }

    private static Sprite Find(string techId, IndustryConfig config)
    {
        if (config == null)
            return null;
        foreach (var recipe in config.Recipes)
            if (recipe != null && recipe.TechId == techId && recipe.Output != null && recipe.Output.Icon != null)
                return recipe.Output.Icon;
        foreach (var project in config.Projects)
            if (project != null && project.TechId == techId && project.Icon != null)
                return project.Icon;
        foreach (var craft in config.Crafts)
            if (craft != null && craft.TechId == techId && craft.Item != null && craft.Item.Icon != null)
                return craft.Item.Icon;
        switch (techId)
        {
            case TechIds.Warehouse:
            case TechIds.Depot:
                return config.WarehouseSprite;
            case TechIds.Cart:
            case TechIds.Dispatch:
            case TechIds.AutoSupply:
                return config.EmptyCartSprite;
            case TechIds.BetterCart:
                return config.CartPennant != null ? config.CartPennant : config.EmptyCartSprite;
            case TechIds.BetterHouses:
                return config.ImprovedHouseSprite(0);
            case TechIds.Administration:
                return config.TownHouseIcon(0);
            case TechIds.StandardTools:
                return RecipeOf(config, TechIds.Sawmill)?.BenchSprite;
            case TechIds.WaterPower:
                return RecipeOf(config, TechIds.Smelting)?.BenchSprite;
            case TechIds.CoProduction:
                return RecipeOf(config, TechIds.PrecisionWork)?.PileSprite(3, 3);
            case TechIds.IndustrialLogistics:
                return RecipeOf(config, TechIds.PrecisionWork)?.CartSprite;
        }
        return null;
    }

    private static ProcessingRecipeDefinition RecipeOf(IndustryConfig config, string techId)
    {
        foreach (var recipe in config.Recipes)
            if (recipe != null && recipe.TechId == techId)
                return recipe;
        return null;
    }

    private static Sprite Fallback(string techId, SettlementManager settlement)
    {
        if (settlement == null)
            return null;
        if (techId == TechIds.Market && settlement.Config.GoldCurrency != null)
            return settlement.Config.GoldCurrency.Icon;
        return settlement.TownHallIcon;
    }
}
