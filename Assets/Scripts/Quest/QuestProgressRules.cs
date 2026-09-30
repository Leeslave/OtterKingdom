using System;

/// <summary>
/// 게임 알림 → 퀘스트 진행 수치 규칙 (UI 없이 테스트 가능하도록 분리). 해당 없는 알림이면 0.
/// </summary>
public static class QuestProgressRules
{
    /// <summary>수확·낚시: 가방에 늘어난 개수 (세이브 복원, 시작 모종 지급 등은 세지 않음)</summary>
    public static int From(QuestDefinition quest, ItemChangedEvent e)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));

        if (e.Delta <= 0)
            return 0;

        // 요정 상점: 한 번 산 것 = 1 (개수가 아니라 횟수)
        if (quest.GoalType == QuestGoalType.ShopPurchase)
            return e.Reason == ItemChangeReason.Purchase ? 1 : 0;

        bool matchesReason =
            (quest.GoalType == QuestGoalType.Harvest && e.Reason == ItemChangeReason.Harvest)
            || (quest.GoalType == QuestGoalType.Catch && e.Reason == ItemChangeReason.Fishing);
        if (!matchesReason)
            return 0;

        return quest.ItemFilter == null || quest.ItemFilter.Contains(e.Item) ? e.Delta : 0;
    }

    /// <summary>판매로 번 골드</summary>
    public static int From(QuestDefinition quest, ItemSoldEvent e)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));

        return quest.GoalType == QuestGoalType.EarnFromSales ? Math.Max(0, e.TotalPrice) : 0;
    }

    /// <summary>생산 업그레이드: 밭·낚싯대 강화 비용을 낸 거래 1번 = 1회</summary>
    public static int From(QuestDefinition quest, CurrencyChange change)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));

        if (quest.GoalType != QuestGoalType.Upgrade || change.Delta >= 0)
            return 0;

        return change.Source == TransactionSource.FarmUpgrade || change.Source == TransactionSource.RodUpgrade ? 1 : 0;
    }

    /// <summary>장난감 놓기: 놓은 것 하나 = 1 (옮기기는 세지 않음)</summary>
    public static int From(QuestDefinition quest, PlacedDecor placed)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));

        return quest.GoalType == QuestGoalType.PlaceDecor && placed != null ? 1 : 0;
    }

    /// <summary>
    /// 지금 목록에 나타나고 진행이 쌓이는지: 레벨이 되었고, 앞 단계 보상을 받았음
    /// (앞 단계가 일일 퀘스트여도 오늘 받았으면 열림)
    /// </summary>
    public static bool IsAvailable(QuestDefinition quest, int level, QuestLog log)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        if (log == null) throw new ArgumentNullException(nameof(log));

        if (level < quest.RequiredLevel)
            return false;
        return quest.Prerequisite == null || log.GetStatus(quest.Prerequisite) == QuestStatus.Claimed;
    }

    /// <summary>도감 등록: 새로 획득·등록된 항목 1개 = 1 (방문 흔적은 세지 않음)</summary>
    public static int From(QuestDefinition quest, CollectionEntry entry, CollectionState state)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        if (entry == null) throw new ArgumentNullException(nameof(entry));

        if (quest.GoalType != QuestGoalType.CollectionRegister || state != CollectionState.Collected)
            return 0;

        return quest.CollectionTab == null || entry.Tab == quest.CollectionTab ? 1 : 0;
    }
}
