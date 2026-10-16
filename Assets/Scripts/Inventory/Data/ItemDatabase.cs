using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 ID → ItemDefinition 조회표. 세이브에는 ID(문자열)만 저장되므로 불러올 때 여기서 찾는다.
/// 게임의 모든 아이템을 등록해 둔다 (⋮ 메뉴 "Collect All Items"로 프로젝트에서 자동 수집).
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game Data/Inventory/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [Tooltip("게임의 모든 아이템. ID가 겹치면 먼저 등록된 것만 쓰고 경고")]
    [SerializeField]
    private List<ItemDefinition> _items = new List<ItemDefinition>();

    private Dictionary<string, ItemDefinition> _byId;

    public IReadOnlyList<ItemDefinition> Items => _items;

    public bool TryGet(string itemId, out ItemDefinition item)
    {
        if (itemId == null)
            throw new ArgumentNullException(nameof(itemId));

        EnsureLookup();
        return _byId.TryGetValue(itemId, out item);
    }

    /// <returns>두 번 이상 등록된 ID 목록 (비어 있어야 정상)</returns>
    public static List<string> FindDuplicateIds(IEnumerable<ItemDefinition> items)
    {
        var seen = new HashSet<string>();
        var duplicates = new List<string>();
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                continue;
            if (!seen.Add(item.ItemId) && !duplicates.Contains(item.ItemId))
                duplicates.Add(item.ItemId);
        }
        return duplicates;
    }

    private void EnsureLookup()
    {
        if (_byId != null)
            return;

        _byId = new Dictionary<string, ItemDefinition>();
        foreach (var item in _items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                continue;
            if (!_byId.ContainsKey(item.ItemId))
                _byId.Add(item.ItemId, item);
        }
    }

    // 에디터에서 목록이나 아이템 ID를 바꾸면 조회표를 다시 만들도록
    private void OnEnable() => _byId = null;

    private void OnValidate()
    {
        _byId = null;

        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] == null)
                Debug.LogWarning($"[{name}] {i}번 칸이 비어 있습니다.", this);
        }

        foreach (var id in FindDuplicateIds(_items))
            Debug.LogWarning($"[{name}] 아이템 ID가 겹칩니다: {id} (먼저 등록된 것만 사용)", this);
    }

#if UNITY_EDITOR
    [ContextMenu("Collect All Items")]
    private void CollectAllItems()
    {
        _items.Clear();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            _items.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(path));
        }
        _items.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));

        UnityEditor.EditorUtility.SetDirty(this);
        OnValidate();
    }
#endif
}
