using System;
using System.Collections.Generic;

/// <summary>
/// 왕국 창고 (순수 C#): 가방 밖에 두는 왕국 자재 (판재 · 석재 블록 · 금속 주괴 · 정밀 부품).
/// 칸이 다 차면 더 넣지 않음 (넣지 못한 몫은 부르는 쪽이 시설에 남김 — 없어지지 않음).
/// 공사 · 계약 · 공동사업은 Reserve로 몫을 잡아 두고 ConsumeReserved로 씀 (기획서 7.2 순서: 예약한 몫이 먼저)
/// </summary>
public class KingdomWarehouse
{
    private readonly IndustrySaveData _data;
    private readonly Func<int> _capacity;

    /// <summary>(아이템 ID, 바뀐 수, 이유). 넣으면 +, 쓰면 −</summary>
    public event Action<string, int, string> Changed;

    public KingdomWarehouse(IndustrySaveData data, Func<int> capacity)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
        _data.warehouse ??= new List<ItemCountSaveData>();
        _data.reserved ??= new List<ItemCountSaveData>();
    }

    public int Capacity => Math.Max(0, _capacity());

    /// <summary>창고에 든 전체 수 (모든 자재 합)</summary>
    public int Total
    {
        get
        {
            int total = 0;
            foreach (var line in _data.warehouse)
                total += Math.Max(0, line.amount);
            return total;
        }
    }

    public int Free => Math.Max(0, Capacity - Total);

    public int Count(string itemId) => Find(_data.warehouse, itemId)?.amount ?? 0;

    public int Reserved(string itemId) => Math.Min(Count(itemId), Find(_data.reserved, itemId)?.amount ?? 0);

    /// <summary>예약하지 않고 바로 쓸 수 있는 수</summary>
    public int Available(string itemId) => Count(itemId) - Reserved(itemId);

    public IEnumerable<(string itemId, int amount)> Items()
    {
        foreach (var line in _data.warehouse)
        {
            if (line.amount > 0)
                yield return (line.itemId, line.amount);
        }
    }

    /// <returns>실제로 넣은 수 (칸이 모자라면 덜 넣음)</returns>
    public int Add(string itemId, int amount, string reason)
    {
        if (string.IsNullOrEmpty(itemId))
            throw new ArgumentException("아이템 ID가 비었어요", nameof(itemId));
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        int added = Math.Min(amount, Free);
        if (added <= 0)
            return 0;
        Line(_data.warehouse, itemId).amount += added;
        Changed?.Invoke(itemId, added, reason);
        return added;
    }

    /// <summary>예약하지 않은 몫에서 한꺼번에 씀. 하나라도 모자라면 아무것도 쓰지 않고 false</summary>
    public bool TryConsume(IReadOnlyList<(string itemId, int amount)> costs, string reason)
    {
        if (costs == null)
            throw new ArgumentNullException(nameof(costs));
        var need = Sum(costs);
        foreach (var pair in need)
        {
            if (Available(pair.Key) < pair.Value)
                return false;
        }
        foreach (var pair in need)
            Take(pair.Key, pair.Value, reason);
        return true;
    }

    /// <summary>쓸 몫을 잡아 둠 (예약하지 않은 몫이 모자라면 false)</summary>
    public bool Reserve(string itemId, int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (Available(itemId) < amount)
            return false;
        if (amount > 0)
            Line(_data.reserved, itemId).amount += amount;
        return true;
    }

    public void Release(string itemId, int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        var line = Find(_data.reserved, itemId);
        if (line == null)
            return;
        line.amount = Math.Max(0, line.amount - amount);
        if (line.amount == 0)
            _data.reserved.Remove(line);
    }

    /// <summary>예약해 둔 몫을 씀 (예약보다 많이는 못 씀)</summary>
    public bool ConsumeReserved(string itemId, int amount, string reason)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (Reserved(itemId) < amount)
            return false;
        Release(itemId, amount);
        Take(itemId, amount, reason);
        return true;
    }

    private void Take(string itemId, int amount, string reason)
    {
        if (amount <= 0)
            return;
        var line = Line(_data.warehouse, itemId);
        line.amount -= amount;
        if (line.amount <= 0)
            _data.warehouse.Remove(line);
        Changed?.Invoke(itemId, -amount, reason);
    }

    private static Dictionary<string, int> Sum(IReadOnlyList<(string itemId, int amount)> costs)
    {
        var need = new Dictionary<string, int>();
        foreach (var (itemId, amount) in costs)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(costs), "비용은 0 이상");
            if (amount == 0)
                continue;
            need.TryGetValue(itemId, out int sum);
            need[itemId] = sum + amount;
        }
        return need;
    }

    private static ItemCountSaveData Find(List<ItemCountSaveData> list, string itemId) => list.Find(l => l.itemId == itemId);

    private static ItemCountSaveData Line(List<ItemCountSaveData> list, string itemId)
    {
        var line = Find(list, itemId);
        if (line == null)
            list.Add(line = new ItemCountSaveData { itemId = itemId });
        return line;
    }
}
