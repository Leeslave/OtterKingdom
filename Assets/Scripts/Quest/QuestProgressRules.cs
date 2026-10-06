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

        // 요정 상점은 가방이 아니라 구매 알림(FromShopPurchase)으로 셈 (묶음 상품의 개수가 아니라 산 수량)
        if (quest.GoalType == QuestGoalType.ShopPurchase)
            return 0;

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

    /// <summary>요정 상점 구매: 한 번에 여러 개를 사면 산 수량만큼 (10개 묶음 상품 하나 = 1). 아이템을 정한 퀘스트는 그 아이템만</summary>
    public static int FromShopPurchase(QuestDefinition quest, ItemDefinition bought, int quantity)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        if (quest.GoalType != QuestGoalType.ShopPurchase || quantity <= 0)
            return 0;
        return quest.Item == null || quest.Item == bought ? quantity : 0;
    }

    /// <summary>장소 가 보기: 그 장소(씬)에 도착하면 1</summary>
    public static int FromArrival(QuestDefinition quest, string sceneName)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        return quest.GoalType == QuestGoalType.VisitZone && !string.IsNullOrEmpty(quest.Target) && quest.Target == sceneName ? 1 : 0;
    }

    /// <summary>일일 퀘스트 끝내기: 일일 퀘스트 보상을 받으면 1</summary>
    public static int FromClaimed(QuestDefinition quest, QuestDefinition claimed)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        if (claimed == null) throw new ArgumentNullException(nameof(claimed));
        return quest.GoalType == QuestGoalType.CompleteDaily && claimed.Kind == QuestKind.Daily ? 1 : 0;
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

    /// <summary>
    /// 값을 낸 거래 1번 = 1회: 생산 업그레이드(밭·낚싯대·곡괭이 강화), 고랑 열기, 가방 칸 늘리기
    /// </summary>
    public static int From(QuestDefinition quest, CurrencyChange change)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));

        if (change.Delta >= 0)
            return 0;

        switch (quest.GoalType)
        {
            case QuestGoalType.Upgrade:
                return change.Source == TransactionSource.FarmUpgrade
                    || change.Source == TransactionSource.RodUpgrade
                    || change.Source == TransactionSource.PickaxeUpgrade ? 1 : 0;
            case QuestGoalType.UnlockFurrow:
                return change.Source == TransactionSource.PlotUnlock ? 1 : 0;
            case QuestGoalType.ExpandBag:
                return change.Source == TransactionSource.InventoryExpand ? 1 : 0;
            default:
                return 0;
        }
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

    /// <summary>레벨별 메인 체인 데이터인지 (정착 단계를 세는 메인 퀘스트가 있음)</summary>
    public static bool HasMainChain(System.Collections.Generic.IEnumerable<QuestDefinition> quests)
    {
        if (quests == null) throw new ArgumentNullException(nameof(quests));
        foreach (var quest in quests)
        {
            if (quest != null && quest.Kind == QuestKind.Main && quest.GoalType == QuestGoalType.Milestone)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 메인 체인이 생기기 전 세이브: 지금 레벨보다 낮은 레벨의 메인 퀘스트는 받은 것으로 넘긴다 (보상 없이).
    /// 이미 지나온 레벨의 정착 단계가 한꺼번에 "보상 받기"로 쏟아져 경험치가 다시 들어오지 않게. 지금 레벨의 것부터 이어서 한다
    /// </summary>
    public static int SkipPassedMainQuests(System.Collections.Generic.IEnumerable<QuestDefinition> quests, int level, QuestLog log)
    {
        if (quests == null) throw new ArgumentNullException(nameof(quests));
        if (log == null) throw new ArgumentNullException(nameof(log));

        int skipped = 0;
        foreach (var quest in quests)
        {
            if (quest == null || quest.Kind != QuestKind.Main || quest.RequiredLevel >= level)
                continue;
            if (log.GetStatus(quest) == QuestStatus.Claimed)
                continue;
            log.LoadRecord(quest.QuestId, quest.Goal, true);
            skipped++;
        }
        return skipped;
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
