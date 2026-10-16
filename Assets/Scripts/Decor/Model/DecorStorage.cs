using System;
using System.Collections.Generic;

/// <summary>
/// 보관함 규칙 (UI 없이 테스트 가능하도록 분리): 가방 개수와 여러 장소에 놓인 개수로 남은 수를 계산한다.
/// </summary>
public static class DecorStorage
{
    /// <summary>보관함에 남은 개수 = 가진 개수 − 모든 격자에 놓인 개수 (0 아래로는 안 내려감)</summary>
    public static int Available(int owned, IEnumerable<DecorLayout> layouts, DecorDefinition decor)
    {
        if (layouts == null) throw new ArgumentNullException(nameof(layouts));

        return Math.Max(0, owned - PlacedCount(layouts, decor));
    }

    public static int PlacedCount(IEnumerable<DecorLayout> layouts, DecorDefinition decor)
    {
        if (layouts == null) throw new ArgumentNullException(nameof(layouts));
        if (decor == null) throw new ArgumentNullException(nameof(decor));

        int count = 0;
        foreach (var layout in layouts)
            count += layout.PlacedCount(decor);
        return count;
    }

    /// <summary>
    /// 가진 개수보다 많이 놓여 있으면(판매·삭제 등으로 가방에서 빠짐) 넘치는 만큼 치운다.
    /// 격자마다 나중에 놓은 것부터, 앞 격자부터 치운다.
    /// </summary>
    /// <returns>치운 개수</returns>
    public static int RemoveExcess(int owned, IEnumerable<DecorLayout> layouts, DecorDefinition decor)
    {
        int excess = PlacedCount(layouts, decor) - owned;
        int removed = 0;
        foreach (var layout in layouts)
        {
            if (excess <= 0)
                break;

            var placed = new List<PlacedDecor>(layout.Placed);
            placed.Sort((a, b) => b.InstanceId.CompareTo(a.InstanceId));
            foreach (var p in placed)
            {
                if (excess <= 0)
                    break;
                if (p.Decor == decor && layout.Remove(p.InstanceId))
                {
                    excess--;
                    removed++;
                }
            }
        }
        return removed;
    }
}
