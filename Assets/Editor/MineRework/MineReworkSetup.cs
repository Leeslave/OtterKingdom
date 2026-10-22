using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 광산 개편 테스트 씬 만들기 (Docs/광산_개편_기획.md). 본 게임과 따로 도는 씬 하나 + 데이터 에셋.
/// 1) 그림 가져오기 (Tools/UIGen/mine_rework_art.py가 만든 Assets/Art/MineRework)
/// 2) 광석 · 층 · 곡괭이 · 설정 · 그림 묶음 에셋 (아래 표, 여러 번 실행해도 표대로 다시 씀)
/// 3) 씬 Assets/Scenes/MineRework.unity (카메라 · 배경 · 바위 칸 5 · 막힌 길 · 광부 리그 · 효과 · 소리 · MineReworkGame)
/// 메뉴: Tools/Mine Rework/Build Test Scene · 배치: -executeMethod MineReworkSetup.Build
/// </summary>
public static class MineReworkSetup
{
    private const string ArtFolder = "Assets/Art/MineRework";
    private const string DataFolder = "Assets/Scriptable Obejects/MineRework";
    private const string ScenePath = "Assets/Scenes/MineRework.unity";
    private const string Background = "Assets/Art/Mine/mine_background.png";
    private const float BackgroundPpu = 96f;
    private const float RockPpu = 230.76924f;

    // 광석: (id, 이름, 등급, 판매가, 아이콘, 광맥 그림, 색, 설명, 쓰임)
    private static readonly (string id, string name, MineOreRarity rarity, int price, string icon, string vein, Color color, string desc, string use)[] OreTable =
    {
        ("ore_stone", "돌", MineOreRarity.Common, 1, "Assets/Art/Item/Ore/Stone.png", null, new Color(0.78f, 0.74f, 0.7f),
            "어디서나 나오는 단단한 돌. 해달들이 집을 지을 때 가장 많이 써요.", "집 · 길 · 거의 모든 건물"),
        ("ore_shell_copper", "조개 구리", MineOreRarity.Common, 2, "Ores/ORE_copper", "Veins/VEIN_copper", new Color(0.91f, 0.55f, 0.32f),
            "조개껍데기 무늬가 새겨진 구리. 해달 대장장이가 제일 먼저 두드려 보는 금속이에요.", "조개 구리 곡괭이 · 마을회관 · 접수 창구"),
        ("ore_wave_iron", "물결 철", MineOreRarity.Uncommon, 4, "Ores/ORE_iron", "Veins/VEIN_iron", new Color(0.5f, 0.58f, 0.69f),
            "파도 무늬가 흐르는 단단한 철. 바닷바람에도 녹슬지 않아요.", "물결 철 곡괭이 · 마을 길드 · 선착장"),
        ("ore_moon_silver", "달빛 은", MineOreRarity.Rare, 10, "Ores/ORE_silver", "Veins/VEIN_silver", new Color(0.84f, 0.87f, 0.95f),
            "달빛을 머금어 밤에도 은은하게 빛나는 은.", "달빛 은 곡괭이 · 길드 지부"),
        ("ore_coral_crystal", "산호 수정", MineOreRarity.Rare, 14, "Ores/ORE_crystal", "Veins/VEIN_crystal", new Color(1f, 0.57f, 0.7f),
            "산호처럼 분홍빛으로 자란 수정. 톡 두드리면 맑은 소리가 나요.", "해님 금 곡괭이 · 기록관 · 수정 램프"),
        ("ore_sun_gold", "해님 금", MineOreRarity.Epic, 25, "Ores/ORE_gold", "Veins/VEIN_gold", new Color(0.97f, 0.76f, 0.23f),
            "해님 조각이 땅속에서 굳었다는 전설의 금.", "해님 금 곡괭이 · 왕국의 성"),
        ("ore_deep_pearl", "깊은 진주", MineOreRarity.Epic, 40, "Ores/ORE_pearl", "Veins/VEIN_pearl", new Color(0.94f, 0.92f, 1f),
            "광맥 깊숙이 잠든 조개가 품고 있던 진주. 바다와 땅이 만나는 곳에서만 나와요.", "꾸미기 · 수집"),
        ("ore_sea_ruby", "바다 루비", MineOreRarity.Epic, 50, "Ores/ORE_ruby", "Veins/VEIN_ruby", new Color(0.91f, 0.28f, 0.38f),
            "깊은 바다의 노을빛을 닮은 붉은 보석.", "별빛 다이아 곡괭이 · 꾸미기"),
        ("ore_star_diamond", "별빛 다이아", MineOreRarity.Legendary, 100, "Assets/Art/Item/Ore/Diamond.png", "Veins/VEIN_diamond", new Color(0.5f, 0.84f, 0.95f),
            "밤하늘의 별이 떨어져 굳었다는 다이아몬드. 용궁 보석방에서만 나와요.", "별빛 다이아 곡괭이 · 왕국 랜드마크"),
        ("ore_rainbow_shell", "무지개 조개석", MineOreRarity.Legendary, 0, "Ores/ORE_rainbow", "Veins/VEIN_rainbow", new Color(1f, 0.8f, 0.95f),
            "일곱 빛깔이 빙글빙글 도는 전설의 조개석. 요정이 무척 갖고 싶어 해요.", "요정과 바꾸기 (조개 · 뽑기권)"),
        ("relic_shell_fossil", "조개 화석", MineOreRarity.Relic, 0, "Relics/RELIC_shell_fossil", null, new Color(0.84f, 0.78f, 0.7f),
            "아주 먼 옛날 바다였던 이 땅의 흔적. 이 굴도 한때는 바닷속이었대요.", "도감 · 옛 왕국 이야기"),
        ("relic_paw_fossil", "옛 해달 발자국", MineOreRarity.Relic, 0, "Relics/RELIC_paw_fossil", null, new Color(0.8f, 0.74f, 0.68f),
            "돌에 찍힌 작은 발자국. 우리보다 먼저 이 굴을 판 해달이 있었나 봐요!", "도감 · 옛 왕국 이야기"),
        ("relic_old_pickaxe", "옛 해달 곡괭이", MineOreRarity.Relic, 0, "Relics/RELIC_old_pickaxe", null, new Color(0.7f, 0.55f, 0.42f),
            "닳고 닳은 자루에 '해달 왕국 첫 광부'라고 새겨져 있어요.", "도감 · 옛 왕국 이야기"),
        ("relic_crown_piece", "옛 왕관 조각", MineOreRarity.Relic, 0, "Relics/RELIC_crown_piece", null, new Color(0.97f, 0.78f, 0.3f),
            "금빛 왕관의 한 조각. 옛날에도 해달 왕국이 있었던 걸까요?", "도감 · 옛 왕국 이야기"),
        ("relic_palace_key", "용궁 열쇠", MineOreRarity.Relic, 0, "Relics/RELIC_palace_key", null, new Color(0.4f, 0.8f, 0.8f),
            "조개 모양 손잡이가 달린 열쇠. 용궁 보석방 깊은 곳의 문이 이 열쇠로 열린대요…", "도감 · 옛 왕국 이야기"),
    };

    // 층: (이름, 소개, 곡괭이 단계, 바위 체력, 광맥(id, 무게), 희귀(id, 무게), 희귀 확률, 화석, 화석 확률, 배경 색, 바위 색, 빛가루 색, 어둡기)
    private static readonly (string name, string sub, int tier, int hp, (string, int)[] veins, (string, int)[] rares, float rareChance,
        string relic, float relicChance, Color bg, Color rock, Color mote, float dark)[] LayerTable =
    {
        ("조약돌 굴", "작은 해달도 캘 수 있는 얕은 굴", 0, 4,
            new[] { ("ore_stone", 50), ("ore_shell_copper", 50) }, new[] { ("ore_wave_iron", 1) }, 0.04f,
            "relic_shell_fossil", 0.02f, Color.white, Color.white, new Color(1f, 0.95f, 0.8f, 0.7f), 0.12f),
        ("물결 갱도", "파도 소리가 들리는 축축한 갱도", 1, 8,
            new[] { ("ore_stone", 25), ("ore_shell_copper", 35), ("ore_wave_iron", 40) }, new[] { ("ore_moon_silver", 1) }, 0.06f,
            "relic_paw_fossil", 0.015f, new Color(0.8f, 0.88f, 1f), new Color(0.84f, 0.92f, 1f), new Color(0.7f, 0.88f, 1f, 0.75f), 0.3f),
        ("산호 수정 동굴", "분홍 수정이 반짝이는 동굴", 2, 12,
            new[] { ("ore_wave_iron", 35), ("ore_coral_crystal", 40), ("ore_moon_silver", 25) }, new[] { ("ore_sun_gold", 1) }, 0.05f,
            "relic_old_pickaxe", 0.015f, new Color(1f, 0.85f, 0.92f), new Color(1f, 0.9f, 0.95f), new Color(1f, 0.7f, 0.85f, 0.8f), 0.4f),
        ("진주 광맥", "땅속에 바다가 숨어 있는 신비한 광맥", 3, 18,
            new[] { ("ore_moon_silver", 40), ("ore_sun_gold", 30), ("ore_deep_pearl", 30) }, new[] { ("ore_sea_ruby", 1) }, 0.06f,
            "relic_crown_piece", 0.015f, new Color(0.78f, 0.82f, 1f), new Color(0.84f, 0.87f, 1f), new Color(0.95f, 0.95f, 1f, 0.8f), 0.5f),
        ("용궁 보석방", "전설 속 용궁의 보물 창고", 4, 26,
            new[] { ("ore_sun_gold", 35), ("ore_coral_crystal", 25), ("ore_deep_pearl", 20), ("ore_sea_ruby", 20) },
            new[] { ("ore_star_diamond", 5), ("ore_rainbow_shell", 1) }, 0.06f,
            "relic_palace_key", 0.012f, new Color(0.88f, 0.8f, 1f), new Color(0.95f, 0.88f, 1f), new Color(1f, 0.85f, 0.5f, 0.85f), 0.55f),
    };

    // 곡괭이: (이름, 소개, 아이콘, 재료(id, 개수), 골드, 힘, 광부, 희귀, 날 갈기 비용)
    private static readonly (string name, string desc, string icon, (string, int)[] recipe, int gold, int power, float miner, float rare, int[] sharpen)[] PickaxeTable =
    {
        ("조약돌 곡괭이", "해달이 처음 쥐는 곡괭이. 조약돌을 끈으로 꽁꽁 묶었어요.", "Pickaxes/PICK_0_pebble", new (string, int)[0], 0, 1, 0f, 0f, new[] { 60, 150 }),
        ("조개 구리 곡괭이", "조개 무늬가 새겨진 구리 날", "Pickaxes/PICK_1_copper", new[] { ("ore_shell_copper", 12) }, 150, 2, 0.1f, 0.01f, new[] { 200, 400 }),
        ("물결 철 곡괭이", "파도처럼 단단한 철 날", "Pickaxes/PICK_2_iron", new[] { ("ore_wave_iron", 15), ("ore_shell_copper", 10) }, 500, 3, 0.2f, 0.02f, new[] { 500, 1000 }),
        ("달빛 은 곡괭이", "밤에도 은은하게 빛나는 은 날", "Pickaxes/PICK_3_silver", new[] { ("ore_moon_silver", 10), ("ore_wave_iron", 20) }, 1500, 4, 0.3f, 0.03f, new[] { 1200, 2500 }),
        ("해님 금 곡괭이", "해님처럼 반짝이는 금 날", "Pickaxes/PICK_4_gold", new[] { ("ore_sun_gold", 10), ("ore_coral_crystal", 15) }, 4000, 6, 0.4f, 0.04f, new[] { 3000, 6000 }),
        ("별빛 다이아 곡괭이", "별빛 다이아로 만든 전설의 곡괭이", "Pickaxes/PICK_5_diamond", new[] { ("ore_star_diamond", 3), ("ore_sea_ruby", 5) }, 10000, 9, 0.5f, 0.06f, new[] { 8000, 15000 }),
    };

    private static readonly string[] LayerBackgrounds = { "BG_01_pebble", "BG_02_wave", "BG_03_coral", "BG_04_pearl", "BG_05_palace" };

    // 장면 자리 (배경 픽셀, 왼쪽 위 원점)
    private static readonly Vector2[] RockSpots = { new Vector2(300, 690), new Vector2(600, 745), new Vector2(330, 925), new Vector2(640, 985), new Vector2(470, 1135) };
    private const float RockScale = 1.35f;
    private static readonly Vector2 BoulderSpot = new Vector2(500, 960);
    private static readonly Vector2 MinerSpot = new Vector2(770, 625);

    [MenuItem("Tools/Mine Rework/Build Test Scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        ImportArt();
        var (config, art) = BuildData();
        BuildScene(config, art);
        Debug.Log($"[MineReworkSetup] 광산 개편 테스트 씬 만듦: {ScenePath}");
    }

    #region 그림

    private static void ImportArt()
    {
        // 아이콘은 그림 폭 = 1유닛 (임시 256px · GPT 1024px 어느 쪽이든 같은 크기)
        foreach (var dir in new[] { "Ores", "Relics", "Pickaxes", "Ui" })
        {
            foreach (var path in Directory.GetFiles(Path.Combine(ArtFolder, dir), "*.png"))
                ImportSprite(path.Replace('\\', '/'), -1f, new Vector2(0.5f, 0.5f));
        }
        string backgrounds = Path.Combine(ArtFolder, "Backgrounds");
        if (Directory.Exists(backgrounds))
        {
            foreach (var path in Directory.GetFiles(backgrounds, "*.png"))
                ImportSprite(path.Replace('\\', '/'), BackgroundPpu, new Vector2(0.5f, 0.5f), 2048);
        }
        // 광산 바위 (GPT 그림이 있으면): 보이는 폭 = 2칸, 발밑 = 보이는 아래쪽. 광맥은 바위와 같은 판 · 같은 설정
        var (rockPpu, rockPivot) = (RockPpu, new Vector2(0.5f, 0.1f));
        string rock0 = $"{ArtFolder}/Rocks/ROCK_0.png";
        if (File.Exists(rock0))
        {
            (rockPpu, rockPivot) = RockImport(rock0);
            foreach (var path in Directory.GetFiles(Path.Combine(ArtFolder, "Rocks"), "*.png"))
                ImportSprite(path.Replace('\\', '/'), rockPpu, rockPivot, 2048);
        }
        foreach (var path in Directory.GetFiles(Path.Combine(ArtFolder, "Veins"), "*.png"))
            ImportSprite(path.Replace('\\', '/'), rockPpu, rockPivot, 2048);
        ImportSprite($"{ArtFolder}/Fx/FX_Glow.png", 256f, new Vector2(0.5f, 0.5f));
        ImportSprite($"{ArtFolder}/Fx/FX_Spark.png", 128f, new Vector2(0.5f, 0.5f));
        ImportSprite($"{ArtFolder}/Fx/FX_Ring.png", 256f, new Vector2(0.5f, 0.5f));
        ImportSprite($"{ArtFolder}/Fx/FX_Mote.png", 64f, new Vector2(0.5f, 0.5f));
        ImportSprite($"{ArtFolder}/Fx/FX_Vignette.png", 512f, new Vector2(0.5f, 0.5f));
    }

    // 바위 그림의 보이는 상자 → 보이는 폭이 2칸이 되는 PPU, 발밑(보이는 아래쪽보다 조금 위) 피벗
    private static (float ppu, Vector2 pivot) RockImport(string path)
    {
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes(path));
        int minX = texture.width, maxX = 0, minY = texture.height, maxY = 0;
        var pixels = texture.GetPixels32();
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a <= 40)
                    continue;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }
        float ppu = Mathf.Max(1, maxX - minX) / 2f;
        // 텍스처 y는 아래부터: 보이는 아래쪽에서 높이의 6% 위
        float pivotY = (minY + (maxY - minY) * 0.06f) / texture.height;
        float pivotX = (minX + maxX) * 0.5f / texture.width;
        Object.DestroyImmediate(texture);
        return (ppu, new Vector2(pivotX, pivotY));
    }

    // ppu < 0 = 그림 폭 (1유닛)
    private static void ImportSprite(string path, float ppu, Vector2 pivot, int maxSize = 1024)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
            return;
        if (ppu < 0f)
        {
            importer.GetSourceTextureWidthAndHeight(out int width, out _);
            ppu = width;
        }
        importer.maxTextureSize = maxSize;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = ppu;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static Sprite Art(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        string full = path.StartsWith("Assets/") ? path : $"{ArtFolder}/{path}.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(full);
        if (sprite == null)
            Debug.LogWarning($"[MineReworkSetup] 그림이 없습니다: {full}");
        return sprite;
    }

    #endregion

    #region 데이터

    private static (MineReworkConfig, MineReworkArt) BuildData()
    {
        Directory.CreateDirectory(DataFolder);
        var ores = new Dictionary<string, MineOreDefinition>();
        var oreList = new List<MineOreDefinition>();
        foreach (var row in OreTable)
        {
            var ore = Asset<MineOreDefinition>($"{DataFolder}/Ores/Ore_{row.id}.asset");
            var so = new SerializedObject(ore);
            so.FindProperty("_oreId").stringValue = row.id;
            so.FindProperty("_displayName").stringValue = row.name;
            so.FindProperty("_description").stringValue = row.desc;
            so.FindProperty("_useText").stringValue = row.use;
            so.FindProperty("_icon").objectReferenceValue = Art(row.icon);
            so.FindProperty("_veinSprite").objectReferenceValue = Art(row.vein);
            so.FindProperty("_color").colorValue = row.color;
            so.FindProperty("_rarity").enumValueIndex = (int)row.rarity;
            so.FindProperty("_sellPrice").intValue = row.price;
            so.ApplyModifiedPropertiesWithoutUndo();
            ores[row.id] = ore;
            oreList.Add(ore);
        }

        var layers = new List<MineLayerDefinition>();
        for (int i = 0; i < LayerTable.Length; i++)
        {
            var row = LayerTable[i];
            var layer = Asset<MineLayerDefinition>($"{DataFolder}/Layers/Layer_{i + 1}.asset");
            var so = new SerializedObject(layer);
            so.FindProperty("_displayName").stringValue = row.name;
            so.FindProperty("_subtitle").stringValue = row.sub;
            so.FindProperty("_requiredPickaxeTier").intValue = row.tier;
            so.FindProperty("_rockHp").intValue = row.hp;
            SetDrops(so.FindProperty("_veins"), row.veins, ores);
            SetDrops(so.FindProperty("_rareDrops"), row.rares, ores);
            so.FindProperty("_rareChance").floatValue = row.rareChance;
            so.FindProperty("_relic").objectReferenceValue = ores[row.relic];
            so.FindProperty("_relicChance").floatValue = row.relicChance;
            var background = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/Backgrounds/{LayerBackgrounds[i]}.png");
            so.FindProperty("_background").objectReferenceValue = background;
            // 층 배경 그림이 있으면 색은 곱하지 않음 (그림이 이미 그 층 색)
            so.FindProperty("_backgroundTint").colorValue = background != null ? Color.white : row.bg;
            so.FindProperty("_rockTint").colorValue = row.rock;
            so.FindProperty("_moteColor").colorValue = row.mote;
            so.FindProperty("_darkness").floatValue = row.dark;
            so.ApplyModifiedPropertiesWithoutUndo();
            layers.Add(layer);
        }

        var pickaxes = new List<MinePickaxeDefinition>();
        for (int i = 0; i < PickaxeTable.Length; i++)
        {
            var row = PickaxeTable[i];
            var pickaxe = Asset<MinePickaxeDefinition>($"{DataFolder}/Pickaxes/Pickaxe_{i}.asset");
            var so = new SerializedObject(pickaxe);
            so.FindProperty("_displayName").stringValue = row.name;
            so.FindProperty("_description").stringValue = row.desc;
            so.FindProperty("_icon").objectReferenceValue = Art(row.icon);
            var recipe = so.FindProperty("_recipe");
            recipe.arraySize = row.recipe.Length;
            for (int k = 0; k < row.recipe.Length; k++)
            {
                var e = recipe.GetArrayElementAtIndex(k);
                e.FindPropertyRelative("_ore").objectReferenceValue = ores[row.recipe[k].Item1];
                e.FindPropertyRelative("_amount").intValue = row.recipe[k].Item2;
            }
            so.FindProperty("_goldCost").intValue = row.gold;
            so.FindProperty("_power").intValue = row.power;
            so.FindProperty("_minerSpeedBonus").floatValue = row.miner;
            so.FindProperty("_rareBonus").floatValue = row.rare;
            var sharpen = so.FindProperty("_sharpenCosts");
            sharpen.arraySize = row.sharpen.Length;
            for (int k = 0; k < row.sharpen.Length; k++)
                sharpen.GetArrayElementAtIndex(k).intValue = row.sharpen[k];
            so.ApplyModifiedPropertiesWithoutUndo();
            pickaxes.Add(pickaxe);
        }

        var config = Asset<MineReworkConfig>($"{DataFolder}/MineReworkConfig.asset");
        var cso = new SerializedObject(config);
        SetList(cso.FindProperty("_ores"), oreList);
        SetList(cso.FindProperty("_layers"), layers);
        SetList(cso.FindProperty("_pickaxes"), pickaxes);
        cso.FindProperty("_stone").objectReferenceValue = ores["ore_stone"];
        cso.ApplyModifiedPropertiesWithoutUndo();

        var art = Asset<MineReworkArt>($"{DataFolder}/MineReworkArt.asset");
        var aso = new SerializedObject(art);
        var stages = aso.FindProperty("_rockStages");
        stages.arraySize = 3;
        // 광산 바위 그림(Rocks/ROCK_0~2 · ROCK_rubble)이 있으면 그것, 없으면 광장 바위
        bool custom = File.Exists($"{ArtFolder}/Rocks/ROCK_0.png");
        for (int k = 0; k < 3; k++)
            stages.GetArrayElementAtIndex(k).objectReferenceValue = Art(custom && File.Exists($"{ArtFolder}/Rocks/ROCK_{k}.png")
                ? $"Rocks/ROCK_{k}" : $"Assets/Art/Settlement/Prop_Rock_{k}.png");
        Set(aso, "_rubble", Art(custom && File.Exists($"{ArtFolder}/Rocks/ROCK_rubble.png")
            ? "Rocks/ROCK_rubble" : "Assets/Art/Settlement/Prop_Rock_Rubble.png"));
        Set(aso, "_chip", Art("Assets/Resources/MinerRigV1/stone_chip.png"));
        Set(aso, "_glow", Art("Fx/FX_Glow"));
        Set(aso, "_spark", Art("Fx/FX_Spark"));
        Set(aso, "_ring", Art("Fx/FX_Ring"));
        Set(aso, "_mote", Art("Fx/FX_Mote"));
        Set(aso, "_vignette", Art("Fx/FX_Vignette"));
        Set(aso, "_rays", Art("Assets/Resources/UI/Celebrate/Rays.png"));
        Set(aso, "_roundCream", Art("Assets/Art/UI/Common/UI_RoundButton_Cream.png"));
        Set(aso, "_roundFeatured", Art("Assets/Art/UI/Common/UI_RoundButton_Featured.png"));
        Set(aso, "_roundPeach", Art("Assets/Art/UI/Common/UI_RoundButton_Peach.png"));
        Set(aso, "_chipNormal", Art("Assets/Art/UI/Common/UI_Chip_Normal.png"));
        Set(aso, "_badge", Art("Assets/Art/UI/Common/UI_Badge.png"));
        Set(aso, "_boxInset", Art("Assets/Art/UI/Common/UI_Box_Inset.png"));
        Set(aso, "_barFill", Art("Assets/Art/UI/Common/UI_Bar_Fill.png"));
        Set(aso, "_ribbonPeach", Art("Assets/Art/UI/Common/UI_Ribbon_Peach.png"));
        Set(aso, "_ribbonLav", Art("Assets/Art/UI/Common/UI_Ribbon_Lav.png"));
        Set(aso, "_tagCommon", Art("Assets/Art/UI/Common/UI_Tag_Rarity_Common.png"));
        Set(aso, "_tagRare", Art("Assets/Art/UI/Common/UI_Tag_Rarity_Rare.png"));
        Set(aso, "_tagEpic", Art("Assets/Art/UI/Common/UI_Tag_Rarity_Epic.png"));
        Set(aso, "_paper", Art("Assets/Art/UI/Common/UI_Button_Paper.png"));
        Set(aso, "_gold", Art("Assets/Art/UI/Currency/Gold.png"));
        Set(aso, "_iconWorkbench", Art("Ui/ICON_Workbench"));
        Set(aso, "_iconLayers", Art("Assets/Art/UI/Travel/ICON_Place_Mine.png"));
        Set(aso, "_iconCollection", Art("Assets/Art/UI/Nav/ICON_Nav_Collection.png"));
        aso.ApplyModifiedPropertiesWithoutUndo();

        foreach (var o in oreList) EditorUtility.SetDirty(o);
        foreach (var l in layers) EditorUtility.SetDirty(l);
        foreach (var p in pickaxes) EditorUtility.SetDirty(p);
        EditorUtility.SetDirty(config);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        return (config, art);
    }

    private static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void SetDrops(SerializedProperty list, (string, int)[] rows, Dictionary<string, MineOreDefinition> ores)
    {
        list.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("_ore").objectReferenceValue = ores[rows[i].Item1];
            e.FindPropertyRelative("_weight").intValue = rows[i].Item2;
        }
    }

    private static void SetList<T>(SerializedProperty list, List<T> items) where T : Object
    {
        list.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;

    #endregion

    #region 씬

    private static Vector3 World(Vector2 px) => new Vector3((px.x - 512f) / BackgroundPpu, (768f - px.y) / BackgroundPpu, 0f);

    private static void BuildScene(MineReworkConfig config, MineReworkArt art)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬을 열면 메모리의 에셋이 내려가므로 경로로 다시 불러옴
        config = AssetDatabase.LoadAssetAtPath<MineReworkConfig>($"{DataFolder}/MineReworkConfig.asset");
        art = AssetDatabase.LoadAssetAtPath<MineReworkArt>($"{DataFolder}/MineReworkArt.asset");

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.12f, 0.1f);
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        cameraGo.AddComponent<AudioListener>();

        var bg = new GameObject("Background").AddComponent<SpriteRenderer>();
        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Background);
        bg.sortingOrder = -100;

        var vignette = new GameObject("Vignette").AddComponent<SpriteRenderer>();
        vignette.sprite = art.Vignette;
        vignette.sortingOrder = 880;
        vignette.color = new Color(0f, 0f, 0f, 0.1f);

        var rocks = new GameObject("Rocks").transform;
        var slots = new List<Transform>();
        for (int i = 0; i < RockSpots.Length; i++)
        {
            var slot = new GameObject($"Rock{i + 1}").transform;
            slot.SetParent(rocks, false);
            slot.position = World(RockSpots[i]);
            slot.localScale = Vector3.one * RockScale;
            slot.gameObject.AddComponent<VeinRockView>();
            slots.Add(slot);
        }
        var boulder = new GameObject("Boulder").transform;
        boulder.position = World(BoulderSpot);
        boulder.localScale = Vector3.one * 2.3f;
        boulder.gameObject.AddComponent<VeinRockView>();
        boulder.gameObject.SetActive(false);

        var miner = new GameObject("Miner");
        miner.transform.position = World(MinerSpot);
        var rig = miner.AddComponent<QuarterRigView>();
        var rso = new SerializedObject(rig);
        rso.FindProperty("resourceFolder").stringValue = "MinerRigV1";
        rso.FindProperty("characterScale").floatValue = 0.56f;
        rso.FindProperty("startFacingLeft").boolValue = true;
        rso.FindProperty("sortingOrder").intValue = 500 - Mathf.RoundToInt(miner.transform.position.y * 20f);
        rso.ApplyModifiedPropertiesWithoutUndo();

        var fx = new GameObject("Fx").AddComponent<MineFx>();
        var sfx = new GameObject("Sfx").AddComponent<MineSfx>();

        var game = new GameObject("MineReworkGame").AddComponent<MineReworkGame>();
        var so = new SerializedObject(game);
        so.FindProperty("_config").objectReferenceValue = config;
        so.FindProperty("_art").objectReferenceValue = art;
        so.FindProperty("_camera").objectReferenceValue = camera;
        so.FindProperty("_background").objectReferenceValue = bg;
        so.FindProperty("_vignette").objectReferenceValue = vignette;
        SetList(so.FindProperty("_rockSlots"), slots);
        so.FindProperty("_boulderSpot").objectReferenceValue = boulder;
        so.FindProperty("_miner").objectReferenceValue = rig;
        so.FindProperty("_fx").objectReferenceValue = fx;
        so.FindProperty("_sfx").objectReferenceValue = sfx;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    #endregion
}
