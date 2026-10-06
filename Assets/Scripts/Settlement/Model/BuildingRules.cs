using System;
using System.Collections.Generic;

/// <summary>건물을 지을 수 없는 이유 (앞의 것부터 확인)</summary>
public enum BuildingBlock
{
    None,
    Level,       // 왕국 레벨이 모자람
    Development, // 필요한 발전(예: 마을회관)이 아직 없음
    Traits,      // 특성 해달이 모자람
    MaxCount,    // 더 지을 수 없음
    Busy,        // 공사 자리가 다 찼음 (다른 건물을 짓는 중)
    NoBuilder,   // 건설 해달이 없음
    Gold,        // 골드가 모자람
    Items,       // 재료가 모자람
}

/// <summary>
/// 자리를 골라 짓는 건물 (P4, 순수 C#): 해금 조건(레벨 · 발전 · 특성 · 최대 수), 동시에 짓는 수, 늘어나는 비용,
/// 공사 완료, 다 지은 집 → 빈 집 기록 (Settlement.Houses, 자리 ID = bld_{배치 ID})
/// </summary>
public static class BuildingRules
{
    /// <summary>동시에 지을 수 있는 건물 수 (건설소 같은 효과는 3단계에서 더함)</summary>
    public const int BaseSlots = 1;

    /// <summary>다 지은 집의 집 자리 ID (장난감 해달 입주용 빈 집)</summary>
    public static string HomeSlotId(int instanceId) => $"bld_{instanceId}";

    public static string HomeInstanceId(int instanceId) => $"house_bld_{instanceId}";

    /// <summary>이 건물이 열렸는지 (레벨 → 발전 → 특성 → 최대 수 순서로 첫 이유)</summary>
    public static BuildingBlock CheckUnlocked(SettlementConfig config, Settlement settlement, BuildingDefinition building, int level)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        if (building == null)
            throw new ArgumentNullException(nameof(building));

        if (building.RequiredLevel > 0 && level < building.RequiredLevel)
            return BuildingBlock.Level;
        if (!string.IsNullOrEmpty(building.RequiredDevelopment) && !settlement.HasDevelopment(building.RequiredDevelopment))
            return BuildingBlock.Development;
        foreach (var requirement in building.Traits)
        {
            if (requirement != null && TraitRules.Count(config, settlement, requirement.Trait) < requirement.Count)
                return BuildingBlock.Traits;
        }
        if (building.MaxCount > 0 && settlement.CountBuildings(building.BuildingId) >= building.MaxCount)
            return BuildingBlock.MaxCount;
        return BuildingBlock.None;
    }

    /// <summary>지금 지을 수 있는지: 열림 → 공사 자리 → 건설 해달 → 골드 → 재료</summary>
    /// <param name="gold">가진 골드</param>
    /// <param name="owned">가진 아이템 수</param>
    public static BuildingBlock CheckStart(SettlementConfig config, Settlement settlement, BuildingDefinition building, int level,
        bool hasBuilder, int gold, Func<ItemDefinition, int> owned)
    {
        if (owned == null)
            throw new ArgumentNullException(nameof(owned));

        var unlocked = CheckUnlocked(config, settlement, building, level);
        if (unlocked != BuildingBlock.None)
            return unlocked;
        if (settlement.ActiveBuildingCount >= BaseSlots)
            return BuildingBlock.Busy;
        if (!hasBuilder)
            return BuildingBlock.NoBuilder;
        if (gold < GoldCost(settlement, building))
            return BuildingBlock.Gold;
        foreach (var cost in ItemCosts(settlement, building))
        {
            if (owned(cost.Item) < cost.Amount)
                return BuildingBlock.Items;
        }
        return BuildingBlock.None;
    }

    /// <summary>비용 배율: 이미 지었거나 짓는 중인 같은 건물 하나마다 CostGrowth씩</summary>
    public static float CostMultiplier(Settlement settlement, BuildingDefinition building) =>
        1f + Math.Max(0f, building.CostGrowth) * settlement.CountBuildings(building.BuildingId);

    public static int GoldCost(Settlement settlement, BuildingDefinition building) =>
        Scale(building.Gold, CostMultiplier(settlement, building));

    public static List<ItemAmount> ItemCosts(Settlement settlement, BuildingDefinition building)
    {
        float multiplier = CostMultiplier(settlement, building);
        var result = new List<ItemAmount>();
        foreach (var cost in building.Items)
        {
            if (cost != null && cost.Item != null && cost.Amount > 0)
                result.Add(new ItemAmount(cost.Item, Scale(cost.Amount, multiplier)));
        }
        return result;
    }

    /// <summary>공사를 시작 (비용은 부른 쪽이 먼저 냄). 끝나는 시각을 저장해 꺼 둔 동안에도 진행된다</summary>
    public static bool Start(Settlement settlement, BuildingDefinition building, int instanceId, long nowUtcTicks, double seconds) =>
        settlement.StartBuilding(instanceId, building.BuildingId, nowUtcTicks, nowUtcTicks + TimeSpan.FromSeconds(Math.Max(0d, seconds)).Ticks);

    /// <summary>시간이 된 공사를 끝낸다 (완성한 기록을 finished에 넣음)</summary>
    public static void FinishDue(Settlement settlement, long nowUtcTicks, List<BuildingRecord> finished)
    {
        if (finished == null)
            throw new ArgumentNullException(nameof(finished));
        finished.Clear();
        foreach (var record in new List<BuildingRecord>(settlement.Buildings))
        {
            if (record.IsDue(nowUtcTicks) && settlement.FinishBuilding(record.InstanceId))
                finished.Add(record);
        }
    }

    /// <summary>다 지은 집마다 빈 집 기록을 맞춘다 (자리마다 한 채. 다시 해도 같음)</summary>
    /// <returns>새로 생긴 집 수</returns>
    public static int SyncHomes(Settlement settlement, Func<string, BuildingDefinition> find)
    {
        if (find == null)
            throw new ArgumentNullException(nameof(find));
        int added = 0;
        foreach (var record in settlement.Buildings)
        {
            if (!record.Built)
                continue;
            var building = find(record.BuildingId);
            if (building == null || building.Kind != BuildingKind.Home)
                continue;
            if (settlement.AddHouse(HomeInstanceId(record.InstanceId), record.BuildingId, HomeSlotId(record.InstanceId)))
                added++;
        }
        return added;
    }

    /// <summary>다 지은 시설의 이 효과 합계 (%). 같은 건물은 여러 채여도 한 번만 셈</summary>
    public static int BonusPercent(Settlement settlement, Func<string, BuildingDefinition> find, KingdomBonusKind kind)
    {
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        if (find == null)
            throw new ArgumentNullException(nameof(find));
        int total = 0;
        var counted = new HashSet<string>();
        foreach (var record in settlement.Buildings)
        {
            if (!record.Built || !counted.Add(record.BuildingId))
                continue;
            var building = find(record.BuildingId);
            if (building == null)
                continue;
            foreach (var bonus in building.Bonuses)
            {
                if (bonus != null && bonus.Kind == kind)
                    total += bonus.Percent;
            }
        }
        return total;
    }

    private static int Scale(int amount, float multiplier) =>
        amount <= 0 ? 0 : (int)Math.Ceiling(amount * multiplier - 0.0001f);
}
