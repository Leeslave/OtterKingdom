/// <summary>
/// 성 승격 (구역·이사 기획안 5막): 왕국 Lv.16(SettlementConfig.CastleLevel) 뒤 마을 길드(가스 사건 뒤 마을회관을 넓힘)가 있고
/// 질서도가 성 기준(CastleOrder, 80)에 닿으면 castle_ready가 열려
/// 게시판에 "왕국의 성" 큰 부탁(req_build_castle)이 붙고, 짓고 나면 castle_built (마을 길드 그림이 성으로).
/// 한번 열린 준비는 질서도가 다시 내려가도 닫지 않는다
/// </summary>
public static class CastleRules
{
    public const string Ready = "castle_ready";
    public const string Built = "castle_built";
    public const string RequestId = "req_build_castle";
    /// <summary>마을 길드 (req_upgrade_town_hall → con_town_hall). 성은 이 그림을 넓혀 세움 (광산 기획 14장)</summary>
    public const string GuildBuilt = "town_hall_built";

    /// <summary>지금 성 준비를 열어야 하는지 (행정 해달 뒤 · 아직 안 열림 · 질서도 기준 · 성 레벨 CastleLevel).
    /// 레벨이 없으면 집이 모두 중앙(주거)인 초반에 질서도 80이 Lv.8에 닿아, 길드 지부가 갈 상업 지역보다 성이 먼저 생김</summary>
    public static bool ShouldOpen(SettlementConfig config, Settlement settlement, int orderScore, int playerLevel) =>
        settlement != null && settlement.HasDevelopment(DistrictGuideRules.Promotion) && !settlement.HasDevelopment(Ready)
        && settlement.HasDevelopment(GuildBuilt) && orderScore >= config.CastleOrder && playerLevel >= config.CastleLevel;
}
