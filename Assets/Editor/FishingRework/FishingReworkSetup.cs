using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 낚시 개편 (Docs/낚시_개편_기획.md): 그림 가져오기 → 물고기 · 가방 아이템 · 도감 항목 · 설정 · 그림 묶음 에셋 → 씬.
/// 물고기 표는 본 게임과 테스트 씬이 같이 씀 (본 게임에 원래 있던 물고기 10종은 원래 아이템 · 도감 항목을 그대로 씀).
/// 그림 다시 그리기: python Tools/UIGen/fishing_rework_art.py
/// 메뉴: Tools/Fishing Rework/Build Test Scene · Apply To Main Game (FishingReworkSetup.Main.cs)
/// </summary>
public static partial class FishingReworkSetup
{
    private const string ArtFolder = "Assets/Art/FishingRework";
    private const string DataFolder = "Assets/Scriptable Obejects/FishingRework";
    private const string ScenePath = "Assets/Scenes/FishingRework.unity";
    private const string MineArtPath = "Assets/Scriptable Obejects/MineRework/MineReworkArt.asset";
    private const string ItemFolder = "Assets/Scriptable Obejects/Inventory/Items/Fishing";
    private const string FishCategoryPath = "Assets/Scriptable Obejects/Inventory/Category/Fish.asset";
    private const string RarityFolder = "Assets/Scriptable Obejects/Inventory/Rarity";
    private const string PierIconPath = "Assets/Art/UI/Travel/ICON_Place_FishingSpot.png";

    // 물고기 표 (임시 수치). id = 가방 아이템 id. existing = 원래 있던 아이템(Items/Fishing/<이름>.asset) — 이름 · 값 · 그림은 그 아이템 것.
    // art = 새 물고기 그림 (Art/FishingRework/Fish/FISH_<art>.png). 거리 · 깊이 m, 무게 kg, 바닥 물고기의 깊이 = 바닥에서 높이.
    // 길이(마지막 칸)는 화면 크기 (휴대폰에서 잘 보이게 실제보다 큼, 바늘에 닿는 범위도 이걸로). 값은 본 게임 골드 규모
    private static readonly (string id, string existing, string art, string name, string desc, FishRarity rarity, int price,
        float minW, float maxW, Vector2 dist, Vector2 depth, bool bottom, float spawn, float speed, float length)[] FishTable =
    {
        // 구간 1 (선착장 앞 ~12m · 깊이 3~7.5m · 줄 1단계)
        ("fish_anchovy", null, "anchovy", "꼬마 멸치", "선착장 앞에 떼 지어 다녀요. 작지만 늘 반가운 첫 손님.",
            FishRarity.Common, 15, 0.03f, 0.08f, new Vector2(3f, 16f), new Vector2(3f, 5f), false, 55f, 1.3f, 0.7f),
        ("fish_sardine", "정어리", null, "", "반짝이는 은빛 비늘. 멸치보다 조금 깊이 다녀요.",
            FishRarity.Common, 0, 0.1f, 0.2f, new Vector2(3f, 20f), new Vector2(4f, 7f), false, 50f, 1.2f, 0.85f),
        ("fish_shrimp", null, "shrimp", "보리새우", "바닥 가까이에서 톡톡 튀어 다녀요.",
            FishRarity.Common, 28, 0.05f, 0.12f, new Vector2(2f, 18f), new Vector2(5f, 7.5f), false, 40f, 0.7f, 0.65f),
        ("fish_clownfish", null, "clownfish", "흰동가리", "주황 몸에 하얀 줄무늬. 산호 근처를 좋아해요.",
            FishRarity.Uncommon, 50, 0.1f, 0.25f, new Vector2(4f, 20f), new Vector2(3.5f, 6f), false, 28f, 0.8f, 0.75f),
        ("fish_goldfish", null, "goldfish", "금붕어", "멸치 떼 사이에서 아주 가끔 반짝여요. 작아도 값이 꽤 나가요.",
            FishRarity.Rare, 180, 0.1f, 0.3f, new Vector2(4f, 30f), new Vector2(3f, 5f), false, 10f, 0.9f, 0.8f),
        ("fish_mackerel", "고등어", null, "", "등에 파도 무늬가 있는 바다의 단골. 해달 식탁의 인기 메뉴.",
            FishRarity.Common, 0, 0.3f, 0.9f, new Vector2(6f, 60f), new Vector2(4f, 8f), false, 45f, 1.0f, 1.15f),
        // 구간 2 (~25m · ~14m · 바닥이 열림)
        ("fish_crab", null, "crab", "꽃게", "바닥을 옆으로 걸어 다녀요. 집게 조심!",
            FishRarity.Common, 35, 0.2f, 0.6f, new Vector2(2f, 26f), new Vector2(0.2f, 0.8f), true, 35f, 0.4f, 0.9f),
        ("fish_moon_jelly", null, "jelly", "달 해파리", "둥실둥실 떠다니는 해파리. 값은 별로지만 자꾸 걸려요.",
            FishRarity.Common, 12, 0.1f, 0.3f, new Vector2(10f, 70f), new Vector2(3f, 10f), false, 40f, 0.3f, 0.9f),
        ("fish_saury", "꽁치", null, "", "가늘고 긴 은빛 물고기. 얕은 물을 빠르게 지나가요.",
            FishRarity.Common, 0, 0.1f, 0.3f, new Vector2(14f, 90f), new Vector2(3f, 6f), false, 35f, 1.4f, 1.1f),
        ("fish_horse_mackerel", "전갱이", null, "", "고등어의 사촌. 조금 더 깊은 데서 무리 지어 다녀요.",
            FishRarity.Common, 0, 0.2f, 0.6f, new Vector2(10f, 60f), new Vector2(6f, 11f), false, 35f, 1.0f, 1.0f),
        ("fish_filefish", null, "filefish", "쥐치", "작은 입으로 오물오물. 거칠한 껍질이 특징.",
            FishRarity.Uncommon, 60, 0.3f, 0.8f, new Vector2(10f, 32f), new Vector2(6f, 11f), false, 30f, 0.8f, 0.9f),
        ("fish_puffer", null, "puffer", "복어", "놀라면 동그랗게 부풀어요. 가시가 있지만 순해요.",
            FishRarity.Uncommon, 70, 0.5f, 1.6f, new Vector2(8f, 40f), new Vector2(6f, 12f), false, 30f, 0.6f, 1.0f),
        ("fish_seahorse", null, "seahorse", "해마", "해초 사이에 숨어 천천히 떠다녀요. 꼬리로 해초를 꼭 잡아요.",
            FishRarity.Rare, 130, 0.05f, 0.15f, new Vector2(6f, 30f), new Vector2(8f, 13f), false, 14f, 0.35f, 0.75f),
        ("fish_flounder", "광어", null, "", "바닥 모래에 납작 엎드려 있어요. 두 눈이 한쪽에!",
            FishRarity.Rare, 0, 0.4f, 1.2f, new Vector2(12f, 42f), new Vector2(0.2f, 0.6f), true, 22f, 0.4f, 1.1f),
        ("fish_sea_bream", "도미", null, "", "분홍빛 몸이 고운 잔칫상의 주인공.",
            FishRarity.Uncommon, 0, 1.2f, 3.2f, new Vector2(16f, 50f), new Vector2(10f, 20f), false, 32f, 1.0f, 1.4f),
        // 구간 3 (~42m · ~25m)
        ("fish_squid", null, "squid", "오징어", "깊은 물에서 쏜살같이 다녀요. 먹물은 쏘지 않아요(아마도).",
            FishRarity.Uncommon, 160, 1.8f, 4.5f, new Vector2(26f, 60f), new Vector2(12f, 26f), false, 30f, 1.2f, 1.7f),
        ("fish_yellowtail", null, "yellowtail", "방어", "노란 줄이 멋진 힘센 물고기. 겨울에 더 맛있대요.",
            FishRarity.Uncommon, 200, 3f, 7f, new Vector2(30f, 65f), new Vector2(12f, 24f), false, 26f, 1.4f, 1.8f),
        ("fish_salmon", "연어", null, "", "강에서 태어나 먼바다를 누비다 돌아와요.",
            FishRarity.Uncommon, 0, 2f, 5f, new Vector2(28f, 60f), new Vector2(12f, 22f), false, 26f, 1.3f, 1.6f),
        ("fish_rockfish", "우럭", null, "", "바닥 바위틈에 사는 순한 물고기. 값은 그럭저럭.",
            FishRarity.Common, 0, 0.5f, 1.5f, new Vector2(20f, 50f), new Vector2(0.3f, 1.2f), true, 30f, 0.5f, 1.1f),
        ("fish_conger_eel", null, "eel", "붕장어", "바닥 굴에서 길쭉한 몸을 쏙 내밀어요.",
            FishRarity.Uncommon, 150, 1f, 3f, new Vector2(25f, 55f), new Vector2(0.3f, 1.5f), true, 26f, 0.6f, 1.8f),
        ("fish_octopus", null, "octopus", "문어", "바닥 바위틈에 살아요. 다리 여덟 개로 바늘을 꼭 붙잡아요.",
            FishRarity.Rare, 300, 3f, 8f, new Vector2(22f, 70f), new Vector2(0.3f, 1.5f), true, 18f, 0.5f, 1.6f),
        ("fish_lobster", null, "lobster", "닭새우", "단단한 껍데기에 긴 더듬이. 바닥의 보물.",
            FishRarity.Rare, 380, 1f, 3f, new Vector2(30f, 60f), new Vector2(0.2f, 0.8f), true, 14f, 0.4f, 1.2f),
        // 구간 4 (~63m · ~40m)
        ("fish_sunfish", null, "sunfish", "개복치", "커다랗고 느긋해요. 무겁지만 값은 그럭저럭.",
            FishRarity.Uncommon, 250, 10f, 25f, new Vector2(45f, 90f), new Vector2(10f, 30f), false, 25f, 0.3f, 2.4f),
        ("fish_tuna", "참치", null, "", "먼바다를 빠르게 달리는 힘센 물고기. 튼튼한 릴이 필요해요.",
            FishRarity.Rare, 0, 7f, 18f, new Vector2(40f, 85f), new Vector2(14f, 30f), false, 20f, 1.8f, 2.6f),
        ("fish_ray", null, "ray", "가오리", "먼바다 바닥을 날개처럼 펄럭이며 미끄러져요.",
            FishRarity.Rare, 450, 9f, 22f, new Vector2(45f, 92f), new Vector2(0.3f, 1.2f), true, 16f, 0.7f, 2.6f),
        ("fish_swordfish", null, "swordfish", "황새치", "칼처럼 긴 주둥이로 바다를 가르는 사냥꾼.",
            FishRarity.Rare, 750, 15f, 30f, new Vector2(50f, 90f), new Vector2(18f, 35f), false, 14f, 2.0f, 2.8f),
        ("fish_golden_mackerel", "황금 고등어", null, "", "먼바다 얕은 물에서 아주 가끔 번쩍여요. 보기만 해도 운이 좋아요.",
            FishRarity.Epic, 0, 0.4f, 1.0f, new Vector2(45f, 95f), new Vector2(3f, 8f), false, 4f, 1.6f, 1.2f),
        // 구간 5 (~95m · ~55m)
        ("fish_deep_jelly", null, "deepjelly", "심해 해파리", "깊은 바다에서 은은하게 빛나요. 값은 낮아요.",
            FishRarity.Uncommon, 200, 1f, 3f, new Vector2(55f, 95f), new Vector2(26f, 50f), false, 30f, 0.3f, 1.2f),
        ("fish_anglerfish", null, "angler", "초롱아귀", "깊고 어두운 바다에서 초롱을 밝혀요. 빛을 따라가면 만날지도.",
            FishRarity.Epic, 1100, 12f, 30f, new Vector2(55f, 95f), new Vector2(30f, 55f), false, 12f, 0.5f, 2.2f),
        ("fish_oarfish", null, "oarfish", "산갈치", "은빛 리본 같은 몸. 바다 깊은 곳의 전설.",
            FishRarity.Epic, 1300, 15f, 35f, new Vector2(60f, 95f), new Vector2(32f, 50f), false, 8f, 0.6f, 3.2f),
        ("fish_dragon_koi", null, "koi", "용궁 잉어", "용궁에서 왔다는 전설의 잉어. 본 해달은 손에 꼽아요.",
            FishRarity.Legendary, 4000, 25f, 38f, new Vector2(62f, 95f), new Vector2(25f, 45f), false, 2f, 1.4f, 3.0f),
    };

    [MenuItem("Tools/Fishing Rework/Build Test Scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        ImportArt();
        var (config, art) = BuildData();
        BuildScene(config, art);
        Debug.Log($"[FishingReworkSetup] 낚시 개편 테스트 씬 만듦: {ScenePath}");
    }

    #region 그림

    private static void ImportArt()
    {
        foreach (var dir in new[] { "Fish", "Ui" })
        {
            foreach (var path in Directory.GetFiles(Path.Combine(ArtFolder, dir), "*.png"))
                ImportSprite(path.Replace('\\', '/'), -1f, new Vector2(0.5f, 0.5f));
        }
        string world = $"{ArtFolder}/World";
        ImportSprite($"{world}/SKY.png", 16f, new Vector2(0.5f, 0.5f));
        ImportSprite($"{world}/WATER.png", 8f, new Vector2(0.5f, 1f));
        ImportSprite($"{world}/WAVE.png", 128f, new Vector2(0.5f, 0.5f), 1024, TextureWrapMode.Repeat);
        ImportSprite($"{world}/RAY.png", 64f, new Vector2(0.5f, 1f));
        ImportSprite($"{world}/ISLAND.png", 1024f / 24f, new Vector2(0.5f, 0f));
        ImportSprite($"{world}/CLOUD.png", 100f, new Vector2(0.5f, 0.5f));
        ImportSprite($"{world}/PIER.png", 128f, new Vector2(0.995f, 0.995f), 2048);
        ImportSprite($"{world}/BUOY.png", 160f, new Vector2(0.5f, 0.3f));
        foreach (var name in new[] { "SEAWEED_0", "SEAWEED_1" })
            ImportSprite($"{world}/{name}.png", 128f, new Vector2(0.5f, 0.02f));
        foreach (var name in new[] { "CORAL_0", "CORAL_1" })
            ImportSprite($"{world}/{name}.png", 128f, new Vector2(0.5f, 0.02f));
        foreach (var name in new[] { "ROCK_0", "ROCK_1" })
            ImportSprite($"{world}/{name}.png", 128f, new Vector2(0.5f, 0.12f));
        ImportSprite($"{world}/HOOK.png", -1f, new Vector2(0.5f, 0.96f));
        ImportSprite($"{world}/SINKER.png", -1f, new Vector2(0.5f, 0.5f));
    }

    // ppu < 0 = 그림 폭 (1유닛)
    private static void ImportSprite(string path, float ppu, Vector2 pivot, int maxSize = 1024, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogWarning($"[FishingReworkSetup] 그림이 없습니다: {path}");
            return;
        }
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
        importer.wrapMode = wrap;
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
        string full = $"{ArtFolder}/{path}.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(full);
        if (sprite == null)
            Debug.LogWarning($"[FishingReworkSetup] 그림이 없습니다: {full}");
        return sprite;
    }

    #endregion

    #region 데이터

    private static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static ItemRarity Rarity(FishRarity rarity)
    {
        string name = rarity >= FishRarity.Epic ? "Epic" : rarity >= FishRarity.Rare ? "Rare" : "Common";
        return AssetDatabase.LoadAssetAtPath<ItemRarity>($"{RarityFolder}/{name}.asset");
    }

    // 원래 있던 아이템이면 그대로 (이름 · 값 · 그림), 새 물고기면 가방 아이템을 만듦
    private static ItemDefinition FishItem((string id, string existing, string art, string name, string desc, FishRarity rarity, int price,
        float minW, float maxW, Vector2 dist, Vector2 depth, bool bottom, float spawn, float speed, float length) f)
    {
        if (!string.IsNullOrEmpty(f.existing))
        {
            var old = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{f.existing}.asset");
            if (old == null)
                Debug.LogError($"[FishingReworkSetup] 원래 물고기 아이템이 없습니다: {f.existing}");
            return old;
        }
        var item = Asset<ItemDefinition>($"{ItemFolder}/{f.name}.asset");
        var so = new SerializedObject(item);
        so.FindProperty("_itemId").stringValue = f.id;
        so.FindProperty("_displayName").stringValue = f.name;
        so.FindProperty("_description").stringValue = f.desc;
        so.FindProperty("_icon").objectReferenceValue = Art($"Fish/FISH_{f.art}");
        so.FindProperty("_category").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemCategory>(FishCategoryPath);
        so.FindProperty("_rarity").objectReferenceValue = Rarity(f.rarity);
        so.FindProperty("_isSellable").boolValue = true;
        so.FindProperty("_sellPrice").intValue = f.price;
        so.FindProperty("_maxStack").intValue = 999;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static (FishingReworkConfig, FishingReworkArt) BuildData()
    {
        Directory.CreateDirectory($"{DataFolder}/Fish");
        var fish = new List<FishDefinition>();
        foreach (var f in FishTable)
        {
            var item = FishItem(f);
            bool existing = !string.IsNullOrEmpty(f.existing) && item != null;
            var def = Asset<FishDefinition>($"{DataFolder}/Fish/Fish_{f.id}.asset");
            def.Set(f.id, existing ? item.DisplayName : f.name, f.desc, existing ? item.Icon : Art($"Fish/FISH_{f.art}"), f.rarity,
                existing ? item.SellPrice : f.price, f.minW, f.maxW, f.dist, f.depth, f.bottom, f.spawn, f.speed, f.length);
            // 원래 물고기 아이콘은 왼쪽을 봄
            def.SetItem(item, existing);
            EditorUtility.SetDirty(def);
            fish.Add(def);
        }

        var config = Asset<FishingReworkConfig>($"{DataFolder}/FishingReworkConfig.asset");
        config.SetFish(fish);
        // 물층마다 한 칸(8m)에 최대 마릿수 · 한 마리씩 다시 오는 시간: 마릿수는 적게(얕은 물이 붐벼 깊은 데를 막지 않게),
        // 다시 오는 건 빠르게(잡아도 몇 초면 채워져 쉬는 시간이 없게 — 한 번 던지기 5~8초보다 짧게)
        config.SetLayers(new[] { 3, 3, 2, 2 }, new[] { 8f, 8f, 10f, 10f });
        // 강화 비용은 본 게임 골드 규모 (낚시는 Lv.15부터, 예전 낚싯대 강화 1~20과 비슷한 합)
        config.SetTracks(
            new FishingUpgradeTrack("줄 길이", "던지는 거리", "m", Art("Ui/ICON_Line"),
                new[] { 12f, 18f, 25f, 33f, 42f, 52f, 63f, 75f },
                new[] { 300, 800, 2000, 5000, 11000, 22000, 40000 }),
            new FishingUpgradeTrack("찌 무게", "내려가는 깊이", "m", Art("Ui/ICON_Sinker"),
                new[] { 7f, 10f, 14f, 19f, 25f, 32f, 40f, 50f },
                new[] { 250, 700, 1800, 4500, 10000, 20000, 36000 }),
            new FishingUpgradeTrack("릴", "버티는 무게", "kg", Art("Ui/ICON_Reel"),
                new[] { 1f, 2f, 3.5f, 6f, 10f, 16f, 25f, 40f },
                new[] { 350, 900, 2200, 5500, 12000, 24000, 45000 }));
        // 바다 · 줄 움직임: 늘 선착장 쪽으로 감김(1.5 · 누르면 2.4m/초), 누르면 올라옴(3m/초), 떼면 찌 깊이까지 가라앉음
        var cso = new SerializedObject(config);
        cso.FindProperty("_seabedStart").floatValue = 8f;
        cso.FindProperty("_seabedSlope").floatValue = 0.5f;
        cso.FindProperty("_autoReel").floatValue = 1.5f;
        cso.FindProperty("_holdReel").floatValue = 2.4f;
        cso.FindProperty("_liftSpeed").floatValue = 3f;
        cso.FindProperty("_sinkSpeed").floatValue = 2.2f;
        cso.FindProperty("_sinkSpeedPerLevel").floatValue = 1f;
        cso.FindProperty("_fishTopDepth").floatValue = 2.5f;
        cso.FindProperty("_hookRadius").floatValue = 0.35f;
        cso.FindProperty("_fishHitScale").floatValue = 0.25f;
        cso.FindProperty("_hookedReel").floatValue = 6.5f;
        cso.FindProperty("_attractRadius").floatValue = 1.8f;
        // 줄이 길어도 한 번 쓸고 오는 데 9초쯤 · 걸린 물고기는 4초쯤 (Docs/낚시_밸런스_시뮬.md)
        cso.FindProperty("_sweepSeconds").floatValue = 9f;
        cso.FindProperty("_landSeconds").floatValue = 4f;
        cso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);

        var art = Asset<FishingReworkArt>($"{DataFolder}/FishingReworkArt.asset");
        art.Set(AssetDatabase.LoadAssetAtPath<MineReworkArt>(MineArtPath),
            Art("World/SKY"), Art("World/WATER"), Art("World/WAVE"), Art("World/RAY"), Art("World/ISLAND"), Art("World/CLOUD"),
            Art("World/PIER"), Art("World/BUOY"),
            new[] { Art("World/SEAWEED_0"), Art("World/SEAWEED_1") },
            new[] { Art("World/CORAL_0"), Art("World/CORAL_1") },
            new[] { Art("World/ROCK_0"), Art("World/ROCK_1") },
            Art("World/HOOK"), Art("World/SINKER"),
            Art("Ui/ICON_Upgrade"), Art("Ui/ICON_Book"), Art("Ui/ICON_AnyFish"), Art("Ui/ICON_Big"), Art("Ui/ICON_Bag"));
        art.SetIconPier(AssetDatabase.LoadAssetAtPath<Sprite>(PierIconPath));
        if (art.Ui == null)
            Debug.LogError($"[FishingReworkSetup] 광산 개편 UI 그림이 없습니다: {MineArtPath} (Tools/Mine Rework/Build Test Scene 먼저)");
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        return (config, art);
    }

    #endregion

    #region 테스트 씬

    private static void BuildScene(FishingReworkConfig config, FishingReworkArt art)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        config = AssetDatabase.LoadAssetAtPath<FishingReworkConfig>($"{DataFolder}/FishingReworkConfig.asset");
        art = AssetDatabase.LoadAssetAtPath<FishingReworkArt>($"{DataFolder}/FishingReworkArt.asset");

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 7.6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.45f, 0.7f, 0.9f);
        cameraGo.transform.position = new Vector3(2.6f, -1.2f, -10f);
        cameraGo.AddComponent<AudioListener>();

        var rig = CreateFisher(null);
        var fx = new GameObject("Fx").AddComponent<MineFx>();
        var sfx = new GameObject("Sfx").AddComponent<MineSfx>();
        CreateGame(config, art, camera, rig, fx, sfx, false, null);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static QuarterRigView CreateFisher(Transform parent)
    {
        var otter = new GameObject("Fisher");
        if (parent != null)
            otter.transform.SetParent(parent, false);
        otter.transform.localPosition = new Vector3(-1.1f, 0.95f, 0f);
        var rig = otter.AddComponent<QuarterRigView>();
        var rso = new SerializedObject(rig);
        rso.FindProperty("resourceFolder").stringValue = "FisherRigV1";
        rso.FindProperty("characterScale").floatValue = 0.5f;
        rso.FindProperty("startFacingLeft").boolValue = false;
        rso.FindProperty("sortingOrder").intValue = 10;
        rso.ApplyModifiedPropertiesWithoutUndo();
        return rig;
    }

    private static FishingReworkGame CreateGame(FishingReworkConfig config, FishingReworkArt art, Camera camera, QuarterRigView rig,
        MineFx fx, MineSfx sfx, bool hosted, Transform seaRoot)
    {
        var game = new GameObject("FishingReworkGame").AddComponent<FishingReworkGame>();
        var so = new SerializedObject(game);
        so.FindProperty("_config").objectReferenceValue = config;
        so.FindProperty("_art").objectReferenceValue = art;
        so.FindProperty("_camera").objectReferenceValue = camera;
        so.FindProperty("_otter").objectReferenceValue = rig;
        so.FindProperty("_fx").objectReferenceValue = fx;
        so.FindProperty("_sfx").objectReferenceValue = sfx;
        so.FindProperty("_hosted").boolValue = hosted;
        so.FindProperty("_seaRoot").objectReferenceValue = seaRoot;
        so.ApplyModifiedPropertiesWithoutUndo();
        return game;
    }

    #endregion
}
