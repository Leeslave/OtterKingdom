using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 구역·이사 4단계 이사 지시서: 맞지 않는 구역의 같은 용도 건물로 지시서(빈 부지 칸이 있는 만큼만, 최대 3채),
/// 이사 중이면 아직 제자리가 아님(질서도에서도), 끝나는 시각이 지나면 이사 끝, 없어진 건물은 끝낸 것으로, 빈 부지 칸 찾기, 세이브 왕복.
/// </summary>
public class RelocationTests
{
    private readonly List<Object> _created = new List<Object>();
    private SettlementConfig _config;
    private DistrictBlockDefinition _home;
    private DistrictBlockDefinition _shop;

    [SetUp]
    public void SetUp()
    {
        // 주거 블록: 큰 칸 2개 (8×5), 작은 칸 1개 (4×3) / 상업 블록: 큰 칸 1개
        _home = Block("home", "home_open", new RectInt(0, 0, 30, 20),
            new DistrictSlot("home_01", new RectInt(1, 1, 8, 5)), new DistrictSlot("home_02", new RectInt(11, 1, 8, 5)),
            new DistrictSlot("home_03", new RectInt(21, 1, 4, 3)));
        _shop = Block("shop", "shop_open", new RectInt(40, 0, 20, 20), new DistrictSlot("shop_01", new RectInt(41, 1, 8, 5)));
        _config = Create<SettlementConfig>();
        _config.SetupDistricts(new[] { _home, _shop }, DistrictType.Residential, 60, 10, 10, 80, null);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _created)
            Object.DestroyImmediate(asset);
        _created.Clear();
    }

    private Settlement HomeDistrict()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment("home_open");
        settlement.SetDistrict("home", DistrictType.Residential);
        return settlement;
    }

    private static RelocationCandidate House(int id, int x, int y) => new RelocationCandidate(id, DistrictType.Residential, new RectInt(x, y, 6, 4));

    [Test]
    public void PickOrder_MisfitsWithFreeSlots_SameType()
    {
        var settlement = HomeDistrict();
        var candidates = new[]
        {
            House(1, 2, 1),                                                          // 이미 주거 칸 안 (제자리)
            House(2, 100, 100),                                                      // 밖
            House(3, 110, 100),                                                      // 밖
            House(4, 120, 100),                                                      // 밖 — 큰 칸이 하나 남아 지시서에 못 들어감
            new RelocationCandidate(5, DistrictType.Commercial, new RectInt(130, 100, 6, 4)), // 상업 구역이 없음
        };
        var occupied = new List<RectInt> { candidates[0].Area };

        var order = RelocationRules.PickOrder(_config, settlement, candidates, occupied, 1);

        Assert.IsNotNull(order);
        Assert.AreEqual(DistrictType.Residential, order.Type);
        CollectionAssert.AreEqual(new[] { 2 }, order.InstanceIds, "큰 칸은 home_02 하나만 비어 있음 (home_01은 1번 집, home_03은 작음)");
    }

    [Test]
    public void PickOrder_NullWhenNothingToMove()
    {
        var settlement = HomeDistrict();
        Assert.IsNull(RelocationRules.PickOrder(_config, settlement, new[] { House(1, 2, 1) }, null, 1));
        Assert.IsNull(RelocationRules.PickOrder(_config, new Settlement(), new[] { House(1, 100, 100) }, null, 1), "구역이 없으면 지시서 없음");
    }

    [Test]
    public void FreeSlots_SmallestFirstAndSkipsTaken()
    {
        var settlement = HomeDistrict();
        var chair = RelocationRules.FreeSlots(_config, settlement, DistrictType.Residential, new Vector2Int(3, 2), new[] { new RectInt(1, 1, 8, 5) });
        CollectionAssert.AreEqual(new[] { "home_03", "home_02" }, chair.ConvertAll(s => s.Slot.SlotId));
        Assert.AreEqual(0, RelocationRules.FreeSlots(_config, settlement, DistrictType.Commercial, new Vector2Int(3, 2), null).Count, "닫힌 · 용도 없는 블록");
    }

    [Test]
    public void Moving_NotDoneUntilFinished_AndNotMatchingInOrderScore()
    {
        var settlement = HomeDistrict();
        var order = new RelocationOrder(1, DistrictType.Residential, new[] { 2 });
        var moved = new[] { House(2, 12, 1) }; // 이미 새 자리 (home_02)

        settlement.StartRelocation(2, "home_02", 100, 200);
        Assert.AreEqual(0, RelocationRules.DoneCount(_config, settlement, order, moved), "이사 중이면 아직");
        var score = DistrictRules.Evaluate(_config, settlement, new[] { new DistrictPlacement(DistrictType.Residential, moved[0].Area, moving: true) });
        Assert.AreEqual(0, score.Matching, "이사 중인 건물은 질서도에서도 아직 안 맞음");

        Assert.AreEqual(0, settlement.FinishRelocations(150).Count);
        CollectionAssert.AreEqual(new[] { 2 }, settlement.FinishRelocations(200));
        Assert.IsTrue(RelocationRules.IsDone(_config, settlement, order, moved));
        Assert.IsNull(RelocationRules.PickOrder(_config, settlement, moved, new[] { moved[0].Area }, 2), "제자리면 다음 지시서 없음");
    }

    [Test]
    public void RemovedBuilding_CountsAsDone_AndCompleteCounts()
    {
        var settlement = HomeDistrict();
        var order = new RelocationOrder(1, DistrictType.Residential, new[] { 2, 3 });
        Assert.AreEqual(1, RelocationRules.DoneCount(_config, settlement, order, new[] { House(2, 100, 100) }));

        settlement.SetRelocationOrder(order);
        settlement.CompleteRelocationOrder();
        Assert.IsNull(settlement.RelocationOrder);
        Assert.AreEqual(1, settlement.RelocationOrdersDone);
        Assert.AreEqual(2 * _config.RelocationGoldPerBuilding, RelocationRules.RewardGold(_config, order));
    }

    [Test]
    public void Save_RoundTrip_AndOldSave()
    {
        var settlement = HomeDistrict();
        settlement.SetRelocationOrder(new RelocationOrder(3, DistrictType.Residential, new[] { 4, 7 }));
        settlement.StartRelocation(7, "home_02", 10, 900);
        var save = new SettlementSaveData();
        settlement.Write(save);

        var loaded = new Settlement();
        loaded.Load(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(save)));
        Assert.AreEqual(3, loaded.RelocationOrder.Serial);
        CollectionAssert.AreEqual(new[] { 4, 7 }, loaded.RelocationOrder.InstanceIds);
        Assert.IsTrue(loaded.TryGetRelocation(7, out var move));
        Assert.AreEqual("home_02", move.SlotId);
        Assert.AreEqual(900, move.EndUtcTicks);

        save.relocation = null; // 옛 세이브
        var old = new Settlement();
        old.Load(save);
        Assert.IsNull(old.RelocationOrder);
        Assert.AreEqual(0, old.RelocationMoves.Count);
    }

    private DistrictBlockDefinition Block(string id, string open, RectInt area, params DistrictSlot[] slots)
    {
        var block = Create<DistrictBlockDefinition>();
        block.Setup(id, id, DistrictType.None, DistrictType.None, new[] { new DistrictStage(open, new[] { area }) }, slots);
        return block;
    }

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }
}
