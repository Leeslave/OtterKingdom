/// <summary>
/// 큰 숫자를 짧게 표시 (1,234 → 1,234 / 12,500 → 12.5K / 3,400,000 → 3.4M)
/// </summary>
public static class NumberFormatter
{
    public static string Short(long value)
    {
        if (value >= 1_000_000_000)
            return (value / 1_000_000_000f).ToString("0.#") + "B";
        if (value >= 1_000_000)
            return (value / 1_000_000f).ToString("0.#") + "M";
        if (value >= 10_000)
            return (value / 1_000f).ToString("0.#") + "K";
        return value.ToString("N0"); // 9,999까지는 쉼표만
    }
}
