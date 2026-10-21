using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 낚시 개편을 본 게임에 넣기 (Docs/낚시_개편_기획.md 7장):
/// 1) 물고기 31종 → 가방 아이템 · 도감 항목 (원래 있던 10종은 그대로 쓰고 획득 장소 · 순서만 새로, 새 21종은 만들고 실루엣도)
/// 2) GameManager 프리팹에 바다 낚시 설정 연결 (줄 · 찌 · 릴 강화, 방치 · 꺼 둔 동안 낚시가 이 표를 씀)
/// 3) 낚시터 씬(Fishing.unity)에 바다 화면을 얹음: 선착장과 멀리 떨어진 곳(SeaRoot)에 바다 · 낚시꾼 해달,
///    FishingReworkGame(본 게임 모드)이 [바다 낚시] 버튼으로 열고 [나가기]로 닫음. 선착장 · 꾸미기 · 개척은 그대로
/// 메뉴: Tools/Fishing Rework/Apply To Main Game · 배치: -executeMethod FishingReworkSetup.ApplyToMainGame
/// </summary>
public static partial class FishingReworkSetup
{
    private const string FishingScenePath = "Assets/Scenes/Fishing.unity";
    private const string GameManagerPrefabPath = "Assets/Prefabs/GameManager.prefab";
    private const string ItemDatabasePath = "Assets/Scriptable Obejects/Inventory/ItemDatabase.asset";
    private const string CollectionDatabasePath = "Assets/Scriptable Obejects/Collection/CollectionDatabase.asset";
    private const string FishTabPath = "Assets/Scriptable Obejects/Collection/Tabs/Tab_Fish.asset";
    private const string EntryFolder = "Assets/Scriptable Obejects/Collection/Entries";
    private const string SeaName = "SeaFishing";
    // 바다는 선착장 그림(가운데 원점, 위아래 ±8)과 겹치지 않게 아래로 멀리
    private static readonly Vector3 SeaOrigin = new Vector3(0f, -400f, 0f);

    [MenuItem("Tools/Fishing Rework/Apply To Main Game")]
    public static void ApplyToMainGame()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        ImportArt();
        var (config, art) = BuildData();
        var entries = ApplyCollection(config);
        AddToDatabases(config, entries);
        SilhouetteGenerator.FillMissing(entries);
        AssignConfigToGameManager(config);
        AssetDatabase.SaveAssets();
        BuildSeaInFishingScene(config, art);
        Debug.Log("[FishingReworkSetup] 낚시 개편을 본 게임에 넣음: 물고기 아이템 · 도감 · GameManager 설정 · 낚시터 씬 바다");
    }

    private static string Where(FishDefinition fish) => fish.NearBottom
        ? $"바다 낚시 · 거리 {fish.MinDistance:0}~{fish.MaxDistance:0}m 바닥"
        : $"바다 낚시 · 거리 {fish.MinDistance:0}~{fish.MaxDistance:0}m · 깊이 {fish.MinDepth:0}~{fish.MaxDepth:0}m";

    private static string PascalId(string id)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var part in id.Replace("fish_", "").Split('_'))
        {
            if (part.Length > 0)
                sb.Append(char.ToUpperInvariant(part[0])).Append(part.Substring(1));
        }
        return sb.ToString();
    }

    // 도감 어류 탭: 바다 구간 순서대로. 원래 항목은 획득 장소 · 순서만, 새 물고기는 항목을 만듦
    private static List<CollectionEntry> ApplyCollection(FishingReworkConfig config)
    {
        var database = AssetDatabase.LoadAssetAtPath<CollectionDatabase>(CollectionDatabasePath);
        var tab = AssetDatabase.LoadAssetAtPath<CollectionTab>(FishTabPath);
        var entries = new List<CollectionEntry>();
        if (database == null || tab == null)
        {
            Debug.LogError("[FishingReworkSetup] 도감 데이터 · 어류 탭이 없습니다.");
            return entries;
        }
        for (int i = 0; i < config.Fish.Count; i++)
        {
            var fish = config.Fish[i];
            if (fish == null || fish.Item == null)
                continue;
            var entry = database.FindByItem(fish.Item);
            bool created = false;
            if (entry == null)
            {
                entry = Asset<CollectionEntry>($"{EntryFolder}/Entry_Fish{PascalId(fish.FishId)}.asset");
                created = true;
            }
            var so = new SerializedObject(entry);
            if (created)
            {
                so.FindProperty("_entryId").stringValue = fish.Item.ItemId;
                so.FindProperty("_tab").objectReferenceValue = tab;
                so.FindProperty("_displayName").stringValue = "";
                so.FindProperty("_tagline").stringValue = fish.Description;
                so.FindProperty("_description").stringValue =
                    $"{FishingReworkArt.RarityName(fish.Rarity)} · 무게 {fish.MinWeight:0.##}~{fish.MaxWeight:0.#}kg";
                so.FindProperty("_linkedItem").objectReferenceValue = fish.Item;
            }
            so.FindProperty("_sortOrder").intValue = 10 + i;
            so.FindProperty("_extraValue").stringValue = Where(fish);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entry);
            entries.Add(entry);
        }
        return entries;
    }

    private static void AddUnique(SerializedProperty list, Object item)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == item)
                return;
        }
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
    }

    private static void AddToDatabases(FishingReworkConfig config, List<CollectionEntry> entries)
    {
        var items = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ItemDatabasePath);
        if (items != null)
        {
            var so = new SerializedObject(items);
            foreach (var fish in config.Fish)
            {
                if (fish != null && fish.Item != null)
                    AddUnique(so.FindProperty("_items"), fish.Item);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(items);
        }
        var collection = AssetDatabase.LoadAssetAtPath<CollectionDatabase>(CollectionDatabasePath);
        if (collection != null)
        {
            var so = new SerializedObject(collection);
            foreach (var entry in entries)
                AddUnique(so.FindProperty("_entries"), entry);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(collection);
        }
    }

    private static void AssignConfigToGameManager(FishingReworkConfig config)
    {
        var root = PrefabUtility.LoadPrefabContents(GameManagerPrefabPath);
        try
        {
            var manager = root.GetComponentInChildren<GameManager>(true);
            var so = new SerializedObject(manager);
            so.FindProperty("fishingConfig").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GameManagerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 낚시터 씬에 바다를 다시 만듦 (여러 번 실행해도 같음). 선착장 · 꾸미기 · 개척 · 낚시 해달은 그대로
    private static void BuildSeaInFishingScene(FishingReworkConfig config, FishingReworkArt art)
    {
        var scene = EditorSceneManager.OpenScene(FishingScenePath, OpenSceneMode.Single);
        config = AssetDatabase.LoadAssetAtPath<FishingReworkConfig>($"{DataFolder}/FishingReworkConfig.asset");
        art = AssetDatabase.LoadAssetAtPath<FishingReworkArt>($"{DataFolder}/FishingReworkArt.asset");
        var cameraGo = GameObject.FindWithTag("MainCamera");
        var camera = cameraGo != null ? cameraGo.GetComponent<Camera>() : null;
        if (camera == null)
        {
            Debug.LogError("[FishingReworkSetup] 낚시터 씬에 Main Camera가 없습니다.");
            return;
        }

        var old = GameObject.Find(SeaName);
        if (old != null)
            Object.DestroyImmediate(old);
        var container = new GameObject(SeaName).transform;
        var seaRoot = new GameObject("SeaRoot").transform;
        seaRoot.SetParent(container, false);
        seaRoot.position = SeaOrigin;
        var rig = CreateFisher(seaRoot);
        var fx = new GameObject("Fx").AddComponent<MineFx>();
        fx.transform.SetParent(container, false);
        var sfx = new GameObject("Sfx").AddComponent<MineSfx>();
        sfx.transform.SetParent(container, false);
        var game = CreateGame(config, art, camera, rig, fx, sfx, true, seaRoot);
        game.transform.SetParent(container, false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
