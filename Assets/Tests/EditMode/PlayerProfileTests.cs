using System;
using NUnit.Framework;

public class PlayerProfileTests
{
    [Test]
    public void New_StartsAtLevelOneWithTrimmedName()
    {
        var profile = new PlayerProfile("  해달왕 ");

        Assert.AreEqual("해달왕", profile.Name);
        Assert.AreEqual(1, profile.Level);
        Assert.AreEqual(0f, profile.ExpRatio);
    }

    [Test]
    public void SetLevel_ClampsRatioAndNotifiesOnlyOnChange()
    {
        // Arrange
        var profile = new PlayerProfile("해달왕");
        int changes = 0;
        profile.OnChanged += () => changes++;

        // Act
        profile.SetLevel(5, 1.7f);
        profile.SetLevel(5, 1f);

        // Assert
        Assert.AreEqual(5, profile.Level);
        Assert.AreEqual(1f, profile.ExpRatio, "1을 넘으면 1로 잘라야 합니다.");
        Assert.AreEqual(1, changes, "값이 같으면 알리지 않습니다.");
    }

    [Test]
    public void InvalidValues_Throw()
    {
        var profile = new PlayerProfile("해달왕");

        Assert.Throws<ArgumentException>(() => profile.SetName("  "));
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.SetLevel(0, 0f));
        Assert.Throws<ArgumentException>(() => profile.SetLevel(2, float.NaN));
    }
}
