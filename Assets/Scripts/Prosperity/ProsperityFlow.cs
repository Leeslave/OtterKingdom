public enum ProsperityFlowStep { None, Castle, Intro, Sawmill, FirstPlank, Plan, Warehouse }

/// <summary>과거 세이브와 신규 진행이 같은 원본 기록으로 다음 행동을 고른다.</summary>
public static class ProsperityFlow
{
    public static ProsperityFlowStep Next(ProsperityResearch research, int level, bool gasRemoved, bool castleBuilt,
        bool firstPlank, bool planCompleted)
    {
        if (!research.Data.introCompleted)
        {
            if (level < 20 || !gasRemoved) return ProsperityFlowStep.None;
            return castleBuilt ? ProsperityFlowStep.Intro : ProsperityFlowStep.Castle;
        }
        if (!research.HasTech(TechIds.Sawmill)) return ProsperityFlowStep.Sawmill;
        if (!firstPlank) return ProsperityFlowStep.FirstPlank;
        if (!planCompleted) return ProsperityFlowStep.Plan;
        if (!research.HasTech(TechIds.Warehouse)) return ProsperityFlowStep.Warehouse;
        return ProsperityFlowStep.None;
    }

    public static bool CanPresent(bool inPlaza, bool loaded, bool busy, bool noticesPending) =>
        inPlaza && loaded && !busy && !noticesPending;
}
