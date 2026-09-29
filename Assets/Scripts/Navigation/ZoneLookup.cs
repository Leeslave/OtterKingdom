using System.Collections.Generic;

/// <summary>
/// 장소 목록에서 찾기/정렬 (UI 없이 테스트 가능하도록 분리)
/// </summary>
public static class ZoneLookup
{
    /// <returns>씬 이름이 같은 장소. 없으면 null (테스트 씬 등)</returns>
    public static ZoneDefinition FindByScene(IEnumerable<ZoneDefinition> zones, string sceneName)
    {
        if (zones == null || string.IsNullOrEmpty(sceneName))
            return null;

        foreach (var zone in zones)
        {
            if (zone != null && zone.SceneName == sceneName)
                return zone;
        }
        return null;
    }

    /// <returns>빈 칸을 뺀 정렬된 새 목록 (SortOrder → ZoneId)</returns>
    public static List<ZoneDefinition> Sorted(IEnumerable<ZoneDefinition> zones)
    {
        var result = new List<ZoneDefinition>();
        if (zones == null)
            return result;

        foreach (var zone in zones)
        {
            if (zone != null)
                result.Add(zone);
        }

        result.Sort((a, b) =>
        {
            int order = a.SortOrder.CompareTo(b.SortOrder);
            return order != 0 ? order : string.CompareOrdinal(a.ZoneId, b.ZoneId);
        });
        return result;
    }
}
