using System;

/// <summary>
/// 가방 변화 → 도감 자동 획득 규칙 (UI 없이 테스트 가능하도록 분리).
/// 연결된 아이템의 개수가 늘어나면(수확, 낚시, 세이브 복원 등) 그 항목을 획득한다.
/// </summary>
public static class CollectionAutoCollector
{
    /// <returns>이번 변화로 새로 획득한 항목이 있으면 true</returns>
    public static bool Handle(Collection collection, CollectionDatabase database, ItemChangedEvent e)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        if (database == null) throw new ArgumentNullException(nameof(database));

        // 팔거나 쓴 변화는 도감과 무관
        if (e.NewCount <= e.OldCount)
            return false;

        var entry = database.FindByItem(e.Item);
        return entry != null && collection.Collect(entry);
    }

    /// <summary>지금 가방에 있는 아이템을 모두 획득 처리 (도감이 생기기 전의 세이브를 위해)</summary>
    /// <returns>새로 획득한 항목 수</returns>
    public static int CollectOwned(Collection collection, CollectionDatabase database, Inventory inventory)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        if (database == null) throw new ArgumentNullException(nameof(database));
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));

        int count = 0;
        foreach (var item in inventory.Counts.Keys)
        {
            var entry = database.FindByItem(item);
            if (entry != null && collection.Collect(entry))
                count++;
        }
        return count;
    }
}
