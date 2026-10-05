using NUnit.Framework;
using UnityEngine;

/// <summary>소모형 모종 작물 판매가: 한 번 수확해서 파는 값 = 모종 하나 값 × 배율</summary>
public class CropPricingTests
{
    [TestCase(100, 3, 1.5, 50)]  // 감자: 모종 100 → 3개 × 50 = 150
    [TestCase(150, 3, 1.5, 75)]  // 고구마: 150 → 3개 × 75 = 225
    [TestCase(300, 5, 1.5, 90)]  // 딸기: 300 → 5개 × 90 = 450
    [TestCase(100, 3, 2.0, 67)]  // 나누어떨어지지 않으면 올림 (배율보다 덜 받지 않게)
    public void SellPrice_MakesOneHarvestWorthRatioTimesSeed(double seedPrice, int yieldCount, double ratio, int expected)
    {
        int price = CropPricing.SellPriceFor(seedPrice, yieldCount, ratio);

        Assert.AreEqual(expected, price);
        Assert.GreaterOrEqual(price * yieldCount, seedPrice * ratio);
    }

    [Test]
    public void SellPrice_InvalidInputs_Zero()
    {
        Assert.AreEqual(0, CropPricing.SellPriceFor(100, 0, 1.5));
        Assert.AreEqual(0, CropPricing.SellPriceFor(0, 3, 1.5));
    }

    [Test]
    public void DefaultRatio_IsOneAndAHalf()
    {
        var pricing = ScriptableObject.CreateInstance<CropPricing>();
        Assert.AreEqual(1.5f, pricing.HarvestToSeedRatio);
        Object.DestroyImmediate(pricing);
    }
}
