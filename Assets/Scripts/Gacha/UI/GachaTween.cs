using System;
using System.Collections;
using UnityEngine;

/// <summary>뽑기 연출용 짧은 보간 (시간 정지와 상관없이 실제 시간으로)</summary>
public static class GachaTween
{
    /// <summary>seconds 동안 0→1을 step에 넘김 (마지막은 정확히 1)</summary>
    public static IEnumerator Run(float seconds, Action<float> step)
    {
        if (seconds > 0f)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                step(t / seconds);
                yield return null;
            }
        }
        step(1f);
    }

    public static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            yield return null;
    }

    public static float OutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

    public static float InCubic(float t) => Mathf.Pow(Mathf.Clamp01(t), 3f);

    public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) / 2f;

    /// <summary>목표를 조금 넘었다 돌아옴 (뽀잉)</summary>
    public static float OutBack(float t, float overshoot = 1.70158f)
    {
        t = Mathf.Clamp01(t) - 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }

    /// <summary>한 번 커졌다 돌아오는 값 (0 → 1 → 0)</summary>
    public static float Bump(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);

    public static void SetAlpha(UnityEngine.UI.Graphic graphic, float alpha)
    {
        var color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
