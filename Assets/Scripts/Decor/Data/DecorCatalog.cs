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

    [Tooltip("자리를 골라 짓는 건물 (보관함에는 나오지 않고 꾸미기 모드의 건물 탭에 나옴)")]
    [SerializeField]
    private List<BuildingDefinition> _buildings = new List<BuildingDefinition>();

    private Dictionary<string, DecorDefinition> _byId;
    private Dictionary<ItemDefinition, DecorDefinition> _byItem;

    public IReadOnlyList<DecorDefinition> Decors => _decors;
    public IReadOnlyList<BuildingDefinition> Buildings => _buildings;

    /// <summary>세이브의 종류 ID(장난감 = 아이템 ID, 건물 = 건물 ID)로 물건 찾기 (세이브 복원용)</summary>
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
        foreach (var building in _buildings)
        {
            if (building != null && !string.IsNullOrEmpty(building.BuildingId) && !_byId.ContainsKey(building.BuildingId))
                _byId.Add(building.BuildingId, building);
        }
    }

    /// <summary>건물 ID로 건물 찾기</summary>
    public BuildingDefinition FindBuilding(string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId))
            return null;
        foreach (var building in _buildings)
        {
            if (building != null && building.BuildingId == buildingId)
                return building;
        }
        return null;
    }

    /// <summary>테스트·설정 도구용</summary>
    public void Setup(IEnumerable<DecorDefinition> decors, IEnumerable<BuildingDefinition> buildings)
    {
        _decors = decors != null ? new List<DecorDefinition>(decors) : new List<DecorDefinition>();
        _buildings = buildings != null ? new List<BuildingDefinition>(buildings) : new List<BuildingDefinition>();
        _byId = null;
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
