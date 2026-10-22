using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 낚시 개편 실제 수치 (FishingReworkConfig 에셋): 강화 단계마다 새로 닿는 구간에서 낚을 수 있는 물고기가 4종 이상
/// (거리 · 찌 깊이 · 릴 무게를 모두 맞춘 것). "초반에 멸치 한 마리밖에 못 잡음" 다시 생기지 않게
/// </summary>
public class FishingReworkBalanceTests
{
    private const string ConfigPath = "Assets/Scriptable Obejects/FishingRework/FishingReworkConfig.asset";

    private static bool Catchable(FishingReworkConfig config, FishDefinition fish, float from, float to, float maxDepth, float reel)
    {
        if (fish.MaxDistance < from || fish.MinDistance > to || fish.MinWeight > reel)
            return false;
        // 겹치는 거리 안에서 한 곳이라도 바늘이 닿으면 됨
        float lo = Mathf.Max(from, fish.MinDistance), hi = Mathf.Min(to, fish.MaxDistance);
        for (float x = lo; x <= hi + 0.01f; x += 0.5f)
        {
            float seabed = config.SeabedDepth(x);
            float shallowest = fish.NearBottom ? seabed - fish.MaxDepth : Mathf.Max(fish.MinDepth, config.FishTopDepth);
            bool lives = fish.NearBottom || fish.MinDepth <= seabed - 0.5f;
            if (lives && shallowest <= maxDepth)
                return true;
        }
        return false;
    }

    [Test]
    public void EveryLevel_OpensAtLeastFourCatchableSpecies()
    {
        var config = AssetDatabase.LoadAssetAtPath<FishingReworkConfig>(ConfigPath);
        if (config == null)
            Assert.Ignore("낚시 개편 데이터가 없음 (Tools/Fishing Rework/Build Test Scene)");
        var line = config.Track(FishingUpgrade.Line);
        var sinker = config.Track(FishingUpgrade.Sinker);
        var reel = config.Track(FishingUpgrade.Reel);
        float from = 0f;
        for (int level = 0; level <= line.MaxLevel; level++)
        {
            float to = line.ValueAt(level);
            var names = new List<string>();
            foreach (var fish in config.Fish)
            {
                if (fish != null && Catchable(config, fish, from, to, sinker.ValueAt(level), reel.ValueAt(level)))
                    names.Add(fish.DisplayName);
            }
            Assert.GreaterOrEqual(names.Count, 4, $"{level + 1}단계 ({from:0}~{to:0}m): {string.Join(", ", names)}");
            from = to;
        }
    }

    /// <summary>
    /// 강화 재료(물고기): 모든 단계에 재료가 있고, 어떤 강화 상태든 가장 낮은 강화의 재료는 지금 낚을 수 있음 (막히지 않음).
    /// 재료는 전설 · 에픽이 아님
    /// </summary>
    [Test]
    public void UpgradeRecipes_AreAlwaysReachable()
    {
        var config = AssetDatabase.LoadAssetAtPath<FishingReworkConfig>(ConfigPath);
        if (config == null)
            Assert.Ignore("낚시 개편 데이터가 없음 (Tools/Fishing Rework/Build Test Scene)");
        var upgrades = new[] { FishingUpgrade.Line, FishingUpgrade.Sinker, FishingUpgrade.Reel };
        int max = config.Track(FishingUpgrade.Line).MaxLevel;
        foreach (var upgrade in upgrades)
        {
            for (int level = 1; level <= config.Track(upgrade).MaxLevel; level++)
            {
                var recipe = config.RecipeFor(upgrade, level);
                Assert.IsNotEmpty(recipe, $"{upgrade} Lv.{level}");
                foreach (var cost in recipe)
                {
                    Assert.Greater(cost.Amount, 0);
                    Assert.LessOrEqual(cost.Fish.Rarity, FishRarity.Rare, $"{upgrade} Lv.{level}: {cost.Fish.DisplayName}");
                }
            }
        }
        // 셋의 단계가 어떻게 섞여 있든 (0..max), 가장 낮은 강화의 재료는 지금 낚을 수 있음
        for (int l = 0; l < max; l++)
        for (int s = 0; s < max; s++)
        for (int r = 0; r < max; r++)
        {
            int low = Mathf.Min(l, Mathf.Min(s, r));
            var lowest = l == low ? FishingUpgrade.Line : s == low ? FishingUpgrade.Sinker : FishingUpgrade.Reel;
            foreach (var cost in config.RecipeFor(lowest, low + 1))
                Assert.IsTrue(config.Catchable(cost.Fish, l, s, r), $"({l},{s},{r}) {lowest} → {cost.Fish.DisplayName}");
        }
    }
}
