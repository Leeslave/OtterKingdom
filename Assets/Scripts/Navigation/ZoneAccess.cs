/// <summary>
/// 장소에 갈 수 있는지 = 씬이 준비됐고(ZoneDefinition.IsAvailable) 정착 진행 조건(발전)도 됐는지.
/// 정착 매니저가 없는 테스트 씬에서는 정착 조건을 보지 않는다.
/// </summary>
public static class ZoneAccess
{
    public static bool IsOpen(ZoneDefinition zone)
    {
        if (zone == null || !zone.IsAvailable)
            return false;
        var settlement = SettlementManager.Instance;
        return settlement == null || settlement.IsZoneOpen(zone);
    }

    /// <summary>잠긴 장소 카드에 보일 문구</summary>
    public static string LockedSubtitle(ZoneDefinition zone)
    {
        if (zone.IsAvailable && !string.IsNullOrEmpty(zone.DevelopmentLockedSubtitle))
            return zone.DevelopmentLockedSubtitle;
        return zone.LockedSubtitle;
    }
}
