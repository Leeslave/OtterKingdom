/// <summary>
/// 구역 진행 표시 (구역·이사 5막 · 밭 재배치): 퀘스트 · 부탁이 볼 수 있게 구역 상태를 발전으로 남긴다.
/// - district_first / district_second: 용도를 정한 블록이 1 · 2곳 이상 (메인 Lv.7 · 8)
/// - farm_discovered: 왕국 Lv.10(FarmDiscoverLevel) 이상 + 농업 지역이 생김 → 해달이가 밭 자리를 발견 (게시판 "먹거리를 길러요", 이동 지도 밭이 열림, 메인 Lv.10)
/// - 광부를 배치했는데 요정이 아직 초대되지 않은 옛 세이브 → 요정 초대 (요정이 농부 대신 광부 뒤에 옴)
/// 한 번 열린 발전은 닫지 않는다 (용도를 바꿔도 밭은 그대로)
/// </summary>
public partial class SettlementManager
{
    public const string DistrictFirstDevelopment = "district_first";
    public const string DistrictSecondDevelopment = "district_second";
    public const string FarmDiscoveredDevelopment = "farm_discovered";
    private const string MineWorkingDevelopment = "mine_working";
    private bool _agriHintShown;

    // TickRelocation과 같은 간격 (TickCastle 옆에서 부름)
    private void TickDistrictMilestones()
    {
        int designated = DistrictRules.DesignatedCount(_config, Settlement);
        bool changed = false;
        if (designated >= 1 && !Settlement.HasDevelopment(DistrictFirstDevelopment))
        {
            Settlement.UnlockDevelopment(DistrictFirstDevelopment);
            changed = true;
        }
        if (designated >= 2 && !Settlement.HasDevelopment(DistrictSecondDevelopment))
        {
            Settlement.UnlockDevelopment(DistrictSecondDevelopment);
            changed = true;
        }
        // 이미 밭을 연 옛 세이브(농경지 개간 끝)는 조용히 발견 처리 (밭 이동이 이 발전을 봄)
        if (!Settlement.HasDevelopment(FarmDiscoveredDevelopment) && Settlement.HasDevelopment(SettlementQuestGate.FarmDevelopment))
        {
            Settlement.UnlockDevelopment(FarmDiscoveredDevelopment);
            changed = true;
        }
        // 밭은 Lv.10 (FarmDiscoverLevel): 농업 지역을 일찍 정했으면 그 레벨이 되는 순간 발견
        if (!Settlement.HasDevelopment(FarmDiscoveredDevelopment) && PlayerLevel >= _config.FarmDiscoverLevel
            && DistrictRules.HasDistrict(_config, Settlement, DistrictType.Agricultural))
        {
            Settlement.UnlockDevelopment(FarmDiscoveredDevelopment);
            GameNotices.Post(new GameNotice("해달이: 농업 지역 너머에 밭을 만들 땅이 있어요! 게시판을 봐요"));
            changed = true;
        }
        // Lv.10에 농업 지역이 없으면 한 번 안내 (개간 기회가 모자라 새 땅이 없을 수 있어 — 정한 땅을 바꾸면 됨). 세션마다 한 번
        if (!_agriHintShown && IsAdministrator && PlayerLevel >= _config.FarmDiscoverLevel
            && !Settlement.HasDevelopment(FarmDiscoveredDevelopment) && !DistrictRules.HasDistrict(_config, Settlement, DistrictType.Agricultural))
        {
            _agriHintShown = true;
            GameNotices.Post(new GameNotice("해달이: 밭을 만들 땅이 필요해요!\n마을회관 → 구역 정하기에서 땅 하나를 농업 지역으로 정해 줘요. 이미 정한 땅도 바꿀 수 있어요"));
        }
        string invited = _config.FairyInvitedDevelopment;
        if (!string.IsNullOrEmpty(invited) && Settlement.HasDevelopment(MineWorkingDevelopment) && !Settlement.HasDevelopment(invited) && !IsFairyArrived)
        {
            Settlement.UnlockDevelopment(invited);
            changed = true;
        }
        if (changed)
            SaveRequested?.Invoke();
    }
}
