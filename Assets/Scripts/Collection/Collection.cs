using System;
using System.Collections.Generic;

public enum CollectionState
{
    Unknown,   // 미획득 / 미발견
    Visited,   // 방문 흔적 (해달 탭만)
    Collected, // 획득 완료 / 등록 완료
}

/// <summary>순간포착 첫 발견 기록: 함께한 해달 이름과 날짜 (yyyy.MM.dd)</summary>
public readonly struct CollectionRecord
{
    public readonly string Companion;
    public readonly string Date;

    public CollectionRecord(string companion, string date)
    {
        Companion = companion;
        Date = date;
    }
}

/// <summary>
/// 도감 모델 (순수 C#). 항목 ID별 상태만 가진다. UI와 세이브 형식을 모른다.
/// 상태는 되돌아가지 않는다: 미획득 → (방문 흔적) → 획득.
/// </summary>
public class Collection
{
    public event Action<CollectionEntry, CollectionState> OnStateChanged;

    /// <summary>"아직 안 본 이야기"가 생기거나 없어질 수 있을 때 (이야기 있는 항목 획득, 이야기 봄)</summary>
    public event Action OnStoriesChanged;

    // 세이브와 같은 ID 기준. DB에서 지워진 항목의 상태도 그대로 보관해 다음 저장 때 잃지 않음
    private readonly Dictionary<string, CollectionState> _states = new Dictionary<string, CollectionState>();

    // 이야기를 본 항목 ID
    private readonly HashSet<string> _watchedStories = new HashSet<string>();

    // 순간포착 기록 (항목 ID → 함께한 해달 · 첫 발견 날짜). 처음 포착한 것만 남는다
    private readonly Dictionary<string, CollectionRecord> _records = new Dictionary<string, CollectionRecord>();

    public IReadOnlyDictionary<string, CollectionState> States => _states;

    /// <summary>첫 발견 기록이 있으면 true</summary>
    public bool TryGetRecord(string entryId, out CollectionRecord record) => _records.TryGetValue(entryId ?? "", out record);

    /// <summary>
    /// 처음 포착했을 때의 기록을 남긴다 (이미 있으면 그대로 둠 = 첫 발견만). 항목을 획득으로 올리는 것은 Collect가 한다
    /// </summary>
    /// <returns>새로 남겼으면 true</returns>
    public bool AddRecord(CollectionEntry entry, string companion, string date)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));
        if (_records.ContainsKey(entry.EntryId))
            return false;

        _records[entry.EntryId] = new CollectionRecord(companion ?? "", date ?? "");
        return true;
    }

    /// <summary>세이브 복원용</summary>
    public void LoadRecord(string entryId, string companion, string date)
    {
        if (string.IsNullOrEmpty(entryId))
            throw new ArgumentException("entryId가 비어 있습니다.", nameof(entryId));
        if (string.IsNullOrEmpty(companion) && string.IsNullOrEmpty(date))
            return;

        _records[entryId] = new CollectionRecord(companion ?? "", date ?? "");
    }

    public bool IsStoryWatched(string entryId) => _watchedStories.Contains(entryId);

    /// <summary>획득했고 이야기가 있는데 아직 안 봤으면 true (N 표시, 자동 재생)</summary>
    public bool HasNewStory(CollectionEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        return entry.HasStory && GetState(entry) == CollectionState.Collected && !_watchedStories.Contains(entry.EntryId);
    }

    public int CountNewStories(IEnumerable<CollectionEntry> entries)
    {
        if (entries == null)
            throw new ArgumentNullException(nameof(entries));

        int count = 0;
        foreach (var entry in entries)
        {
            if (entry != null && HasNewStory(entry))
                count++;
        }
        return count;
    }

    /// <summary>이야기를 끝까지 봤거나 건너뜀</summary>
    /// <returns>처음 본 것이면 true</returns>
    public bool MarkStoryWatched(CollectionEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        if (!_watchedStories.Add(entry.EntryId))
            return false;

        OnStoriesChanged?.Invoke();
        return true;
    }

    /// <summary>세이브를 다 불러온 뒤 호출 → N 표시를 다시 계산하게 알림 (복원 중에는 하나씩 알리지 않음)</summary>
    public void NotifyLoaded() => OnStoriesChanged?.Invoke();

    /// <summary>세이브 복원용. 알림 없이 "봤음"으로 넣는다</summary>
    public void LoadStoryWatched(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
            throw new ArgumentException("entryId가 비어 있습니다.", nameof(entryId));

        _watchedStories.Add(entryId);
    }

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

    /// <summary>도감 진척도 (%, 내림): 획득한 항목 ÷ 모든 항목. 방문 흔적은 세지 않음. 항목이 없으면 0</summary>
    public int CollectedPercent(IReadOnlyCollection<CollectionEntry> entries)
    {
        if (entries == null)
            throw new ArgumentNullException(nameof(entries));

        int total = 0;
        foreach (var entry in entries)
        {
            if (entry != null)
                total++;
        }
        return total == 0 ? 0 : Count(entries, CollectionState.Collected) * 100 / total;
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

        if (target == CollectionState.Collected && entry.HasStory)
            OnStoriesChanged?.Invoke();
        return true;
    }
}
