using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Makes every zone scene (Farm, Fishing, Plaza) use one GameManager prefab,
// so crop lists and balance data can't drift apart between scenes. The prefab
// is built once from Farm.unity's GameManager (with the fishing balance asset
// filled in, which Farm's copy was missing); each scene's plain GameManager is
// then swapped for a prefab instance. The plaza instance turns the temporary
// sale button off. Safe to re-run: existing prefab instances are kept.
// Run via: OtterKingdom > Tools > Setup GameManager Prefab (outside Play mode).
public static class GameManagerPrefabSetup
{
    private const string PrefabPath = "Assets/Prefabs/GameManager.prefab";
    private const string FishingBalancePath = "Assets/Data/Fishing/FishingBalanceData.asset";
    private const string FarmScenePath = "Assets/Scenes/Farm.unity";
    private const string PlazaScenePath = "Assets/Scenes/Plaza.unity";
    private static readonly string[] ZoneScenePaths = { FarmScenePath, "Assets/Scenes/Fishing.unity", PlazaScenePath };

    [MenuItem("OtterKingdom/Tools/Setup GameManager Prefab")]
    public static void Run()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[GameManagerPrefabSetup] Stop Play mode first.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string startScenePath = SceneManager.GetActiveScene().path;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = CreatePrefabFromFarm();
        if (prefab == null) return;

        foreach (var path in ZoneScenePaths) PlaceInScene(path, prefab);

        if (!string.IsNullOrEmpty(startScenePath)) EditorSceneManager.OpenScene(startScenePath);
        Debug.Log($"[GameManagerPrefabSetup] Done. Farm, Fishing and Plaza now use {PrefabPath}.");
    }

    private static GameObject CreatePrefabFromFarm()
    {
        var scene = EditorSceneManager.OpenScene(FarmScenePath);
        var sources = FindGameManagers(scene);
        if (sources.Count == 0)
        {
            Debug.LogError($"[GameManagerPrefabSetup] No GameManager in {FarmScenePath} to build the prefab from.");
            return null;
        }

        // Work on a copy so Farm's own object is swapped like the other scenes'.
        var copy = Object.Instantiate(sources[0].gameObject);
        copy.name = "GameManager";

        var so = new SerializedObject(copy.GetComponent<GameManager>());
        var fishingBalance = so.FindProperty("fishingBalance");
        if (fishingBalance.objectReferenceValue == null)
        {
            fishingBalance.objectReferenceValue = AssetDatabase.LoadAssetAtPath<FishingBalanceData>(FishingBalancePath);
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
        Object.DestroyImmediate(copy);
        return prefab;
    }

    private static void PlaceInScene(string scenePath, GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(scenePath);

        GameObject instance = null;
        foreach (var manager in FindGameManagers(scene))
        {
            var go = manager.gameObject;
            bool isOurInstance = PrefabUtility.GetCorrespondingObjectFromSource(go) == prefab;
            if (isOurInstance && instance == null)
            {
                instance = go;
                continue;
            }
            Object.DestroyImmediate(go);
        }

        if (instance == null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "GameManager";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static List<GameManager> FindGameManagers(Scene scene)
    {
        var managers = new List<GameManager>();
        foreach (var root in scene.GetRootGameObjects())
        {
            managers.AddRange(root.GetComponentsInChildren<GameManager>(true));
        }
        return managers;
    }
}
