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
            || (quest.GoalType == QuestGoalType.Catch && e.Reason == ItemChangeReason.Fishing)
            || (quest.GoalType == QuestGoalType.Mine && e.Reason == ItemChangeReason.Mining)
            || (quest.GoalType == QuestGoalType.Gather && e.Reason == ItemChangeReason.Gather);
        if (!matchesReason)
            return 0;
        if (quest.Item != null && quest.Item != e.Item)
            return 0;

        return quest.ItemFilter == null || quest.ItemFilter.Contains(e.Item) ? e.Delta : 0;
    }

    /// <summary>
    /// 건설 완료: 다 지은 건물 기록 중 이 퀘스트가 세는 수 (건물 목록 필터, 건설 해달 참여).
    /// 기록으로 다시 세는 값이라 진행 수치를 이 값으로 맞춘다 (더하지 않음)
    /// </summary>
    public static int CountBuildings(QuestDefinition quest, System.Collections.Generic.IEnumerable<ConstructionDefinition> completed)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        if (completed == null) throw new ArgumentNullException(nameof(completed));
        if (quest.GoalType != QuestGoalType.CompleteConstruction)
            return 0;

        int count = 0;
        foreach (var construction in completed)
        {
            if (construction == null || construction.TargetType == ConstructionTarget.Clearing)
                continue;
            if (quest.BuilderOnly && !construction.NeedsBuilder)
                continue;
            if (quest.Constructions.Count > 0 && !Contains(quest.Constructions, construction))
                continue;
            count++;
        }
        return count;
    }

    private static bool Contains(System.Collections.Generic.IReadOnlyList<ConstructionDefinition> list, ConstructionDefinition target)
    {
        foreach (var c in list)
        {
            if (c == target)
                return true;
        }
        return false;
    }

    /// <summary>판매로 번 골드</summary>
    public static int From(QuestDefinition quest, ItemSoldEvent e)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));

        return quest.GoalType == QuestGoalType.EarnFromSales ? Math.Max(0, e.TotalPrice) : 0;
    }

    /// <summary>생산 업그레이드: 밭·낚싯대·곡괭이 강화 비용을 낸 거래 1번 = 1회</summary>
    public static int From(QuestDefinition quest, CurrencyChange change)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));

        if (quest.GoalType != QuestGoalType.Upgrade || change.Delta >= 0)
            return 0;

        return change.Source == TransactionSource.FarmUpgrade
            || change.Source == TransactionSource.RodUpgrade
            || change.Source == TransactionSource.PickaxeUpgrade ? 1 : 0;
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

    /// <summary>
    /// 받은 퀘스트를 목록 아래 완료 칸에 남길지. 레벨을 한 챕터처럼: 이번 레벨에 받은 것만 남고 레벨이 오르면 비워진다.
    /// 일일 퀘스트는 받은 기록이 매일 초기화되므로 오늘 받았으면 남긴다.
    /// 받은 레벨을 모르는 옛 기록(0)은 남기지 않는다.
    /// </summary>
    public static bool ShowsAsCompleted(QuestDefinition quest, int level, QuestLog log)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        if (log == null) throw new ArgumentNullException(nameof(log));

        if (log.GetStatus(quest) != QuestStatus.Claimed)
            return false;
        return quest.Kind == QuestKind.Daily || log.GetClaimedLevel(quest) == level;
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
