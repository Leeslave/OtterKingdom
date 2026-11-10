using System;
using System.Collections.Generic;

/// <summary>B P0 메인 표. 레벨/재화 곡선과 옛 퀘스트 ID는 변경하지 않는다.</summary>
public static class ProsperityQuestPlan
{
    public const string TransitionId = "__prosperity_main_start_v1";
    public sealed class Row
    {
        public readonly int Level, Step, Goal;
        public readonly string Title, Description, Target;
        public readonly QuestGoalType Type;
        public string Id => $"prosperity_main_{Level}_{Step}";
        public Row(int level, int step, string title, string description, string target, int goal = 1,
            QuestGoalType type = QuestGoalType.ProsperityRecord)
        { Level = level; Step = step; Title = title; Description = description; Target = target; Goal = goal; Type = type; }
    }

    public static readonly IReadOnlyList<Row> Rows = Array.AsReadOnly(new[]
    {
        new Row(23, 1, "돌을 다듬는 기술", "석재 규격화(I02) 연구 완료하기", "tech:I02"),
        new Row(23, 2, "첫 석재 블록", "채석 작업소에서 석재 블록 처음 만들기", "first_stone_block", type: QuestGoalType.Milestone),
        new Row(23, 3, "함께 세운 계획", "번영 계획 수립 공동사업 완료하기", "prosperity_plan_completed", type: QuestGoalType.Milestone),
        new Row(23, 4, "장터의 문", "장터 주문(E01) 연구 완료하기", "tech:E01"),
        new Row(23, 5, "신선한 첫 납품", "장터의 신선한 당근 주문 완료하기", "market:market_carrot_01"),
        new Row(24, 1, "수레가 다니는 왕국", "운송 수레(L02) 연구 완료하기", "tech:L02"),
        new Row(24, 2, "첫 운송 경로", "일터의 가공 화면에서 수레 경로 한 곳 켜기", "carts"),
        new Row(24, 3, "반듯한 첫 판재", "목공소에서 판재 처음 만들기", "first_plank", type: QuestGoalType.Milestone),
        new Row(24, 4, "주택 자재 준비", "왕국 창고에 판재 6개 모으기", "warehouse:plank", 6),
        new Row(24, 5, "상인의 포장 재료", "상인의 포장 목재 주문 완료하기", "market:market_wood_01"),
        new Row(25, 1, "마을을 정비할 때", "마을 정비(C01) 연구 완료하기", "tech:C01"),
        new Row(25, 2, "더 편안한 집", "주거 개량 1차(C02) 연구 완료하기", "tech:C02"),
        new Row(25, 3, "첫 개량 주택", "기존 작은 집 한 채 개량하기", "first_improved_house", type: QuestGoalType.Milestone),
        new Row(25, 4, "왕국 자재로 꾸미기", "창고 자재로 왕국 표지판이나 가로등 만들기", "first_town_craft", type: QuestGoalType.Milestone),
        new Row(25, 5, "풍요로운 텃밭", "작물 30개 수확하기", "", 30, QuestGoalType.Harvest),
        new Row(26, 1, "공동 창고의 기반", "공동 창고(L01) 연구 완료하기", "tech:L01"),
        new Row(26, 2, "장터의 신뢰", "초도 장터 주문 2개 완료하기", "markets", 2),
        new Row(26, 3, "넉넉한 먹거리", "작물 60개 수확하기", "", 60, QuestGoalType.Harvest),
        new Row(26, 4, "바다의 먹거리", "물고기 10마리 낚기", "", 10, QuestGoalType.Catch),
        new Row(26, 5, "왕국의 살림", "판매로 1,000골드 벌기", "", 1000, QuestGoalType.EarnFromSales),
        new Row(27, 1, "일곱 가지 발전", "초기 기술 7개 연구 완료하기", "techs", 7),
        new Row(27, 2, "다음 공사를 위한 판재", "왕국 창고에 판재 12개 모으기", "warehouse:plank", 12),
        new Row(27, 3, "튼튼한 석재 비축", "왕국 창고에 석재 블록 6개 모으기", "warehouse:stone_block", 6),
        new Row(27, 4, "달라진 마을 풍경", "가구 2개 놓기", "", 2, QuestGoalType.PlaceDecor),
        new Row(27, 5, "번영을 향한 일상", "오늘의 일일 퀘스트 보상 1개 받기", "", 1, QuestGoalType.CompleteDaily),
    });

    public static bool IsNew(string id) => id != null && id.StartsWith("prosperity_main_", StringComparison.Ordinal);

    public static int LegacyLevel(string id)
    {
        if (id == null || !id.StartsWith("main_lv", StringComparison.Ordinal)) return 0;
        int end = id.IndexOf('_', 7);
        return end > 7 && int.TryParse(id.Substring(7, end - 7), out int level) ? level : 0;
    }

    /// <summary>일부 진행/미수령/수령한 레벨은 옛 체인으로 끝낸다. Lv.28+는 전환하지 않는다.</summary>
    public static int ChooseStart(int level, IReadOnlyDictionary<string, QuestRecord> records)
    {
        if (level >= 28) return 0;
        int start = Math.Max(23, level);
        foreach (var pair in records)
        {
            int legacy = LegacyLevel(pair.Key);
            if (legacy >= 23 && (pair.Value.Progress > 0 || pair.Value.Claimed)) start = Math.Max(start, legacy + 1);
        }
        return start <= 27 ? start : 0;
    }

    public static int Share(int total, int step) => step == 5 ? total - total / 5 * 4 : total / 5;
}
