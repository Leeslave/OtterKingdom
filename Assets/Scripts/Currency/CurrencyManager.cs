using System;
using System.Collections.Generic;
using UnityEngine;

// 다른 스크립트의 Awake/OnEnable보다 먼저 실행되어야 UI가 구독 시점에 Instance를 찾을 수 있음
[DefaultExecutionOrder(-100)]
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }
    public event Action<Currency, int> OnCurrencyChanged;

    /// <summary>
    /// 거래(획득·소비)로 잔액이 실제로 바뀔 때마다, 출처와 함께 (퀘스트의 "업그레이드 N회" 등).
    /// 세이브 복원(SetBalance)은 거래가 아니므로 알리지 않는다.
    /// </summary>
    public event Action<CurrencyChange> OnTransaction;

    private readonly Dictionary<Currency, int> _wallets = new Dictionary<Currency, int>();
    [SerializeField] private List<Currency> _allCurrencies;

    private void InitializeWallets()
    {
        foreach (var currency in _allCurrencies)
        {
            _wallets[currency] = 0;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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

    public bool CanAfford(Currency currency, int amount)
    {
        return GetCurrency(currency) >= amount;
    }

    public int GetCurrency(Currency currency)
    {
        if (currency == null) throw new ArgumentNullException(nameof(currency));

        return _wallets.TryGetValue(currency, out int balance) ? balance : 0;
    }

    /// <summary>
    /// 잔액을 그대로 지정 (세이브 복원용). 배율 미적용, 한도 안으로 자름.
    /// </summary>
    public void SetBalance(Currency currency, int amount)
    {
        if (currency == null) throw new ArgumentNullException(nameof(currency));

        int oldBalance = GetCurrency(currency);
        int newBalance = Math.Clamp(amount, currency.MinCapacity, currency.MaxCapacity);
        _wallets[currency] = newBalance;

        if (newBalance != oldBalance)
            OnCurrencyChanged?.Invoke(currency, newBalance);
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
        {
            OnCurrencyChanged?.Invoke(currency, newBalance);
            OnTransaction?.Invoke(new CurrencyChange(currency, newBalance - oldBalance, source));
        }
    }

    // 버프(곱하기 연산)
    private CurrencyTransaction ApplyGlobalModifiers(CurrencyTransaction tx)
    {
        return tx;
    }
}
