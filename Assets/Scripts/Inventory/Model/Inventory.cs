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

    // 가방을 열어 확인하기 전의 새 종류 (가방 버튼 N 표시용). 이미 가진 종류를 더 얻는 건 새 것이 아님
    private readonly HashSet<ItemDefinition> _unseenItems = new HashSet<ItemDefinition>();

    /// <summary>새 종류가 있는지 여부가 바뀔 때 (false → true, true → false)</summary>
    public event Action<bool> OnHasNewItemsChanged;
    public bool HasNewItems => _unseenItems.Count > 0;

    public event Action<int> OnCapacityChanged;

    private int _capacity;
    private readonly int _maxCapacity;

    public int Capacity => _capacity;
    public int MaxCapacity => _maxCapacity;
    // 종류 하나 = 1칸. 칸을 차지하지 않는 아이템(장난감 등)은 세지 않음
    public int UsedSlots
    {
        get
        {
            int used = 0;
            foreach (var item in _counts.Keys)
            {
                if (item.UsesBagCapacity)
                    used++;
            }
            return used;
        }
    }
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

    public bool IsNew(ItemDefinition item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        return _unseenItems.Contains(item);
    }

    /// <summary>가방을 열어 봤을 때 호출 → 모든 새 표시 해제</summary>
    public void MarkAllSeen()
    {
        if (_unseenItems.Count == 0)
            return;

        _unseenItems.Clear();
        OnHasNewItemsChanged?.Invoke(false);
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
        if (oldCount == 0 && item.UsesBagCapacity && FreeSlots <= 0)
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

        bool hadNewItems = HasNewItems;
        if (oldCount == 0)
            _unseenItems.Add(item);

        OnItemChanged?.Invoke(new ItemChangedEvent(item, oldCount, newCount, reason));
        NotifyIfHasNewItemsChanged(hadNewItems);
        return added;
    }

    /// <returns>지금 더 넣을 수 있는 개수. 새 종류인데 빈 칸이 없으면 0 (수확 전에 가방이 꽉 찼는지 확인용)</returns>
    public int GetAddableAmount(ItemDefinition item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        int count = GetCount(item);
        if (count == 0 && item.UsesBagCapacity && FreeSlots <= 0)
            return 0;

        return Math.Max(0, item.MaxStack - count);
    }

    /// <summary>
    /// 세이브 복원용. 용량과 최대 스택을 무시하고 그대로 넣는다 (옛 세이브가 한도를 넘어도 버리지 않음).
    /// 새 아이템(N)으로 표시하지 않는다. 호출 순서가 획득 순서가 된다.
    /// </summary>
    public void LoadItem(ItemDefinition item, int count)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "count는 1 이상이어야 합니다.");
        if (_counts.ContainsKey(item))
            throw new InvalidOperationException($"이미 가방에 있는 아이템입니다: {item.ItemId}");

        _counts[item] = count;
        _acquiredOrder[item] = ++_acquireSequence;

        OnItemChanged?.Invoke(new ItemChangedEvent(item, 0, count, ItemChangeReason.Load));
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

        bool hadNewItems = HasNewItems;
        if (newCount == 0)
        {
            _counts.Remove(item);
            _acquiredOrder.Remove(item);
            _unseenItems.Remove(item); // 확인하기 전에 다 써버린 새 아이템은 표시할 필요 없음
        }
        else
            _counts[item] = newCount;

        OnItemChanged?.Invoke(new ItemChangedEvent(item, oldCount, newCount, reason));
        NotifyIfHasNewItemsChanged(hadNewItems);
        return true;
    }

    // OnItemChanged를 받은 쪽(열려 있는 가방)이 MarkAllSeen을 불렀을 수도 있으므로 지금 상태와 비교
    private void NotifyIfHasNewItemsChanged(bool hadNewItems)
    {
        if (hadNewItems != HasNewItems)
            OnHasNewItemsChanged?.Invoke(HasNewItems);
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
