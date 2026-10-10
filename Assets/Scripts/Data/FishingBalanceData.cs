using System.Collections.Generic;
using UnityEngine;

// Fishing tuning shared by every scene's GameManager. Values are placeholders.
// The catch items' names and sale prices live in their ItemDefinitions.
// 물고기는 fishTable에서 고름: 낚싯대 레벨이 minRodLevel 이상인 물고기 중 무게로 (레어 · 에픽은 레벨이 오를수록 무게가 커짐).
// 표가 비어 있으면 예전처럼 fishItemId 하나.
[CreateAssetMenu(menuName = "OtterKingdom/Fishing Balance", fileName = "FishingBalanceData")]
public class FishingBalanceData : ScriptableObject
{
    [Header("Catch items")]
    [Tooltip("표가 비었거나 낚을 수 있는 물고기가 없을 때 나오는 물고기")]
    public string fishItemId = "fish_mackerel";
    public string trashItemId = "trash_basic";

    [System.Serializable]
    public struct FishEntry
    {
        public string itemId;
        [Tooltip("0 흔함 · 1 레어 · 2 에픽")]
        [Range(0, 2)] public int tier;
        [Tooltip("이 낚싯대 레벨부터 낚임")]
        [Min(1)] public int minRodLevel;
        [Tooltip("고를 때의 무게 (흔함 100 · 레어 15 · 에픽 3 기준)")]
        [Min(0f)] public float weight;

        public FishEntry(string itemId, int tier, int minRodLevel, float weight)
        {
            this.itemId = itemId;
            this.tier = tier;
            this.minRodLevel = minRodLevel;
            this.weight = weight;
        }
    }

    [Header("Fish")]
    public FishEntry[] fishTable = new FishEntry[0];
    [Tooltip("레어 · 에픽 무게 배율 = 1 + 이 값 × (낚싯대 레벨 − 1)")]
    [Min(0f)] public float rareLuckPerRodLevel = 0.05f;

    [Header("Timing")]
    [Tooltip("Seconds from the bobber landing until something bites, rolled per cast.")]
    public Vector2 biteDelaySec = new Vector2(10f, 30f);

    [Header("Rod")]
    [Tooltip("Fish chance at rod level 1; the rest is trash.")]
    [Range(0f, 1f)] public float baseFishChance = 0.5f;
    [Tooltip("Added to the fish chance per rod level above 1.")]
    [Range(0f, 1f)] public float fishChancePerRodLevel = 0.05f;
    [Tooltip("물고기 확률의 끝 (나머지는 쓰레기)")]
    [Range(0f, 1f)] public float maxFishChance = 1f;
    // Cost to go from level (index+1) to (index+2). Max level = length + 1.
    public int[] rodUpgradeCosts = { 100, 200, 400, 800 };
    public int MaxRodLevel => rodUpgradeCosts.Length + 1;

    [Header("Offline")]
    [Tooltip("Rod level at which fishing keeps going while the game is closed (no fishing duty needed).")]
    public int offlineUnlockRodLevel = 2;
    [Tooltip("Seconds of cast/bite/pull/reaction animation per catch online, on top of the bite delay.")]
    public float onlineAnimationSecPerCatch = 5f;
    [Tooltip("Offline time per catch = average online time per catch x this.")]
    public float offlineSlowdown = 4f;

    public float FishChanceAt(int rodLevel)
    {
        return Mathf.Clamp(baseFishChance + (rodLevel - 1) * fishChancePerRodLevel, 0f, maxFishChance);
    }

    public bool HasFishTable => fishTable != null && fishTable.Length > 0;

    public float WeightAt(FishEntry fish, int rodLevel)
    {
        if (rodLevel < fish.minRodLevel)
            return 0f;
        float luck = fish.tier > 0 ? 1f + rareLuckPerRodLevel * Mathf.Max(0, rodLevel - 1) : 1f;
        return Mathf.Max(0f, fish.weight) * luck;
    }

    /// <summary>물고기 하나 고르기 (roll01 = 0~1 난수)</summary>
    public string RollFish(int rodLevel, double roll01)
    {
        if (!HasFishTable)
            return fishItemId;
        double total = 0;
        foreach (var fish in fishTable)
            total += WeightAt(fish, rodLevel);
        if (total <= 0)
            return fishItemId;
        double pick = roll01 * total;
        foreach (var fish in fishTable)
        {
            float weight = WeightAt(fish, rodLevel);
            if (weight <= 0f)
                continue;
            if (pick < weight)
                return fish.itemId;
            pick -= weight;
        }
        // 부동소수 끝자리: 마지막으로 낚을 수 있는 물고기
        for (int i = fishTable.Length - 1; i >= 0; i--)
        {
            if (WeightAt(fishTable[i], rodLevel) > 0f)
                return fishTable[i].itemId;
        }
        return fishItemId;
    }

    /// <summary>물고기 또는 쓰레기 (fishRoll로 물고기인지, fishPick으로 어떤 물고기인지)</summary>
    public string RollCatch(int rodLevel, double fishRoll, double fishPick) =>
        fishRoll < FishChanceAt(rodLevel) ? RollFish(rodLevel, fishPick) : trashItemId;

    public bool IsFish(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;
        if (itemId == fishItemId)
            return true;
        if (fishTable != null)
        {
            foreach (var fish in fishTable)
            {
                if (fish.itemId == itemId)
                    return true;
            }
        }
        return false;
    }

    /// <summary>이 낚싯대 레벨에서 처음 낚이는 물고기</summary>
    public List<FishEntry> FirstCaughtAt(int rodLevel)
    {
        var list = new List<FishEntry>();
        if (fishTable != null)
        {
            foreach (var fish in fishTable)
            {
                if (Mathf.Max(1, fish.minRodLevel) == rodLevel)
                    list.Add(fish);
            }
        }
        return list;
    }

    /// <summary>이 물고기를 낚을 수 있는 낚싯대 레벨 (표에 없으면 1)</summary>
    public int MinRodLevelOf(string itemId)
    {
        if (fishTable != null)
        {
            foreach (var fish in fishTable)
            {
                if (fish.itemId == itemId)
                    return Mathf.Max(1, fish.minRodLevel);
            }
        }
        return 1;
    }

    // Average bite delay + animation, slowed down. ~100s with the defaults.
    public float OfflineSecPerCatch =>
        ((biteDelaySec.x + biteDelaySec.y) * 0.5f + onlineAnimationSecPerCatch) * offlineSlowdown;
}
