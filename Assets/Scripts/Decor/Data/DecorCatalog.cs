using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 놓을 수 있는 물건 전부. 모든 장소(광장, 밭, 낚시터)의 격자가 같은 목록을 쓴다.
/// </summary>
[CreateAssetMenu(fileName = "DecorCatalog", menuName = "Game Data/Decor/Decor Catalog")]
public class DecorCatalog : ScriptableObject
{
    [Tooltip("놓을 수 있는 물건 전부 (보관함 정렬 순서)")]
    [SerializeField]
    private List<DecorDefinition> _decors = new List<DecorDefinition>();

    private Dictionary<string, DecorDefinition> _byId;
    private Dictionary<ItemDefinition, DecorDefinition> _byItem;

    public IReadOnlyList<DecorDefinition> Decors => _decors;

    /// <summary>아이템 ID로 물건 찾기 (세이브 복원용)</summary>
    public bool TryGetDecor(string itemId, out DecorDefinition decor)
    {
        if (itemId == null)
            throw new ArgumentNullException(nameof(itemId));

        EnsureLookup();
        return _byId.TryGetValue(itemId, out decor);
    }

    /// <returns>이 아이템을 놓는 물건. 놓을 수 없는 아이템이면 null</returns>
    public DecorDefinition FindByItem(ItemDefinition item)
    {
        if (item == null)
            return null;

        EnsureLookup();
        return _byItem.TryGetValue(item, out var decor) ? decor : null;
    }

    private void EnsureLookup()
    {
        if (_byId != null)
            return;

        _byId = new Dictionary<string, DecorDefinition>();
        _byItem = new Dictionary<ItemDefinition, DecorDefinition>();
        foreach (var decor in _decors)
        {
            if (decor == null || decor.Item == null || string.IsNullOrEmpty(decor.ItemId) || _byId.ContainsKey(decor.ItemId))
                continue;

            _byId.Add(decor.ItemId, decor);
            _byItem[decor.Item] = decor;
        }
    }

    // 에디터에서 목록을 바꾸면 조회표를 다시 만들도록
    private void OnEnable() => _byId = null;

    private void OnValidate()
    {
        _byId = null;

        var seen = new HashSet<string>();
        foreach (var decor in _decors)
        {
            if (decor == null)
                Debug.LogWarning($"[{name}] 빈 칸이 있습니다.", this);
            else if (!seen.Add(decor.ItemId ?? ""))
                Debug.LogWarning($"[{name}] 같은 아이템의 물건이 겹칩니다: {decor.ItemId} (먼저 등록된 것만 사용)", this);
        }
    }
}
