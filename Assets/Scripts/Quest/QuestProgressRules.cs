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
    /// 값을 낸 거래 1번 = 1회: 생산 업그레이드(밭·낚싯대·곡괭이 강화), 가방 칸 늘리기.
    /// 고랑 열기는 거래가 아니라 열린 고랑 수로 센다 (FromOpenedFurrows)
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
            case QuestGoalType.ExpandBag:
                return change.Source == TransactionSource.InventoryExpand ? 1 : 0;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 고랑 열기: 처음부터 열린 밭 1을 뺀, 지금 열린 고랑 수가 이 퀘스트의 순번(Target, 1부터) 이상이면 1.
    /// 기록으로 다시 세는 값이라, 메인 체인보다 고랑을 먼저 열어도 퀘스트가 열리자마자 끝난다
    /// (예전에는 퀘스트가 열린 동안 연 고랑만 세서, 미리 다 열면 체인이 영영 막혔다). Target이 비면 첫 고랑
    /// </summary>
    public static int FromOpenedFurrows(QuestDefinition quest, int openedFurrows)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        if (quest.GoalType != QuestGoalType.UnlockFurrow)
            return 0;

        int needed = int.TryParse(quest.Target, out int order) && order > 0 ? order : 1;
        return openedFurrows >= needed ? 1 : 0;
    }

    /// <summary>
    /// 넘친 진행을 나눠 쓰는 종류 (QuestLog.TakeCarry). 메인 · 도전의 이벤트로 세는 목표(수확 · 판매 · 업그레이드 · 광석 …)만,
    /// 종류 · 목표 · 대상 · 아이템 · 분류 · 도감 탭이 같으면 같은 몫. 기록으로 다시 세는 목표 · 일일은 null (넘기지 않음)
    /// </summary>
    public static string CarryKey(QuestDefinition quest)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        if (quest.Kind == QuestKind.Daily || quest.CountsFromRecords)
            return null;
        switch (quest.GoalType)
        {
            case QuestGoalType.VisitZone:
            case QuestGoalType.UnlockFurrow:
            case QuestGoalType.CompleteDaily:
                return null;
        }
        return $"{quest.Kind}|{quest.GoalType}|{quest.Target}|{(quest.Item != null ? quest.Item.name : "")}|" +
               $"{(quest.ItemFilter != null ? quest.ItemFilter.name : "")}|{quest.CollectionTab}";
    }

    /// <summary>업그레이드 목표의 대상이 이 값이면 곡괭이 레벨로 다시 센다 (FromPickaxeLevel)</summary>
    public const string PickaxeTarget = "pickaxe";

    /// <summary>
    /// 곡괭이 강화(대상 "pickaxe"): 지금까지 올린 횟수 = 곡괭이 레벨 - 1. 기록으로 다시 세는 값이라,
    /// 곡괭이를 미리 다 올려 둬도(강화는 4번이 끝) 퀘스트가 열리자마자 끝난다 (밭 전 Lv.5 메인이 막히지 않게)
    /// </summary>
    public static int FromPickaxeLevel(QuestDefinition quest, int pickaxeLevel)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        if (quest.GoalType != QuestGoalType.Upgrade || quest.Target != PickaxeTarget)
            return 0;
        return Math.Max(0, pickaxeLevel - 1);
    }

    /// <summary>
    /// 일일 퀘스트 끝내기: 오늘 받은 일일 퀘스트 수. 기록으로 다시 세는 값이라, 이 퀘스트가 열리기 전에 오늘 일일을 받았어도 들어간다
    /// (Lv.9엔 밭 · 낚시가 없어 일일이 "오늘의 장사" 하나뿐 — 먼저 받으면 다음 날까지 막혔음)
    /// </summary>
    public static int FromClaimedDailies(QuestDefinition quest, int claimedToday)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));
        return quest.GoalType == QuestGoalType.CompleteDaily ? Math.Max(0, claimedToday) : 0;
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
        var prerequisite = quest.Prerequisite;
        // 기능 안내(정착 단계 · 장소 가 보기 · 상점 등)는 앞의 모으기 퀘스트(광석 60개 등)를 기다리지 않음 — 레벨이 오르면 바로 뜸
        if (quest.Kind == QuestKind.Main && !IsGrind(quest))
        {
            while (prerequisite != null && prerequisite.Kind == QuestKind.Main && IsGrind(prerequisite))
                prerequisite = prerequisite.Prerequisite;
        }
        return prerequisite == null || log.GetStatus(prerequisite) == QuestStatus.Claimed;
    }

    // 이만큼 이상 모으는 퀘스트는 "모으기" (Lv.1 재료 10개 · 판매 10골드, 첫 당근 3개 같은 짧은 안내는 기능 안내로 봄)
    private const int GrindGoal = 20;

    /// <summary>수확 · 낚시 · 판매 · 채굴 · 줍기를 많이 모으는 퀘스트인지 (기능 안내가 기다리지 않는 것)</summary>
    public static bool IsGrind(QuestDefinition quest)
    {
        if (quest == null) throw new ArgumentNullException(nameof(quest));

        switch (quest.GoalType)
        {
            case QuestGoalType.Harvest:
            case QuestGoalType.Catch:
            case QuestGoalType.EarnFromSales:
            case QuestGoalType.Mine:
            case QuestGoalType.Gather:
                return quest.Goal >= GrindGoal;
            default:
                return false;
        }
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
