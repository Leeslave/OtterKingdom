using System;
using System.Collections.Generic;

/// <summary>낚시 개편 테스트 씬 세이브 (persistentDataPath/fishing_rework_test.json, 본 게임 세이브와 따로)</summary>
[Serializable]
public class FishingReworkSave
{
    public int version = 1;
    public bool started;
    public int gold;
    public int lineLevel;
    public int sinkerLevel;
    public int reelLevel;
    public int totalCaught;
    public int requestsDone;
    // 지난번 던진 거리 (m). 0 = 끝까지 (줄을 강화하면 같이 늘어남)
    public float lastCastDistance;
    // 빨간 핀: 바다를 끌어 직접 꽂은 거리 (m, 0 = 없음)
    public float pinDistance;
    public List<FishingBookSave> book = new List<FishingBookSave>();
    public List<FishingBagSave> bag = new List<FishingBagSave>();

    // 지금 의뢰 (requestKind < 0 = 없음 → 새로 뽑음)
    public int requestKind = -1;
    public string requestFishId = "";
    public float requestTarget;
    public float requestProgress;
    public int requestReward;
    public float requestSkipCooldown;
}

/// <summary>가방 속 물고기 한 마리</summary>
[Serializable]
public class FishingBagSave
{
    public string fishId;
    public float weight;
    public int price;
}

[Serializable]
public class FishingBookSave
{
    public string fishId;
    public int count;
    public float bestWeight;
}
