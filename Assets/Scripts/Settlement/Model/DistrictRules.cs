using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>블록에 용도를 정할 수 없는 이유</summary>
public enum DistrictDesignateBlock
{
    None,          // 정할 수 있음
    Unknown,       // 없는 블록
    Fixed,         // 변경할 수 없는 고정 블록 (중앙 공터는 예외)
    Closed,        // 아직 열리지 않음 (그 방향 숲 개간 · 영토 확장 전)
    AlreadySet,    // 현재와 같은 용도
    NotAssignable, // 고를 수 없는 용도 (없음 · 중앙)
    TypeLocked,    // 그 용도의 시설이 아직 없음 (예: 가게가 생겨야 상업 지역)
    FirstDistrict, // 첫 구역은 정해진 용도로 (튜토리얼: 주거)
    TypeTaken,     // 이전 중복 제한의 값 (호환을 위해 유지, 지금은 같은 용도를 여러 블록에 지정 가능)
}

/// <summary>구역이 있는 건물 하나가 놓인 자리 (질서도 · 보너스 계산용)</summary>
public readonly struct DistrictPlacement
{
    public DistrictType Type { get; }
    /// <summary>차지한 칸 (광장 격자 세이브 좌표)</summary>
    public RectInt Area { get; }
    /// <summary>이사 중 (새 자리에 있지만 아직 맞는 구역으로 치지 않음)</summary>
    public bool Moving { get; }

    public DistrictPlacement(DistrictType type, RectInt area, bool moving = false)
    {
        Type = type;
        Area = area;
        Moving = moving;
    }
}

/// <summary>왕국 질서도 계산 결과 (마을회관 게이지)</summary>
public class DistrictOrder
{
    private readonly int[] _matchingByType = new int[Enum.GetValues(typeof(DistrictType)).Length];

    /// <summary>구역이 있는 건물 수</summary>
    public int Buildings { get; internal set; }
    /// <summary>그중 맞는 구역에 있는 건물 수</summary>
    public int Matching { get; internal set; }
    /// <summary>플레이어가 용도를 정한 블록 수 (지정한 중앙 공터도 포함)</summary>
    public int Designated { get; internal set; }
    /// <summary>0~100</summary>
    public int Score { get; internal set; }

    /// <summary>그 용도의 구역에 맞게 들어선 건물 수</summary>
    public int MatchingOf(DistrictType type)
    {
        int index = (int)type;
        return index >= 0 && index < _matchingByType.Length ? _matchingByType[index] : 0;
    }

    internal void AddMatching(DistrictType type)
    {
        Matching++;
        int index = (int)type;
        if (index >= 0 && index < _matchingByType.Length)
            _matchingByType[index]++;
    }
}

/// <summary>
/// 구역 규칙 (구역·이사 기획, 데이터 + 상태 → 결과). 순수 C#.
/// - 블록: 첫 단계(그 방향 영토 확장 1단계)가 열리면 용도를 고름. 단계가 열릴 때마다 블록 칸 · 부지 칸이 늘어남.
///   중앙 공터는 마을회관 완성 후 첫 주거 튜토리얼에서 지정한다. 지정한 용도는 시설 조건에 맞으면 변경할 수 있다.
///   같은 용도를 여러 블록에 정할 수 있다. 용도는 그 시설이 생겨야 고를 수 있음 (상업 = 가게 등).
/// - 건물이 블록의 열린 칸 안에 다 들어 있고 그 블록의 용도가 건물의 구역과 같으면 "맞는 구역"
/// - 질서도 = 맞는 구역 건물 비율 × 가중치 + 정한 블록 수 × 블록 점수 (0~100). 수치는 설정, 임시
/// - 구역 보너스 = 그 용도의 구역이 있으면 기본 % + 맞게 들어선 건물마다 % (최대 %)
/// </summary>
public static class DistrictRules
{
    public const int MaxOrder = 100;

    #region 블록

    /// <summary>블록의 지금 용도 (정해진 용도 또는 고른 용도, 안 골랐으면 None)</summary>
    public static DistrictType TypeOf(DistrictBlockDefinition block, Settlement settlement)
    {
        if (block == null)
            return DistrictType.None;
        var chosen = settlement.GetDistrict(block.BlockId);
        return IsEditable(block) && chosen != DistrictType.None ? chosen : block.IsFixed ? block.FixedType : chosen;
    }

    public const string CenterBlockId = "district_center";

    // 중앙 공터는 기존 에셋의 행정 기본값을 유지하되, 플레이어가 주거 등으로 지정·변경할 수 있다.
    public static bool IsEditable(DistrictBlockDefinition block) =>
        block != null && (!block.IsFixed || block.BlockId == CenterBlockId);

    public static List<DistrictBlockDefinition> EditableBlocks(SettlementConfig config, Settlement settlement)
    {
        var blocks = new List<DistrictBlockDefinition>();
        foreach (var block in config.DistrictBlocks)
            if (IsEditable(block) && IsOpen(block, settlement)) blocks.Add(block);
        return blocks;
    }

    public static bool IsOpen(DistrictBlockDefinition block, Settlement settlement) => OpenStages(block, settlement) > 0;

    /// <summary>블록의 열린 단계 수 (0 = 닫힘)</summary>
    public static int OpenStages(DistrictBlockDefinition block, Settlement settlement) =>
        block != null ? block.OpenStages(settlement.HasDevelopment) : 0;

    /// <summary>그 용도를 고를 수 있게 됐는지 (그 용도의 시설 발전, 표에 없으면 처음부터)</summary>
    public static bool IsTypeUnlocked(SettlementConfig config, Settlement settlement, DistrictType type)
    {
        var unlock = config.FindDistrictTypeUnlock(type);
        return unlock == null || string.IsNullOrEmpty(unlock.Development) || settlement.HasDevelopment(unlock.Development);
    }

    /// <summary>용도를 정한 변경 가능한 블록 수 (기본 용도만 있는 고정 블록은 제외)</summary>
    public static int DesignatedCount(SettlementConfig config, Settlement settlement)
    {
        int count = 0;
        foreach (var block in config.DistrictBlocks)
        {
            if (IsEditable(block) && settlement.GetDistrict(block.BlockId) != DistrictType.None)
                count++;
        }
        return count;
    }

    /// <summary>그 용도의 구역이 있는지 (정해진 블록 포함)</summary>
    public static bool HasDistrict(SettlementConfig config, Settlement settlement, DistrictType type)
    {
        if (type == DistrictType.None)
            return false;
        foreach (var block in config.DistrictBlocks)
        {
            if (TypeOf(block, settlement) == type)
                return true;
        }
        return false;
    }

    /// <summary>열렸지만 아직 용도를 고르지 않은 블록 ("이 땅을 뭘로 쓸까요?")</summary>
    public static List<DistrictBlockDefinition> PendingBlocks(SettlementConfig config, Settlement settlement)
    {
        var list = new List<DistrictBlockDefinition>();
        foreach (var block in config.DistrictBlocks)
        {
            if (IsEditable(block) && IsOpen(block, settlement) && settlement.GetDistrict(block.BlockId) == DistrictType.None)
                list.Add(block);
        }
        return list;
    }

    /// <summary>이 영역(세이브 좌표)이 열린 칸 안에 다 들어 있는 블록 (없으면 null)</summary>
    public static DistrictBlockDefinition BlockAt(SettlementConfig config, Settlement settlement, RectInt area)
    {
        foreach (var block in config.DistrictBlocks)
        {
            int open = OpenStages(block, settlement);
            if (open > 0 && block.Contains(area, open))
                return block;
        }
        return null;
    }

    #endregion

    #region 용도 정하기

    public static DistrictDesignateBlock CheckDesignate(SettlementConfig config, Settlement settlement, DistrictBlockDefinition block, DistrictType type)
    {
        if (block == null)
            return DistrictDesignateBlock.Unknown;
        if (!IsEditable(block))
            return DistrictDesignateBlock.Fixed;
        if (!IsOpen(block, settlement))
            return DistrictDesignateBlock.Closed;
        if (settlement.GetDistrict(block.BlockId) == type && type != DistrictType.None)
            return DistrictDesignateBlock.AlreadySet;
        if (!Districts.IsAssignable(type))
            return DistrictDesignateBlock.NotAssignable;
        if (!IsTypeUnlocked(config, settlement, type))
            return DistrictDesignateBlock.TypeLocked;
        bool centralFirst = block.BlockId == CenterBlockId && settlement.GetDistrict(block.BlockId) == DistrictType.None;
        if ((centralFirst || DesignatedCount(config, settlement) == 0) && config.FirstDistrict != DistrictType.None && type != config.FirstDistrict)
            return DistrictDesignateBlock.FirstDistrict;
        return DistrictDesignateBlock.None;
    }

    /// <summary>지금 이 블록에 고를 수 있는 용도 (고르기 화면)</summary>
    public static List<DistrictType> Choices(SettlementConfig config, Settlement settlement, DistrictBlockDefinition block)
    {
        var list = new List<DistrictType>();
        foreach (var type in Districts.Assignable)
        {
            if (CheckDesignate(config, settlement, block, type) == DistrictDesignateBlock.None)
                list.Add(type);
        }
        return list;
    }

    /// <returns>정할 수 없으면 그 이유 (아무것도 바꾸지 않음)</returns>
    public static DistrictDesignateBlock Designate(SettlementConfig config, Settlement settlement, DistrictBlockDefinition block, DistrictType type)
    {
        var reason = CheckDesignate(config, settlement, block, type);
        if (reason == DistrictDesignateBlock.None)
            settlement.SetDistrict(block.BlockId, type);
        return reason;
    }

    #endregion

    #region 질서도 · 보너스

    /// <summary>놓인 구역 건물들로 질서도를 계산 (구역이 None인 건물은 빼고 줌)</summary>
    public static DistrictOrder Evaluate(SettlementConfig config, Settlement settlement, IEnumerable<DistrictPlacement> placements)
    {
        var order = new DistrictOrder { Designated = DesignatedCount(config, settlement) };
        if (placements != null)
        {
            foreach (var placement in placements)
            {
                if (placement.Type == DistrictType.None)
                    continue;
                order.Buildings++;
                var block = BlockAt(config, settlement, placement.Area);
                if (!placement.Moving && block != null && TypeOf(block, settlement) == placement.Type)
                    order.AddMatching(placement.Type);
            }
        }
        double ratio = order.Buildings > 0 ? (double)order.Matching / order.Buildings : 0d;
        int score = (int)Math.Round(config.OrderMatchWeight * ratio, MidpointRounding.AwayFromZero) + config.OrderPerDistrict * order.Designated;
        order.Score = Math.Max(0, Math.Min(MaxOrder, score));
        return order;
    }

    /// <summary>이사 지시서가 열리는 질서도인지</summary>
    public static bool CanRelocate(SettlementConfig config, DistrictOrder order) => order != null && order.Score >= config.RelocationOrder;

    /// <summary>마을회관을 성으로 올릴 수 있는 질서도인지</summary>
    public static bool CanBecomeCastle(SettlementConfig config, DistrictOrder order) => order != null && order.Score >= config.CastleOrder;

    /// <summary>구역 보너스 합계 (%) — 그 용도의 구역이 있으면 기본 % + 맞게 들어선 건물마다 % (최대 %)</summary>
    public static int BonusPercent(SettlementConfig config, Settlement settlement, DistrictOrder order, KingdomBonusKind kind)
    {
        int total = 0;
        foreach (var entry in config.DistrictBonuses)
        {
            if (entry == null || entry.Kind != kind || !HasDistrict(config, settlement, entry.District))
                continue;
            int percent = entry.FlatPercent + entry.PerBuildingPercent * (order != null ? order.MatchingOf(entry.District) : 0);
            if (entry.MaxPercent > 0)
                percent = Math.Min(percent, entry.MaxPercent);
            total += percent;
        }
        return total;
    }

    #endregion

    #region 부지 칸

    public static bool Fits(DistrictSlot slot, Vector2Int footprint) =>
        slot != null && footprint.x > 0 && footprint.y > 0 && footprint.x <= slot.Area.width && footprint.y <= slot.Area.height;

    /// <summary>부지 칸에 놓을 때의 왼쪽 아래 칸 (세이브 좌표): 가로 가운데, 아래 맞춤 (집 앞이 길 쪽)</summary>
    public static Vector2Int OriginIn(DistrictSlot slot, Vector2Int footprint) =>
        new Vector2Int(slot.Area.x + (slot.Area.width - footprint.x) / 2, slot.Area.y);

    /// <summary>
    /// 이 크기의 건물이 들어갈 빈 부지 칸 (없으면 null). 들어가는 칸 중 가장 작은 칸 → 블록 순서.
    /// occupied = 이미 놓인 것들이 차지한 칸 (세이브 좌표), openStages = 열린 단계 수 (그 단계까지의 칸만, 음수면 모두)
    /// </summary>
    public static DistrictSlot FreeSlot(DistrictBlockDefinition block, Vector2Int footprint, IEnumerable<RectInt> occupied, int openStages = -1)
    {
        if (block == null)
            return null;
        var taken = occupied != null ? new List<RectInt>(occupied) : new List<RectInt>();
        DistrictSlot best = null;
        foreach (var slot in block.Slots)
        {
            if (slot == null || (openStages >= 0 && slot.Stage >= openStages) || !Fits(slot, footprint) || taken.Exists(rect => rect.Overlaps(slot.Area)))
                continue;
            if (best == null || slot.Area.width * slot.Area.height < best.Area.width * best.Area.height)
                best = slot;
        }
        return best;
    }

    #endregion
}
