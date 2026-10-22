using System.Collections.Generic;

/// <summary>상단바 아바타에 고를 수 있는 해달: 첫 해달과 정착한(주민이 된) 해달 중 얼굴 그림이 있는 해달</summary>
public static class ProfilePortraitRules
{
    public static List<SettlementOtterDefinition> Choices(SettlementConfig config, Settlement settlement)
    {
        var result = new List<SettlementOtterDefinition>();
        if (config == null)
            return result;
        if (config.FirstOtter != null && config.FirstOtter.Portrait != null)
            result.Add(config.FirstOtter);
        if (settlement == null)
            return result;
        foreach (var otterId in settlement.ResidentOrder)
        {
            var otter = config.FindOtter(otterId);
            if (otter == null || otter.Portrait == null || result.Contains(otter))
                continue;
            if (settlement.TryGetResidentState(otterId, out var state) && state == ResidentState.Resident)
                result.Add(otter);
        }
        return result;
    }

    /// <summary>고른 해달 (없거나 모르는 ID면 첫 해달)</summary>
    public static SettlementOtterDefinition Resolve(SettlementConfig config, string otterId)
    {
        if (config == null)
            return null;
        var otter = string.IsNullOrEmpty(otterId) ? null : config.FindOtter(otterId);
        return otter != null && otter.Portrait != null ? otter : config.FirstOtter;
    }
}
