using System;
using System.Collections.Generic;

/// <summary>왕국에서 해달의 처지</summary>
public enum ResidentState
{
    Visitor,              // 놀러 옴 (주민 수에 안 들어감)
    SettlementCandidate,  // 살고 싶어 함 (집이 필요)
    Resident,             // 주민
    SpecialNpc,           // 건설 해달처럼 역할로 와 있는 해달 (집이 생기면 주민)
}

[Serializable]
public class ResidentSaveData
{
    public string otterId;
    public ResidentState state;
}

/// <summary>진행 중인 건설 하나. 끝나는 시각(UTC)을 저장해 게임을 꺼 둔 동안에도 진행된다</summary>
[Serializable]
public class ConstructionJobSaveData
{
    public string requestId;
    public string constructionId;
    public long startUtcTicks;
    public long endUtcTicks;
    // 일할 해달이 아직 가는 중 (옛 세이브는 없음 = 이미 시작)
    public bool waitingForWorker;
}

/// <summary>주운 나뭇가지가 다시 생기는 시각</summary>
[Serializable]
public class GatherCooldownSaveData
{
    public string pointId;
    public long readyUtcTicks;
}

/// <summary>진행 중인 주민 작업 하나. 끝나는 시각(UTC)을 저장해 게임을 꺼 둔 동안에도 진행된다</summary>
[Serializable]
public class SettlementTaskSaveData
{
    public string taskId;
    public long startUtcTicks;
    public long endUtcTicks;
    // 보낸 주민 해달 (이 해달들은 작업이 끝날 때까지 Working)
    public List<string> assignedOtterIds = new List<string>();
}

/// <summary>
/// 정착 진행 세이브 (SaveData.settlement). SettlementManager가 읽고 쓴다.
/// 개간 지역의 단계는 따로 저장하지 않는다: 열린 발전(unlockedDevelopments)과 작업(tasks, completedTasks)으로 정해짐.
/// </summary>
[Serializable]
public class SettlementSaveData
{
    // 새 게임 처리(첫 해달 도착, 시작 재료)를 했는지. 재접속 때 다시 하지 않게
    public bool initialized;
    // 정착 진행이 생기기 전 세이브: 불러올 때 모든 부탁을 끝낸 것으로 처리 (이미 하던 밭을 다시 잠그지 않게)
    public bool legacyComplete;
    public bool boardVisited;
    public int kingdomStage;
    public List<string> completedRequests = new List<string>();
    public List<string> unlockedDevelopments = new List<string>();
    public List<ResidentSaveData> residents = new List<ResidentSaveData>();
    public List<string> guestbookEntries = new List<string>();
    // 진행 중인 건설 (없으면 requestId가 비어 있음. JsonUtility는 null을 저장하지 않음)
    public ConstructionJobSaveData construction = new ConstructionJobSaveData();
    public List<GatherCooldownSaveData> gatherCooldowns = new List<GatherCooldownSaveData>();
    // 한 번만 보이는 안내를 봤는지 등 (예: visited_Farm)
    public List<string> flags = new List<string>();
    // 진행 중인 주민 작업 (P1. 옛 세이브는 없음 = 비어 있음)
    public List<SettlementTaskSaveData> tasks = new List<SettlementTaskSaveData>();
    // 끝낸 주민 작업
    public List<string> completedTasks = new List<string>();
}
