using System;
using System.Collections.Generic;

/// <summary>광산 미로 테스트 씬의 저장 (본 게임 세이브와 따로, persistentDataPath/mine_maze_test.json)</summary>
[Serializable]
public class MineMazeSave
{
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
