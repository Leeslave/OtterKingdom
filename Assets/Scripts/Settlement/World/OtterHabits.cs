using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 특성마다 광장에서 좋아하는 곳과 그곳에서 하는 동작 (해달마다 다른 개성).
/// 나무캐기 해달은 흔드는 나무 옆, 채굴 해달은 바위 옆, 장사 해달은 요정 상점 앞, 기록 해달은 게시판 앞 …
/// OtterWanderAgent가 가끔 그 근처로 걸어가 그 동작을 하며 머문다 (SettlementPlazaView.Spawn이 연결)
/// </summary>
public static class OtterHabits
{
    private static readonly Dictionary<OtterTrait, string[]> ActionTable = new Dictionary<OtterTrait, string[]>
    {
        { OtterTrait.Woodcutting, new[] { "Stretch", "Squat" } },
        { OtterTrait.Mining, new[] { "Squat", "Stretch" } },
        { OtterTrait.Farming, new[] { "Squat", "Net" } },
        { OtterTrait.Fishing, new[] { "Net", "Stretch" } },
        { OtterTrait.Building, new[] { "Squat", "Stretch", "Wave" } },
        { OtterTrait.Crafting, new[] { "Paint", "Write", "Happy" } },
        { OtterTrait.Hauling, new[] { "Squat", "Stretch" } },
        { OtterTrait.Trading, new[] { "Wave", "Happy" } },
        { OtterTrait.Exploring, new[] { "Wave", "Stretch" } },
        { OtterTrait.Recording, new[] { "Write", "Wave" } },
    };

    /// <summary>이 특성이 좋아하는 곳에서 고를 동작 (해달마다 있는 것만 씀, 특성이 없으면 null)</summary>
    public static IReadOnlyList<string> Actions(OtterTrait trait) => ActionTable.TryGetValue(trait, out var actions) ? actions : null;

    /// <summary>지금 광장에 켜져 있는, 이 특성이 좋아하는 곳들</summary>
    /// <param name="shore">바닷가 길 (낚시 해달)</param>
    public static void CollectTargets(OtterTrait trait, Transform shore, List<Transform> result)
    {
        result.Clear();
        switch (trait)
        {
            case OtterTrait.Woodcutting: Add<TreeShakeView>(result); break;
            case OtterTrait.Mining: Add<RockNodeView>(result); break;
            case OtterTrait.Farming:
                var sign = GameObject.Find("FarmSign");
                if (sign != null)
                    result.Add(sign.transform);
                break;
            case OtterTrait.Fishing:
                if (shore != null)
                    result.Add(shore);
                break;
            case OtterTrait.Building: Add<ConstructionSiteView>(result); Add<PlacedBuildingView>(result); break;
            case OtterTrait.Crafting: Add<PlacedDecorView>(result); break;
            case OtterTrait.Hauling: Add<GatherPointView>(result); break;
            case OtterTrait.Trading: Add<FairyNpcView>(result); break;
            case OtterTrait.Exploring: Add<TerritorySiteView>(result); break;
            case OtterTrait.Recording: Add<SettlementBoardPropView>(result); break;
        }
    }

    private static void Add<T>(List<Transform> result) where T : Component
    {
        foreach (var target in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            // 손재주 해달은 장난감 옆 (건물 · 건설 자리 · 가구는 뺌)
            if (typeof(T) == typeof(PlacedDecorView) && target is PlacedDecorView decor
                && (decor.Placed == null || decor.Placed.Decor.IsBuilding || decor.Placed.Decor.IsFurniture))
                continue;
            result.Add(target.transform);
        }
    }
}
