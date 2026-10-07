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

/// <summary>전문 해달(광부·농부)의 처지. 세이브에 숫자로 저장되므로 순서를 바꾸지 않는다</summary>
public enum SpecialistState
{
    NotArrived = 0, // 아직 광장에서 만나지 않음 (찾아오기로 했어도 광장에 나타나기 전)
    AtPlaza = 1,    // 광장에 와 있음 → 말을 걸어 일할 곳에 배치
    Assigned = 2,   // 배치함 (일할 곳의 생산 안내를 끝내기 전이라 아직 생산하지 않음)
    Working = 3,    // 일하는 중 (생산)
}

/// <summary>전문 해달 한 마리의 처지. 화면의 해달 오브젝트가 아니라 이 기록이 원본이다</summary>
[Serializable]
public class SpecialistSaveData
{
    public string otterId;
    public SpecialistState state;
    // 배치한 지역 (예: region_mine)
    public string regionId;
    // 일하기 시작한 시각(UTC). 그 전 시간은 생산에 넣지 않음
    public long workingSinceUtcTicks;
}

/// <summary>관리 역할을 맡은 해달 하나 (게시판 관리 등). 역할 ID가 있으면 맡긴 것</summary>
[Serializable]
public class RoleAssignmentSaveData
{
    public string roleId;
    public string otterId;
    // 근무 자리 (예: station_board)
    public string stationId;
}

/// <summary>
/// 공동사업 한 회차의 진행 (P3). 단계 완료는 저장하지 않는다 (건설·장애물·작업·입주·모임의 원본 기록에서 계산).
/// 여기에는 원본이 없는 것만: 납품한 양, 고른 소품, 끝냈는지
/// </summary>
[Serializable]
public class ProjectSaveData
{
    public string projectId;
    // 반복 사업의 회차 (0부터). 한 번만 하는 사업은 0
    public int cycle;
    // 지금까지 넣은 골드
    public int gold;
    // 지금까지 넣은 아이템 (itemId, 개수)
    public List<ItemStack> items = new List<ItemStack>();
    // 고른 환영 소품의 건설 부탁 ID (고르기 전 = 비어 있음)
    public string choice;
    // 모든 단계를 끝내 결과를 받았음
    public bool completed;
}

/// <summary>집 한 채 (같은 집 정의를 여러 채 쓸 수 있게 인스턴스와 배치 자리를 나눔)</summary>
[Serializable]
public class HouseSaveData
{
    public string instanceId;
    // 집의 종류 (건설 ID, 예: con_p3_house)
    public string definitionId;
    // 광장의 배치 자리 (예: slot_plaza_expand_01_house)
    public string slotId;
    // 입주한 해달 (입주 전 = 비어 있음)
    public string residentId;
}

/// <summary>생활 의뢰 하나 (회차로 틀·아이템·개수가 정해져 재접속해도 다시 뽑지 않음)</summary>
[Serializable]
public class LifeRequestSaveData
{
    // 몇 번째 의뢰인지 (0부터). 작업 기록 ID가 이 값을 씀
    public int serial;
    public string templateId;
    // 건네줄 아이템 (주민 작업 의뢰는 비어 있음)
    public string itemId;
    public int amount;
}

/// <summary>
/// 정착 진행 세이브 (SaveData.settlement). SettlementManager가 읽고 쓴다.
/// 개간 지역의 단계는 따로 저장하지 않는다: 열린 발전(unlockedDevelopments)과 작업(tasks, completedTasks)으로 정해짐.
/// 게시판 등급·큰 부탁 단계도 저장하지 않는다: 발전·끝낸 부탁·역할 기록으로 정해짐.
/// 공동사업 단계도 저장하지 않는다: 건설·장애물·작업·집의 입주민·모임 기록으로 정해짐 (납품량·고른 소품·보상 기록만 저장).
/// </summary>
[Serializable]
public class SettlementSaveData
{
    /// <summary>
    /// 정착 세이브 버전. 0 = P2 전 (필드가 없던 세이브), 2 = P2 (관리 역할), 3 = P3 (공동사업·집·생활 의뢰·요정 방문 순서),
    /// 4 = 가로등이 첫 집과 따로 지어짐 (건설 모드).
    /// 불러올 때 낮은 버전이면 SettlementManager가 한 번 옮기고 이 값으로 올린다
    /// </summary>
    public const int CurrentVersion = 4;

    public int version;
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
    // 전문 해달 (광부·농부. 옛 세이브는 없음 = 불러올 때 이미 운영 중인 지역이면 바로 일하는 중으로)
    public List<SpecialistSaveData> specialists = new List<SpecialistSaveData>();
    // 광장에서 처음 만난 해달 (새 해달 만나기 퀘스트. 한 번만 셈)
    public List<string> metOtters = new List<string>();
    // 관리 역할을 맡은 해달 (P2. 옛 세이브는 없음 = 아무에게도 맡기지 않음)
    public List<RoleAssignmentSaveData> roles = new List<RoleAssignmentSaveData>();
    // 공동사업 진행 (P3. 옛 세이브는 없음 = 첫 사업부터)
    public List<ProjectSaveData> projects = new List<ProjectSaveData>();
    // 받은 사업·의뢰 보상 (예: p3_supply_01#0). 경험치를 한 번만 주는 기록
    public List<string> rewardKeys = new List<string>();
    // 집 인스턴스 (P3 새 이웃의 집부터)
    public List<HouseSaveData> houses = new List<HouseSaveData>();
    // 지금 게시판에 걸린 생활 의뢰
    public List<LifeRequestSaveData> lifeRequests = new List<LifeRequestSaveData>();
    // 다음 생활 의뢰의 회차
    public int lifeRequestSerial;
    // 끝낸 생활 의뢰 수
    public int lifeRequestsDone;
    // 다음 장난감 해달이 찾아오는 시각 (UTC ticks, 0 = 시계가 돌지 않음. P4 — 옛 세이브는 0)
    public long toyVisitDueUtcTicks;
    // 찾아온 장난감 해달 수 (해달 고르기의 회차)
    public int toyVisitSerial;
    // 자리를 골라 지은 건물의 공사·완성 기록 (P4. 놓인 자리는 꾸미기 세이브)
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
}
