using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CollectionSetup;

/// <summary>
/// 특성 · 장난감 방문 해달 (P4, Docs/특성사회성장_P4_기획반영.md): 기존 9마리에 특성을 붙이고, 장난감 해달 11마리와 첫 방문 방명록을 만든다.
/// 그림은 임시: 기존 광장 해달 프리팹·얼굴을 빌려 쓰고 색(_plazaTint)으로 구분. 여러 번 실행해도 결과가 같다
/// </summary>
public static partial class SettlementSetup
{
    // 기존 해달의 특성
    private static readonly (string otterId, OtterTrait trait)[] ExistingTraitTable =
    {
        ("otter_first", OtterTrait.Exploring),
        ("otter_painter", OtterTrait.Crafting),
        ("otter_sleepy", OtterTrait.Woodcutting),
        ("otter_builder", OtterTrait.Building),
        ("otter_miner", OtterTrait.Mining),
        ("otter_farmer", OtterTrait.Farming),
        ("otter_fisher", OtterTrait.Fishing),
        ("otter_receptionist", OtterTrait.Recording),
        ("otter_p3_neighbor", OtterTrait.Hauling),
    };

    // 장난감 해달: 에셋 이름, ID, 이름, 특성, 등급(0 흔함 · 1 레어), 빌려 쓰는 광장 프리팹, 얼굴을 빌릴 해달, 색, 말 3개, 입주 말, 집 없을 때 말, 첫 방문 방명록
    private static readonly (string asset, string id, string name, OtterTrait trait, int tier, string prefab, string portraitOf, Color tint,
        string[] lines, string moveIn, string homeless, string arrival)[] ToyOtterTable =
    {
        ("Otter_Toy_Kungkung", "otter_toy_kungkung", "쿵쿵이", OtterTrait.Woodcutting, 0, "PlazaOtter_Sleepy", "otter_sleepy", new Color(0.86f, 0.76f, 0.66f),
            new[] { "나무 한 그루쯤은 번쩍!", "쿵쿵 걸으면 다들 쳐다봐요.", "숲 냄새가 좋아요." },
            "빈 집이 있다고요?\n제가 살아도 될까요?", "덩치가 커서 집이 꼭 필요해요.\n빈 집이 생기면 알려 줘요!",
            "공 차는 소리에 숲에서 나와 봤어요."),
        ("Otter_Toy_Degul", "otter_toy_degul", "데굴이", OtterTrait.Mining, 0, "PlazaOtter_Miner", "otter_miner", new Color(0.82f, 0.84f, 0.95f),
            new[] { "동글동글한 돌이 제일 좋아요.", "데굴데굴~", "광산 돌은 반짝여요!" },
            "여기 돌이 많아서 좋아요.\n저 집에서 살아도 될까요?", "돌 굴리며 기다릴게요.\n빈 집이 생기면 불러 줘요!",
            "굴러가는 공을 따라왔어요!"),
        ("Otter_Toy_Bodeul", "otter_toy_bodeul", "보들이", OtterTrait.Farming, 0, "PlazaOtter", "otter_farmer", new Color(0.9f, 1f, 0.82f),
            new[] { "새싹은 보들보들해요.", "흙을 만지면 기분이 좋아요.", "오늘은 물 줄 날이에요." },
            "밭 옆이라 딱 좋아요!\n저 집에서 살아도 될까요?", "밭 가까이 살고 싶어요.\n빈 집이 생기면 알려 줘요!",
            "들판에서 공 소리를 듣고 왔어요."),
        ("Otter_Toy_Pongdang", "otter_toy_pongdang", "퐁당이", OtterTrait.Fishing, 0, "PlazaOtter_Fisher", "otter_fisher", new Color(0.8f, 0.92f, 1f),
            new[] { "퐁당! 물에 들어가고 싶어요.", "조개는 손으로 톡톡.", "바닷바람이 시원해요." },
            "바다가 보이는 집이네요!\n여기 살아도 될까요?", "바다 가까운 집이면 좋겠어요.\n빈 집이 생기면 불러 줘요!",
            "바닷가에서 헤엄치다 놀러 왔어요."),
        ("Otter_Toy_Yeongcha", "otter_toy_yeongcha", "영차", OtterTrait.Hauling, 0, "PlazaOtter_Snack", "otter_first", new Color(1f, 0.86f, 0.76f),
            new[] { "영차영차! 짐은 맡겨요.", "무거운 것도 거뜬해요.", "다 옮기고 나면 개운해요." },
            "제 짐도 다 들어가겠어요!\n여기 살아도 될까요?", "짐 풀 곳이 필요해요.\n빈 집이 생기면 알려 줘요!",
            "짐 보따리 메고 놀러 왔어요."),
        ("Otter_Toy_Jjalrang", "otter_toy_jjalrang", "짤랑이", OtterTrait.Trading, 0, "PlazaOtter_Clerk", "otter_receptionist", new Color(1f, 0.95f, 0.72f),
            new[] { "짤랑짤랑, 동전 소리 좋죠?", "좋은 물건은 제값에!", "오늘 장사 잘될 것 같아요." },
            "장사하기 좋은 마을이에요.\n저 집에서 살아도 될까요?", "가게 낼 집이 필요해요.\n빈 집이 생기면 불러 줘요!",
            "북적이는 소리에 장사하러 왔어요."),
        ("Otter_Toy_Jaejal", "otter_toy_jaejal", "재잘이", OtterTrait.Trading, 1, "PlazaOtter_Painter", "otter_painter", new Color(1f, 0.86f, 0.9f),
            new[] { "있잖아요, 그거 들었어요?", "수다는 하루 종일 할 수 있어요.", "다들 무슨 얘기 해요?" },
            "이웃이 많아서 좋아요!\n여기 살아도 될까요?", "이야기할 이웃이 많아서 좋아요.\n빈 집이 생기면 알려 줘요!",
            "퍼즐 맞추는 소리에 수다 떨러 왔어요."),
        ("Otter_Toy_Kongkong", "otter_toy_kongkong", "콩콩이", OtterTrait.Building, 1, "PlazaOtter_Miner", "otter_miner", new Color(1f, 0.82f, 0.72f),
            new[] { "콩콩, 못 박는 소리 좋아요.", "고칠 데 있으면 불러요!", "튼튼하게 지어야죠." },
            "잘 지은 집이네요!\n여기 살아도 될까요?", "집이 없으면 제가 지어도 되는데…\n빈 집이 생기면 알려 줘요!",
            "망치 들고 구경 왔어요."),
        ("Otter_Toy_Kkomkkom", "otter_toy_kkomkkom", "꼼꼼이", OtterTrait.Crafting, 1, "PlazaOtter_Painter", "otter_painter", new Color(0.86f, 0.86f, 1f),
            new[] { "한 땀 한 땀 꼼꼼하게.", "퍼즐 조각은 하나도 안 빠뜨려요.", "손으로 만드는 게 좋아요." },
            "구석구석 잘 정리된 집이에요.\n여기 살아도 될까요?", "작업할 방이 있으면 좋겠어요.\n빈 집이 생기면 불러 줘요!",
            "퍼즐 조각을 맞추러 왔어요."),
        ("Otter_Toy_Duribeon", "otter_toy_duribeon", "두리번", OtterTrait.Exploring, 1, "PlazaOtter_Snack", "otter_first", new Color(0.8f, 1f, 0.94f),
            new[] { "저 너머엔 뭐가 있을까요?", "길은 제가 잘 찾아요.", "두리번두리번…" },
            "여기를 베이스캠프로 할래요!\n살아도 될까요?", "돌아올 집이 있으면 좋겠어요.\n빈 집이 생기면 알려 줘요!",
            "길을 찾다가 이 마을을 발견했어요."),
        ("Otter_Toy_Kkeujeok", "otter_toy_kkeujeok", "끄적이", OtterTrait.Recording, 1, "PlazaOtter_Clerk", "otter_receptionist", new Color(0.9f, 0.86f, 1f),
            new[] { "오늘 일도 적어 둘게요.", "끄적끄적…", "기록은 기억보다 오래가요." },
            "책상 놓을 자리가 있네요!\n여기 살아도 될까요?", "기록할 책상이 필요해요.\n빈 집이 생기면 불러 줘요!",
            "이 마을 이야기를 적으러 왔어요."),
    };

    [MenuItem("Tools/Settlement/Apply Trait Otters")]
    public static void ApplyTraitOtters()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        if (config == null)
        {
            Debug.LogError("[SettlementSetup] 정착 설정이 없습니다. Tools/Settlement/Create Data를 먼저 실행하세요.");
            return;
        }
        var configSo = new SerializedObject(config);
        var otters = new Dictionary<string, SettlementOtterDefinition>();
        foreach (var otter in config.Otters)
        {
            if (otter != null)
                otters[otter.OtterId] = otter;
        }
        CreateTraitData(configSo, otters);
        configSo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log($"[SettlementSetup] 특성 적용 완료: 기존 해달 {ExistingTraitTable.Length}마리 특성, 장난감 해달 {ToyOtterTable.Length}마리");
    }

    private static void CreateTraitData(SerializedObject configSo, Dictionary<string, SettlementOtterDefinition> otters)
    {
        foreach (var (otterId, trait) in ExistingTraitTable)
        {
            if (!otters.TryGetValue(otterId, out var otter))
            {
                Debug.LogWarning($"[SettlementSetup] 특성을 붙일 해달이 없습니다: {otterId}");
                continue;
            }
            var so = new SerializedObject(otter);
            so.FindProperty("_trait").intValue = (int)trait;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var row in ToyOtterTable)
        {
            var entry = LoadOrCreate<GuestbookEntryDefinition>($"{DataFolder}/Guestbook/gb_{row.id}_arrival.asset").asset;
            var otter = LoadOrCreate<SettlementOtterDefinition>($"{DataFolder}/Otters/{row.asset}.asset").asset;

            var entrySo = new SerializedObject(entry);
            entrySo.FindProperty("_entryId").stringValue = $"gb_{row.id}_arrival";
            entrySo.FindProperty("_otter").objectReferenceValue = otter;
            entrySo.FindProperty("_tag").stringValue = "첫 방문";
            entrySo.FindProperty("_message").stringValue = row.arrival;
            entrySo.ApplyModifiedPropertiesWithoutUndo();

            otters.TryGetValue(row.portraitOf, out var lookalike);
            var so = new SerializedObject(otter);
            so.FindProperty("_otterId").stringValue = row.id;
            so.FindProperty("_displayName").stringValue = row.name;
            so.FindProperty("_portrait").objectReferenceValue = lookalike != null ? lookalike.Portrait : null;
            so.FindProperty("_plazaPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlazaPrefabFolder}/{row.prefab}.prefab");
            so.FindProperty("_plazaTint").colorValue = row.tint;
            so.FindProperty("_trait").intValue = (int)row.trait;
            so.FindProperty("_toyVisitor").boolValue = true;
            so.FindProperty("_visitTier").intValue = row.tier;
            so.FindProperty("_homelessLine").stringValue = row.homeless;
            so.FindProperty("_isBuilder").boolValue = false;
            so.FindProperty("_arrivalEntry").objectReferenceValue = entry;
            so.FindProperty("_moveInLine").stringValue = row.moveIn;
            SetStrings(so.FindProperty("_lines"), row.lines);
            so.ApplyModifiedPropertiesWithoutUndo();
            otters[row.id] = otter;

            AddUnique(configSo.FindProperty("_otters"), otter);
            AddUnique(configSo.FindProperty("_guestbookEntries"), entry);
        }
    }
}
