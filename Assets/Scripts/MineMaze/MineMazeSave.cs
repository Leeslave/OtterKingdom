using System;
using System.Collections.Generic;

/// <summary>광산 미로 저장. 본 게임에서는 SaveData에, 독립 테스트에서는 mine_maze_test.json에 저장.</summary>
[Serializable]
public class MineMazeSave
{
    public MineCommunityState community = new MineCommunityState();
    public int progressionVersion;
    public int gold;
    public int kingdomLevel;
    public int gateDamage;
    public bool floorCleared;
    public double minerProgressSeconds;
    public long savedUtcTicks;
    public int currentFloor;
    public List<MazeFloorSave> floors = new List<MazeFloorSave>();
    public List<MineOreCountSave> ores = new List<MineOreCountSave>();
    public List<string> discovered = new List<string>();
    public int pickaxeTier;
    // 나갔다 온 횟수 (지도 해달)
    public int runs;
    public bool mapOtter;
    // 밟아 본 칸 ("x,y")
    public List<string> visited = new List<string>();
    public List<MazeRockSave> rocks = new List<MazeRockSave>();
    // 연 상자 ("x,y")
    public List<string> openedChests = new List<string>();
    public int rocksBroken;
}

[Serializable]
public class MazeFloorSave
{
    public int floor;
    public int gateDamage;
    public bool cleared;
    public List<string> visited = new List<string>();
    public List<string> openedChests = new List<string>();
    public List<MazeRockSave> rocks = new List<MazeRockSave>();

    public static MazeFloorSave From(MineMazeSave save, int floor) => new MazeFloorSave
    {
        floor = floor, gateDamage = save.gateDamage, cleared = save.floorCleared,
        visited = save.visited, openedChests = save.openedChests, rocks = save.rocks
    };
}

/// <summary>돌 하나 (자리 · 한 번 뚫었는지 · 지금 있는지 · 다시 생기는 시각)</summary>
[Serializable]
public class MazeRockSave
{
    public int x;
    public int y;
    public bool opened;
    public bool present;
    public string vein;
    public int hp;
    public int maxHp;
    public bool shiny;
    // 다시 생기는 시각 (UTC ticks, 없으면 0)
    public long respawnUtcTicks;
}
