/// <summary>
/// 정착 진행 때문에 아직 할 수 없는 퀘스트를 숨긴다 (밭이 열리기 전의 수확 퀘스트 등).
/// 퀘스트 데이터는 그대로 두고, 목표 종류에 필요한 발전이 열렸는지만 본다. 정착 매니저가 없으면 다 보인다.
/// </summary>
public static class SettlementQuestGate
{
    // 목표 종류 → 필요한 발전 (SettlementSetup의 건설 결과 ID, 지역의 생산 발전 ID)
    public const string FarmDevelopment = "farmland";
    public const string FishingDevelopment = "fishing_dock";
    // 요정이 광장에 오면 상점이 열림 (광부를 배치하면 초대 — 밭보다 먼저)
    public const string FairyDevelopment = "fairy_arrived";
    // 농부·광부·낚시꾼이 일하기 시작하면 열림 (그 전에는 수확·채굴·낚시를 할 수 없음)
    public const string FarmProductionDevelopment = "farm_working";
    public const string MineProductionDevelopment = "mine_working";
    public const string FishingProductionDevelopment = "fishing_working";

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
            case QuestGoalType.UnlockFurrow:
                return FarmProductionDevelopment;
            case QuestGoalType.Mine:
                return MineProductionDevelopment;
            case QuestGoalType.Catch:
                return FishingProductionDevelopment;
            // 요정 상점은 요정이 광장에 오면 열림 (모종 같은 밭 상품은 밭이 열린 뒤 레벨로 판매)
            case QuestGoalType.ShopPurchase:
                return FairyDevelopment;
            // 장난감은 요정 상점에서 사므로 상점이 나오기 전에는 놓을 수 없음
            case QuestGoalType.PlaceDecor:
                return FairyDevelopment;
            // 도감은 새 해달을 만나도(해달 칸) 채워져 언제든 할 수 있음 (작물·물고기 칸은 밭·낚시터가 열리면 더해짐).
            // 예전엔 밭 뒤로 숨겼는데, 밭이 Lv.10으로 가며 도감 시작(Lv.2) · 수집가(Lv.5)가 Lv.10까지 안 보였음
            case QuestGoalType.CollectionRegister:
                return null;
            default:
                return null;
        }
    }
}
