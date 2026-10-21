using System;
using System.Collections.Generic;

/// <summary>세이브: 시설 하나가 모으던 재료 (아직 1개가 안 된 몫 · 가방이 차서 못 넣은 몫)</summary>
[Serializable]
public class ProductionProgressSaveData
{
    public string buildingId;
    public float amount;
}

/// <summary>세이브: 시설 생산 (목공소 · 채석 작업소. SettlementSaveData.production)</summary>
[Serializable]
public class BuildingProductionSaveData
{
    // 마지막으로 생산을 셈한 시각 (UTC ticks, 0 = 아직 셈한 적 없음)
    public long lastUtcTicks;
    public List<ProductionProgressSaveData> progress = new List<ProductionProgressSaveData>();
}

/// <summary>시설 생산 상태. 규칙은 ProductionRules, 잇기(가방 · 시계)는 SettlementManager.Production</summary>
public partial class Settlement
{
    public BuildingProductionSaveData Production { get; private set; } = new BuildingProductionSaveData();

    private void LoadProduction(SettlementSaveData saved) => Production = saved.production ?? new BuildingProductionSaveData();

    private void WriteProduction(SettlementSaveData result) => result.production = Production;
}

/// <summary>
/// 시설 생산 (순수 C#): 다 지은 생산 시설(BuildingDefinition.Produces)마다, 마을에 사는 그 특성 해달 수만큼 일해
/// 1시간에 (해달 한 마리 몫 × 수)만큼 재료가 쌓인다. 1개가 안 된 몫은 다음에 이어지고, 쌓아 둘 수 있는 양은 최대 MaxStoredHours 시간치.
/// </summary>
public static class ProductionRules
{
    /// <summary>가방이 차서 못 넣은 몫까지 쌓아 두는 최대 시간 (오프라인 생산처럼 8시간)</summary>
    public const double MaxStoredHours = 8;

    /// <summary>지금 이 시설에서 일하는 해달 수 (그 특성의 주민 수)</summary>
    public static int Workers(SettlementConfig config, Settlement settlement, BuildingDefinition building) =>
        building != null && building.Produces ? TraitRules.Count(config, settlement, building.ProductionTrait) : 0;

    /// <summary>1시간에 모으는 개수</summary>
    public static int PerHour(SettlementConfig config, Settlement settlement, BuildingDefinition building) =>
        building != null && building.Produces ? building.ProductionPerHour * Workers(config, settlement, building) : 0;

    /// <summary>
    /// seconds만큼 일한다. 시설마다 쌓인 몫이 1개 이상이면 꺼내 (시설, 개수)로 돌려줌 — 가방에 못 넣은 것은 Return으로 되돌림.
    /// 같은 건물을 여러 채 지어도 한 번만 셈 (일하는 해달은 같은 해달들)
    /// </summary>
    public static List<(BuildingDefinition building, int amount)> Produce(SettlementConfig config, Settlement settlement,
        Func<string, BuildingDefinition> find, double seconds)
    {
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        if (find == null)
            throw new ArgumentNullException(nameof(find));

        var result = new List<(BuildingDefinition, int)>();
        if (seconds <= 0)
            return result;
        var counted = new HashSet<string>();
        foreach (var record in settlement.Buildings)
        {
            if (!record.Built || !counted.Add(record.BuildingId))
                continue;
            var building = find(record.BuildingId);
            int perHour = PerHour(config, settlement, building);
            if (perHour <= 0)
                continue;
            var progress = ProgressOf(settlement.Production, building.BuildingId);
            progress.amount = (float)Math.Min(progress.amount + perHour * seconds / 3600d, perHour * MaxStoredHours);
            int whole = (int)Math.Floor(progress.amount);
            if (whole <= 0)
                continue;
            progress.amount -= whole;
            result.Add((building, whole));
        }
        return result;
    }

    /// <summary>이 시설이 처음 재료를 가방에 넣으면 열리는 발전 (메인 퀘스트 "목공소의 첫 목재" 등이 셈)</summary>
    public static string FirstProducedDevelopment(string buildingId) => "produced_" + buildingId;

    /// <summary>가방이 차서 못 넣은 몫을 시설에 되돌림 (다음에 다시 넣어 봄)</summary>
    public static void Return(Settlement settlement, BuildingDefinition building, int amount)
    {
        if (amount > 0)
            ProgressOf(settlement.Production, building.BuildingId).amount += amount;
    }

    private static ProductionProgressSaveData ProgressOf(BuildingProductionSaveData data, string buildingId)
    {
        var progress = data.progress.Find(p => p.buildingId == buildingId);
        if (progress == null)
            data.progress.Add(progress = new ProductionProgressSaveData { buildingId = buildingId });
        return progress;
    }
}
