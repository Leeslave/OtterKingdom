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
        building != null && building.Produces ? TraitRules.Settled(config, settlement, building.ProductionTrait)
            .FindAll(id => CanProduce(config, settlement, id)).Count : 0;

    private static bool CanProduce(SettlementConfig config, Settlement settlement, string id)
    {
        if (settlement.GetWorkState(id) != ResidentWorkState.Idle || settlement.HasRole(id)) return false;
        var otter = config.FindOtter(id);
        if (otter == null || otter.IsBuilder || (otter.IsSpecialist && settlement.GetSpecialistState(id) >= SpecialistState.Assigned)) return false;
        var request = settlement.Job != null ? config.FindRequest(settlement.Job.RequestId) : null;
        return request == null || request.Construction == null || request.Construction.NeedsBuilder || request.Requester?.OtterId != id;
    }

    /// <summary>1시간에 모으는 개수 (반올림)</summary>
    /// <param name="workerPercent">해달 ID → 그 해달 몫에 더하는 % (편성 · 장신구 얻는 양, 없으면 0)</param>
    public static int PerHour(SettlementConfig config, Settlement settlement, BuildingDefinition building, Func<string, int> workerPercent = null) =>
        (int)Math.Round(PerHourExact(config, settlement, building, workerPercent));

    /// <summary>1시간에 모으는 양 (소수 그대로): 일하는 해달마다 한 마리 몫 × (100 + 그 해달 %) / 100</summary>
    public static double PerHourExact(SettlementConfig config, Settlement settlement, BuildingDefinition building, Func<string, int> workerPercent = null)
    {
        if (building == null || !building.Produces)
            return 0;
        double total = 0;
        foreach (var otterId in TraitRules.Settled(config, settlement, building.ProductionTrait))
        {
            if (!CanProduce(config, settlement, otterId)) continue;
            total += building.ProductionPerHour * (100 + Math.Max(0, workerPercent != null ? workerPercent(otterId) : 0)) / 100.0;
        }
        return total;
    }

    /// <summary>
    /// seconds만큼 일한다. 시설마다 쌓인 몫이 1개 이상이면 꺼내 (시설, 개수)로 돌려줌 — 가방에 못 넣은 것은 Return으로 되돌림.
    /// 같은 건물을 여러 채 지어도 한 번만 셈 (일하는 해달은 같은 해달들)
    /// </summary>
    public static List<(BuildingDefinition building, int amount)> Produce(SettlementConfig config, Settlement settlement,
        Func<string, BuildingDefinition> find, double seconds, Func<BuildingDefinition, Func<string, int>> workerPercent = null)
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
            double perHour = PerHourExact(config, settlement, building, building != null && workerPercent != null ? workerPercent(building) : null);
            if (perHour <= 0)
                continue;
            var progress = ProgressOf(settlement.Production, building.BuildingId);
            progress.amount = AccruedAmount(progress.amount, perHour, seconds);
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

    /// <summary>8시간치까지 모으되 일손이 줄어도 이미 모은 재료는 보존한다.</summary>
    public static float AccruedAmount(float stored, double perHour, double seconds)
    {
        if (float.IsNaN(stored) || float.IsInfinity(stored) || stored < 0) stored = 0;
        if (perHour <= 0 || double.IsNaN(perHour) || double.IsInfinity(perHour)
            || seconds <= 0 || double.IsNaN(seconds)) return stored;
        double capacity = Math.Max(stored, perHour * MaxStoredHours);
        return (float)Math.Min(stored + perHour * seconds / 3600d, capacity);
    }

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
