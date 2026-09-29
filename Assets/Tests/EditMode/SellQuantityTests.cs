using System;
using NUnit.Framework;

public class SellQuantityTests
{
    [Test]
    public void Reset_StartsAtOneWithMaxOwned()
    {
        // Arrange
        var quantity = new SellQuantity();

        // Act
        quantity.Reset(12);

        // Assert
        Assert.AreEqual(1, quantity.Value);
        Assert.AreEqual(12, quantity.Max);
        Assert.IsFalse(quantity.CanDecrease);
        Assert.IsTrue(quantity.CanIncrease);
    }

    [Test]
    public void IncreaseAndDecrease_StayWithinOneAndMax()
    {
        // Arrange
        var quantity = new SellQuantity();
        quantity.Reset(2);

        // Act & Assert
        quantity.Decrease();
        Assert.AreEqual(1, quantity.Value, "1 아래로 내려가면 안 됩니다.");
        quantity.Increase();
        quantity.Increase();
        Assert.AreEqual(2, quantity.Value, "가진 개수를 넘으면 안 됩니다.");
        Assert.IsFalse(quantity.CanIncrease);
    }

    [Test]
    public void SetToMax_SelectsAll()
    {
        // Arrange
        var quantity = new SellQuantity();
        quantity.Reset(7);

        // Act
        quantity.SetToMax();

        // Assert
        Assert.AreEqual(7, quantity.Value);
    }

    [Test]
    public void Reset_AfterSellingMore_StartsOverAtOne()
    {
        // Arrange: 다른 아이템을 팔려고 다시 열었을 때 이전 수량이 남지 않아야 함
        var quantity = new SellQuantity();
        quantity.Reset(9);
        quantity.SetToMax();

        // Act
        quantity.Reset(3);

        // Assert
        Assert.AreEqual(1, quantity.Value);
        Assert.AreEqual(3, quantity.Max);
    }

    [Test]
    public void Total_IsPriceTimesQuantityAndClamped()
    {
        // Arrange
        var quantity = new SellQuantity();
        quantity.Reset(1000);
        quantity.SetToMax();

        // Act & Assert
        Assert.AreEqual(15000, quantity.Total(15));
        Assert.AreEqual(int.MaxValue, quantity.Total(int.MaxValue), "int 범위를 넘으면 최대값으로 잘라야 합니다.");
        Assert.AreEqual(0, quantity.Total(-5));
    }

    [Test]
    public void Reset_NothingOwned_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SellQuantity().Reset(0));
    }
}
