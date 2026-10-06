using System;

/// <summary>
/// 왕국의 특성 수 (P4). 주민(Resident)으로 사는 서로 다른 해달만 센다:
/// 방문만 한 해달·정착 후보·역할로 와 있는 해달(집이 없는 건설 해달)은 안 세고, 배치(광산·밭·역할)와는 상관없다
/// </summary>
public static class TraitRules
{
    /// <summary>이 특성을 가진 주민 수</summary>
    public static int Count(SettlementConfig config, Settlement settlement, OtterTrait trait)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));
        if (trait == OtterTrait.None)
            return 0;

        int count = 0;
        foreach (var otterId in settlement.ResidentOrder)
        {
            var otter = config.FindOtter(otterId);
            if (otter != null && otter.Trait == trait && IsSettled(settlement, otterId))
                count++;
        }
        return count;
    }

    /// <summary>특성별 주민 수. 결과[(int)특성], 길이 OtterTraits.Count + 1 (0번 = None은 늘 0)</summary>
    public static int[] CountAll(SettlementConfig config, Settlement settlement)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (settlement == null)
            throw new ArgumentNullException(nameof(settlement));

        var counts = new int[OtterTraits.Count + 1];
        foreach (var otterId in settlement.ResidentOrder)
        {
            var otter = config.FindOtter(otterId);
            int index = otter != null ? (int)otter.Trait : 0;
            if (index > 0 && index < counts.Length && IsSettled(settlement, otterId))
                counts[index]++;
        }
        return counts;
    }

    private static bool IsSettled(Settlement settlement, string otterId) =>
        settlement.TryGetResidentState(otterId, out var state) && state == ResidentState.Resident;
}
