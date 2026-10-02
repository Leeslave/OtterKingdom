using System;

/// <summary>
/// 한국어 조사 고르기. 숫자는 읽는 소리의 받침으로 (5,500을 / 1,002를), 낱말은 끝 글자의 받침으로 (조개가 / 골드가) 정한다.
/// </summary>
public static class KoreanParticle
{
    // 일 이 삼 사 오 육 칠 팔 구 → 받침이 있는 것: 일(ㄹ) 삼(ㅁ) 육(ㄱ) 칠(ㄹ) 팔(ㄹ)
    private static readonly bool[] DigitHasFinal = { false, true, false, true, false, false, true, true, true, false };

    /// <returns>"을" 또는 "를"</returns>
    public static string ObjectParticle(long number)
    {
        return HasFinalConsonant(number) ? "을" : "를";
    }

    /// <returns>낱말 뒤 "이" 또는 "가" (한글이 아닌 글자로 끝나면 "가")</returns>
    public static string SubjectParticle(string word) => HasFinalConsonant(word) ? "이" : "가";

    /// <returns>낱말 뒤 "을" 또는 "를" (한글이 아닌 글자로 끝나면 "를")</returns>
    public static string ObjectParticle(string word) => HasFinalConsonant(word) ? "을" : "를";

    private static bool HasFinalConsonant(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;
        char last = word[word.Length - 1];
        return last >= '가' && last <= '힣' && (last - '가') % 28 != 0;
    }

    // 0으로 끝나면 십·백·천·만·억 등 자릿수 이름으로 끝나는데, 모두 받침이 있다 (0 하나는 "영"도 받침 있음)
    private static bool HasFinalConsonant(long number)
    {
        int last = (int)Math.Abs(number % 10);
        return last == 0 || DigitHasFinal[last];
    }
}
