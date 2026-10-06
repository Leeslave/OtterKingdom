using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CollectionSetup;
using static GlobalUISetup;

/// <summary>
/// 퀘스트 데이터(퀘스트·DB), 목록 한 줄 프리팹, 화면(QuestScreen)을 만든다. GlobalUISetup.Run이 함께 호출한다.
/// 이미 있는 퀘스트 에셋은 사람이 고친 문구·수치를 지키기 위해 덮어쓰지 않고, 비어 있는 그림만 채운다.
/// </summary>
public static class QuestSetup
{
    private const string DataFolder = "Assets/Scriptable Obejects/Quest";
    private const string QuestFolder = DataFolder + "/Quests";
    internal const string DatabasePath = DataFolder + "/QuestDatabase.asset";
    private const string RowPrefabPath = "Assets/Prefab/Quest/QuestRow.prefab";
    private const string ItemFolder = "Assets/Scriptable Obejects/Inventory/Items";
    private const string CategoryFolder = "Assets/Scriptable Obejects/Inventory/Category";
    private const string OtterTabPath = "Assets/Scriptable Obejects/Collection/Tabs/Tab_Otter.asset";
    private const string GoldPath = "Assets/Scriptable Obejects/Gold.asset";
    private const string PlaceIconFolder = "Assets/Art/UI/Travel";
    private const string ConstructionFolder = "Assets/Scriptable Obejects/Settlement/Constructions";

    private static readonly Color Body = new Color32(0x6B, 0x4A, 0x3A, 0xFF);

    // 아이콘: "item:경로" (아이템 아이콘), "gold" (골드 아이콘), "place:파일", "otter:파일", "npc:파일", "nav:파일", "settle:파일"
    // 필터: 분류 이름(Crops 등) / "otter"(도감 해달 탭) / "only:아이템 경로"(그 아이템만 — 수확·상점 구매) / "build:건설ID,건설ID"(그 건물만) / "builder"(건설 해달 참여 공사만)
    // 대상: 정착 단계 = 끝낸 부탁 ID 또는 열린 발전 ID / 장소 가 보기 = 씬 이름
    //
    // 메인: 레벨마다 몇 개를 하나씩 순서대로 (모두 한 줄로 이어짐 — 앞 단계 보상을 받으면 다음). 한 레벨의 메인 경험치 합 = 그 레벨의 필요 경험치.
    //   정착 단계 중 스스로 경험치를 주는 것(공동사업·영토 미션)이나 레벨을 바로 올리는 부탁(의자·광산 길)은 경험치 0, 나머지가 채운다.
    //   표와 이유: Docs/레벨별_메인퀘스트.md. 수치는 임시 (플레이 실측 뒤 조정)
    // 도전: 예전 누적 체인. 골드만, 경험치 없음. 일일: Lv.9부터, 골드만.
    // 이미 있는 에셋도 구조 칸(종류·레벨·앞 단계·경험치·정렬·대상·목표 종류)은 이 표로 맞춘다. 문구·목표 수치·보상은 사람이 고친 값을 지킨다.
    // (에셋, ID, 제목, 조건, 목표 종류, 목표, 필터, 골드, 아이콘, 종류, 필요 레벨, 앞 단계 에셋, 경험치, 경험치 %, 대상)
    private static readonly (string asset, string id, string title, string description, QuestGoalType type, int goal, string filter,
        int reward, string icon, QuestKind kind, int level, string prerequisite, int exp, float expPercent, string target)[] Quests =
    {
        // Lv.1 → 2 해달 왕국에 어서 와 (100) — 의자를 끝내면 바로 Lv.2
        ("M01_House", "main_lv1_house", "첫 번째 집", "게시판 부탁: 첫 번째 집 만들기", QuestGoalType.Milestone, 1, "", 30, "settle:ICON_House_Blue", QuestKind.Main, 1, "", 40, 0f, "req_first_house"),
        ("Main_Gather_1", "main_gather_1", "부지런한 손", "광장에서 재료 10개 줍기", QuestGoalType.Gather, 10, "", 30, "item:Mining/목재", QuestKind.Main, 1, "M01_House", 30, 0f, ""),
        ("Main_Sales_1", "main_sales_1", "티끌 모아 왕국", "판매로 10골드 벌기", QuestGoalType.EarnFromSales, 10, "", 30, "gold", QuestKind.Main, 1, "Main_Gather_1", 30, 0f, ""),
        ("M01_Chair", "main_lv1_chair", "쉬어 갈 의자", "게시판 부탁: 쉬어 갈 의자 만들기", QuestGoalType.Milestone, 1, "", 50, "settle:ICON_Chair", QuestKind.Main, 1, "Main_Sales_1", 0, 0f, "req_chair"),
        // Lv.2 → 3 광산을 찾았어요 (150) — 광산 길을 열면 바로 Lv.3
        ("M02_VisitMine", "main_lv2_visit_mine", "광산을 찾았어요", "광산에 가 보기", QuestGoalType.VisitZone, 1, "", 30, "place:ICON_Place_Mine", QuestKind.Main, 2, "M01_Chair", 50, 0f, "Mine"),
        ("Main_Rock_1", "main_rock_1", "돌 깨는 해달", "광장 바위를 깨서 돌 6개 얻기", QuestGoalType.Gather, 6, "Ore", 30, "item:Mining/돌", QuestKind.Main, 2, "M02_VisitMine", 100, 0f, ""),
        ("M02_MinePath", "main_lv2_mine_path", "광산 길 열기", "광산 길을 막은 나무·바위 치우기", QuestGoalType.Milestone, 1, "", 50, "settle:ICON_MinePath", QuestKind.Main, 2, "Main_Rock_1", 0, 0f, "req_mine_path"),
        // Lv.3 → 4 광부와 새 이웃 (220)
        ("M03_Miner", "main_lv3_miner", "광산에서 일할 친구", "깡깡이를 광산에 배치하기", QuestGoalType.Milestone, 1, "", 50, "otter:ICON_Otter_Miner", QuestKind.Main, 3, "M02_MinePath", 50, 0f, "req_assign_miner"),
        ("Main_Mine_1", "main_mine_1", "첫 곡괭이질", "광산에서 광석 5개 캐기", QuestGoalType.Mine, 5, "Ore", 30, "item:Mining/돌", QuestKind.Main, 3, "M03_Miner", 70, 0f, ""),
        ("M03_Neighbor", "main_lv3_neighbor", "새 이웃의 집", "게시판 부탁: 새 이웃의 집 짓기", QuestGoalType.Milestone, 1, "", 100, "settle:ICON_House_Red", QuestKind.Main, 3, "Main_Mine_1", 100, 0f, "req_neighbor_house"),
        // Lv.4 → 5 먹거리를 길러요 (320)
        ("M04_Farmland", "main_lv4_farmland", "먹거리를 길러요", "밭을 개간하기", QuestGoalType.Milestone, 1, "", 100, "place:ICON_Place_Farm", QuestKind.Main, 4, "M03_Neighbor", 100, 0f, "req_farmland"),
        ("M04_Farmer", "main_lv4_farmer", "농사를 지을 해달", "새싹이를 밭에 배치하기", QuestGoalType.Milestone, 1, "", 50, "otter:ICON_Otter_Farmer", QuestKind.Main, 4, "M04_Farmland", 60, 0f, "req_assign_farmer"),
        ("Main_FirstCarrots", "quest_first_carrots", "첫 수확을 해봐요", "당근 3개 수확하기", QuestGoalType.Harvest, 3, "only:Farming/당근", 30, "item:Farming/당근", QuestKind.Main, 4, "M04_Farmer", 60, 0f, ""),
        ("M04_Sales", "main_lv4_sales", "첫 장사", "판매로 300골드 벌기", QuestGoalType.EarnFromSales, 300, "", 50, "gold", QuestKind.Main, 4, "Main_FirstCarrots", 100, 0f, ""),
        // Lv.5 → 6 고랑 넓히기 (450)
        ("M05_Seed", "main_lv5_seed", "요정과 첫 거래", "요정 상점에서 감자 모종 사기", QuestGoalType.ShopPurchase, 1, "only:Farming/감자 모종", 50, "npc:Fairy", QuestKind.Main, 5, "M04_Sales", 60, 0f, ""),
        ("M05_Furrow", "main_lv5_furrow", "고랑 넓히기", "밭의 고랑 한 칸 열기", QuestGoalType.UnlockFurrow, 1, "", 0, "place:ICON_Place_Farm", QuestKind.Main, 5, "M05_Seed", 70, 0f, ""),
        ("M05_Board", "main_lv5_board", "부탁이 많아졌어요", "게시판 보강하기", QuestGoalType.Milestone, 1, "", 100, "settle:ICON_BoardUpgrade", QuestKind.Main, 5, "M05_Furrow", 120, 0f, "req_upgrade_board"),
        ("M05_Harvest", "main_lv5_harvest", "부지런한 농부", "작물 60개 수확하기", QuestGoalType.Harvest, 60, "Crops", 100, "item:Farming/감자", QuestKind.Main, 5, "M05_Board", 200, 0f, ""),
        // Lv.6 → 7 함께 일하는 마을 (600)
        ("M06_Clerk", "main_lv6_clerk", "게시판을 맡아 줄 친구", "또박이에게 게시판 맡기기", QuestGoalType.Milestone, 1, "", 50, "otter:ICON_Otter_Clerk", QuestKind.Main, 6, "M05_Harvest", 80, 0f, "req_assign_receptionist"),
        ("M06_Common", "main_lv6_common", "다 같이 쉴 자리", "광장에 쉴 자리 정리하기", QuestGoalType.Milestone, 1, "", 50, "settle:ICON_CommonSpace", QuestKind.Main, 6, "M06_Clerk", 100, 0f, "req_prepare_common_space"),
        ("M06_Furrow", "main_lv6_furrow", "고랑 하나 더", "밭의 고랑 한 칸 더 열기", QuestGoalType.UnlockFurrow, 1, "", 0, "place:ICON_Place_Farm", QuestKind.Main, 6, "M06_Common", 60, 0f, ""),
        ("M06_Potato", "main_lv6_potato", "감자 농사", "감자 20개 수확하기", QuestGoalType.Harvest, 20, "only:Farming/감자", 100, "item:Farming/감자", QuestKind.Main, 6, "M06_Furrow", 170, 0f, ""),
        ("M06_Guild", "main_lv6_guild", "큰 부탁도 함께", "길드 접수소 짓기", QuestGoalType.Milestone, 1, "", 200, "settle:ICON_GuildOffice", QuestKind.Main, 6, "M06_Potato", 190, 0f, "req_build_guild_office"),
        // Lv.7 → 8 우리 마을의 회의소 (800 = 첫 비축 사업 560 + 240)
        ("M07_TownHall", "main_lv7_town_hall", "우리 마을의 회의소", "마을회관 짓기", QuestGoalType.Milestone, 1, "", 300, "settle:ICON_TownHall", QuestKind.Main, 7, "M06_Guild", 120, 0f, "req_upgrade_town_hall"),
        ("M07_Supply", "main_lv7_supply", "우리 마을의 첫 비축", "공동사업: 비축 상자 만들기", QuestGoalType.Milestone, 1, "", 100, "settle:ICON_TownHall", QuestKind.Main, 7, "M07_TownHall", 0, 0f, "supply_ready"),
        ("M07_Mine", "main_lv7_mine", "광석 모으기", "광산에서 광석 50개 캐기", QuestGoalType.Mine, 50, "Ore", 200, "item:Mining/다이아몬드", QuestKind.Main, 7, "M07_Supply", 120, 0f, ""),
        // Lv.8 → 9 영토 넓히기 (1,050 = 첫 영토 미션 600 + 450)
        ("M08_Furrow", "main_lv8_furrow", "고랑 셋째 칸", "밭의 고랑 한 칸 더 열기", QuestGoalType.UnlockFurrow, 1, "", 0, "place:ICON_Place_Farm", QuestKind.Main, 8, "M07_Mine", 80, 0f, ""),
        ("M08_Territory", "main_lv8_territory", "영토 넓히기", "숲을 개간해 첫 영토 열기", QuestGoalType.Milestone, 1, "", 200, "settle:ICON_Clearing", QuestKind.Main, 8, "M08_Furrow", 0, 0f, "territory_first"),
        ("M08_SweetPotato", "main_lv8_sweet_potato", "달콤한 고구마", "고구마 30개 수확하기", QuestGoalType.Harvest, 30, "only:Farming/고구마", 200, "item:Farming/고구마", QuestKind.Main, 8, "M08_Territory", 220, 0f, ""),
        ("M08_Sales", "main_lv8_sales", "알뜰 살림", "판매로 3,000골드 벌기", QuestGoalType.EarnFromSales, 3000, "", 200, "gold", QuestKind.Main, 8, "M08_SweetPotato", 150, 0f, ""),
        // Lv.9 → 10 새 이웃 (1,350 = 이웃 맞이 610 + 환영 공간 340 + 400)
        ("M09_Neighbor", "main_lv9_neighbor", "새 이웃 맞이하기", "공동사업: 새 이웃의 집", QuestGoalType.Milestone, 1, "", 200, "settle:ICON_House_Red", QuestKind.Main, 9, "M08_Sales", 0, 0f, "p3_neighbor_settled"),
        ("M09_Welcome", "main_lv9_welcome", "환영 공간 꾸미기", "공동사업: 화분이나 빨랫줄 놓기", QuestGoalType.Milestone, 1, "", 200, "settle:ICON_CommonSpace", QuestKind.Main, 9, "M09_Neighbor", 0, 0f, "welcome_corner_ready"),
        ("M09_Furrow", "main_lv9_furrow", "고랑 넷째 칸", "밭의 고랑 한 칸 더 열기", QuestGoalType.UnlockFurrow, 1, "", 0, "place:ICON_Place_Farm", QuestKind.Main, 9, "M09_Welcome", 100, 0f, ""),
        ("M09_Daily", "main_lv9_daily", "오늘의 할 일", "일일 퀘스트 1개 끝내기", QuestGoalType.CompleteDaily, 1, "", 100, "nav:ICON_Nav_Quest", QuestKind.Main, 9, "M09_Furrow", 100, 0f, ""),
        ("M09_Harvest", "main_lv9_harvest", "대풍년", "작물 300개 수확하기", QuestGoalType.Harvest, 300, "Crops", 300, "item:Farming/당근", QuestKind.Main, 9, "M09_Daily", 200, 0f, ""),
        // Lv.10 → 11 첫 모임 (1,700 = 첫 모임 425 + 1,275)
        ("M10_Gathering", "main_lv10_gathering", "우리 마을의 첫 모임", "공동사업: 첫 모임 열기", QuestGoalType.Milestone, 1, "", 300, "settle:ICON_CommonSpace", QuestKind.Main, 10, "M09_Harvest", 0, 0f, "first_gathering_complete"),
        ("M10_Upgrade", "main_lv10_upgrade", "더 좋은 도구", "생산 업그레이드 1회", QuestGoalType.Upgrade, 1, "", 300, "place:ICON_Place_Farm", QuestKind.Main, 10, "M10_Gathering", 200, 0f, ""),
        ("M10_Decor", "main_lv10_decor", "광장 놀이터", "장난감 2개 놓기", QuestGoalType.PlaceDecor, 2, "", 300, "item:Decor/축구공", QuestKind.Main, 10, "M10_Upgrade", 200, 0f, ""),
        ("M10_Sales", "main_lv10_sales", "마을의 살림꾼", "판매로 10,000골드 벌기", QuestGoalType.EarnFromSales, 10000, "", 500, "gold", QuestKind.Main, 10, "M10_Decor", 300, 0f, ""),
        ("M10_Harvest", "main_lv10_harvest", "밭의 주인", "작물 500개 수확하기", QuestGoalType.Harvest, 500, "Crops", 500, "item:Farming/감자", QuestKind.Main, 10, "M10_Sales", 575, 0f, ""),
        // Lv.11 → 12 새빨간 딸기 (2,100)
        ("M11_StrawberrySeed", "main_lv11_strawberry_seed", "새빨간 딸기", "요정 상점에서 딸기 모종 사기", QuestGoalType.ShopPurchase, 1, "only:Farming/딸기 모종", 200, "npc:Fairy", QuestKind.Main, 11, "M10_Harvest", 200, 0f, ""),
        ("M11_Furrow", "main_lv11_furrow", "고랑 다섯째 칸", "밭의 고랑 한 칸 더 열기", QuestGoalType.UnlockFurrow, 1, "", 0, "place:ICON_Place_Farm", QuestKind.Main, 11, "M11_StrawberrySeed", 200, 0f, ""),
        ("M11_Strawberry", "main_lv11_strawberry", "딸기 농사", "딸기 100개 수확하기", QuestGoalType.Harvest, 100, "only:Farming/딸기", 800, "item:Farming/딸기", QuestKind.Main, 11, "M11_Furrow", 800, 0f, ""),
        ("M11_Mine", "main_lv11_mine", "광산 깊이", "광산에서 광석 200개 캐기", QuestGoalType.Mine, 200, "Ore", 800, "item:Mining/다이아몬드", QuestKind.Main, 11, "M11_Strawberry", 900, 0f, ""),
        // Lv.12 → 13 왕국의 살림 (2,600)
        ("M12_Upgrade", "main_lv12_upgrade", "장인의 손길", "생산 업그레이드 2회", QuestGoalType.Upgrade, 2, "", 800, "place:ICON_Place_Mine", QuestKind.Main, 12, "M11_Mine", 500, 0f, ""),
        ("M12_Harvest", "main_lv12_harvest", "풍년이다!", "작물 800개 수확하기", QuestGoalType.Harvest, 800, "Crops", 1000, "item:Farming/딸기", QuestKind.Main, 12, "M12_Upgrade", 1000, 0f, ""),
        ("M12_Sales", "main_lv12_sales", "큰손 해달", "판매로 30,000골드 벌기", QuestGoalType.EarnFromSales, 30000, "", 1500, "gold", QuestKind.Main, 12, "M12_Harvest", 1100, 0f, ""),
        // Lv.13 → 14 넓어진 밭 (3,200)
        ("M13_Furrow", "main_lv13_furrow", "마지막 고랑", "밭의 고랑 한 칸 더 열기", QuestGoalType.UnlockFurrow, 1, "", 0, "place:ICON_Place_Farm", QuestKind.Main, 13, "M12_Sales", 300, 0f, ""),
        ("M13_Decor", "main_lv13_decor", "해달 놀이공원", "장난감 3개 놓기", QuestGoalType.PlaceDecor, 3, "", 800, "item:Decor/퍼즐", QuestKind.Main, 13, "M13_Furrow", 400, 0f, ""),
        ("M13_Mine", "main_lv13_mine", "광맥을 찾아서", "광산에서 광석 400개 캐기", QuestGoalType.Mine, 400, "Ore", 1500, "item:Mining/다이아몬드", QuestKind.Main, 13, "M13_Decor", 1200, 0f, ""),
        ("M13_Sales", "main_lv13_sales", "해달 재벌", "판매로 50,000골드 벌기", QuestGoalType.EarnFromSales, 50000, "", 2000, "gold", QuestKind.Main, 13, "M13_Mine", 1300, 0f, ""),
        // Lv.14 → 15 바다를 향해 (3,900)
        ("M14_Upgrade", "main_lv14_upgrade", "완벽한 설비", "생산 업그레이드 2회", QuestGoalType.Upgrade, 2, "", 1500, "place:ICON_Place_Farm", QuestKind.Main, 14, "M13_Sales", 500, 0f, ""),
        ("M14_Harvest", "main_lv14_harvest", "전설의 농부", "작물 1,500개 수확하기", QuestGoalType.Harvest, 1500, "Crops", 2000, "item:Farming/고구마", QuestKind.Main, 14, "M14_Upgrade", 1700, 0f, ""),
        ("M14_Sales", "main_lv14_sales", "왕국의 금고", "판매로 80,000골드 벌기", QuestGoalType.EarnFromSales, 80000, "", 2500, "gold", QuestKind.Main, 14, "M14_Harvest", 1700, 0f, ""),
        // Lv.15 → 16 바다가 열렸다 (4,700)
        ("M15_Dock", "main_lv15_dock", "바다로 나가는 선착장", "게시판 부탁: 선착장 고치기", QuestGoalType.Milestone, 1, "", 1000, "settle:ICON_FishingDock", QuestKind.Main, 15, "M14_Sales", 1200, 0f, "req_fishing_dock"),
        ("M15_Fisher", "main_lv15_fisher", "낚시를 할 해달", "첨벙이를 낚시터에 배치하기", QuestGoalType.Milestone, 1, "", 500, "otter:ICON_Otter_Fisher", QuestKind.Main, 15, "M15_Dock", 500, 0f, "req_assign_fisher"),
        ("M15_VisitDock", "main_lv15_visit_dock", "바다가 열렸다", "낚시터에 가 보기", QuestGoalType.VisitZone, 1, "", 300, "place:ICON_Place_FishingSpot", QuestKind.Main, 15, "M15_Fisher", 300, 0f, "Fishing"),
        ("M15_Catch", "main_lv15_catch", "첫 만선", "물고기 30마리 낚기", QuestGoalType.Catch, 30, "Fish", 2000, "item:Fishing/고등어", QuestKind.Main, 15, "M15_VisitDock", 2700, 0f, ""),

        // 도전: 예전 누적 체인 (골드만). 첫 단계가 메인으로 옮겨 간 체인은 그 메인을 앞 단계로 둔다
        ("Main_Harvest_1", "main_harvest_1", "내가 키운 첫 수확", "작물 5개 수확하기", QuestGoalType.Harvest, 5, "Crops", 50, "item:Farming/당근", QuestKind.Challenge, 1, "", 0, 0f, ""),
        ("Main_Harvest_2", "main_harvest_2", "부지런한 손길", "작물 20개 수확하기", QuestGoalType.Harvest, 20, "Crops", 100, "item:Farming/감자", QuestKind.Challenge, 2, "Main_Harvest_1", 0, 0f, ""),
        ("Main_Harvest_3", "main_harvest_3", "텃밭 농부", "작물 60개 수확하기", QuestGoalType.Harvest, 60, "Crops", 200, "item:Farming/당근", QuestKind.Challenge, 3, "Main_Harvest_2", 0, 0f, ""),
        ("Main_Harvest_4", "main_harvest_4", "밭의 주인", "작물 150개 수확하기", QuestGoalType.Harvest, 150, "Crops", 400, "item:Farming/감자", QuestKind.Challenge, 5, "Main_Harvest_3", 0, 0f, ""),
        ("Main_Harvest_5", "main_harvest_5", "풍년이다!", "작물 400개 수확하기", QuestGoalType.Harvest, 400, "Crops", 800, "item:Farming/당근", QuestKind.Challenge, 8, "Main_Harvest_4", 0, 0f, ""),
        ("Main_Harvest_6", "main_harvest_6", "농사 달인", "작물 1,000개 수확하기", QuestGoalType.Harvest, 1000, "Crops", 1500, "item:Farming/감자", QuestKind.Challenge, 11, "Main_Harvest_5", 0, 0f, ""),
        ("Main_Harvest_7", "main_harvest_7", "전설의 농부", "작물 2,500개 수확하기", QuestGoalType.Harvest, 2500, "Crops", 3000, "item:Farming/당근", QuestKind.Challenge, 14, "Main_Harvest_6", 0, 0f, ""),
        ("Main_Catch_1", "main_catch_1", "첫 입질", "물고기 2마리 낚기", QuestGoalType.Catch, 2, "Fish", 50, "item:Fishing/고등어", QuestKind.Challenge, 1, "", 0, 0f, ""),
        ("Main_Catch_2", "main_catch_2", "오늘부터 낚시왕", "물고기 6마리 낚기", QuestGoalType.Catch, 6, "Fish", 100, "item:Fishing/고등어", QuestKind.Challenge, 2, "Main_Catch_1", 0, 0f, ""),
        ("Main_Catch_3", "main_catch_3", "바다의 단골", "물고기 15마리 낚기", QuestGoalType.Catch, 15, "Fish", 250, "item:Fishing/고등어", QuestKind.Challenge, 4, "Main_Catch_2", 0, 0f, ""),
        ("Main_Catch_4", "main_catch_4", "만선의 꿈", "물고기 40마리 낚기", QuestGoalType.Catch, 40, "Fish", 500, "item:Fishing/고등어", QuestKind.Challenge, 6, "Main_Catch_3", 0, 0f, ""),
        ("Main_Catch_5", "main_catch_5", "고등어 사냥꾼", "물고기 100마리 낚기", QuestGoalType.Catch, 100, "Fish", 1000, "item:Fishing/고등어", QuestKind.Challenge, 9, "Main_Catch_4", 0, 0f, ""),
        ("Main_Catch_6", "main_catch_6", "바다의 전설", "물고기 250마리 낚기", QuestGoalType.Catch, 250, "Fish", 2000, "item:Fishing/고등어", QuestKind.Challenge, 12, "Main_Catch_5", 0, 0f, ""),
        ("Main_Mine_2", "main_mine_2", "광부의 하루", "광산에서 광석 20개 캐기", QuestGoalType.Mine, 20, "Ore", 80, "item:Mining/다이아몬드", QuestKind.Challenge, 3, "Main_Mine_1", 0, 0f, ""),
        ("Main_Build_1", "main_build_1", "첫 보금자리", "집 1채 짓기", QuestGoalType.CompleteConstruction, 1, "build:con_house_1,con_house_2", 50, "settle:ICON_House_Blue", QuestKind.Challenge, 1, "", 0, 0f, ""),
        ("Main_Build_2", "main_build_2", "왕국을 가꾸는 손", "건축물 2개 짓기", QuestGoalType.CompleteConstruction, 2, "", 100, "settle:ICON_Chair", QuestKind.Challenge, 2, "Main_Build_1", 0, 0f, ""),
        ("Main_Build_3", "main_build_3", "뚝딱뚝딱", "건설 해달과 공사 1번 끝내기", QuestGoalType.CompleteConstruction, 1, "builder", 150, "settle:ICON_Otter_Builder", QuestKind.Challenge, 3, "", 0, 0f, ""),
        ("Main_Gather_2", "main_gather_2", "광장 청소부", "광장에서 재료 40개 줍기", QuestGoalType.Gather, 40, "", 80, "item:Mining/돌", QuestKind.Challenge, 2, "Main_Gather_1", 0, 0f, ""),
        ("Main_Sales_2", "main_sales_2", "첫 장사", "판매로 1,000골드 벌기", QuestGoalType.EarnFromSales, 1000, "", 100, "gold", QuestKind.Challenge, 2, "Main_Sales_1", 0, 0f, ""),
        ("Main_Sales_3", "main_sales_3", "알뜰 상인", "판매로 4,000골드 벌기", QuestGoalType.EarnFromSales, 4000, "", 300, "gold", QuestKind.Challenge, 4, "Main_Sales_2", 0, 0f, ""),
        ("Main_Sales_4", "main_sales_4", "왕국의 살림꾼", "판매로 15,000골드 벌기", QuestGoalType.EarnFromSales, 15000, "", 700, "gold", QuestKind.Challenge, 7, "Main_Sales_3", 0, 0f, ""),
        ("Main_Sales_5", "main_sales_5", "큰손 해달", "판매로 50,000골드 벌기", QuestGoalType.EarnFromSales, 50000, "", 1500, "gold", QuestKind.Challenge, 10, "Main_Sales_4", 0, 0f, ""),
        ("Main_Sales_6", "main_sales_6", "해달 재벌", "판매로 150,000골드 벌기", QuestGoalType.EarnFromSales, 150000, "", 3000, "gold", QuestKind.Challenge, 13, "Main_Sales_5", 0, 0f, ""),
        ("Main_Upgrade_1", "main_upgrade_1", "더 좋은 도구가 필요해", "생산 업그레이드 1회", QuestGoalType.Upgrade, 1, "", 100, "place:ICON_Place_Farm", QuestKind.Challenge, 2, "", 0, 0f, ""),
        ("Main_Upgrade_2", "main_upgrade_2", "장인의 손길", "생산 업그레이드 2회", QuestGoalType.Upgrade, 2, "", 200, "place:ICON_Place_FishingSpot", QuestKind.Challenge, 4, "Main_Upgrade_1", 0, 0f, ""),
        ("Main_Upgrade_3", "main_upgrade_3", "최고의 장비", "생산 업그레이드 3회", QuestGoalType.Upgrade, 3, "", 400, "place:ICON_Place_Farm", QuestKind.Challenge, 7, "Main_Upgrade_2", 0, 0f, ""),
        ("Main_Upgrade_4", "main_upgrade_4", "완벽한 설비", "생산 업그레이드 2회", QuestGoalType.Upgrade, 2, "", 800, "place:ICON_Place_FishingSpot", QuestKind.Challenge, 10, "Main_Upgrade_3", 0, 0f, ""),
        ("Main_Shop_1", "main_shop_1", "요정과 첫 거래", "요정 상점에서 1번 사기", QuestGoalType.ShopPurchase, 1, "", 50, "npc:Fairy", QuestKind.Challenge, 2, "", 0, 0f, ""),
        ("Main_Shop_2", "main_shop_2", "요정 상점 단골", "요정 상점에서 5번 사기", QuestGoalType.ShopPurchase, 5, "", 300, "npc:Fairy", QuestKind.Challenge, 6, "Main_Shop_1", 0, 0f, ""),
        ("Main_Decor_1", "main_decor_1", "광장 꾸미기", "장난감 1개 놓기", QuestGoalType.PlaceDecor, 1, "", 100, "item:Decor/축구공", QuestKind.Challenge, 3, "", 0, 0f, ""),
        ("Main_Decor_2", "main_decor_2", "놀이터 만들기", "장난감 3개 놓기", QuestGoalType.PlaceDecor, 3, "", 200, "item:Decor/퍼즐", QuestKind.Challenge, 5, "Main_Decor_1", 0, 0f, ""),
        ("Main_Decor_3", "main_decor_3", "해달 놀이공원", "장난감 6개 놓기", QuestGoalType.PlaceDecor, 6, "", 500, "item:Decor/축구공", QuestKind.Challenge, 9, "Main_Decor_2", 0, 0f, ""),
        ("Main_Collection_1", "main_collection_1", "도감 시작", "도감 3칸 채우기", QuestGoalType.CollectionRegister, 3, "", 100, "nav:ICON_Nav_Collection", QuestKind.Challenge, 2, "", 0, 0f, ""),
        ("Main_Collection_2", "main_collection_2", "수집가", "도감 6칸 채우기", QuestGoalType.CollectionRegister, 6, "", 300, "nav:ICON_Nav_Collection", QuestKind.Challenge, 5, "Main_Collection_1", 0, 0f, ""),
        ("Main_Otter_1", "main_otter_1", "처음 뵙겠습니다!", "해달 1마리 만나기", QuestGoalType.MeetOtter, 1, "", 200, "otter:ICON_Otter_Fisher", QuestKind.Challenge, 3, "", 0, 0f, ""),
        ("Main_Otter_2", "main_otter_2", "해달 친구들", "해달 2마리 만나기", QuestGoalType.MeetOtter, 2, "", 500, "otter:ICON_Otter_Farmer", QuestKind.Challenge, 8, "Main_Otter_1", 0, 0f, ""),

        // 일일: Lv.9부터 (성장곡선 5장), 골드만
        ("Daily_Harvest", "daily_harvest", "오늘의 수확", "작물 30개 수확하기", QuestGoalType.Harvest, 30, "Crops", 150, "item:Farming/당근", QuestKind.Daily, 9, "", 0, 0f, ""),
        ("Daily_Catch", "daily_catch", "오늘의 낚시", "물고기 3마리 낚기", QuestGoalType.Catch, 3, "Fish", 150, "item:Fishing/고등어", QuestKind.Daily, 9, "", 0, 0f, ""),
        ("Daily_Sales", "daily_sales", "오늘의 장사", "판매로 1,000골드 벌기", QuestGoalType.EarnFromSales, 1000, "", 200, "gold", QuestKind.Daily, 9, "", 0, 0f, ""),
    };

    #region 데이터

    [MenuItem("Tools/Quest/Create Quest Data")]
    public static void CreateData()
    {
        EnsureFolder(QuestFolder);
        var gold = AssetDatabase.LoadAssetAtPath<Currency>(GoldPath);

        for (int i = 0; i < Quests.Length; i++)
        {
            var q = Quests[i];
            var (quest, isNew) = LoadOrCreate<QuestDefinition>($"{QuestFolder}/{q.asset}.asset");
            var so = new SerializedObject(quest);

            // 구조 (메인 체인·도전·일일, 경험치): 이미 있는 에셋도 표로 맞춘다
            so.FindProperty("_sortOrder").intValue = i;
            so.FindProperty("_goalType").enumValueIndex = (int)q.type;
            so.FindProperty("_kind").enumValueIndex = (int)q.kind;
            so.FindProperty("_requiredLevel").intValue = q.level;
            so.FindProperty("_expReward").intValue = q.exp;
            so.FindProperty("_expPercentOfLevel").floatValue = q.expPercent;
            so.FindProperty("_target").stringValue = q.target;

            if (isNew)
            {
                so.FindProperty("_questId").stringValue = q.id;
                so.FindProperty("_title").stringValue = q.title;
                so.FindProperty("_description").stringValue = q.description;
                so.FindProperty("_goal").intValue = q.goal;
                so.FindProperty("_rewardCurrency").objectReferenceValue = gold;
                so.FindProperty("_rewardAmount").intValue = q.reward;
                if (q.filter == "otter")
                    so.FindProperty("_collectionTab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CollectionTab>(OtterTabPath);
                else if (q.filter == "builder")
                    so.FindProperty("_builderOnly").boolValue = true;
                else if (q.filter.StartsWith("build:"))
                    SetConstructions(so.FindProperty("_constructions"), q.filter.Substring("build:".Length).Split(','));
                else if (q.filter.StartsWith("only:"))
                    so.FindProperty("_item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{q.filter.Substring("only:".Length)}.asset");
                else if (!string.IsNullOrEmpty(q.filter))
                    so.FindProperty("_itemFilter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemCategory>($"{CategoryFolder}/{q.filter}.asset");
            }
            FillIfEmpty(so, "_icon", LoadQuestIcon(q.icon, gold));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 체인의 앞 단계 연결 (모든 에셋이 만들어진 뒤에). 구조라 이미 있는 에셋도 표로 맞춘다
        foreach (var q in Quests)
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>($"{QuestFolder}/{q.asset}.asset");
            var so = new SerializedObject(quest);
            var prerequisite = string.IsNullOrEmpty(q.prerequisite)
                ? null
                : AssetDatabase.LoadAssetAtPath<QuestDefinition>($"{QuestFolder}/{q.prerequisite}.asset");
            if (!string.IsNullOrEmpty(q.prerequisite) && prerequisite == null)
                Debug.LogWarning($"[QuestSetup] 앞 단계를 찾을 수 없습니다: {q.asset} ← {q.prerequisite}");
            so.FindProperty("_prerequisite").objectReferenceValue = prerequisite;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // DB는 항상 폴더 안의 모든 퀘스트로 다시 채움 (사람이 추가한 퀘스트도 포함)
        var (database, _) = LoadOrCreate<QuestDatabase>(DatabasePath);
        var dbSo = new SerializedObject(database);
        SetList(dbSo.FindProperty("_quests"), FindAll<QuestDefinition>(QuestFolder));
        dbSo.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
    }

    private static void SetConstructions(SerializedProperty list, string[] ids)
    {
        list.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++)
        {
            var construction = AssetDatabase.LoadAssetAtPath<ConstructionDefinition>($"{ConstructionFolder}/{ids[i]}.asset");
            if (construction == null)
                Debug.LogWarning($"[QuestSetup] 건설을 찾을 수 없습니다: {ids[i]}");
            list.GetArrayElementAtIndex(i).objectReferenceValue = construction;
        }
    }

    private static Sprite LoadQuestIcon(string icon, Currency gold)
    {
        int colon = icon.IndexOf(':');
        string kind = colon < 0 ? icon : icon.Substring(0, colon);
        string value = colon < 0 ? "" : icon.Substring(colon + 1);

        switch (kind)
        {
            case "item":
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{value}.asset");
                if (item == null)
                    Debug.LogWarning($"[QuestSetup] 아이템을 찾을 수 없습니다: {value}");
                return item != null ? item.Icon : null;
            case "gold":
                return gold != null ? gold.Icon : null;
            case "place":
                return LoadIcon(PlaceIconFolder, value);
            case "otter":
                return ImportSprite(OtterFolder, value);
            case "npc":
                return ImportSprite("Assets/Art/Npc", value);
            case "nav":
                return LoadIcon(NavIconFolder, value);
            case "settle":
                return SettlementSetup.LoadArt(value);
            default:
                throw new System.ArgumentException($"알 수 없는 아이콘 종류: {icon}");
        }
    }

    #endregion

    #region 프리팹 (목록 한 줄)

    public static void BuildPrefabs()
    {
        EnsureFolder(Path.GetDirectoryName(RowPrefabPath).Replace('\\', '/'));

        var root = new GameObject("QuestRow", typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(936, 166);
        var background = root.AddComponent<Image>();
        background.sprite = LoadSprite(CommonSpriteFolder, "UI_Button_Paper");
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;
        var group = root.AddComponent<CanvasGroup>();
        var layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = 166;

        // 왼쪽 살구색 그림 칸
        var frame = CreateImage("IconFrame", rect, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Filled"), false);
        Place(frame.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 2), new Vector2(140, 140));
        var icon = CreateImage("Icon", frame.rectTransform, null, false);
        Stretch(icon.rectTransform, 0);
        icon.rectTransform.offsetMin = new Vector2(20, 26); // 칸 아래쪽 입체 턱만큼 위로
        icon.rectTransform.offsetMax = new Vector2(-20, -16);
        icon.preserveAspect = true;
        var check = CreateImage("ClaimedCheck", frame.rectTransform, ImportSprite(CollectionSpriteFolder, "UI_Icon_CheckSmall"), false);
        Place(check.rectTransform, new Vector2(1, 1), new Vector2(12, 12), new Vector2(56, 56));
        check.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true; // 줄이 흐려져도 체크는 또렷하게

        // 제목, 조건 (보상 칸과 겹치지 않도록 오른쪽 380은 비움)
        var title = CreateText("Title", rect, _titleFont, "퀘스트 제목", 36, Cocoa);
        title.alignment = TextAlignmentOptions.Left;
        FitText(title, 28, 36);
        TopBand(title.rectTransform, 172, 380, 20, 50);

        var description = CreateText("Description", rect, _bodyFont, "조건", 27, Body);
        description.alignment = TextAlignmentOptions.Left;
        FitText(description, 20, 27);
        TopBand(description.rectTransform, 174, 380, 70, 38);

        var bar = BuildBar(rect, "ProgressBar");
        Place((RectTransform)bar.transform, new Vector2(0, 1), new Vector2(174, -116), new Vector2(236, 30));

        // "320 / 500"처럼 길어지면 줄바꿈 대신 글자를 줄임
        var progress = CreateText("ProgressText", rect, _titleFont, "0 / 3", 26, Body);
        progress.alignment = TextAlignmentOptions.Left;
        FitText(progress, 18, 26);
        Place(progress.rectTransform, new Vector2(0, 1), new Vector2(422, -110), new Vector2(148, 42));

        // 보상: 코인 아이콘 + 금액, 그 아래 경험치 칩 (버튼 왼쪽 열)
        var rewardIcon = CreateImage("RewardIcon", rect, null, false);
        Place(rewardIcon.rectTransform, new Vector2(1, 1), new Vector2(-274, -14), new Vector2(54, 54));
        rewardIcon.preserveAspect = true;
        var reward = CreateText("RewardText", rect, _titleFont, "100", 30, Cocoa);
        Place(reward.rectTransform, new Vector2(1, 1), new Vector2(-236, -68), new Vector2(130, 40));

        var expTag = CreateImage("ExpTag", rect, LoadSprite(CommonSpriteFolder, "UI_Chip_Selected"), false);
        expTag.pixelsPerUnitMultiplier = 1.6f; // 칩이 낮아서 테두리를 얇게
        Place(expTag.rectTransform, new Vector2(1, 1), new Vector2(-236, -112), new Vector2(130, 38));
        var expText = CreateText("Label", expTag.rectTransform, _titleFont, "경험치 50", 20, Color.white);
        expText.enableAutoSizing = true;
        expText.fontSizeMin = 14;
        expText.fontSizeMax = 20;
        Stretch(expText.rectTransform, 0);
        expText.rectTransform.offsetMin = new Vector2(6, 4);
        expText.rectTransform.offsetMax = new Vector2(-6, 0);

        // 일일 퀘스트: 그림 칸 왼쪽 위 모서리의 산호색 칩
        var dailyTag = CreateImage("DailyTag", rect, LoadSprite(CommonSpriteFolder, "UI_Tag_Highlight"), false);
        Place(dailyTag.rectTransform, new Vector2(0, 1), new Vector2(6, 2), new Vector2(76, 36));
        var dailyText = CreateText("Label", dailyTag.rectTransform, _titleFont, "일일", 20, Color.white);
        Stretch(dailyText.rectTransform, 0);
        dailyText.rectTransform.offsetMin = new Vector2(0, 3);

        var claimable = ImportSprite(CollectionSpriteFolder, "UI_Tab_Gold");
        var idle = ImportSprite(CommonSpriteFolder, "UI_Button_Idle");
        var buttonImage = CreateImage("ClaimButton", rect, idle, true);
        buttonImage.pixelsPerUnitMultiplier = 1.3f; // 버튼이 낮아서 테두리를 조금 얇게
        Place(buttonImage.rectTransform, new Vector2(1, 0.5f), new Vector2(-18, 4), new Vector2(212, 92));
        var button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        var colors = button.colors;
        colors.disabledColor = Color.white; // 진행 중·완료는 회색 스프라이트로 표현하므로 따로 흐리게 하지 않음
        button.colors = colors;
        var label = CreateText("Label", buttonImage.rectTransform, _titleFont, "진행 중", 32, LightBrown);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 8);

        var view = root.AddComponent<QuestRowView>();
        Set(view, "_icon", icon);
        Set(view, "_titleText", title);
        Set(view, "_descriptionText", description);
        Set(view, "_progressBar", bar);
        Set(view, "_progressText", progress);
        Set(view, "_rewardIcon", rewardIcon);
        Set(view, "_rewardText", reward);
        Set(view, "_expTag", expTag.gameObject);
        Set(view, "_expText", expText);
        Set(view, "_dailyTag", dailyTag.gameObject);
        Set(view, "_button", button);
        Set(view, "_buttonImage", buttonImage);
        Set(view, "_buttonLabel", label);
        Set(view, "_claimedCheck", check.gameObject);
        Set(view, "_group", group);
        Set(view, "_claimableSprite", claimable);
        Set(view, "_idleSprite", idle);

        check.gameObject.SetActive(false);
        dailyTag.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        Object.DestroyImmediate(root);
    }

    // 크림색 홈 + 초록 채움. 채움 길이는 ProgressBarView가 정함
    private static ProgressBarView BuildBar(Transform parent, string name)
    {
        var track = CreateImage(name, parent, LoadSprite(CommonSpriteFolder, "UI_Slider_Track"), false);
        track.pixelsPerUnitMultiplier = 2f; // 바가 낮아서 테두리를 얇게
        var fill = CreateImage("Fill", track.rectTransform, ImportSprite(CommonSpriteFolder, "UI_Bar_Fill"), false);
        fill.pixelsPerUnitMultiplier = 2f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0.5f, 1);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        var view = track.gameObject.AddComponent<ProgressBarView>();
        Set(view, "_fill", fill.rectTransform);
        return view;
    }

    private static void FitText(TextMeshProUGUI text, float min, float max)
    {
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    #endregion

    #region 화면

    /// <summary>
    /// 퀘스트 화면을 canvas 아래에 만든다 (닫힌 채로 시작). 1080×1920 기준, 시안 수치를 환산.
    /// </summary>
    public static (QuestPresenter presenter, UIPopupAnimator screen) BuildScreen(RectTransform canvas)
    {
        // 씬을 연 뒤 호출되므로 에셋은 경로로 다시 불러온다
        var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath).GetComponent<QuestRowView>();

        var screen = CreateRect("QuestScreen", canvas);
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

        var panel = CreateImage("Panel", safe, LoadPanelSprite(), false);
        panel.type = Image.Type.Sliced;
        Stretch(panel.rectTransform, 0);
        panel.rectTransform.offsetMin = new Vector2(32, 40);
        panel.rectTransform.offsetMax = new Vector2(-32, -110);
        var panelRect = panel.rectTransform;

        BuildHeader(panelRect);
        var (levelText, expText, expBar) = BuildOverall(panelRect);
        var (scroll, content) = BuildList(panelRect);
        var (claimAll, claimAllImage, claimAllLabel) = BuildClaimAll(panelRect);

        BuildTitleBoard(safe);
        var close = BuildCloseButton(safe);

        var animator = screen.gameObject.AddComponent<UIPopupAnimator>();
        Set(animator, "_dim", dimGroup);
        Set(animator, "_panel", safe);
        Set(animator, "_panelGroup", safeGroup);
        Set(animator, "_dimButton", dimButton);
        var animatorSo = new SerializedObject(animator);
        animatorSo.FindProperty("_startScale").floatValue = 0.92f; // 도감처럼 은은하게
        animatorSo.FindProperty("_overshoot").floatValue = 1.2f;
        animatorSo.FindProperty("_endScale").floatValue = 0.95f;
        animatorSo.ApplyModifiedPropertiesWithoutUndo();

        var presenter = safe.gameObject.AddComponent<QuestPresenter>();
        Set(presenter, "_rowPrefab", rowPrefab);
        Set(presenter, "_rowParent", content);
        Set(presenter, "_scrollRect", scroll);
        Set(presenter, "_levelText", levelText);
        Set(presenter, "_expText", expText);
        Set(presenter, "_expBar", expBar);
        Set(presenter, "_claimAllButton", claimAll);
        Set(presenter, "_claimAllImage", claimAllImage);
        Set(presenter, "_claimAllLabel", claimAllLabel);
        Set(presenter, "_closeButton", close);
        Set(presenter, "_claimAllActiveSprite", ImportSprite(CollectionSpriteFolder, "UI_Tab_Gold"));
        Set(presenter, "_claimAllIdleSprite", ImportSprite(CommonSpriteFolder, "UI_Button_Idle"));
        Set(presenter, "_screen", animator);

        screen.gameObject.SetActive(false);
        return (presenter, animator);
    }

    // "퀘스트" 나무 간판: 패널 윗선에 걸침, 왼쪽에 두루마리 아이콘, 오른쪽에 잎
    private static void BuildTitleBoard(RectTransform parent)
    {
        var board = CreateImage("TitleBoard", parent, ImportSprite(CollectionSpriteFolder, "UI_TitleBoard"), false);
        Place(board.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(440, 124));

        var title = CreateText("Title", board.rectTransform, _titleFont, "퀘스트", 68, Cocoa);
        Stretch(title.rectTransform, 0);
        title.rectTransform.offsetMin = new Vector2(70, 10); // 아이콘 자리만큼 오른쪽으로

        var icon = CreateImage("Icon", board.rectTransform, LoadIcon(NavIconFolder, "ICON_Nav_Quest"), false);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-138, 8), new Vector2(112, 112));
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.preserveAspect = true;

        var leaf = CreateImage("Leaf", board.rectTransform, ImportSprite(CollectionSpriteFolder, "UI_Deco_Leaf"), false);
        Place(leaf.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(196, 6), new Vector2(52, 52));
        leaf.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        leaf.rectTransform.localScale = new Vector3(-1, 1, 1);
    }

    // 해달 그림 + "왕국 성장" + 부제
    private static void BuildHeader(RectTransform panel)
    {
        var frame = CreateImage("HeaderArt", panel, LoadSprite(InventorySpriteFolder, "UI_Inventory_Slot_Filled"), false);
        Place(frame.rectTransform, new Vector2(0, 1), new Vector2(48, -92), new Vector2(330, 224));
        var otter = CreateImage("Otter", frame.rectTransform, ImportSprite(OtterFolder, "ICON_Otter_Farmer"), false);
        Stretch(otter.rectTransform, 0);
        otter.rectTransform.offsetMin = new Vector2(24, 30);
        otter.rectTransform.offsetMax = new Vector2(-24, -14);
        otter.preserveAspect = true;

        var title = CreateText("Title", panel, _titleFont, "왕국 성장", 64, Cocoa);
        title.alignment = TextAlignmentOptions.Left;
        TopBand(title.rectTransform, 410, 48, 112, 84);

        var subtitle = CreateText("Subtitle", panel, _bodyFont, "해달들과 함께 왕국을 키워요!", 30, LightBrown);
        subtitle.alignment = TextAlignmentOptions.Left;
        FitText(subtitle, 22, 30);
        TopBand(subtitle.rectTransform, 412, 40, 206, 48);
    }

    // "Lv.3 [경험치 바] 120 / 300" (경험치는 퀘스트 보상으로만 쌓임)
    private static (TextMeshProUGUI level, TextMeshProUGUI exp, ProgressBarView bar) BuildOverall(RectTransform panel)
    {
        var box = CreateImage("Overall", panel, LoadSprite(CommonSpriteFolder, "UI_Button_Paper"), false);
        TopBand(box.rectTransform, 40, 40, 342, 96);
        var rect = box.rectTransform;

        var label = CreateText("Level", rect, _titleFont, "Lv.1", 38, Cocoa);
        label.alignment = TextAlignmentOptions.Left;
        Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(36, 4), new Vector2(150, 54));

        var bar = BuildBar(rect, "Bar");
        var barRect = (RectTransform)bar.transform;
        barRect.anchorMin = new Vector2(0, 0.5f);
        barRect.anchorMax = new Vector2(1, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.offsetMin = new Vector2(190, -14);
        barRect.offsetMax = new Vector2(-236, 22);

        var count = CreateText("Exp", rect, _titleFont, "0 / 100", 30, Cocoa);
        count.alignment = TextAlignmentOptions.Right;
        count.enableAutoSizing = true;
        count.fontSizeMin = 20;
        count.fontSizeMax = 30;
        Place(count.rectTransform, new Vector2(1, 0.5f), new Vector2(-36, 4), new Vector2(190, 50));
        return (label, count, bar);
    }

    private static (ScrollRect scroll, RectTransform content) BuildList(RectTransform panel)
    {
        var scrollImage = CreateImage("ScrollView", panel, null, true);
        scrollImage.color = new Color(1, 1, 1, 0); // 빈 곳을 끌어도 스크롤되게
        Stretch(scrollImage.rectTransform, 0);
        scrollImage.rectTransform.offsetMin = new Vector2(40, 196);
        scrollImage.rectTransform.offsetMax = new Vector2(-40, -458);

        var viewport = CreateRect("Viewport", scrollImage.rectTransform);
        Stretch(viewport, 0);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14;
        layout.padding = new RectOffset(0, 0, 6, 10); // 체크 표시가 칸 위로 튀어나오는 만큼
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewport;
        scroll.content = content;
        return (scroll, content);
    }

    private static (Button button, Image image, TextMeshProUGUI label) BuildClaimAll(RectTransform panel)
    {
        var image = CreateImage("ClaimAllButton", panel, ImportSprite(CollectionSpriteFolder, "UI_Tab_Gold"), true);
        Place(image.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 54), new Vector2(500, 116));
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.disabledColor = Color.white; // 받을 것이 없으면 회색 스프라이트로 표현
        button.colors = colors;

        var label = CreateText("Label", image.rectTransform, _titleFont, "모두 받기", 46, Cocoa);
        Stretch(label.rectTransform, 0);
        label.rectTransform.offsetMin = new Vector2(0, 10);

        var leaf = ImportSprite(CollectionSpriteFolder, "UI_Deco_Leaf");
        var left = CreateImage("LeafLeft", image.rectTransform, leaf, false);
        Place(left.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-290, 4), new Vector2(60, 60));
        left.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        var right = CreateImage("LeafRight", image.rectTransform, leaf, false);
        Place(right.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(290, 4), new Vector2(60, 60));
        right.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        right.rectTransform.localScale = new Vector3(-1, 1, 1);

        return (button, image, label);
    }

    #endregion
}
