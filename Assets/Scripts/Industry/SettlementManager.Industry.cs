using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 번영의 시대 산업 · 물류 잇기 (Docs/번영의시대_개발분담.md, 개발자 A):
/// 가공을 켠 시설은 해달들이 모은 원재료를 가방 대신 가공에 넣고(시설 생산 RunProduction이 부름), 만든 가공품은 시설에 쌓였다가
/// 수령 · 수레로 왕국 창고에. 오프라인도 같은 순서 (원재료 → 가공 → 창고, 기획서 7.3 1~4단계)
/// </summary>
public partial class SettlementManager
{
    private IndustryConfig _industryConfig;
    private KingdomWarehouse _warehouse;
    private IndustrySaveData _warehouseData;

    /// <summary>창고 자재가 바뀜 (아이템 ID, 바뀐 수, 이유) — 퀘스트 · 계약이 셈 (세이브를 다시 읽어도 이어짐)</summary>
    public static event Action<string, int, string> WarehouseChanged;

    /// <summary>가공 · 창고 · 수레 상태가 바뀜 (화면 갱신)</summary>
    public event Action IndustryChanged;

    public IndustryConfig IndustryConfig =>
        _industryConfig != null ? _industryConfig : (_industryConfig = Resources.Load<IndustryConfig>(IndustryConfig.ResourcePath));

    /// <summary>왕국 창고 (불러오기 전이면 null)</summary>
    public KingdomWarehouse Warehouse
    {
        get
        {
            if (!IsLoaded)
                return null;
            var data = Settlement.Industry;
            if (_warehouse == null || _warehouseData != data)
            {
                _warehouseData = data;
                _warehouse = new KingdomWarehouse(data, WarehouseCapacity);
                _warehouse.Changed += (itemId, delta, reason) => WarehouseChanged?.Invoke(itemId, delta, reason);
            }
            return _warehouse;
        }
    }

    // 공동 창고(L01) 전에는 가공품만 잠깐 두는 작은 칸
    private int WarehouseCapacity()
    {
        var config = IndustryConfig;
        if (config == null)
            return 0;
        return TechGate.Has(TechIds.Warehouse) ? config.WarehouseCapacity : config.BaseCapacity;
    }

    /// <summary>일터 창을 창고 탭으로 엶</summary>
    public void OpenWarehouse() => OpenProduction(ProductionSheet.Tab.Warehouse);

    #region 가공

    public ProcessingRecipeDefinition RecipeFor(BuildingDefinition building) =>
        building != null && IndustryConfig != null ? IndustryConfig.RecipeFor(building.BuildingId) : null;

    /// <summary>이 시설에서 가공을 켤 수 있나 (레시피가 있고 · 지었고 · 기술을 배움)</summary>
    public bool CanProcess(BuildingDefinition building)
    {
        var recipe = RecipeFor(building);
        return recipe != null && IsLoaded && Settlement.CountBuilt(building.BuildingId) > 0 && TechGate.Has(recipe.TechId);
    }

    public ProcessorSaveData ProcessorOf(BuildingDefinition building) => ProcessingRules.Processor(Settlement.Industry, building.BuildingId);

    public bool IsProcessing(BuildingDefinition building) => CanProcess(building) && ProcessorOf(building).on;

    /// <summary>가공 켜기 · 끄기. 처음 켜면 한 개를 바로 만들어 줌</summary>
    public void SetProcessing(BuildingDefinition building, bool on)
    {
        if (!CanProcess(building))
            return;
        var recipe = RecipeFor(building);
        if (ProcessingRules.SetOn(ProcessorOf(building), recipe.ToRecipe(), on))
            MarkFirstProcessed(recipe);
        RequestSave();
        IndustryChanged?.Invoke();
    }

    /// <summary>가방의 원재료를 가공에 넣음 (넣을 자리 · 가진 수만큼)</summary>
    /// <returns>넣은 수</returns>
    public int LoadProcessorFromBag(BuildingDefinition building, int amount)
    {
        if (!IsProcessing(building) || amount <= 0)
            return 0;
        var inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        if (inventory == null)
            return 0;
        var recipe = RecipeFor(building);
        var processor = ProcessorOf(building);
        int room = Math.Max(0, recipe.ToRecipe().InputCapacity - processor.input);
        int take = Math.Min(amount, Math.Min(room, inventory.GetCount(recipe.Input)));
        if (take <= 0 || !inventory.TryRemove(recipe.Input, take, ItemChangeReason.Processing))
            return 0;
        ProcessingRules.Feed(processor, recipe.ToRecipe(), take);
        RequestSave();
        IndustryChanged?.Invoke();
        return take;
    }

    /// <summary>시설에 쌓인 가공품을 창고로</summary>
    /// <returns>옮긴 수 (창고가 차면 덜 옮김)</returns>
    public int CollectProcessed(BuildingDefinition building)
    {
        var recipe = RecipeFor(building);
        if (recipe == null || !IsLoaded || Warehouse == null)
            return 0;
        int moved = ProcessingRules.Collect(ProcessorOf(building), recipe.ToRecipe(), Warehouse, "collect");
        if (moved > 0)
        {
            RequestSave();
            IndustryChanged?.Invoke();
        }
        return moved;
    }

    // 시설 생산이 부름: 가공 중이면 모은 원재료를 먼저 가공에 넣음
    /// <returns>가공에 넣은 수 (나머지는 가방으로)</returns>
    private int FeedProcessor(BuildingDefinition building, int produced)
    {
        if (produced <= 0 || !IsProcessing(building))
            return 0;
        return ProcessingRules.Feed(ProcessorOf(building), RecipeFor(building).ToRecipe(), produced);
    }

    private void MarkFirstProcessed(ProcessingRecipeDefinition recipe)
    {
        if (!string.IsNullOrEmpty(recipe.FirstDevelopment))
            Settlement.UnlockDevelopment(recipe.FirstDevelopment);
    }

    #endregion

    #region 수레

    /// <summary>수레가 다닐 수 있는 시설 수 (L02 1곳 · L03 3곳)</summary>
    public int CartRouteLimit => CartRules.RouteLimit(TechGate.Has);

    public bool HasCartRoute(BuildingDefinition building) => building != null && IsLoaded && CartRules.HasRoute(Settlement.Industry, building.BuildingId);

    /// <returns>바꿨나 (수레가 모자라면 false)</returns>
    public bool SetCartRoute(BuildingDefinition building, bool on)
    {
        if (building == null || !IsLoaded || RecipeFor(building) == null)
            return false;
        if (!CartRules.SetRoute(Settlement.Industry, building.BuildingId, on, CartRouteLimit))
            return false;
        RequestSave();
        IndustryChanged?.Invoke();
        return true;
    }

    #endregion

    // RunProduction 끝에서: 가공하고 수레가 창고로 옮김
    private void RunIndustry(double seconds, bool away)
    {
        var config = IndustryConfig;
        if (config == null || !IsLoaded)
            return;
        bool changed = false;
        var recipes = new List<ProcessingRecipe>();
        foreach (var definition in config.Recipes)
        {
            if (definition == null || !definition.IsValid)
                continue;
            var recipe = definition.ToRecipe();
            recipes.Add(recipe);
            if (!IsProcessing(definition.Building))
                continue;
            int made = ProcessingRules.Run(ProcessorOf(definition.Building), recipe, seconds);
            if (made <= 0)
                continue;
            changed = true;
            MarkFirstProcessed(definition);
            if (away)
            {
                _producedWhileAway.TryGetValue(definition.Output, out int sum);
                _producedWhileAway[definition.Output] = sum + made;
            }
        }
        if (Warehouse != null && CartRules.Deliver(Settlement.Industry, recipes, Warehouse, CartRouteLimit).Count > 0)
            changed = true;
        if (changed)
            IndustryChanged?.Invoke();
    }
}
