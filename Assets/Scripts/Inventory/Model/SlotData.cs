public enum SlotState { Item, Empty, Locked }

/// <summary>
/// 슬롯 하나에 무엇을 그릴지 (아이템 / 빈 칸 / 잠긴 칸)
/// </summary>
public readonly struct SlotData
{
    public readonly SlotState State;
    public readonly ItemDefinition Item;
    public readonly int Count;

    private SlotData(SlotState state, ItemDefinition item, int count)
    {
        State = state;
        Item = item;
        Count = count;
    }

    public static SlotData ForItem(ItemDefinition item, int count) => new SlotData(SlotState.Item, item, count);
    public static readonly SlotData Empty = new SlotData(SlotState.Empty, null, 0);
    public static readonly SlotData Locked = new SlotData(SlotState.Locked, null, 0);
}
