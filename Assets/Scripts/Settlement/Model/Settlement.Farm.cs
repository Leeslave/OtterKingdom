/// <summary>광장 농장 상태 (밭 칸 · 수확 상자). 규칙은 PlazaFarm, 잇기는 SettlementManager.Farm</summary>
public partial class Settlement
{
    public PlazaFarm Farm { get; private set; } = new PlazaFarm(new PlazaFarmSaveData());

    private void LoadFarm(SettlementSaveData saved) => Farm = new PlazaFarm(saved.farm ?? new PlazaFarmSaveData());

    private void WriteFarm(SettlementSaveData result) => result.farm = Farm.Data;
}
