using System;
using NUnit.Framework;
using UnityEngine;

public class GameSettingsTests
{
    private GameSettings _settings;
    private int _changedCount;

    [SetUp]
    public void SetUp()
    {
        _settings = new GameSettings();
        _changedCount = 0;
        _settings.OnChanged += () => _changedCount++;
    }

    [Test]
    public void Defaults_MatchDesign()
    {
        Assert.AreEqual(GameSettings.DefaultBgmVolume, _settings.BgmVolume);
        Assert.AreEqual(GameSettings.DefaultSfxVolume, _settings.SfxVolume);
        Assert.IsTrue(_settings.VibrationEnabled);
        Assert.IsFalse(_settings.PushEnabled);
        Assert.AreEqual(GameLanguage.Korean, _settings.Language);
    }

    [Test]
    public void SetBgmVolume_OutOfRange_IsClamped()
    {
        _settings.SetBgmVolume(1.5f);
        Assert.AreEqual(1f, _settings.BgmVolume);

        _settings.SetBgmVolume(-0.2f);
        Assert.AreEqual(0f, _settings.BgmVolume);
    }

    [Test]
    public void SetBgmVolume_NaN_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _settings.SetBgmVolume(float.NaN));
    }

    [Test]
    public void Setters_SameValue_DoNotFireEvent()
    {
        _settings.SetSfxVolume(_settings.SfxVolume);
        _settings.SetVibrationEnabled(_settings.VibrationEnabled);
        _settings.SetPushEnabled(_settings.PushEnabled);
        _settings.SetLanguage(_settings.Language);

        Assert.AreEqual(0, _changedCount);
    }

    [Test]
    public void Setters_NewValue_FireEventOnce()
    {
        _settings.SetPushEnabled(true);

        Assert.IsTrue(_settings.PushEnabled);
        Assert.AreEqual(1, _changedCount);
    }

    [Test]
    public void SetLanguage_Undefined_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _settings.SetLanguage((GameLanguage)99));
    }

    [Test]
    public void DataRoundTrip_KeepsValues()
    {
        _settings.SetBgmVolume(0.3f);
        _settings.SetSfxVolume(0.9f);
        _settings.SetVibrationEnabled(false);
        _settings.SetPushEnabled(true);

        var json = JsonUtility.ToJson(_settings.ToData());
        var restored = new GameSettings();
        restored.Apply(JsonUtility.FromJson<SettingsData>(json));

        Assert.AreEqual(0.3f, restored.BgmVolume, 1e-5f);
        Assert.AreEqual(0.9f, restored.SfxVolume, 1e-5f);
        Assert.IsFalse(restored.VibrationEnabled);
        Assert.IsTrue(restored.PushEnabled);
    }

    [Test]
    public void Apply_InvalidData_IsCorrectedWithoutEvent()
    {
        var restored = new GameSettings();
        int events = 0;
        restored.OnChanged += () => events++;

        restored.Apply(new SettingsData { bgmVolume = 3f, sfxVolume = float.NaN, language = 42 });

        Assert.AreEqual(1f, restored.BgmVolume);
        Assert.AreEqual(GameSettings.DefaultSfxVolume, restored.SfxVolume);
        Assert.AreEqual(GameLanguage.Korean, restored.Language);
        Assert.AreEqual(0, events);
    }

    [Test]
    public void Apply_MissingFields_UseDefaults()
    {
        // 옛 버전에서 저장한 JSON에 새 필드가 없을 때
        var restored = new GameSettings();
        restored.Apply(JsonUtility.FromJson<SettingsData>("{\"push\":true}"));

        Assert.AreEqual(GameSettings.DefaultBgmVolume, restored.BgmVolume);
        Assert.IsTrue(restored.VibrationEnabled);
        Assert.IsTrue(restored.PushEnabled);
    }
}
