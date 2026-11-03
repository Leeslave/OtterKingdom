using System;
using System.Collections.Generic;

/// <summary>세이브: 아이템 ID · 개수 한 줄</summary>
[Serializable]
public class ItemCountSaveData
{
    public string itemId;
    public int amount;
}

/// <summary>세이브: 시설 하나의 가공 (목공소 → 판재 등)</summary>
[Serializable]
public class ProcessorSaveData
{
    public string buildingId;
    // 가공을 켰나 (켜면 그 시설이 모은 원재료가 가방 대신 가공으로 들어감)
    public bool on;
    // 가공에 넣어 둔 원재료 수
    public int input;
    // 지금 만드는 한 개의 진행 (초)
    public float progress;
    // 다 만들어 시설에 쌓아 둔 가공품 (창고로 옮기기 전)
    public int tray;
    // 처음 켤 때 한 개 공짜로 줬나
    public bool trialGiven;
}

/// <summary>세이브: 번영의 시대 산업 · 물류 (SettlementSaveData.industry)</summary>
[Serializable]
public class IndustrySaveData
{
    // 왕국 창고 (가방 밖 왕국 자재: 판재 · 석재 블록 …)
    public List<ItemCountSaveData> warehouse = new List<ItemCountSaveData>();
    // 그중 공사 · 계약 · 공동사업이 예약해 둔 몫
    public List<ItemCountSaveData> reserved = new List<ItemCountSaveData>();
    public List<ProcessorSaveData> processors = new List<ProcessorSaveData>();
    // 수레가 다니는 시설 (가공품을 창고로 저절로 옮김)
    public List<string> cartRoutes = new List<string>();
    // 개량한 집 (광장에 놓인 건물 번호 — 같은 집 · 같은 입주민 그대로, 그림만 개량 주택)
    public List<int> improvedHouses = new List<int>();
}
