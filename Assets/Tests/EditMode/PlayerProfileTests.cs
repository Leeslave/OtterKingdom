using System;
using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class PlayerProfileTests
{
    [TestCase(null, false, false)]
    [TestCase("해달왕", false, false)]
    [TestCase("해달왕", true, true)]
    [TestCase("수달이", false, true)]
    public void SavedName_SetupStateSurvivesRoundTrip(string name, bool chosen, bool expected)
    {
        var owner = new GameObject("ProfileTest");
        owner.SetActive(false);
        var table = ScriptableObject.CreateInstance<LevelTable>();
        try
        {
            var manager = owner.AddComponent<ProfileManager>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ProfileManager).GetField("_levelTable", flags).SetValue(manager, table);
            typeof(ProfileManager).GetMethod("Awake", flags).Invoke(manager, null);
            manager.LoadFromSave(new ProfileSaveData { name = name, nameChosen = chosen });
            Assert.AreEqual(expected, manager.HasChosenName);
            manager.ChooseName("  해달왕  ");
            var saved = new ProfileSaveData();
            manager.WriteToSave(saved);
            var restored = JsonUtility.FromJson<ProfileSaveData>(JsonUtility.ToJson(saved));
            manager.LoadFromSave(restored);
            Assert.IsTrue(manager.HasChosenName, "Choosing the default name must also finish setup.");
            Assert.AreEqual("해달왕", manager.Profile.Name);
        }
        finally
        {
            // This inactive object never enters Unity's normal lifecycle.
            typeof(ProfileManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(owner.GetComponent<ProfileManager>(), null);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(table);
        }
    }

    [TestCase(null, false)]
    [TestCase("   ", false)]
    [TestCase("<b>왕</b>", false)]
    [TestCase("해달\n왕", false)]
    [TestCase("12345", false)]
    [TestCase("1234", true)]
    [TestCase("  해달이  ", true)]
    public void NewName_ValidatesInput(string name, bool expected)
    {
        Assert.AreEqual(expected, PlayerProfile.TryNormalizeNewName(name, out var normalized));
        if (expected) Assert.AreEqual(name.Trim(), normalized);
    }

    [Test]
    public void SetName_NotifiesHudAndPreservesProgress()
    {
        var profile = new PlayerProfile("해달왕");
        profile.SetLevel(3, 0.5f);
        int changes = 0;
        profile.OnChanged += () => changes++;
        profile.SetName("  수달이  ");
        Assert.AreEqual("수달이", profile.Name);
        Assert.AreEqual(3, profile.Level);
        Assert.AreEqual(0.5f, profile.ExpRatio);
        Assert.AreEqual(1, changes);
    }

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
