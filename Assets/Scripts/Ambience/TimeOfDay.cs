using System;
using UnityEngine;

/// <summary>
/// 하루 시간대의 빛 (순수 계산): 실제 시각에 맞춰 아침은 상쾌하게, 낮은 그대로, 해질녘은 노을빛, 밤은 푸르고 어둡게.
/// 시각 사이는 부드럽게 섞는다. Night(0~1)는 밤 불빛(창문·가로등·반딧불)을 켜는 정도.
/// 에디터에서 PreviewHour로 아무 시각이나 미리 볼 수 있다 (Tools/Time Of Day 메뉴).
/// </summary>
public static class TimeOfDay
{
    /// <summary>미리 보기 시각 (0~24). 음수면 실제 시각</summary>
    public static float PreviewHour = -1f;

    // (시각, 빛 색, 세기)
    private static readonly (float hour, Color color, float intensity)[] Keys =
    {
        (0f, new Color(0.38f, 0.45f, 0.85f), 0.55f),     // 밤: 푸르고 어둡게 (창문·가로등이 돋보이게)
        (4.5f, new Color(0.38f, 0.45f, 0.85f), 0.55f),
        (5.8f, new Color(0.98f, 0.76f, 0.86f), 0.8f),    // 새벽 분홍
        (7.5f, new Color(1f, 0.97f, 0.92f), 1f),         // 아침
        (12f, Color.white, 1f),                          // 낮
        (16.5f, new Color(1f, 0.95f, 0.85f), 1f),        // 오후
        (18.2f, new Color(1f, 0.72f, 0.5f), 0.95f),      // 노을
        (19.6f, new Color(0.72f, 0.6f, 0.9f), 0.72f),    // 땅거미 보라
        (21f, new Color(0.38f, 0.45f, 0.85f), 0.55f),    // 밤
        (24f, new Color(0.38f, 0.45f, 0.85f), 0.55f),
    };

    /// <summary>지금 시각 (0~24, 미리 보기 우선)</summary>
    public static float CurrentHour => PreviewHour >= 0f ? PreviewHour % 24f : (float)GameClock.Now.TimeOfDay.TotalHours;

    /// <summary>그 시각의 빛 색·세기</summary>
    public static (Color color, float intensity) Light(float hour)
    {
        hour = Wrap(hour);
        for (int i = 0; i < Keys.Length - 1; i++)
        {
            var a = Keys[i];
            var b = Keys[i + 1];
            if (hour < a.hour || hour > b.hour)
                continue;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.hour, b.hour, hour));
            return (Color.Lerp(a.color, b.color, t), Mathf.Lerp(a.intensity, b.intensity, t));
        }
        return (Keys[0].color, Keys[0].intensity);
    }

    /// <summary>밤 불빛을 켜는 정도: 해질녘 18:30~20:00 서서히 켜지고, 새벽 5:00~6:30 서서히 꺼짐</summary>
    public static float Night(float hour)
    {
        hour = Wrap(hour);
        if (hour >= 20f || hour <= 5f)
            return 1f;
        if (hour > 18.5f)
            return Mathf.SmoothStep(0f, 1f, (hour - 18.5f) / 1.5f);
        if (hour < 6.5f)
            return Mathf.SmoothStep(1f, 0f, (hour - 5f) / 1.5f);
        return 0f;
    }

    private static float Wrap(float hour) => ((hour % 24f) + 24f) % 24f;
}
