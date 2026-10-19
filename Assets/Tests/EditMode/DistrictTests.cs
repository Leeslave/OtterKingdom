using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 구역·이사: 블록 용도 정하기(열림 · 정해진 블록 · 첫 구역 주거 · 한 용도 한 블록 · 시설이 있어야 그 용도), 질서도(맞는 구역 비율 + 정한 블록 수),
/// 구역 보너스(기본 + 건물마다, 최대), 부지 칸(들어가는 가장 작은 빈 칸 · 가운데 아래 맞춤), 블록이 단계마다 커짐, 세이브 왕복,
/// 옛 세이브(열린 블록 용도 · 물건 옮기기), 실제 데이터(블록 단계 · 부지 칸 · 건물 구역 표 · 지도 배치와 영토 데이터).
/// </summary>
public class DistrictTests
{
    private const string ConfigPath = "Assets/Scriptable Obejects/Settlement/SettlementConfig.asset";
    private const string PlazaBoardPath = "Assets/Scriptable Obejects/Decor/Boards/Board_plaza.asset";

    private readonly List<Object> _created = new List<Object>();
    private SettlementConfig _config;
    private DistrictBlockDefinition _center;
    private DistrictBlockDefinition _a;
    private DistrictBlockDefinition _b;
    private DistrictBlockDefinition _c;
    private DistrictBlockDefinition _d;
    private DistrictBlockDefinition _e;

    [SetUp]
    public void SetUp()
    {
        _center = Block("center", DistrictType.Central, "", new RectInt(0, 0, 20, 20));
        _a = Block("a", DistrictType.None, "a_open", new RectInt(30, 0, 20, 20),
            new DistrictSlot("a_01", new RectInt(31, 1, 8, 5)), new DistrictSlot("a_02", new RectInt(41, 1, 4, 3)));
        _b = Block("b", DistrictType.None, "b_open", new RectInt(60, 0, 20, 20));
        _c = Block("c", DistrictType.None, "c_open", new RectInt(0, 30, 20, 20));
        _d = Block("d", DistrictType.None, "d_open", new RectInt(30, 30, 20, 20));
        _e = Block("e", DistrictType.None, "e_open", new RectInt(60, 30, 20, 20));
        _config = Create<SettlementConfig>();
        _config.SetupDistricts(new[] { _center, _a, _b, _c, _d, _e }, DistrictType.Residential, 60, 10, 10, 80, new[]
        {
            new DistrictBonusEntry(DistrictType.Residential, KingdomBonusKind.VisitSpeed, 0, 5, 10),
            new DistrictBonusEntry(DistrictType.Agricultural, KingdomBonusKind.GrowSpeed, 10, 0, 10),
        });
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _created)
            Object.DestroyImmediate(asset);
        _created.Clear();
    }

    #region 용도 정하기

    [Test]
    public void Designate_ClosedFixedFirstResidentialAndEditableTypes()
    {
        var settlement = new Settlement();
        Assert.AreEqual(DistrictDesignateBlock.Closed, DistrictRules.CheckDesignate(_config, settlement, _a, DistrictType.Residential), "숲 개간 전");
        Assert.AreEqual(DistrictDesignateBlock.Fixed, DistrictRules.CheckDesignate(_config, settlement, _center, DistrictType.Residential));
        Assert.AreEqual(DistrictDesignateBlock.Unknown, DistrictRules.CheckDesignate(_config, settlement, null, DistrictType.Residential));

        Open(settlement, "a_open", "b_open", "c_open", "d_open", "e_open");
        Assert.AreEqual(DistrictDesignateBlock.FirstDistrict, DistrictRules.Designate(_config, settlement, _a, DistrictType.Commercial), "첫 구역은 주거");
        Assert.AreEqual(DistrictType.None, settlement.GetDistrict("a"), "안 되면 바꾸지 않음");
        CollectionAssert.AreEqual(new[] { DistrictType.Residential }, DistrictRules.Choices(_config, settlement, _a));

        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, settlement, _a, DistrictType.Residential));
        Assert.AreEqual(DistrictType.Residential, DistrictRules.TypeOf(_a, settlement));
        Assert.AreEqual(DistrictType.Central, DistrictRules.TypeOf(_center, settlement), "정해진 블록");
        Assert.AreEqual(DistrictDesignateBlock.AlreadySet, DistrictRules.CheckDesignate(_config, settlement, _a, DistrictType.Residential));
        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.CheckDesignate(_config, settlement, _a, DistrictType.Commercial), "이미 지정한 구역도 변경 가능");
        Assert.AreEqual(DistrictDesignateBlock.NotAssignable, DistrictRules.CheckDesignate(_config, settlement, _b, DistrictType.Central));
        Assert.AreEqual(DistrictDesignateBlock.NotAssignable, DistrictRules.CheckDesignate(_config, settlement, _b, DistrictType.None));

        // 중앙 공터와 확장한 땅 등 여러 블록을 같은 용도로 쓸 수 있다.
        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.CheckDesignate(_config, settlement, _b, DistrictType.Residential));
        CollectionAssert.AreEqual(new[] { DistrictType.Residential, DistrictType.Commercial, DistrictType.Production, DistrictType.Agricultural },
            DistrictRules.Choices(_config, settlement, _b));
        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, settlement, _b, DistrictType.Commercial));
        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, settlement, _c, DistrictType.Production));
        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, settlement, _d, DistrictType.Agricultural));

        // 네 용도를 다 쓴 뒤에는 겹쳐도 됨
        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.CheckDesignate(_config, settlement, _e, DistrictType.Residential));
        Assert.AreEqual(4, DistrictRules.DesignatedCount(_config, settlement));
    }

    [Test]
    public void PendingBlocks_OpenAndNotDesignated()
    {
        var settlement = new Settlement();
        Assert.IsEmpty(DistrictRules.PendingBlocks(_config, settlement), "중앙은 정해져 있고 나머지는 닫힘");
        Open(settlement, "a_open", "b_open");
        CollectionAssert.AreEqual(new[] { _a, _b }, DistrictRules.PendingBlocks(_config, settlement));
        DistrictRules.Designate(_config, settlement, _a, DistrictType.Residential);
        CollectionAssert.AreEqual(new[] { _b }, DistrictRules.PendingBlocks(_config, settlement));
    }

    #endregion

    #region 질서도 · 보너스

    [Test]
    public void Order_MatchingRatioPlusDesignatedBlocks()
    {
        var settlement = new Settlement();
        var houseInCenter = new DistrictPlacement(DistrictType.Residential, new RectInt(2, 2, 6, 4));
        var houseInCenter2 = new DistrictPlacement(DistrictType.Residential, new RectInt(10, 2, 7, 4));
        var lamp = new DistrictPlacement(DistrictType.None, new RectInt(5, 10, 1, 1));

        var order = DistrictRules.Evaluate(_config, settlement, new DistrictPlacement[0]);
        Assert.AreEqual(0, order.Score);

        // 1막: 집이 공터에 아무렇게나 → 맞는 구역 0
        order = DistrictRules.Evaluate(_config, settlement, new[] { houseInCenter, houseInCenter2, lamp });
        Assert.AreEqual(2, order.Buildings, "구역이 없는 가로등은 안 셈");
        Assert.AreEqual(0, order.Matching);
        Assert.AreEqual(0, order.Score);

        // 첫 구역(주거)을 정함 → 블록 점수
        Open(settlement, "a_open");
        DistrictRules.Designate(_config, settlement, _a, DistrictType.Residential);
        order = DistrictRules.Evaluate(_config, settlement, new[] { houseInCenter, houseInCenter2 });
        Assert.AreEqual(10, order.Score);
        Assert.IsTrue(DistrictRules.CanRelocate(_config, order), "이사 지시서가 열림 (10)");

        // 한 채를 주거 구역으로 옮김 → 60 × 1/2 + 10
        var moved = new DistrictPlacement(DistrictType.Residential, new RectInt(31, 1, 6, 4));
        order = DistrictRules.Evaluate(_config, settlement, new[] { moved, houseInCenter2 });
        Assert.AreEqual(1, order.Matching);
        Assert.AreEqual(1, order.MatchingOf(DistrictType.Residential));
        Assert.AreEqual(40, order.Score);

        // 블록 경계에 걸치면 그 블록이 아님
        var straddling = new DistrictPlacement(DistrictType.Residential, new RectInt(47, 1, 6, 4));
        order = DistrictRules.Evaluate(_config, settlement, new[] { moved, straddling });
        Assert.AreEqual(1, order.Matching);

        // 다른 용도의 블록에 있으면 안 맞음, 기록관(중앙)은 공터에 있으면 맞음
        var archiveInCenter = new DistrictPlacement(DistrictType.Central, new RectInt(2, 12, 4, 3));
        var shopInHomes = new DistrictPlacement(DistrictType.Commercial, new RectInt(41, 1, 4, 3));
        order = DistrictRules.Evaluate(_config, settlement, new[] { moved, archiveInCenter, shopInHomes });
        Assert.AreEqual(3, order.Buildings);
        Assert.AreEqual(2, order.Matching);
        Assert.AreEqual(50, order.Score, "60 × 2/3 + 10");
        Assert.IsFalse(DistrictRules.CanBecomeCastle(_config, order));
    }

    [Test]
    public void Order_ClampsAtHundred()
    {
        var settlement = new Settlement();
        Open(settlement, "a_open", "b_open", "c_open", "d_open", "e_open");
        DistrictRules.Designate(_config, settlement, _a, DistrictType.Residential);
        DistrictRules.Designate(_config, settlement, _b, DistrictType.Commercial);
        DistrictRules.Designate(_config, settlement, _c, DistrictType.Production);
        DistrictRules.Designate(_config, settlement, _d, DistrictType.Agricultural);
        DistrictRules.Designate(_config, settlement, _e, DistrictType.Residential);
        var order = DistrictRules.Evaluate(_config, settlement, new[] { new DistrictPlacement(DistrictType.Residential, new RectInt(31, 1, 6, 4)) });
        Assert.AreEqual(100, order.Score, "60 + 50 → 100");
        Assert.IsTrue(DistrictRules.CanBecomeCastle(_config, order));
    }

    [Test]
    public void Bonus_FlatPlusPerBuildingCapped_OnlyWithDistrict()
    {
        var settlement = new Settlement();
        var homes = new[]
        {
            new DistrictPlacement(DistrictType.Residential, new RectInt(31, 1, 6, 4)),
            new DistrictPlacement(DistrictType.Residential, new RectInt(41, 1, 4, 3)),
            new DistrictPlacement(DistrictType.Residential, new RectInt(31, 8, 6, 4)),
        };
        var order = DistrictRules.Evaluate(_config, settlement, homes);
        Assert.AreEqual(0, DistrictRules.BonusPercent(_config, settlement, order, KingdomBonusKind.VisitSpeed), "구역이 없으면 보너스 없음");

        Open(settlement, "a_open", "b_open");
        DistrictRules.Designate(_config, settlement, _a, DistrictType.Residential);
        order = DistrictRules.Evaluate(_config, settlement, new[] { homes[0] });
        Assert.AreEqual(5, DistrictRules.BonusPercent(_config, settlement, order, KingdomBonusKind.VisitSpeed));
        order = DistrictRules.Evaluate(_config, settlement, homes);
        Assert.AreEqual(10, DistrictRules.BonusPercent(_config, settlement, order, KingdomBonusKind.VisitSpeed), "최대 10");
        Assert.AreEqual(0, DistrictRules.BonusPercent(_config, settlement, order, KingdomBonusKind.GrowSpeed));

        DistrictRules.Designate(_config, settlement, _b, DistrictType.Agricultural);
        Assert.AreEqual(10, DistrictRules.BonusPercent(_config, settlement, order, KingdomBonusKind.GrowSpeed), "농업: 정하기만 해도");
        Assert.AreEqual(0, DistrictRules.BonusPercent(_config, settlement, order, KingdomBonusKind.SellPrice));
    }

    #endregion

    #region 부지 칸

    [Test]
    public void Slots_SmallestFreeSlotThatFits_CenteredAtBottom()
    {
        var small = new Vector2Int(3, 2);
        var house = new Vector2Int(7, 4);
        Assert.AreEqual("a_02", DistrictRules.FreeSlot(_a, small, null).SlotId, "들어가는 가장 작은 칸");
        Assert.AreEqual("a_01", DistrictRules.FreeSlot(_a, house, null).SlotId);
        Assert.AreEqual("a_01", DistrictRules.FreeSlot(_a, small, new[] { new RectInt(42, 2, 1, 1) }).SlotId, "작은 칸이 차 있으면 큰 칸");
        Assert.IsNull(DistrictRules.FreeSlot(_a, small, new[] { new RectInt(42, 2, 1, 1), new RectInt(35, 3, 1, 1) }));
        Assert.IsNull(DistrictRules.FreeSlot(_a, new Vector2Int(9, 4), null), "큰 칸보다 큼");

        Assert.AreEqual(new Vector2Int(31, 1), DistrictRules.OriginIn(_a.FindSlot("a_01"), new Vector2Int(8, 5)));
        Assert.AreEqual(new Vector2Int(32, 1), DistrictRules.OriginIn(_a.FindSlot("a_01"), new Vector2Int(6, 4)), "가로 가운데, 아래 맞춤");
        Assert.AreEqual(new Vector2Int(31, 1), DistrictRules.OriginIn(_a.FindSlot("a_01"), house));
    }

    #endregion

    #region 세이브

    [Test]
    public void Save_RoundTrip_SkipsBadRows()
    {
        var settlement = new Settlement();
        settlement.SetDistrict("b", DistrictType.Commercial);
        settlement.SetDistrict("a", DistrictType.Residential);
        var saved = new SettlementSaveData();
        settlement.Write(saved);
        Assert.AreEqual(2, saved.districts.Count);
        Assert.AreEqual("a", saved.districts[0].blockId, "블록 ID 순");

        var json = JsonUtility.ToJson(saved);
        var loaded = new Settlement();
        loaded.Load(JsonUtility.FromJson<SettlementSaveData>(json));
        Assert.AreEqual(DistrictType.Residential, loaded.GetDistrict("a"));
        Assert.AreEqual(DistrictType.Commercial, loaded.GetDistrict("b"));

        saved.districts.Add(new DistrictSaveData { blockId = "c", type = DistrictType.None });
        saved.districts.Add(new DistrictSaveData { blockId = "d", type = (DistrictType)99 });
        saved.districts.Add(new DistrictSaveData { blockId = "a", type = DistrictType.Production });
        saved.districts.Add(null);
        loaded.Load(saved);
        Assert.AreEqual(2, loaded.DistrictDesignations.Count);
        Assert.AreEqual(DistrictType.Residential, loaded.GetDistrict("a"), "중복은 앞의 것");

        var old = JsonUtility.FromJson<SettlementSaveData>("{\"version\":4}");
        loaded.Load(old);
        Assert.AreEqual(0, loaded.DistrictDesignations.Count, "옛 세이브 = 아무 블록도 정하지 않음");
    }

    #endregion

    #region 블록 단계 · 시설 · 옛 세이브

    [Test]
    public void Stages_GrowTheBlockAndOpenSlots()
    {
        var grow = Create<DistrictBlockDefinition>();
        grow.Setup("grow", "grow", DistrictType.None, DistrictType.Residential, new[]
        {
            new DistrictStage("grow_1", new[] { new RectInt(30, 60, 10, 10) }),
            new DistrictStage("grow_2", new[] { new RectInt(40, 60, 10, 10) }),
        }, new[]
        {
            new DistrictSlot("grow_01", new RectInt(31, 61, 8, 5), 0),
            new DistrictSlot("grow_02", new RectInt(41, 61, 8, 5), 1),
        });
        _config.SetupDistricts(new[] { _center, grow }, DistrictType.Residential, 60, 10, 10, 80, null);
        var settlement = new Settlement();
        var house = new Vector2Int(7, 4);

        Assert.AreEqual(0, DistrictRules.OpenStages(grow, settlement));
        Assert.IsFalse(DistrictRules.IsOpen(grow, settlement));
        var skipped = new Settlement();
        skipped.UnlockDevelopment("grow_2");
        Assert.AreEqual(0, DistrictRules.OpenStages(grow, skipped), "앞 단계가 닫혀 있으면 뒤 단계도 안 셈");

        settlement.UnlockDevelopment("grow_1");
        Assert.AreEqual(1, DistrictRules.OpenStages(grow, settlement));
        DistrictRules.Designate(_config, settlement, grow, DistrictType.Residential);
        var taken = new[] { new RectInt(31, 61, 8, 5) };
        Assert.IsNull(DistrictRules.FreeSlot(grow, house, taken, DistrictRules.OpenStages(grow, settlement)), "2단계 부지 칸은 아직");
        var inSecond = new DistrictPlacement(DistrictType.Residential, new RectInt(41, 61, 7, 4));
        Assert.AreEqual(0, DistrictRules.Evaluate(_config, settlement, new[] { inSecond }).Matching, "아직 블록이 아닌 칸");

        settlement.UnlockDevelopment("grow_2");
        Assert.AreEqual(2, DistrictRules.OpenStages(grow, settlement));
        Assert.AreEqual("grow_02", DistrictRules.FreeSlot(grow, house, taken, DistrictRules.OpenStages(grow, settlement)).SlotId);
        Assert.AreEqual(1, DistrictRules.Evaluate(_config, settlement, new[] { inSecond }).Matching, "블록이 커지면 맞는 구역");
    }

    [Test]
    public void TypeUnlock_NeedsTheFacility()
    {
        _config.SetupDistrictTypeUnlocks(new[] { new DistrictTypeUnlock(DistrictType.Commercial, "shop_ready", "가게가 생기면") });
        var settlement = new Settlement();
        Open(settlement, "a_open", "b_open");
        DistrictRules.Designate(_config, settlement, _a, DistrictType.Residential);
        Assert.AreEqual(DistrictDesignateBlock.TypeLocked, DistrictRules.CheckDesignate(_config, settlement, _b, DistrictType.Commercial));
        CollectionAssert.DoesNotContain(DistrictRules.Choices(_config, settlement, _b), DistrictType.Commercial);
        Assert.IsTrue(DistrictRules.IsTypeUnlocked(_config, settlement, DistrictType.Production), "표에 없는 용도는 처음부터");

        settlement.UnlockDevelopment("shop_ready");
        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.CheckDesignate(_config, settlement, _b, DistrictType.Commercial));
    }

    [Test]
    public void OldSave_DesignatesOpenedBlocksWithSuggestedTypes()
    {
        var ne = Territory(TerritoryDirection.NorthEast, "territory_ne_1");
        var w = Territory(TerritoryDirection.West, "territory_w_1");
        var s = Territory(TerritoryDirection.South, "territory_s_1");
        _config.SetupTerritory(new[] { ne, w, s }, 6);
        var neBlock = StageBlock("ne", DistrictType.Residential, "territory_ne_1", new RectInt(30, 0, 20, 20));
        var wBlock = StageBlock("w", DistrictType.Agricultural, "territory_w_1", new RectInt(60, 0, 20, 20));
        var sBlock = StageBlock("s", DistrictType.Commercial, "territory_s_1", new RectInt(0, 30, 20, 20));
        _config.SetupDistricts(new[] { _center, neBlock, wBlock, sBlock }, DistrictType.Residential, 60, 10, 10, 80, null);
        _config.SetupDistrictTypeUnlocks(new[] { new DistrictTypeUnlock(DistrictType.Commercial, "shop_ready", "") });

        var settlement = new Settlement();
        Open(settlement, "territory_ne_1", "territory_w_1", "territory_s_1");
        var designated = CenterStartMigration.DesignateOpened(_config, settlement);
        CollectionAssert.AreEqual(new[] { neBlock, wBlock }, designated, "상업은 가게 전이라 비워 둠");
        Assert.AreEqual(DistrictType.Residential, settlement.GetDistrict("ne"));
        Assert.AreEqual(DistrictType.Agricultural, settlement.GetDistrict("w"));
        Assert.AreEqual(DistrictType.None, settlement.GetDistrict("s"));
    }

    [Test]
    public void OldSave_PlanDecor_ZoneSlotsThenShiftedIntoTheClearing()
    {
        // 공터 (0,0)~(20,20), 주거 블록 a (30..50) — 옛 광장 물건은 공터 밖
        var settlement = new Settlement();
        Open(settlement, "a_open");
        DistrictRules.Designate(_config, settlement, _a, DistrictType.Residential);
        var shift = CenterStartMigration.StartShift;
        var oldStart = CenterStartMigration.OldStart;
        var oldHouse = new RectInt(oldStart.x + 2, oldStart.y + 2, 6, 4);
        var oldChair = new RectInt(oldStart.x + 10, oldStart.y + 2, 3, 2);
        var oldBall = new RectInt(oldStart.x + 4, oldStart.y + 30, 1, 1);
        var kept = new RectInt(5, 5, 1, 1);
        var moves = CenterStartMigration.PlanDecor(_config, settlement, new[]
        {
            new PlacedForMigration(1, DistrictType.Residential, oldHouse),
            new PlacedForMigration(2, DistrictType.Residential, oldChair),
            new PlacedForMigration(3, DistrictType.None, oldBall),
            new PlacedForMigration(4, DistrictType.None, kept),
        });

        Assert.AreEqual(new Vector2Int(32, 1), moves[1], "집 → 주거 블록 큰 부지 칸 (가운데 아래)");
        Assert.AreEqual(new Vector2Int(41, 1), moves[2], "의자 → 작은 부지 칸");
        Assert.IsFalse(moves.ContainsKey(4), "열린 땅 위의 물건은 그대로");
        var ball = new RectInt(moves[3], Vector2Int.one);
        Assert.IsTrue(_center.Contains(ball, 1), "장난감은 공터 안으로");
        var wish = oldBall.position + shift;
        if (_center.Contains(new RectInt(wish, Vector2Int.one), 1) && wish != kept.position)
            Assert.AreEqual(wish, moves[3], "옛 광장 자리를 그대로 민 자리");
    }

    #endregion

    #region 실제 데이터

    [Test]
    public void Data_BlocksStagesAndSlotsFitTheBoardWithoutOverlap()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        var board = AssetDatabase.LoadAssetAtPath<DecorBoardDefinition>(PlazaBoardPath);
        Assert.IsNotNull(config);
        Assert.IsNotNull(board);
        var blocks = config.DistrictBlocks;
        Assert.AreEqual(6, blocks.Count, "중앙 + 4방향 + 동쪽");

        var expansions = new HashSet<string>();
        foreach (var territory in config.Territories)
            expansions.Add(territory.ExpandedDevelopment);

        var boardRect = new RectInt(-board.SaveOrigin, board.Size);
        int central = 0;
        var suggested = new HashSet<DistrictType>();
        var areas = new List<(string id, RectInt area)>();
        var slots = new List<(string id, RectInt area)>();
        foreach (var block in blocks)
        {
            Assert.IsNotNull(block);
            if (block.IsFixed)
            {
                Assert.AreEqual(DistrictType.Central, block.FixedType);
                Assert.IsTrue(string.IsNullOrEmpty(block.RequiredDevelopment), "중앙은 처음부터");
                central++;
            }
            else
            {
                // 공터 동쪽은 공터 · 북동 땅 모양을 채우는 1단계 블록 (바닷가 길이 지나가 부지 칸 1개, 예시 용도 없음)
                bool east = block.BlockId == "district_east";
                if (east)
                    Assert.AreEqual(DistrictType.None, block.SuggestedType, "동쪽은 예시 용도 없음");
                else
                    Assert.IsTrue(suggested.Add(block.SuggestedType), $"예시 용도가 겹침: {block.BlockId}");
                Assert.AreEqual(east ? 1 : 3, block.Stages.Count, $"{block.BlockId}: 단계 수");
                foreach (var stage in block.Stages)
                    Assert.IsTrue(expansions.Contains(stage.Development), $"{block.BlockId}: 단계 발전 '{stage.Development}'이 영토 확장에 없음");
                for (int s = 0; s < block.Stages.Count; s++)
                    Assert.IsTrue(block.Slots.Count(x => x.Stage == s) >= (east ? 1 : 2), $"{block.BlockId} {s + 1}단계 부지 칸이 모자람");
            }
            foreach (var stage in block.Stages)
            {
                foreach (var area in stage.Areas)
                {
                    Assert.IsTrue(Inside(boardRect, area), $"{block.BlockId} 영역이 격자 밖");
                    foreach (var other in areas)
                        Assert.IsFalse(area.Overlaps(other.area), $"{block.BlockId} · {other.id} 겹침");
                    areas.Add((block.BlockId, area));
                }
            }
            foreach (var slot in block.Slots)
            {
                Assert.IsTrue(block.Contains(slot.Area, slot.Stage + 1), $"{slot.SlotId}가 {slot.Stage + 1}단계까지의 블록 밖");
                foreach (var other in slots)
                    Assert.IsFalse(slot.Area.Overlaps(other.area), $"{slot.SlotId} · {other.id} 겹침");
                slots.Add((slot.SlotId, slot.Area));
            }
        }
        Assert.AreEqual(1, central);
        CollectionAssert.AreEquivalent(Districts.Assignable, suggested, "예시 용도 4개가 모두 있음");
        Assert.AreEqual(slots.Count, new HashSet<string>(slots.ConvertAll(s => s.id)).Count, "부지 칸 ID가 겹침");
    }

    [Test]
    public void Data_DistrictBuildingsFitFirstStageSlotsOfTheirSuggestedBlock()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        var expected = new Dictionary<string, DistrictType>
        {
            { "Plot_House1", DistrictType.Residential },
            { "Plot_House2", DistrictType.Residential },
            { "Plot_Chair", DistrictType.Residential },
            { "Building_SmallHouse", DistrictType.Residential },
            { "Building_Shop", DistrictType.Commercial },
            { "Building_Workshop", DistrictType.Production },
            { "Building_Quarry", DistrictType.Production },
            { "Building_Granary", DistrictType.Production },
            { "Plot_Lamp", DistrictType.None },
        };
        int checkedCount = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:DecorDefinition", new[] { "Assets/Scriptable Obejects/Decor" }))
        {
            var decor = AssetDatabase.LoadAssetAtPath<DecorDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (!(decor is IDistrictBuilding building))
                continue;
            if (expected.TryGetValue(decor.name, out var type))
            {
                Assert.AreEqual(type, building.District, decor.name);
                checkedCount++;
            }
            if (building.District == DistrictType.None || building.District == DistrictType.Central)
                continue;
            var block = FindSuggested(config, building.District);
            Assert.IsNotNull(block, $"{building.District} 블록이 없음");
            Assert.IsNotNull(DistrictRules.FreeSlot(block, decor.Footprint, null, 1), $"{decor.name} ({decor.Footprint})가 들어갈 1단계 부지 칸이 없음");
        }
        Assert.AreEqual(expected.Count, checkedCount);
    }

    [Test]
    public void Data_TerritoryLayoutMatchesTerritoriesAndBlocks()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        var layout = System.IO.File.ReadAllText("Assets/Art/Plaza/territory_layout.json");
        Assert.AreEqual(13, config.Territories.Count, "4방향 × 3단계 + 동쪽 1단계");
        foreach (var territory in config.Territories)
        {
            StringAssert.Contains($"\"development\": \"{territory.ExpandedDevelopment}\"", layout, $"{territory.ExpansionId}의 걷기 영역");
            for (int k = 1; k <= territory.Clearings.Count; k++)
                StringAssert.Contains($"\"development\": \"{territory.ClearingDevelopment(k)}\"", layout, $"{territory.ExpansionId} 개간 {k}의 숲");
            StringAssert.DoesNotContain($"\"development\": \"{territory.ClearingDevelopment(territory.Clearings.Count + 1)}\"", layout,
                $"{territory.ExpansionId}: 지도 숲 조각이 개간 수보다 많음");
        }
        Assert.IsNotNull(CenterStartMigration.StartBlock(config), "중앙 공터 블록");
    }

    #endregion

    #region 도우미

    private static DistrictBlockDefinition FindSuggested(SettlementConfig config, DistrictType type)
    {
        foreach (var block in config.DistrictBlocks)
        {
            if (block != null && block.SuggestedType == type)
                return block;
        }
        return null;
    }

    private static bool Inside(RectInt outer, RectInt inner) =>
        inner.xMin >= outer.xMin && inner.yMin >= outer.yMin && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;

    private static void Open(Settlement settlement, params string[] developments)
    {
        foreach (var id in developments)
            settlement.UnlockDevelopment(id);
    }

    private DistrictBlockDefinition Block(string id, DistrictType fixedType, string open, RectInt area, params DistrictSlot[] slots)
    {
        var block = Create<DistrictBlockDefinition>();
        block.Setup(id, id, fixedType, DistrictType.None, new[] { new DistrictStage(open, new[] { area }) }, slots);
        return block;
    }

    private DistrictBlockDefinition StageBlock(string id, DistrictType suggested, string open, RectInt area)
    {
        var block = Create<DistrictBlockDefinition>();
        block.Setup(id, id, DistrictType.None, suggested, new[] { new DistrictStage(open, new[] { area }) }, null);
        return block;
    }

    private TerritoryExpansionDefinition Territory(TerritoryDirection direction, string id)
    {
        var territory = Create<TerritoryExpansionDefinition>();
        territory.Setup(id, direction, 1, id, null, null);
        return territory;
    }

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    #endregion
}
