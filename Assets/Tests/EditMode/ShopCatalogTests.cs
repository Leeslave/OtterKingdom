using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 요정 상점 상품 순서: 탭이 없는 재료(목재) → 탭 순서 → 같은 탭 안에서는 필요 레벨 낮은 순 (4차 QA: 모종이 고구마 Lv.8 · 딸기 Lv.11 · 무 Lv.5 순이었음)
/// </summary>
public class ShopCatalogTests
{
    private readonly List<Object> _created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _created)
            Object.DestroyImmediate(obj);
        _created.Clear();
    }

    [Test]
    public void Products_MaterialFirst_ThenTabOrder_ThenLevel()
    {
        var gold = Make<Currency>();
        var seeds = Make<ItemCategory>();
        var toys = Make<ItemCategory>();
        var materials = Make<ItemCategory>();

        var sweetPotato = Product("sweet_potato", seeds, gold, sortOrder: 1, level: 8);
        var radish = Product("radish", seeds, gold, sortOrder: 10, level: 5);
        var ball = Product("ball", toys, gold, sortOrder: 3, level: 1);
        var wood = Product("wood", materials, gold, sortOrder: 0, level: 4);

        var catalog = Make<ShopCatalog>();
        var so = new SerializedObject(catalog);
        var tabs = so.FindProperty("_tabs");
        tabs.arraySize = 2;
        tabs.GetArrayElementAtIndex(0).FindPropertyRelative("_label").stringValue = "씨앗";
        tabs.GetArrayElementAtIndex(0).FindPropertyRelative("_category").objectReferenceValue = seeds;
        tabs.GetArrayElementAtIndex(1).FindPropertyRelative("_label").stringValue = "장난감";
        tabs.GetArrayElementAtIndex(1).FindPropertyRelative("_category").objectReferenceValue = toys;
        var products = so.FindProperty("_products");
        var list = new[] { ball, sweetPotato, wood, radish };
        products.arraySize = list.Length;
        for (int i = 0; i < list.Length; i++)
            products.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        CollectionAssert.AreEqual(new[] { wood, radish, sweetPotato, ball }, catalog.Products.ToArray());
        Assert.IsNull(catalog.FindTab(wood), "재료는 탭 칩 없음");
    }

    private ShopProduct Product(string id, ItemCategory category, Currency currency, int sortOrder, int level)
    {
        var item = Make<ItemDefinition>();
        var itemSo = new SerializedObject(item);
        itemSo.FindProperty("_itemId").stringValue = id;
        itemSo.FindProperty("_category").objectReferenceValue = category;
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        var product = Make<ShopProduct>();
        var so = new SerializedObject(product);
        so.FindProperty("_productId").stringValue = "shop_" + id;
        so.FindProperty("_item").objectReferenceValue = item;
        so.FindProperty("_priceCurrency").objectReferenceValue = currency;
        so.FindProperty("_price").intValue = 100;
        so.FindProperty("_sortOrder").intValue = sortOrder;
        so.FindProperty("_requiredLevel").intValue = level;
        so.ApplyModifiedPropertiesWithoutUndo();
        return product;
    }

    private T Make<T>() where T : ScriptableObject
    {
        var obj = ScriptableObject.CreateInstance<T>();
        _created.Add(obj);
        return obj;
    }
}
