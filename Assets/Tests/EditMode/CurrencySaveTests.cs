using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 재화 세이브: 골드뿐 아니라 조개도 저장·복원된다. 씬의 재화 목록에 없는 재화(밭·낚시터 씬의 조개)도
/// 처음 쓰는 순간 세이브 잔액으로 채워지고, 세이브에 없는 재화는 0.
/// </summary>
public class CurrencySaveTests
{
    private readonly List<Object> _created = new List<Object>();
    private Currency _gold;
    private Currency _gem;
    private CurrencyManager _manager;

    [SetUp]
    public void SetUp()
    {
        _gold = Currency("1");
        _gem = Currency("Gem");
        var go = new GameObject("CurrencyManager");
        _created.Add(go);
        _manager = go.AddComponent<CurrencyManager>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    [Test]
    public void SavedShells_ComeBackEvenIfUnknownAtLoad()
    {
        _manager.GetCurrency(_gold); // 이 씬은 골드만 앎

        _manager.LoadSavedBalances(new[] { Pair("1", 300), Pair("Gem", 25) });

        Assert.AreEqual(300, _manager.GetCurrency(_gold));
        Assert.AreEqual(25, _manager.GetCurrency(_gem), "처음 쓰는 순간 세이브 잔액");
    }

    [Test]
    public void Balances_IncludeShellsForSaving()
    {
        _manager.LoadSavedBalances(new[] { Pair("1", 100) });
        _manager.ProcessTransaction(new CurrencyTransaction(_gem, 7, TransactionSource.TestGet));

        var saved = _manager.Balances.ToDictionary(p => p.Key.CurrencyID, p => p.Value);
        Assert.AreEqual(7, saved["Gem"]);
    }

    [Test]
    public void MissingFromSave_IsZero_AndReloadResetsKnownWallets()
    {
        _manager.ProcessTransaction(new CurrencyTransaction(_gem, 9, TransactionSource.TestGet));

        _manager.LoadSavedBalances(new[] { Pair("1", 50) });

        Assert.AreEqual(0, _manager.GetCurrency(_gem), "새 세이브에 조개가 없으면 0");
        Assert.AreEqual(50, _manager.GetCurrency(_gold));
    }

    private Currency Currency(string id)
    {
        var currency = ScriptableObject.CreateInstance<Currency>();
        currency.CurrencyID = id;
        _created.Add(currency);
        return currency;
    }

    private static KeyValuePair<string, int> Pair(string id, int amount) => new KeyValuePair<string, int>(id, amount);
}
