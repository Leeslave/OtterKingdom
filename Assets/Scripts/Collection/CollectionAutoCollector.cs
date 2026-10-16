using System;

/// <summary>
/// 가방 변화 → 도감 자동 획득 규칙 (UI 없이 테스트 가능하도록 분리).
/// 연결된 아이템을 직접 생산해서(수확, 낚시, 채굴) 얻었을 때만 그 항목을 획득한다.
/// 구매·지급·세이브 복원으로 늘어난 것은 해금 조건이 아니다 (기획: 도감_이벤트컷씬_기획서 1.1).
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

        if (!IsDiscovery(e.Reason))
            return false;

        var entry = database.FindByItem(e.Item);
        return entry != null && collection.Collect(entry);
    }

    /// <summary>직접 생산해서 얻은 경우인지 (테스트 씬의 지급 버튼은 개발용으로 허용)</summary>
    public static bool IsDiscovery(ItemChangeReason reason)
    {
        switch (reason)
        {
            case ItemChangeReason.Harvest:
            case ItemChangeReason.Fishing:
            case ItemChangeReason.Mining:
            case ItemChangeReason.Test:
                return true;
            default:
                return false;
        }
    }

    /// <summary>지금 가방에 있는 아이템을 모두 획득 처리 (도감이 생기기 전의 세이브를 위해 — 작물·물고기는 수확·낚시로만 가방에 들어오므로 규칙과 어긋나지 않음)</summary>
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
