/// <summary>
/// 거래로 잔액이 실제로 바뀐 내용. Delta는 한도에 걸려 잘린 뒤의 실제 변화량 (소비는 음수).
/// </summary>
public readonly struct CurrencyChange
{
    public readonly Currency Currency;
    public readonly int Delta;
    public readonly TransactionSource Source;

    public CurrencyChange(Currency currency, int delta, TransactionSource source)
    {
        Currency = currency;
        Delta = delta;
        Source = source;
    }
}
