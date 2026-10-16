using UnityEngine;

/// <summary>재화 비용 조건: 재화를 내고 연다 (예: 조개 100)</summary>
[CreateAssetMenu(fileName = "Unlock_Currency", menuName = "Game Data/Unlock/Currency Requirement")]
public class CurrencyUnlockRequirement : UnlockRequirement
{
    [Tooltip("내는 재화 (예: 조개)")]
    [SerializeField]
    private Currency _currency;

    [Tooltip("내는 금액")]
    [Min(1)]
    [SerializeField]
    private int _amount = 1;

    public Currency Currency => _currency;
    public int Amount => _amount;

    public override bool IsMet(UnlockContext context)
    {
        return _currency != null && context.Currency != null && context.Currency.CanAfford(_currency, _amount);
    }

    public override bool TryFulfill(UnlockContext context)
    {
        return _currency != null && context.Currency != null
            && context.Currency.TrySpend(_currency, _amount, TransactionSource.DecorExpand);
    }

    public override string Describe()
    {
        string name = _currency != null ? _currency.DisplayName : "?";
        return $"{name} {_amount:N0}";
    }
}
