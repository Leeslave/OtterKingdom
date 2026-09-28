using System;
using System.Collections.Generic;

/// <summary>
/// 한 탭에 보여줄 슬롯 순서를 계산한다 (UI 없이 테스트 가능하도록 분리).
/// [이 카테고리 아이템] + [빈 칸: 공유 용량의 남은 칸] + [잠긴 칸: 최대 용량까지]
/// </summary>
public static class InventorySlotLayout
{
    public static void Build(Inventory inventory, ItemCategory category, List<SlotData> result)
    {
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));
        if (result == null) throw new ArgumentNullException(nameof(result));

        result.Clear();

        if (category != null)
        {
            foreach (var pair in inventory.Counts)
            {
                if (category.Contains(pair.Key))
                    result.Add(SlotData.ForItem(pair.Key, pair.Value));
            }
        }

        // Dictionary 순서는 보장되지 않으므로 직접 정렬: 카테고리순(전체 탭용) → 희귀한 것 먼저 → ID순
        result.Sort(CompareItems);

        for (int i = 0; i < inventory.FreeSlots; i++)
            result.Add(SlotData.Empty);

        for (int i = 0; i < inventory.MaxCapacity - inventory.Capacity; i++)
            result.Add(SlotData.Locked);
    }

    private static int CompareItems(SlotData a, SlotData b)
    {
        int orderA = a.Item.Category != null ? a.Item.Category.SortOrder : int.MaxValue;
        int orderB = b.Item.Category != null ? b.Item.Category.SortOrder : int.MaxValue;

        if (orderA != orderB)
            return orderA.CompareTo(orderB);

        int tierA = a.Item.Rarity != null ? a.Item.Rarity.Tier : 0;
        int tierB = b.Item.Rarity != null ? b.Item.Rarity.Tier : 0;

        if (tierA != tierB)
            return tierB.CompareTo(tierA);

        return string.CompareOrdinal(a.Item.ItemId, b.Item.ItemId);
    }
}
