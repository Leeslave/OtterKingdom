using System;
using System.Collections.Generic;
using UnityEngine;

// 다른 스크립트의 Awake/OnEnable보다 먼저 실행되어야 UI가 구독 시점에 Instance를 찾을 수 있음
[DefaultExecutionOrder(-100)]
public class CurrencyManager : MonoBehaviour
{
    private static CurrencyManager _instance;

    // 없으면 새로 생성하는 Instance와 달리, 순수 존재 확인용. 씬이 닫히는 도중(OnDisable/OnDestroy)에
    // Instance를 읽어버리면 그 시점에 새 GameObject가 생성되어 정리되지 못한 채 남는 문제가 있었다.
    public static bool Exists => _instance != null;

    // 씬에 배치하지 않은 씬(밭/광장 등)에서도 쓸 수 있도록 없으면 새로 생성
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

    public event Action<Currency, int> OnCurrencyChanged;

    private readonly Dictionary<Currency, int> _wallets = new Dictionary<Currency, int>();
    [SerializeField] private List<Currency> _allCurrencies;

    private void InitializeWallets()
    {
        // 코드로 생성된 경우 목록이 비어 있음 → LoadFromSave에서 채움
        if (_allCurrencies == null) return;

        foreach (var currency in _allCurrencies)
        {
            _wallets[currency] = 0;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeWallets();
    }

    /// <summary>
    /// 재화 획득 (배율 적용). 음수 금액은 MinCapacity까지 깎이므로 소비에는 TrySpend를 사용.
    /// </summary>
    public void ProcessTransaction(CurrencyTransaction tx)
    {
        if (tx.Currency == null) throw new ArgumentNullException(nameof(tx.Currency));

        tx = ApplyGlobalModifiers(tx);
        ApplyChange(tx.Currency, tx.FinalAmount, tx.Source);
    }

    /// <summary>
    /// 재화 소비. 잔액이 부족하면 아무것도 바꾸지 않고 false 반환 (배율 미적용).
    /// </summary>
    public bool TrySpend(Currency currency, int amount, TransactionSource source)
    {
        if (currency == null) throw new ArgumentNullException(nameof(currency));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "amount는 1 이상이어야 합니다.");

        if (!CanAfford(currency, amount))
            return false;

        ApplyChange(currency, -amount, source);
        return true;
    }

    /// <summary>
    /// 재화 획득 (ProcessTransaction과 같이 배율 적용). 판매 대금처럼 0원이 나올 수 있는 곳에서 쓰므로
    /// 0 이하는 무시하고 false 반환.
    /// </summary>
    public bool Add(Currency currency, int amount, TransactionSource source)
    {
        if (currency == null) throw new ArgumentNullException(nameof(currency));
        if (amount <= 0) return false;

        ProcessTransaction(new CurrencyTransaction(currency, amount, source));
        return true;
    }

    public bool CanAfford(Currency currency, int amount)
    {
        return GetCurrency(currency) >= amount;
    }

    public int GetCurrency(Currency currency)
    {
        if (currency == null) throw new ArgumentNullException(nameof(currency));

        return _wallets.TryGetValue(currency, out int balance) ? balance : 0;
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

    private void ApplyChange(Currency currency, int delta, TransactionSource source)
    {
        int oldBalance = GetCurrency(currency);

        // long으로 계산해서 int 범위를 넘는 경우(오버플로)를 막은 뒤 한도 안으로 자름
        long raw = (long)oldBalance + delta;
        int newBalance = (int)Math.Clamp(raw, currency.MinCapacity, currency.MaxCapacity);

        _wallets[currency] = newBalance;

        Debug.Log($"[{source}] {currency.CurrencyID} {newBalance - oldBalance:+#;-#;0} 변동됨. (현재 잔액: {newBalance})");

        // 한도에 걸려 실제 변화가 없으면 알림 생략
        if (newBalance != oldBalance)
            OnCurrencyChanged?.Invoke(currency, newBalance);
    }

    // 버프(곱하기 연산)
    private CurrencyTransaction ApplyGlobalModifiers(CurrencyTransaction tx)
    {
        return tx;
    }
}
