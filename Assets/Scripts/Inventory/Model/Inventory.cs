using System;
using System.Collections.Generic;

public class Inventory
{
    public event Action<ItemChangedEvent> OnItemChanged;
    private readonly Dictionary<ItemDefinition, int> _counts =
        new Dictionary<ItemDefinition, int>();

    // UI 목록 표시용
    public IReadOnlyDictionary<ItemDefinition, int> Counts => _counts;

    // 최신순 정렬용: 획득할 때마다 1씩 커지는 번호 (같은 프레임에 여러 개를 얻어도 겹치지 않도록 시간 대신 번호 사용)
    private long _acquireSequence;
    private readonly Dictionary<ItemDefinition, long> _acquiredOrder =
        new Dictionary<ItemDefinition, long>();

    public event Action<int> OnCapacityChanged;

    private int _capacity;
    private readonly int _maxCapacity;

    public int Capacity => _capacity;
    public int MaxCapacity => _maxCapacity;
    public int UsedSlots => _counts.Count; // 종류 하나 = 1칸
    public int FreeSlots => _capacity - UsedSlots;

    public Inventory(int capacity, int maxCapacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "capacity는 1 이상이어야 합니다."
            );
        if (maxCapacity < capacity)
            throw new ArgumentOutOfRangeException(
                nameof(maxCapacity),
                "maxCapacity는 capacity 이상이어야 합니다."
            );

        _capacity = capacity;
        _maxCapacity = maxCapacity;
    }

    public int GetCount(ItemDefinition item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        return _counts.TryGetValue(item, out int count) ? count : 0;
    }

    /// <returns>마지막으로 획득한 순번 (클수록 최근). 가지고 있지 않으면 0</returns>
    public long GetAcquiredOrder(ItemDefinition item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        return _acquiredOrder.TryGetValue(item, out long order) ? order : 0;
    }

    /// <returns> 실제로 추가된 개수 (MaxStack에 걸리면 요청보다 적을 수 있음) </returns>
    public int Add(ItemDefinition item, int amount, ItemChangeReason reason)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "amount는 1 이상이어야 합니다.");

        int oldCount = GetCount(item);

        // 처음 들어오는 종류는 빈 칸이 있어야 함 (이미 가진 종류는 칸을 더 쓰지 않음)
        if (oldCount == 0 && FreeSlots <= 0)
            return 0;

        int space = item.MaxStack - oldCount;
        int added = Math.Min(amount, space);

        // 가득 차서 하나도 못 넣음
        if (added <= 0)
            return 0;

        int newCount = oldCount + added;
        _counts[item] = newCount;

        // 이미 가진 아이템도 갱신 (마지막 획득 순). 이벤트 전에 기록해야 받는 쪽이 새 순서로 정렬함
        _acquiredOrder[item] = ++_acquireSequence;

        OnItemChanged?.Invoke(new ItemChangedEvent(item, oldCount, newCount, reason));
        return added;
    }

    // 몇개 갖고있는지 확인하는 함수
    public bool Has(ItemDefinition item, int amount)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        return GetCount(item) >= amount;
    }

    // 아이템 소모하는 함수
    public bool TryRemove(ItemDefinition item, int amount, ItemChangeReason reason)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "amount는 1 이상이어야 합니다.");

        int oldCount = GetCount(item);

        if (oldCount < amount)
        {
            return false;
        }

        int newCount = oldCount - amount;

        if (newCount == 0)
        {
            _counts.Remove(item);
            _acquiredOrder.Remove(item);
        }
        else
            _counts[item] = newCount;

        OnItemChanged?.Invoke(new ItemChangedEvent(item, oldCount, newCount, reason));
        return true;
    }

    /// <returns>실제로 늘어난 칸 수 (MaxCapacity에 걸리면 요청보다 적을 수 있음)</returns>
    public int ExpandCapacity(int amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "amount는 1 이상이어야 합니다.");

        int room = _maxCapacity - _capacity;
        int expanded = Math.Min(amount, room);

        // 이미 최대 용량 → 변화 없음, 알림도 없음
        if (expanded <= 0)
            return 0;

        _capacity += expanded;

        OnCapacityChanged?.Invoke(_capacity);
        return expanded;
    }
}
