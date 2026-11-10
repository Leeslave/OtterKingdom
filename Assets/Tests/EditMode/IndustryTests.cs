using System.Collections.Generic;
using NUnit.Framework;

/// <summary>번영의 시대 산업 · 물류 규칙 (KingdomWarehouse · ProcessingRules · CartRules)</summary>
public class IndustryTests
{
    // 목공소: 목재 4 → 판재 1, 6분
    private static readonly ProcessingRecipe Plank = new ProcessingRecipe("bld_workshop", "wood", 4, "plank", 360f);

    private static KingdomWarehouse Warehouse(IndustrySaveData data, int capacity = 200) => new KingdomWarehouse(data, () => capacity);

    [Test]
    public void Warehouse_AddsUpToCapacity_AndNeverLosesTheRest()
    {
        var data = new IndustrySaveData();
        var warehouse = Warehouse(data, 10);
        Assert.AreEqual(7, warehouse.Add("plank", 7, "test"));
        Assert.AreEqual(3, warehouse.Add("stone_block", 5, "test"), "칸만큼만");
        Assert.AreEqual(10, warehouse.Total);
        Assert.AreEqual(0, warehouse.Free);
        Assert.AreEqual(0, warehouse.Add("plank", 1, "test"));
    }

    [Test]
    public void Warehouse_ConsumeIsAllOrNothing_AndReservedIsProtected()
    {
        var warehouse = Warehouse(new IndustrySaveData());
        warehouse.Add("plank", 10, "test");
        warehouse.Add("stone_block", 2, "test");
        var changes = new List<(string, int)>();
        warehouse.Changed += (id, delta, reason) => changes.Add((id, delta));

        Assert.IsFalse(warehouse.TryConsume(new[] { ("plank", 4), ("stone_block", 3) }, "build"), "하나라도 모자라면");
        Assert.AreEqual(10, warehouse.Count("plank"), "아무것도 안 씀");

        Assert.IsTrue(warehouse.Reserve("plank", 8));
        Assert.AreEqual(2, warehouse.Available("plank"));
        Assert.IsFalse(warehouse.TryConsume(new[] { ("plank", 3) }, "contract"), "예약한 몫은 다른 데서 못 씀");
        Assert.IsTrue(warehouse.ConsumeReserved("plank", 8, "build"));
        Assert.AreEqual(2, warehouse.Count("plank"));
        Assert.AreEqual(0, warehouse.Reserved("plank"));
        CollectionAssert.AreEqual(new[] { ("plank", -8) }, changes);
    }

    [Test]
    public void Processing_FirstOnGivesOne_ThenMakesFromInput_OverTime()
    {
        var processor = new ProcessorSaveData { buildingId = "bld_workshop" };
        Assert.IsTrue(ProcessingRules.SetOn(processor, Plank, true), "처음 켜면 체험");
        Assert.AreEqual(1, processor.tray);
        Assert.IsFalse(ProcessingRules.SetOn(processor, Plank, true), "체험은 한 번만");

        Assert.AreEqual(10, ProcessingRules.Feed(processor, Plank, 10));
        Assert.AreEqual(0, ProcessingRules.Run(processor, Plank, 359f));
        Assert.AreEqual(1, ProcessingRules.Run(processor, Plank, 1f), "6분에 하나");
        Assert.AreEqual(6, processor.input);
        Assert.AreEqual(1, ProcessingRules.Run(processor, Plank, 3600f), "재료(6개)가 하나 몫만 남아 하나만");
        Assert.AreEqual(2, processor.input, "남은 재료는 그대로");
        Assert.AreEqual(3, processor.tray);

        // 재료를 기다린 동안 진행은 한 개 몫까지만 → 재료가 오면 바로 하나
        ProcessingRules.Feed(processor, Plank, 2);
        Assert.AreEqual(1, ProcessingRules.Run(processor, Plank, 0.1f));
        Assert.AreEqual(0, ProcessingRules.Run(processor, Plank, 300f), "그 뒤는 다시 6분");
    }

    [Test]
    public void Processing_StopsWhenOff_AndInputHasACap()
    {
        var processor = new ProcessorSaveData { buildingId = "bld_workshop" };
        Assert.AreEqual(Plank.InputCapacity, ProcessingRules.Feed(processor, Plank, 999), "넣을 자리만큼");
        Assert.AreEqual(0, ProcessingRules.Feed(processor, Plank, 5));
        Assert.AreEqual(0, ProcessingRules.Run(processor, Plank, 3600f), "꺼져 있으면 안 만듦");
        processor.on = true;
        processor.trialGiven = true;
        Assert.AreEqual(10, ProcessingRules.Run(processor, Plank, 3600f), "1시간에 최대 10개");
    }

    [Test]
    public void Processing_FullTrayWaits_AndCollectMovesWhatFits()
    {
        var data = new IndustrySaveData();
        var processor = ProcessingRules.Processor(data, "bld_workshop");
        processor.on = true;
        processor.trialGiven = true;
        processor.tray = Plank.TrayCapacity - 1;
        ProcessingRules.Feed(processor, Plank, 40);
        Assert.AreEqual(1, ProcessingRules.Run(processor, Plank, 3600f), "시설이 차면 멈춤");
        Assert.AreEqual(36, processor.input, "멈춘 동안 재료는 안 빠짐");

        var warehouse = Warehouse(data, 50);
        Assert.AreEqual(50, ProcessingRules.Collect(processor, Plank, warehouse, "collect"));
        Assert.AreEqual(Plank.TrayCapacity - 50, processor.tray, "못 옮긴 건 시설에 남음");
    }

    [Test]
    public void Cart_NeedsTech_AndDeliversOnlyItsRoutes()
    {
        Assert.AreEqual(0, CartRules.RouteLimit(_ => false));
        Assert.AreEqual(1, CartRules.RouteLimit(id => id == TechIds.Cart));
        Assert.AreEqual(3, CartRules.RouteLimit(id => id == TechIds.Cart || id == TechIds.Dispatch));

        var data = new IndustrySaveData();
        var stone = new ProcessingRecipe("bld_quarry", "stone", 6, "stone_block", 360f);
        ProcessingRules.Processor(data, "bld_workshop").tray = 5;
        ProcessingRules.Processor(data, "bld_quarry").tray = 4;
        Assert.IsTrue(CartRules.SetRoute(data, "bld_workshop", true, 1));
        Assert.IsFalse(CartRules.SetRoute(data, "bld_quarry", true, 1), "수레 1대");

        var warehouse = Warehouse(data);
        var moved = CartRules.Deliver(data, new[] { Plank, stone }, warehouse, 1);
        CollectionAssert.AreEqual(new[] { ("bld_workshop", 5) }, moved);
        Assert.AreEqual(5, warehouse.Count("plank"));
        Assert.AreEqual(4, ProcessingRules.Processor(data, "bld_quarry").tray, "수레가 안 가는 곳은 그대로");
    }

    [Test]
    public void Speed_StandardToolsDoubles_WaterPowerAdds15Percent()
    {
        Assert.AreEqual(1f, ProcessingRules.Speed(_ => false), 0.001f);
        Assert.AreEqual(2f, ProcessingRules.Speed(id => id == TechIds.StandardTools), 0.001f, "슬롯 +1");
        Assert.AreEqual(1.15f, ProcessingRules.Speed(id => id == TechIds.WaterPower), 0.001f);
        Assert.AreEqual(2.3f, ProcessingRules.Speed(id => id == TechIds.StandardTools || id == TechIds.WaterPower), 0.001f);
    }

    [Test]
    public void Processing_Speed2_MakesTwiceAsFast_StillLimitedByInput()
    {
        var processor = new ProcessorSaveData { buildingId = "bld_workshop", on = true, trialGiven = true };
        ProcessingRules.Feed(processor, Plank, 40);
        Assert.AreEqual(2, ProcessingRules.Run(processor, Plank, 360f, 2f), "6분에 둘");
        Assert.AreEqual(8, ProcessingRules.Run(processor, Plank, 3600f, 2f), "재료(32개 = 8개 몫)까지만");
        Assert.AreEqual(20f, ProcessingRules.ActualPerHour(Plank, 999, 2f), 0.001f);
    }

    [Test]
    public void AutoSupply_FillsRoom_KeepsSomeInBag()
    {
        var processor = new ProcessorSaveData { buildingId = "bld_workshop", input = 30 };
        Assert.AreEqual(10, ProcessingRules.AutoSupplyAmount(processor, Plank, 25, 10), "넣을 자리 10개");
        Assert.AreEqual(2, ProcessingRules.AutoSupplyAmount(processor, Plank, 12, 10), "가방에 10개는 남김");
        Assert.AreEqual(0, ProcessingRules.AutoSupplyAmount(processor, Plank, 5, 10));
        Assert.AreEqual(ProcessingRules.DefaultKeep, new ProcessorSaveData().keep, "옛 세이브 · 새 시설은 기본으로 남김");
    }

    [Test]
    public void TownHouse_NeedsImprovedFirst_AndOnlyOnce()
    {
        var data = new IndustrySaveData();
        Assert.IsFalse(TownRules.MakeTownHouse(data, 12), "개량 주택이 먼저");
        TownRules.Improve(data, 12);
        Assert.IsTrue(TownRules.MakeTownHouse(data, 12));
        Assert.IsFalse(TownRules.MakeTownHouse(data, 12), "한 번만");
        Assert.IsTrue(TownRules.IsTownHouse(data, 12));
        Assert.IsTrue(TownRules.IsImproved(data, 12), "1단계 기록은 그대로");
    }

    [Test]
    public void WarehouseRecipe_TakesBothMaterialsPerItem_WaitsWhenShort()
    {
        // 정밀 공방: 판재 2 + 주괴 2 → 정밀 부품 1, 12분 (창고에서 바로)
        var precision = new ProcessingRecipe("bld_workshop_precision", "plank", 2, "precision_part", 720f, "ingot", 2, true);
        var data = new IndustrySaveData();
        var warehouse = Warehouse(data);
        warehouse.Add("plank", 5, "test");
        warehouse.Add("ingot", 9, "test");
        var processor = ProcessingRules.Processor(data, precision.ProcessorId);
        processor.on = true;
        processor.trialGiven = true;
        Assert.AreEqual(2, ProcessingRules.RunFromWarehouse(processor, precision, 3600f, 1f, warehouse), "판재가 두 개 몫뿐");
        Assert.AreEqual(1, warehouse.Count("plank"));
        Assert.AreEqual(5, warehouse.Count("ingot"));
        Assert.AreEqual(2, processor.tray);
        Assert.AreEqual(720f, processor.progress, 0.01f, "기다리는 동안 한 개 몫까지만");
        warehouse.Add("plank", 1, "test");
        Assert.AreEqual(1, ProcessingRules.RunFromWarehouse(processor, precision, 0.1f, 1f, warehouse), "재료가 오면 바로 하나");
        Assert.AreEqual(ProcessingRules.Processor(data, "bld_workshop"), ProcessingRules.Processor(data, "bld_workshop"));
        Assert.AreNotSame(processor, ProcessingRules.Processor(data, "bld_workshop"), "같은 시설의 다른 줄은 따로");
    }

    [Test]
    public void Cooperation_FivePercentPerExtraLine_UpTo20()
    {
        Assert.AreEqual(0f, ProcessingRules.CooperationBonus(0), 0.001f);
        Assert.AreEqual(0f, ProcessingRules.CooperationBonus(1), 0.001f, "한 줄은 협업 없음");
        Assert.AreEqual(0.1f, ProcessingRules.CooperationBonus(3), 0.001f);
        Assert.AreEqual(0.2f, ProcessingRules.CooperationBonus(9), 0.001f, "최대 +20%");
    }

    [Test]
    public void BetterCart_ServesFiveLines()
    {
        Assert.AreEqual(5, CartRules.RouteLimit(id => id == TechIds.Cart || id == TechIds.Dispatch || id == TechIds.BetterCart));
    }

    [Test]
    public void ActualPerHour_IsLimitedByIncomingWood()
    {
        Assert.AreEqual(7.5f, ProcessingRules.ActualPerHour(Plank, 30), 0.001f, "해달 1명(목재 30/h) → 판재 7.5/h");
        Assert.AreEqual(10f, ProcessingRules.ActualPerHour(Plank, 60), 0.001f, "레시피 최대 10/h");
    }

    [Test]
    public void Save_RoundTripsThroughSettlement()
    {
        var settlement = new Settlement();
        var saved = new SettlementSaveData();
        saved.industry.warehouse.Add(new ItemCountSaveData { itemId = "plank", amount = 3 });
        saved.industry.cartRoutes.Add("bld_workshop");
        saved.industry.improvedHouses.Add(12);
        settlement.Load(saved);
        var written = new SettlementSaveData();
        settlement.Write(written);
        Assert.AreEqual(3, written.industry.warehouse[0].amount);
        CollectionAssert.AreEqual(new[] { "bld_workshop" }, written.industry.cartRoutes);
        CollectionAssert.AreEqual(new[] { 12 }, written.industry.improvedHouses);
    }

    [Test]
    public void Save_OldIndustryWithoutImprovedHouses_LoadsEmpty()
    {
        var settlement = new Settlement();
        var saved = new SettlementSaveData();
        saved.industry.improvedHouses = null;
        saved.industry.townHouses = null;
        settlement.Load(saved);
        Assert.IsNotNull(settlement.Industry.improvedHouses);
        Assert.IsNotNull(settlement.Industry.townHouses);
        Assert.IsFalse(TownRules.IsImproved(settlement.Industry, 12));
    }

    [Test]
    public void House_ImprovesOnce_AndKeepsTheSameInstance()
    {
        var data = new IndustrySaveData();
        Assert.IsTrue(TownRules.Improve(data, 12));
        Assert.IsFalse(TownRules.Improve(data, 12), "한 번만");
        Assert.IsTrue(TownRules.IsImproved(data, 12));
        Assert.IsFalse(TownRules.IsImproved(data, 13));
    }

    [Test]
    public void Craft_CostsSkipZero_AndConsumeAllOrNothing()
    {
        var costs = TownRules.Costs("plank", 2, "stone_block", 0);
        CollectionAssert.AreEqual(new[] { ("plank", 2) }, costs);
        var warehouse = Warehouse(new IndustrySaveData());
        warehouse.Add("plank", 1, "test");
        Assert.IsFalse(warehouse.TryConsume(TownRules.Costs("plank", 1, "stone_block", 1), "craft"), "석재 블록이 없으면 판재도 안 씀");
        Assert.AreEqual(1, warehouse.Count("plank"));
    }

    [Test]
    public void PileSprite_TiersByTrayFill()
    {
        var recipe = UnityEngine.ScriptableObject.CreateInstance<ProcessingRecipeDefinition>();
        Assert.IsNull(recipe.PileSprite(5, 80), "그림이 없으면 null");
        UnityEngine.Object.DestroyImmediate(recipe);
    }
}
