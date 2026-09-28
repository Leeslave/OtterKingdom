using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 슬롯 희귀도 점 / 잠긴 칸 스프라이트 / 희귀도 색 / 모종 아이템을 설정한다. 여러 번 실행해도 결과가 같음.
/// 배치 모드: Unity.exe -batchmode -projectPath . -executeMethod InventorySlotSetup.Run -quit
/// </summary>
public static class InventorySlotSetup
{
    private const string SlotPrefabPath = "Assets/Prefab/Inventory/UI/ItemSlot.prefab";
    private const string ScenePath = "Assets/Scenes/InventoryTestScene.unity";
    private const string DataFolder = "Assets/Scriptable Obejects/Inventory";
    private const string IconFolder = "Assets/Art/Icons/Items";
    private const string CommonSpriteFolder = "Assets/Art/UI/Common";

    // 시안: 190 칸의 오른쪽 위에서 18 들어간 24 원 → 테두리 포함 28
    private const float DotSize = 28f;
    private const float DotInset = 18f;

    // (에셋 이름, ID, 표시 이름, 설명, 아이콘, 희귀도, 판매가) — 판매가는 임시값 (상점 구매가의 절반)
    private static readonly (string asset, string id, string name, string desc, string icon, string rarity, int sellPrice)[] Seedlings =
    {
        ("감자 모종", "seed_potato", "감자 모종", "밭에 심으면 감자가 자라요.", "ICON_Seedling_Potato", "Common", 50),
        ("고구마 모종", "seed_sweet_potato", "고구마 모종", "밭에 심으면 고구마가 자라요.", "ICON_Seedling_SweetPotato", "Common", 75),
        ("딸기 모종", "seed_strawberry", "딸기 모종", "밭에 심으면 딸기가 열려요.", "ICON_Seedling_Strawberry", "Rare", 150),
    };

    [MenuItem("Tools/Inventory/Setup Slot Rarity and Seedlings")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[InventorySlotSetup] Play 모드를 끄고 실행하세요.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SetRarityColors();
        SetupSlotPrefab();
        CreateSeedlings();
        AssetDatabase.SaveAssets();
        AddSeedlingsToTestGiver();
        Debug.Log("[InventorySlotSetup] 완료");
    }

    // 시안 RARITY 색 (흔함 / 희귀 / 특별)
    private static void SetRarityColors()
    {
        SetRarityColor("Common", new Color32(0xCD, 0xBB, 0xAA, 0xFF));
        SetRarityColor("Rare", new Color32(0x9C, 0xC8, 0xEC, 0xFF));
        SetRarityColor("Epic", new Color32(0xC9, 0xB6, 0xF0, 0xFF));
    }

    private static void SetRarityColor(string assetName, Color color)
    {
        var rarity = LoadRarity(assetName);
        var so = new SerializedObject(rarity);
        so.FindProperty("_color").colorValue = color;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rarity);
    }

    private static ItemRarity LoadRarity(string assetName)
    {
        var rarity = AssetDatabase.LoadAssetAtPath<ItemRarity>($"{DataFolder}/Rarity/{assetName}.asset");
        if (rarity == null)
            throw new FileNotFoundException($"희귀도 에셋이 없습니다: {assetName}");
        return rarity;
    }

    private static void SetupSlotPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
        try
        {
            var dotTransform = root.transform.Find("RarityDot");
            var dot = dotTransform != null ? dotTransform.GetComponent<Image>() : CreateDot(root.transform);

            dot.sprite = LoadSprite($"{CommonSpriteFolder}/UI_Knob.png");
            dot.preserveAspect = true;
            dot.raycastTarget = false;
            var rect = dot.rectTransform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(DotSize, DotSize);
            rect.anchoredPosition = new Vector2(-DotInset, -DotInset);
            rect.SetAsLastSibling();

            var so = new SerializedObject(root.GetComponent<ItemSlotView>());
            so.FindProperty("_rarityDot").objectReferenceValue = dot;
            so.FindProperty("_lockedSprite").objectReferenceValue =
                LoadSprite($"{CommonSpriteFolder}/UI_Inventory_Slot_Locked.png");
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Image CreateDot(Transform parent)
    {
        var go = new GameObject("RarityDot", typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static void CreateSeedlings()
    {
        var category = AssetDatabase.LoadAssetAtPath<ItemCategory>($"{DataFolder}/Category/Seedlings.asset");
        if (category == null)
            throw new FileNotFoundException("모종 카테고리(Seedlings.asset)가 없습니다.");

        foreach (var s in Seedlings)
        {
            string path = $"{DataFolder}/Items/{s.asset}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            var so = new SerializedObject(item);
            so.FindProperty("_itemId").stringValue = s.id;
            so.FindProperty("_displayName").stringValue = s.name;
            so.FindProperty("_description").stringValue = s.desc;
            so.FindProperty("_icon").objectReferenceValue = LoadSprite($"{IconFolder}/{s.icon}.png");
            so.FindProperty("_category").objectReferenceValue = category;
            so.FindProperty("_rarity").objectReferenceValue = LoadRarity(s.rarity);
            so.FindProperty("_sellPrice").intValue = s.sellPrice;
            so.FindProperty("_maxStack").intValue = 999;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }
    }

    // 테스트 씬에서 바로 지급해 볼 수 있도록
    private static void AddSeedlingsToTestGiver()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var giver = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<TestInventoryGiver>(true))
            .First();

        var so = new SerializedObject(giver);
        var list = so.FindProperty("_items");
        foreach (var s in Seedlings)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{DataFolder}/Items/{s.asset}.asset");
            bool exists = Enumerable.Range(0, list.arraySize)
                .Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == item);
            if (exists)
                continue;

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Single/Multiple 임포트 모두 대응 (Multiple이면 첫 번째 조각)
    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null)
            throw new FileNotFoundException($"스프라이트를 찾을 수 없습니다: {path}");
        return sprite;
    }
}
