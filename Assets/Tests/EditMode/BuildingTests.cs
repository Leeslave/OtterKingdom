using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 자리를 골라 짓는 건물 (P4): 해금 순서(레벨 → 발전 → 특성 → 최대 수), 시작 조건(공사 자리 → 건설 해달 → 골드 → 재료),
/// 늘어나는 비용, 공사 완료 · 다 지은 집 = 빈 집(장난감 해달 입주), 시설은 집이 아님, 세이브 왕복, 개발용 시간 줄이기,
/// 꾸미기 세이브의 건물 ID · 세이브 기준 칸(격자를 넓혀도 옛 자리 그대로).
/// </summary>
public class BuildingTests
{
    private readonly List<Object> _created = new List<Object>();

    private ItemDefinition _wood;
    private SettlementOtterDefinition _lumber;
    private SettlementOtterDefinition _toy;
    private BuildingDefinition _house;
    private BuildingDefinition _workshop;
    private SettlementConfig _config;

    private static readonly long Now = new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc).Ticks;

    [SetUp]
    public void SetUp()
    {
        _wood = Create<ItemDefinition>();
        var itemSo = new SerializedObject(_wood);
        itemSo.FindProperty("_itemId").stringValue = "material_wood";
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        _lumber = Otter("otter_lumber", OtterTrait.Woodcutting, false);
        _toy = Otter("otter_toy", OtterTrait.Fishing, true);
        _config = Create<SettlementConfig>();
        SetList(_config, "_otters", new Object[] { _lumber, _toy });

        _house = Building("bld_small_house", BuildingKind.Home, 0, "town_hall_built", null, 0, 600, 20, 0.3f);
        _workshop = Building("bld_workshop", BuildingKind.Facility, 8, "", new[] { new TraitRequirement(OtterTrait.Woodcutting, 2) }, 1, 1000, 0, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _created)
            Object.DestroyImmediate(asset);
        _created.Clear();
    }

    #region 해금 · 시작 조건

    [Test]
    public void Unlocked_ChecksLevelThenDevelopmentThenTraitsThenMax()
    {
        var settlement = new Settlement();
        Assert.AreEqual(BuildingBlock.Development, BuildingRules.CheckUnlocked(_config, settlement, _house, 1));
        settlement.UnlockDevelopment("town_hall_built");
        Assert.AreEqual(BuildingBlock.None, BuildingRules.CheckUnlocked(_config, settlement, _house, 1));

        Assert.AreEqual(BuildingBlock.Level, BuildingRules.CheckUnlocked(_config, settlement, _workshop, 7));
        Assert.AreEqual(BuildingBlock.Traits, BuildingRules.CheckUnlocked(_config, settlement, _workshop, 8));
        settlement.SetResident(_lumber.OtterId, ResidentState.Resident);
        Assert.AreEqual(BuildingBlock.Traits, BuildingRules.CheckUnlocked(_config, settlement, _workshop, 8), "나무캐기 1/2");

        var second = Otter("otter_lumber_2", OtterTrait.Woodcutting, false);
        SetList(_config, "_otters", new Object[] { _lumber, _toy, second });
        settlement.SetResident(second.OtterId, ResidentState.Resident);
        Assert.AreEqual(BuildingBlock.None, BuildingRules.CheckUnlocked(_config, settlement, _workshop, 8));

        settlement.StartBuilding(1, _workshop.BuildingId, Now, Now + 1);
        Assert.AreEqual(BuildingBlock.MaxCount, BuildingRules.CheckUnlocked(_config, settlement, _workshop, 8), "최대 1채");
    }

    [Test]
    public void Start_ChecksSlotBuilderGoldItems()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment("town_hall_built");
        Func<ItemDefinition, int> plenty = _ => 999;

        Assert.AreEqual(BuildingBlock.NoBuilder, BuildingRules.CheckStart(_config, settlement, _house, 1, false, 9999, plenty));
        Assert.AreEqual(BuildingBlock.Gold, BuildingRules.CheckStart(_config, settlement, _house, 1, true, 599, plenty));
        Assert.AreEqual(BuildingBlock.Items, BuildingRules.CheckStart(_config, settlement, _house, 1, true, 600, _ => 19));
        Assert.AreEqual(BuildingBlock.None, BuildingRules.CheckStart(_config, settlement, _house, 1, true, 600, _ => 20));

        settlement.StartBuilding(1, _house.BuildingId, Now, Now + TimeSpan.FromMinutes(3).Ticks);
        Assert.AreEqual(BuildingBlock.Busy, BuildingRules.CheckStart(_config, settlement, _house, 1, true, 9999, plenty), "한 번에 한 채");
    }

    [Test]
    public void Cost_GrowsPerBuildingOfSameKind()
    {
        var settlement = new Settlement();
        Assert.AreEqual(600, BuildingRules.GoldCost(settlement, _house));
        Assert.AreEqual(20, BuildingRules.ItemCosts(settlement, _house)[0].Amount);

        settlement.StartBuilding(1, _house.BuildingId, Now, Now);
        Assert.AreEqual(780, BuildingRules.GoldCost(settlement, _house), "+30%");
        Assert.AreEqual(26, BuildingRules.ItemCosts(settlement, _house)[0].Amount);
        Assert.AreEqual(1000, BuildingRules.GoldCost(settlement, _workshop), "다른 건물은 그대로");
    }

    #endregion

    #region 공사 · 집

    [Test]
    public void FinishDue_HomeBecomesFreeHomeForToyVisitor()
    {
        var settlement = new Settlement();
        Assert.IsTrue(BuildingRules.Start(settlement, _house, 7, Now, 180));
        Assert.IsFalse(BuildingRules.Start(settlement, _house, 7, Now, 180), "같은 배치는 한 번");
        Assert.AreEqual(1, settlement.ActiveBuildingCount);
        Assert.AreEqual(0.5f, settlement.FindBuilding(7).Progress(Now + TimeSpan.FromSeconds(90).Ticks), 0.001f);

        var finished = new List<BuildingRecord>();
        BuildingRules.FinishDue(settlement, Now + TimeSpan.FromSeconds(179).Ticks, finished);
        Assert.AreEqual(0, finished.Count);
        BuildingRules.FinishDue(settlement, Now + TimeSpan.FromSeconds(180).Ticks, finished);
        Assert.AreEqual(1, finished.Count);
        Assert.IsTrue(settlement.FindBuilding(7).Built);
        Assert.AreEqual(0, settlement.ActiveBuildingCount);

        Assert.AreEqual(1, BuildingRules.SyncHomes(settlement, Find));
        Assert.AreEqual(0, BuildingRules.SyncHomes(settlement, Find), "다시 해도 같음");
        var home = settlement.FindHouseBySlot(BuildingRules.HomeSlotId(7));
        Assert.IsNotNull(home);
        Assert.AreEqual(home, ToyVisitRules.FindFreeHome(_config, settlement), "장난감 해달이 들어갈 빈 집");

        settlement.SetResident(_toy.OtterId, ResidentState.Visitor);
        settlement.MarkMet(_toy.OtterId);
        Assert.IsTrue(ToyVisitRules.MoveIn(_config, settlement, _toy));
        Assert.AreEqual(_toy.OtterId, home.ResidentId);
    }

    [Test]
    public void Facility_IsNotAHome()
    {
        var settlement = new Settlement();
        BuildingRules.Start(settlement, _workshop, 3, Now, 1);
        BuildingRules.FinishDue(settlement, Now + TimeSpan.FromSeconds(1).Ticks, new List<BuildingRecord>());
        Assert.AreEqual(0, BuildingRules.SyncHomes(settlement, Find));
        Assert.AreEqual(1, settlement.CountBuilt(_workshop.BuildingId));
    }

    [Test]
    public void SaveRoundTrip_AndDevShorten()
    {
        var settlement = new Settlement();
        BuildingRules.Start(settlement, _house, 4, Now, 600);
        BuildingRules.Start(settlement, _workshop, 5, Now, 1);
        settlement.FinishBuilding(5);

        var saved = new SettlementSaveData();
        settlement.Write(saved);
        var loaded = new Settlement();
        loaded.Load(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(saved)));
        Assert.AreEqual(2, loaded.Buildings.Count);
        Assert.IsFalse(loaded.FindBuilding(4).Built);
        Assert.AreEqual(Now + TimeSpan.FromSeconds(600).Ticks, loaded.FindBuilding(4).EndUtcTicks);
        Assert.IsTrue(loaded.FindBuilding(5).Built);

        long half = Now + TimeSpan.FromSeconds(300).Ticks;
        Assert.IsTrue(loaded.ShortenTimers(half, TimeSpan.FromSeconds(5)));
        Assert.LessOrEqual(loaded.FindBuilding(4).EndUtcTicks - half, TimeSpan.FromSeconds(5).Ticks);
        Assert.AreEqual(0.5f, loaded.FindBuilding(4).Progress(half), 0.01f, "진행 비율은 그대로");

        var old = new Settlement();
        old.Load(new SettlementSaveData { buildings = null });
        Assert.AreEqual(0, old.Buildings.Count, "옛 세이브");
    }

    #endregion

    #region 꾸미기 세이브

    [Test]
    public void DecorSave_WritesBuildingIdAndShiftsBySaveOrigin()
    {
        var toy = Create<DecorDefinition>();
        var toyItem = Create<ItemDefinition>();
        var toySo = new SerializedObject(toyItem);
        toySo.FindProperty("_itemId").stringValue = "toy_ball";
        toySo.ApplyModifiedPropertiesWithoutUndo();
        var decorSo = new SerializedObject(toy);
        decorSo.FindProperty("_item").objectReferenceValue = toyItem;
        decorSo.ApplyModifiedPropertiesWithoutUndo();
        var catalog = Create<DecorCatalog>();
        catalog.Setup(new[] { toy }, new[] { _house });

        var origin = new Vector2Int(77, 0);
        var layout = new DecorLayout(new Vector2Int(101, 40));
        layout.AddRegion("base", new[] { new RectInt(0, 0, 101, 40) }, true);
        Assert.AreEqual(DecorPlacementResult.Ok, layout.TryPlace(toy, new Vector2Int(80, 5), DecorRotation.R0, out _));
        Assert.AreEqual(DecorPlacementResult.Ok, layout.TryPlace(_house, new Vector2Int(10, 20), DecorRotation.R90, out var house));
        Assert.AreEqual(DecorRotation.R0, house.Rotation, "돌리기를 끈 건물은 0도");

        var saved = new DecorBoardSaveData { boardId = "plaza" };
        DecorSaveConverter.Write(layout, saved, origin);
        Assert.AreEqual("toy_ball", saved.placed[0].itemId);
        Assert.AreEqual(3, saved.placed[0].x, "세이브 = 칸 − 기준");
        Assert.AreEqual("bld_small_house", saved.placed[1].itemId);
        Assert.AreEqual(-67, saved.placed[1].x, "넓힌 쪽(서쪽)은 음수");

        var loaded = new DecorLayout(new Vector2Int(101, 40));
        loaded.AddRegion("base", new[] { new RectInt(0, 0, 101, 40) }, true);
        Assert.AreEqual(0, DecorSaveConverter.Read(saved, loaded, catalog, origin));
        Assert.IsTrue(loaded.TryGet(house.InstanceId, out var back));
        Assert.AreEqual(_house, back.Decor);
        Assert.AreEqual(new Vector2Int(10, 20), back.Origin);

        // 넓히기 전 세이브 (기준 칸 없이 0, 0 기준으로 저장됨)
        var oldSave = new DecorBoardSaveData { boardId = "plaza" };
        oldSave.placed.Add(new PlacedDecorSaveEntry(1, "toy_ball", 3, 5, DecorRotation.R0));
        var expanded = new DecorLayout(new Vector2Int(101, 40));
        expanded.AddRegion("base", new[] { new RectInt(0, 0, 101, 40) }, true);
        DecorSaveConverter.Read(oldSave, expanded, catalog, origin);
        Assert.IsTrue(expanded.TryGet(1, out var oldToy));
        Assert.AreEqual(new Vector2Int(80, 5), oldToy.Origin, "옛 자리 그대로");
    }

    #endregion

    #region 돌리기

    [Test]
    public void RotatedBuilding_FootprintTurnsForOverlap()
    {
        var house = RotatableHouse();
        var layout = OpenLayout();

        Assert.AreEqual(DecorPlacementResult.Ok, layout.TryPlace(house, new Vector2Int(2, 2), DecorRotation.R90, out var placed));
        Assert.AreEqual(DecorRotation.R90, placed.Rotation);
        Assert.AreEqual(new Vector2Int(3, 4), placed.Size, "4×3 → 3×4");
        Assert.AreEqual(placed, layout.GetAt(new Vector2Int(4, 5)), "돌린 칸의 위쪽 끝");
        Assert.IsNull(layout.GetAt(new Vector2Int(5, 2)), "돌리기 전이면 차지했을 칸은 비어 있음");

        // 빈 칸이 된 오른쪽에는 놓을 수 있고, 돌린 칸과 겹치면 안 됨
        var toy = Create<DecorDefinition>();
        toy.SetupPlacement(Vector2Int.one, false, null);
        Assert.AreEqual(DecorPlacementResult.Ok, layout.Check(toy, new Vector2Int(5, 3), DecorRotation.R0));
        Assert.AreEqual(DecorPlacementResult.Occupied, layout.Check(toy, new Vector2Int(3, 5), DecorRotation.R0));
        Assert.AreEqual(DecorPlacementResult.Occupied, layout.Check(house, new Vector2Int(4, 4), DecorRotation.R0));

        // 옮기면서 다시 돌림 (0도로 돌아오면 다시 4×3)
        Assert.AreEqual(DecorPlacementResult.Ok, layout.TryMove(placed.InstanceId, new Vector2Int(2, 2), DecorRotation.R180));
        Assert.AreEqual(new Vector2Int(4, 3), placed.Size);
        Assert.AreEqual(placed, layout.GetAt(new Vector2Int(5, 2)));
        Assert.IsNull(layout.GetAt(new Vector2Int(2, 5)));
    }

    [Test]
    public void RotatedBuilding_KeepsDoorstepBelow()
    {
        var house = RotatableHouse();
        var layout = OpenLayout();
        layout.TryPlace(house, new Vector2Int(2, 2), DecorRotation.R270, out _);

        var toy = Create<DecorDefinition>();
        toy.SetupPlacement(Vector2Int.one, false, null);
        Assert.AreEqual(DecorPlacementResult.Occupied, layout.Check(toy, new Vector2Int(4, 1), DecorRotation.R0), "돌린 집도 발밑 아래 줄은 비워 둠");
        Assert.AreEqual(DecorPlacementResult.Ok, layout.Check(toy, new Vector2Int(5, 1), DecorRotation.R0), "돌린 칸(3칸 폭) 밖은 문 앞이 아님");
    }

    [Test]
    public void RotatedBuilding_SaveRoundTrip()
    {
        var house = RotatableHouse();
        var catalog = Create<DecorCatalog>();
        catalog.Setup(new DecorDefinition[0], new[] { house });
        var layout = OpenLayout();
        layout.TryPlace(house, new Vector2Int(2, 2), DecorRotation.R270, out var placed);

        var saved = new DecorBoardSaveData { boardId = "plaza" };
        DecorSaveConverter.Write(layout, saved);
        var loaded = OpenLayout();
        Assert.AreEqual(0, DecorSaveConverter.Read(JsonUtility.FromJson<DecorBoardSaveData>(JsonUtility.ToJson(saved)), loaded, catalog));

        Assert.IsTrue(loaded.TryGet(placed.InstanceId, out var back));
        Assert.AreEqual(DecorRotation.R270, back.Rotation);
        Assert.AreEqual(new RectInt(2, 2, 3, 4), back.Area, "불러와도 돌린 칸 그대로");
        Assert.AreEqual(back, loaded.GetAt(new Vector2Int(4, 5)));
    }

    [Test]
    public void FarmPart_DoesNotRotate()
    {
        var field = Create<BuildingDefinition>();
        field.Setup("bld_farm_field", "밭", BuildingKind.Facility, 0, null, null, 0, 0, null, 0f, 60f);
        field.SetupPlacement(new Vector2Int(3, 3), false, null);
        var so = new SerializedObject(field);
        so.FindProperty("_farmPart").intValue = (int)FarmPartKind.Field;
        so.ApplyModifiedPropertiesWithoutUndo();

        OpenLayout().TryPlace(field, new Vector2Int(2, 2), DecorRotation.R90, out var placed);
        Assert.AreEqual(DecorRotation.R0, placed.Rotation);
        Assert.IsFalse(DecorEditSession.ForNew(field, Vector2Int.zero).CanRotate, "회전 버튼이 안 보임");
    }

    [Test]
    public void Picture_MirrorsAt90And180_UnlessOwnPicture()
    {
        var house = RotatableHouse();
        var finished = CreateSprite();
        var stage = CreateSprite();
        house.SetupPlacement(new Vector2Int(4, 3), true, finished);

        Assert.IsFalse(house.FlipsAt(DecorRotation.R0, finished));
        Assert.IsTrue(house.FlipsAt(DecorRotation.R90, finished));
        Assert.IsTrue(house.FlipsAt(DecorRotation.R180, finished));
        Assert.IsFalse(house.FlipsAt(DecorRotation.R270, finished));
        Assert.AreEqual(finished, house.FinishedSprite(DecorRotation.R90));

        // 90도 전용 그림을 넣으면 그 그림은 뒤집지 않고, 공사 단계 그림은 여전히 뒤집음
        var side = CreateSprite();
        var so = new SerializedObject(house);
        so.FindProperty("_rotationSprites").GetArrayElementAtIndex(1).objectReferenceValue = side;
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreEqual(side, house.FinishedSprite(DecorRotation.R90));
        Assert.IsFalse(house.FlipsAt(DecorRotation.R90, side));
        Assert.IsTrue(house.FlipsAt(DecorRotation.R90, stage));
    }

    [Test]
    public void BuildingAssets_RotateExceptFarmParts()
    {
        int rotatable = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:BuildingDefinition", new[] { "Assets/Scriptable Obejects/Decor/Buildings" }))
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (building == null)
                continue;
            Assert.AreEqual(!building.IsFarmPart, building.CanRotate, $"{building.name}: 농장 부품만 돌리지 않음");
            if (building.CanRotate)
                rotatable++;
        }
        Assert.Greater(rotatable, 0);
    }

    private BuildingDefinition RotatableHouse()
    {
        var house = Create<BuildingDefinition>();
        house.Setup("bld_rot_house", "집", BuildingKind.Home, 0, null, null, 0, 0, null, 0f, 60f);
        house.SetupPlacement(new Vector2Int(4, 3), true, null);
        return house;
    }

    private static DecorLayout OpenLayout()
    {
        var layout = new DecorLayout(new Vector2Int(12, 12));
        layout.AddRegion("base", new[] { new RectInt(0, 0, 12, 12) }, true);
        return layout;
    }

    private Sprite CreateSprite()
    {
        var texture = new Texture2D(4, 4);
        _created.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f));
        _created.Add(sprite);
        return sprite;
    }

    #endregion

    #region 만들기

    private BuildingDefinition Find(string id) => id == _house.BuildingId ? _house : id == _workshop.BuildingId ? _workshop : null;

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        _created.Add(asset);
        return asset;
    }

    private BuildingDefinition Building(string id, BuildingKind kind, int level, string development, TraitRequirement[] traits, int max,
        int gold, int wood, float growth)
    {
        var building = Create<BuildingDefinition>();
        var items = wood > 0 ? new[] { new ItemAmount(_wood, wood) } : null;
        building.Setup(id, id, kind, level, development, traits, max, gold, items, growth, 180f);
        building.SetupPlacement(new Vector2Int(4, 3), false, null);
        return building;
    }

    private SettlementOtterDefinition Otter(string id, OtterTrait trait, bool toy)
    {
        var otter = Create<SettlementOtterDefinition>();
        var so = new SerializedObject(otter);
        so.FindProperty("_otterId").stringValue = id;
        so.FindProperty("_trait").intValue = (int)trait;
        so.FindProperty("_toyVisitor").boolValue = toy;
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    private static void SetList(Object target, string property, Object[] values)
    {
        var so = new SerializedObject(target);
        var list = so.FindProperty(property);
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion
}
