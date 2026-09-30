using System;
using UnityEngine;

/// <summary>게임 언어. 새 값은 맨 뒤에 추가 (저장된 번호가 바뀌지 않게)</summary>
public enum GameLanguage
{
    Korean,
}

public static class GameLanguageExtensions
{
    public static string ToDisplayName(this GameLanguage language)
    {
        switch (language)
        {
            case GameLanguage.Korean: return "한국어";
            default: return language.ToString();
        }
    }
}

/// <summary>
/// 기기별 환경설정 (배경음, 효과음, 진동, 푸시 알림, 언어). 세이브 파일이 아니라 기기에 저장한다.
/// 값을 바꾸고 알리기만 하고, 소리/진동에 실제로 적용하는 쪽은 이 값을 읽어 간다.
/// </summary>
public class GameSettings
{
    public const float DefaultBgmVolume = 0.7f;
    public const float DefaultSfxVolume = 0.7f;

    public float BgmVolume { get; private set; } = DefaultBgmVolume;
    public float SfxVolume { get; private set; } = DefaultSfxVolume;
    public bool VibrationEnabled { get; private set; } = true;
    public bool PushEnabled { get; private set; }
    public GameLanguage Language { get; private set; } = GameLanguage.Korean;

    /// <summary>값이 실제로 바뀔 때만</summary>
    public event Action OnChanged;

    public void SetBgmVolume(float volume) => SetVolume(volume, BgmVolume, v => BgmVolume = v);
    public void SetSfxVolume(float volume) => SetVolume(volume, SfxVolume, v => SfxVolume = v);

    public void SetVibrationEnabled(bool enabled)
    {
        if (enabled == VibrationEnabled)
            return;
        VibrationEnabled = enabled;
        OnChanged?.Invoke();
    }

    public void SetPushEnabled(bool enabled)
    {
        if (enabled == PushEnabled)
            return;
        PushEnabled = enabled;
        OnChanged?.Invoke();
    }

    public void SetLanguage(GameLanguage language)
    {
        if (!Enum.IsDefined(typeof(GameLanguage), language))
            throw new ArgumentOutOfRangeException(nameof(language));
        if (language == Language)
            return;
        Language = language;
        OnChanged?.Invoke();
    }

    private void SetVolume(float volume, float current, Action<float> assign)
    {
        if (float.IsNaN(volume))
            throw new ArgumentOutOfRangeException(nameof(volume), "음량이 숫자가 아닙니다.");

        volume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(volume, current))
            return;
        assign(volume);
        OnChanged?.Invoke();
    }

    #region 저장

    public SettingsData ToData()
    {
        return new SettingsData
        {
            bgmVolume = BgmVolume,
            sfxVolume = SfxVolume,
            vibration = VibrationEnabled,
            push = PushEnabled,
            language = (int)Language,
        };
    }

    /// <summary>저장된 값 복원. 범위를 벗어나거나 모르는 언어는 보정한다 (알림 없음)</summary>
    public void Apply(SettingsData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        BgmVolume = float.IsNaN(data.bgmVolume) ? DefaultBgmVolume : Mathf.Clamp01(data.bgmVolume);
        SfxVolume = float.IsNaN(data.sfxVolume) ? DefaultSfxVolume : Mathf.Clamp01(data.sfxVolume);
        VibrationEnabled = data.vibration;
        PushEnabled = data.push;
        Language = Enum.IsDefined(typeof(GameLanguage), data.language) ? (GameLanguage)data.language : GameLanguage.Korean;
    }

    #endregion
}

/// <summary>기기에 JSON으로 저장하는 형식 (필드 이름을 바꾸면 저장된 값이 사라지니 주의)</summary>
[Serializable]
public class SettingsData
{
    public float bgmVolume = GameSettings.DefaultBgmVolume;
    public float sfxVolume = GameSettings.DefaultSfxVolume;
    public bool vibration = true;
    public bool push;
    public int language;
}
