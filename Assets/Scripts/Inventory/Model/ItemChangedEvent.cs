using UnityEngine;

/// <summary>
/// 아이템 수량 변경 알림에 담기는 정보
/// </summary>
public readonly struct ItemChangedEvent
{
    public readonly ItemDefinition Item;
    public readonly int OldCount;
    public readonly int NewCount;
    public readonly ItemChangeReason Reason;

    public int Delta => NewCount - OldCount;

    public ItemChangedEvent(
        ItemDefinition item,
        int oldCount,
        int newCount,
        ItemChangeReason reason
    )
    {
        Item = item;
        OldCount = oldCount;
        NewCount = newCount;
        Reason = reason;
    }
}
