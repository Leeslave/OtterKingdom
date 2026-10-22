using System;
using System.Collections.Generic;

/// <summary>
/// 특수 해달 편성 · 장신구 효과를 게임에 거는 곳 (Docs/가챠개편_특수해달조각_장신구_기획.md 3 · 4장).
/// 편성 버프는 KingdomBonus(왕국 전체)에 이미 합쳐져 있고, 장신구는 그 해달 한 마리 몫만 여기서 더한다.
/// 뽑기 매니저가 없거나 아직 세이브를 안 불렀으면 장신구 효과 0
/// </summary>
public static class CompanionBonus
{
    private static CompanionCollection Companions =>
        GachaManager.Instance != null && GachaManager.Instance.IsLoaded ? GachaManager.Instance.Companions : null;

    /// <summary>이 해달이 낀 장신구의 이 효과 % (없으면 0)</summary>
    public static int Accessory(string otterId, AccessoryEffect effect)
    {
        var companions = Companions;
        return companions != null && !string.IsNullOrEmpty(otterId) ? companions.AccessoryPercent(otterId, effect) : 0;
    }

    /// <summary>걷는 속도 배수: 왕국 이동속도(음유시인 편성) + 그 해달의 장신구 이동속도. 플레이어 해달 · 광산 탐험에는 쓰지 않음</summary>
    public static float MoveMultiplier(string otterId) =>
        1f + (KingdomBonus.Percent(KingdomBonusKind.MoveSpeed) + Accessory(otterId, AccessoryEffect.MoveSpeed)) / 100f;

    /// <summary>얻는 개수: 왕국 효과(kind) + 그 해달 장신구 "얻는 양" (소수는 그 확률로 1개 더, KingdomBonus.Amount와 같은 방식)</summary>
    public static int Amount(KingdomBonusKind kind, string otterId, int amount) => Amount(kind, otterId, amount, UnityEngine.Random.value);

    /// <param name="roll">0~1 난수 (테스트용)</param>
    public static int Amount(KingdomBonusKind kind, string otterId, int amount, float roll) =>
        AddPercent(amount, KingdomBonus.Percent(kind) + Accessory(otterId, AccessoryEffect.Yield), roll);

    /// <summary>장신구 "얻는 양"만 (왕국 효과가 따로 없는 광석 · 보석)</summary>
    public static int AccessoryAmount(string otterId, int amount) =>
        AddPercent(amount, Accessory(otterId, AccessoryEffect.Yield), UnityEngine.Random.value);

    /// <summary>주민 작업 시간: 맡은 해달 중 가장 좋은 장신구 "작업 시간"만큼 줄임 (최대 90%)</summary>
    public static double WorkSeconds(IEnumerable<string> otterIds, double seconds)
    {
        int best = 0;
        if (otterIds != null)
        {
            foreach (var id in otterIds)
                best = Math.Max(best, Accessory(id, AccessoryEffect.WorkTime));
        }
        best = Math.Min(90, best);
        return best <= 0 ? seconds : seconds * (100 - best) / 100.0;
    }

    /// <summary>개수에 %를 더함: 정수 부분은 늘, 남는 소수는 그 확률로 1개 (평균이 정확히 +%)</summary>
    public static int AddPercent(int amount, int percent, float roll)
    {
        if (percent <= 0 || amount <= 0)
            return amount;
        double exact = amount * (100 + percent) / 100.0;
        int whole = (int)Math.Floor(exact);
        return roll < exact - whole ? whole + 1 : whole;
    }
}
