using System;

/// <summary>
/// 레벨 → 도감 해금 규칙 (UI 없이 테스트 가능하도록 분리).
/// 항목의 UnlockLevel(0보다 큼)에 도달했으면 등록한다 (예: 농부 해달 2레벨, 낚시꾼 해달 10레벨).
/// </summary>
public static class CollectionLevelUnlocker
{
    /// <returns>새로 등록한 항목 수</returns>
    public static int Unlock(Collection collection, CollectionDatabase database, int level)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        if (database == null) throw new ArgumentNullException(nameof(database));

        int count = 0;
        foreach (var entry in database.Entries)
        {
            if (entry == null || entry.UnlockLevel <= 0 || level < entry.UnlockLevel)
                continue;

            if (collection.Collect(entry))
                count++;
        }
        return count;
    }
}
