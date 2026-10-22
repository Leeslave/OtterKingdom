using UnityEngine;

/// <summary>
/// 오래된 갱도(광산 미로)를 처음 찾는 조건 (Docs/광산_미로탐험과_왕도성장_기획.md 2 · 3장).
/// 왕국 Lv.5 이상 + 광산 업그레이드 1회(곡괭이 강화 = 오프라인 채굴이 열리는 곡괭이 레벨) + 광부가 채굴 중 +
/// 게시판 해달(또박이)을 게시판에 배치한 뒤면 깡깡이가 갱도를 찾아 광산에서 연락이 온다 (Lv.5에 한꺼번에 열리지 않게:
/// 게시판 보강 → 또박이 배치 → 광산 연락). 한 번 찾으면 발전(FoundDevelopment)으로 남아, 나중에 채굴을 멈춰도 닫히지 않는다.
/// </summary>
public static class MineMazeRules
{
    /// <summary>갱도를 찾음 (광산 씬의 입구 · 미로 장소 · 메인 퀘스트가 본다)</summary>
    public const string FoundDevelopment = "mine_maze_found";
    /// <summary>갱도에서 처음 돌을 부숨 (메인 Lv.6 "길을 막은 돌", 기록으로 다시 셈)</summary>
    public const string FirstRockDevelopment = "maze_first_rock";
    /// <summary>그 층의 돌을 모두 부수고 처음 다녀옴 (메인 Lv.7 "갱도 한 바퀴")</summary>
    public const string FirstTripDevelopment = "maze_first_trip";
    /// <summary>갱도 작업대에서 이 단계 곡괭이를 만듦 (메인 Lv.18 · 21, 아래 층 입장 조건)</summary>
    public static string PickaxeDevelopment(int tier) => "maze_pickaxe_" + tier;
    /// <summary>지하 N층(1부터)의 돌을 모두 부숴 다음 층이 열림 (메인 Lv.18 · 21 · 22)</summary>
    public static string FloorClearedDevelopment(int floor) => $"maze_floor_{floor}_cleared";
    /// <summary>미로 씬 이름 (메인 퀘스트 "갱도에 들어가 보기" 대상)</summary>
    public const string SceneName = "MineMaze";
    public const int FoundLevel = 5;
    /// <summary>게시판 해달(또박이) 배치 부탁: 이게 끝나야 광산에서 연락이 옴</summary>
    public const string BoardOtterRequest = "req_assign_receptionist";

    /// <param name="pickaxeLevel">본 게임 곡괭이 레벨 (1 = 강화 전)</param>
    /// <param name="upgradedPickaxeLevel">광산 업그레이드 1회 = 이 곡괭이 레벨 (MiningBalanceData.offlineUnlockPickaxeLevel)</param>
    /// <param name="miningActive">광부가 광산 안에서 채굴 중</param>
    /// <param name="minerWorking">광부가 광산에 배치되어 생산할 수 있음</param>
    /// <param name="boardOtterAssigned">게시판 해달을 배치함 (BoardOtterRequest를 끝냄)</param>
    public static bool ShouldDiscover(bool alreadyFound, int kingdomLevel, int pickaxeLevel, int upgradedPickaxeLevel,
        bool miningActive, bool minerWorking, bool boardOtterAssigned) =>
        !alreadyFound && kingdomLevel >= FoundLevel && pickaxeLevel >= Mathf.Max(2, upgradedPickaxeLevel)
        && miningActive && minerWorking && boardOtterAssigned;
}
