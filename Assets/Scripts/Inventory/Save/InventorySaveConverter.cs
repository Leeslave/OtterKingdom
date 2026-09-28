using System;
using System.Collections.Generic;

/// <summary>
/// Inventory ↔ 세이브 형식(List&lt;ItemStack&gt;, SaveData.inventory) 변환.
/// Inventory 모델은 세이브 타입을 모르도록 변환은 여기서만 한다.
/// </summary>
public static class InventorySaveConverter
{
    /// <summary>
    /// 가방 내용을 저장 형식으로. 획득 순서대로 쓰므로 복원해도 최신순 정렬이 유지된다.
    /// 불러올 때 ID를 몰라서 보관해 둔 항목도 뒤에 그대로 다시 쓴다.
    /// </summary>
    public static void Write(Inventory inventory, IReadOnlyList<ItemStack> unknownStacks, List<ItemStack> result)
    {
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));
        if (result == null) throw new ArgumentNullException(nameof(result));

        result.Clear();

        var items = new List<ItemDefinition>(inventory.Counts.Keys);
        items.Sort((a, b) => inventory.GetAcquiredOrder(a).CompareTo(inventory.GetAcquiredOrder(b)));
        foreach (var item in items)
            result.Add(new ItemStack(item.ItemId, inventory.GetCount(item)));

        if (unknownStacks != null)
        {
            foreach (var stack in unknownStacks)
                result.Add(new ItemStack(stack.itemId, stack.quantity));
        }
    }

    /// <summary>
    /// 저장된 내용을 빈 가방에 복원한다. 용량을 넘어도 버리지 않는다.
    /// DB에 없는 ID(삭제됐거나 아직 ItemDefinition이 없는 아이템)는 unknownStacks에 모아 두고,
    /// Write 때 그대로 다시 써서 데이터가 사라지지 않게 한다.
    /// </summary>
    public static void Read(IEnumerable<ItemStack> stacks, ItemDatabase database, Inventory inventory, List<ItemStack> unknownStacks)
    {
        if (stacks == null) throw new ArgumentNullException(nameof(stacks));
        if (database == null) throw new ArgumentNullException(nameof(database));
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));
        if (unknownStacks == null) throw new ArgumentNullException(nameof(unknownStacks));
        if (inventory.UsedSlots > 0)
            throw new InvalidOperationException("세이브는 빈 가방에만 불러올 수 있습니다.");

        unknownStacks.Clear();

        // 같은 ID가 여러 번 저장돼 있으면 합침 (처음 나온 위치의 순서 유지)
        var order = new List<string>();
        var totals = new Dictionary<string, int>();
        foreach (var stack in stacks)
        {
            if (stack == null || string.IsNullOrEmpty(stack.itemId) || stack.quantity <= 0)
                continue;

            if (totals.TryGetValue(stack.itemId, out int total))
            {
                totals[stack.itemId] = (int)Math.Min((long)total + stack.quantity, int.MaxValue);
            }
            else
            {
                totals.Add(stack.itemId, stack.quantity);
                order.Add(stack.itemId);
            }
        }

        foreach (var id in order)
        {
            if (database.TryGet(id, out var item))
                inventory.LoadItem(item, totals[id]);
            else
                unknownStacks.Add(new ItemStack(id, totals[id]));
        }
    }
}
