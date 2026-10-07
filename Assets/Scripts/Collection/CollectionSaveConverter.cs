using System;
using System.Collections.Generic;

/// <summary>세이브 한 줄: 도감 항목 ID와 상태</summary>
[Serializable]
public class CollectionSaveEntry
{
    public string entryId;
    public CollectionState state;
    // 이야기를 봤는지 (옛 세이브에는 없어서 false = 안 본 것으로 시작)
    public bool storyWatched;

    public CollectionSaveEntry() { }

    public CollectionSaveEntry(string entryId, CollectionState state, bool storyWatched = false)
    {
        this.entryId = entryId;
        this.state = state;
        this.storyWatched = storyWatched;
    }
}

/// <summary>
/// Collection ↔ 세이브 형식(List&lt;CollectionSaveEntry&gt;) 변환. 모델이 세이브 타입을 모르도록 여기서만 한다.
/// </summary>
public static class CollectionSaveConverter
{
    /// <summary>미획득이 아닌 항목만 ID 순으로 쓴다 (DB에 없는 ID도 보관해 둔 그대로 씀)</summary>
    public static void Write(Collection collection, List<CollectionSaveEntry> result)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        if (result == null) throw new ArgumentNullException(nameof(result));

        result.Clear();

        var ids = new List<string>(collection.States.Keys);
        ids.Sort(string.CompareOrdinal);
        foreach (var id in ids)
            result.Add(new CollectionSaveEntry(id, collection.States[id], collection.IsStoryWatched(id)));
    }

    /// <summary>저장된 상태를 넣는다. 비었거나 잘못된 줄은 건너뛴다.</summary>
    public static void Read(IEnumerable<CollectionSaveEntry> saved, Collection collection)
    {
        if (saved == null) throw new ArgumentNullException(nameof(saved));
        if (collection == null) throw new ArgumentNullException(nameof(collection));

        foreach (var line in saved)
        {
            if (line == null || string.IsNullOrEmpty(line.entryId) || !Enum.IsDefined(typeof(CollectionState), line.state))
                continue;

            collection.LoadState(line.entryId, line.state);
            if (line.storyWatched)
                collection.LoadStoryWatched(line.entryId);
        }
    }
}
