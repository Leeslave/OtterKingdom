using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 보물 조개 뽑기 (Docs/장난감_뽑기_기획.md): 데이터(장난감 13종 · 뽑기권 · 반짝 조각 · 배너 2개 · 한정 해달 살랑이)와
/// 화면(뽑기 화면 · 확률 정보 · 교환소 · 연출 · HUD 뽑기 버튼)을 만든다. GlobalUISetup(Build Global UI)이 함께 호출한다.
/// 그림: Tools/UIGen/import_gacha_art.py(받은 그림 다듬기) · gacha_fx.py(금 · 반쪽 조개 · 물결). 1080×1920 기준.
/// 이미 있는 데이터 에셋은 사람이 고친 값을 지키기 위해 덮어쓰지 않는다 (비어 있는 그림·참조만 채움).
/// </summary>
public static class GachaSetup
{
    private const string DataFolder = "Assets/Scriptable Obejects/Gacha";
    private const string BannerFolder = DataFolder + "/Banners";
    private const string CurrencyFolder = DataFolder + "/Currencies";
    internal const string StandardBannerPath = BannerFolder + "/Banner_Standard.asset";
    internal const string PickupBannerPath = BannerFolder + "/Banner_Pickup_Spring.asset";
    internal const string GoldBannerPath = BannerFolder + "/Banner_Gold.asset";
    private const string GachaGlobalUIPath = "Assets/Resources/GlobalUI.prefab";
    private const string TitleFontPath = "Assets/Fonts/Cafe24Ssurround-v2.0 SDF.asset";
    private const string BodyFontPath = "Assets/Fonts/NanumSquareRoundOTFR SDF.asset";
    private const string ShardPath = CurrencyFolder + "/Shard.asset";
    private const string ItemFolder = "Assets/Scriptable Obejects/Inventory/Items/Decor";
    private const string ItemDatabasePath = "Assets/Scriptable Obejects/Inventory/ItemDatabase.asset";
    private const string ToyCategoryPath = "Assets/Scriptable Obejects/Inventory/Category/Toy.asset";
    private const string RarityFolder = "Assets/Scriptable Obejects/Inventory/Rarity";
    private const string DecorFolder = "Assets/Scriptable Obejects/Decor/Decors";
    private const string SettlementFolder = "Assets/Scriptable Obejects/Settlement";
    private const string SallangOtterPath = SettlementFolder + "/Otters/Otter_Toy_Sallang.asset";
    private const string SallangEntryPath = SettlementFolder + "/Guestbook/gb_otter_toy_sallang_arrival.asset";
    private const string PlazaPrefabFolder = "Assets/Prefabs/Plaza";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string GemPath = "Assets/Scriptable Obejects/Gem.asset";
    private const string GachaArtFolder = "Assets/Art/Gacha";
    private const string ToyArtFolder = "Assets/Art/Item/Toy";
    private const string CurrencyArtFolder = "Assets/Art/UI/Currency";
    private const string CelebrateFolder = "Assets/Resources/UI/Celebrate";
    private const string PetalFolder = "Assets/Art/Settlement";
    private const string NpcArtFolder = "Assets/Art/Npc";

    private static readonly Color Body = new Color32(0x6B, 0x4A, 0x3A, 0xFF);
    private static readonly Color DayWater = new Color32(0x6E, 0xC8, 0xD6, 0xFF);

    // 장난감: (에셋, 아이템 ID, 이름, 설명, 아이콘, 등급(0 흔함 · 1 레어 · 2 에픽), 놀이 반응, 동시 인원, 칸)
    // 상시 = 흔함 6(축구공 포함) · 레어 4(퍼즐 포함) · 에픽 2, 픽업 = 벚꽃 바람개비(에픽) · 꽃잎 연 · 나비 모빌(레어)
    private static readonly (string asset, string id, string name, string description, string icon, int tier, DecorPlayStyle style, int players, int size)[] Toys =
    {
        ("BeachBall", "toy_beachball", "비치볼", "통통 튀는 알록달록 공이에요. 해달들이 머리로 받으며 놀아요.", "ICON_Toy_BeachBall", 0, DecorPlayStyle.Bounce, 2, 1),
        ("RubberDuck", "toy_rubberduck", "고무 오리", "물에 띄우면 둥실둥실, 누르면 꽉꽉 소리가 나요.", "ICON_Toy_RubberDuck", 0, DecorPlayStyle.Wiggle, 1, 1),
        ("SandBucket", "toy_sandbucket", "모래 양동이", "모래성을 쌓을 때 꼭 필요한 양동이와 삽이에요.", "ICON_Toy_SandBucket", 0, DecorPlayStyle.Wiggle, 2, 1),
        ("Yoyo", "toy_yoyo", "요요", "말랑한 끈을 따라 오르락내리락, 손재주 자랑에 딱이에요.", "ICON_Toy_Yoyo", 0, DecorPlayStyle.Bounce, 1, 1),
        ("PaperBoat", "toy_paperboat", "종이배", "작은 물웅덩이만 있으면 어디든 떠나는 종이배예요.", "ICON_Toy_PaperBoat", 0, DecorPlayStyle.Wiggle, 1, 1),
        ("Drum", "toy_drum", "작은 북", "둥둥 두드리면 광장이 금세 흥겨워져요.", "ICON_Toy_Drum", 1, DecorPlayStyle.Bounce, 1, 1),
        ("Bubbles", "toy_bubbles", "비눗방울", "후 불면 무지갯빛 방울이 하늘로 날아올라요.", "ICON_Toy_Bubbles", 1, DecorPlayStyle.Wiggle, 2, 1),
        ("Kite", "toy_kite", "다이아몬드 연", "바람 좋은 날, 리본 꼬리를 흔들며 높이 날아요.", "ICON_Toy_Kite", 1, DecorPlayStyle.Wiggle, 1, 1),
        ("Carousel", "toy_carousel", "회전목마", "빙글빙글 도는 작은 회전목마예요. 해달 셋이 함께 탈 수 있어요.", "ICON_Toy_Carousel", 2, DecorPlayStyle.Spin, 3, 2),
        ("HotAirBalloon", "toy_hotairballoon", "미니 열기구", "작지만 진짜처럼 둥실 떠오르는 열기구예요.", "ICON_Toy_HotAirBalloon", 2, DecorPlayStyle.Wiggle, 2, 2),
        ("CherryPinwheel", "toy_cherrypinwheel", "벚꽃 바람개비", "봄바람에 빙글빙글 도는 벚꽃 바람개비. 살랑이가 가장 좋아해요.", "ICON_Toy_CherryPinwheel", 2, DecorPlayStyle.Spin, 2, 1),
        ("PetalKite", "toy_petalkite", "꽃잎 연", "벚꽃잎 모양으로 하늘하늘 나는 연이에요.", "ICON_Toy_PetalKite", 1, DecorPlayStyle.Wiggle, 1, 1),
        ("ButterflyMobile", "toy_butterflymobile", "나비 모빌", "바람이 불면 나비들이 춤을 춰요.", "ICON_Toy_ButterflyMobile", 1, DecorPlayStyle.Spin, 1, 1),
    };

    private static readonly string[] StandardPool =
        { "축구공", "BeachBall", "RubberDuck", "SandBucket", "Yoyo", "PaperBoat", "퍼즐", "Drum", "Bubbles", "Kite", "Carousel", "HotAirBalloon" };

    private static readonly string[] PickupFeatured = { "CherryPinwheel", "PetalKite", "ButterflyMobile" };

    // 골드 배너 = 상시 장난감 중 흔함 · 레어 + 가구 (PlazaLifeSetup이 만든 아이템, 흔함). 에픽은 조개 뽑기에만
    private static readonly string[] GoldFurniture = { "작은 덤불", "그루터기 의자", "통나무 의자", "나무 벤치", "가로등", "피크닉 매트" };

    // 조개 모습 순서 (GachaRevealView): 흔함 · 레어 · 에픽 · 픽업
    private static readonly string[] ShellLooks = { "Common", "Rare", "Epic", "Pickup" };

    /// <summary>뽑기 화면들 (GlobalUISetup이 프리팹에 넣고 Attach로 연결)</summary>
    public struct Screens
    {
        public GachaScreenView screen;
        public GachaRatesPopupView rates;
        public GachaExchangePopupView exchange;
        public GachaRevealView reveal;
    }

    #region 데이터

    /// <summary>뽑기권 · 반짝 조각 · 장난감(아이템 + 꾸미기) · 살랑이 · 배너. 꾸미기 데이터(DecorSetup)보다 먼저</summary>
    public static void CreateData()
    {
        EnsureFolder(BannerFolder);
        EnsureFolder(CurrencyFolder);
        EnsureFolder(ItemFolder);
        EnsureFolder(DecorFolder);
        EnsureFolder(SettlementFolder + "/Otters");
        EnsureFolder(SettlementFolder + "/Guestbook");
        ImportArt();

        var pickupTicket = CreateCurrency("TicketPickup", "별빛 조개권", "픽업 뽑기를 한 번 할 수 있어요.");
        var standardTicket = CreateCurrency("TicketStandard", "보물 조개권", "상시 뽑기를 한 번 할 수 있어요.");
        CreateCurrency("Shard", "반짝 조각", "이미 있는 장난감이 나오면 덤으로 받아요. 교환소에서 장난감과 바꿔요.");

        var toys = CreateToys();
        var sallang = CreateLimitedOtter(toys["CherryPinwheel"]);
        CreateBanners(toys, sallang, pickupTicket, standardTicket);
        AssetDatabase.SaveAssets();
    }

    private static void ImportArt()
    {
        foreach (var name in new[] { "Otter_Float_Rest", "Otter_Float_Ready", "Otter_Float_Surprise", "BG_Sea_Day", "BG_Sea_Night",
                     "Banner_Pickup_Spring", "Banner_Standard", "Banner_Gold", "FX_Ring", "FX_WaterBody", "FX_WaterFoam" })
            ImportSprite(GachaArtFolder, name);
        foreach (var look in ShellLooks)
        {
            ImportSprite(GachaArtFolder, $"Shell_{look}");
            ImportSprite(GachaArtFolder, $"Shell_{look}_L");
            ImportSprite(GachaArtFolder, $"Shell_{look}_R");
        }
        for (int stage = 1; stage <= 3; stage++)
        {
            ImportSprite(GachaArtFolder, $"FX_CrackCore_{stage}");
            ImportSprite(GachaArtFolder, $"FX_CrackGlow_{stage}");
        }
    }

    private static Currency CreateCurrency(string id, string displayName, string description)
    {
        var (currency, isNew) = LoadOrCreate<Currency>($"{CurrencyFolder}/{id}.asset");
        if (isNew)
        {
            currency.CurrencyID = id;
            currency.DisplayName = displayName;
            currency.Description = description;
        }
        if (currency.Icon == null)
            currency.Icon = ImportSprite(CurrencyArtFolder, id);
        EditorUtility.SetDirty(currency);
        return currency;
    }

    private static Dictionary<string, ItemDefinition> CreateToys()
    {
        var category = AssetDatabase.LoadAssetAtPath<ItemCategory>(ToyCategoryPath);
        var rarities = new[]
        {
            AssetDatabase.LoadAssetAtPath<ItemRarity>($"{RarityFolder}/Common.asset"),
            AssetDatabase.LoadAssetAtPath<ItemRarity>($"{RarityFolder}/Rare.asset"),
            AssetDatabase.LoadAssetAtPath<ItemRarity>($"{RarityFolder}/Epic.asset"),
        };

        var items = new Dictionary<string, ItemDefinition>();
        var decors = new List<DecorDefinition>();
        foreach (var t in Toys)
        {
            var (item, itemIsNew) = LoadOrCreate<ItemDefinition>($"{ItemFolder}/{t.name}.asset");
            var itemSo = new SerializedObject(item);
            if (itemIsNew)
            {
                itemSo.FindProperty("_itemId").stringValue = t.id;
                itemSo.FindProperty("_displayName").stringValue = t.name;
                itemSo.FindProperty("_description").stringValue = t.description;
                itemSo.FindProperty("_category").objectReferenceValue = category;
                itemSo.FindProperty("_rarity").objectReferenceValue = rarities[t.tier];
                itemSo.FindProperty("_isSellable").boolValue = false; // 광장에 놓인 채로 팔리는 일이 없게
                itemSo.FindProperty("_maxStack").intValue = 99;
            }
            FillIfEmpty(itemSo, "_icon", ImportSprite(ToyArtFolder, t.icon));
            itemSo.ApplyModifiedPropertiesWithoutUndo();
            items[t.asset] = item;

            var (decor, decorIsNew) = LoadOrCreate<DecorDefinition>($"{DecorFolder}/Decor_{t.asset}.asset");
            if (decorIsNew)
            {
                var so = new SerializedObject(decor);
                so.FindProperty("_item").objectReferenceValue = item;
                so.FindProperty("_footprint").vector2IntValue = new Vector2Int(t.size, t.size);
                so.FindProperty("_playStyle").enumValueIndex = (int)t.style;
                so.FindProperty("_maxPlayers").intValue = t.players;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            decors.Add(decor);
        }

        // 세이브 복원이 찾도록 ItemDatabase에, 꾸미기 보관함에 보이도록 DecorCatalog에 (이미 있으면 그대로)
        var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        if (database != null)
        {
            var so = new SerializedObject(database);
            foreach (var item in items.Values)
                AddUnique(so.FindProperty("_items"), item);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>(DecorSetup.CatalogPath);
        if (catalog != null)
        {
            var so = new SerializedObject(catalog);
            foreach (var decor in decors)
                AddUnique(so.FindProperty("_decors"), decor);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return items;
    }

    // 한정 해달 살랑이: 벚꽃 바람개비가 광장에 있을 때만 찾아옴 (그림은 얼굴만 새것, 광장 모습은 기존 해달 + 분홍 색)
    private static SettlementOtterDefinition CreateLimitedOtter(ItemDefinition pinwheel)
    {
        var (entry, _) = LoadOrCreate<GuestbookEntryDefinition>(SallangEntryPath);
        var (otter, isNew) = LoadOrCreate<SettlementOtterDefinition>(SallangOtterPath);

        var entrySo = new SerializedObject(entry);
        entrySo.FindProperty("_entryId").stringValue = "gb_otter_toy_sallang_arrival";
        entrySo.FindProperty("_otter").objectReferenceValue = otter;
        entrySo.FindProperty("_tag").stringValue = "첫 방문";
        entrySo.FindProperty("_message").stringValue = "바람개비 도는 소리를 따라 봄바람을 타고 날아왔어요!";
        entrySo.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(otter);
        if (isNew)
        {
            so.FindProperty("_otterId").stringValue = "otter_toy_sallang";
            so.FindProperty("_displayName").stringValue = "살랑이";
            so.FindProperty("_plazaPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlazaPrefabFolder}/PlazaOtter_Painter.prefab");
            so.FindProperty("_plazaTint").colorValue = new Color(1f, 0.84f, 0.9f);
            so.FindProperty("_trait").intValue = (int)OtterTrait.Exploring;
            so.FindProperty("_toyVisitor").boolValue = true;
            so.FindProperty("_visitTier").intValue = GachaTable.Epic;
            so.FindProperty("_homelessLine").stringValue = "바람이 잘 드는 집이 있으면 좋겠어요.\n빈 집이 생기면 불러 줘요!";
            so.FindProperty("_arrivalEntry").objectReferenceValue = entry;
            so.FindProperty("_moveInLine").stringValue = "바람이 살랑살랑 잘 드는 집이네요!\n여기 살아도 될까요?";
            var lines = so.FindProperty("_lines");
            var texts = new[] { "바람이 살랑~ 기분 좋아요!", "바람개비가 빙글빙글 돌아요.", "벚꽃잎 하나 드릴까요?" };
            lines.arraySize = texts.Length;
            for (int i = 0; i < texts.Length; i++)
                lines.GetArrayElementAtIndex(i).stringValue = texts[i];
        }
        FillIfEmpty(so, "_portrait", ImportSprite(OtterFolder, "ICON_Otter_Cast_Blossom"));
        FillIfEmpty(so, "_favoriteToy", pinwheel);
        so.ApplyModifiedPropertiesWithoutUndo();
        return otter;
    }

    /// <summary>
    /// 지금 정착 설정에 한정 해달만 넣는다 (정착 데이터를 다시 만들지 않고).
    /// Build Global UI는 정착 설정을 처음 상태로 다시 만들어 P3·P4 데이터(Apply P3 Projects)가 빠지므로, 그 뒤에는 이 메뉴 대신 Apply P3 Projects를 다시 실행
    /// 배치 모드: -executeMethod GachaSetup.AddLimitedOttersToSettlement
    /// </summary>
    [MenuItem("Tools/Gacha/Add Limited Otters To Settlement")]
    public static void AddLimitedOttersToSettlement()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(SettlementSetup.ConfigPath);
        if (config == null)
        {
            Debug.LogError("[GachaSetup] 정착 설정이 없습니다.");
            return;
        }
        var configSo = new SerializedObject(config);
        AddLimitedOtters(configSo);
        configSo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("[GachaSetup] 정착 설정에 한정 해달을 넣었습니다.");
    }

    /// <summary>정착 설정의 해달 목록에 한정 해달을 넣는다 (SettlementSetup이 장난감 해달을 다시 채울 때 부름)</summary>
    internal static void AddLimitedOtters(SerializedObject configSo)
    {
        var otter = AssetDatabase.LoadAssetAtPath<SettlementOtterDefinition>(SallangOtterPath);
        if (otter == null)
            return; // 뽑기 데이터를 아직 안 만듦 (Build Global UI가 정착 데이터보다 먼저 만듦)
        AddUnique(configSo.FindProperty("_otters"), otter);
        var entry = AssetDatabase.LoadAssetAtPath<GuestbookEntryDefinition>(SallangEntryPath);
        if (entry != null)
            AddUnique(configSo.FindProperty("_guestbookEntries"), entry);
    }

    private static void CreateBanners(Dictionary<string, ItemDefinition> toys, SettlementOtterDefinition sallang,
        Currency pickupTicket, Currency standardTicket)
    {
        var gem = AssetDatabase.LoadAssetAtPath<Currency>(GemPath);
        var pool = StandardPool.Select(key => toys.TryGetValue(key, out var item) ? item
                : AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{key}.asset"))
            .Where(item => item != null).ToList();
        var featured = PickupFeatured.Select(key => toys[key]).ToList();

        var (standard, standardIsNew) = LoadOrCreate<GachaBannerDefinition>(StandardBannerPath);
        var so = new SerializedObject(standard);
        if (standardIsNew)
        {
            so.FindProperty("_bannerId").stringValue = "standard_seaside";
            so.FindProperty("_kind").enumValueIndex = (int)GachaBannerKind.Standard;
            so.FindProperty("_title").stringValue = "바닷가 보물 조개";
            so.FindProperty("_tabTitle").stringValue = "보물 조개";
            so.FindProperty("_subtitle").stringValue = "늘 열려 있는 보물 조개. 하루 한 번은 골드로!";
            so.FindProperty("_sortOrder").intValue = 1;
            so.FindProperty("_themeColor").colorValue = new Color(1f, 0.86f, 0.5f);
            SetList(so.FindProperty("_pool"), pool);
            so.FindProperty("_epicRate").floatValue = 0.025f;
            so.FindProperty("_rareRate").floatValue = 0.15f;
            so.FindProperty("_epicPity").intValue = 80;
            so.FindProperty("_rarePity").intValue = 10;
            so.FindProperty("_gemCost").intValue = 30;
            so.FindProperty("_exchangePoints").intValue = 0;
            so.FindProperty("_dailyGoldPull").boolValue = true;
            so.FindProperty("_goldPullBase").intValue = 1000;
            so.FindProperty("_goldPullPerLevel").intValue = 800;
        }
        FillIfEmpty(so, "_bannerArt", ImportSprite(GachaArtFolder, "Banner_Standard"));
        FillIfEmpty(so, "_shellIcon", ImportSprite(GachaArtFolder, "Shell_Epic"));
        FillIfEmpty(so, "_gem", gem);
        FillIfEmpty(so, "_ticket", standardTicket);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 픽업 기간은 2주 (기기 날짜). 다음 픽업은 새 배너 에셋을 만들어 기간을 이어 붙임
        var (pickup, pickupIsNew) = LoadOrCreate<GachaBannerDefinition>(PickupBannerPath);
        so = new SerializedObject(pickup);
        if (pickupIsNew)
        {
            so.FindProperty("_bannerId").stringValue = "pickup_spring_breeze";
            so.FindProperty("_kind").enumValueIndex = (int)GachaBannerKind.Pickup;
            so.FindProperty("_title").stringValue = "봄바람 픽업";
            so.FindProperty("_subtitle").stringValue = "벚꽃 바람개비를 두면 한정 해달 살랑이가 와요!";
            so.FindProperty("_sortOrder").intValue = 0;
            so.FindProperty("_startDate").stringValue = "2026-10-07";
            so.FindProperty("_endDate").stringValue = "2026-10-20";
            so.FindProperty("_themeColor").colorValue = new Color(1f, 0.78f, 0.86f);
            SetList(so.FindProperty("_pool"), pool);
            SetList(so.FindProperty("_featured"), featured);
            so.FindProperty("_epicRate").floatValue = 0.03f;
            so.FindProperty("_rareRate").floatValue = 0.15f;
            so.FindProperty("_featuredShare").floatValue = 0.5f;
            so.FindProperty("_epicPity").intValue = 60;
            so.FindProperty("_rarePity").intValue = 10;
            so.FindProperty("_gemCost").intValue = 30;
            so.FindProperty("_exchangePoints").intValue = 100;
            so.FindProperty("_dailyGoldPull").boolValue = false;
        }
        FillIfEmpty(so, "_bannerArt", ImportSprite(GachaArtFolder, "Banner_Pickup_Spring"));
        FillIfEmpty(so, "_shellIcon", ImportSprite(GachaArtFolder, "Shell_Pickup"));
        FillIfEmpty(so, "_featuredOtter", sallang);
        FillIfEmpty(so, "_gem", gem);
        FillIfEmpty(so, "_ticket", pickupTicket);
        so.ApplyModifiedPropertiesWithoutUndo();

        CreateGoldBanner(pool);
    }

    /// <summary>
    /// 골드 배너 "모래사장 골드 조개": 골드 소모처. 1회 = 500 + 250 × 왕국 레벨 (Lv.5 1,750 · Lv.10 3,000 · Lv.15 4,250 · Lv.19 5,250),
    /// 10회 = 1회 × 9. 레어 10% · 10회 안에 레어 확정, 에픽 없음 (조개 값이 떨어지지 않게). 가구(흔함)가 함께 나옴
    /// </summary>
    private static GachaBannerDefinition CreateGoldBanner(List<ItemDefinition> standardPool)
    {
        var pool = standardPool.Where(item => item.Rarity == null || item.Rarity.Tier < GachaTable.Epic).ToList();
        foreach (var name in GoldFurniture)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{name}.asset");
            if (item != null)
                pool.Add(item);
            else
                Debug.LogWarning($"[GachaSetup] 가구 {name}가 없어 골드 배너에서 뺍니다 (Tools/Settlement/Apply Plaza Life 먼저).");
        }

        var (banner, isNew) = LoadOrCreate<GachaBannerDefinition>(GoldBannerPath);
        var so = new SerializedObject(banner);
        if (isNew)
        {
            so.FindProperty("_bannerId").stringValue = "gold_sandbar";
            so.FindProperty("_kind").enumValueIndex = (int)GachaBannerKind.Gold;
            so.FindProperty("_title").stringValue = "골드 조개";
            so.FindProperty("_subtitle").stringValue = "골드로 언제든! 장난감과 광장 가구가 나와요";
            so.FindProperty("_sortOrder").intValue = 2;
            so.FindProperty("_themeColor").colorValue = new Color(1f, 0.8f, 0.36f);
            SetList(so.FindProperty("_pool"), pool);
            so.FindProperty("_epicRate").floatValue = 0f;
            so.FindProperty("_rareRate").floatValue = 0.1f;
            so.FindProperty("_epicPity").intValue = 999;
            so.FindProperty("_rarePity").intValue = 10;
            so.FindProperty("_exchangePoints").intValue = 0;
            so.FindProperty("_dailyGoldPull").boolValue = false;
            so.FindProperty("_goldCostBase").intValue = 500;
            so.FindProperty("_goldCostPerLevel").intValue = 250;
            so.FindProperty("_goldTenPulls").intValue = 9;
        }
        FillIfEmpty(so, "_bannerArt", ImportSprite(GachaArtFolder, "Banner_Gold"));
        FillIfEmpty(so, "_shellIcon", ImportSprite(GachaArtFolder, "Shell_Rare"));
        so.ApplyModifiedPropertiesWithoutUndo();
        return banner;
    }

    /// <summary>
    /// 골드 배너만 만들어 전역 UI 프리팹의 뽑기 매니저 · 프레젠터에 붙인다 (Build Global UI를 다시 돌리지 않고).
    /// Build Global UI는 정착 설정을 처음 상태로 다시 만들므로 이미 만든 프로젝트에서는 이 메뉴를 쓴다.
    /// 배치 모드: -executeMethod GachaSetup.AddGoldBanner
    /// </summary>
    [MenuItem("Tools/Gacha/Add Gold Banner")]
    public static void AddGoldBanner()
    {
        var standard = AssetDatabase.LoadAssetAtPath<GachaBannerDefinition>(StandardBannerPath);
        if (standard == null)
        {
            Debug.LogError("[GachaSetup] 상시 배너가 없습니다. Build Global UI를 먼저 실행하세요.");
            return;
        }
        ImportSprite(GachaArtFolder, "Banner_Gold");
        var gold = CreateGoldBanner(standard.Pool.Where(item => item != null).ToList());
        AssetDatabase.SaveAssets();

        var root = PrefabUtility.LoadPrefabContents(GachaGlobalUIPath);
        try
        {
            var manager = root.GetComponentInChildren<GachaManager>(true);
            var presenter = root.GetComponentInChildren<GachaPresenter>(true);
            if (manager == null || presenter == null)
            {
                Debug.LogError("[GachaSetup] 전역 UI에 뽑기 매니저 · 프레젠터가 없습니다.");
                return;
            }
            var managerSo = new SerializedObject(manager);
            AddUnique(managerSo.FindProperty("_banners"), gold);
            managerSo.ApplyModifiedPropertiesWithoutUndo();
            Set(presenter, "_goldButton", Common("UI_Button_Coin"));
            PrefabUtility.SaveAsPrefabAsset(root, GachaGlobalUIPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"[GachaSetup] 골드 배너를 만들어 전역 UI에 붙였습니다 (뽑기 {gold.Pool.Count}종).");
    }

    /// <summary>
    /// 전역 UI 프리팹 안의 뽑기 화면(화면 · 확률 · 교환소 · 연출)과 HUD 뽑기 버튼만 새로 만들어 바꾼다 (같은 그리기 순서, 프레젠터 연결).
    /// Build Global UI는 정착 설정을 처음 상태로 다시 만들므로, 뽑기 화면만 고쳤을 때는 이 메뉴를 쓴다.
    /// 배치 모드: -executeMethod GachaSetup.RebuildScreens
    /// </summary>
    [MenuItem("Tools/Gacha/Rebuild Gacha Screens")]
    public static void RebuildScreens()
    {
        _titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        _bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath) ?? _titleFont;
        ImportArt();
        FillTabTitle(StandardBannerPath, "보물 조개");

        var root = PrefabUtility.LoadPrefabContents(GachaGlobalUIPath);
        try
        {
            var rootRect = (RectTransform)root.transform;
            var presenter = root.GetComponentInChildren<GachaPresenter>(true);
            if (presenter == null)
            {
                Debug.LogError("[GachaSetup] 전역 UI에 뽑기 프레젠터가 없습니다. Build Global UI를 먼저 실행하세요.");
                return;
            }

            // 예전 화면이 있던 자리(그리기 순서)를 기억하고 지움
            int first = -1;
            foreach (var name in new[] { "GachaScreen", "GachaRates", "GachaExchange", "GachaReveal" })
            {
                var old = rootRect.Find(name);
                if (old == null)
                    continue;
                if (first < 0 || old.GetSiblingIndex() < first)
                    first = old.GetSiblingIndex();
                Object.DestroyImmediate(old.gameObject);
            }
            var screens = BuildScreens(rootRect);
            if (first >= 0)
            {
                screens.screen.transform.SetSiblingIndex(first);
                screens.rates.transform.SetSiblingIndex(first + 1);
                screens.exchange.transform.SetSiblingIndex(first + 2);
                screens.reveal.transform.SetSiblingIndex(first + 3);
            }

            var hud = (RectTransform)rootRect.Find("HudSafeArea");
            var oldButton = hud.Find("GachaButton");
            int buttonIndex = oldButton != null ? oldButton.GetSiblingIndex() : -1;
            if (oldButton != null)
                Object.DestroyImmediate(oldButton.gameObject);
            var hudButton = BuildHudButton(hud);
            if (buttonIndex >= 0)
                hudButton.transform.SetSiblingIndex(buttonIndex);

            Set(presenter, "_screen", screens.screen);
            Set(presenter, "_reveal", screens.reveal);
            Set(presenter, "_rates", screens.rates);
            Set(presenter, "_exchange", screens.exchange);
            Set(presenter, "_hudButton", hudButton);
            PrefabUtility.SaveAsPrefabAsset(root, GachaGlobalUIPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[GachaSetup] 전역 UI의 뽑기 화면을 다시 만들었습니다.");
    }

    // 이미 있는 배너에 탭 이름이 비어 있으면 채움 (사람이 고친 이름은 그대로)
    private static void FillTabTitle(string path, string tabTitle)
    {
        var banner = AssetDatabase.LoadAssetAtPath<GachaBannerDefinition>(path);
        if (banner == null)
            return;
        var so = new SerializedObject(banner);
        var prop = so.FindProperty("_tabTitle");
        if (!string.IsNullOrEmpty(prop.stringValue))
            return;
        prop.stringValue = tabTitle;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddUnique(SerializedProperty list, Object value)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                return;
        }
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
    }

    #endregion

    #region 연결

    /// <summary>뽑기 매니저(기록 · 세이브)와 프레젠터(흐름)를 전역 UI 루트에 붙인다</summary>
    public static void Attach(GameObject root, Screens screens, GachaHudButtonView hudButton, CurrencyShortagePopupView shortage)
    {
        var manager = root.AddComponent<GachaManager>();
        SetArray(manager, "_banners", new Object[]
        {
            AssetDatabase.LoadAssetAtPath<GachaBannerDefinition>(PickupBannerPath),
            AssetDatabase.LoadAssetAtPath<GachaBannerDefinition>(StandardBannerPath),
            AssetDatabase.LoadAssetAtPath<GachaBannerDefinition>(GoldBannerPath),
        });
        Set(manager, "_gold", AssetDatabase.LoadAssetAtPath<Currency>(GoldPath));
        Set(manager, "_shard", AssetDatabase.LoadAssetAtPath<Currency>(ShardPath));

        var presenter = root.AddComponent<GachaPresenter>();
        Set(presenter, "_screen", screens.screen);
        Set(presenter, "_reveal", screens.reveal);
        Set(presenter, "_rates", screens.rates);
        Set(presenter, "_exchange", screens.exchange);
        Set(presenter, "_shortage", shortage);
        Set(presenter, "_hudButton", hudButton);
        Set(presenter, "_gemButton", Common("UI_Button_Shell"));
        Set(presenter, "_ticketButton", Common("UI_Button_Primary"));
        Set(presenter, "_goldButton", Common("UI_Button_Coin"));
    }

    private static void SetArray(Object target, string property, Object[] values)
    {
        var so = new SerializedObject(target);
        var list = so.FindProperty(property);
        if (list == null)
            throw new System.MissingFieldException($"{target.GetType().Name}.{property}");
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region HUD 버튼

    /// <summary>HUD 오른쪽, 안내 띠 아래 뽑기 버튼 (뽑기가 열리면 보임)</summary>
    public static GachaHudButtonView BuildHudButton(RectTransform hud)
    {
        var root = CreateRect("GachaButton", hud);
        Place(root, new Vector2(1, 1), new Vector2(-24, -318), new Vector2(150, 180));

        var body = CreateRect("Body", root);
        Stretch(body, 0);
        var circle = CreateImage("Circle", body, Common("UI_RoundButton_Featured"), true);
        Place(circle.rectTransform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(140, 140));
        var button = MakeButton(circle);
        var icon = CreateImage("Icon", circle.rectTransform, Art("Shell_Pickup"), false);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(90, 90));
        icon.preserveAspect = true;

        var tag = CreateImage("Label", body, Common("UI_Tag_Category"), false);
        Place(tag.rectTransform, new Vector2(0.5f, 0), Vector2.zero, new Vector2(120, 50));
        var text = CreateText("Text", tag.rectTransform, _titleFont, "뽑기", 28, Cocoa);
        Stretch(text.rectTransform, 0);
        text.rectTransform.offsetMin = new Vector2(0, 4);

        var badge = CreateImage("Badge", body, Common("UI_Badge"), false);
        Place(badge.rectTransform, new Vector2(1, 1), new Vector2(-8, -2), new Vector2(40, 40));

        var view = root.gameObject.AddComponent<GachaHudButtonView>();
        Set(view, "_body", body.gameObject);
        Set(view, "_button", button);
        Set(view, "_badge", badge.gameObject);
        Set(view, "_icon", icon.rectTransform);
        body.gameObject.SetActive(false);
        return view;
    }

    #endregion

    #region 화면

    /// <summary>뽑기 화면 → 확률 정보 → 교환소 → 연출 순서로 위에 그려짐. 재화 부족 팝업보다 먼저 만들어야 그 아래에 그려진다</summary>
    public static Screens BuildScreens(RectTransform canvas)
    {
        return new Screens
        {
            screen = BuildScreen(canvas),
            rates = BuildRates(canvas),
            exchange = BuildExchange(canvas),
            reveal = BuildReveal(canvas),
        };
    }

    private static GachaScreenView BuildScreen(RectTransform canvas)
    {
        var screen = CreateRect("GachaScreen", canvas);
        Stretch(screen, 0);

        // 배경 = 낮 바다 (딤 자리라 열 때 같이 나타남). 안전 영역은 투명 판으로 막아 배경을 눌러 닫히지 않게
        var dim = CreateImage("Dim", screen, null, true);
        Stretch(dim.rectTransform, 0);
        dim.color = DayWater;
        var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;
        Cover("Sea", dim.rectTransform, Art("BG_Sea_Day"));

        var safe = CreateRect("SafeArea", screen);
        Stretch(safe, 0);
        safe.gameObject.AddComponent<SafeAreaFltter>();
        var group = safe.gameObject.AddComponent<CanvasGroup>();
        var blocker = CreateImage("Blocker", safe, null, true);
        Stretch(blocker.rectTransform, 0);
        blocker.color = new Color(1, 1, 1, 0);

        var animator = screen.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", safe);
        Set(animator, "_panelGroup", group);
        Set(animator, "_dimButton", dimButton);
        var animatorSo = new SerializedObject(animator);
        animatorSo.FindProperty("_startScale").floatValue = 0.92f; // 전체 화면은 은은하게
        animatorSo.FindProperty("_overshoot").floatValue = 1.2f;
        animatorSo.FindProperty("_endScale").floatValue = 0.95f;
        animatorSo.ApplyModifiedPropertiesWithoutUndo();

        // ── 위: 제목 · 보유 뽑기권 · 조개 · 닫기
        var title = CreateImage("Title", safe, Common("UI_Ribbon_Peach"), false);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(30, -80), new Vector2(380, 100));
        var titleText = CreateText("Label", title.rectTransform, _titleFont, "보물 조개 뽑기", 40, Cocoa);
        Stretch(titleText.rectTransform, 0);
        titleText.rectTransform.offsetMin = new Vector2(0, 10);

        var (ticketIcon, ticketText) = BuildPill(safe, "TicketPill", null, 415, 220);
        BuildGemPill(safe);

        var close = CreateImage("CloseButton", safe, LoadSprite(InventorySpriteFolder, "UI_Inventory_CloseButton"), true);
        Place(close.rectTransform, new Vector2(1, 1), new Vector2(-30, -80), new Vector2(96, 96));
        close.raycastPadding = new Vector4(-16, -16, -16, -16);
        var closeButton = MakeButton(close);

        // ── 배너 탭
        var tabs = CreateRect("Tabs", safe);
        Place(tabs, new Vector2(0, 1), new Vector2(30, -200), new Vector2(1020, 110));
        var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 20;
        tabLayout.childAlignment = TextAnchor.MiddleLeft;
        tabLayout.childControlWidth = false;
        tabLayout.childControlHeight = false;
        tabLayout.childForceExpandWidth = false;
        tabLayout.childForceExpandHeight = false;
        var tabTemplate = BuildTab(tabs);

        // ── 배너 그림 (둥근 틀 안에 잘라 보임) · 기간 · 확률 UP · 이름표
        var frame = CreateImage("Banner", safe, Common("UI_Button_Paper"), false);
        Place(frame.rectTransform, new Vector2(0, 1), new Vector2(30, -326), new Vector2(1020, 690));
        var mask = CreateImage("Mask", frame.rectTransform, Common("UI_Box_Inset"), false);
        Stretch(mask.rectTransform, 12);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var art = CreateImage("Art", mask.rectTransform, Art("Banner_Pickup_Spring"), false);
        Stretch(art.rectTransform, 0);

        var period = CreateImage("Period", frame.rectTransform, Common("UI_Tag_Highlight"), false);
        Place(period.rectTransform, new Vector2(0, 1), new Vector2(30, -30), new Vector2(230, 64));
        var periodText = CreateText("Text", period.rectTransform, _titleFont, "13일 남음", 30, Color.white);
        Stretch(periodText.rectTransform, 0);
        periodText.rectTransform.offsetMin = new Vector2(0, 5);

        var rateUp = CreateImage("RateUp", frame.rectTransform, Common("UI_Tag_Level"), false);
        Place(rateUp.rectTransform, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(230, 70));
        var rateUpText = CreateText("Text", rateUp.rectTransform, _titleFont, "확률 UP!", 32, Cocoa);
        Stretch(rateUpText.rectTransform, 0);
        rateUpText.rectTransform.offsetMin = new Vector2(0, 5);

        var plate = CreateImage("TitlePlate", frame.rectTransform, Common("UI_Button_Paper"), false);
        Place(plate.rectTransform, new Vector2(0, 0), new Vector2(26, 26), new Vector2(640, 160));
        plate.color = new Color(1, 1, 1, 0.95f);
        var bannerTitle = Label("Title", plate.rectTransform, _titleFont, "봄바람 픽업", 52, Cocoa, TextAlignmentOptions.Left, 36);
        TopBand(bannerTitle.rectTransform, 30, 20, 16, 72);
        var bannerSubtitle = Label("Subtitle", plate.rectTransform, _bodyFont, "벚꽃 바람개비를 두면 한정 해달 살랑이가 와요!", 28, LightBrown,
            TextAlignmentOptions.Left, 20);
        TopBand(bannerSubtitle.rectTransform, 30, 20, 92, 48);

        // ── 눈여겨볼 장난감 (픽업 = 확률 UP, 상시 = 에픽)
        var highlights = CreateRect("Highlights", safe);
        Place(highlights, new Vector2(0, 1), new Vector2(30, -1032), new Vector2(1020, 150));
        var highlightLabel = Label("Label", highlights, _titleFont, "확률 UP!", 34, Cocoa, TextAlignmentOptions.Left, 22);
        highlightLabel.textWrappingMode = TextWrappingModes.NoWrap;
        Place(highlightLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(214, 60));
        // 다섯 칸 (픽업 3 · 상시 에픽 2 · 골드 레어 2 + 가구 3)
        var chips = new GachaToyChipView[5];
        for (int i = 0; i < chips.Length; i++)
            chips[i] = BuildToyChip(highlights, new Vector2(246 + i * 154, -7));

        // ── 아래: 천장 · 포인트/골드 뽑기 패널, 작은 버튼, 뽑기 버튼 (화면이 길면 위와 사이가 벌어짐)
        var info = CreateImage("Info", safe, Common("UI_Button_Paper"), false);
        Place(info.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 370), new Vector2(1020, 300));

        var pityText = Label("PityText", info.rectTransform, _titleFont, "에픽 확정까지 <b>60회</b>", 34, Cocoa, TextAlignmentOptions.Left);
        Place(pityText.rectTransform, new Vector2(0, 1), new Vector2(36, -24), new Vector2(600, 52));
        var pityBar = BuildBar(info.rectTransform, "PityBar", new Vector2(36, -86), new Vector2(620, 30));

        var guarantee = CreateImage("Guarantee", info.rectTransform, Common("UI_Tag_Highlight"), false);
        Place(guarantee.rectTransform, new Vector2(1, 1), new Vector2(-30, -26), new Vector2(330, 60));
        var guaranteeText = CreateText("Text", guarantee.rectTransform, _titleFont, "다음 에픽은 픽업 확정!", 24, Color.white);
        Stretch(guaranteeText.rectTransform, 0);
        guaranteeText.rectTransform.offsetMin = new Vector2(0, 4);

        var points = CreateRect("Points", info.rectTransform);
        Place(points, new Vector2(0, 1), new Vector2(36, -140), new Vector2(950, 130));
        var rewardFrame = CreateImage("RewardFrame", points, Common("UI_Button_Paper"), false);
        Place(rewardFrame.rectTransform, new Vector2(0, 1), new Vector2(0, -6), new Vector2(116, 116));
        rewardFrame.color = new Color(1f, 0.86f, 0.45f);
        var rewardIcon = CreateImage("Icon", rewardFrame.rectTransform, null, false);
        IconInFrame(rewardIcon.rectTransform, 14);
        rewardIcon.preserveAspect = true;
        var pointsText = Label("Text", points, _titleFont, "별빛 포인트 <b>0</b> / 100", 30, Cocoa, TextAlignmentOptions.Left);
        Place(pointsText.rectTransform, new Vector2(0, 1), new Vector2(136, -8), new Vector2(520, 48));
        var pointsBar = BuildBar(points, "Bar", new Vector2(136, -66), new Vector2(520, 28));
        var pointsButtonImage = CreateImage("ExchangeButton", points, Common("UI_Button_Idle"), true);
        Place(pointsButtonImage.rectTransform, new Vector2(1, 1), new Vector2(0, -14), new Vector2(250, 100));
        var pointsButton = MakeButton(pointsButtonImage);
        var pointsButtonText = CreateText("Label", pointsButtonImage.rectTransform, _titleFont, "교환", 38, Cocoa);
        Stretch(pointsButtonText.rectTransform, 0);
        pointsButtonText.rectTransform.offsetMin = new Vector2(0, 8);

        var gold = CreateRect("GoldPull", info.rectTransform);
        Place(gold, new Vector2(0, 1), new Vector2(36, -140), new Vector2(950, 130));
        var goldLabel = Label("Label", gold, _titleFont, "오늘의 골드 뽑기", 34, Cocoa, TextAlignmentOptions.Left);
        Place(goldLabel.rectTransform, new Vector2(0, 1), new Vector2(0, -12), new Vector2(600, 52));
        var goldSub = Label("Sub", gold, _bodyFont, "하루 한 번, 골드로 1회 뽑아요", 26, LightBrown, TextAlignmentOptions.Left);
        Place(goldSub.rectTransform, new Vector2(0, 1), new Vector2(0, -68), new Vector2(600, 42));
        var goldButtonImage = CreateImage("GoldButton", gold, Common("UI_Button_Coin"), true);
        Place(goldButtonImage.rectTransform, new Vector2(1, 1), new Vector2(0, -12), new Vector2(300, 104));
        var goldButton = MakeButton(goldButtonImage);
        var (_, goldCost) = BuildCostRow(goldButtonImage.rectTransform, AssetDatabase.LoadAssetAtPath<Currency>(GoldPath).Icon, 38, 0, 8);

        // 안내 줄: 포인트 · 오늘의 골드 뽑기 줄이 없는 배너(골드 조개)에서 빈자리 대신
        var note = CreateRect("Note", info.rectTransform);
        Place(note, new Vector2(0, 1), new Vector2(36, -140), new Vector2(950, 130));
        var noteFrame = CreateImage("IconFrame", note, Common("UI_Button_Paper"), false);
        Place(noteFrame.rectTransform, new Vector2(0, 1), new Vector2(0, -6), new Vector2(116, 116));
        noteFrame.color = new Color(1f, 0.86f, 0.45f);
        var noteIcon = CreateImage("Icon", noteFrame.rectTransform, AssetDatabase.LoadAssetAtPath<Currency>(GoldPath).Icon, false);
        IconInFrame(noteIcon.rectTransform, 18);
        noteIcon.preserveAspect = true;
        var noteText = Label("Text", note, _titleFont, "골드로 언제든 뽑아요", 32, Cocoa, TextAlignmentOptions.Left, 22);
        Place(noteText.rectTransform, new Vector2(0, 1), new Vector2(140, -6), new Vector2(800, 116));
        note.gameObject.SetActive(false);

        var links = CreateRect("Links", safe);
        Place(links, new Vector2(0.5f, 0), new Vector2(0, 264), new Vector2(1020, 90));
        var rates = CreateImage("RatesButton", links, Common("UI_Button_Secondary"), true);
        Place(rates.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(330, 90));
        var ratesButton = MakeButton(rates);
        var ratesText = CreateText("Label", rates.rectTransform, _titleFont, "확률 정보", 32, LightBrown);
        Stretch(ratesText.rectTransform, 0);
        ratesText.rectTransform.offsetMin = new Vector2(0, 6);

        var exchange = CreateImage("ExchangeButton", links, Common("UI_Button_Secondary"), true);
        Place(exchange.rectTransform, new Vector2(0, 0.5f), new Vector2(350, 0), new Vector2(420, 90));
        var exchangeButton = MakeButton(exchange);
        var exchangeRow = Row(exchange.rectTransform, 12);
        exchangeRow.offsetMin = new Vector2(0, 6);
        RowIcon(exchangeRow, AssetDatabase.LoadAssetAtPath<Currency>(ShardPath).Icon, 56);
        CreateText("Label", exchangeRow, _titleFont, "교환소", 32, LightBrown);
        var shardText = CreateText("Count", exchangeRow, _titleFont, "0", 32, Cocoa);

        var (singleButton, singleImage, singleIcon, singleCost) = BuildPullButton(safe, "SingleButton", "1회 뽑기", new Vector2(0, 0), new Vector2(40, 40));
        var (tenButton, tenImage, tenIcon, tenCost) = BuildPullButton(safe, "TenButton", "10회 뽑기", new Vector2(1, 0), new Vector2(-40, 40));
        var tenTag = CreateImage("Guarantee", tenButton.transform, Common("UI_Tag_Highlight"), false);
        Place(tenTag.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 24), new Vector2(270, 48));
        var tenTagText = CreateText("Text", tenTag.rectTransform, _titleFont, "레어 이상 1개 확정", 22, Color.white);
        Stretch(tenTagText.rectTransform, 0);
        tenTagText.rectTransform.offsetMin = new Vector2(0, 4);

        // ── 요정 말풍선 알림 (배너 위)
        var notice = CreateImage("Notice", safe, Common("UI_Button_Paper"), false);
        Place(notice.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -420), new Vector2(900, 160));
        var noticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
        noticeGroup.alpha = 0f;
        noticeGroup.blocksRaycasts = false;
        var fairy = CreateImage("Fairy", notice.rectTransform, ImportSprite(NpcArtFolder, "Fairy"), false);
        Place(fairy.rectTransform, new Vector2(0, 0.5f), new Vector2(-10, 10), new Vector2(130, 160));
        fairy.preserveAspect = true;
        var noticeText = Label("Text", notice.rectTransform, _bodyFont, "요정의 선물!", 32, Cocoa, TextAlignmentOptions.Center, 22);
        Stretch(noticeText.rectTransform, 0);
        noticeText.rectTransform.offsetMin = new Vector2(130, 18);
        noticeText.rectTransform.offsetMax = new Vector2(-30, -14);

        var view = screen.gameObject.AddComponent<GachaScreenView>();
        Set(view, "_animator", animator);
        Set(view, "_closeButton", closeButton);
        Set(view, "_tabParent", tabs);
        Set(view, "_tabTemplate", tabTemplate);
        Set(view, "_bannerArt", art);
        Set(view, "_titleText", bannerTitle);
        Set(view, "_subtitleText", bannerSubtitle);
        Set(view, "_periodChip", period);
        Set(view, "_periodText", periodText);
        Set(view, "_limitedChipSprite", Common("UI_Tag_Highlight"));
        Set(view, "_alwaysChipSprite", Common("UI_Tag_Category"));
        Set(view, "_rateUpBadge", rateUp.gameObject);
        Set(view, "_highlightLabel", highlightLabel);
        SetArray(view, "_highlightChips", chips);
        Set(view, "_pityText", pityText);
        Set(view, "_pityBar", pityBar);
        Set(view, "_guaranteeChip", guarantee.gameObject);
        Set(view, "_pointsGroup", points.gameObject);
        Set(view, "_pointsText", pointsText);
        Set(view, "_pointsBar", pointsBar);
        Set(view, "_pointsRewardIcon", rewardIcon);
        Set(view, "_pointsButton", pointsButton);
        Set(view, "_pointsButtonImage", pointsButtonImage);
        Set(view, "_goldGroup", gold.gameObject);
        Set(view, "_goldLabel", goldLabel);
        Set(view, "_goldButton", goldButton);
        Set(view, "_goldButtonImage", goldButtonImage);
        Set(view, "_goldCostText", goldCost);
        Set(view, "_noteGroup", note.gameObject);
        Set(view, "_noteIcon", noteIcon);
        Set(view, "_noteText", noteText);
        Set(view, "_ratesButton", ratesButton);
        Set(view, "_exchangeButton", exchangeButton);
        Set(view, "_shardText", shardText);
        Set(view, "_ticketIcon", ticketIcon);
        Set(view, "_ticketText", ticketText);
        Set(view, "_singleButton", singleButton);
        Set(view, "_singleImage", singleImage);
        Set(view, "_singleIcon", singleIcon);
        Set(view, "_singleCost", singleCost);
        Set(view, "_tenButton", tenButton);
        Set(view, "_tenImage", tenImage);
        Set(view, "_tenIcon", tenIcon);
        Set(view, "_tenCost", tenCost);
        Set(view, "_notice", noticeGroup);
        Set(view, "_noticeText", noticeText);
        Set(view, "_activeButton", Common("UI_Button_Primary"));
        Set(view, "_idleButton", Common("UI_Button_Idle"));
        Set(view, "_coinButton", Common("UI_Button_Coin"));

        screen.gameObject.SetActive(false);
        return view;
    }

    private static GachaBannerTabView BuildTab(RectTransform parent)
    {
        var background = CreateImage("Tab", parent, LoadSprite(InventorySpriteFolder, "UI_Inventory_Tab_Normal"), true);
        background.rectTransform.sizeDelta = new Vector2(500, 110);
        var button = MakeButton(background);
        var icon = CreateImage("Icon", background.rectTransform, Art("Shell_Pickup"), false);
        Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(16, 4), new Vector2(68, 68));
        icon.preserveAspect = true;
        // 탭이 셋이면 폭이 좁아짐 → 한 줄로, 넘치면 글자를 줄임
        var label = Label("Label", background.rectTransform, _titleFont, "봄바람 픽업", 34, Cocoa, TextAlignmentOptions.Left, 22);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(92, 8);
        label.rectTransform.offsetMax = new Vector2(-18, 0);
        var dot = CreateImage("NewDot", background.rectTransform, Common("UI_Badge"), false);
        Place(dot.rectTransform, new Vector2(1, 1), new Vector2(-6, -2), new Vector2(34, 34));

        var view = background.gameObject.AddComponent<GachaBannerTabView>();
        Set(view, "_button", button);
        Set(view, "_background", background);
        Set(view, "_icon", icon);
        Set(view, "_label", label);
        Set(view, "_newDot", dot.gameObject);
        Set(view, "_selectedSprite", LoadSprite(InventorySpriteFolder, "UI_Inventory_Tab_Selected"));
        Set(view, "_normalSprite", LoadSprite(InventorySpriteFolder, "UI_Inventory_Tab_Normal"));
        return view;
    }

    private static GachaToyChipView BuildToyChip(RectTransform parent, Vector2 position)
    {
        var frame = CreateImage("Chip", parent, Common("UI_Button_Paper"), false);
        Place(frame.rectTransform, new Vector2(0, 1), position, new Vector2(136, 136));
        var icon = CreateImage("Icon", frame.rectTransform, null, false);
        IconInFrame(icon.rectTransform, 16);
        icon.preserveAspect = true;
        var up = CreateImage("Up", frame.rectTransform, Common("UI_Tag_Highlight"), false);
        Place(up.rectTransform, new Vector2(1, 1), new Vector2(8, 10), new Vector2(70, 40));
        var upText = CreateText("Text", up.rectTransform, _titleFont, "UP", 22, Color.white);
        Stretch(upText.rectTransform, 0);
        upText.rectTransform.offsetMin = new Vector2(0, 3);

        var view = frame.gameObject.AddComponent<GachaToyChipView>();
        Set(view, "_frame", frame);
        Set(view, "_icon", icon);
        Set(view, "_upTag", up.gameObject);
        return view;
    }

    // [재화 아이콘 + 숫자] 칸. 아이콘이 왼쪽 끝 위로 튀어나옴 (상단바 재화 칸과 같은 모양)
    private static (Image icon, TextMeshProUGUI amount) BuildPill(RectTransform parent, string name, Sprite iconSprite, float right, float width)
    {
        var pill = CreateImage(name, parent, Common("UI_Button_Paper"), false);
        pill.pixelsPerUnitMultiplier = 1.3f;
        Place(pill.rectTransform, new Vector2(1, 1), new Vector2(-right, -92), new Vector2(width, 76));
        var amount = CreateText("Amount", pill.rectTransform, _titleFont, "0", 32, Cocoa);
        amount.alignment = TextAlignmentOptions.Right;
        amount.enableAutoSizing = true;
        amount.fontSizeMin = 22;
        amount.fontSizeMax = 32;
        Place(amount.rectTransform, new Vector2(1, 0.5f), new Vector2(-26, 4), new Vector2(width - 90, 50));
        var icon = CreateImage("Icon", pill.rectTransform, iconSprite, false);
        Place(icon.rectTransform, new Vector2(0, 1), new Vector2(-22, 8), new Vector2(84, 84));
        icon.preserveAspect = true;
        return (icon, amount);
    }

    private static void BuildGemPill(RectTransform parent)
    {
        var gem = AssetDatabase.LoadAssetAtPath<Currency>(GemPath);
        var (icon, amount) = BuildPill(parent, "GemPill", gem.Icon, 160, 230);
        var view = icon.transform.parent.gameObject.AddComponent<CurrencyAmountView>();
        Set(view, "_currency", gem);
        Set(view, "_icon", icon);
        Set(view, "_amountText", amount);
    }

    private static ProgressBarView BuildBar(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        var track = CreateImage(name, parent, Common("UI_Slider_Track"), false);
        track.pixelsPerUnitMultiplier = 2.4f; // 바가 낮아서 테두리를 얇게
        Place(track.rectTransform, new Vector2(0, 1), position, size);
        var fill = CreateImage("Fill", track.rectTransform, Common("UI_Bar_Fill"), false);
        fill.pixelsPerUnitMultiplier = 2.4f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0.5f, 1);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        var bar = track.gameObject.AddComponent<ProgressBarView>();
        Set(bar, "_fill", fill.rectTransform);
        return bar;
    }

    // 큰 뽑기 버튼: 위 "1회 뽑기", 아래 [재화 아이콘 + 값]
    private static (Button button, Image image, Image icon, TextMeshProUGUI cost) BuildPullButton(RectTransform parent, string name, string label,
        Vector2 anchor, Vector2 position)
    {
        var image = CreateImage(name, parent, Common("UI_Button_Shell"), true);
        Place(image.rectTransform, anchor, position, new Vector2(480, 200));
        var button = MakeButton(image);
        var text = CreateText("Label", image.rectTransform, _titleFont, label, 46, Cocoa);
        TopBand(text.rectTransform, 0, 0, 24, 66);
        var (icon, cost) = BuildCostRow(image.rectTransform, null, 42, 0, 0);
        TopBand((RectTransform)icon.transform.parent, 0, 0, 98, 70);
        return (button, image, icon, cost);
    }

    private static (Image icon, TextMeshProUGUI cost) BuildCostRow(RectTransform parent, Sprite iconSprite, float fontSize, float left, float bottom)
    {
        var row = Row(parent, 10);
        row.offsetMin = new Vector2(left, bottom);
        var icon = RowIcon(row, iconSprite, fontSize + 16);
        var cost = CreateText("Cost", row, _titleFont, "30", fontSize, Cocoa);
        return (icon, cost);
    }

    private static RectTransform Row(RectTransform parent, float spacing)
    {
        var row = CreateRect("Content", parent);
        Stretch(row, 0);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    private static Image RowIcon(RectTransform row, Sprite sprite, float size)
    {
        var icon = CreateImage("Icon", row, sprite, false);
        icon.preserveAspect = true;
        var element = icon.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = size;
        element.preferredHeight = size;
        return icon;
    }

    #endregion

    #region 확률 정보 · 교환소

    private static GachaRatesPopupView BuildRates(RectTransform canvas)
    {
        var (screen, panel, animator) = PopupRoot(canvas, "GachaRates", new Vector2(900, 1380));
        var titleText = Ribbon(panel, "UI_Ribbon_Lav", "확률 정보", 560);
        var closeButton = CornerClose(panel);

        var (scroll, content) = BuildScroll(panel, new Vector2(44, 44), new Vector2(-44, -112));
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 30);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var body = CreateText("Body", content, _bodyFont, "", 30, Cocoa);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 8;
        body.richText = true;

        var view = screen.gameObject.AddComponent<GachaRatesPopupView>();
        Set(view, "_animator", animator);
        Set(view, "_titleText", titleText);
        Set(view, "_bodyText", body);
        Set(view, "_scroll", scroll);
        Set(view, "_closeButton", closeButton);
        return view;
    }

    private static GachaExchangePopupView BuildExchange(RectTransform canvas)
    {
        var (screen, panel, animator) = PopupRoot(canvas, "GachaExchange", new Vector2(920, 1420));
        Ribbon(panel, "UI_Ribbon_Peach", "반짝 조각 교환소", 560);
        var closeButton = CornerClose(panel);

        var shardPill = CreateImage("Shards", panel, Common("UI_Button_Paper"), false);
        shardPill.pixelsPerUnitMultiplier = 1.3f;
        Place(shardPill.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(300, 76));
        var shardRow = Row(shardPill.rectTransform, 12);
        shardRow.offsetMin = new Vector2(0, 4);
        RowIcon(shardRow, AssetDatabase.LoadAssetAtPath<Currency>(ShardPath).Icon, 56);
        var shardText = CreateText("Count", shardRow, _titleFont, "0", 34, Cocoa);

        var note = Label("Note", panel, _bodyFont, "", 26, LightBrown, TextAlignmentOptions.Center, 20);
        TopBand(note.rectTransform, 50, 50, 180, 84);

        var (_, content) = BuildScroll(panel, new Vector2(40, 40), new Vector2(-40, -280));
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 16;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var template = BuildExchangeRow(content);

        var empty = Label("Empty", panel, _bodyFont, "바꿀 수 있는 장난감이 없어요", 30, LightBrown);
        Place(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(700, 80));

        var view = screen.gameObject.AddComponent<GachaExchangePopupView>();
        Set(view, "_animator", animator);
        Set(view, "_shardText", shardText);
        Set(view, "_rowParent", content);
        Set(view, "_rowTemplate", template);
        Set(view, "_emptyText", empty);
        Set(view, "_noteText", note);
        Set(view, "_closeButton", closeButton);
        return view;
    }

    private static GachaExchangeRowView BuildExchangeRow(RectTransform parent)
    {
        var row = CreateImage("Row", parent, Common("UI_Button_Paper"), false);
        row.rectTransform.sizeDelta = new Vector2(0, 150);
        var frame = CreateImage("Frame", row.rectTransform, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Filled"), false);
        Place(frame.rectTransform, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(120, 120));
        var icon = CreateImage("Icon", frame.rectTransform, null, false);
        Stretch(icon.rectTransform, 16);
        icon.preserveAspect = true;
        var name = Label("Name", row.rectTransform, _titleFont, "회전목마", 36, Cocoa, TextAlignmentOptions.Left, 24);
        Place(name.rectTransform, new Vector2(0, 1), new Vector2(156, -20), new Vector2(400, 56));
        var rarity = CreateImage("Rarity", row.rectTransform, Common("UI_Tag_Rarity_Epic"), false);
        Place(rarity.rectTransform, new Vector2(0, 1), new Vector2(156, -84), new Vector2(110, 46));
        var rarityText = CreateText("Text", rarity.rectTransform, _titleFont, "에픽", 24, Color.white);
        Stretch(rarityText.rectTransform, 0);
        rarityText.rectTransform.offsetMin = new Vector2(0, 4);
        var past = CreateImage("Past", row.rectTransform, Common("UI_Tag_Level"), false);
        Place(past.rectTransform, new Vector2(0, 1), new Vector2(278, -84), new Vector2(150, 46));
        var pastText = CreateText("Text", past.rectTransform, _titleFont, "지난 픽업", 22, Cocoa);
        Stretch(pastText.rectTransform, 0);
        pastText.rectTransform.offsetMin = new Vector2(0, 4);

        var buttonImage = CreateImage("Button", row.rectTransform, Common("UI_Button_Primary"), true);
        Place(buttonImage.rectTransform, new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(250, 100));
        var button = MakeButton(buttonImage);
        var costRow = Row(buttonImage.rectTransform, 8);
        costRow.offsetMin = new Vector2(0, 8);
        RowIcon(costRow, AssetDatabase.LoadAssetAtPath<Currency>(ShardPath).Icon, 50);
        var cost = CreateText("Cost", costRow, _titleFont, "300", 36, Cocoa);
        var confirm = CreateText("Confirm", buttonImage.rectTransform, _titleFont, "교환할까요?", 30, Cocoa);
        Stretch(confirm.rectTransform, 0);
        confirm.rectTransform.offsetMin = new Vector2(0, 8);
        confirm.gameObject.SetActive(false);

        var view = row.gameObject.AddComponent<GachaExchangeRowView>();
        Set(view, "_icon", icon);
        Set(view, "_nameText", name);
        Set(view, "_rarityTag", rarity);
        Set(view, "_rarityText", rarityText);
        Set(view, "_pastBadge", past.gameObject);
        Set(view, "_button", button);
        Set(view, "_buttonImage", buttonImage);
        Set(view, "_costText", cost);
        Set(view, "_confirmText", confirm);
        Set(view, "_costGroup", costRow.gameObject);
        SetArray(view, "_rarityTags", RarityTags());
        Set(view, "_affordableButton", Common("UI_Button_Primary"));
        Set(view, "_idleButton", Common("UI_Button_Idle"));
        Set(view, "_confirmButton", Common("UI_Button_Coin"));
        return view;
    }

    // 딤 + SafeArea + 가운데 패널 (닫힌 채로 시작)
    private static (RectTransform screen, RectTransform panel, UIPopupAnimator animator) PopupRoot(RectTransform canvas, string name, Vector2 size)
    {
        var screen = CreateRect(name, canvas);
        Stretch(screen, 0);
        var dim = CreateImage("Dim", screen, null, true);
        Stretch(dim.rectTransform, 0);
        dim.color = new Color(0, 0, 0, 0.6f);
        var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;

        var safe = CreateRect("SafeArea", screen);
        Stretch(safe, 0);
        safe.gameObject.AddComponent<SafeAreaFltter>();
        var panel = CreateImage("Panel", safe, LoadPanelSprite(), true);
        panel.type = Image.Type.Sliced;
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), size);
        var group = panel.gameObject.AddComponent<CanvasGroup>();

        var animator = screen.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", panel.rectTransform);
        Set(animator, "_panelGroup", group);
        Set(animator, "_dimButton", dimButton);
        screen.gameObject.SetActive(false);
        return (screen, panel.rectTransform, animator);
    }

    private static TextMeshProUGUI Ribbon(RectTransform panel, string sprite, string text, float width)
    {
        var ribbon = CreateImage("Ribbon", panel, Common(sprite), false);
        Place(ribbon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 50), new Vector2(width, 104));
        var label = CreateText("Title", ribbon.rectTransform, _titleFont, text, 44, Cocoa);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 10);
        return label;
    }

    private static Button CornerClose(RectTransform panel)
    {
        var close = CreateImage("CloseButton", panel, LoadSprite(InventorySpriteFolder, "UI_Inventory_CloseButton"), true);
        Place(close.rectTransform, new Vector2(1, 1), new Vector2(20, 34), new Vector2(96, 96));
        close.raycastPadding = new Vector4(-16, -16, -16, -16);
        return MakeButton(close);
    }

    private static (ScrollRect scroll, RectTransform content) BuildScroll(RectTransform parent, Vector2 offsetMin, Vector2 offsetMax)
    {
        var scrollImage = CreateImage("ScrollView", parent, null, true);
        scrollImage.color = new Color(1, 1, 1, 0); // 빈 곳을 끌어도 스크롤되게
        Stretch(scrollImage.rectTransform, 0);
        scrollImage.rectTransform.offsetMin = offsetMin;
        scrollImage.rectTransform.offsetMax = offsetMax;
        var viewport = CreateRect("Viewport", scrollImage.rectTransform);
        Stretch(viewport, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewport;
        scroll.content = content;
        return (scroll, content);
    }

    #endregion

    #region 연출

    // 뒤에서 앞으로: 낮 바다 → 밤바다(별) → 벚꽃잎 → 화면 탭 → 무대(1회 · 10회 · 반짝임, 흔들림) → 번쩍 → 안전 영역(카드 · 결과 · 안내 · 건너뛰기)
    private static GachaRevealView BuildReveal(RectTransform canvas)
    {
        var root = CreateRect("GachaReveal", canvas);
        Stretch(root, 0);
        var group = root.gameObject.AddComponent<CanvasGroup>();

        var day = CreateImage("Day", root, null, false);
        Stretch(day.rectTransform, 0);
        day.color = DayWater;
        Cover("Sea", day.rectTransform, Art("BG_Sea_Day"));

        var night = CreateRect("Night", root);
        Stretch(night, 0);
        var nightGroup = night.gameObject.AddComponent<CanvasGroup>();
        nightGroup.alpha = 0f;
        nightGroup.blocksRaycasts = false;
        Cover("Sea", night, Art("BG_Sea_Night"));
        // 별: 화면 위쪽 (가운데 기준 좌표로 별을 흩뿌리도록 가운데 피벗)
        var stars = CreateRect("Stars", night);
        stars.anchorMin = new Vector2(0, 1);
        stars.anchorMax = new Vector2(1, 1);
        stars.pivot = new Vector2(0.5f, 0.5f);
        stars.sizeDelta = new Vector2(0, 560);
        stars.anchoredPosition = new Vector2(0, -280);

        var petals = CreateRect("Petals", root);
        Stretch(petals, 0);

        var tap = CreateImage("TapArea", root, null, true);
        Stretch(tap.rectTransform, 0);
        tap.color = new Color(1, 1, 1, 0);
        var tapButton = tap.gameObject.AddComponent<Button>();
        tapButton.targetGraphic = tap;
        tapButton.transition = Selectable.Transition.None;

        var stage = CreateRect("Stage", root);
        Stretch(stage, 0);
        var single = BuildSingleStage(stage);
        var raft = BuildRaft(stage);
        var sparkles = CreateRect("Sparkles", stage);
        Stretch(sparkles, 0);

        var flash = CreateImage("Flash", root, null, false);
        Stretch(flash.rectTransform, 0);
        flash.color = new Color(1, 1, 1, 0);

        // 10회 결과 뒤를 어둡게 (화면 전체 — 뗏목 장난감이 결과 위로 비치지 않게). 결과가 켜고 끔
        var backdrop = CreateImage("ResultsBackdrop", root, null, false);
        Stretch(backdrop.rectTransform, 0);
        backdrop.color = new Color(0.05f, 0.08f, 0.16f, 0.55f);
        var backdropGroup = backdrop.gameObject.AddComponent<CanvasGroup>();
        backdropGroup.blocksRaycasts = false;
        backdrop.gameObject.SetActive(false);

        var safe = CreateRect("SafeArea", root);
        Stretch(safe, 0);
        safe.gameObject.AddComponent<SafeAreaFltter>();
        var card = BuildCard(safe);
        var results = BuildResults(safe, backdropGroup);

        var hint = CreateImage("TapHint", safe, Common("UI_Button_Paper"), false);
        Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 220), new Vector2(860, 110));
        hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hint.rectTransform.anchoredPosition = new Vector2(0, 275);
        hint.color = new Color(1, 1, 1, 0.95f);
        var hintText = Label("Text", hint.rectTransform, _titleFont, "톡톡! 조개를 두드려 보세요", 40, Cocoa, TextAlignmentOptions.Center, 26);
        Stretch(hintText.rectTransform, 0);
        hintText.rectTransform.offsetMin = new Vector2(30, 8);
        hintText.rectTransform.offsetMax = new Vector2(-30, 0);

        var skip = CreateImage("SkipButton", safe, Common("UI_Button_Secondary"), true);
        Place(skip.rectTransform, new Vector2(1, 1), new Vector2(-30, -60), new Vector2(220, 84));
        var skipButton = MakeButton(skip);
        var skipText = CreateText("Label", skip.rectTransform, _titleFont, "건너뛰기", 32, LightBrown);
        Stretch(skipText.rectTransform, 0);
        skipText.rectTransform.offsetMin = new Vector2(0, 6);

        var view = root.gameObject.AddComponent<GachaRevealView>();
        Set(view, "_group", group);
        Set(view, "_night", nightGroup);
        Set(view, "_starLayer", stars);
        Set(view, "_petalLayer", petals);
        Set(view, "_shakeRoot", stage);
        Set(view, "_flash", flash);
        Set(view, "_sparkleLayer", sparkles);
        Set(view, "_skipButton", skipButton);
        Set(view, "_tapArea", tapButton);
        Set(view, "_tapHintRoot", hint.rectTransform);
        Set(view, "_tapHint", hintText);

        Set(view, "_single", single.root);
        Set(view, "_otterRoot", single.otterRoot);
        Set(view, "_otter", single.otter);
        Set(view, "_shellRoot", single.shellRoot);
        Set(view, "_shellGlow", single.shellGlow);
        Set(view, "_shell", single.shell);
        Set(view, "_crackGlow", single.crackGlow);
        Set(view, "_crackCore", single.crackCore);
        Set(view, "_shellLeft", single.left);
        Set(view, "_shellRight", single.right);
        Set(view, "_water", single.water);
        Set(view, "_foam", single.foam);
        Set(view, "_toyRoot", single.toyRoot);
        Set(view, "_rays", single.rays);
        Set(view, "_toyGlow", single.toyGlow);
        Set(view, "_ring", single.ring);
        Set(view, "_toy", single.toy);
        Set(view, "_card", card);

        Set(view, "_raft", raft.root);
        SetArray(view, "_raftSlots", raft.slots);
        Set(view, "_results", results);

        Set(view, "_otterRest", Art("Otter_Float_Rest"));
        Set(view, "_otterReady", Art("Otter_Float_Ready"));
        Set(view, "_otterSurprise", Art("Otter_Float_Surprise"));
        SetArray(view, "_shellSprites", ShellLooks.Select(look => (Object)Art($"Shell_{look}")).ToArray());
        SetArray(view, "_shellLeftSprites", ShellLooks.Select(look => (Object)Art($"Shell_{look}_L")).ToArray());
        SetArray(view, "_shellRightSprites", ShellLooks.Select(look => (Object)Art($"Shell_{look}_R")).ToArray());
        SetArray(view, "_crackCores", new Object[] { Art("FX_CrackCore_1"), Art("FX_CrackCore_2"), Art("FX_CrackCore_3") });
        SetArray(view, "_crackGlows", new Object[] { Art("FX_CrackGlow_1"), Art("FX_CrackGlow_2"), Art("FX_CrackGlow_3") });
        Set(view, "_twinkle", Fx("Twinkle"));
        SetArray(view, "_petals", new Object[]
        {
            AssetDatabase.LoadAssetAtPath<Sprite>($"{PetalFolder}/FX_Petal_0.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>($"{PetalFolder}/FX_Petal_1.png"),
        });
        var so = new SerializedObject(view);
        so.FindProperty("_dayWater").colorValue = DayWater;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.gameObject.SetActive(false);
        return view;
    }

    private struct SingleStage
    {
        public RectTransform root, otterRoot, shellRoot, toyRoot;
        public Image otter, shellGlow, shell, crackGlow, crackCore, left, right, water, foam, rays, toyGlow, ring, toy;
    }

    // 1회: 장난감 빛(해달 뒤) → 해달(배 위 조개) → 물결 → 빛 고리 → 장난감
    private static SingleStage BuildSingleStage(RectTransform stage)
    {
        var s = new SingleStage();
        s.root = CreateRect("Single", stage);
        Stretch(s.root, 0);

        s.toyGlow = CreateImage("ToyGlow", s.root, Fx("Glow"), false);
        Center(s.toyGlow.rectTransform, 0, 330, 760, 760);
        s.toyGlow.color = new Color(1, 1, 1, 0);
        s.rays = CreateImage("Rays", s.root, Fx("Rays"), false);
        Center(s.rays.rectTransform, 0, 330, 1250, 1250);
        s.rays.gameObject.SetActive(false);

        s.otterRoot = CreateRect("Otter", s.root);
        Center(s.otterRoot, 0, -150, 1000, 667);
        s.otter = CreateImage("Image", s.otterRoot, Art("Otter_Float_Rest"), false);
        Stretch(s.otter.rectTransform, 0);
        s.otter.preserveAspect = true;

        // 배 위 조개 (해달과 함께 둥실). 금 · 빛 · 반쪽은 조개 그림과 같은 캔버스라 그대로 겹침
        s.shellRoot = CreateRect("Shell", s.otterRoot);
        Center(s.shellRoot, 40, -40, 230, 230);
        s.shellGlow = CreateImage("Glow", s.shellRoot, Fx("Glow"), false);
        Center(s.shellGlow.rectTransform, 0, 0, 430, 430);
        s.shellGlow.gameObject.SetActive(false);
        s.shell = CreateImage("Image", s.shellRoot, Art("Shell_Common"), false);
        Stretch(s.shell.rectTransform, 0);
        s.crackGlow = CreateImage("CrackGlow", s.shellRoot, Art("FX_CrackGlow_1"), false);
        Stretch(s.crackGlow.rectTransform, 0);
        s.crackGlow.gameObject.SetActive(false);
        s.crackCore = CreateImage("Crack", s.shellRoot, Art("FX_CrackCore_1"), false);
        Stretch(s.crackCore.rectTransform, 0);
        s.crackCore.gameObject.SetActive(false);
        s.left = CreateImage("Left", s.shellRoot, Art("Shell_Common_L"), false);
        Center(s.left.rectTransform, 0, 0, 230, 230);
        s.left.gameObject.SetActive(false);
        s.right = CreateImage("Right", s.shellRoot, Art("Shell_Common_R"), false);
        Center(s.right.rectTransform, 0, 0, 230, 230);
        s.right.gameObject.SetActive(false);
        s.shellRoot.gameObject.SetActive(false);

        // 해달 앞 물결: 아랫몸을 덮어 물에 떠 있게 (거품 줄은 물결 위 가장자리에 맞춤)
        s.water = CreateImage("Water", s.root, Art("FX_WaterBody"), false);
        Center(s.water.rectTransform, 0, -420, 1800, 330);
        s.water.color = DayWater;
        s.foam = CreateImage("Foam", s.root, Art("FX_WaterFoam"), false);
        Center(s.foam.rectTransform, 0, -296, 1800, 81);

        s.ring = CreateImage("Ring", s.root, Art("FX_Ring"), false);
        Center(s.ring.rectTransform, 0, 0, 400, 400);
        s.ring.gameObject.SetActive(false);

        s.toyRoot = CreateRect("Toy", s.root);
        Center(s.toyRoot, 0, 330, 480, 480);
        s.toy = CreateImage("Image", s.toyRoot, null, false);
        Stretch(s.toy.rectTransform, 0);
        s.toy.preserveAspect = true;
        s.toyRoot.gameObject.SetActive(false);

        s.root.gameObject.SetActive(false);
        return s;
    }

    // 10회 뗏목: 뒤 3마리 → 가운데 4마리 → 앞 3마리 (뒤가 먼저 그려짐) → 앞 줄 아래를 덮는 물결
    private static readonly Vector2[] RaftSpots =
    {
        new Vector2(-300, 250), new Vector2(0, 250), new Vector2(300, 250),
        new Vector2(-383, 15), new Vector2(-128, 15), new Vector2(128, 15), new Vector2(383, 15),
        new Vector2(-300, -220), new Vector2(0, -220), new Vector2(300, -220),
    };

    private static (RectTransform root, Object[] slots) BuildRaft(RectTransform stage)
    {
        var raft = CreateRect("Raft", stage);
        Stretch(raft, 0);
        var slots = new Object[RaftSpots.Length];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = BuildRaftSlot(raft, i, RaftSpots[i]);
        var water = CreateImage("Water", raft, Art("FX_WaterBody"), false);
        Center(water.rectTransform, 0, -412, 1800, 330);
        water.color = DayWater;
        var foam = CreateImage("Foam", raft, Art("FX_WaterFoam"), false);
        Center(foam.rectTransform, 0, -288, 1800, 81);
        raft.gameObject.SetActive(false);
        return (raft, slots);
    }

    private static GachaRaftSlotView BuildRaftSlot(RectTransform raft, int index, Vector2 position)
    {
        const float scale = 0.34f; // 1회 해달(1000) 대비
        var slot = CreateRect($"Slot{index}", raft);
        Center(slot, position.x, position.y, 340, 320);
        var body = CreateRect("Body", slot);
        Center(body, 0, 0, 340, 227);
        var otter = CreateImage("Otter", body, Art("Otter_Float_Rest"), false);
        Stretch(otter.rectTransform, 0);
        otter.preserveAspect = true;
        var glow = CreateImage("Glow", body, Fx("Glow"), false);
        Center(glow.rectTransform, 40 * scale, -40 * scale, 190, 190);
        glow.gameObject.SetActive(false);
        var shell = CreateImage("Shell", body, Art("Shell_Common"), false);
        Center(shell.rectTransform, 40 * scale, -40 * scale, 230 * scale * 1.2f, 230 * scale * 1.2f);
        var toy = CreateImage("Toy", slot, null, false);
        Center(toy.rectTransform, 0, 150, 130, 130);
        toy.preserveAspect = true;
        toy.gameObject.SetActive(false);
        var badge = CreateImage("New", slot, Common("UI_Tag_Highlight"), false);
        Center(badge.rectTransform, 70, 205, 84, 36);
        var badgeText = CreateText("Text", badge.rectTransform, _titleFont, "NEW", 22, Color.white);
        Stretch(badgeText.rectTransform, 0);
        badgeText.rectTransform.offsetMin = new Vector2(0, 3);
        badge.gameObject.SetActive(false);

        var view = slot.gameObject.AddComponent<GachaRaftSlotView>();
        Set(view, "_body", body);
        Set(view, "_otter", otter);
        Set(view, "_glow", glow);
        Set(view, "_shell", shell);
        Set(view, "_toy", toy);
        Set(view, "_newBadge", badge.gameObject);
        return view;
    }

    // 1회 결과 카드 (아래에서 올라옴)
    private static GachaResultCardView BuildCard(RectTransform safe)
    {
        var root = CreateRect("Card", safe);
        Stretch(root, 0);
        var panel = CreateImage("Panel", root, LoadPanelSprite(), true);
        panel.type = Image.Type.Sliced;
        Place(panel.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1000, 600));
        var group = panel.gameObject.AddComponent<CanvasGroup>();

        var (rarityTag, rarityText) = Tag(panel.rectTransform, "Rarity", Common("UI_Tag_Rarity_Epic"), "에픽", Color.white, new Vector2(40, -34), new Vector2(150, 60), 30);
        var (newBadge, _) = Tag(panel.rectTransform, "New", Common("UI_Tag_Highlight"), "NEW", Color.white, new Vector2(204, -34), new Vector2(120, 60), 30);
        var (featuredBadge, _) = Tag(panel.rectTransform, "Featured", Common("UI_Tag_Level"), "픽업", Cocoa, new Vector2(338, -34), new Vector2(140, 60), 30);

        var name = Label("Name", panel.rectTransform, _titleFont, "벚꽃 바람개비", 58, Cocoa, TextAlignmentOptions.Center, 40);
        TopBand(name.rectTransform, 40, 40, 108, 76);
        var description = Label("Description", panel.rectTransform, _bodyFont, "봄바람에 빙글빙글 도는 벚꽃 바람개비.", 30, Body, TextAlignmentOptions.Center, 22);
        TopBand(description.rectTransform, 60, 60, 190, 90);

        var hintRow = CreateRect("Hint", panel.rectTransform);
        TopBand(hintRow, 60, 60, 288, 100);
        var portraitFrame = CreateImage("PortraitFrame", hintRow, Common("UI_RoundButton_Peach"), false);
        Place(portraitFrame.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(100, 100));
        var portrait = CreateImage("Portrait", portraitFrame.rectTransform, null, false);
        Stretch(portrait.rectTransform, 8);
        portrait.preserveAspect = true;
        var hint = Label("Text", hintRow, _titleFont, "광장에 두면 해달 친구가 놀러 와요", 32, Cocoa, TextAlignmentOptions.Center, 22);
        Stretch(hint.rectTransform, 0);
        hint.rectTransform.offsetMin = new Vector2(110, 0);

        var shards = Label("Shards", panel.rectTransform, _bodyFont, "", 26, LightBrown);
        TopBand(shards.rectTransform, 40, 40, 396, 40);

        var (okButton, againButton, againImage, againIcon, againLabel, againCost) = ResultButtons(panel.rectTransform);

        var view = root.gameObject.AddComponent<GachaResultCardView>();
        Set(view, "_panel", panel.rectTransform);
        Set(view, "_group", group);
        Set(view, "_rarityTag", rarityTag);
        Set(view, "_rarityText", rarityText);
        Set(view, "_newBadge", newBadge.gameObject);
        Set(view, "_featuredBadge", featuredBadge.gameObject);
        SetArray(view, "_rarityTags", RarityTags());
        Set(view, "_nameText", name);
        Set(view, "_descriptionText", description);
        Set(view, "_hintText", hint);
        Set(view, "_hintPortrait", portrait);
        Set(view, "_hintPortraitFrame", portraitFrame.gameObject);
        Set(view, "_shardText", shards);
        Set(view, "_okButton", okButton);
        Set(view, "_againButton", againButton);
        Set(view, "_againImage", againImage);
        Set(view, "_againIcon", againIcon);
        Set(view, "_againLabel", againLabel);
        Set(view, "_againCost", againCost);
        root.gameObject.SetActive(false);
        return view;
    }

    // 10회 결과: 10칸 (5 × 2) + 요약 + 버튼
    private static GachaResultsView BuildResults(RectTransform safe, CanvasGroup backdrop)
    {
        var root = CreateRect("Results", safe);
        Stretch(root, 0);
        var panel = CreateImage("Panel", root, LoadPanelSprite(), true);
        panel.type = Image.Type.Sliced;
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1000, 920));
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        Ribbon(panel.rectTransform, "UI_Ribbon_Peach", "10회 결과", 440);

        var grid = CreateRect("Grid", panel.rectTransform);
        TopBand(grid, 40, 40, 96, 500);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(170, 236);
        layout.spacing = new Vector2(12, 20);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        layout.childAlignment = TextAnchor.UpperCenter;
        var cells = new Object[10];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = BuildResultCell(grid, i);

        var summary = Label("Summary", panel.rectTransform, _titleFont, "새 장난감 0개", 34, Cocoa);
        TopBand(summary.rectTransform, 40, 40, 610, 56);

        var (okButton, againButton, againImage, againIcon, againLabel, againCost) = ResultButtons(panel.rectTransform);

        var view = root.gameObject.AddComponent<GachaResultsView>();
        Set(view, "_panel", panel.rectTransform);
        Set(view, "_group", group);
        SetArray(view, "_cells", cells);
        Set(view, "_summaryText", summary);
        Set(view, "_backdrop", backdrop);
        Set(view, "_okButton", okButton);
        Set(view, "_againButton", againButton);
        Set(view, "_againImage", againImage);
        Set(view, "_againIcon", againIcon);
        Set(view, "_againLabel", againLabel);
        Set(view, "_againCost", againCost);
        root.gameObject.SetActive(false);
        return view;
    }

    private static GachaResultCellView BuildResultCell(RectTransform grid, int index)
    {
        var cell = CreateRect($"Cell{index}", grid);
        var frame = CreateImage("Frame", cell, Common("UI_Button_Paper"), false);
        Place(frame.rectTransform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(170, 170));
        var icon = CreateImage("Icon", frame.rectTransform, null, false);
        IconInFrame(icon.rectTransform, 20);
        icon.preserveAspect = true;
        var (newBadge, _) = Tag(frame.rectTransform, "New", Common("UI_Tag_Highlight"), "NEW", Color.white, new Vector2(-6, 10), new Vector2(80, 36), 20);
        var (featured, _) = Tag(frame.rectTransform, "Featured", Common("UI_Tag_Level"), "픽업", Cocoa, new Vector2(96, 10), new Vector2(80, 36), 20);
        var name = Label("Name", cell, _bodyFont, "회전목마", 24, Cocoa, TextAlignmentOptions.Center, 16);
        name.textWrappingMode = TextWrappingModes.NoWrap;
        TopBand(name.rectTransform, -6, -6, 178, 40);

        var view = cell.gameObject.AddComponent<GachaResultCellView>();
        Set(view, "_frame", frame);
        Set(view, "_icon", icon);
        Set(view, "_newBadge", newBadge.gameObject);
        Set(view, "_featuredBadge", featured.gameObject);
        Set(view, "_nameText", name);
        return view;
    }

    // [확인] (크림) · [한 번 더 + 값] (값 치르는 방법 색)
    private static (Button ok, Button again, Image againImage, Image againIcon, TextMeshProUGUI againLabel, TextMeshProUGUI againCost)
        ResultButtons(RectTransform panel)
    {
        var ok = CreateImage("OkButton", panel, Common("UI_Button_Secondary"), true);
        Place(ok.rectTransform, new Vector2(0, 0), new Vector2(60, 44), new Vector2(400, 124));
        var okButton = MakeButton(ok);
        var okText = CreateText("Label", ok.rectTransform, _titleFont, "확인", 44, LightBrown);
        Stretch(okText.rectTransform, 0);
        okText.rectTransform.offsetMin = new Vector2(0, 8);

        var again = CreateImage("AgainButton", panel, Common("UI_Button_Shell"), true);
        Place(again.rectTransform, new Vector2(1, 0), new Vector2(-60, 44), new Vector2(400, 124));
        var againButton = MakeButton(again);
        var againLabel = CreateText("Label", again.rectTransform, _titleFont, "한 번 더", 34, Cocoa);
        TopBand(againLabel.rectTransform, 0, 0, 12, 50);
        var (icon, cost) = BuildCostRow(again.rectTransform, null, 34, 0, 0);
        TopBand((RectTransform)icon.transform.parent, 0, 0, 60, 50);
        return (okButton, againButton, again, icon, againLabel, cost);
    }

    private static (Image tag, TextMeshProUGUI text) Tag(RectTransform parent, string name, Sprite sprite, string text, Color color,
        Vector2 topLeft, Vector2 size, float fontSize)
    {
        var tag = CreateImage(name, parent, sprite, false);
        Place(tag.rectTransform, new Vector2(0, 1), topLeft, size);
        var label = CreateText("Text", tag.rectTransform, _titleFont, text, fontSize, color);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 4);
        return (tag, label);
    }

    private static Object[] RarityTags() => new Object[]
    {
        Common("UI_Tag_Rarity_Common"), Common("UI_Tag_Rarity_Rare"), Common("UI_Tag_Rarity_Epic"),
    };

    #endregion

    #region 도우미

    private static Sprite Common(string name) => LoadSprite(CommonSpriteFolder, name);

    private static Sprite Art(string name) => ImportSprite(GachaArtFolder, name);

    private static Sprite Fx(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{CelebrateFolder}/{name}.png");

    private static Button MakeButton(Image image)
    {
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center, float minSize = 0f)
    {
        var label = CreateText(name, parent, font, text, size, color);
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal; // 코드로 만든 글은 기본이 줄바꿈 안 함
        if (minSize > 0f)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = minSize;
            label.fontSizeMax = size;
        }
        return label;
    }

    // 종이 칸(UI_Button_Paper) 안 아이콘: 아래쪽 입체 턱만큼 위로
    private static void IconInFrame(RectTransform icon, float inset)
    {
        Stretch(icon, inset);
        icon.offsetMin = new Vector2(inset, inset + 6);
    }

    private static void Center(RectTransform rect, float x, float y, float width, float height) =>
        Place(rect, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(width, height));

    // 화면을 꽉 채우는 배경 (비율을 지키며 넘치는 쪽은 잘림)
    private static void Cover(string name, RectTransform parent, Sprite sprite)
    {
        var image = CreateImage(name, parent, sprite, false);
        var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite != null ? sprite.rect.width / sprite.rect.height : 1f;
    }

    #endregion
}
