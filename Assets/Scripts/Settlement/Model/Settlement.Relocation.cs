using System;
using System.Collections.Generic;

/// <summary>세이브: 옮기는 중인 건물 하나 (UTC 틱. 부지 칸 ID는 지시서로 고른 칸, 꾸미기 모드에서 옮겼으면 비어 있음)</summary>
[Serializable]
public class RelocationMoveSaveData
{
    public int instanceId;
    public string slotId;
    public long startUtcTicks;
    public long endUtcTicks;
}

/// <summary>세이브: 이사 지시서 (orderSerial 0 = 지금 지시서 없음)</summary>
[Serializable]
public class RelocationSaveData
{
    public int orderSerial;
    public DistrictType orderType;
    public List<int> orderIds = new List<int>();
    public int ordersDone;
    public List<RelocationMoveSaveData> moves = new List<RelocationMoveSaveData>();
}

/// <summary>이사 지시서 한 장: 이 건물들(광장 격자 배치 ID)을 이 용도의 구역으로</summary>
public class RelocationOrder
{
    public int Serial { get; }
    public DistrictType Type { get; }
    public IReadOnlyList<int> InstanceIds => _instanceIds;

    private readonly List<int> _instanceIds;

    public RelocationOrder(int serial, DistrictType type, IEnumerable<int> instanceIds)
    {
        if (instanceIds == null)
            throw new ArgumentNullException(nameof(instanceIds));
        Serial = serial;
        Type = type;
        _instanceIds = new List<int>(instanceIds);
    }
}

/// <summary>옮기는 중인 건물 하나: 격자에서는 이미 새 자리에 있고, 끝나는 시각까지 "이사 중" (제자리로 치지 않음)</summary>
public readonly struct RelocationMove
{
    public int InstanceId { get; }
    public string SlotId { get; }
    public long StartUtcTicks { get; }
    public long EndUtcTicks { get; }

    public RelocationMove(int instanceId, string slotId, long startUtcTicks, long endUtcTicks)
    {
        InstanceId = instanceId;
        SlotId = slotId;
        StartUtcTicks = startUtcTicks;
        EndUtcTicks = endUtcTicks;
    }

    public float Progress(long now) =>
        EndUtcTicks <= StartUtcTicks ? 1f : Math.Min(1f, Math.Max(0f, (float)(now - StartUtcTicks) / (EndUtcTicks - StartUtcTicks)));

    public TimeSpan Remaining(long now) => TimeSpan.FromTicks(Math.Max(0, EndUtcTicks - now));
}

/// <summary>
/// 이사 (구역·이사 4단계): 지금 이사 지시서 · 끝낸 지시서 수 · 옮기는 중인 건물. 기록만 하고, 고르기 · 완료 판정은 RelocationRules.
/// 꺼 둔 동안에도 시간이 흐른다 (끝나는 시각 저장)
/// </summary>
public partial class Settlement
{
    private readonly Dictionary<int, RelocationMove> _relocationMoves = new Dictionary<int, RelocationMove>();

    public RelocationOrder RelocationOrder { get; private set; }
    public int RelocationOrdersDone { get; private set; }
    public IReadOnlyCollection<RelocationMove> RelocationMoves => _relocationMoves.Values;

    public bool IsRelocating(int instanceId) => _relocationMoves.ContainsKey(instanceId);

    public bool TryGetRelocation(int instanceId, out RelocationMove move) => _relocationMoves.TryGetValue(instanceId, out move);

    public void StartRelocation(int instanceId, string slotId, long startUtcTicks, long endUtcTicks)
    {
        if (endUtcTicks < startUtcTicks)
            throw new ArgumentException("끝나는 시각이 시작보다 빠릅니다.", nameof(endUtcTicks));
        _relocationMoves[instanceId] = new RelocationMove(instanceId, slotId, startUtcTicks, endUtcTicks);
        OnChanged?.Invoke();
    }

    /// <summary>끝나는 시각이 지난 이사를 끝냄</summary>
    /// <returns>방금 끝난 건물 (배치 ID)</returns>
    public List<int> FinishRelocations(long nowUtcTicks)
    {
        var done = new List<int>();
        foreach (var move in _relocationMoves.Values)
        {
            if (move.EndUtcTicks <= nowUtcTicks)
                done.Add(move.InstanceId);
        }
        foreach (var id in done)
            _relocationMoves.Remove(id);
        if (done.Count > 0)
            OnChanged?.Invoke();
        return done;
    }

    /// <summary>없어진 건물의 이사 기록을 지움</summary>
    public void ForgetRelocation(int instanceId)
    {
        if (_relocationMoves.Remove(instanceId))
            OnChanged?.Invoke();
    }

    public void SetRelocationOrder(RelocationOrder order)
    {
        RelocationOrder = order;
        OnChanged?.Invoke();
    }

    public void CompleteRelocationOrder()
    {
        if (RelocationOrder == null)
            return;
        RelocationOrder = null;
        RelocationOrdersDone++;
        OnChanged?.Invoke();
    }

    private void LoadRelocation(SettlementSaveData saved)
    {
        RelocationOrder = null;
        RelocationOrdersDone = 0;
        _relocationMoves.Clear();
        var save = saved.relocation;
        if (save == null)
            return;
        RelocationOrdersDone = Math.Max(0, save.ordersDone);
        if (save.orderSerial > 0 && save.orderIds != null && save.orderIds.Count > 0 && Enum.IsDefined(typeof(DistrictType), save.orderType))
            RelocationOrder = new RelocationOrder(save.orderSerial, save.orderType, save.orderIds);
        if (save.moves == null)
            return;
        foreach (var m in save.moves)
        {
            if (m != null && m.endUtcTicks >= m.startUtcTicks)
                _relocationMoves[m.instanceId] = new RelocationMove(m.instanceId, m.slotId, m.startUtcTicks, m.endUtcTicks);
        }
    }

    private void WriteRelocation(SettlementSaveData result)
    {
        var save = new RelocationSaveData { ordersDone = RelocationOrdersDone };
        if (RelocationOrder != null)
        {
            save.orderSerial = RelocationOrder.Serial;
            save.orderType = RelocationOrder.Type;
            save.orderIds.AddRange(RelocationOrder.InstanceIds);
        }
        var moves = new List<RelocationMove>(_relocationMoves.Values);
        moves.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));
        foreach (var m in moves)
            save.moves.Add(new RelocationMoveSaveData { instanceId = m.InstanceId, slotId = m.SlotId, startUtcTicks = m.StartUtcTicks, endUtcTicks = m.EndUtcTicks });
        result.relocation = save;
    }
}
