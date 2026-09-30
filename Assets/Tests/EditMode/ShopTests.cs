using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ShopTests
{
    private ItemDefinition _item;
    private ShopProduct _single;
    private ShopProduct _bundle;

    [SetUp]
    public void SetUp()
    {
        _item = ScriptableObject.CreateInstance<ItemDefinition>();
        var itemSo = new SerializedObject(_item);
        itemSo.FindProperty("_displayName").stringValue = "지렁이 미끼";
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        _single = CreateProduct(1, 300);
        _bundle = CreateProduct(10, 50);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_single);
        Object.DestroyImmediate(_bundle);
        Object.DestroyImmediate(_item);
    }

    private ShopProduct CreateProduct(int bundle, int price)
    {
        var product = ScriptableObject.CreateInstance<ShopProduct>();
        var so = new SerializedObject(product);
        so.FindProperty("_item").objectReferenceValue = _item;
        so.FindProperty("_bundleSize").intValue = bundle;
        so.FindProperty("_price").intValue = price;
        so.ApplyModifiedPropertiesWithoutUndo();
        return product;
    }

    [Test]
    public void BundleName_ShowsCount()
    {
        Assert.AreEqual("지렁이 미끼", _single.DisplayName);
        Assert.AreEqual("지렁이 미끼 x10", _bundle.DisplayName);
    }

    [Test]
    public void MaxQuantity_FollowsBagRoomAndBundle()
    {
        Assert.AreEqual(4, ShopPurchaseRules.MaxQuantity(_single, 4));
        Assert.AreEqual(2, ShopPurchaseRules.MaxQuantity(_bundle, 25), "10개 묶음은 25개 자리에 2번만 들어갑니다.");
        Assert.AreEqual(0, ShopPurchaseRules.MaxQuantity(_bundle, 9), "한 묶음도 못 넣으면 0");
        Assert.AreEqual(ShopPurchaseRules.MaxQuantityPerPurchase, ShopPurchaseRules.MaxQuantity(_single, 5000), "한 번에 너무 많이 고르지 않도록 제한");
    }

    [Test]
    public void Totals_MultiplyByQuantity()
    {
        Assert.AreEqual(1500, ShopPurchaseRules.TotalPrice(_single, 5));
        Assert.AreEqual(30, ShopPurchaseRules.TotalItems(_bundle, 3));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => ShopPurchaseRules.TotalPrice(_single, 0));
    }
}
