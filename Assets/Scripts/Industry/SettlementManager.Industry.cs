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

    /// <summary>시설이 가공품을 만듦 (가공품, 개수) — 퀘스트 "가공품 만들기"가 셈 (처음 켤 때 1개 · 오프라인 포함)</summary>
    public event Action<ItemDefinition, int> OnProcessed;

    // 퀘스트 "확인하기" 단계용 발전 기록 (기획서 11.2 Lv.24~27, Milestone 목표의 Target)
    public const string ProcessingOnDevelopment = "industry_processing_on";
    public const string CartRouteDevelopment = "industry_cart_route";
    public const string CartDeliveredDevelopment = "industry_cart_delivered";
    public const string WarehouseViewedDevelopment = "industry_warehouse_viewed";
    public const string HouseViewedDevelopment = "industry_house_viewed";

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

    // 공동 창고(L01) 전에는 가공품만 잠깐 두는 작은 칸, 집산소(L05)를 배우면 더 큼
    private int WarehouseCapacity()
    {
        var config = IndustryConfig;
        if (config == null)
            return 0;
        if (TechGate.Has(TechIds.Depot))
            return config.DepotCapacity;
        return TechGate.Has(TechIds.Warehouse) ? config.WarehouseCapacity : config.BaseCapacity;
    }

    /// <summary>가공 속도 배수 (표준 공구 I04 ×2 · 수력 기계 I05 ×1.15)</summary>
    public float ProcessingSpeed => ProcessingRules.Speed(TechGate.Has);

    /// <summary>창고를 봤음 (퀘스트 "왕국 자재 장부 확인")</summary>
    public void MarkWarehouseViewed()
    {
        if (IsLoaded)
            Settlement.UnlockDevelopment(WarehouseViewedDevelopment);
    }

    /// <summary>일터 창을 창고 탭으로 엶</summary>
    public void OpenWarehouse() => OpenProduction(ProductionSheet.Tab.Warehouse);

    #region 가공 (가공 줄 = 레시피)

    /// <summary>이 시설의 첫 가공 줄 (시설이 모은 원재료를 받는 줄)</summary>
    public ProcessingRecipeDefinition RecipeFor(BuildingDefinition building) =>
        building != null && IndustryConfig != null ? IndustryConfig.RecipeFor(building.BuildingId) : null;

    /// <summary>이 줄을 켤 수 있나 (시설을 지었고 · 기술을 배움)</summary>
    public bool CanProcess(ProcessingRecipeDefinition recipe) =>
        recipe != null && recipe.IsValid && IsLoaded && Settlement.CountBuilt(recipe.Building.BuildingId) > 0 && TechGate.Has(recipe.TechId);

    public ProcessorSaveData ProcessorOf(ProcessingRecipeDefinition recipe) => ProcessingRules.Processor(Settlement.Industry, recipe.ProcessorId);

    public bool IsProcessing(ProcessingRecipeDefinition recipe) => CanProcess(recipe) && ProcessorOf(recipe).on;

    /// <summary>이 시설의 첫 줄이 가공 중인가 (시설이 모은 원재료가 가공으로 감)</summary>
    public bool IsProcessing(BuildingDefinition building) => IsProcessing(RecipeFor(building));

    /// <summary>가공 켜기 · 끄기. 처음 켜면 한 개를 바로 만들어 줌</summary>
    public void SetProcessing(ProcessingRecipeDefinition recipe, bool on)
    {
        if (!CanProcess(recipe))
            return;
        if (ProcessingRules.SetOn(ProcessorOf(recipe), recipe.ToRecipe(), on))
        {
            MarkFirstProcessed(recipe);
            OnProcessed?.Invoke(recipe.Output, 1);
        }
        if (on)
            Settlement.UnlockDevelopment(ProcessingOnDevelopment);
        RequestSave();
        IndustryChanged?.Invoke();
    }

    /// <summary>가방의 원재료를 가공에 넣음 (넣을 자리 · 가진 수만큼, 창고 재료 줄은 안 됨)</summary>
    /// <returns>넣은 수</returns>
    public int LoadProcessorFromBag(ProcessingRecipeDefinition recipe, int amount)
    {
        if (!IsProcessing(recipe) || recipe.FromWarehouse || amount <= 0)
            return 0;
        var inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        if (inventory == null)
            return 0;
        var processor = ProcessorOf(recipe);
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
    public int CollectProcessed(ProcessingRecipeDefinition recipe)
    {
        if (recipe == null || !recipe.IsValid || !IsLoaded || Warehouse == null)
            return 0;
        int moved = ProcessingRules.Collect(ProcessorOf(recipe), recipe.ToRecipe(), Warehouse, "collect");
        if (moved > 0)
        {
            RequestSave();
            IndustryChanged?.Invoke();
        }
        return moved;
    }

    /// <summary>원료 자동 공급(L04) 켜기 · 끄기 (가방 재료 줄만)</summary>
    public void SetAutoSupply(ProcessingRecipeDefinition recipe, bool on)
    {
        if (!CanProcess(recipe) || recipe.FromWarehouse || !TechGate.Has(TechIds.AutoSupply))
            return;
        ProcessorOf(recipe).autoSupply = on;
        if (on)
            AutoSupply(recipe);
        RequestSave();
        IndustryChanged?.Invoke();
    }

    // 자동 공급: 가방의 원재료를 넣을 자리만큼 (가방에 keep개는 남김)
    private void AutoSupply(ProcessingRecipeDefinition recipe)
    {
        if (recipe.FromWarehouse)
            return;
        var processor = ProcessorOf(recipe);
        var inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        if (!processor.autoSupply || !processor.on || inventory == null || !TechGate.Has(TechIds.AutoSupply))
            return;
        int take = ProcessingRules.AutoSupplyAmount(processor, recipe.ToRecipe(), inventory.GetCount(recipe.Input), Math.Max(0, processor.keep));
        if (take > 0 && inventory.TryRemove(recipe.Input, take, ItemChangeReason.Processing))
            ProcessingRules.Feed(processor, recipe.ToRecipe(), take);
    }

    // 시설 생산이 부름: 첫 줄이 가공 중이고 그 재료가 시설이 모은 것이면 먼저 가공에 넣음
    /// <returns>가공에 넣은 수 (나머지는 가방으로)</returns>
    private int FeedProcessor(BuildingDefinition building, int produced)
    {
        var recipe = RecipeFor(building);
        if (produced <= 0 || recipe == null || recipe.FromWarehouse || recipe.Input != building.ProductionItem || !IsProcessing(recipe))
            return 0;
        return ProcessingRules.Feed(ProcessorOf(recipe), recipe.ToRecipe(), produced);
    }

    private void MarkFirstProcessed(ProcessingRecipeDefinition recipe)
    {
        if (!string.IsNullOrEmpty(recipe.FirstDevelopment))
            Settlement.UnlockDevelopment(recipe.FirstDevelopment);
    }

    #endregion

    #region 수레

    /// <summary>수레가 다닐 수 있는 가공 줄 수 (L02 1곳 · L03 3곳 · L06 5곳)</summary>
    public int CartRouteLimit => CartRules.RouteLimit(TechGate.Has);

    public bool HasCartRoute(ProcessingRecipeDefinition recipe) =>
        recipe != null && recipe.IsValid && IsLoaded && CartRules.HasRoute(Settlement.Industry, recipe.ProcessorId);

    /// <returns>바꿨나 (수레가 모자라면 false)</returns>
    public bool SetCartRoute(ProcessingRecipeDefinition recipe, bool on)
    {
        if (recipe == null || !recipe.IsValid || !IsLoaded)
            return false;
        if (!CartRules.SetRoute(Settlement.Industry, recipe.ProcessorId, on, CartRouteLimit))
            return false;
        if (on)
            Settlement.UnlockDevelopment(CartRouteDevelopment);
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
        float speed = ProcessingSpeed;
        var recipes = new List<ProcessingRecipe>();
        foreach (var definition in config.Recipes)
        {
            if (definition == null || !definition.IsValid)
                continue;
            var recipe = definition.ToRecipe();
            recipes.Add(recipe);
            if (!IsProcessing(definition))
                continue;
            int made;
            if (recipe.FromWarehouse)
            {
                made = Warehouse != null ? ProcessingRules.RunFromWarehouse(ProcessorOf(definition), recipe, seconds, speed, Warehouse) : 0;
            }
            else
            {
                AutoSupply(definition);
                made = ProcessingRules.Run(ProcessorOf(definition), recipe, seconds, speed);
            }
            if (made <= 0)
                continue;
            changed = true;
            MarkFirstProcessed(definition);
            OnProcessed?.Invoke(definition.Output, made);
            if (away)
            {
                _producedWhileAway.TryGetValue(definition.Output, out int sum);
                _producedWhileAway[definition.Output] = sum + made;
            }
        }
        if (Warehouse != null && CartRules.Deliver(Settlement.Industry, recipes, Warehouse, CartRouteLimit).Count > 0)
        {
            changed = true;
            Settlement.UnlockDevelopment(CartDeliveredDevelopment);
        }
        if (changed)
            IndustryChanged?.Invoke();
    }
}
