/// <summary>
/// 정착 진행 때문에 아직 할 수 없는 퀘스트를 숨긴다 (밭이 열리기 전의 수확 퀘스트 등).
/// 퀘스트 데이터는 그대로 두고, 목표 종류에 필요한 발전이 열렸는지만 본다. 정착 매니저가 없으면 다 보인다.
/// </summary>
public static class SettlementQuestGate
{
    // 목표 종류 → 필요한 발전 (SettlementSetup의 건설 결과 ID, 지역의 생산 발전 ID)
    public const string FarmDevelopment = "farmland";
    public const string FishingDevelopment = "fishing_dock";
    // 농부·광부가 일하기 시작하면 열림 (그 전에는 수확·채굴을 할 수 없음)
    public const string FarmProductionDevelopment = "farm_working";
    public const string MineProductionDevelopment = "mine_working";

    public static bool IsReachable(QuestDefinition quest)
    {
        var settlement = SettlementManager.Instance;
        if (settlement == null || !settlement.IsLoaded)
            return true;

        string required = RequiredDevelopment(quest);
        return string.IsNullOrEmpty(required) || settlement.HasDevelopment(required);
    }

    /// <summary>이 퀘스트를 하려면 열려 있어야 하는 발전 (없으면 null)</summary>
    public static string RequiredDevelopment(QuestDefinition quest)
    {
        switch (quest.GoalType)
        {
            case QuestGoalType.Harvest:
                return FarmProductionDevelopment;
            case QuestGoalType.Mine:
                return MineProductionDevelopment;
            case QuestGoalType.Catch:
                return FishingDevelopment;
            // 요정 상점은 밭이 열릴 때 광장에 나옴
            case QuestGoalType.ShopPurchase:
                return FarmDevelopment;
            // 도감의 작물·물고기 칸은 밭이 열려야 채울 수 있음 (탭을 정한 해달 만나기는 언제든)
            case QuestGoalType.CollectionRegister:
                return quest.CollectionTab != null ? null : FarmDevelopment;
            default:
                return null;
        }
    }
}
