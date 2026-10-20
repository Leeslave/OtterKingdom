using NUnit.Framework;
using UnityEditor;

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
    public void FirstOtter_IsNamedHaedari()
    {
        var first = AssetDatabase.LoadAssetAtPath<SettlementOtterDefinition>(
            "Assets/Scriptable Obejects/Settlement/Otters/Otter_First.asset");
        Assert.AreEqual("해달이", first.DisplayName);
    }
}
