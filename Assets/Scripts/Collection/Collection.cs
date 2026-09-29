using System;
using System.Collections.Generic;

public enum CollectionState
{
    Unknown,   // 미획득 / 미발견
    Visited,   // 방문 흔적 (해달 탭만)
    Collected, // 획득 완료 / 등록 완료
}

/// <summary>
/// 도감 모델 (순수 C#). 항목 ID별 상태만 가진다. UI와 세이브 형식을 모른다.
/// 상태는 되돌아가지 않는다: 미획득 → (방문 흔적) → 획득.
/// </summary>
public class Collection
{
    public event Action<CollectionEntry, CollectionState> OnStateChanged;

    // 세이브와 같은 ID 기준. DB에서 지워진 항목의 상태도 그대로 보관해 다음 저장 때 잃지 않음
    private readonly Dictionary<string, CollectionState> _states = new Dictionary<string, CollectionState>();

    public IReadOnlyDictionary<string, CollectionState> States => _states;

    public CollectionState GetState(CollectionEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        return _states.TryGetValue(entry.EntryId, out var state) ? state : CollectionState.Unknown;
    }

    /// <returns>새로 획득했으면 true (이미 획득했으면 false, 알림 없음)</returns>
    public bool Collect(CollectionEntry entry)
    {
        return Raise(entry, CollectionState.Collected);
    }

    /// <summary>방문 흔적 남기기. 방문 흔적을 쓰지 않는 탭이거나 이미 방문/획득했으면 아무것도 하지 않는다.</summary>
    /// <returns>새로 방문 흔적이 생겼으면 true</returns>
    public bool MarkVisited(CollectionEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        if (entry.Tab == null || !entry.Tab.SupportsVisits)
            return false;

        return Raise(entry, CollectionState.Visited);
    }

    /// <summary>세이브 복원용. 알림 없이 상태를 넣는다 (더 높은 상태만 반영).</summary>
    public void LoadState(string entryId, CollectionState state)
    {
        if (string.IsNullOrEmpty(entryId))
            throw new ArgumentException("entryId가 비어 있습니다.", nameof(entryId));

        if (state == CollectionState.Unknown)
            return;

        if (!_states.TryGetValue(entryId, out var current) || current < state)
            _states[entryId] = state;
    }

    public int Count(IEnumerable<CollectionEntry> entries, CollectionState state)
    {
        if (entries == null)
            throw new ArgumentNullException(nameof(entries));

        int count = 0;
        foreach (var entry in entries)
        {
            if (entry != null && GetState(entry) == state)
                count++;
        }
        return count;
    }

    // 상태는 올라가기만 한다 (획득한 것이 방문 흔적으로 내려가지 않게)
    private bool Raise(CollectionEntry entry, CollectionState target)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        var current = GetState(entry);
        if (current >= target)
            return false;

        _states[entry.EntryId] = target;
        OnStateChanged?.Invoke(entry, target);
        return true;
    }
}
