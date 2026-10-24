using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class SettlementSetup
{
    [Serializable] private class FlowRequest
    {
        public string asset, requires, arrival, done;
        public int order, level;
    }
    [Serializable] private class FlowBuilding
    {
        public string asset;
        public int level, gold, wood, stone, seconds, perHour;
    }
    [Serializable] private class FlowConfig
    {
        public FlowRequest[] requests;
        public FlowBuilding[] buildings;
    }

    [MenuItem("Tools/Settlement/Apply Progression Flow")]
    public static void ApplyProgressionFlow()
    {
        ApplyFlowBuildings();
        ApplyFlowRequests();
        AssetDatabase.SaveAssets();
    }

    private static FlowConfig ReadFlow() => JsonUtility.FromJson<FlowConfig>(
        File.ReadAllText(Path.Combine(Application.dataPath, "../Tools/Progression/flow.json")));

    private static void ApplyFlowBuildings()
    {
        foreach (var row in ReadFlow().buildings)
        {
            var building = LoadBuilding(row.asset);
            if (building == null) continue;
            var so = new SerializedObject(building);
            so.FindProperty("_requiredLevel").intValue = row.level;
            so.FindProperty("_gold").intValue = row.gold;
            so.FindProperty("_buildSeconds").floatValue = row.seconds;
            var items = so.FindProperty("_items");
            if (items.arraySize >= 2)
            {
                items.GetArrayElementAtIndex(0).FindPropertyRelative("_amount").intValue = row.wood;
                items.GetArrayElementAtIndex(1).FindPropertyRelative("_amount").intValue = row.stone;
            }
            if (row.perHour > 0)
            {
                so.FindProperty("_productionPerHour").intValue = row.perHour;
                so.FindProperty("_description").stringValue = "한가한 특성 주민이 한 마리당 1분에 1개씩 모아요. 정찰·개간에 나간 주민은 생산을 쉬어요. 최대 8시간 보관해요.";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ApplyFlowRequests(Dictionary<string, SettlementOtterDefinition> otters = null)
    {
        var config = AssetDatabase.LoadAssetAtPath<SettlementConfig>(ConfigPath);
        foreach (var row in ReadFlow().requests)
        {
            var request = AssetDatabase.LoadAssetAtPath<BoardRequestDefinition>($"{DataFolder}/Requests/{row.asset}.asset");
            if (request == null) continue;
            var so = new SerializedObject(request);
            so.FindProperty("_requiredDevelopment").stringValue = row.requires;
            so.FindProperty("_order").intValue = row.order;
            so.FindProperty("_kingdomLevel").intValue = row.level;
            so.FindProperty("_completionMessage").stringValue = row.done;
            SettlementOtterDefinition arrival = null;
            if (!string.IsNullOrEmpty(row.arrival))
            {
                if (otters != null) otters.TryGetValue(row.arrival, out arrival);
                else arrival = config.FindOtter(row.arrival);
                if (arrival == null) throw new InvalidOperationException($"진행표의 방문 해달을 찾을 수 없습니다: {row.arrival}");
            }
            var arrivals = so.FindProperty("_arrivals");
            arrivals.arraySize = arrival != null ? 1 : 0;
            if (arrival != null)
            {
                arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("_otter").objectReferenceValue = arrival;
                arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("_state").enumValueIndex = (int)ResidentState.Visitor;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
