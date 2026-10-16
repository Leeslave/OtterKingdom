using NUnit.Framework;
using UnityEngine;

public class TimeOfDayTests
{
    [Test]
    public void Night_IsOnAtNight_OffAtNoon_AndFadesAtDusk()
    {
        Assert.AreEqual(1f, TimeOfDay.Night(22f));
        Assert.AreEqual(1f, TimeOfDay.Night(3f));
        Assert.AreEqual(0f, TimeOfDay.Night(12f));
        float dusk = TimeOfDay.Night(19.25f);
        Assert.Greater(dusk, 0f, "해질녘에는 서서히 켜짐");
        Assert.Less(dusk, 1f);
        Assert.AreEqual(1f, TimeOfDay.Night(46f), "24시간을 넘어도 같은 시각으로");
    }

    [Test]
    public void Light_IsWhiteAtNoon_DimBlueAtNight_WarmAtSunset()
    {
        var (noon, noonIntensity) = TimeOfDay.Light(12f);
        Assert.AreEqual(Color.white, noon);
        Assert.AreEqual(1f, noonIntensity, 0.001f);

        var (night, nightIntensity) = TimeOfDay.Light(23f);
        Assert.Greater(night.b, night.r, "밤은 푸름");
        Assert.Less(nightIntensity, 0.8f, "밤은 어두움");

        var (sunset, _) = TimeOfDay.Light(18.2f);
        Assert.Greater(sunset.r, sunset.b, "노을은 따뜻함");
    }
}
