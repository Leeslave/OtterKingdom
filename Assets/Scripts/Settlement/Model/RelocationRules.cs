using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>이사할 수 있는 구역 건물 하나 (광장 격자 배치 ID · 들어갈 구역 · 차지한 칸, 세이브 좌표)</summary>
public readonly struct RelocationCandidate
{
    public int InstanceId { get; }
    public DistrictType Type { get; }
    public RectInt Area { get; }

    public RelocationCandidate(int instanceId, DistrictType type, RectInt area)
    {
        InstanceId = instanceId;
        Type = type;
        Area = area;
    }
}

/// <summary>건물이 들어갈 수 있는 빈 부지 칸 하나</summary>
public readonly struct RelocationSlot
{
    public DistrictBlockDefinition Block { get; }
    public DistrictSlot Slot { get; }

    public RelocationSlot(DistrictBlockDefinition block, DistrictSlot slot)
    {
        Block = block;
        Slot = slot;
    }
}

/// <summary>
/// 이사 지시서 규칙 (구역·이사 4단계, 순수 C#).
/// 지시서 = 맞지 않는 구역에 있는 건물 중 같은 용도끼리 1~MaxPerOrder채. 그 용도의 블록에 들어갈 빈 부지 칸이 있는 건물만
/// (칸을 차례로 맡겨 보며 셈 → 지시서는 늘 끝낼 수 있음). 맞지 않는 건물이 가장 많은 용도부터.
/// 끝남 = 지시서의 건물이 모두 맞는 구역에 있고 이사 중이 아님 (없어진 건물은 끝낸 것으로)
/// </summary>
public static class RelocationRules
{
    public const int MaxPerOrder = 3;

    /// <summary>건물이 맞는 구역(열린 칸 안 · 같은 용도)에 있는지</summary>
    public static bool Fits(SettlementConfig config, Settlement settlement, RelocationCandidate candidate)
    {
        if (candidate.Type == DistrictType.None)
            return false;
        var block = DistrictRules.BlockAt(config, settlement, candidate.Area);
        return block != null && DistrictRules.TypeOf(block, settlement) == candidate.Type;
    }

    /// <summary>이 크기의 건물이 들어갈 이 용도의 빈 부지 칸들 (블록 순서 → 작은 칸부터)</summary>
    public static List<RelocationSlot> FreeSlots(SettlementConfig config, Settlement settlement, DistrictType type, Vector2Int footprint, IEnumerable<RectInt> occupied)
    {
        var taken = occupied != null ? new List<RectInt>(occupied) : new List<RectInt>();
        var list = new List<RelocationSlot>();
        foreach (var block in config.DistrictBlocks)
        {
            int open = DistrictRules.OpenStages(block, settlement);
            if (open == 0 || DistrictRules.TypeOf(block, settlement) != type)
                continue;
            var slots = new List<DistrictSlot>();
            foreach (var slot in block.Slots)
            {
                if (slot != null && slot.Stage < open && DistrictRules.Fits(slot, footprint) && !taken.Exists(rect => rect.Overlaps(slot.Area)))
                    slots.Add(slot);
            }
            slots.Sort((a, b) => (a.Area.width * a.Area.height).CompareTo(b.Area.width * b.Area.height));
            foreach (var slot in slots)
                list.Add(new RelocationSlot(block, slot));
        }
        return list;
    }

    /// <param name="candidates">놓인 구역 건물 (다 지은 것만)</param>
    /// <param name="occupied">광장에 놓인 모든 물건이 차지한 칸 (세이브 좌표)</param>
    /// <returns>새 지시서 (옮길 건물이 없으면 null)</returns>
    public static RelocationOrder PickOrder(SettlementConfig config, Settlement settlement, IEnumerable<RelocationCandidate> candidates,
        IEnumerable<RectInt> occupied, int serial)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (settlement == null) throw new ArgumentNullException(nameof(settlement));
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));

        var sorted = new List<RelocationCandidate>(candidates);
        sorted.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));
        var byType = new Dictionary<DistrictType, List<int>>();
        var reserved = new Dictionary<DistrictType, List<RectInt>>();
        var baseTaken = occupied != null ? new List<RectInt>(occupied) : new List<RectInt>();
        foreach (var c in sorted)
        {
            if (c.Type == DistrictType.None || settlement.IsRelocating(c.InstanceId) || Fits(config, settlement, c))
                continue;
            if (!reserved.TryGetValue(c.Type, out var mine))
                reserved[c.Type] = mine = new List<RectInt>();
            var taken = new List<RectInt>(baseTaken);
            taken.AddRange(mine);
            var free = FreeSlots(config, settlement, c.Type, c.Area.size, taken);
            if (free.Count == 0)
                continue;
            mine.Add(free[0].Slot.Area);
            if (!byType.TryGetValue(c.Type, out var ids))
                byType[c.Type] = ids = new List<int>();
            ids.Add(c.InstanceId);
        }
        if (byType.Count == 0)
            return null;

        var best = DistrictType.None;
        int bestCount = 0;
        foreach (var pair in byType)
        {
            // 많은 용도부터, 같으면 앞 번호
            if (pair.Value.Count > bestCount || (pair.Value.Count == bestCount && pair.Key < best))
            {
                best = pair.Key;
                bestCount = pair.Value.Count;
            }
        }
        var chosen = byType[best];
        if (chosen.Count > MaxPerOrder)
            chosen.RemoveRange(MaxPerOrder, chosen.Count - MaxPerOrder);
        return new RelocationOrder(serial, best, chosen);
    }

    /// <summary>지시서에서 끝낸 건물 수 (맞는 구역 · 이사 끝남, 없어진 건물도 끝낸 것으로)</summary>
    public static int DoneCount(SettlementConfig config, Settlement settlement, RelocationOrder order, IEnumerable<RelocationCandidate> placed)
    {
        if (order == null) throw new ArgumentNullException(nameof(order));
        var byId = new Dictionary<int, RelocationCandidate>();
        if (placed != null)
        {
            foreach (var p in placed)
                byId[p.InstanceId] = p;
        }
        int done = 0;
        foreach (var id in order.InstanceIds)
        {
            if (!byId.TryGetValue(id, out var c) || (!settlement.IsRelocating(id) && Fits(config, settlement, c)))
                done++;
        }
        return done;
    }

    public static bool IsDone(SettlementConfig config, Settlement settlement, RelocationOrder order, IEnumerable<RelocationCandidate> placed) =>
        DoneCount(config, settlement, order, placed) >= order.InstanceIds.Count;

    /// <summary>지시서 보상 골드</summary>
    public static int RewardGold(SettlementConfig config, RelocationOrder order) =>
        order == null ? 0 : order.InstanceIds.Count * config.RelocationGoldPerBuilding;
}
