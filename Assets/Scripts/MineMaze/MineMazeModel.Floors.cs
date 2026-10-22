using System;
using System.Collections.Generic;
using UnityEngine;

public partial class MineMazeModel
{
    private MineMazeConfig _rootConfig;
    private readonly Dictionary<int, MazeFloorSave> _floorStates = new Dictionary<int, MazeFloorSave>();
    public int CurrentFloor { get; private set; }
    public int FloorCount => 1 + _rootConfig.AdditionalFloors.Count;
    public bool AtContentEnd => FloorCount > 1 && CurrentFloor == FloorCount - 1;
    public MineMazeConfig FloorConfig(int index) => index == 0 ? _rootConfig : _rootConfig.AdditionalFloors[index - 1];
    public int UnlockedFloor
    {
        get
        {
            int unlocked = 0;
            while (unlocked < FloorCount - 1 && IsFloorCleared(unlocked)) unlocked++;
            return unlocked;
        }
    }
    public bool IsFloorCleared(int index) => index == CurrentFloor ? FloorCleared
        : _floorStates.TryGetValue(index, out var state) && state.cleared;

    private void InitializeFloors(MineMazeConfig root, MineMazeSave save)
    {
        _rootConfig = root;
        if (save.floors != null)
            foreach (var state in save.floors)
                if (state != null && state.floor >= 0 && state.floor < FloorCount)
                    _floorStates[state.floor] = state;
        // Before multi-floor saves, all local progress belonged to floor 1.
        if (_floorStates.Count == 0) _floorStates[0] = MazeFloorSave.From(save, 0);
        CurrentFloor = Mathf.Clamp(save.currentFloor, 0, FloorCount - 1);
        int unlocked = 0;
        while (unlocked < FloorCount - 1 && _floorStates.TryGetValue(unlocked, out var state) && state.cleared) unlocked++;
        CurrentFloor = Math.Min(CurrentFloor, unlocked);
        _config = FloorConfig(CurrentFloor);
    }

    private void RestoreFloorState(DateTime now)
    {
        _visited.Clear();
        _openedChests.Clear();
        _rocks.Clear();
        foreach (var cell in _map.Rocks) _rocks[cell] = new MazeRock { Cell = cell };
        if (!_floorStates.TryGetValue(CurrentFloor, out var state)) state = new MazeFloorSave();
        GateDamage = Mathf.Clamp(state.gateDamage, 0, _config.GateHits);
        FloorCleared = state.cleared || GateDamage == _config.GateHits;
        if (FloorCleared) GateDamage = _config.GateHits;
        LoadFloor(state);
        foreach (var rock in _rocks.Values)
            if (!rock.Present && now >= rock.RespawnUtc) Spawn(rock);
        Position = _map.Start;
        Facing = _map.StartFacing;
        Combo = 0;
        _lastHitTime = float.NegativeInfinity;
    }

    public bool SelectFloor(int floor, DateTime now)
    {
        // During a run only the stairs may lead forward. Floor selection otherwise belongs to the entrance.
        if (floor < 0 || floor > UnlockedFloor || floor >= FloorCount) return false;
        if (floor >= 3 && !_fishingAccess) return false;
        if (Inside && !(OnStairs && FloorCleared && floor == CurrentFloor + 1)) return false;
        ToSave(); // Snapshot current floor before replacing its local state.
        CurrentFloor = floor;
        _config = FloorConfig(floor);
        _map = MazeMap.Parse(_config.MapRows);
        RestoreFloorState(now);
        _runBroken.Clear(); // 다녀온 횟수는 나올 때 서 있는 층의 돌로 셈
        if (Inside) { Community.Discover(CurrentFloor, _fishingAccess); Visit(Position); }
        // Caller rebuilds map UI before invoking its refresh (different floor dimensions).
        return true;
    }
}
