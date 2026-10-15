using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>광장에 놓인 물건 하나 (옛 세이브 옮기기 계획용, 자리는 세이브 좌표)</summary>
public readonly struct PlacedForMigration
{
    public int InstanceId { get; }
    public DistrictType District { get; }
    public RectInt Area { get; }

    public PlacedForMigration(int instanceId, DistrictType district, RectInt area)
    {
        InstanceId = instanceId;
        District = district;
        Area = area;
    }
}

/// <summary>
/// 옛 세이브 옮기기 (정착 세이브 버전 5, 구역·이사 2단계): 광장이 남동쪽 끝에서 지도 가운데 공터로 옮겨 갔다.
/// - 영토: 옛 서쪽 · 북쪽 단계의 진행(넓힌 단계 수 · 개간 수)을 새 방향 단계에 차례로 옮긴다 (북동 → 서 → 남 → 북서, 1단계부터).
///   보상은 다시 주지 않는다. 진행 중이던 옛 개간은 끝난 것으로 셈 (보낸 해달은 쉼)
/// - 구역: "이사까지 끝난 것"으로 처리 — 열린 블록마다 예시 용도를 정한다 (그 용도의 시설이 아직이면 비워 둠)
/// - 광장 물건: 열린 땅 밖에 있는 것을 옮긴다. 구역 건물은 맞는 구역의 빈 부지 칸, 나머지는 옛 광장에서의 자리를 공터로 그대로 옮기고
///   (겹치거나 공터를 벗어나면 가까운 빈자리) — PlanDecor
/// </summary>
public static class CenterStartMigration
{
    public const int Version = 5;

    /// <summary>P3의 광장 동쪽 확장 (옛 구조에서도 없어짐): 옛 서쪽 1단계를 넓힌 것과 같음</summary>
    public const string LegacyPlazaExpandDevelopment = "plaza_expand_01";

    /// <summary>옛 시작 광장 (세이브 좌표): 지금은 바닷가</summary>
    public static readonly RectInt OldStart = new RectInt(-1, -5, 27, 39);

    /// <summary>옛 광장 물건을 공터로 옮길 때 그대로 미는 칸 수 (게시판 · 바위 · 나무 묶음과 같은 거리)</summary>
    public static readonly Vector2Int StartShift = new Vector2Int(-38, 31);

    // 옛 단계: (방향, 단계, 개간 수)
    private static readonly (string direction, int tier)[] OldTiers =
    {
        ("west", 1), ("west", 2), ("west", 3), ("north", 1), ("north", 2),
    };

    private static readonly Regex OldClearing = new Regex(@"^territory_(west|north)_(\d)_c(\d)$");
    private static readonly Regex OldTask = new Regex(@"^task_territory_(west|north)_(\d)_(\d)$");

    /// <summary>옛 진행을 옮길 새 단계 순서</summary>
    public static readonly TerritoryDirection[] Order =
    {
        TerritoryDirection.NorthEast, TerritoryDirection.West, TerritoryDirection.South, TerritoryDirection.NorthWest,
    };

    #region 영토 · 구역

    /// <returns>옮겼으면 true (버전 5 이상이거나 옛 진행이 없으면 false)</returns>
    public static bool MigrateTerritory(SettlementConfig config, Settlement settlement, int fromVersion)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        if (fromVersion >= Version)
            return false;

        // 옛 진행: 넓힌 단계 수 + 넓히지 못한 단계의 개간 수 (진행 중이던 개간 포함)
        var expandedOld = new HashSet<(string, int)>();
        foreach (var (direction, tier) in OldTiers)
        {
            if (settlement.HasDevelopment($"territory_{direction}_{tier}"))
                expandedOld.Add((direction, tier));
        }
        if (settlement.HasDevelopment(LegacyPlazaExpandDevelopment))
            expandedOld.Add(("west", 1));
        var clearedOld = new HashSet<(string, int, int)>();
        foreach (var id in settlement.Developments)
            AddOldClearing(OldClearing.Match(id), expandedOld, clearedOld);
        foreach (var id in settlement.CompletedTaskIds)
            AddOldClearing(OldTask.Match(id), expandedOld, clearedOld);
        foreach (var job in new List<SettlementTaskJob>(settlement.TaskJobs))
        {
            var match = OldTask.Match(job.TaskId);
            if (!match.Success)
                continue;
            settlement.FinishTask(job.TaskId, null);
            AddOldClearing(match, expandedOld, clearedOld);
        }
        int expanded = expandedOld.Count;
        int clearings = clearedOld.Count;
        if (expanded == 0 && clearings == 0)
            return false;

        // 새 단계에 차례로: 앞 expanded 단계는 다 넓힘, 그다음 단계에 남은 개간
        foreach (var territory in NewOrder(config))
        {
            if (expanded > 0)
            {
                CompleteTier(territory, settlement);
                expanded--;
                continue;
            }
            int count = Math.Min(clearings, territory.Clearings.Count);
            for (int i = 0; i < count; i++)
                settlement.UnlockDevelopment(territory.ClearingDevelopment(i + 1));
            break;
        }
        TerritoryRules.Sync(config, settlement);
        return true;
    }

    /// <summary>열린 블록마다 예시 용도를 정함 ("이사까지 끝난 것") — 넓힌 차례대로, 고를 수 없으면(시설 전 · 겹침) 비워 둠</summary>
    /// <returns>정한 블록</returns>
    public static List<DistrictBlockDefinition> DesignateOpened(SettlementConfig config, Settlement settlement)
    {
        var designated = new List<DistrictBlockDefinition>();
        foreach (var direction in Order)
        {
            var tiers = TerritoryRules.InDirection(config, direction);
            if (tiers.Count == 0)
                continue;
            foreach (var block in config.DistrictBlocks)
            {
                if (block == null || block.IsFixed || block.RequiredDevelopment != tiers[0].ExpandedDevelopment)
                    continue;
                if (DistrictRules.Designate(config, settlement, block, block.SuggestedType) == DistrictDesignateBlock.None)
                    designated.Add(block);
            }
        }
        return designated;
    }

    // 새 단계 순서: 1단계를 방향 순서대로 → 2단계 → 3단계
    private static List<TerritoryExpansionDefinition> NewOrder(SettlementConfig config)
    {
        var byDirection = new List<List<TerritoryExpansionDefinition>>();
        int maxTier = 0;
        foreach (var direction in Order)
        {
            var list = TerritoryRules.InDirection(config, direction);
            byDirection.Add(list);
            maxTier = Math.Max(maxTier, list.Count);
        }
        var order = new List<TerritoryExpansionDefinition>();
        for (int t = 0; t < maxTier; t++)
        {
            foreach (var list in byDirection)
            {
                if (t < list.Count)
                    order.Add(list[t]);
            }
        }
        return order;
    }

    private static void CompleteTier(TerritoryExpansionDefinition territory, Settlement settlement)
    {
        for (int i = 0; i < territory.Clearings.Count; i++)
            settlement.UnlockDevelopment(territory.ClearingDevelopment(i + 1));
        settlement.UnlockDevelopment(territory.ClearedDevelopment);
        var mission = territory.Mission;
        if (mission != null)
        {
            settlement.MarkProjectCompleted(mission.ProjectId);
            settlement.AddReward(CommunityProjectRules.RewardKey(mission, 0));
            if (!string.IsNullOrEmpty(mission.PaidDevelopment))
                settlement.UnlockDevelopment(mission.PaidDevelopment);
        }
        settlement.UnlockDevelopment(territory.ExpandedDevelopment);
    }

    private static void AddOldClearing(Match match, HashSet<(string, int)> expanded, HashSet<(string, int, int)> cleared)
    {
        if (!match.Success)
            return;
        string direction = match.Groups[1].Value;
        int tier = int.Parse(match.Groups[2].Value);
        if (!expanded.Contains((direction, tier)))
            cleared.Add((direction, tier, int.Parse(match.Groups[3].Value)));
    }

    #endregion

    #region 광장 물건

    /// <summary>
    /// 열린 땅 밖에 놓인 물건을 옮길 자리 (세이브 좌표 왼쪽 아래 칸). 구역 건물 먼저: 맞는 구역의 빈 부지 칸 →
    /// 없으면 (구역 없음 · 나머지 물건) 옛 광장 자리를 StartShift만큼 민 자리 → 겹치거나 공터 밖이면 공터의 가까운 빈자리.
    /// 열린 땅 = 열린 구역 블록의 칸 (중앙 공터 포함). 옮길 곳이 없으면 그대로 둔다
    /// </summary>
    public static Dictionary<int, Vector2Int> PlanDecor(SettlementConfig config, Settlement settlement, IReadOnlyList<PlacedForMigration> placed)
    {
        var moves = new Dictionary<int, Vector2Int>();
        var start = StartBlock(config);
        if (start == null || placed == null)
            return moves;

        var occupied = new Dictionary<int, RectInt>();
        foreach (var p in placed)
            occupied[p.InstanceId] = p.Area;

        // 구역 건물이 먼저 부지 칸을 고름
        var order = new List<PlacedForMigration>(placed);
        order.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
        foreach (var p in order)
        {
            if (DistrictRules.BlockAt(config, settlement, p.Area) != null)
                continue;
            occupied.Remove(p.InstanceId);
            var size = p.Area.size;
            Vector2Int? target = null;

            if (p.District != DistrictType.None && p.District != DistrictType.Central)
            {
                foreach (var block in config.DistrictBlocks)
                {
                    if (DistrictRules.TypeOf(block, settlement) != p.District)
                        continue;
                    var slot = DistrictRules.FreeSlot(block, size, occupied.Values, DistrictRules.OpenStages(block, settlement));
                    if (slot != null)
                    {
                        target = DistrictRules.OriginIn(slot, size);
                        break;
                    }
                }
            }
            if (target == null)
            {
                var startArea = start.Stages[0].Areas[0];
                var wish = OldStart.Contains(p.Area.position) ? p.Area.position + StartShift
                    : new Vector2Int((int)startArea.center.x - size.x / 2, (int)startArea.center.y - size.y / 2);
                target = NearestFree(start, size, wish, occupied.Values);
            }

            if (target == null)
            {
                occupied[p.InstanceId] = p.Area;
                continue;
            }
            moves[p.InstanceId] = target.Value;
            occupied[p.InstanceId] = new RectInt(target.Value, size);
        }
        return moves;
    }

    /// <summary>중앙 공터 블록 (정해진 용도 = 중앙)</summary>
    public static DistrictBlockDefinition StartBlock(SettlementConfig config)
    {
        foreach (var block in config.DistrictBlocks)
        {
            if (block != null && block.FixedType == DistrictType.Central && block.Stages.Count > 0 && block.Stages[0].Areas.Count > 0)
                return block;
        }
        return null;
    }

    // 원하는 자리에서 가까운 순으로, 공터 첫 단계 안에 다 들어가고 아무것과도 겹치지 않는 자리
    private static Vector2Int? NearestFree(DistrictBlockDefinition start, Vector2Int size, Vector2Int wish, IEnumerable<RectInt> occupied)
    {
        var taken = new List<RectInt>(occupied);
        var bounds = start.Stages[0].Areas[0];
        int reach = bounds.width + bounds.height;
        for (int r = 0; r <= reach; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                        continue;
                    var area = new RectInt(wish + new Vector2Int(dx, dy), size);
                    if (start.Contains(area, 1) && !taken.Exists(t => t.Overlaps(area)))
                        return area.position;
                }
            }
        }
        return null;
    }

    // 구역 건물(큰 것부터) → 나머지
    private static int Rank(PlacedForMigration p) =>
        (p.District != DistrictType.None && p.District != DistrictType.Central ? 0 : 1000000) - p.Area.width * p.Area.height;

    #endregion
}
