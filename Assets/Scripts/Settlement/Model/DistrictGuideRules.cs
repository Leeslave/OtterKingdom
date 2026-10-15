using UnityEngine;

/// <summary>행정 해달 승격과 구역 안내 문구. 발전·역할 기록만 사용하므로 옛 세이브도 같은 조건으로 이어 간다.</summary>
public static class DistrictGuideRules
{
    public const string Promotion = "admin_otter_promoted";
    public const string TownHall = "board_upgraded";
    public const string BoardRole = "role_board_manager";
    public const string Receptionist = "otter_receptionist";

    public static bool CanPromote(Settlement settlement) => settlement != null
        && !settlement.HasDevelopment(Promotion) && settlement.HasDevelopment(TownHall)
        && settlement.HasDevelopment("receptionist_assigned")
        && settlement.TryGetRole(BoardRole, out var role) && role.OtterId == Receptionist;

    public static bool Promote(Settlement settlement)
    {
        if (!CanPromote(settlement)) return false;
        settlement.UnlockDevelopment(Promotion);
        return true;
    }

    public static DistrictBlockDefinition FirstDistrictBlock(SettlementConfig config, Settlement settlement)
    {
        foreach (var block in config.DistrictBlocks)
            if (block != null && block.BlockId == DistrictRules.CenterBlockId) return block;
        var pending = DistrictRules.PendingBlocks(config, settlement);
        return pending.Count > 0 ? pending[0] : null;
    }

    public static bool FirstDistrictConfigured(SettlementConfig config, Settlement settlement)
    {
        foreach (var block in config.DistrictBlocks)
            if (block != null && block.BlockId == DistrictRules.CenterBlockId)
                return settlement.GetDistrict(block.BlockId) != DistrictType.None;
        return DistrictRules.DesignatedCount(config, settlement) > 0;
    }

    public static bool NeedsFirstDistrict(SettlementConfig config, Settlement settlement) =>
        settlement.HasDevelopment(Promotion) && !FirstDistrictConfigured(config, settlement)
        && FirstDistrictBlock(config, settlement) is DistrictBlockDefinition first && DistrictRules.IsOpen(first, settlement);

    public static string LockedReason(SettlementConfig config, DistrictType type, DistrictDesignateBlock reason)
    {
        switch (reason)
        {
            case DistrictDesignateBlock.None: return "이 용도로 정할 수 있어요.";
            case DistrictDesignateBlock.TypeLocked:
                var hint = config.FindDistrictTypeUnlock(type)?.LockedHint;
                return string.IsNullOrEmpty(hint) ? "이 용도에 필요한 시설을 먼저 열어 주세요." : hint;
            case DistrictDesignateBlock.FirstDistrict: return "첫 구역은 주거 지역으로 정해 봐요.";
            case DistrictDesignateBlock.TypeTaken: return "이미 다른 땅에 있어요. 나머지 용도를 먼저 정해요.";
            case DistrictDesignateBlock.AlreadySet: return "현재 용도예요. 다른 용도를 골라 바꿀 수 있어요.";
            case DistrictDesignateBlock.Closed: return "숲 개간과 영토 확장을 먼저 마쳐 주세요.";
            case DistrictDesignateBlock.Fixed: return "중앙 공터는 행정 구역이에요.";
            default: return "이 땅에는 정할 수 없는 용도예요.";
        }
    }

    public static string OrderGoal(SettlementConfig config, int score)
    {
        if (score < config.RelocationOrder)
            return $"구역을 정하면 +{config.OrderPerDistrict} · 이사 지시서 기준까지 {config.RelocationOrder - score}";
        if (score < config.CastleOrder)
            return $"이사 지시서 기준 달성 · 성 기준까지 {config.CastleOrder - score}";
        return "성으로 발전할 질서도 기준을 달성했어요!";
    }

    public static Color ColorOf(DistrictType type)
    {
        switch (type)
        {
            case DistrictType.Residential: return new Color(0.40f, 0.72f, 0.95f);
            case DistrictType.Commercial: return new Color(0.98f, 0.69f, 0.32f);
            case DistrictType.Production: return new Color(0.75f, 0.57f, 0.87f);
            case DistrictType.Agricultural: return new Color(0.48f, 0.78f, 0.35f);
            default: return new Color(0.90f, 0.84f, 0.62f);
        }
    }
}
