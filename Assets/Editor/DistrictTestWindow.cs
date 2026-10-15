using UnityEditor;
using UnityEngine;

/// <summary>
/// 구역 · 이사 · 성 승격을 바로 시험하는 치트 창 (플레이 중에만). 메뉴: OtterKingdom/Dev/구역 · 성 테스트.
/// 세이브에 남는 것: 승격 · 성 준비 · 재료(가방 · 골드). 질서도 덮어쓰기는 저장되지 않음 (플레이를 끄면 풀림)
/// </summary>
public class DistrictTestWindow : EditorWindow
{
    private int _order = 80;

    [MenuItem("OtterKingdom/Dev/구역 · 성 테스트")]
    private static void Open() => GetWindow<DistrictTestWindow>("구역 · 성 테스트");

    private void OnInspectorUpdate() => Repaint();

    private void OnDisable() => SettlementManager.DevOrderOverride = -1;

    private void OnGUI()
    {
        var manager = Application.isPlaying ? SettlementManager.Instance : null;
        if (manager == null || !manager.IsLoaded)
        {
            EditorGUILayout.HelpBox("플레이 중에 광장에서 쓰세요.", MessageType.Info);
            return;
        }

        var order = manager.DistrictOrder;
        EditorGUILayout.LabelField("지금", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("행정 해달", manager.IsAdministrator ? "승격함" : "아직");
        EditorGUILayout.LabelField("왕국 질서도", order != null ? $"{order.Score} / 100 (맞는 건물 {order.Matching}/{order.Buildings}, 정한 구역 {order.Designated})" : "-");
        EditorGUILayout.LabelField("성", manager.IsCastleBuilt ? "완성" : manager.IsCastleReady ? "세울 수 있음 (게시판)" : "아직");
        EditorGUILayout.LabelField("이사 지시서", manager.RelocationOrder != null ? $"#{manager.RelocationOrder.Serial} ({manager.RelocationDoneCount()}/{manager.RelocationOrder.InstanceIds.Count})" : "없음");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("구역", EditorStyles.boldLabel);
        if (!manager.IsAdministrator && GUILayout.Button("또박이 행정 해달로 승격"))
            manager.Settlement.UnlockDevelopment(DistrictGuideRules.Promotion);

        using (new EditorGUILayout.HorizontalScope())
        {
            _order = EditorGUILayout.IntSlider("질서도 덮어쓰기", _order, 0, 100);
            if (GUILayout.Button(SettlementManager.DevOrderOverride >= 0 ? "적용 중 · 끄기" : "적용", GUILayout.Width(110)))
                SettlementManager.DevOrderOverride = SettlementManager.DevOrderOverride >= 0 ? -1 : _order;
        }
        if (SettlementManager.DevOrderOverride >= 0 && SettlementManager.DevOrderOverride != _order)
            SettlementManager.DevOrderOverride = _order;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("성 승격", EditorStyles.boldLabel);
        if (GUILayout.Button("성 세우기 바로 열기 (질서도 80 없이)"))
        {
            if (!manager.IsAdministrator)
                manager.Settlement.UnlockDevelopment(DistrictGuideRules.Promotion);
            SettlementManager.DevOrderOverride = Mathf.Max(SettlementManager.DevOrderOverride, manager.Config.CastleOrder);
            _order = SettlementManager.DevOrderOverride;
        }
        if (GUILayout.Button("성 재료 넉넉히 받기 (골드 · 목재 · 돌)"))
            GiveCastleMaterials(manager);
        DevTimers.Fast = EditorGUILayout.Toggle($"빠른 타이머 ({DevTimers.FastSeconds}초, 새로 시작하는 공사부터)", DevTimers.Fast);
        if (GUILayout.Button("성 되돌리기 (다시 시험)"))
        {
            manager.Settlement.DevForget(CastleRules.Built, CastleRules.RequestId);
            manager.Settlement.DevForget(CastleRules.Ready, null);
            SettlementManager.DevOrderOverride = -1;
        }
        EditorGUILayout.HelpBox("성 세우기: 질서도가 기준에 닿으면 알림 → 게시판 큰 부탁 '왕국의 성을 세워요' → 짓기 → 완성 연출.\n" +
            "성 그림(Assets/Art/Settlement/Prop_Castle.png)이 없으면 마을 길드 그림이 그대로 보여요.", MessageType.None);
    }

    private static void GiveCastleMaterials(SettlementManager manager)
    {
        var request = manager.Config.FindRequest(CastleRules.RequestId);
        var construction = request != null ? request.Construction : null;
        if (construction == null)
        {
            Debug.LogWarning("[구역 · 성 테스트] 성 부탁이 없습니다. Tools/Settlement/Apply Castle을 먼저 실행하세요.");
            return;
        }
        if (CurrencyManager.Instance != null && manager.Config.GoldCurrency != null)
            CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(manager.Config.GoldCurrency, construction.RequiredGold, TransactionSource.TestGet));
        var inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        if (inventory == null)
            return;
        foreach (var item in construction.RequiredItems)
        {
            if (item != null && item.Item != null)
                inventory.Add(item.Item, item.Amount, ItemChangeReason.Test);
        }
    }
}
