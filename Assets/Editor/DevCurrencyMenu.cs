using UnityEditor;
using UnityEngine;

/// <summary>
/// OtterKingdom/Dev/Currency: Play 중에 재화를 넉넉히 준다 (테스트용 치트). 개발자 지급(TestGet)이라 분석 로그·퀘스트에 세지 않는다.
/// 에디터 메뉴라 빌드에는 들어가지 않는다.
/// </summary>
public static class DevCurrencyMenu
{
    private const string Root = "OtterKingdom/Dev/Currency/";

    // (재화 에셋 경로, 한 번에 주는 양)
    private static readonly (string path, int amount)[] Grants =
    {
        ("Assets/Scriptable Obejects/Gold.asset", 100000),
        ("Assets/Scriptable Obejects/Gem.asset", 10000),
        ("Assets/Scriptable Obejects/Gacha/Currencies/TicketPickup.asset", 10),
        ("Assets/Scriptable Obejects/Gacha/Currencies/TicketStandard.asset", 10),
        ("Assets/Scriptable Obejects/Gacha/Currencies/Shard.asset", 1000),
    };

    [MenuItem(Root + "Give All (골드 10만 · 조개 1만 · 뽑기권 10 · 조각 1000)")]
    private static void GiveAll()
    {
        foreach (var (path, amount) in Grants)
            Give(path, amount);
    }

    [MenuItem(Root + "Gold +100,000")]
    private static void GiveGold() => Give(Grants[0].path, Grants[0].amount);

    [MenuItem(Root + "Gem +10,000")]
    private static void GiveGem() => Give(Grants[1].path, Grants[1].amount);

    [MenuItem(Root + "Gacha Tickets +10")]
    private static void GiveTickets()
    {
        Give(Grants[2].path, Grants[2].amount);
        Give(Grants[3].path, Grants[3].amount);
    }

    [MenuItem(Root + "Shard +1,000")]
    private static void GiveShard() => Give(Grants[4].path, Grants[4].amount);

    [MenuItem(Root + "Give All (골드 10만 · 조개 1만 · 뽑기권 10 · 조각 1000)", true)]
    [MenuItem(Root + "Gold +100,000", true)]
    [MenuItem(Root + "Gem +10,000", true)]
    [MenuItem(Root + "Gacha Tickets +10", true)]
    [MenuItem(Root + "Shard +1,000", true)]
    private static bool CanGive() => EditorApplication.isPlaying && CurrencyManager.Instance != null;

    private static void Give(string path, int amount)
    {
        var currency = AssetDatabase.LoadAssetAtPath<Currency>(path);
        if (currency == null)
        {
            Debug.LogWarning($"[Dev] 재화가 없습니다: {path}");
            return;
        }
        CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(currency, amount, TransactionSource.TestGet));
        Debug.Log($"[Dev] {currency.DisplayName} +{amount:N0} (지금 {CurrencyManager.Instance.GetCurrency(currency):N0})");
    }
}
