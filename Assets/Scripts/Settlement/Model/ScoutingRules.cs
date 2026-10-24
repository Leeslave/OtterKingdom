using System;

public partial class Settlement
{
    public ScoutingProgress Scouting { get; private set; } = new ScoutingProgress();
}

public static class ScoutingRules
{
    public const string MapJoined = "island_map_joined";

    public static bool UsesScouting(Settlement settlement) => settlement.Scouting.Version >= 1;

    // 첫 개간은 지도 해달을 기다리지 않는다. 이관한 세이브의 진행 중인 땅도 다시 잠그지 않는다.
    public static bool CanClear(SettlementConfig config, Settlement settlement, TerritoryExpansionDefinition territory) =>
        !UsesScouting(settlement) || TerritoryRules.IsTutorial(territory, settlement)
        || settlement.Scouting.HasDiscovered(territory.ExpansionId);

    public static bool CanScout(SettlementConfig config, Settlement settlement, TerritoryExpansionDefinition territory) =>
        territory != null && settlement.HasDevelopment(MapJoined)
        && !settlement.Scouting.HasDiscovered(territory.ExpansionId)
        && territory == TerritoryRules.Current(config, settlement, territory.Direction)
        && !TerritoryRules.IsTutorial(territory, settlement);

    public static void Migrate(SettlementConfig config, Settlement settlement)
    {
        if (UsesScouting(settlement)) return;
        foreach (var territory in config.Territories)
        {
            if (territory == null) continue;
            bool started = TerritoryRules.IsExpanded(territory, settlement);
            for (int i = 0; i < territory.Clearings.Count; i++)
            {
                var step = territory.Clearings[i];
                started |= TerritoryRules.IsClearingDone(territory, i, settlement)
                    || (step?.Task != null && settlement.TryGetTaskJob(step.Task.TaskId, out _));
            }
            if (started || (territory.Direction == TerritoryDirection.South && territory.Tier == 1
                && settlement.HasDevelopment("fishing_found")))
                settlement.Scouting.Discover(territory.ExpansionId);
        }
        settlement.Scouting.MarkMigrated();
    }

    public static TimeSpan Duration(TerritoryExpansionDefinition territory) =>
        TimeSpan.FromSeconds(territory.Tier == 1 ? 90 : territory.Tier * 180);

    public static string Hint(TerritoryExpansionDefinition territory) => territory.Direction == TerritoryDirection.South && territory.Tier == 1
        ? "바닷바람 너머에 바닷가와 낡은 선착장이 있을 것 같아요."
        : territory.Direction == TerritoryDirection.NorthWest ? "나무와 돌이 풍부해요. 생산 일터를 마련하기 좋은 땅이에요."
        : "새 집이나 밭을 마련할 땅을 찾아봐요.";
}
