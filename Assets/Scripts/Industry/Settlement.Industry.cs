/// <summary>번영의 시대 산업 · 물류 세이브 (규칙은 KingdomWarehouse · ProcessingRules · CartRules, 잇기는 SettlementManager.Industry)</summary>
public partial class Settlement
{
    public IndustrySaveData Industry { get; private set; } = new IndustrySaveData();

    private void LoadIndustry(SettlementSaveData saved)
    {
        Industry = saved.industry ?? new IndustrySaveData();
        Industry.warehouse ??= new System.Collections.Generic.List<ItemCountSaveData>();
        Industry.reserved ??= new System.Collections.Generic.List<ItemCountSaveData>();
        Industry.processors ??= new System.Collections.Generic.List<ProcessorSaveData>();
        Industry.cartRoutes ??= new System.Collections.Generic.List<string>();
        Industry.improvedHouses ??= new System.Collections.Generic.List<int>();
        Industry.townHouses ??= new System.Collections.Generic.List<int>();
    }

    private void WriteIndustry(SettlementSaveData result) => result.industry = Industry;
}
