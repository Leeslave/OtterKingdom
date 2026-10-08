using UnityEditor;
using UnityEngine;

/// <summary>
/// OtterKingdom/Cheat: Play 중에 조개 · 금화(골드) 등 재화를 주거나 0으로 만든다 (뽑기 디버그용 치트).
/// 개발자 지급(TestGet · TestUse)이라 분석 로그 · 퀘스트에 세지 않는다. 에디터 메뉴라 빌드에는 들어가지 않는다.
/// Play 중이 아니면 안내 창만 띄운다 (재화는 세이브를 불러온 뒤의 매니저가 가지고 있음).
/// </summary>
public static class DevCurrencyMenu
{
    private const string Root = "OtterKingdom/Cheat/";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string GemPath = "Assets/Scriptable Obejects/Gem.asset";
    private const string TicketPickupPath = "Assets/Scriptable Obejects/Gacha/Currencies/TicketPickup.asset";
    private const string TicketStandardPath = "Assets/Scriptable Obejects/Gacha/Currencies/TicketStandard.asset";
    private const string ShardPath = "Assets/Scriptable Obejects/Gacha/Currencies/Shard.asset";

    // 조개 (뽑기 1회 30 · 10회 300)
    [MenuItem(Root + "조개 +300 (10회 뽑기 1번)", false, 0)]
    private static void Gem300() => Give(GemPath, 300);

    [MenuItem(Root + "조개 +3,000", false, 1)]
    private static void Gem3000() => Give(GemPath, 3000);

    [MenuItem(Root + "조개 +30,000", false, 2)]
    private static void Gem30000() => Give(GemPath, 30000);

    [MenuItem(Root + "조개 0으로", false, 3)]
    private static void GemZero() => Clear(GemPath);

    // 금화 (골드 조개 1회 = 500 + 250 × 왕국 레벨)
    [MenuItem(Root + "금화 +10,000", false, 20)]
    private static void Gold10K() => Give(GoldPath, 10000);

    [MenuItem(Root + "금화 +100,000", false, 21)]
    private static void Gold100K() => Give(GoldPath, 100000);

    [MenuItem(Root + "금화 +1,000,000", false, 22)]
    private static void Gold1M() => Give(GoldPath, 1000000);

    [MenuItem(Root + "금화 0으로", false, 23)]
    private static void GoldZero() => Clear(GoldPath);

    // 뽑기권 · 반짝 조각
    [MenuItem(Root + "뽑기권 +10 (별빛 · 보물)", false, 40)]
    private static void Tickets()
    {
        Give(TicketPickupPath, 10);
        Give(TicketStandardPath, 10);
    }

    [MenuItem(Root + "반짝 조각 +1,000", false, 41)]
    private static void Shards() => Give(ShardPath, 1000);

    [MenuItem(Root + "전부 넉넉히 (금화 100만 · 조개 3만 · 뽑기권 10 · 조각 1000)", false, 60)]
    private static void GiveAll()
    {
        Give(GoldPath, 1000000);
        Give(GemPath, 30000);
        Tickets();
        Shards();
    }

    private static void Give(string path, int amount)
    {
        var currency = Load(path);
        if (currency == null)
            return;
        CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(currency, amount, TransactionSource.TestGet));
        Debug.Log($"[Cheat] {currency.DisplayName} +{amount:N0} (지금 {CurrencyManager.Instance.GetCurrency(currency):N0})");
    }

    private static void Clear(string path)
    {
        var currency = Load(path);
        if (currency == null)
            return;
        int balance = CurrencyManager.Instance.GetCurrency(currency);
        if (balance > 0)
            CurrencyManager.Instance.TrySpend(currency, balance, TransactionSource.TestUse);
        Debug.Log($"[Cheat] {currency.DisplayName} 0으로 (지금 {CurrencyManager.Instance.GetCurrency(currency):N0})");
    }

    private static Currency Load(string path)
    {
        if (!EditorApplication.isPlaying || CurrencyManager.Instance == null)
        {
            EditorUtility.DisplayDialog("치트", "Play 중에만 쓸 수 있어요.\nPlay를 누른 뒤 다시 골라 주세요.", "확인");
            return null;
        }
        var currency = AssetDatabase.LoadAssetAtPath<Currency>(path);
        if (currency == null)
            Debug.LogWarning($"[Cheat] 재화가 없습니다: {path}");
        return currency;
    }
}
