using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static GlobalUISetup;

/// <summary>
/// 도감 데이터(탭·항목·DB)와 화면(CollectionScreen)을 만든다. GlobalUISetup.Run이 함께 호출한다.
/// 이미 있는 데이터 에셋은 사람이 고친 문구를 지키기 위해 덮어쓰지 않고, 비어 있는 그림만 채운다.
/// </summary>
public static class CollectionSetup
{
    private const string DataFolder = "Assets/Scriptable Obejects/Collection";
    internal const string DatabasePath = DataFolder + "/CollectionDatabase.asset";
    private const string TabPrefabPath = "Assets/Prefab/Collection/CollectionTab.prefab";
    private const string SlotPrefabPath = "Assets/Prefab/Collection/CollectionSlot.prefab";
    internal const string CollectionSpriteFolder = "Assets/Art/UI/Collection";
    private const string SilhouetteFolder = "Assets/Art/UI/Collection/Silhouettes";
    internal const string OtterFolder = "Assets/Art/Otter";
    private const string ItemFolder = "Assets/Scriptable Obejects/Inventory/Items";
    private const string BordersJsonPath = "Tools/UIGen/kit_borders.json";

    private static readonly Color BadgeGreen = new Color32(0x3F, 0x6B, 0x38, 0xFF);

    // (에셋, ID, 이름, 정렬, 순서, 획득 장소 등 제목, 방문 흔적, 범례 실루엣) — 문구: 수집/미획득/획득 완료
    private static readonly (string asset, string id, string name, int order, string count, string unknown, string collected, string extra, bool visits, string legendIcon)[] Tabs =
    {
        ("Tab_Vegetable", "Vegetable", "채소", 0, "수집", "미획득", "획득 완료", "획득 장소", false, "SIL_Potato"),
        ("Tab_Fish", "Fish", "어류", 1, "수집", "미획득", "획득 완료", "획득 장소", false, "SIL_Mackerel"),
        // 광물 탭 항목(Entry_Ore* · Entry_Relic*)은 에셋으로만 있음 (광산 · 오래된 갱도에서 캐면 자동 획득)
        ("Tab_Mineral", "Mineral", "광물", 2, "수집", "미획득", "획득 완료", "획득 장소", false, "SIL_Ore_Stone"),
        ("Tab_Otter", "Otter", "해달", 3, "등록", "미발견", "등록 완료", "좋아하는 것", true, "SIL_Otter"),
    };

    // (에셋, ID, 탭, 이름(비우면 아이템 이름), 연결 아이템 경로, 그림(비우면 아이템 아이콘), 실루엣, 소개, 설명, 추가 값)
    private static readonly (string asset, string id, string tab, string name, string item, string portrait, string silhouette, string tagline, string description, string extra)[] Entries =
    {
        ("Entry_Carrot", "crop_carrot", "Tab_Vegetable", "", "Farming/당근", "", "SIL_Carrot",
            "밭에서 자라는 아삭한 채소.", "달콤하고 아삭해서 해달들이 좋아해요.", "밭"),
        ("Entry_Potato", "crop_potato", "Tab_Vegetable", "", "Farming/감자", "", "SIL_Potato",
            "흙 속에서 동글동글 자라는 채소.", "포슬포슬하게 삶으면 해달들이 줄을 서요.", "밭"),
        ("Entry_SweetPotato", "crop_sweet_potato", "Tab_Vegetable", "", "Farming/고구마", "", "SIL_SweetPotato",
            "달콤한 보랏빛 뿌리채소.", "구우면 꿀처럼 달콤해져요.", "밭"),
        ("Entry_Cucumber", "crop_cucumber", "Tab_Vegetable", "", "Farming/오이", "", "SIL_Cucumber",
            "시원하고 아삭한 여름 채소.", "더운 날 해달들의 간식이에요.", "밭"),
        ("Entry_Strawberry", "crop_strawberry", "Tab_Vegetable", "", "Farming/딸기", "", "SIL_Strawberry",
            "새콤달콤한 빨간 열매.", "잘 익은 딸기를 보면 해달들이 서로 먹겠다고 다퉈요.", "밭"),
        ("Entry_Mackerel", "fish_mackerel", "Tab_Fish", "", "Fishing/고등어", "", "SIL_Mackerel",
            "푸른 줄무늬를 가진 바다 물고기.", "은빛 배를 반짝이며 바닷속을 헤엄쳐요.", "바다 낚시터"),
        ("Entry_OtterFarmer", "otter_farmer", "Tab_Otter", "농부 해달", "", "ICON_Otter_Farmer", "SIL_Otter",
            "밭을 돌보는 부지런한 친구", "작은 새싹을 보면 그냥 지나치지 못해요.", "당근"),
        ("Entry_OtterFisher", "otter_fisher", "Tab_Otter", "낚시꾼 해달", "", "ICON_Otter_Cast_Fisher", "SIL_Otter",
            "낚시를 좋아하는 느긋한 친구", "파도 소리를 들으며 찌가 움직이길 기다려요.", "고등어"),
        // 광부 해달: 광장에서 만나 광산에 배치하면 등록 (농부 해달도 같음)
        ("Entry_OtterMiner", "otter_miner", "Tab_Otter", "광부 해달", "", "ICON_Otter_Cast_Miner", "SIL_Otter",
            "광산을 지키는 듬직한 친구", "곡괭이 소리만 들어도 신이 나요.", "다이아몬드"),
        // 게시판 해달 (P2): 광장에서 만나 게시판 관리를 맡기면 등록. 그림은 임시 (SettlementSetup.P2가 만듦)
        ("Entry_OtterClerk", "otter_receptionist", "Tab_Otter", "게시판 해달", "", "ICON_Otter_Cast_Sailor", "SIL_Otter",
            "게시판을 맡은 꼼꼼한 친구", "부탁을 받으면 또박또박 적어 둬요.", "게시판"),
    };

    private const string StoryFolder = DataFolder + "/Stories";
    // 컷 그림: CUT_{항목ID}_{A,B,C}.png (도감_이벤트컷씬_기획서 5장). 없으면 항목 그림으로 임시 컷
    internal const string CutsceneFolder = "Assets/Art/Collection/Cutscenes";

    // 레벨로 해금하는 항목 (기획서 1.1). 지금은 없음: 해달은 정착 진행에서 만나거나 배치할 때
    // SettlementManager가 등록한다 (농부는 밭 개간 뒤, 낚시꾼은 Lv.15 선착장 뒤)
    private static readonly Dictionary<string, int> UnlockLevels = new Dictionary<string, int>();

    // (항목 에셋, 컷 수, 대사(컷 번호, 문장)) — 기획서 4장. {이름}은 플레이어 이름
    private static readonly (string entry, int cuts, (int cut, string text)[] lines)[] Stories =
    {
        ("Entry_Carrot", 2, new[]
        {
            (0, "{이름}님, {이름}님! 이것 좀 보세요!"),
            (0, "영차~ 하고 당겼더니 쑥 뽑혔어요. 제 키만 해요!"),
            (1, "…딱 한 입만 먹어 봐도 될까요? 진짜 딱 한 입이요."),
        }),
        ("Entry_Potato", 2, new[]
        {
            (0, "어? 하나 캤는데… 또 나와요!"),
            (1, "하나, 둘, 셋, 넷… 세다가 까먹었어요."),
            (1, "괜찮아요. 동글동글한 건 다 귀여우니까요!"),
        }),
        ("Entry_SweetPotato", 3, new[]
        {
            (0, "후~ 후~ 앗, 뜨거!"),
            (1, "껍질을 벗기면요… 노란 속이… 짠…"),
            (2, "{이름}님 몫은 제일 큰 걸로 남겨 뒀어요. 진짜예요!"),
        }),
        ("Entry_Cucumber", 2, new[]
        {
            (0, "아~ 시원해…"),
            (1, "오이는요, 먹기 전에 볼에 한 번 대 보는 거예요."),
            (1, "해달 규칙이에요. 방금 제가 만들었어요!"),
        }),
        // 작물 11종 (도감_컷씬_일러스트_프롬프트 7장). 말하는 해달은 농부 해달, 딸기 · 황금 감자는 낚시꾼 해달이 말없이 함께 나옴
        ("Entry_Radish", 2, new[]
        {
            (0, "무를 숭덩숭덩 썰어서 보글보글~"),
            (1, "국은요, 숟가락 말고 그릇째 마시는 거예요!"),
            (1, "카아~ 시원하다! {이름}님 것도 떠 올게요!"),
        }),
        ("Entry_Raspberry", 2, new[]
        {
            (0, "산딸기는요, 손가락에 쏙 끼워서 먹어요!"),
            (1, "냠! …으으읍! 새, 새콤해요!"),
            (1, "입이 안 펴져요… 그래도 하나만 더요!"),
        }),
        ("Entry_Taro", 2, new[]
        {
            (0, "얘도 털옷을 입었네요! 저처럼요!"),
            (0, "털 고르기 해 줄게요. 슥슥, 슥슥…"),
            (1, "앗, 삶았더니 미끄러워요! 거기 서요!"),
        }),
        ("Entry_Zucchini", 2, new[]
        {
            (0, "애호박이에요! 길쭉한 게 꼭 방망이 같아요!"),
            (1, "얇게 썰어서 지글지글~ 냄새 좋죠?"),
            (1, "어? 광장 해달들이 다 모였어요! 줄 서세요~"),
        }),
        ("Entry_Beet", 2, new[]
        {
            (0, "으앗! {이름}님, 제 손이 빨개졌어요!"),
            (1, "물에 박박 씻어도… 안 지워져요."),
            (1, "괜찮아요. 오늘은 빨간 손 해달 할래요!"),
        }),
        ("Entry_PurplePotato", 2, new[]
        {
            (0, "어? 삶았는데도 보라색이에요!"),
            (1, "먹으면 저도 보라색이 될까요? 메롱~"),
            (1, "…혀만 보라색이 됐어요. 반만 성공이에요!"),
        }),
        ("Entry_Strawberry", 2, new[]
        {
            (0, "제일 빨간 딸기! 제가 먼저 찜했어요!"),
            (1, "…반으로 나누면 둘 다 먼저 찜한 거예요."),
            (1, "음~ 반쪽짜리가 제일 달아요!"),
        }),
        ("Entry_WhiteStrawberry", 2, new[]
        {
            (0, "하얀 딸기는… 아직 덜 익은 거 아니에요?"),
            (1, "한 입만… 냠. …!!"),
            (1, "{이름}님! 하얀 게 더 달아요! 속았어요!"),
        }),
        ("Entry_Blueberry", 2, new[]
        {
            (0, "{이름}님! 덤불에서 보석을 찾았어요!"),
            (1, "배 위에 올려놓고 하나씩… 냠, 냠."),
            (1, "어? 입이 파래졌다고요? 보석 먹어서 그래요!"),
        }),
        ("Entry_GoldenCarrot", 2, new[]
        {
            (0, "우와앗, 눈부셔요! 당근이 빛나요!"),
            (1, "아삭! …방금 제 입에서도 빛났죠? 그렇죠?"),
            (1, "{이름}님도 한 입… 아, 다 먹었네요. 헤헤."),
        }),
        ("Entry_GoldenPotato", 2, new[]
        {
            (0, "쉿! {이름}님, 흙 속에서 뭔가 빛나요…!"),
            (0, "살살… 살살… 황금 감자예요!"),
            (1, "이런 날은 다 같이 손잡고 춤추는 거예요!"),
        }),
        ("Entry_Mackerel", 2, new[]
        {
            (0, "첨벙… 잡았어요오…"),
            (1, "배 위에 올려 두면요… 식탁이 돼요."),
            (1, "오늘 바다는… 기분이 좋은가 봐요. 반짝반짝…"),
        }),
        ("Entry_OtterFarmer", 2, new[]
        {
            (0, "처음 뵙겠습니다, {이름}님! 오늘부터 밭을 맡은 해달이에요."),
            (1, "새싹 하나도 그냥 안 지나칠게요. 맡겨 주세요!"),
            (1, "…그런데 당근은 가끔 제가 먹어도 되죠? 가끔이요!"),
        }),
        // C컷 = A컷에서 잠든 모습 (마지막 줄 "쿨…")
        ("Entry_OtterFisher", 3, new[]
        {
            (0, "아… {이름}님이다… 안녕하세요오…"),
            (1, "이건요… 제일 좋아하는 돌이에요. 겨드랑이에 넣어 다녀요."),
            (2, "물결 소리 듣다 보면… 물고기가 먼저 와요… 쿨…"),
        }),
        // 해달 20마리 (컷: Tools/StoryCuts/build.py — 광장 리그 모습 + 게임 그림으로 구성)
        ("Entry_OtterFirst", 2, new[]
        {
            (0, "{이름}님! 제가 이 섬에 제일 먼저 온 해달이에요."),
            (0, "그땐 광산도 밭도 선착장도 다 수풀 속에 숨어 있었어요."),
            (1, "이젠 길마다 친구가 있네요. 가방 속 지도도 새로 그려야겠어요!"),
        }),
        ("Entry_OtterPainter", 2, new[]
        {
            (0, "쉿, {이름}님. 지금 광장을 그리는 중이에요."),
            (0, "하늘색이 모자라서… 바다를 조금 찍어 발랐어요."),
            (1, "짠! 제목은 '우리 왕국'. 구석에 작게 그린 게 저예요!"),
        }),
        ("Entry_OtterSleepy", 2, new[]
        {
            (0, "하암… {이름}님 안녕하세요… 오늘은 나무 세 그루 벨 거예요…"),
            (1, "한 그루 베고… 벤치에서 잠깐만… 아주 잠깐만…"),
            (1, "…쿨… 꿈에서 두 그루 더 벴어요… 쿨…"),
        }),
        ("Entry_OtterClerk", 2, new[]
        {
            (0, "{이름}님, 오늘 들어온 부탁이에요. 하나, 둘… 일곱 장!"),
            (0, "글씨는 또박또박, 순서는 차곡차곡. 그게 제 일이에요."),
            (1, "…근데 이 쪽지는 누가 썼죠? '당근 더 주세요'… 아, 새싹이구나."),
        }),
        ("Entry_OtterNeighbor", 2, new[]
        {
            (0, "영차… 이삿짐은 이게 다예요. 상자 속엔 제일 아끼는 이불이 있어요!"),
            (1, "새 집에 불을 켰더니… 햇살 냄새가 나요."),
            (1, "{이름}님, 이웃이 돼 줘서 고마워요. 오늘 밤은 포근하게 잘 수 있겠어요."),
        }),
        ("Entry_OtterMiner", 2, new[]
        {
            (0, "깡! 깡! {이름}님, 이 소리 들려요? 돌이 대답하는 소리예요."),
            (0, "반짝이는 걸 찾으면 꼭 {이름}님께 먼저 보여 드리고 싶었어요."),
            (1, "다이아몬드…인 줄 알았는데 조약돌이네요. 그래도 반짝이죠?"),
        }),
        ("Entry_OtterBuilder", 2, new[]
        {
            (0, "뚝딱뚝딱! {이름}님, 재료만 주시면 뭐든 지어요!"),
            (0, "기둥 세우고, 지붕 얹고… 마지막 못 하나는 꼭 제가 박아요."),
            (1, "완성! …어, 굴뚝이 조금 기울었나? 바람 따라 멋 부린 거예요!"),
        }),
        ("Entry_OtterPhotographer", 3, new[]
        {
            (0, "{이름}님, 거기 서 보세요. 아니, 조금만 왼쪽으로…"),
            (1, "자, 웃어요! 하나, 둘… 찰칵!"),
            (2, "음… 눈을 감으셨네요. 괜찮아요, 이것도 좋은 순간이에요!"),
        }),
        ("Entry_OtterToyBodeul", 2, new[]
        {
            (0, "새싹은요, 쓰다듬어 주면 쑥쑥 자라요. 진짜예요."),
            (1, "보들보들… 착하지… {이름}님도 해 볼래요?"),
            (1, "봐요, 방금 조금 컸죠? …바람 때문 아니에요!"),
        }),
        ("Entry_OtterToyDegul", 2, new[]
        {
            (0, "{이름}님, 오늘의 조약돌이에요! 바닷가에서 주웠어요."),
            (1, "이건 동글동글, 이건 울퉁불퉁, 이건… 반짝반짝!"),
            (1, "제일 반짝이는 건 {이름}님 거예요. 데굴데굴 굴려서 드릴게요~"),
        }),
        ("Entry_OtterToyDuribeon", 2, new[]
        {
            (0, "두리번두리번… 저 숲 너머에 새 땅이 있다고 들었어요!"),
            (1, "지도엔 아직 빈 곳이 많아요. 그래서 제가 채우러 왔죠."),
            (1, "{이름}님, 다음에 땅 넓힐 땐 꼭 불러 주세요. 길은 제가 찾을게요!"),
        }),
        ("Entry_OtterToyJaejal", 2, new[]
        {
            (0, "있잖아요, {이름}님! 그거 들었어요? 상점에 새 물건 들어온대요!"),
            (1, "공동 식탁에 앉으면요, 마을 소식이 다 들려요."),
            (1, "…근데 방금 무슨 얘기 하려고 했더라? 아, 맞다! 또 놀러 와요!"),
        }),
        ("Entry_OtterToyJjalrang", 2, new[]
        {
            (0, "짤랑짤랑~ 이 소리 좋죠? 오늘 첫 손님은 {이름}님이에요!"),
            (1, "좋은 물건은 제값에! …라고 하고 싶지만 {이름}님은 단골이니까요."),
            (1, "덤으로 반짝이 목걸이도 하나! 장사는 웃는 얼굴이 남는 거예요."),
        }),
        ("Entry_OtterToyKkeujeok", 2, new[]
        {
            (0, "쉿… 다들 자요. 밤에는 마을 이야기가 더 잘 들리거든요."),
            (1, "끄적끄적… 오늘 새싹이는 당근을 세 번 몰래 먹었어요."),
            (1, "기록은 기억보다 오래가요. {이름}님 이야기도 여기 다 있어요."),
        }),
        ("Entry_OtterToyKkomkkom", 2, new[]
        {
            (0, "{이름}님, 이 스프링클러… 나사가 반 바퀴 덜 조여졌어요."),
            (0, "한 바퀴, 또 한 바퀴… 꼼꼼하게."),
            (1, "됐다! 이제 물방울이 똑같은 크기로 나와요. 세어 봤거든요!"),
        }),
        ("Entry_OtterToyKongkong", 2, new[]
        {
            (0, "콩콩! 해가 떠 있는 동안은 망치를 못 내려놔요."),
            (1, "울타리 다 고쳤어요. 고칠 데 있으면 언제든 불러요!"),
            (1, "{이름}님 집 문이 삐걱거린다고요? 내일 아침 일찍 갈게요!"),
        }),
        ("Entry_OtterToyKungkung", 2, new[]
        {
            (0, "쿵, 쿵… 놀라지 마세요. 제 발소리예요."),
            (1, "통나무 하나쯤은 번쩍! …두 개는 조금 무거워요."),
            (1, "{이름}님, 숲에서 제일 큰 나무 냄새 맡아 봤어요? 그게 제일 좋아요."),
        }),
        ("Entry_OtterToyPongdang", 2, new[]
        {
            (0, "퐁당! {이름}님, 물이 딱 좋아요!"),
            (0, "조개는 손으로 톡톡. '똑똑, 계세요?' 하고 묻는 거예요."),
            (1, "…아무도 안 계셨어요. 대신 진주를 주웠어요! 선물이에요."),
        }),
        ("Entry_OtterToySallang", 2, new[]
        {
            (0, "바람이 살랑~ {이름}님, 이 바람을 타고 왔어요!"),
            (0, "바람개비가 빙글빙글 돌면, 다음 바람이 어디로 갈지 알 수 있어요."),
            (1, "벚꽃잎 하나 드릴게요. 봄이 끝나도 이건 안 시들어요!"),
        }),
        ("Entry_OtterToyYeongcha", 2, new[]
        {
            (0, "영차, 영차! 수확 상자는 저한테 맡겨요."),
            (0, "무거운 걸 다 옮기고 나면… 어깨가 개운해요."),
            (1, "오늘 짐은 끝! {이름}님, 그럼 같이 산책해요!"),
        }),
    };

    #region 데이터

    /// <summary>
    /// 도감 데이터만 다시 채움 (이야기 에셋 · 컷 그림 · DB). 컷 그림을 Cutscenes 폴더에 넣은 뒤 누르면 빈 컷 칸에 들어간다.
    /// Build Global UI와 달리 정착 설정 데이터를 건드리지 않는다
    /// </summary>
    [MenuItem("Tools/Collection/Apply Stories And Cutscenes")]
    public static void ApplyStories() => CreateData();

    public static void CreateData()
    {
        EnsureFolder(DataFolder + "/Tabs");
        EnsureFolder(DataFolder + "/Entries");

        var tabs = new Dictionary<string, CollectionTab>();
        foreach (var t in Tabs)
        {
            var (tab, isNew) = LoadOrCreate<CollectionTab>($"{DataFolder}/Tabs/{t.asset}.asset");
            var so = new SerializedObject(tab);
            if (isNew)
            {
                so.FindProperty("_tabId").stringValue = t.id;
                so.FindProperty("_displayName").stringValue = t.name;
                so.FindProperty("_sortOrder").intValue = t.order;
                so.FindProperty("_countLabel").stringValue = t.count;
                so.FindProperty("_unknownLabel").stringValue = t.unknown;
                so.FindProperty("_collectedLabel").stringValue = t.collected;
                so.FindProperty("_extraLabel").stringValue = t.extra;
                so.FindProperty("_supportsVisits").boolValue = t.visits;
            }
            FillIfEmpty(so, "_unknownIcon", ImportSprite(SilhouetteFolder, t.legendIcon));
            so.ApplyModifiedPropertiesWithoutUndo();
            tabs[t.asset] = tab;
        }

        var entries = new List<CollectionEntry>();
        foreach (var e in Entries)
        {
            var (entry, isNew) = LoadOrCreate<CollectionEntry>($"{DataFolder}/Entries/{e.asset}.asset");
            var so = new SerializedObject(entry);
            if (isNew)
            {
                so.FindProperty("_entryId").stringValue = e.id;
                so.FindProperty("_tab").objectReferenceValue = tabs[e.tab];
                so.FindProperty("_displayName").stringValue = e.name;
                so.FindProperty("_sortOrder").intValue = entries.Count;
                so.FindProperty("_tagline").stringValue = e.tagline;
                so.FindProperty("_description").stringValue = e.description;
                so.FindProperty("_extraValue").stringValue = e.extra;
                if (!string.IsNullOrEmpty(e.item))
                    so.FindProperty("_linkedItem").objectReferenceValue = LoadItem(e.item);
            }
            if (!string.IsNullOrEmpty(e.portrait))
                FillIfEmpty(so, "_portrait", ImportSprite(OtterFolder, e.portrait));
            FillIfEmpty(so, "_silhouette", ImportSprite(SilhouetteFolder, e.silhouette));

            // 새로 생긴 칸이라 기존 항목에도 채움 (사람이 바꾼 값은 0이 아니므로 유지)
            var unlockLevel = so.FindProperty("_unlockLevel");
            if (unlockLevel.intValue == 0 && UnlockLevels.TryGetValue(e.asset, out int level))
                unlockLevel.intValue = level;

            so.ApplyModifiedPropertiesWithoutUndo();
            entries.Add(entry);
        }

        CreateStories();

        // 사람이 추가한 항목까지 포함해, 실루엣이 비어 있으면 그림(또는 아이템 아이콘)으로 자동 생성
        SilhouetteGenerator.FillMissing(FindAll<CollectionEntry>(DataFolder + "/Entries"));

        // DB는 항상 폴더 안의 모든 탭·항목으로 다시 채움 (사람이 추가한 항목도 포함)
        var (database, _) = LoadOrCreate<CollectionDatabase>(DatabasePath);
        var dbSo = new SerializedObject(database);
        SetList(dbSo.FindProperty("_tabs"), FindAll<CollectionTab>(DataFolder + "/Tabs"));
        SetList(dbSo.FindProperty("_entries"), FindAll<CollectionEntry>(DataFolder + "/Entries"));
        dbSo.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
    }

    // 이야기 에셋: 새로 만들 때만 대사를 넣고(사람이 고친 문구 유지), 컷 그림은 파일이 생기면 빈 칸에 채움
    private static void CreateStories()
    {
        EnsureFolder(StoryFolder);

        foreach (var s in Stories)
        {
            var entry = AssetDatabase.LoadAssetAtPath<CollectionEntry>($"{DataFolder}/Entries/{s.entry}.asset");
            if (entry == null)
                continue;

            var (story, isNew) = LoadOrCreate<CollectionStory>($"{StoryFolder}/Story_{s.entry}.asset");
            var so = new SerializedObject(story);

            if (isNew)
            {
                var lines = so.FindProperty("_lines");
                lines.arraySize = s.lines.Length;
                for (int i = 0; i < s.lines.Length; i++)
                {
                    var line = lines.GetArrayElementAtIndex(i);
                    line.FindPropertyRelative("_cut").intValue = s.lines[i].cut;
                    line.FindPropertyRelative("_text").stringValue = s.lines[i].text;
                }
            }

            var cuts = so.FindProperty("_cuts");
            if (cuts.arraySize < s.cuts)
                cuts.arraySize = s.cuts;
            for (int i = 0; i < cuts.arraySize; i++)
            {
                string file = $"CUT_{entry.EntryId}_{(char)('A' + i)}";
                var element = cuts.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == null && File.Exists($"{CutsceneFolder}/{file}.png"))
                    element.objectReferenceValue = ImportSprite(CutsceneFolder, file);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var entrySo = new SerializedObject(entry);
            FillIfEmpty(entrySo, "_story", story);
            entrySo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    internal static (T asset, bool isNew) LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return (asset, false);

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return (asset, true);
    }

    internal static void FillIfEmpty(SerializedObject so, string property, Object value)
    {
        var prop = so.FindProperty(property);
        if (prop.objectReferenceValue == null && value != null)
            prop.objectReferenceValue = value;
    }

    internal static List<T> FindAll<T>(string folder) where T : Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null)
            .OrderBy(asset => asset.name, System.StringComparer.Ordinal)
            .ToList();
    }

    internal static void SetList<T>(SerializedProperty list, List<T> values) where T : Object
    {
        list.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static ItemDefinition LoadItem(string relativePath)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{relativePath}.asset");
        if (item == null)
            Debug.LogWarning($"[CollectionSetup] 아이템을 찾을 수 없습니다: {relativePath}");
        return item;
    }

    #endregion

    #region 스프라이트 가져오기

    /// <summary>새 PNG를 스프라이트로 가져오고, kit_borders.json에 테두리가 있으면 9-slice로 설정</summary>
    internal static Sprite ImportSprite(string folder, string name)
    {
        string path = $"{folder}/{name}.png";
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[CollectionSetup] 그림 파일이 없습니다: {path}");
            return null;
        }

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var border = ReadBorder(name);

        bool dirty = importer.textureType != TextureImporterType.Sprite
                     || importer.textureShape != TextureImporterShape.Texture2D // guid만 적은 메타는 큐브맵으로 불러와질 때가 있음
                     || importer.spriteImportMode != SpriteImportMode.Single
                     || importer.mipmapEnabled
                     || (border.HasValue && importer.spriteBorder != border.Value);
        if (dirty)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            if (border.HasValue)
                importer.spriteBorder = border.Value;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // kit_borders.json: 이름 → [왼, 위, 오른, 아래]. Unity는 (왼, 아래, 오른, 위) 순서
    private static Vector4? ReadBorder(string name)
    {
        if (!File.Exists(BordersJsonPath))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            File.ReadAllText(BordersJsonPath),
            "\"" + name + "\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\]");
        if (!match.Success)
            return null;

        float left = float.Parse(match.Groups[1].Value);
        float top = float.Parse(match.Groups[2].Value);
        float right = float.Parse(match.Groups[3].Value);
        float bottom = float.Parse(match.Groups[4].Value);
        return new Vector4(left, bottom, right, top);
    }

    private static Sprite CollectionSprite(string name) => ImportSprite(CollectionSpriteFolder, name);

    #endregion

    #region 프리팹 (탭, 칸)

    public static void BuildPrefabs()
    {
        EnsureFolder(Path.GetDirectoryName(TabPrefabPath).Replace('\\', '/'));
        BuildTabPrefab();
        BuildSlotPrefab();
    }

    private static void BuildTabPrefab()
    {
        var root = new GameObject("CollectionTab", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(260, 86);
        var normal = LoadSprite(InventorySpriteFolder, "UI_Inventory_Tab_Normal");
        var background = root.AddComponent<Image>();
        background.sprite = normal;
        background.type = Image.Type.Sliced;
        var button = root.AddComponent<Button>();
        button.targetGraphic = background;

        var label = CreateText("Label", root.transform, _titleFont, "탭", 38, Cocoa);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 8); // 탭 아래쪽 입체 턱만큼 위로

        // 안 본 이야기가 있는 탭: 오른쪽 위 모서리에 걸친 작은 N
        var newBadge = BuildNewBadge(root.transform, new Vector2(-14, -2), 44, 26, pulse: false);

        var view = root.AddComponent<CollectionTabView>();
        Set(view, "_button", button);
        Set(view, "_background", background);
        Set(view, "_label", label);
        Set(view, "_newBadge", newBadge);
        Set(view, "_normalSprite", normal);
        Set(view, "_selectedSprite", CollectionSprite("UI_Tab_Gold"));

        PrefabUtility.SaveAsPrefabAsset(root, TabPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void BuildSlotPrefab()
    {
        var root = new GameObject("CollectionSlot", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(210, 222);
        var empty = LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Empty");
        var background = root.AddComponent<Image>();
        background.sprite = empty;
        background.type = Image.Type.Sliced;
        var button = root.AddComponent<Button>();
        button.targetGraphic = background;

        // 그림은 칸 아래쪽 입체 턱과 "???" 글자 자리를 비워 두고 위쪽에
        var portrait = CreateImage("Portrait", root.transform, null, false);
        Stretch(portrait.rectTransform, 0);
        portrait.rectTransform.offsetMin = new Vector2(22, 44);
        portrait.rectTransform.offsetMax = new Vector2(-22, -20);
        portrait.preserveAspect = true;

        var silhouette = CreateImage("Silhouette", root.transform, null, false);
        Stretch(silhouette.rectTransform, 0);
        silhouette.rectTransform.offsetMin = new Vector2(30, 52);
        silhouette.rectTransform.offsetMax = new Vector2(-30, -24);
        silhouette.preserveAspect = true;

        var unknown = CreateText("Unknown", root.transform, _titleFont, "???", 30, Cocoa);
        BottomBand(unknown.rectTransform, 16, 36);

        var visit = CreateRect("Visit", root.transform);
        BottomBand(visit, 16, 40);
        var visitLayout = visit.gameObject.AddComponent<HorizontalLayoutGroup>();
        visitLayout.childAlignment = TextAnchor.MiddleCenter;
        visitLayout.spacing = 6;
        visitLayout.childControlWidth = true;
        visitLayout.childControlHeight = true;
        visitLayout.childForceExpandWidth = false;
        visitLayout.childForceExpandHeight = false;
        var paw = CreateImage("Paw", visit, CollectionSprite("UI_Icon_Paw"), false);
        paw.preserveAspect = true;
        var pawSize = paw.gameObject.AddComponent<LayoutElement>();
        pawSize.preferredWidth = 32;
        pawSize.preferredHeight = 32;
        CreateText("Label", visit, _titleFont, "방문 흔적", 24, Cocoa);

        var check = CreateImage("Check", root.transform, CollectionSprite("UI_Icon_CheckSmall"), false);
        Place(check.rectTransform, new Vector2(1, 1), new Vector2(-10, -10), new Vector2(46, 46));

        var ring = CreateImage("SelectRing", root.transform, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_SelectRing"), false);
        Stretch(ring.rectTransform, -8);

        // 안 본 이야기: 체크 자리에 통통 튀는 N (선택 테두리보다 위)
        var newBadge = BuildNewBadge(root.transform, new Vector2(-33, -33), 54, 32, pulse: true);

        var view = root.AddComponent<CollectionSlotView>();
        Set(view, "_button", button);
        Set(view, "_background", background);
        Set(view, "_portrait", portrait);
        Set(view, "_silhouette", silhouette);
        Set(view, "_check", check.gameObject);
        Set(view, "_unknownLabel", unknown.gameObject);
        Set(view, "_visitLabel", visit.gameObject);
        Set(view, "_selectRing", ring.gameObject);
        Set(view, "_newBadge", newBadge);
        Set(view, "_collectedSprite", LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Filled"));
        Set(view, "_emptySprite", empty);

        ring.gameObject.SetActive(false);
        visit.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        Object.DestroyImmediate(root);
    }

    // 빨간 동그라미 + 흰 N (가방 버튼의 N과 같은 모양). 가운데 기준으로 두어 커졌다 작아져도 자리가 안 밀림
    private static GameObject BuildNewBadge(Transform parent, Vector2 centerFromTopRight, float size, float fontSize, bool pulse)
    {
        var badge = CreateImage("NewBadge", parent, LoadSprite(CommonSpriteFolder, "UI_Badge"), false);
        badge.preserveAspect = true;
        var rect = badge.rectTransform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = centerFromTopRight;

        var text = CreateText("Text", rect, _titleFont, "N", fontSize, Color.white);
        Stretch(text.rectTransform, 0);
        text.rectTransform.offsetMin = new Vector2(0, 2);

        if (pulse)
            badge.gameObject.AddComponent<PulseAnimator>();

        badge.gameObject.SetActive(false);
        return badge.gameObject;
    }

    #endregion

    #region 화면

    /// <summary>
    /// 도감 화면을 canvas 아래에 만든다 (닫힌 채로 시작). 1080×1920 기준, 시안 수치를 환산.
    /// </summary>
    public static (CollectionPresenter presenter, UIPopupAnimator screen) BuildScreen(RectTransform canvas)
    {
        // 씬을 연 뒤 호출되므로 에셋은 경로로 다시 불러온다
        var tabPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabPrefabPath).GetComponent<CollectionTabView>();
        var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath).GetComponent<CollectionSlotView>();

        var screen = CreateRect("CollectionScreen", canvas);
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
        var safeGroup = safe.gameObject.AddComponent<CanvasGroup>();

        var detail = BuildDetail(safe);
        BuildTitleBoard(safe);
        var close = BuildCloseButton(safe);

        var tabs = CreateRect("Tabs", safe);
        TopBand(tabs, 90, 90, 672, 86);
        var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 24;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;

        var list = CreateImage("ListPanel", safe, LoadPanelSprite(), false);
        list.type = Image.Type.Sliced;
        Stretch(list.rectTransform, 0);
        list.rectTransform.offsetMin = new Vector2(32, 40);
        list.rectTransform.offsetMax = new Vector2(-32, -752);
        list.rectTransform.SetSiblingIndex(tabs.GetSiblingIndex()); // 탭이 목록 패널 위에 그려지게

        var count = CreateText("Count", list.rectTransform, _titleFont, "수집 0/0", 34, LightBrown);
        count.alignment = TextAlignmentOptions.Left;
        TopBand(count.rectTransform, 58, 58, 40, 50);

        var (scroll, content) = BuildScroll(list.rectTransform);
        var legend = BuildLegend(list.rectTransform);

        var animator = screen.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", safe);
        Set(animator, "_panelGroup", safeGroup);
        Set(animator, "_dimButton", dimButton);
        var animatorSo = new SerializedObject(animator);
        animatorSo.FindProperty("_startScale").floatValue = 0.92f; // 가방처럼 은은하게
        animatorSo.FindProperty("_overshoot").floatValue = 1.2f;
        animatorSo.FindProperty("_endScale").floatValue = 0.95f;
        animatorSo.ApplyModifiedPropertiesWithoutUndo();

        var presenter = safe.gameObject.AddComponent<CollectionPresenter>();
        Set(presenter, "_detailView", detail);
        Set(presenter, "_tabPrefab", tabPrefab);
        Set(presenter, "_tabParent", tabs);
        Set(presenter, "_slotPrefab", slotPrefab);
        Set(presenter, "_slotParent", content);
        Set(presenter, "_countText", count);
        Set(presenter, "_legend", legend);
        Set(presenter, "_scrollRect", scroll);
        Set(presenter, "_closeButton", close);
        Set(presenter, "_screen", animator);

        screen.gameObject.SetActive(false);
        return (presenter, animator);
    }

    private static CollectionDetailView BuildDetail(RectTransform parent)
    {
        var panel = CreateImage("DetailPanel", parent, LoadPanelSprite(), false);
        panel.type = Image.Type.Sliced;
        TopBand(panel.rectTransform, 40, 40, 110, 540);
        var rect = panel.rectTransform;

        var frame = CreateImage("PortraitFrame", rect, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Filled"), false);
        Place(frame.rectTransform, new Vector2(0, 1), new Vector2(50, -70), new Vector2(380, 400));

        var portrait = CreateImage("Portrait", frame.rectTransform, null, false);
        Stretch(portrait.rectTransform, 0);
        portrait.rectTransform.offsetMin = new Vector2(30, 44);
        portrait.rectTransform.offsetMax = new Vector2(-30, -30);
        portrait.preserveAspect = true;

        var silhouette = CreateImage("Silhouette", frame.rectTransform, null, false);
        Stretch(silhouette.rectTransform, 0);
        silhouette.rectTransform.offsetMin = new Vector2(56, 70);
        silhouette.rectTransform.offsetMax = new Vector2(-56, -56);
        silhouette.preserveAspect = true;

        var nameText = CreateText("Name", rect, _titleFont, "이름", 60, Cocoa);
        nameText.alignment = TextAlignmentOptions.Left;
        TopBand(nameText.rectTransform, 470, 110, 70, 76);

        var badge = CreateImage("Badge", rect, CollectionSprite("UI_Badge_Status"), false);
        Place(badge.rectTransform, new Vector2(0, 1), new Vector2(470, -160), new Vector2(236, 60));
        var badgeCheck = CreateImage("Check", badge.rectTransform, CollectionSprite("UI_Icon_CheckSmall"), false);
        Place(badgeCheck.rectTransform, new Vector2(0, 0.5f), new Vector2(18, 3), new Vector2(38, 38));
        badgeCheck.rectTransform.pivot = new Vector2(0, 0.5f);
        var badgeText = CreateText("Label", badge.rectTransform, _titleFont, "획득 완료", 30, BadgeGreen);
        Stretch(badgeText.rectTransform, 0);
        badgeText.rectTransform.offsetMin = new Vector2(52, 6);

        var tagline = CreateText("Tagline", rect, _bodyFont, "소개", 30, LightBrown);
        tagline.alignment = TextAlignmentOptions.Left;
        TopBand(tagline.rectTransform, 470, 50, 238, 44);

        var box = CreateImage("DescriptionBox", rect, LoadSprite(InventorySpriteFolder, "UI_Inventory_Box"), false);
        Stretch(box.rectTransform, 0);
        box.rectTransform.offsetMin = new Vector2(452, 56);
        box.rectTransform.offsetMax = new Vector2(-44, -300);

        var description = CreateText("Description", box.rectTransform, _bodyFont, "설명", 26, Cocoa);
        description.alignment = TextAlignmentOptions.TopLeft;
        description.enableAutoSizing = true; // 긴 설명 · 여러 줄 힌트(못 만난 해달)는 박스 안에서 글자를 줄임
        description.fontSizeMin = 20;
        description.fontSizeMax = 26;
        TopBand(description.rectTransform, 28, 28, 26, 76);

        var extra = CreateText("Extra", box.rectTransform, _bodyFont, "획득 장소: 밭", 28, Cocoa);
        extra.alignment = TextAlignmentOptions.Left;
        BottomBand(extra.rectTransform, 30, 42);
        extra.rectTransform.offsetMin = new Vector2(28, extra.rectTransform.offsetMin.y);
        extra.rectTransform.offsetMax = new Vector2(-28, extra.rectTransform.offsetMax.y);

        // [이야기 보기] / [다시 보기]: 그림 틀 아래쪽에 걸침. 가운데 기준이라 커졌다 작아져도 자리가 안 밀림
        var newStorySprite = LoadSprite(CommonSpriteFolder, "UI_Button_Primary");
        var watchedStorySprite = LoadSprite(CommonSpriteFolder, "UI_Button_Paper");
        var storyImage = CreateImage("StoryButton", rect, newStorySprite, true);
        var storyRect = storyImage.rectTransform;
        storyRect.anchorMin = new Vector2(0, 1);
        storyRect.anchorMax = new Vector2(0, 1);
        storyRect.pivot = new Vector2(0.5f, 0.5f);
        storyRect.sizeDelta = new Vector2(300, 90);
        storyRect.anchoredPosition = new Vector2(240, -480); // 그림 틀(가로 50~430) 가운데, 틀 아랫선 위
        storyImage.pixelsPerUnitMultiplier = SlicedScale(newStorySprite, 90);
        var storyButton = storyImage.gameObject.AddComponent<Button>();
        storyButton.targetGraphic = storyImage;
        var storyLabel = CreateText("Label", storyRect, _titleFont, "이야기 보기", 34, Color.white);
        Stretch(storyLabel.rectTransform, 0);
        storyLabel.rectTransform.offsetMin = new Vector2(0, 8); // 버튼 아래쪽 입체 턱만큼 위로
        var storyPulse = storyImage.gameObject.AddComponent<PulseAnimator>();
        storyPulse.enabled = false;
        storyImage.gameObject.SetActive(false);

        var view = panel.gameObject.AddComponent<CollectionDetailView>();
        Set(view, "_storyButton", storyButton);
        Set(view, "_storyButtonImage", storyImage);
        Set(view, "_storyButtonLabel", storyLabel);
        Set(view, "_storyPulse", storyPulse);
        Set(view, "_newStorySprite", newStorySprite);
        Set(view, "_watchedStorySprite", watchedStorySprite);
        Set(view, "_portrait", portrait);
        Set(view, "_silhouette", silhouette);
        Set(view, "_nameText", nameText);
        Set(view, "_badge", badge.gameObject);
        Set(view, "_badgeText", badgeText);
        Set(view, "_taglineText", tagline);
        Set(view, "_descriptionText", description);
        Set(view, "_extraText", extra);
        return view;
    }

    // "도감" 나무 간판: 상세 패널 윗선에 걸침, 양옆에 잎
    private static void BuildTitleBoard(RectTransform parent)
    {
        var board = CreateImage("TitleBoard", parent, CollectionSprite("UI_TitleBoard"), false);
        Place(board.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(420, 124));

        var title = CreateText("Title", board.rectTransform, _titleFont, "도감", 68, Cocoa);
        Stretch(title.rectTransform, 0);
        title.rectTransform.offsetMin = new Vector2(0, 10);

        var leaf = CollectionSprite("UI_Deco_Leaf");
        var left = CreateImage("LeafLeft", board.rectTransform, leaf, false);
        Place(left.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-130, 6), new Vector2(52, 52));
        left.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        var right = CreateImage("LeafRight", board.rectTransform, leaf, false);
        Place(right.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(130, 6), new Vector2(52, 52));
        right.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        right.rectTransform.localScale = new Vector3(-1, 1, 1); // 좌우 대칭
    }

    internal static Button BuildCloseButton(RectTransform parent)
    {
        var close = CreateImage("CloseButton", parent, LoadSprite(InventorySpriteFolder, "UI_Inventory_CloseButton"), true);
        Place(close.rectTransform, new Vector2(1, 1), new Vector2(-24, -86), new Vector2(96, 96));
        close.raycastPadding = new Vector4(-16, -16, -16, -16); // 보이는 크기보다 넓게 눌리도록
        var button = close.gameObject.AddComponent<Button>();
        button.targetGraphic = close;
        return button;
    }

    private static (ScrollRect scroll, RectTransform content) BuildScroll(RectTransform list)
    {
        var scrollImage = CreateImage("ScrollView", list, null, true);
        scrollImage.color = new Color(1, 1, 1, 0); // 빈 곳을 끌어도 스크롤되게
        Stretch(scrollImage.rectTransform, 0);
        scrollImage.rectTransform.offsetMin = new Vector2(44, 186);
        scrollImage.rectTransform.offsetMax = new Vector2(-44, -104);

        var viewport = CreateRect("Viewport", scrollImage.rectTransform);
        Stretch(viewport, 0);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(210, 222);
        grid.spacing = new Vector2(22, 22);
        // 항목이 적어도 왼쪽부터 채움. 좌우 11은 4칸(906)을 스크롤 폭(928) 가운데에 두기 위한 여백, 위아래는 선택 테두리용
        grid.padding = new RectOffset(11, 11, 12, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperLeft;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewport;
        scroll.content = content;
        return (scroll, content);
    }

    private static CollectionLegendView BuildLegend(RectTransform list)
    {
        var legend = CreateImage("Legend", list, LoadSprite(CommonSpriteFolder, "UI_Box_Inset"), false);
        BottomBand(legend.rectTransform, 44, 120);
        legend.rectTransform.offsetMin = new Vector2(44, legend.rectTransform.offsetMin.y);
        legend.rectTransform.offsetMax = new Vector2(-44, legend.rectTransform.offsetMax.y);
        var layout = legend.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 64;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var (unknownIcon, unknownLabel, _) = BuildLegendItem(legend.rectTransform, "Unknown", null, "미획득");
        var (_, _, visited) = BuildLegendItem(legend.rectTransform, "Visited", CollectionSprite("UI_Icon_Paw"), "방문 흔적");
        var (_, collectedLabel, _) = BuildLegendItem(legend.rectTransform, "Collected", CollectionSprite("UI_Icon_CheckSmall"), "획득 완료");

        var view = legend.gameObject.AddComponent<CollectionLegendView>();
        Set(view, "_unknownIcon", unknownIcon);
        Set(view, "_unknownLabel", unknownLabel);
        Set(view, "_visitedItem", visited);
        Set(view, "_collectedLabel", collectedLabel);
        return view;
    }

    private static (Image icon, TextMeshProUGUI label, GameObject item) BuildLegendItem(RectTransform parent, string name, Sprite sprite, string text)
    {
        var item = CreateRect(name, parent);
        var layout = item.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 16;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var icon = CreateImage("Icon", item, sprite, false);
        icon.preserveAspect = true;
        var size = icon.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = 60;
        size.preferredHeight = 60;

        var label = CreateText("Label", item, _titleFont, text, 34, Cocoa);
        return (icon, label, item.gameObject);
    }

    #region 이야기 재생 화면

    private const float CardWidth = 1000f;
    private const float CardHeight = 680f; // 컷 960×640 (3:2, GPT 가로 이미지 1536×1024와 같은 비율) + 테두리
    private const float CardCenterY = 120f;
    private const int MaxDots = 5;

    /// <summary>
    /// 이야기(컷씬) 재생 화면을 canvas 아래에 만든다 (닫힌 채로 시작). 도감 화면보다 뒤에 만들어야 위에 그려진다.
    /// 딤 → 화면 전체 탭 영역 → 컷 카드(아래쪽에 대사창) → 진행 점 → 건너뛰기
    /// </summary>
    public static CollectionStoryPlayerView BuildStoryPlayer(RectTransform canvas)
    {
        var screen = CreateRect("CollectionStory", canvas);
        Stretch(screen, 0);

        var dim = CreateImage("Dim", screen, null, false);
        Stretch(dim.rectTransform, 0);
        dim.color = new Color(0, 0, 0, 0.75f);
        var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();

        var safe = CreateRect("SafeArea", screen);
        Stretch(safe, 0);
        safe.gameObject.AddComponent<SafeAreaFltter>();

        // 화면 어디를 눌러도 다음으로 (카드·글자는 클릭을 막지 않음)
        var tapImage = CreateImage("TapArea", safe, null, true);
        Stretch(tapImage.rectTransform, 0);
        tapImage.color = new Color(1, 1, 1, 0);
        var tapArea = tapImage.gameObject.AddComponent<Button>();
        tapArea.targetGraphic = tapImage;
        tapArea.transition = Selectable.Transition.None;

        var card = CreateImage("Card", safe, LoadPanelSprite(), false);
        card.type = Image.Type.Sliced;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, CardCenterY), new Vector2(CardWidth, CardHeight));
        var cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;

        var cutBack = BuildCutImage("CutBack", card.rectTransform);
        var cutFront = BuildCutImage("CutFront", card.rectTransform);

        // 대사창: 컷 아래쪽 30%에 겹친 반투명 크림 상자
        var box = CreateImage("LineBox", card.rectTransform, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        box.color = new Color(1, 1, 1, 0.94f);
        BottomBand(box.rectTransform, 40, 210);
        box.rectTransform.offsetMin = new Vector2(44, box.rectTransform.offsetMin.y);
        box.rectTransform.offsetMax = new Vector2(-44, box.rectTransform.offsetMax.y);

        var line = CreateText("Line", box.rectTransform, _titleFont, "대사", 38, Cocoa);
        line.alignment = TextAlignmentOptions.TopLeft;
        line.lineSpacing = 10;
        Stretch(line.rectTransform, 0);
        line.rectTransform.offsetMin = new Vector2(40, 36);
        line.rectTransform.offsetMax = new Vector2(-60, -30);

        // ▼는 제목 폰트에 없는 기호라 본문 폰트
        var next = CreateText("Next", box.rectTransform, _bodyFont, "▼", 28, LightBrown);
        Place(next.rectTransform, new Vector2(1, 0), new Vector2(-30, 30), new Vector2(40, 40));
        next.gameObject.AddComponent<PulseAnimator>();

        var dots = CreateRect("Dots", safe);
        Place(dots, new Vector2(0.5f, 0.5f), new Vector2(0, CardCenterY - CardHeight / 2 - 60), new Vector2(400, 28));
        dots.pivot = new Vector2(0.5f, 0.5f);
        var dotLayout = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
        dotLayout.childAlignment = TextAnchor.MiddleCenter;
        dotLayout.spacing = 18;
        dotLayout.childControlWidth = false;
        dotLayout.childControlHeight = false;
        dotLayout.childForceExpandWidth = false;
        dotLayout.childForceExpandHeight = false;
        var dotSprite = LoadSprite(CommonSpriteFolder, "UI_Knob");
        var dotImages = new List<Image>();
        for (int i = 0; i < MaxDots; i++)
        {
            var dot = CreateImage($"Dot{i}", dots, dotSprite, false);
            dot.rectTransform.sizeDelta = new Vector2(26, 26);
            dotImages.Add(dot);
        }

        var skip = BuildSkipButton(safe);

        var animator = screen.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", card.rectTransform);
        Set(animator, "_panelGroup", cardGroup);
        var animatorSo = new SerializedObject(animator);
        animatorSo.FindProperty("_startScale").floatValue = 0.7f;
        animatorSo.FindProperty("_overshoot").floatValue = 1.4f;
        animatorSo.ApplyModifiedPropertiesWithoutUndo();

        var view = screen.gameObject.AddComponent<CollectionStoryPlayerView>();
        Set(view, "_animator", animator);
        Set(view, "_cutFront", cutFront);
        Set(view, "_cutBack", cutBack);
        Set(view, "_lineText", line);
        Set(view, "_nextIndicator", next.gameObject);
        Set(view, "_tapArea", tapArea);
        Set(view, "_skipButton", skip);
        var viewSo = new SerializedObject(view);
        var dotList = viewSo.FindProperty("_dots");
        dotList.arraySize = dotImages.Count;
        for (int i = 0; i < dotImages.Count; i++)
            dotList.GetArrayElementAtIndex(i).objectReferenceValue = dotImages[i];
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        screen.gameObject.SetActive(false);
        return view;
    }

    // 컷: 카드 테두리 안쪽을 채움. 임시 컷(정사각 아이콘)도 찌그러지지 않게 비율 유지
    private static Image BuildCutImage(string name, RectTransform card)
    {
        var cut = CreateImage(name, card, null, false);
        Stretch(cut.rectTransform, 20);
        cut.preserveAspect = true;
        return cut;
    }

    private static Button BuildSkipButton(RectTransform parent)
    {
        var paper = LoadSprite(CommonSpriteFolder, "UI_Button_Paper");
        var image = CreateImage("SkipButton", parent, paper, true);
        image.pixelsPerUnitMultiplier = SlicedScale(paper, 84);
        Place(image.rectTransform, new Vector2(1, 1), new Vector2(-36, -60), new Vector2(220, 84));
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = CreateText("Label", image.rectTransform, _titleFont, "건너뛰기", 32, LightBrown);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 8);
        return button;
    }

    // 9-slice 테두리(위+아래)가 높이보다 크면 모서리가 찌그러지므로 테두리를 줄이는 배율
    private static float SlicedScale(Sprite sprite, float height)
    {
        float borders = sprite.border.y + sprite.border.w;
        return borders > height ? borders / (height * 0.9f) : 1f;
    }

    #endregion

    // 위쪽 가로 띠 (top-stretch): 왼/오른 여백, 위에서 거리, 높이
    internal static void TopBand(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    #endregion
}
