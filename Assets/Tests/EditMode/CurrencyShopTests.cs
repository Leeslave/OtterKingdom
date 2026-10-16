using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CurrencyShopTests
{
    private CurrencyPack CreatePack(int baseAmount, int bonus, Currency price)
    {
        var pack = ScriptableObject.CreateInstance<CurrencyPack>();
        var so = new SerializedObject(pack);
        so.FindProperty("_baseAmount").intValue = baseAmount;
        so.FindProperty("_bonusAmount").intValue = bonus;
        so.FindProperty("_priceCurrency").objectReferenceValue = price;
        so.ApplyModifiedPropertiesWithoutUndo();
        return pack;
    }

    [Test]
    public void Pack_TotalAndBonusPercent()
    {
        var pack = CreatePack(600, 100, null);

        Assert.AreEqual(700, pack.TotalAmount);
        Assert.AreEqual(17, pack.BonusPercent, "100/600 = 16.7% → 반올림 17%");
        Assert.IsTrue(pack.IsCashPack, "가격 재화가 없으면 현금 상품입니다.");

        Object.DestroyImmediate(pack);
    }

    [Test]
    public void Pack_WithoutBonus_IsZeroPercent()
    {
        var gem = ScriptableObject.CreateInstance<Currency>();
        var pack = CreatePack(1000, 0, gem);

        Assert.AreEqual(0, pack.BonusPercent);
        Assert.IsFalse(pack.IsCashPack);

        Object.DestroyImmediate(pack);
        Object.DestroyImmediate(gem);
    }

    [TestCase(5500, "을")]
    [TestCase(1000, "을")]
    [TestCase(30000, "을")]
    [TestCase(1002, "를")]
    [TestCase(1005, "를")]
    [TestCase(1003, "을")]
    [TestCase(1009, "를")]
    public void ObjectParticle_FollowsHowTheNumberIsRead(long number, string expected)
    {
        Assert.AreEqual(expected, KoreanParticle.ObjectParticle(number));
    }

    [TestCase("조개", "가")]
    [TestCase("골드", "가")]
    [TestCase("보석", "이")]
    [TestCase("Gem", "가")]
    public void SubjectParticle_FollowsLastSyllable(string word, string expected)
    {
        Assert.AreEqual(expected, KoreanParticle.SubjectParticle(word));
    }
}
