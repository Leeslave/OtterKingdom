using System;
using System.Collections.Generic;

/// <summary>
/// 해금 조건 여러 개를 한꺼번에 다룬다 (UI 없이 테스트 가능하도록 분리).
/// </summary>
public static class UnlockRules
{
    /// <summary>조건을 모두 만족하는지 (비용은 내지 않음). 빈 칸은 만족하지 않은 것으로 본다</summary>
    public static bool AllMet(IEnumerable<UnlockRequirement> requirements, UnlockContext context)
    {
        if (requirements == null)
            throw new ArgumentNullException(nameof(requirements));

        foreach (var requirement in requirements)
        {
            if (requirement == null || !requirement.IsMet(context))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 모두 만족하는지 먼저 확인한 뒤에 차례로 채운다 (비용 지불).
    /// 확인을 먼저 해야 첫 비용만 내고 둘째 조건에서 실패하는 일이 없다.
    /// </summary>
    /// <returns>모두 채웠으면 true. 하나라도 모자라면 아무것도 바꾸지 않고 false</returns>
    public static bool TryFulfillAll(IReadOnlyList<UnlockRequirement> requirements, UnlockContext context)
    {
        if (!AllMet(requirements, context))
            return false;

        foreach (var requirement in requirements)
        {
            if (!requirement.TryFulfill(context))
                throw new InvalidOperationException($"조건을 확인했는데 채우지 못했습니다: {requirement.name}");
        }
        return true;
    }
}
