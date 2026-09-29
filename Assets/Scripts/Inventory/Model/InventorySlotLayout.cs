using System;
using System.Collections.Generic;

/// <summary>
/// 한 탭에 보여줄 슬롯 순서를 계산한다 (UI 없이 테스트 가능하도록 분리).
/// [필터에 맞는 아이템(정렬)] + [빈 칸: 공유 용량의 남은 칸] + [잠긴 칸: 최대 용량까지]
/// </summary>
public static class InventorySlotLayout
{
    /// <param name="filter">탭 또는 소분류 칩. 탭이면 그 아래 소분류 아이템까지 포함</param>
    public static void Build(Inventory inventory, ItemCategory filter, ItemSortMode sortMode, List<SlotData> result)
    {
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));
        if (result == null) throw new ArgumentNullException(nameof(result));

        result.Clear();

        if (filter != null)
        {
            foreach (var pair in inventory.Counts)
            {
                if (filter.Contains(pair.Key))
                    result.Add(SlotData.ForItem(pair.Key, pair.Value));
            }
        }

        // Dictionary 순서는 보장되지 않으므로 직접 정렬: 선택한 기준 → 카테고리순 → ID순
        result.Sort((a, b) => CompareItems(inventory, sortMode, a.Item, b.Item));

        for (int i = 0; i < inventory.FreeSlots; i++)
            result.Add(SlotData.Empty);

        for (int i = 0; i < inventory.MaxCapacity - inventory.Capacity; i++)
            result.Add(SlotData.Locked);
    }

    private static int CompareItems(Inventory inventory, ItemSortMode sortMode, ItemDefinition a, ItemDefinition b)
    {
        int result = CompareBySortMode(inventory, sortMode, a, b);
        if (result != 0)
            return result;

        result = CompareCategory(a.Category, b.Category);
        if (result != 0)
            return result;

        return string.CompareOrdinal(a.ItemId, b.ItemId);
    }

    private static int CompareBySortMode(Inventory inventory, ItemSortMode sortMode, ItemDefinition a, ItemDefinition b)
    {
        switch (sortMode)
        {
            case ItemSortMode.Recent:
                return inventory.GetAcquiredOrder(b).CompareTo(inventory.GetAcquiredOrder(a));

            case ItemSortMode.Rarity:
                return GetTier(b).CompareTo(GetTier(a));

            case ItemSortMode.Name:
                // 한글 음절은 유니코드가 가나다순이라 Ordinal로 충분 (기기 언어 설정에 영향받지 않음)
                return string.CompareOrdinal(a.DisplayName ?? "", b.DisplayName ?? "");

            default:
                return 0;
        }
    }

    private static int GetTier(ItemDefinition item)
    {
        return item.Rarity != null ? item.Rarity.Tier : 0;
    }

    // 탭 순서 → 같은 탭 안에서는 소분류 순서. 카테고리가 없으면 맨 뒤
    private static int CompareCategory(ItemCategory a, ItemCategory b)
    {
        if (a == b) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        int result = a.Root.SortOrder.CompareTo(b.Root.SortOrder);
        if (result != 0)
            return result;

        return a.SortOrder.CompareTo(b.SortOrder);
    }
}
