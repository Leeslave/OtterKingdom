using System;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    private static CurrencyManager _instance;

    // 없으면 새로 생성하는 Instance와 달리, 순수 존재 확인용. 씬이 닫히는 도중(OnDisable/OnDestroy)에
    // Instance를 읽어버리면 그 시점에 새 GameObject가 생성되어 정리되지 못한 채 남는 문제가 있었다.
    public static bool Exists => _instance != null;

    public static CurrencyManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(CurrencyManager));
                _instance = go.AddComponent<CurrencyManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public Action<Currency, int> OnCurrencyChanged;

    private Dictionary<Currency, int> _wallets = new Dictionary<Currency, int>();
    [SerializeField] private List<Currency> _allCurrencies;

    private void InitializeWallets()
    {
        if (_allCurrencies == null) return;

        foreach (var currency in _allCurrencies)
        {
            _wallets[currency] = 0;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        InitializeWallets();
    }

    // 트랜잭션을 적용한다. 결과 잔액이 화폐의 보유 한도를 벗어나면(예: 잔액 부족) 적용하지 않고 false를 반환한다.
    public bool ProcessTransaction(CurrencyTransaction tx)
    {
        tx = ApplyGlobalModifiers(tx);
        int finalAmount = tx.FinalAmount;

        if (!_wallets.ContainsKey(tx.Currency))
            _wallets[tx.Currency] = 0;

        int newBalance = _wallets[tx.Currency] + finalAmount;
        if (newBalance < tx.Currency.MinCapacity || newBalance > tx.Currency.MaxCapacity)
            return false;

        _wallets[tx.Currency] = newBalance;

        Debug.Log($"[{tx.Source}] {tx.Currency.CurrencyID} {finalAmount} 변동됨. (현재 잔액: {_wallets[tx.Currency]})");

        // UI 업데이트 방송
        OnCurrencyChanged?.Invoke(tx.Currency, _wallets[tx.Currency]);
        return true;
    }

    public bool TrySpend(Currency currency, int amount, TransactionSource source)
    {
        if (amount <= 0) return false;
        return ProcessTransaction(new CurrencyTransaction(currency, -amount, source));
    }

    public bool Add(Currency currency, int amount, TransactionSource source)
    {
        if (amount <= 0) return false;
        return ProcessTransaction(new CurrencyTransaction(currency, amount, source));
    }

    public int GetCurrency(Currency currency)
    {
        return _wallets.TryGetValue(currency, out var amount) ? amount : 0;
    }

    public void LoadFromSave(SaveData save, IEnumerable<Currency> knownCurrencies)
    {
        foreach (var currency in knownCurrencies)
        {
            if (currency == null) continue;

            var entry = save.currencies.Find(c => c.currencyId == currency.CurrencyID);
            _wallets[currency] = entry != null ? entry.amount : 0;
        }
    }

    public void SaveToSave(SaveData save)
    {
        save.currencies.Clear();
        foreach (var wallet in _wallets)
        {
            save.currencies.Add(new CurrencyBalance { currencyId = wallet.Key.CurrencyID, amount = wallet.Value });
        }
    }

    // 버프(곱하기 연산)
    private CurrencyTransaction ApplyGlobalModifiers(CurrencyTransaction tx)
    {
        return tx;
    }
}
