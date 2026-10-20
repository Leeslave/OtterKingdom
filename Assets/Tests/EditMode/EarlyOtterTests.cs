using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EarlyOtterTests
{
    [Test]
    public void BeforeBoardUpgrade_OnlyFirstFourOttersAppear()
    {
        var settlement = new Settlement();
        foreach (var id in new[] { "otter_first", "otter_miner", "otter_painter", "otter_builder" })
            Assert.IsTrue(EarlyOtterRules.CanAppear(id, settlement), id);
        foreach (var id in new[] { "otter_sleepy", "otter_receptionist", "otter_farmer", "otter_toy_jjalrang" })
            Assert.IsFalse(EarlyOtterRules.CanAppear(id, settlement), id);

        settlement.UnlockDevelopment(EarlyOtterRules.OpenDevelopment);
        Assert.IsTrue(EarlyOtterRules.CanAppear("otter_sleepy", settlement));
        Assert.IsTrue(EarlyOtterRules.CanAppear("otter_receptionist", settlement));
    }

    [Test]
    public void PainterAndBuilder_SkipTheVisitorQueue()
    {
        Assert.IsTrue(EarlyOtterRules.IsImmediate("otter_painter"));
        Assert.IsTrue(EarlyOtterRules.IsImmediate("otter_builder"));
        Assert.IsFalse(EarlyOtterRules.IsImmediate("otter_first"));
        Assert.IsFalse(EarlyOtterRules.IsImmediate("otter_sleepy"));
    }

    [Test]
    public void AssigningMiner_BringsPainterAndBuilder()
    {
        var request = AssetDatabase.LoadAssetAtPath<BoardRequestDefinition>(
            "Assets/Scriptable Obejects/Settlement/Requests/req_assign_miner.asset");
        Assert.IsNotNull(request);
        var ids = new System.Collections.Generic.List<string>();
        foreach (var arrival in request.Arrivals)
            ids.Add(arrival.Otter.OtterId);
        CollectionAssert.AreEquivalent(new[] { "otter_painter", "otter_builder" }, ids);
    }

    [Test]
    public void ProfilePortrait_OffersFirstOtterAndSettledResidentsOnly()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(
            "Assets/Scriptable Obejects/Settlement/SettlementConfig.asset");
        var settlement = new Settlement();
        settlement.SetResident("otter_painter", ResidentState.Resident);
        settlement.SetResident("otter_builder", ResidentState.SpecialNpc);
        settlement.SetResident("otter_sleepy", ResidentState.Visitor);

        var ids = new System.Collections.Generic.List<string>();
        foreach (var otter in ProfilePortraitRules.Choices(config, settlement))
            ids.Add(otter.OtterId);
        CollectionAssert.AreEqual(new[] { "otter_first", "otter_painter" }, ids);

        Assert.AreEqual("otter_first", ProfilePortraitRules.Resolve(config, null).OtterId);
        Assert.AreEqual("otter_first", ProfilePortraitRules.Resolve(config, "nobody").OtterId);
        Assert.AreEqual("otter_painter", ProfilePortraitRules.Resolve(config, "otter_painter").OtterId);
    }

    [Test]
    public void GuildOrder_TownHallUnlocksEarlyThings_GuildWaitsForHospital()
    {
        const string folder = "Assets/Scriptable Obejects/";
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(folder + "Settlement/SettlementConfig.asset");
        Assert.IsNull(config.FindRequest(SettlementMigration.RetiredGuildOfficeRequestId), "접수 창구는 메인에서 빠짐");
        Assert.AreEqual("mine_hospital", config.FindRequest("req_upgrade_town_hall").RequiredDevelopment);
        Assert.AreEqual("board_upgraded", config.LifeRequestDevelopment);
        Assert.AreEqual("board_upgraded", AssetDatabase.LoadAssetAtPath<BuildingDefinition>(folder + "Decor/Buildings/Building_SmallHouse.asset").RequiredDevelopment);
        Assert.AreEqual("maze_first_rock", AssetDatabase.LoadAssetAtPath<QuestDefinition>(folder + "Quest/Quests/M06_Guild.asset").Target);
        Assert.AreEqual("maze_first_trip", AssetDatabase.LoadAssetAtPath<QuestDefinition>(folder + "Quest/Quests/M07_TownHall.asset").Target);
    }

    [Test]
    public void RetiredGuildOfficeJob_FinishesQuietly()
    {
        var settlement = new Settlement();
        settlement.StartJob(SettlementMigration.RetiredGuildOfficeRequestId, "con_guild_office", 0, 1);
        Assert.IsTrue(SettlementMigration.RetireGuildOfficeJob(settlement));
        Assert.IsNull(settlement.Job);
        Assert.IsTrue(settlement.HasDevelopment(SettlementMigration.RetiredGuildOfficeDevelopment));
        Assert.IsFalse(SettlementMigration.RetireGuildOfficeJob(settlement));
    }

    [Test]
    public void FirstField_IsFreeAndInstant_OnlyOnce()
    {
        var field = ScriptableObject.CreateInstance<BuildingDefinition>();
        var so = new SerializedObject(field);
        so.FindProperty("_buildingId").stringValue = "bld_test_field";
        so.FindProperty("_farmPart").enumValueIndex = (int)FarmPartKind.Field;
        so.FindProperty("_gold").intValue = 500;
        so.ApplyModifiedPropertiesWithoutUndo();
        var settlement = new Settlement();
        Assert.IsTrue(BuildingRules.IsFreeFirstField(settlement, field));
        Assert.AreEqual(0, BuildingRules.GoldCost(settlement, field));
        Assert.IsEmpty(BuildingRules.ItemCosts(settlement, field));

        BuildingRules.Start(settlement, field, 1, 0, 0);
        Assert.IsFalse(BuildingRules.IsFreeFirstField(settlement, field));
        Assert.AreEqual(500, BuildingRules.GoldCost(settlement, field));
        Object.DestroyImmediate(field);
    }

    [Test]
    public void FirstOtter_IsNamedHaedari()
    {
        var first = AssetDatabase.LoadAssetAtPath<SettlementOtterDefinition>(
            "Assets/Scriptable Obejects/Settlement/Otters/Otter_First.asset");
        Assert.AreEqual("해달이", first.DisplayName);
    }
}
