using System;
using UnityEngine;

/// <summary>
/// 번영의 시대 광장 연출 줄이기 (저사양 옵션, 기획서 P2): 켜면 수레 해달 · 먼지 연출을 끔. 장부(수레 옮기기 · 가공)는 그대로.
/// 기기마다 PlayerPrefs (세이브와 따로)
/// </summary>
public static class IndustryFx
{
    private const string Key = "industry_reduced_fx";
    private static bool? _reduced;

    public static event Action Changed;

    public static bool Reduced
    {
        get
        {
            if (!_reduced.HasValue)
                _reduced = PlayerPrefs.GetInt(Key, 0) == 1;
            return _reduced.Value;
        }
        set
        {
            if (Reduced == value)
                return;
            _reduced = value;
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
