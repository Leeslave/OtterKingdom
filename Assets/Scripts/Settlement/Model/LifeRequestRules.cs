using System;
using System.Collections.Generic;

/// <summary>
/// 생활 의뢰 규칙 (데이터 + 상태 → 결과). 난수 없이 회차로 정해진다: 틀은 목록 순서대로 돌고,
/// 건네주기 틀의 아이템은 후보 중 지금 얻을 수 있는 것을 회차에 따라 차례로 고른다. 얻을 수 있는 것이 없으면 다음 틀로.
/// 저장된 회차를 쓰므로 재접속해도 다시 뽑지 않는다.
/// </summary>
public static class LifeRequestRules
{
    /// <summary>생활 의뢰가 열렸는지 (마을회관 + 레벨)</summary>
    public static bool IsOpen(SettlementConfig config, Settlement settlement, int playerLevel) =>
        config.LifeRequests.Count > 0
        && !string.IsNullOrEmpty(config.LifeRequestDevelopment)
        && settlement.HasDevelopment(config.LifeRequestDevelopment)
        && playerLevel >= config.LifeRequestLevel;

    /// <summary>
    /// 이 회차에 걸 의뢰 (틀 · 아이템 · 개수). 회차의 틀부터 차례로 보아 걸 수 있는 첫 틀.
    /// 걸 수 있는 틀이 하나도 없으면 false
    /// </summary>
    /// <param name="isObtainable">이 아이템을 지금 얻을 수 있는지 (작물: 심을 수 있음, 목재·돌: 광장에서 주울 수 있음)</param>
    /// <param name="excludeTemplate">이미 걸린 의뢰의 틀 (같은 틀이 두 칸에 나란히 걸리지 않게, 다른 틀이 있으면)</param>
    public static bool TryPick(SettlementConfig config, int serial, Func<ItemDefinition, bool> isObtainable,
        string excludeTemplate, out LifeRequestTemplate template, out ItemDefinition item, out int amount)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (isObtainable == null)
            throw new ArgumentNullException(nameof(isObtainable));
        template = null;
        item = null;
        amount = 0;

        var templates = new List<LifeRequestTemplate>();
        foreach (var t in config.LifeRequests)
        {
            if (t != null && !string.IsNullOrEmpty(t.TemplateId))
                templates.Add(t);
        }
        int count = templates.Count;
        if (count == 0)
            return false;

        LifeRequestTemplate fallback = null;
        ItemDefinition fallbackItem = null;
        int start = Math.Max(0, serial) % count;
        for (int i = 0; i < count; i++)
        {
            var candidate = templates[(start + i) % count];
            ItemDefinition chosen = null;
            if (candidate.Kind == LifeRequestKind.Deliver)
            {
                chosen = PickItem(candidate, serial / count, isObtainable);
                if (chosen == null)
                    continue;
            }
            else if (candidate.Task == null)
            {
                continue;
            }

            if (candidate.TemplateId == excludeTemplate)
            {
                if (fallback == null)
                {
                    fallback = candidate;
                    fallbackItem = chosen;
                }
                continue;
            }
            template = candidate;
            item = chosen;
            amount = candidate.Kind == LifeRequestKind.Deliver ? candidate.Amount : 0;
            return true;
        }

        if (fallback == null)
            return false;
        template = fallback;
        item = fallbackItem;
        amount = fallback.Kind == LifeRequestKind.Deliver ? fallback.Amount : 0;
        return true;
    }

    // 후보 중 지금 얻을 수 있는 것을 회차에 따라 차례로
    private static ItemDefinition PickItem(LifeRequestTemplate template, int round, Func<ItemDefinition, bool> isObtainable)
    {
        var obtainable = new List<ItemDefinition>();
        foreach (var item in template.Items)
        {
            if (item != null && isObtainable(item))
                obtainable.Add(item);
        }
        return obtainable.Count == 0 ? null : obtainable[Math.Max(0, round) % obtainable.Count];
    }

    /// <summary>보상 경험치: 받는 순간 레벨에서 다음 레벨까지 필요한 경험치의 % (최소 1)</summary>
    public static int XpFor(LifeRequestTemplate template, int level, ILevelCurve curve)
    {
        if (template == null || template.XpPercentOfLevel <= 0f || curve == null)
            return 0;
        int need = curve.ExpToNext(level);
        return need <= 0 ? 0 : Math.Max(1, (int)Math.Round(need * template.XpPercentOfLevel / 100f));
    }

    /// <summary>의뢰의 말 ({item}·{amount}를 바꿔 넣음)</summary>
    public static string Line(LifeRequestTemplate template, ItemDefinition item, int amount)
    {
        string line = template.Line ?? string.Empty;
        return line.Replace("{item}", item != null ? item.DisplayName : string.Empty).Replace("{amount}", amount.ToString());
    }
}
