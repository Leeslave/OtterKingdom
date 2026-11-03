using System.Collections.Generic;

/// <summary>번영의 시대 도시 규칙 (순수 C#): 개량한 집 장부 · 창고 자재 비용 묶음</summary>
public static class TownRules
{
    public static bool IsImproved(IndustrySaveData data, int instanceId) => data.improvedHouses.Contains(instanceId);

    /// <returns>이번에 개량으로 바뀌었나 (이미 개량했으면 false)</returns>
    public static bool Improve(IndustrySaveData data, int instanceId)
    {
        if (data.improvedHouses.Contains(instanceId))
            return false;
        data.improvedHouses.Add(instanceId);
        return true;
    }

    /// <summary>창고에서 낼 자재 (0개는 뺌)</summary>
    public static List<(string itemId, int amount)> Costs(string firstId, int first, string secondId, int second)
    {
        var costs = new List<(string, int)>();
        if (first > 0)
            costs.Add((firstId, first));
        if (second > 0)
            costs.Add((secondId, second));
        return costs;
    }
}
