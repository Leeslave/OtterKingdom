using System.Collections.Generic;
using UnityEngine;

public partial class SettlementManager
{
    public const string SprinklerEquipment = "equipment_sprinkler";
    public const string StonePathEquipment = "equipment_stone_path";
    private ProductionSheet _productionSheet;

    public void OpenProduction()
    {
        if (!IsLoaded) return;
        if (_productionSheet == null) _productionSheet = gameObject.AddComponent<ProductionSheet>();
        _productionSheet.Open(this);
    }

    public string EquipmentHint(bool stonePath)
    {
        if (HasDevelopment(stonePath ? StonePathEquipment : SprinklerEquipment)) return "설비 확장 완료";
        if (!HasBuilt(stonePath ? "bld_quarry" : "bld_workshop")) return stonePath ? "채석 작업소가 필요해요" : "목공소가 필요해요";
        if (!HasDevelopment("farm_working")) return "먼저 밭 농사를 시작해요";
        if (PlayerLevel < 13) return "왕국 Lv.13에 농장 설비를 확장할 수 있어요";
        if (stonePath && !HasDevelopment(FarmPathPlacedDevelopment)) return "농업 창고를 짓고 흙길을 한 번 놓아 봐요";
        return null;
    }

    public bool TryUpgradeEquipment(bool stonePath)
    {
        if (!IsLoaded || EquipmentHint(stonePath) != null) return false;
        int gold = stonePath ? 700 : 500;
        var wood = FindBuilding("bld_workshop")?.ProductionItem;
        var stone = FindBuilding("bld_quarry")?.ProductionItem;
        if (wood == null || stone == null) return false;
        var items = new List<ItemAmount> { new ItemAmount(wood, stonePath ? 10 : 15), new ItemAmount(stone, stonePath ? 15 : 5) };
        if (!HasEnoughGold(gold) || !HasEnoughItems(items)) return false;
        PayCost(gold, items);
        Settlement.UnlockDevelopment(stonePath ? StonePathEquipment : SprinklerEquipment);
        // 이미 놓인 흙길도 확장 직후 돌길 이용 기록에 반영한다.
        if (stonePath) RecordFarmPartUse();
        OnChanged?.Invoke();
        SaveRequested?.Invoke();
        return true;
    }

    private void MigrateEquipment(bool existingSave)
    {
        if (!existingSave || ScoutingRules.UsesScouting(Settlement)) return;
        // 개편 전에 이미 사용 가능했던 기능은 다시 잠그지 않는다.
        if (HasBuilt("bld_workshop")) Settlement.UnlockDevelopment(SprinklerEquipment);
        if (HasBuilt("bld_quarry")) Settlement.UnlockDevelopment(StonePathEquipment);
    }
}
