using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>광산 개편 규칙 (MineReworkModel): 바위 · 콤보 · 치명타 · 반짝 광맥 · 대박 · 다시 생김 · 곡괭이 · 막힌 길 · 화석 · 저장 · 꺼 둔 동안</summary>
public class MineReworkTests
{
    private readonly List<Object> _created = new List<Object>();
    private MineOreDefinition _stone, _copper, _iron, _silver, _fossil;
    private MineLayerDefinition _layer1, _layer2;
    private MinePickaxeDefinition _pick0, _pick1;
    private MineReworkConfig _config;

    [SetUp]
    public void SetUp()
    {
        _stone = Ore("ore_stone", MineOreRarity.Common, 1);
        _copper = Ore("ore_copper", MineOreRarity.Common, 6);
        _iron = Ore("ore_iron", MineOreRarity.Uncommon, 12);
        _silver = Ore("ore_silver", MineOreRarity.Rare, 30);
        _fossil = Ore("relic_fossil", MineOreRarity.Relic, 0);
        _layer1 = Layer(0, 4, new[] { (_copper, 1) }, new[] { (_silver, 1) }, 0f, _fossil, 0f);
        _layer2 = Layer(1, 8, new[] { (_iron, 1) }, new (MineOreDefinition, int)[0], 0f, null, 0f);
        _pick0 = Pickaxe(1, 0, new (MineOreDefinition, int)[0]);
        _pick1 = Pickaxe(2, 100, new[] { (_copper, 5) });
        _config = Config(crit: 0f, shiny: 0f, jackpot: 0f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in _created)
            Object.DestroyImmediate(o);
        _created.Clear();
    }

    #region 준비

    private T Track<T>(T o) where T : Object
    {
        _created.Add(o);
        return o;
    }

    private MineOreDefinition Ore(string id, MineOreRarity rarity, int price)
    {
        var ore = Track(ScriptableObject.CreateInstance<MineOreDefinition>());
        var so = new SerializedObject(ore);
        so.FindProperty("_oreId").stringValue = id;
        so.FindProperty("_displayName").stringValue = id;
        so.FindProperty("_rarity").enumValueIndex = (int)rarity;
        so.FindProperty("_sellPrice").intValue = price;
        so.ApplyModifiedPropertiesWithoutUndo();
        return ore;
    }

    private MineLayerDefinition Layer(int tier, int hp, (MineOreDefinition, int)[] veins, (MineOreDefinition, int)[] rares, float rareChance,
        MineOreDefinition relic, float relicChance)
    {
        var layer = Track(ScriptableObject.CreateInstance<MineLayerDefinition>());
        var so = new SerializedObject(layer);
        so.FindProperty("_requiredPickaxeTier").intValue = tier;
        so.FindProperty("_rockHp").intValue = hp;
        Drops(so.FindProperty("_veins"), veins);
        Drops(so.FindProperty("_rareDrops"), rares ?? new (MineOreDefinition, int)[0]);
        so.FindProperty("_rareChance").floatValue = rareChance;
        so.FindProperty("_relic").objectReferenceValue = relic;
        so.FindProperty("_relicChance").floatValue = relicChance;
        so.ApplyModifiedPropertiesWithoutUndo();
        return layer;
    }

    private static void Drops(SerializedProperty list, (MineOreDefinition, int)[] rows)
    {
        list.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            list.GetArrayElementAtIndex(i).FindPropertyRelative("_ore").objectReferenceValue = rows[i].Item1;
            list.GetArrayElementAtIndex(i).FindPropertyRelative("_weight").intValue = rows[i].Item2;
        }
    }

    private MinePickaxeDefinition Pickaxe(int power, int gold, (MineOreDefinition, int)[] recipe)
    {
        var pickaxe = Track(ScriptableObject.CreateInstance<MinePickaxeDefinition>());
        var so = new SerializedObject(pickaxe);
        so.FindProperty("_power").intValue = power;
        so.FindProperty("_goldCost").intValue = gold;
        var list = so.FindProperty("_recipe");
        list.arraySize = recipe.Length;
        for (int i = 0; i < recipe.Length; i++)
        {
            list.GetArrayElementAtIndex(i).FindPropertyRelative("_ore").objectReferenceValue = recipe[i].Item1;
            list.GetArrayElementAtIndex(i).FindPropertyRelative("_amount").intValue = recipe[i].Item2;
        }
        var sharpen = so.FindProperty("_sharpenCosts");
        sharpen.arraySize = 1;
        sharpen.GetArrayElementAtIndex(0).intValue = 50;
        so.ApplyModifiedPropertiesWithoutUndo();
        return pickaxe;
    }

    private MineReworkConfig Config(float crit, float shiny, float jackpot, int slots = 2)
    {
        var config = Track(ScriptableObject.CreateInstance<MineReworkConfig>());
        var so = new SerializedObject(config);
        Set(so.FindProperty("_ores"), new Object[] { _stone, _copper, _iron, _silver, _fossil });
        Set(so.FindProperty("_layers"), new Object[] { _layer1, _layer2 });
        Set(so.FindProperty("_pickaxes"), new Object[] { _pick0, _pick1 });
        so.FindProperty("_stone").objectReferenceValue = _stone;
        so.FindProperty("_rockSlots").intValue = slots;
        so.FindProperty("_rockRespawnSeconds").floatValue = 10f;
        so.FindProperty("_veinYield").vector2IntValue = new Vector2Int(2, 2);
        so.FindProperty("_stoneYield").vector2IntValue = new Vector2Int(3, 3);
        so.FindProperty("_shinyChance").floatValue = shiny;
        so.FindProperty("_boulderHits").intValue = 3;
        so.FindProperty("_comboWindow").floatValue = 1f;
        so.FindProperty("_comboBonusEvery").intValue = 100;
        so.FindProperty("_baseCritChance").floatValue = crit;
        so.FindProperty("_critPerFiveCombo").floatValue = 0f;
        so.FindProperty("_jackpotChanceOnShiny").floatValue = jackpot;
        so.FindProperty("_jackpotChancePerRock").floatValue = jackpot;
        so.FindProperty("_jackpotSeconds").floatValue = 5f;
        so.FindProperty("_minerSeconds").floatValue = 4f;
        so.FindProperty("_offlineSlowdown").floatValue = 2f;
        so.FindProperty("_offlineMaxFinds").intValue = 100;
        so.FindProperty("_startGold").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        return config;
    }

    private static void Set(SerializedProperty list, Object[] items)
    {
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private MineReworkModel Model(MineReworkConfig config = null, MineReworkSave save = null) =>
        new MineReworkModel(config ?? _config, save ?? new MineReworkSave(), new System.Random(3));

    private static int Gained(MineHit hit, MineOreDefinition ore)
    {
        int n = 0;
        foreach (var gain in hit.Gains)
        {
            if (gain.Ore == ore)
                n += gain.Amount;
        }
        return n;
    }

    #endregion

    [Test]
    public void Hit_BreaksWhenHpRunsOut_AndGivesVeinOres()
    {
        var model = Model();
        Assert.IsTrue(model.Rocks[0].Present, "처음엔 바위가 가득");
        Assert.AreEqual(4, model.HitsToBreak(model.Rocks[0]));

        MineHit hit = null;
        for (int i = 0; i < 3; i++)
        {
            hit = model.Hit(0, i * 10f);
            Assert.IsFalse(hit.Broken);
        }
        hit = model.Hit(0, 40f);

        Assert.IsTrue(hit.Broken);
        Assert.IsFalse(model.Rocks[0].Present);
        Assert.AreEqual(2, Gained(hit, _copper));
        Assert.AreEqual(2, model.Count(_copper));
        Assert.IsTrue(model.IsDiscovered(_copper));
        Assert.IsNull(model.Hit(0, 50f), "빈 칸은 칠 수 없음");
    }

    [Test]
    public void Combo_ContinuesInsideWindow_AndCritDoublesDamage()
    {
        var model = Model(Config(crit: 1f, shiny: 0f, jackpot: 0f));
        var a = model.Hit(0, 0f);
        var b = model.Hit(1, 0.5f);
        Assert.AreEqual(1, a.Combo);
        Assert.AreEqual(2, b.Combo);
        Assert.IsTrue(a.Crit);
        Assert.AreEqual(2, a.Damage, "치명타는 두 배");
        var c = model.Hit(1, 5f);
        Assert.AreEqual(1, c.Combo, "시간이 지나면 콤보가 끊김");
    }

    [Test]
    public void ShinyRock_GivesRareAndMoreHp()
    {
        var model = Model(Config(crit: 0f, shiny: 1f, jackpot: 0f));
        var rock = model.Rocks[0];
        Assert.IsTrue(rock.Shiny);
        Assert.Greater(rock.MaxHp, _layer1.RockHp);
        MineHit hit = null;
        while (hit == null || !hit.Broken)
            hit = model.Hit(0, 0f);
        Assert.AreEqual(1, Gained(hit, _silver), "반짝 광맥은 희귀가 반드시");
        Assert.AreEqual(3, Gained(hit, _copper), "광맥 2 + 반짝 1");
    }

    [Test]
    public void Jackpot_RefillsBrokenRockAndDoublesOres()
    {
        var model = Model(Config(crit: 0f, shiny: 0f, jackpot: 1f));
        MineHit hit = null;
        while (hit == null || !hit.Broken)
            hit = model.Hit(0, 0f);
        Assert.IsTrue(hit.JackpotStarted);
        Assert.IsTrue(model.JackpotActive);
        Assert.IsTrue(model.Rocks[0].Present, "대박 중엔 바로 다시 차오름");

        hit = null;
        while (hit == null || !hit.Broken)
            hit = model.Hit(0, 0f);
        Assert.AreEqual(4, Gained(hit, _copper), "대박 중엔 두 배");

        model.Tick(6f);
        Assert.IsFalse(model.JackpotActive);
    }

    [Test]
    public void BrokenRock_ComesBackAfterRespawnTime()
    {
        var model = Model();
        while (model.Rocks[0].Present)
            model.Hit(0, 0f);
        model.Tick(9f);
        Assert.IsFalse(model.Rocks[0].Present);
        model.Tick(1.5f);
        Assert.IsTrue(model.Rocks[0].Present);
    }

    [Test]
    public void Craft_UsesOresAndGold_ThenBoulderOpensNextLayer()
    {
        var model = Model();
        Assert.IsFalse(model.CanCraftNext());
        model.CheatAdd(_copper, 7);
        model.CheatGold(150);
        Assert.AreEqual(-1, model.BlockedLayer, "곡괭이 전엔 막힌 길도 없음");

        Assert.IsTrue(model.TryCraftNext());
        Assert.AreEqual(1, model.PickaxeTier);
        Assert.AreEqual(2, model.Count(_copper));
        Assert.AreEqual(50, model.Gold);
        Assert.AreEqual(1, model.BlockedLayer);
        Assert.IsFalse(model.IsLayerOpen(1));

        MineHit hit = null;
        for (int i = 0; i < 3; i++)
            hit = model.HitBoulder(i * 10f);
        Assert.IsTrue(hit.LayerOpened);
        Assert.IsTrue(model.IsLayerOpen(1));
        Assert.AreEqual(1, model.LayerIndex, "열린 층으로 감");
        Assert.AreEqual(_iron, model.Rocks[0].Vein, "새 층 바위");
        Assert.IsTrue(model.TryTravel(0));
        Assert.AreEqual(_copper, model.Rocks[0].Vein);
    }

    [Test]
    public void Relic_DropsOnlyOnce()
    {
        _layer1 = Layer(0, 1, new[] { (_copper, 1) }, null, 0f, _fossil, 1f);
        var config = Config(crit: 0f, shiny: 0f, jackpot: 0f);
        var model = Model(config);
        var first = model.Hit(0, 0f);
        var second = model.Hit(1, 0f);
        Assert.AreEqual(1, Gained(first, _fossil));
        Assert.AreEqual(0, Gained(second, _fossil), "화석은 한 번만");
    }

    [Test]
    public void Sharpen_RaisesCrit_AndSellGivesGold()
    {
        var model = Model();
        model.CheatGold(60);
        float before = model.CritChance;
        Assert.IsTrue(model.TrySharpen());
        Assert.Greater(model.CritChance, before);
        Assert.AreEqual(10, model.Gold);
        Assert.IsFalse(model.TrySharpen(), "날 갈기는 정해진 횟수까지");

        model.CheatAdd(_copper, 5);
        Assert.AreEqual(18, model.Sell(_copper, 3));
        Assert.AreEqual(2, model.Count(_copper));
        Assert.AreEqual(0, model.Sell(_fossil, 1), "화석은 못 팖");
    }

    [Test]
    public void Save_RoundTrips()
    {
        var model = Model();
        model.CheatAdd(_copper, 4);
        model.CheatGold(77);
        model.Hit(0, 0f);
        var save = model.ToSave(System.DateTime.UtcNow);

        var loaded = Model(save: save);
        Assert.AreEqual(4, loaded.Count(_copper));
        Assert.AreEqual(77, loaded.Gold);
        Assert.AreEqual(model.Rocks[0].Hp, loaded.Rocks[0].Hp);
        Assert.AreEqual(model.Rocks[0].Vein, loaded.Rocks[0].Vein);
        Assert.IsTrue(loaded.IsDiscovered(_copper));
    }

    [Test]
    public void Offline_RefillsRocksAndMinerDigsSlower()
    {
        var model = Model();
        while (model.Rocks[0].Present)
            model.Hit(0, 0f);
        while (model.Rocks[1].Present)
            model.Hit(1, 0f);
        int before = model.Count(_copper);

        // 광부 4초 × 2배 느림 = 8초마다 → 80초면 10개
        var gains = model.ApplyOffline(80);
        Assert.IsTrue(model.Rocks[0].Present && model.Rocks[1].Present);
        int dug = 0;
        foreach (var gain in gains)
            dug += gain.Amount;
        Assert.AreEqual(10, dug);
        Assert.AreEqual(before + 10, model.Count(_copper));
    }

    [Test]
    public void Miner_DigsEveryInterval_FasterWhenCheered()
    {
        var model = Model();
        int found = 0;
        model.MinerFound += _ => found++;
        model.Tick(4.1f);
        Assert.AreEqual(1, found);
        Assert.IsTrue(model.TryCheer());
        Assert.IsFalse(model.TryCheer(), "쉬어야 다시 응원");
        model.Tick(2.1f);
        Assert.AreEqual(2, found, "응원 중엔 두 배");
    }
}
