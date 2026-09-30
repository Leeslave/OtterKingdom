using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 꾸미기 격자의 규칙 모델 (순수 C#): 칸마다 구역·막힘·차지한 물건을 기억하고, 놓기/옮기기/치우기/구역 열기를 처리한다.
/// 가방 개수(보관함에 남은 수)와 재화는 모른다. 그런 일은 DecorManager가 조율한다.
/// </summary>
public class DecorLayout
{
    public event Action<PlacedDecor> OnPlaced;
    public event Action<PlacedDecor> OnMoved;
    public event Action<PlacedDecor> OnRemoved;
    public event Action<string> OnRegionUnlocked;

    public Vector2Int Size { get; }

    private readonly string[] _regionOfCell; // null = 어느 구역도 아님
    private readonly bool[] _blocked;         // 나무·집 등 월드 쪽이 막은 칸
    private readonly int[] _occupant;         // 0 = 빈 칸, 그 외 = 놓인 물건의 InstanceId

    private readonly HashSet<string> _regions = new HashSet<string>();
    private readonly HashSet<string> _unlockedRegions = new HashSet<string>();
    private readonly Dictionary<int, PlacedDecor> _placed = new Dictionary<int, PlacedDecor>();
    private int _nextInstanceId = 1;

    public IReadOnlyCollection<PlacedDecor> Placed => _placed.Values;
    public IReadOnlyCollection<string> UnlockedRegions => _unlockedRegions;

    public DecorLayout(Vector2Int size)
    {
        if (size.x < 1 || size.y < 1)
            throw new ArgumentOutOfRangeException(nameof(size), "격자는 1×1 이상이어야 합니다.");

        Size = size;
        int count = size.x * size.y;
        _regionOfCell = new string[count];
        _blocked = new bool[count];
        _occupant = new int[count];
    }

    #region 구역

    /// <summary>구역을 등록한다. 칸이 겹치면 나중에 등록한 구역이 가져간다 (격자 밖 칸은 무시)</summary>
    public void AddRegion(string regionId, IEnumerable<RectInt> areas, bool unlocked)
    {
        if (string.IsNullOrEmpty(regionId))
            throw new ArgumentException("regionId가 비어 있습니다.", nameof(regionId));
        if (areas == null)
            throw new ArgumentNullException(nameof(areas));

        _regions.Add(regionId);
        if (unlocked)
            _unlockedRegions.Add(regionId);

        foreach (var area in areas)
        {
            foreach (var cell in area.allPositionsWithin)
            {
                if (InBounds(cell))
                    _regionOfCell[Index(cell)] = regionId;
            }
        }
    }

    public bool HasRegion(string regionId) => regionId != null && _regions.Contains(regionId);

    public bool IsRegionUnlocked(string regionId) => regionId != null && _unlockedRegions.Contains(regionId);

    /// <returns>새로 열렸으면 true (없는 구역이거나 이미 열렸으면 false)</returns>
    public bool UnlockRegion(string regionId)
    {
        if (!HasRegion(regionId) || !_unlockedRegions.Add(regionId))
            return false;

        OnRegionUnlocked?.Invoke(regionId);
        return true;
    }

    /// <returns>칸이 속한 구역 ID. 없으면 null</returns>
    public string RegionAt(Vector2Int cell) => InBounds(cell) ? _regionOfCell[Index(cell)] : null;

    #endregion

    #region 막힌 칸

    /// <summary>월드 쪽(나무, 집, 물가 등)이 칸을 막거나 푼다. 이미 놓인 물건은 그대로 둔다</summary>
    public void SetBlocked(Vector2Int cell, bool blocked)
    {
        if (InBounds(cell))
            _blocked[Index(cell)] = blocked;
    }

    public void ClearBlocked() => Array.Clear(_blocked, 0, _blocked.Length);

    #endregion

    #region 조회

    public DecorCellState GetCellState(Vector2Int cell)
    {
        if (!InBounds(cell))
            return DecorCellState.Unavailable;

        int i = Index(cell);
        if (_occupant[i] != 0)
            return DecorCellState.Occupied;
        if (_regionOfCell[i] == null || _blocked[i])
            return DecorCellState.Unavailable;
        return _unlockedRegions.Contains(_regionOfCell[i]) ? DecorCellState.Free : DecorCellState.Locked;
    }

    /// <returns>칸에 놓인 물건. 없으면 null</returns>
    public PlacedDecor GetAt(Vector2Int cell)
    {
        if (!InBounds(cell))
            return null;

        int id = _occupant[Index(cell)];
        return id != 0 ? _placed[id] : null;
    }

    public bool TryGet(int instanceId, out PlacedDecor placed) => _placed.TryGetValue(instanceId, out placed);

    public int PlacedCount(DecorDefinition decor)
    {
        int count = 0;
        foreach (var p in _placed.Values)
        {
            if (p.Decor == decor)
                count++;
        }
        return count;
    }

    /// <summary>
    /// 이 자리·방향에 놓을 수 있는지. ignoreInstanceId는 옮기는 중인 물건 자신 (자기 칸은 비어 있는 것으로 봄).
    /// 여러 칸이 문제면 가장 먼저 걸린 이유를 돌려준다 (격자 밖 → 막힘 → 잠김 → 겹침 순)
    /// </summary>
    public DecorPlacementResult Check(DecorDefinition decor, Vector2Int origin, DecorRotation rotation, int ignoreInstanceId = 0)
    {
        if (decor == null)
            throw new ArgumentNullException(nameof(decor));

        var area = new RectInt(origin, Normalize(decor, rotation).Apply(decor.Footprint));
        if (area.xMin < 0 || area.yMin < 0 || area.xMax > Size.x || area.yMax > Size.y)
            return DecorPlacementResult.OutOfBounds;

        var worst = DecorPlacementResult.Ok;
        foreach (var cell in area.allPositionsWithin)
        {
            int i = Index(cell);
            DecorPlacementResult result;
            if (_regionOfCell[i] == null || _blocked[i])
                result = DecorPlacementResult.Unavailable;
            else if (!_unlockedRegions.Contains(_regionOfCell[i]))
                result = DecorPlacementResult.Locked;
            else if (_occupant[i] != 0 && _occupant[i] != ignoreInstanceId)
                result = DecorPlacementResult.Occupied;
            else
                continue;

            // 격자 밖을 뺀 이유 중 가장 앞의 것 (막힘 < 잠김 < 겹침 순으로 enum 값이 작음)
            if (worst == DecorPlacementResult.Ok || result < worst)
                worst = result;
        }
        return worst;
    }

    #endregion

    #region 놓기 / 옮기기 / 치우기

    /// <summary>물건을 놓는다 (보관함 개수는 검사하지 않음, DecorManager가 먼저 확인)</summary>
    public DecorPlacementResult TryPlace(DecorDefinition decor, Vector2Int origin, DecorRotation rotation, out PlacedDecor placed)
    {
        placed = null;
        var result = Check(decor, origin, rotation);
        if (result != DecorPlacementResult.Ok)
            return result;

        placed = new PlacedDecor(_nextInstanceId++, decor, origin, Normalize(decor, rotation));
        Occupy(placed);
        OnPlaced?.Invoke(placed);
        return DecorPlacementResult.Ok;
    }

    /// <summary>놓인 물건을 다른 자리·방향으로 옮긴다. 실패하면 원래 자리 그대로</summary>
    public DecorPlacementResult TryMove(int instanceId, Vector2Int origin, DecorRotation rotation)
    {
        if (!_placed.TryGetValue(instanceId, out var placed))
            throw new ArgumentException($"놓여 있지 않은 물건입니다: {instanceId}", nameof(instanceId));

        var result = Check(placed.Decor, origin, rotation, instanceId);
        if (result != DecorPlacementResult.Ok)
            return result;

        Release(placed);
        placed.Origin = origin;
        placed.Rotation = Normalize(placed.Decor, rotation);
        Occupy(placed);
        OnMoved?.Invoke(placed);
        return DecorPlacementResult.Ok;
    }

    /// <summary>물건을 치운다 (보관함으로 돌아감)</summary>
    /// <returns>치웠으면 true, 없는 ID면 false</returns>
    public bool Remove(int instanceId)
    {
        if (!_placed.TryGetValue(instanceId, out var placed))
            return false;

        Release(placed);
        _placed.Remove(instanceId);
        OnRemoved?.Invoke(placed);
        return true;
    }

    #endregion

    #region 세이브 복원

    /// <summary>
    /// 세이브의 물건을 알림 없이 다시 놓는다. 자리가 맞지 않으면(격자·구역이 바뀐 경우) 놓지 않고 false.
    /// 같은 InstanceId가 이미 있으면 false
    /// </summary>
    public bool LoadPlaced(int instanceId, DecorDefinition decor, Vector2Int origin, DecorRotation rotation)
    {
        if (decor == null)
            throw new ArgumentNullException(nameof(decor));

        if (instanceId <= 0 || _placed.ContainsKey(instanceId))
            return false;

        if (Check(decor, origin, rotation) != DecorPlacementResult.Ok)
            return false;

        var placed = new PlacedDecor(instanceId, decor, origin, Normalize(decor, rotation));
        Occupy(placed);
        _nextInstanceId = Math.Max(_nextInstanceId, instanceId + 1);
        return true;
    }

    /// <summary>세이브의 열린 구역을 알림 없이 넣는다. 없는 구역이면 false</summary>
    public bool LoadUnlockedRegion(string regionId)
    {
        return HasRegion(regionId) && _unlockedRegions.Add(regionId);
    }

    #endregion

    private static DecorRotation Normalize(DecorDefinition decor, DecorRotation rotation)
    {
        return decor.CanRotate ? rotation : DecorRotation.R0;
    }

    private void Occupy(PlacedDecor placed)
    {
        _placed[placed.InstanceId] = placed;
        foreach (var cell in placed.Area.allPositionsWithin)
            _occupant[Index(cell)] = placed.InstanceId;
    }

    private void Release(PlacedDecor placed)
    {
        foreach (var cell in placed.Area.allPositionsWithin)
        {
            int i = Index(cell);
            if (_occupant[i] == placed.InstanceId)
                _occupant[i] = 0;
        }
    }

    private bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Size.x && cell.y < Size.y;

    private int Index(Vector2Int cell) => cell.y * Size.x + cell.x;
}
