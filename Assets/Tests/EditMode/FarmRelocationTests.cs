using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// 밭 해금을 농업 지역으로 (Docs/밭해금_재배치_기획.md): 실제 데이터의 메인 퀘스트 체인이 한 줄로 이어지고 레벨이 줄지 않으며,
/// 밭이 열리는 Lv.10 전에는 밭에서만 할 수 있는 목표가 없고, 밭 부탁 · 장소가 농업 지역 발견 뒤에 열린다.
/// </summary>
public class FarmRelocationTests
{
    private const string DatabasePath = "Assets/Scriptable Obejects/Quest/QuestDatabase.asset";
    private const string RequestFolder = "Assets/Scriptable Obejects/Settlement/Requests";

    private static List<QuestDefinition> MainChain()
    {
        var database = AssetDatabase.LoadAssetAtPath<QuestDatabase>(DatabasePath);
        Assert.IsNotNull(database, DatabasePath);
        var mains = database.Quests.Where(q => q != null && q.Kind == QuestKind.Main).ToList();
        var next = new Dictionary<QuestDefinition, QuestDefinition>();
        QuestDefinition first = null;
        foreach (var quest in mains)
        {
            if (quest.Prerequisite == null)
            {
                Assert.IsNull(first, $"메인 체인의 시작이 둘: {first?.QuestId}, {quest.QuestId}");
                first = quest;
                continue;
            }
            Assert.IsFalse(next.ContainsKey(quest.Prerequisite), $"갈림길: {quest.Prerequisite.QuestId} 뒤에 둘");
            next[quest.Prerequisite] = quest;
        }
        var chain = new List<QuestDefinition>();
        for (var q = first; q != null; q = next.TryGetValue(q, out var n) ? n : null)
            chain.Add(q);
        Assert.AreEqual(mains.Count, chain.Count, "체인에서 떨어진 메인 퀘스트가 있음");
        return chain;
    }

    [Test]
    public void MainChain_IsSingleLine_WithNonDecreasingLevels()
    {
        var chain = MainChain();
        for (int i = 1; i < chain.Count; i++)
            Assert.GreaterOrEqual(chain[i].RequiredLevel, chain[i - 1].RequiredLevel, $"{chain[i - 1].QuestId} → {chain[i].QuestId}");
    }

    [Test]
    public void NoFarmOnlyGoals_BeforeLevel10()
    {
        foreach (var quest in MainChain().Where(q => q.RequiredLevel < 10))
        {
            Assert.AreNotEqual(QuestGoalType.Harvest, quest.GoalType, quest.QuestId);
            Assert.AreNotEqual(QuestGoalType.UnlockFurrow, quest.GoalType, quest.QuestId);
            Assert.AreNotEqual("req_farmland", quest.Target, quest.QuestId);
            Assert.AreNotEqual("req_assign_farmer", quest.Target, quest.QuestId);
        }
    }

    [Test]
    public void FarmRequest_AndZone_WaitForAgriculturalDistrict()
    {
        var farmland = AssetDatabase.LoadAssetAtPath<BoardRequestDefinition>($"{RequestFolder}/req_farmland.asset");
        Assert.AreEqual(SettlementManager.FarmDiscoveredDevelopment, farmland.RequiredDevelopment);
        var board = AssetDatabase.LoadAssetAtPath<BoardRequestDefinition>($"{RequestFolder}/req_upgrade_board.asset");
        Assert.AreEqual(SettlementQuestGate.FairyDevelopment, board.RequiredDevelopment, "게시판 보강은 밭이 아니라 요정 뒤");
        var zone = AssetDatabase.LoadAssetAtPath<ZoneDefinition>("Assets/Scriptable Obejects/Navigation/Zone_Farm.asset");
        Assert.AreEqual(SettlementManager.FarmDiscoveredDevelopment, new SerializedObject(zone).FindProperty("_requiredDevelopment").stringValue);
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>("Assets/Scriptable Obejects/Settlement/SettlementConfig.asset");
        Assert.IsNull(config.FindDistrictTypeUnlock(DistrictType.Agricultural), "농업 지역은 처음부터 고를 수 있어야 밭이 열림");
        Assert.AreEqual(10, config.FarmDiscoverLevel, "농업 지역을 일찍 정해도 밭 발견은 Lv.10");
        Assert.Greater(config.CastleLevel, 12, "성 → 길드 지부(상업 지역)는 짤랑이 가게(Lv.12) 뒤");
    }

    [Test]
    public void NoSeedOnSale_BeforeFarm()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:ShopProduct", new[] { "Assets/Scriptable Obejects/Shop/Products" }))
        {
            var product = AssetDatabase.LoadAssetAtPath<ShopProduct>(AssetDatabase.GUIDToAssetPath(guid));
            if (product != null && product.name.StartsWith("Seed_"))
                Assert.GreaterOrEqual(product.RequiredLevel, 10, product.name);
        }
    }

    [Test]
    public void UpgradeGoals_BeforeFarm_CountPickaxeLevel()
    {
        // 밭 전에는 곡괭이 강화(4번이 끝)뿐이라, 열린 동안의 강화만 세면 미리 다 올린 플레이어의 체인이 막힘
        foreach (var quest in MainChain().Where(q => q.RequiredLevel < 10 && q.GoalType == QuestGoalType.Upgrade))
            Assert.AreEqual(QuestProgressRules.PickaxeTarget, quest.Target, quest.QuestId);
    }
}
