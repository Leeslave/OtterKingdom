using System;

/// <summary>
/// 판매 팝업의 수량 선택 (UI 없이 테스트 가능하도록 분리). 1개 ~ 가진 개수 사이에서만 움직인다.
/// </summary>
public class SellQuantity
{
    public int Value { get; private set; } = 1;
    public int Max { get; private set; } = 1;

    public bool CanDecrease => Value > 1;
    public bool CanIncrease => Value < Max;

    /// <summary>새 아이템으로 시작: 1개부터</summary>
    public void Reset(int owned)
    {
        if (owned < 1)
            throw new ArgumentOutOfRangeException(nameof(owned), "팔 수 있는 개수가 1 이상이어야 합니다.");

        Max = owned;
        Value = 1;
    }

    public void Increase() => Value = Math.Min(Value + 1, Max);
    public void Decrease() => Value = Math.Max(Value - 1, 1);
    public void SetToMax() => Value = Max;

    /// <returns>판매가 × 수량 (int 범위를 넘으면 int 최대값)</returns>
    public int Total(int unitPrice)
    {
        return (int)Math.Min((long)Math.Max(0, unitPrice) * Value, int.MaxValue);
    }
}
