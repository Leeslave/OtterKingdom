using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DistrictGuideTests
{
    private SettlementConfig _config;
    private DistrictBlockDefinition _first;
    private DistrictBlockDefinition _second;

    [SetUp]
    public void SetUp()
    {
        _first = ScriptableObject.CreateInstance<DistrictBlockDefinition>();
        _first.Setup("first", "첫 땅", DistrictType.None, DistrictType.Commercial,
            new[] { new DistrictStage("first_open", new[] { new RectInt(0, 0, 10, 10) }) }, null);
        _second = ScriptableObject.CreateInstance<DistrictBlockDefinition>();
        _second.Setup("second", "둘째 땅", DistrictType.None, DistrictType.Agricultural,
            new[] { new DistrictStage("second_open", new[] { new RectInt(20, 0, 10, 10) }) }, null);
        _config = ScriptableObject.CreateInstance<SettlementConfig>();
        _config.SetupDistricts(new[] { _first, _second }, DistrictType.Residential, 60, 10, 10, 80, null);
        // 실제 설정 도구와 같은 용도별 잠금.
        var so = new SerializedObject(_config);
        var unlocks = so.FindProperty("_districtTypeUnlocks");
        unlocks.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var entry = unlocks.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("_district").enumValueIndex = (int)Districts.Assignable[i + 1];
            entry.FindPropertyRelative("_development").stringValue = "facility_" + i;
            entry.FindPropertyRelative("_lockedHint").stringValue = "시설 " + i + " 필요";
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_config);
        Object.DestroyImmediate(_first);
        Object.DestroyImmediate(_second);
    }

    private static Settlement Ready()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment(DistrictGuideRules.TownHall);
        settlement.UnlockDevelopment("receptionist_assigned");
        settlement.AssignRole(DistrictGuideRules.BoardRole, DistrictGuideRules.Receptionist, "board");
        return settlement;
    }

    [Test]
    public void Promotion_RequiresHallAndActualReceptionistRole()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment("receptionist_assigned");
        settlement.AssignRole(DistrictGuideRules.BoardRole, DistrictGuideRules.Receptionist, "board");
        Assert.IsFalse(DistrictGuideRules.CanPromote(settlement));
        settlement.UnlockDevelopment(DistrictGuideRules.TownHall);
        Assert.IsTrue(DistrictGuideRules.CanPromote(settlement));
        var wrong = new Settlement();
        wrong.UnlockDevelopment(DistrictGuideRules.TownHall);
        wrong.UnlockDevelopment("receptionist_assigned");
        wrong.AssignRole(DistrictGuideRules.BoardRole, "other_otter", "board");
        Assert.IsFalse(DistrictGuideRules.CanPromote(wrong));
    }

    [Test]
    public void Promotion_FlagsAloneDoNotReplaceRole()
    {
        var settlement = new Settlement();
        settlement.UnlockDevelopment(DistrictGuideRules.TownHall);
        settlement.UnlockDevelopment("receptionist_assigned");
        Assert.IsFalse(DistrictGuideRules.Promote(settlement));
        Assert.IsFalse(settlement.HasDevelopment(DistrictGuideRules.Promotion));
    }

    [Test]
    public void Promotion_OnceAndPersistsAfterSaveReload()
    {
        var settlement = Ready();
        int events = 0;
        settlement.OnDevelopmentUnlocked += _ => events++;
        Assert.IsTrue(DistrictGuideRules.Promote(settlement));
        Assert.IsFalse(DistrictGuideRules.Promote(settlement));
        Assert.AreEqual(1, events);
        var save = new SettlementSaveData();
        settlement.Write(save);
        var loaded = new Settlement();
        loaded.Load(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(save)));
        Assert.IsFalse(DistrictGuideRules.CanPromote(loaded));
        Assert.IsTrue(loaded.HasDevelopment(DistrictGuideRules.Promotion));
    }

    [Test]
    public void OldSave_EligibleUntilPromotionActuallyFinishes()
    {
        var save = new SettlementSaveData();
        Ready().Write(save);
        save.version = 4;
        var loaded = new Settlement();
        loaded.Load(save);
        Assert.IsTrue(DistrictGuideRules.CanPromote(loaded));
        Assert.IsFalse(loaded.HasDevelopment(DistrictGuideRules.Promotion));
        Assert.IsTrue(DistrictGuideRules.Promote(loaded));
    }

    [Test]
    public void FirstDistrict_WaitsForPromotionAndOpenLand_EndsOnlyAfterDesignation()
    {
        var settlement = Ready();
        Assert.IsFalse(DistrictGuideRules.NeedsFirstDistrict(_config, settlement));
        settlement.UnlockDevelopment("first_open");
        Assert.IsFalse(DistrictGuideRules.NeedsFirstDistrict(_config, settlement));
        DistrictGuideRules.Promote(settlement);
        Assert.IsTrue(DistrictGuideRules.NeedsFirstDistrict(_config, settlement));
        // 설명을 읽는 것은 상태를 바꾸지 않으며, 잠긴 선택도 완료되지 않는다.
        DistrictRules.Designate(_config, settlement, _first, DistrictType.Commercial);
        Assert.IsTrue(DistrictGuideRules.NeedsFirstDistrict(_config, settlement));
        Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, settlement, _first, DistrictType.Residential));
        Assert.IsFalse(DistrictGuideRules.NeedsFirstDistrict(_config, settlement));
    }

    [Test]
    public void AssignedLand_ChangeRequiresFacilityUnlock_ExplainsEveryLock()
    {
        var settlement = Ready();
        settlement.UnlockDevelopment("first_open");
        settlement.UnlockDevelopment("second_open");
        DistrictRules.Designate(_config, settlement, _first, DistrictType.Residential);
        Assert.IsEmpty(DistrictRules.Choices(_config, settlement, _first));
        foreach (var type in Districts.Assignable)
        {
            var reason = DistrictRules.CheckDesignate(_config, settlement, _first, type);
            Assert.AreNotEqual(DistrictDesignateBlock.None, reason);
            Assert.IsNotEmpty(DistrictGuideRules.LockedReason(_config, type, reason));
        }
        Assert.AreEqual("시설 0 필요", DistrictGuideRules.LockedReason(_config, DistrictType.Commercial, DistrictDesignateBlock.TypeLocked));
        settlement.UnlockDevelopment("facility_0");
        CollectionAssert.AreEqual(new[] { DistrictType.Commercial }, DistrictRules.Choices(_config, settlement, _first));
    }

    [Test]
    public void ExistingDesignations_DoNotRestartFirstTutorial()
    {
        var settlement = Ready();
        DistrictGuideRules.Promote(settlement);
        settlement.UnlockDevelopment("first_open");
        settlement.UnlockDevelopment("second_open");
        settlement.SetDistrict("first", DistrictType.Residential);
        Assert.IsFalse(DistrictGuideRules.NeedsFirstDistrict(_config, settlement));
        Assert.AreEqual(1, DistrictRules.PendingBlocks(_config, settlement).Count);
    }

    [Test]
    public void Center_PromotionStartsGuideWithoutForestExpansion()
    {
        var central = ScriptableObject.CreateInstance<DistrictBlockDefinition>();
        try
        {
            central.Setup(DistrictRules.CenterBlockId, "중앙 공터", DistrictType.Central, DistrictType.Central,
                new[] { new DistrictStage("", new[] { new RectInt(0, 0, 26, 20) }) }, null);
            _config.SetupDistricts(new[] { central, _first, _second }, DistrictType.Residential, 60, 10, 10, 80, null);
            var settlement = Ready();
            Assert.IsFalse(DistrictGuideRules.NeedsFirstDistrict(_config, settlement));
            DistrictGuideRules.Promote(settlement);
            Assert.IsTrue(DistrictGuideRules.NeedsFirstDistrict(_config, settlement), "숲은 닫혀 있어도 중앙 공터에서 안내 시작");
            Assert.AreSame(central, DistrictGuideRules.FirstDistrictBlock(_config, settlement));
            CollectionAssert.AreEqual(new[] { central }, DistrictRules.PendingBlocks(_config, settlement));
            settlement.UnlockDevelopment("facility_0"); // 시설 잠금이 풀려도 첫 중앙 지정은 주거
            Assert.AreEqual(DistrictDesignateBlock.FirstDistrict, DistrictRules.CheckDesignate(_config, settlement, central, DistrictType.Commercial));
            Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, settlement, central, DistrictType.Residential));
            Assert.IsFalse(DistrictGuideRules.NeedsFirstDistrict(_config, settlement));
            Assert.AreEqual(DistrictType.Residential, DistrictRules.TypeOf(central, settlement));
            Assert.AreEqual(1, DistrictRules.DesignatedCount(_config, settlement));
            CollectionAssert.Contains(DistrictRules.EditableBlocks(_config, settlement), central, "지정 후에도 관리 목록에 남음");
        }
        finally { Object.DestroyImmediate(central); }
    }

    [Test]
    public void Center_CanChangeAndReload_ClosedFacilityKeepsExistingType()
    {
        var central = ScriptableObject.CreateInstance<DistrictBlockDefinition>();
        try
        {
            central.Setup(DistrictRules.CenterBlockId, "중앙 공터", DistrictType.Central, DistrictType.Central,
                new[] { new DistrictStage("", new[] { new RectInt(0, 0, 26, 20) }) }, null);
            _config.SetupDistricts(new[] { central, _first }, DistrictType.Residential, 60, 10, 10, 80, null);
            var settlement = Ready();
            DistrictRules.Designate(_config, settlement, central, DistrictType.Residential);
            Assert.AreEqual(DistrictDesignateBlock.TypeLocked, DistrictRules.Designate(_config, settlement, central, DistrictType.Commercial));
            Assert.AreEqual(DistrictType.Residential, DistrictRules.TypeOf(central, settlement));
            settlement.UnlockDevelopment("facility_0");
            Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, settlement, central, DistrictType.Commercial));
            var save = new SettlementSaveData();
            settlement.Write(save);
            var loaded = new Settlement(); loaded.Load(save);
            Assert.AreEqual(DistrictType.Commercial, DistrictRules.TypeOf(central, loaded));
            Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, loaded, central, DistrictType.Residential));
            loaded.UnlockDevelopment("first_open");
            Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, loaded, _first, DistrictType.Residential), "중앙과 바깥 땅 둘 다 주거 가능");
            Assert.AreEqual(2, DistrictRules.DesignatedCount(_config, loaded));
            var house = new DistrictPlacement(DistrictType.Residential, new RectInt(1, 1, 3, 3));
            Assert.AreEqual(1, DistrictRules.Evaluate(_config, loaded, new[] { house }).Matching, "중앙 주거 안의 집에 적용");
        }
        finally { Object.DestroyImmediate(central); }
    }

    [Test]
    public void CenterGuide_StillRunsAfterLegacyOuterDistrictWasDesignated()
    {
        var central = ScriptableObject.CreateInstance<DistrictBlockDefinition>();
        try
        {
            central.Setup(DistrictRules.CenterBlockId, "중앙 공터", DistrictType.Central, DistrictType.Central,
                new[] { new DistrictStage("", new[] { new RectInt(0, 0, 26, 20) }) }, null);
            _config.SetupDistricts(new[] { central, _first }, DistrictType.Residential, 60, 10, 10, 80, null);
            var settlement = Ready(); DistrictGuideRules.Promote(settlement);
            settlement.UnlockDevelopment("first_open"); settlement.SetDistrict("first", DistrictType.Residential);
            Assert.IsTrue(DistrictGuideRules.NeedsFirstDistrict(_config, settlement));
            Assert.AreEqual(DistrictDesignateBlock.None, DistrictRules.Designate(_config, settlement, central, DistrictType.Residential));
            Assert.IsTrue(DistrictGuideRules.FirstDistrictConfigured(_config, settlement));
        }
        finally { Object.DestroyImmediate(central); }
    }

    [TestCase(0, "이사 지시서 기준까지 10")]
    [TestCase(10, "성 기준까지 70")]
    [TestCase(80, "기준을 달성")]
    public void OrderGoal_UsesConfiguredThresholds(int score, string text) =>
        StringAssert.Contains(text, DistrictGuideRules.OrderGoal(_config, score));

    [Test]
    public void GeneratedUI_HasFourChoicesAndConnectedHallCard()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/GlobalUI.prefab");
        var popup = root.GetComponentInChildren<DistrictPopupView>(true);
        Assert.IsNotNull(popup);
        Assert.IsNotNull(root.GetComponent<DistrictPresenter>());
        var so = new SerializedObject(popup);
        foreach (var property in new[] { "_choices", "_titles", "_reasons" })
        {
            var array = so.FindProperty(property);
            Assert.AreEqual(4, array.arraySize);
            for (int i = 0; i < 4; i++) Assert.IsNotNull(array.GetArrayElementAtIndex(i).objectReferenceValue);
        }
        var hall = new SerializedObject(root.GetComponentInChildren<TownHallPopupView>(true));
        foreach (var property in new[] { "_districtCard", "_orderText", "_orderGoal", "_orderBar", "_districtButton" })
            Assert.IsNotNull(hall.FindProperty(property).objectReferenceValue, property);
    }
}
