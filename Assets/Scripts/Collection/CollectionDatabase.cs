using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도감의 모든 탭과 항목. ID로 항목 찾기, 아이템으로 항목 찾기(자동 획득용)를 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "CollectionDatabase", menuName = "Game Data/Collection/Collection Database")]
public class CollectionDatabase : ScriptableObject
{
    [Tooltip("도감 탭 (SortOrder 순으로 표시)")]
    [SerializeField]
    private List<CollectionTab> _tabs = new List<CollectionTab>();

    [Tooltip("도감 항목 전부. ID가 겹치면 먼저 등록된 것만 쓰고 경고")]
    [SerializeField]
    private List<CollectionEntry> _entries = new List<CollectionEntry>();

    private Dictionary<string, CollectionEntry> _byId;
    private Dictionary<ItemDefinition, CollectionEntry> _byItem;

    public IReadOnlyList<CollectionTab> Tabs => _tabs;
    public IReadOnlyList<CollectionEntry> Entries => _entries;

    public bool TryGet(string entryId, out CollectionEntry entry)
    {
        if (entryId == null)
            throw new ArgumentNullException(nameof(entryId));

        EnsureLookup();
        return _byId.TryGetValue(entryId, out entry);
    }

    /// <returns>이 아이템에 연결된 항목. 없으면 null (쓰레기, 모종 등)</returns>
    public CollectionEntry FindByItem(ItemDefinition item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        EnsureLookup();
        return _byItem.TryGetValue(item, out var entry) ? entry : null;
    }

    /// <returns>이 탭의 항목 (SortOrder → ID 순)</returns>
    public List<CollectionEntry> GetEntries(CollectionTab tab)
    {
        var result = new List<CollectionEntry>();
        foreach (var entry in _entries)
        {
            if (entry != null && entry.Tab == tab)
                result.Add(entry);
        }

        result.Sort((a, b) =>
        {
            int order = a.SortOrder.CompareTo(b.SortOrder);
            return order != 0 ? order : string.CompareOrdinal(a.EntryId, b.EntryId);
        });
        return result;
    }

    private void EnsureLookup()
    {
        if (_byId != null)
            return;

        _byId = new Dictionary<string, CollectionEntry>();
        _byItem = new Dictionary<ItemDefinition, CollectionEntry>();
        foreach (var entry in _entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.EntryId) || _byId.ContainsKey(entry.EntryId))
                continue;

            _byId.Add(entry.EntryId, entry);
            if (entry.LinkedItem != null && !_byItem.ContainsKey(entry.LinkedItem))
                _byItem.Add(entry.LinkedItem, entry);
        }
    }

    // 에디터에서 목록이나 ID를 바꾸면 조회표를 다시 만들도록
    private void OnEnable() => _byId = null;

    private void OnValidate()
    {
        _byId = null;

        var seen = new HashSet<string>();
        foreach (var entry in _entries)
        {
            if (entry == null)
            {
                Debug.LogWarning($"[{name}] 빈 칸이 있습니다.", this);
                continue;
            }
            if (!seen.Add(entry.EntryId))
                Debug.LogWarning($"[{name}] 항목 ID가 겹칩니다: {entry.EntryId} (먼저 등록된 것만 사용)", this);
        }
    }
}
