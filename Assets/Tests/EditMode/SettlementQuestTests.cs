using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SettlementQuestTests
{
    private QuestDefinition _quest;

    [TearDown]
    public void TearDown()
    {
        if (_quest != null)
            Object.DestroyImmediate(_quest);
    }

    private QuestDefinition Quest(QuestGoalType type)
    {
        _quest = ScriptableObject.CreateInstance<QuestDefinition>();
        var so = new SerializedObject(_quest);
        so.FindProperty("_questId").stringValue = "q";
        so.FindProperty("_goalType").enumValueIndex = (int)type;
        so.FindProperty("_goal").intValue = 5;
        so.ApplyModifiedPropertiesWithoutUndo();
        return _quest;
    }

    [Test]
    public void MineQuest_CountsOnlyMining()
    {
        var quest = Quest(QuestGoalType.Mine);

        Assert.AreEqual(1, QuestProgressRules.From(quest, new ItemChangedEvent(null, 0, 1, ItemChangeReason.Mining)));
        Assert.AreEqual(0, QuestProgressRules.From(quest, new ItemChangedEvent(null, 0, 2, ItemChangeReason.Gather)));
    }

    [Test]
    public void GatherQuest_CountsOnlyGathering()
    {
        var quest = Quest(QuestGoalType.Gather);

        Assert.AreEqual(2, QuestProgressRules.From(quest, new ItemChangedEvent(null, 3, 5, ItemChangeReason.Gather)));
        Assert.AreEqual(0, QuestProgressRules.From(quest, new ItemChangedEvent(null, 0, 1, ItemChangeReason.Mining)));
    }

    [Test]
    public void LockedZoneQuests_NeedTheirDevelopment()
    {
        Assert.AreEqual(SettlementQuestGate.FarmDevelopment, SettlementQuestGate.RequiredDevelopment(Quest(QuestGoalType.Harvest)));
        Object.DestroyImmediate(_quest);
        Assert.AreEqual(SettlementQuestGate.FishingDevelopment, SettlementQuestGate.RequiredDevelopment(Quest(QuestGoalType.Catch)));
        Object.DestroyImmediate(_quest);
        Assert.IsNull(SettlementQuestGate.RequiredDevelopment(Quest(QuestGoalType.Mine)), "광산은 처음부터");
    }
}
