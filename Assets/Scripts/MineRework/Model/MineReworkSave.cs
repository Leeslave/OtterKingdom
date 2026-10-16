using System;
using System.Collections.Generic;

/// <summary>광산 개편 테스트 씬의 저장 (본 게임 세이브와 따로, persistentDataPath/mine_rework_test.json)</summary>
[Serializable]
public class MineReworkSave
{
    public int gold;
    public List<MineOreCountSave> ores = new List<MineOreCountSave>();
    public List<string> discovered = new List<string>();
    public int pickaxeTier;
    public int sharpen;
    // 지금 있는 층 (0 = 1층)
    public int layer;
    // 열린 층 수 (1 = 1층만)
    public int openedLayers = 1;
    // 다음 층 막힌 길에 친 횟수
    public int boulderHits;
    public List<MineRockSave> rocks = new List<MineRockSave>();
    public float respawnTimer;
    public float minerTimer;
    public int rocksBroken;
    // 마지막 저장 시각 (꺼 둔 동안 바위 · 광부)
    public long savedUtcTicks;
    // 첫 실행 표시 (처음 골드를 한 번만 줌)
    public bool started;
}

[Serializable]
public class MineOreCountSave
{
    public string id;
    public int count;
}

/// <summary>바위 칸 하나 (hp 0 = 빈 칸)</summary>
[Serializable]
public class MineRockSave
{
    public string vein;
    public int hp;
    public int maxHp;
    public bool shiny;
}
