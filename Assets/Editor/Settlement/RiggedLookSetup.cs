using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 광장에 리그 해달만 나오게 한다 (리그가 없는 캐스트 그림 해달은 잠시 뺌).
/// - 정착 해달 20마리: 이름 · 역할 · 해금 조건은 그대로, 광장 모습만 리그 프리팹(PlazaOtter_&lt;이름&gt;Rigged)으로
/// - 특수 행동이 없는 일반 리그 7마리: 처음부터 광장을 거니는 해달 (PlazaSettings.otterPrefabs)
/// - 건설 해달 뚝딱이는 광장 씬의 BuilderOtterController._rigPrefab(야구 해달)로 바꿈 (씬에 직접 연결)
/// 다른 설정 메뉴(SettlementSetup, OtterCastSetup)가 해달을 다시 채워도 ApplyTo로 리그 모습이 남는다.
/// 웨이터 · 요리사 · 잠옷 · 사진가 · 청소부 · 음악가는 해금 조건이 정해질 때까지 광장에 나오지 않음.
/// </summary>
public static class RiggedLookSetup
{
    private const string PrefabDir = "Assets/Prefabs/Plaza";
    private const string PlazaSettingsPath = "Assets/Data/Plaza/PlazaSettings.asset";

    /// <summary>정착 해달 → 리그 (PlazaOtter_&lt;rig&gt;Rigged). 농부는 V14 농부 리그(PlazaOtter_Rigged)</summary>
    private static readonly (string otterId, string rig)[] Assignments =
    {
        ("otter_first", "Walker"),          // 몽실: 산책하는
        ("otter_painter", "Fashion"),       // 물감이: 꾸미는
        ("otter_sleepy", "Sleepy"),         // 꾸벅이: 잠꾸러기
        ("otter_receptionist", "Reception"),// 또박이: 안내원
        ("otter_p3_neighbor", "Fluffy"),    // 포근이: 복슬복슬
        ("otter_farmer", ""),               // 새싹이: 농부 (V14)
        ("otter_miner", "Miner"),           // 깡깡이: 광부
        ("otter_fisher", "Fisher"),         // 첨벙이: 뱃사람
        ("otter_toy_bodeul", "Overalls"),   // 보들이 (농사): 노랑 멜빵
        ("otter_toy_degul", "Playful"),     // 데굴이 (채굴): 장난꾸러기
        ("otter_toy_duribeon", "Hoodie"),   // 두리번 (탐험): 후드티
        ("otter_toy_jaejal", "Lively"),     // 재잘이 (장사): 활발한
        ("otter_toy_jjalrang", "Merchant"), // 짤랑이 (장사): 행상인
        ("otter_toy_kkeujeok", "Scholar"),  // 끄적이 (기록): 안경 학자
        ("otter_toy_kkomkkom", "Neat"),     // 꼼꼼이 (손재주): 단정한
        ("otter_toy_kongkong", "Artisan"),  // 콩콩이 (건설): 작업반장
        ("otter_toy_kungkung", "Varsity"),  // 쿵쿵이 (나무캐기): 야구 점퍼
        ("otter_toy_pongdang", "Diver"),    // 퐁당이 (낚시): 잠수부
        ("otter_toy_sallang", "Flower"),    // 살랑이 (탐험): 꽃 카디건
        ("otter_toy_yeongcha", "Knit"),     // 영차 (운반): 케이블 니트
    };

    /// <summary>처음부터 광장을 거니는 일반 리그 (특수 행동 없음)</summary>
    private static readonly string[] Wanderers = { "BearPajama", "Beanie", "Stripe", "Plaid", "Shy", "Laidback", "Quiet" };

    private static GameObject Prefab(string rig) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/PlazaOtter_{rig}Rigged.prefab");

    /// <summary>리그 모습이 정해진 해달이면 광장 모습을 리그로 덮어씀 (광장 프리팹이 없던 해달은 그대로 없게)</summary>
    internal static void ApplyTo(SerializedObject otterSo, string otterId)
    {
        var row = Assignments.FirstOrDefault(a => a.otterId == otterId);
        var plaza = otterSo.FindProperty("_plazaPrefab");
        if (row.otterId == null || plaza.objectReferenceValue == null)
            return;
        var prefab = Prefab(row.rig);
        if (prefab == null)
        {
            Debug.LogWarning($"[RiggedLookSetup] 리그 프리팹이 없습니다: PlazaOtter_{row.rig}Rigged ({otterId})");
            return;
        }
        plaza.objectReferenceValue = prefab;
        otterSo.FindProperty("_plazaTint").colorValue = Color.white;
    }

    [MenuItem("Tools/Settlement/Apply Rigged Looks")]
    public static void Apply()
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(SettlementSetup.ConfigPath);
        int linked = 0;
        if (config != null)
        {
            foreach (var otter in config.Otters)
            {
                if (otter == null)
                    continue;
                var so = new SerializedObject(otter);
                ApplyTo(so, otter.OtterId);
                if (so.ApplyModifiedPropertiesWithoutUndo())
                    linked++;
            }
        }

        var settings = AssetDatabase.LoadAssetAtPath<PlazaSettings>(PlazaSettingsPath);
        if (settings != null)
        {
            settings.otterPrefabs = Wanderers.Select(Prefab).Where(p => p != null).ToArray();
            // 실제 광장의 주민은 정착·장난감 방문 기록으로만 등장한다.
            settings.otterCount = 0;
            EditorUtility.SetDirty(settings);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[RiggedLookSetup] 해달 {linked}마리를 리그 모습으로, 광장을 거니는 리그 {settings?.otterCount ?? 0}마리");
    }
}
