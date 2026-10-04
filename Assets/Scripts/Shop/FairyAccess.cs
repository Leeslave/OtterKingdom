/// <summary>
/// 요정 상점을 지금 열 수 있는지 (요정이 광장에 찾아왔는지). 요정은 농부를 밭에 파견한 뒤 찾아온다 (SettlementManager.IsFairyArrived).
/// 요정 NPC 말고 다른 입구(꾸미기 보관함 [+ 상점], 밭 알림 [상점으로])도 이것을 보고, 요정이 오기 전에는 상점을 먼저 열지 않는다.
/// 정착 매니저가 없는 테스트 씬은 늘 열림.
/// </summary>
public static class FairyAccess
{
    public static bool IsShopOpen
    {
        get
        {
            var manager = SettlementManager.Instance;
            return manager == null || manager.IsLoaded && manager.IsFairyArrived;
        }
    }

    /// <summary>열 수 있으면 요정 상점을 연다 (이미 열려 있으면 그대로)</summary>
    public static void OpenShop()
    {
        var shop = FairyShopPresenter.Instance;
        if (IsShopOpen && shop != null && !shop.IsOpen)
            shop.Open();
    }
}
