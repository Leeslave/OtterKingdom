using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보관함 규칙 (UI 없이 테스트 가능하도록 분리): 놓은 장난감 · 가구는 가방에서 빠지고, 치우면 가방으로 돌아온다.
/// 그래서 보관함에 남은 개수 = 가방에 있는 개수. 건물은 가방과 상관없다.
/// </summary>
public static class DecorStorage
{
    public static int PlacedCount(IEnumerable<DecorLayout> layouts, DecorDefinition decor)
    {
        if (layouts == null) throw new ArgumentNullException(nameof(layouts));
        if (decor == null) throw new ArgumentNullException(nameof(decor));

        int count = 0;
        foreach (var layout in layouts)
            count += layout.PlacedCount(decor);
        return count;
    }

    /// <summary>가방에서 하나 꺼내 놓는다. 가방에 없으면 NotOwned, 자리가 안 맞으면 가방은 그대로</summary>
    public static DecorPlacementResult TryPlaceFromBag(Inventory bag, DecorLayout layout, DecorDefinition decor, Vector2Int origin,
        DecorRotation rotation, out PlacedDecor placed)
    {
        if (bag == null) throw new ArgumentNullException(nameof(bag));
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (decor == null) throw new ArgumentNullException(nameof(decor));

        placed = null;
        if (decor.Item == null || bag.GetCount(decor.Item) <= 0)
            return DecorPlacementResult.NotOwned;

        var result = layout.Check(decor, origin, rotation);
        if (result != DecorPlacementResult.Ok)
            return result;

        // 가방에서 먼저 빼야 함: 놓인 알림(OnPlaced)을 받는 쪽이 보관함 개수를 다시 셀 때 이미 빠져 있게
        bag.TryRemove(decor.Item, 1, ItemChangeReason.DecorPlace);
        result = layout.TryPlace(decor, origin, rotation, out placed);
        if (result != DecorPlacementResult.Ok)
            bag.Add(decor.Item, 1, ItemChangeReason.DecorReturn);
        return result;
    }

    /// <summary>놓인 장난감 · 가구를 치워 가방에 돌려놓는다. 건물이거나 가방에 자리가 없으면 그대로 두고 false</summary>
    public static bool TryReturnToBag(Inventory bag, DecorLayout layout, int instanceId)
    {
        if (bag == null) throw new ArgumentNullException(nameof(bag));
        if (layout == null) throw new ArgumentNullException(nameof(layout));

        if (!layout.TryGet(instanceId, out var placed) || placed.Decor.IsBuilding || placed.Decor.Item == null)
            return false;
        if (bag.GetAddableAmount(placed.Decor.Item) < 1)
            return false;

        layout.Remove(instanceId);
        bag.Add(placed.Decor.Item, 1, ItemChangeReason.DecorReturn);
        return true;
    }

    /// <summary>
    /// 옛 세이브(놓아도 가방에 남아 있던 때) 격자 하나를 한 번 맞춘다: 놓인 장난감 · 가구만큼 가방에서 뺀다.
    /// 가방에 모자라면 있는 만큼만 뺀다 (놓인 물건은 그대로)
    /// </summary>
    /// <returns>가방에서 뺀 개수</returns>
    public static int TakePlacedOutOfBag(Inventory bag, DecorLayout layout)
    {
        if (bag == null) throw new ArgumentNullException(nameof(bag));
        if (layout == null) throw new ArgumentNullException(nameof(layout));

        var counts = new Dictionary<ItemDefinition, int>();
        foreach (var placed in layout.Placed)
        {
            var item = placed.Decor.IsBuilding ? null : placed.Decor.Item;
            if (item != null)
                counts[item] = counts.TryGetValue(item, out int n) ? n + 1 : 1;
        }

        int removed = 0;
        foreach (var pair in counts)
        {
            int take = Math.Min(pair.Value, bag.GetCount(pair.Key));
            if (take > 0 && bag.TryRemove(pair.Key, take, ItemChangeReason.DecorPlace))
                removed += take;
        }
        return removed;
    }

    /// <summary>세이브에 놓여 있었지만 자리가 맞지 않아 놓지 못한 장난감 · 가구를 가방에 돌려놓는다 (가방 한도는 보지 않음)</summary>
    /// <returns>돌려놓은 개수</returns>
    public static int ReturnSkippedToBag(Inventory bag, IEnumerable<DecorDefinition> skipped)
    {
        if (bag == null) throw new ArgumentNullException(nameof(bag));
        if (skipped == null) throw new ArgumentNullException(nameof(skipped));

        var counts = new Dictionary<ItemDefinition, int>();
        foreach (var decor in skipped)
        {
            var item = decor == null || decor.IsBuilding ? null : decor.Item;
            if (item != null)
                counts[item] = counts.TryGetValue(item, out int n) ? n + 1 : 1;
        }

        int returned = 0;
        foreach (var pair in counts)
        {
            int had = bag.GetCount(pair.Key);
            // 처음 들어오는 종류는 세이브 복원처럼 넣고(칸 · 스택 한도 무시), 이미 있으면 한도까지 더함
            if (had == 0)
            {
                bag.LoadItem(pair.Key, pair.Value);
                returned += pair.Value;
            }
            else
                returned += bag.Add(pair.Key, pair.Value, ItemChangeReason.DecorReturn);
        }
        return returned;
    }
}
