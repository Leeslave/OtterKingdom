using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CollectionSetup;

/// <summary>
/// Lv.10~15 해달의 부탁 (P4, Docs/특성사회성장_P4_기획반영.md 9장): 새 해달이 찾아옴 → 살 집(입주 부탁) → 그 해달의 일터(건물 부탁).
/// 건물 부탁은 왕국 레벨 잠금(끝내야 다음 레벨) — 첫 모임(Lv.10) 뒤 11 · 12 · 13 · 14 · 15.
/// 부탁 · 방명록을 만들고 정착 설정의 부탁 목록에 넣는다. 여러 번 실행해도 결과가 같다
/// </summary>
public static partial class SettlementSetup
{
    // P4 부탁의 콘텐츠 버전 (정착 진행 전 옛 세이브도 처음부터 진행)
    private const int P4ContentVersion = 4;

    private const string P4FirstHome = "p4_first_home";
    private const string P4TraderSettled = "p4_trader_settled";
    private const string P4ShopBuilt = "p4_shop_built";
    private const string P4LumberSettled = "p4_lumber_settled";
    private const string P4WorkshopBuilt = "p4_workshop_built";
    private const string P4MinerSettled = "p4_miner_settled";
    private const string P4QuarryBuilt = "p4_quarry_built";
    private const string P4FarmerSettled = "p4_farmer_settled";
    private const string P4GranaryBuilt = "p4_granary_built";
    private const string FirstGatheringDevelopment = "first_gathering_complete";

    // 순서, ID, 제목, 설명(부탁 화면에서 해달이 하는 말), 부탁하는 해달, 필요 발전,
    // 지을 건물 에셋(입주 부탁이면 null), 입주할 해달들, 결과 발전, 왕국 레벨(0 = 잠금 없음), 찾아오는 해달, 완료 문구, 방명록(해달, 꼬리표, 글)
    private static readonly (int order, string id, string title, string description, string requester, string requires,
        string building, string[] settlers, string result, int level, string arrival, string done,
        (string otter, string tag, string message) entry)[] P4StoryTable =
    {
        (30, "req_p4_first_home", "놀러 온 친구의 집", "광장에 놀러 오는 해달들이 늘었어요.\n머물 수 있는 작은 집을 하나 지어 줄래요?",
            "otter_p3_neighbor", FirstGatheringDevelopment, "Building_SmallHouse", null, P4FirstHome, 11, "otter_toy_jjalrang",
            "작은 집이 생겼어요! 장사를 하고 싶다는 짤랑이가 찾아왔어요.",
            ("otter_p3_neighbor", "새 집", "놀러 온 친구가 머물 작은 집이 생겼어요.")),
        (31, "req_p4_trader_home", "짤랑이가 살 집", "짤랑짤랑! 이 마을에서 장사를 하고 싶어요.\n우선 지낼 집이 있으면 좋겠어요.",
            "otter_toy_jjalrang", P4FirstHome, null, new[] { "otter_toy_jjalrang" }, P4TraderSettled, 0, null,
            "짤랑이가 마을 주민이 되었어요!",
            ("otter_toy_jjalrang", "입주", "오늘부터 저도 이 마을 해달이에요. 짤랑!")),
        (32, "req_p4_shop", "짤랑이의 가게", "좋은 물건은 제값에! 광장에 가게를 차리면\n요정도 함께 장사하고 싶대요.",
            "otter_toy_jjalrang", P4TraderSettled, "Building_Shop", null, P4ShopBuilt, 12, "otter_toy_kungkung",
            "해달 상점이 문을 열었어요! 요정 상점도 이곳으로 이사했어요. (판매가 +10%)",
            ("otter_toy_jjalrang", "개업", "해달 상점 개업! 요정이랑 같이 장사해요.")),
        (33, "req_p4_lumber_home", "꾸벅이와 쿵쿵이의 집", "숲에서 나무를 하는 쿵쿵이가 찾아왔어요.\n늘 낮잠만 자던 꾸벅이도 같이 나무를 하겠대요. 둘이 살 집이 필요해요.",
            "otter_toy_kungkung", P4ShopBuilt, null, new[] { "otter_sleepy", "otter_toy_kungkung" }, P4LumberSettled, 0, null,
            "꾸벅이와 쿵쿵이가 마을 주민이 되었어요!",
            ("otter_sleepy", "입주", "하암… 이제 여기가 우리 집이에요. 나무도 열심히 할게요.")),
        (34, "req_p4_workshop", "나무가 모자라요", "집을 짓다 보니 목재가 늘 모자라요.\n목공소가 있으면 숲에서 나무를 더 잘 다듬을 수 있어요.",
            "otter_toy_kungkung", P4LumberSettled, "Building_Workshop", null, P4WorkshopBuilt, 13, "otter_toy_degul",
            "목공소가 생겼어요! 숲을 개간하면 목재를 더 얻어요. (개간 목재 +20%)",
            ("otter_toy_kungkung", "목공소", "쿵쿵! 목공소에서 나무를 척척 다듬어요.")),
        (35, "req_p4_miner_home", "데굴이가 살 집", "동글동글한 돌을 좋아하는 데굴이가 찾아왔어요.\n깡깡이와 함께 돌을 캐고 싶대요.",
            "otter_toy_degul", P4WorkshopBuilt, null, new[] { "otter_toy_degul" }, P4MinerSettled, 0, null,
            "데굴이가 마을 주민이 되었어요!",
            ("otter_toy_degul", "입주", "데굴데굴~ 돌이 많은 마을이라 좋아요.")),
        (36, "req_p4_quarry", "돌 깨는 일터", "깡깡이랑 같이 돌을 다듬을 작업소가 있으면\n돌을 훨씬 많이 얻을 수 있어요.",
            "otter_toy_degul", P4MinerSettled, "Building_Quarry", null, P4QuarryBuilt, 14, "otter_toy_bodeul",
            "채석 작업소가 생겼어요! 바위와 광산에서 돌을 더 얻어요. (돌 +20%)",
            ("otter_miner", "채석", "데굴이랑 같이 캐니까 돌이 쑥쑥 나와요. 깡깡!")),
        (37, "req_p4_farmer_home", "보들이가 살 집", "새싹을 좋아하는 보들이가 찾아왔어요.\n새싹이와 함께 밭을 돌보고 싶대요.",
            "otter_toy_bodeul", P4QuarryBuilt, null, new[] { "otter_toy_bodeul" }, P4FarmerSettled, 0, null,
            "보들이가 마을 주민이 되었어요!",
            ("otter_toy_bodeul", "입주", "흙냄새 나는 마을이에요. 오늘부터 잘 부탁해요!")),
        (38, "req_p4_granary", "밭 옆 창고", "거둔 작물을 잘 보관할 창고가 있으면\n수확이 훨씬 넉넉해질 거예요.",
            "otter_toy_bodeul", P4FarmerSettled, "Building_Granary", null, P4GranaryBuilt, 15, null,
            "농업 창고가 생겼어요! 밭에서 거두는 작물이 늘어나요. (수확량 +10%)",
            ("otter_farmer", "창고", "보들이랑 같이 거두니 창고가 가득해요!")),
    };

    [MenuItem("Tools/Settlement/Apply P4 Story")]
    public static void ApplyP4Story()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        if (config == null)
        {
            Debug.LogError("[SettlementSetup] 정착 설정이 없습니다. Tools/Settlement/Create Data를 먼저 실행하세요.");
            return;
        }
        CreateBuildingData();
        var configSo = new SerializedObject(config);
        var otters = new Dictionary<string, SettlementOtterDefinition>();
        foreach (var otter in config.Otters)
        {
            if (otter != null)
                otters[otter.OtterId] = otter;
        }
        CreateP4Story(configSo, otters);
        configSo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log($"[SettlementSetup] P4 해달의 부탁 적용 완료: 부탁 {P4StoryTable.Length}개");
    }

    private static void CreateP4Story(SerializedObject configSo, Dictionary<string, SettlementOtterDefinition> otters)
    {
        SettlementOtterDefinition Otter(string id) => !string.IsNullOrEmpty(id) && otters.TryGetValue(id, out var o) ? o : null;

        foreach (var row in P4StoryTable)
        {
            var entry = LoadOrCreate<GuestbookEntryDefinition>($"{DataFolder}/Guestbook/gb_{row.id}.asset").asset;
            var entrySo = new SerializedObject(entry);
            entrySo.FindProperty("_entryId").stringValue = $"gb_{row.id}";
            entrySo.FindProperty("_otter").objectReferenceValue = Otter(row.entry.otter);
            entrySo.FindProperty("_tag").stringValue = row.entry.tag;
            entrySo.FindProperty("_message").stringValue = row.entry.message;
            entrySo.ApplyModifiedPropertiesWithoutUndo();
            AddUnique(configSo.FindProperty("_guestbookEntries"), entry);

            var building = row.building != null ? LoadBuilding(row.building) : null;
            var requester = Otter(row.requester);
            var settlers = new List<SettlementOtterDefinition>();
            if (row.settlers != null)
            {
                foreach (var id in row.settlers)
                {
                    var otter = Otter(id);
                    if (otter != null)
                        settlers.Add(otter);
                    else
                        Debug.LogWarning($"[SettlementSetup] 입주할 해달이 없습니다: {id}");
                }
            }

            var request = LoadOrCreate<BoardRequestDefinition>($"{DataFolder}/Requests/{row.id}.asset").asset;
            request.SetupRecordAction(building, 1, settlers, row.result);
            var so = new SerializedObject(request);
            so.FindProperty("_requestId").stringValue = row.id;
            so.FindProperty("_order").intValue = row.order;
            so.FindProperty("_category").enumValueIndex = (int)RequestCategory.Main;
            so.FindProperty("_contentVersion").intValue = P4ContentVersion;
            so.FindProperty("_title").stringValue = row.title;
            so.FindProperty("_description").stringValue = row.description;
            so.FindProperty("_icon").objectReferenceValue = building != null ? building.Icon : requester != null ? requester.Portrait : null;
            so.FindProperty("_requester").objectReferenceValue = requester;
            so.FindProperty("_requiredDevelopment").stringValue = row.requires;
            so.FindProperty("_minResidents").intValue = 0;
            so.FindProperty("_construction").objectReferenceValue = null;
            so.FindProperty("_clearZone").objectReferenceValue = null;
            so.FindProperty("_assignSpecialist").objectReferenceValue = null;
            so.FindProperty("_assignRole").objectReferenceValue = null;
            so.FindProperty("_completionTask").objectReferenceValue = null;
            so.FindProperty("_settles").arraySize = 0;
            var arrivals = so.FindProperty("_arrivals");
            var arrival = Otter(row.arrival);
            arrivals.arraySize = arrival != null ? 1 : 0;
            if (arrival != null)
            {
                var element = arrivals.GetArrayElementAtIndex(0);
                element.FindPropertyRelative("_otter").objectReferenceValue = arrival;
                element.FindPropertyRelative("_state").enumValueIndex = (int)ResidentState.Visitor;
            }
            so.FindProperty("_stageOnComplete").intValue = -1;
            so.FindProperty("_completionEntry").objectReferenceValue = entry;
            so.FindProperty("_completionMessage").stringValue = row.done;
            so.FindProperty("_kingdomLevel").intValue = row.level;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(request);
            AddUnique(configSo.FindProperty("_requests"), request);
        }
    }
}
