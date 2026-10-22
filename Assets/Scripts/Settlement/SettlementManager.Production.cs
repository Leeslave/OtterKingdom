using System;
using System.Collections.Generic;

/// <summary>
/// 시설 생산 잇기: 다 지은 목공소 · 채석 작업소에서 그 특성 해달들이 꾸준히 목재 · 돌을 모아 가방에 넣는다 (규칙은 ProductionRules).
/// 앱을 꺼 두거나 내려 둔 동안도 (다른 오프라인 생산처럼 최대 8시간) 돌아오면 한꺼번에 셈하고, 돌아옴 팝업에 모은 양을 알린다.
/// </summary>
public partial class SettlementManager
{
    private const float ProductionTickSeconds = 2f;
    // 이만큼 넘게 시계가 건너뛰면(앱을 내려 둠 · 꺼 둠) 돌아옴 팝업에 알림
    private const double ProductionNewsGapSeconds = 180;

    private float _productionTimer;
    // 자리를 비운 동안 모은 것 (돌아옴 팝업에 한 번 알리고 비움)
    private readonly Dictionary<ItemDefinition, int> _producedWhileAway = new Dictionary<ItemDefinition, int>();

    /// <summary>이 시설에서 지금 1시간에 모으는 개수 (생산 시설이 아니거나 일할 해달이 없으면 0)</summary>
    public int ProductionPerHour(BuildingDefinition building) =>
        IsLoaded ? ProductionRules.PerHour(_config, Settlement, building, WorkerPercent(building)) : 0;

    // 일하는 해달 몫에 더하는 %: 목재는 산림관리사 · 돌은 지질학자 편성(왕국 효과) + 그 해달의 장신구 "얻는 양"
    private Func<string, int> WorkerPercent(BuildingDefinition building)
    {
        var kind = building != null && building.ProductionTrait == OtterTrait.Woodcutting ? KingdomBonusKind.WoodYield : KingdomBonusKind.StoneYield;
        int kingdom = KingdomBonus.Percent(kind);
        return otterId => kingdom + CompanionBonus.Accessory(otterId, AccessoryEffect.Yield);
    }

    // Update에서 (SettlementManager.cs)
    private void TickProduction(float dt)
    {
        _productionTimer -= dt;
        if (_productionTimer > 0f)
            return;
        _productionTimer = ProductionTickSeconds;
        RunProduction();
    }

    // 마지막으로 셈한 뒤 흐른 시간만큼 일함 (시계를 되돌려도 같은 시간을 두 번 세지 않게 앞으로만)
    private void RunProduction()
    {
        var inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        if (inventory == null)
            return;
        var data = Settlement.Production;
        long now = NowTicks;
        if (data.lastUtcTicks <= 0 || now <= data.lastUtcTicks)
        {
            data.lastUtcTicks = Math.Max(data.lastUtcTicks, now);
            return;
        }
        double seconds = (now - data.lastUtcTicks) / (double)TimeSpan.TicksPerSecond;
        data.lastUtcTicks = now;
        bool away = seconds > ProductionNewsGapSeconds;

        foreach (var (building, amount) in ProductionRules.Produce(_config, Settlement, FindBuilding, OfflineProductionService.Credited(seconds), WorkerPercent))
        {
            var item = building.ProductionItem;
            int added = inventory.Add(item, amount, ItemChangeReason.Production);
            ProductionRules.Return(Settlement, building, amount - added);
            if (added > 0)
                Settlement.UnlockDevelopment(ProductionRules.FirstProducedDevelopment(building.BuildingId));
            if (away && added > 0)
            {
                _producedWhileAway.TryGetValue(item, out int sum);
                _producedWhileAway[item] = sum + added;
            }
        }
    }

    // 돌아옴 팝업: "목공소 해달들이 목재 120개를 모았어요" (CollectAbsenceNews가 부름. Update보다 먼저 불릴 수 있어 여기서도 셈)
    private void CollectProductionNews(List<string> lines)
    {
        RunProduction();
        foreach (var pair in _producedWhileAway)
        {
            string name = pair.Key.DisplayName;
            lines.Add($"해달들이 일해 {name} {pair.Value:N0}개를 모았어요");
        }
        _producedWhileAway.Clear();
    }
}
