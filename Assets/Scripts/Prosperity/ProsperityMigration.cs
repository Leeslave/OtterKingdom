/// <summary>레벨로 완료를 추정하지 않고 기존의 실제 완료 기록만 새 진행에 연결한다.</summary>
public static class ProsperityMigration
{
    public static bool Reconcile(SaveData save)
    {
        bool changed = false;
        if (save.prosperity == null) { save.prosperity = new ProsperitySaveData(); changed = true; }
        changed |= ProsperityResearch.Sanitize(save.prosperity);
        if (save.settlement == null) { save.settlement = new SettlementSaveData(); changed = true; }
        if (save.settlement.unlockedDevelopments == null)
        {
            save.settlement.unlockedDevelopments = new System.Collections.Generic.List<string>();
            changed = true;
        }
        var developments = save.settlement.unlockedDevelopments;
        if (save.mineMaze?.community?.sourceRemoved == true && !developments.Contains("mine_source_removed"))
        {
            developments.Add("mine_source_removed");
            changed = true;
        }
        if (save.settlement.completedRequests != null && save.settlement.completedRequests.Contains(CastleRules.RequestId)
            && !developments.Contains(CastleRules.Built))
        {
            developments.Add(CastleRules.Built);
            changed = true;
        }
        return changed;
    }
}
