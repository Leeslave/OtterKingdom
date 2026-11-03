using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class QASavePlacementTests
{
    [Test]
    public void QA25_PhotoStudioDoesNotConsumeAllCommercialLandForGuildBranch()
    {
        var save = JsonUtility.FromJson<SaveData>(File.ReadAllText("Docs/QA_saves/web25_lv20_photo_studio_save.json"));
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>("Assets/Scriptable Obejects/Settlement/SettlementConfig.asset");
        var board = AssetDatabase.LoadAssetAtPath<DecorBoardDefinition>("Assets/Scriptable Obejects/Decor/Boards/Board_plaza.asset");
        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>("Assets/Scriptable Obejects/Decor/DecorCatalog.asset");
        var guild = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/Scriptable Obejects/Decor/Buildings/Building_GuildBranch.asset");
        var settlement = new Settlement();
        settlement.Load(save.settlement);
        var layout = DecorManager.CreateLayout(board);
        var savedBoard = save.decor.boards.Find(b => b.boardId == board.BoardId);
        Assert.IsNotNull(savedBoard);
        DecorSaveConverter.Read(savedBoard, layout, catalog, board.SaveOrigin);
        var occupied = new List<RectInt>();
        int before = 0;
        foreach (var placed in layout.Placed)
        {
            occupied.Add(new RectInt(placed.Area.position - board.SaveOrigin, placed.Area.size));
            before++;
        }
        var slots = RelocationRules.FreeSlots(config, settlement, DistrictType.Commercial, guild.Footprint, occupied);
        Assert.IsTrue(slots.Exists(slot => layout.Check(guild,
            DistrictRules.OriginIn(slot.Slot, guild.Footprint) + board.SaveOrigin, DecorRotation.R0) == DecorPlacementResult.Ok),
            "사진관을 옮기거나 기존 땅을 다시 지정하지 않고 열린 상업 구역에 길드 지부가 들어갈 후보가 있어야 함");
        int after = 0;
        foreach (var placed in layout.Placed) after++;
        Assert.AreEqual(before, after, "빈 자리 조회는 기존 건물을 이동·삭제하지 않아야 함");
    }

    [Test]
    public void BagCapacityBalance_PreservesPurchasedSlotsAndFitsMoreItemTypes()
    {
        var config = AssetDatabase.LoadAssetAtPath<InventoryConfig>("Assets/Scriptable Obejects/Inventory/InventoryConfig.asset");
        Assert.AreEqual(24, config.InitialCapacity);
        Assert.AreEqual(8, config.ExpandStep);
        Assert.GreaterOrEqual(config.MaxCapacity, 56);
        Assert.GreaterOrEqual(config.MaxCapacity, 40, "기존 최대 용량 구매자를 줄이면 안 됨");
    }
}
