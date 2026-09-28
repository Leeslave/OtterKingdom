using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가방 화면에 소분류 칩 줄 + 정렬 드롭다운을 배치하고 프레젠터에 연결한다. 여러 번 실행해도 결과가 같음.
/// 배치 모드: Unity.exe -batchmode -projectPath . -executeMethod InventoryChipSortSetup.Run -quit
/// </summary>
public static class InventoryChipSortSetup
{
    private const string ScenePath = "Assets/Scenes/InventoryTestScene.unity";
    private const string TabPrefabPath = "Assets/Prefab/Inventory/UI/CategoryButton.prefab";
    private const string ChipPrefabPath = "Assets/Prefab/Inventory/UI/SubCategoryChip.prefab";
    private const string CategoryFolder = "Assets/Scriptable Obejects/Inventory/Category";
    private const string CommonSpriteFolder = "Assets/Art/UI/Common";
    private const string BordersJsonPath = "Tools/UIGen/kit_borders.json";

    // 시안 수치 (1080×1920): 목록 프레임 안쪽 기준
    private const float SideInset = 40f;
    private const float ToolbarTop = 36f;
    private const float ToolbarHeight = 64f;
    private const float ToolbarGap = 20f;
    private const float ChipWidth = 140f;
    private const float ChipSpacing = 16f;
    private const float SortWidth = 200f;

    [MenuItem("Tools/Inventory/Setup Sub-Category Chips and Sort")]
    public static void Run()
    {
        ApplyCommonSpriteBorders();
        CreateChipPrefab();
        AssetDatabase.SaveAssets();
        SetupScene();
        Debug.Log("[InventoryChipSortSetup] 완료");
    }

    #region 스프라이트

    // ui_gen.py가 만든 kit_borders.json(L, T, R, B)으로 공용 스프라이트를 Single + 9-slice로 임포트
    private static void ApplyCommonSpriteBorders()
    {
        var borders = new Dictionary<string, Vector4>();
        if (File.Exists(BordersJsonPath))
        {
            var json = File.ReadAllText(BordersJsonPath);
            foreach (Match m in Regex.Matches(json, @"""(\w+)"":\s*\[\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+)\s*\]"))
            {
                float l = float.Parse(m.Groups[2].Value), t = float.Parse(m.Groups[3].Value);
                float r = float.Parse(m.Groups[4].Value), b = float.Parse(m.Groups[5].Value);
                borders[m.Groups[1].Value] = new Vector4(l, b, r, t); // Unity 순서: L, B, R, T
            }
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CommonSpriteFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            var border = borders.TryGetValue(Path.GetFileNameWithoutExtension(path), out var b4) ? b4 : Vector4.zero;
            if (importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && importer.spriteBorder == border)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }
    }

    private static Sprite LoadCommonSprite(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CommonSpriteFolder}/{name}.png");
        if (sprite == null)
            throw new FileNotFoundException($"스프라이트를 찾을 수 없습니다: {name}");
        return sprite;
    }

    #endregion

    #region 칩 프리팹

    // 탭 프리팹을 복제해서 스프라이트와 크기만 바꿈 (스크립트는 같은 CategoryTabView)
    private static void CreateChipPrefab()
    {
        var tabPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabPrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(tabPrefab);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = Path.GetFileNameWithoutExtension(ChipPrefabPath);

        ((RectTransform)instance.transform).sizeDelta = new Vector2(ChipWidth, ToolbarHeight);

        var normal = LoadCommonSprite("UI_Chip_Normal");
        var selected = LoadCommonSprite("UI_Chip_Selected");

        var view = instance.GetComponent<CategoryTabView>();
        var so = new SerializedObject(view);
        so.FindProperty("_normalSprite").objectReferenceValue = normal;
        so.FindProperty("_selectedSprite").objectReferenceValue = selected;
        so.ApplyModifiedPropertiesWithoutUndo();

        var background = (Image)so.FindProperty("_background").objectReferenceValue;
        background.sprite = normal;
        background.type = Image.Type.Sliced;

        var label = (TextMeshProUGUI)so.FindProperty("_label").objectReferenceValue;
        label.fontSize = 28;

        PrefabUtility.SaveAsPrefabAsset(instance, ChipPrefabPath);
        Object.DestroyImmediate(instance);
    }

    #endregion

    #region 씬

    private static void SetupScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 씬을 열면 쓰지 않는 에셋이 메모리에서 내려가므로, 에셋 참조는 씬을 연 뒤에 불러와야 함
        var chipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChipPrefabPath).GetComponent<CategoryTabView>();
        if (chipPrefab == null)
            throw new FileNotFoundException($"칩 프리팹을 불러오지 못했습니다: {ChipPrefabPath}");

        var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToList();

        var presenter = all.Select(t => t.GetComponent<InventoryPresenter>()).First(p => p != null);
        var frame = (RectTransform)all.First(t => t.name == "ItemListFrame");
        var scroll = (RectTransform)frame.Find("Scroll View");
        var font = AssetDatabase.LoadAssetAtPath<GameObject>(TabPrefabPath).GetComponentInChildren<TextMeshProUGUI>(true).font;

        // 다시 실행할 때는 이전 결과를 지우고 새로 만듦
        var old = frame.Find("Toolbar");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var toolbar = CreateRect("Toolbar", frame);
        toolbar.anchorMin = new Vector2(0, 1);
        toolbar.anchorMax = new Vector2(1, 1);
        toolbar.pivot = new Vector2(0.5f, 1);
        toolbar.offsetMin = new Vector2(SideInset, -(ToolbarTop + ToolbarHeight));
        toolbar.offsetMax = new Vector2(-SideInset, -ToolbarTop);
        toolbar.SetSiblingIndex(scroll.GetSiblingIndex());

        var chips = CreateRect("Chips", toolbar);
        chips.anchorMin = Vector2.zero;
        chips.anchorMax = Vector2.one;
        chips.offsetMin = Vector2.zero;
        chips.offsetMax = new Vector2(-(SortWidth + ChipSpacing), 0);
        var layout = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = ChipSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var sortView = CreateSortDropdown(toolbar, font);

        // 목록은 툴바 아래부터
        scroll.offsetMax = new Vector2(scroll.offsetMax.x, -(ToolbarTop + ToolbarHeight + ToolbarGap));

        var categories = AssetDatabase.FindAssets("t:ItemCategory", new[] { CategoryFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemCategory>(AssetDatabase.GUIDToAssetPath(g)))
            .OrderBy(c => c.Parent != null)
            .ThenBy(c => c.SortOrder)
            .ToList();

        var so = new SerializedObject(presenter);
        so.FindProperty("_chipPrefab").objectReferenceValue = chipPrefab;
        so.FindProperty("_chipParent").objectReferenceValue = chips;
        so.FindProperty("_sortDropdown").objectReferenceValue = sortView;
        var list = so.FindProperty("_categories");
        list.arraySize = categories.Count;
        for (int i = 0; i < categories.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = categories[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static SortDropdownView CreateSortDropdown(RectTransform parent, TMP_FontAsset font)
    {
        var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        go.name = "SortDropdown";
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(SortWidth, 0);

        var paper = LoadCommonSprite("UI_Button_Paper");
        var muted = new Color32(0xA0, 0x86, 0x72, 0xFF);
        var cocoa = new Color32(0x4B, 0x2E, 0x22, 0xFF);

        var dropdown = go.GetComponent<TMP_Dropdown>();
        var background = go.GetComponent<Image>();
        background.sprite = paper;
        background.type = Image.Type.Sliced;

        dropdown.captionText.font = font;
        dropdown.captionText.fontSize = 26;
        dropdown.captionText.color = muted;
        dropdown.captionText.alignment = TextAlignmentOptions.Center;
        var captionRect = dropdown.captionText.rectTransform;
        captionRect.offsetMin = new Vector2(16, 0);
        captionRect.offsetMax = new Vector2(-44, 0);

        dropdown.itemText.font = font;
        dropdown.itemText.fontSize = 26;
        dropdown.itemText.color = cocoa;

        // 기본 화살표 이미지(흰 사각형) 대신 글자 ▼
        var arrow = (RectTransform)go.transform.Find("Arrow");
        Object.DestroyImmediate(arrow.GetComponent<Image>());
        var arrowText = arrow.gameObject.AddComponent<TextMeshProUGUI>();
        arrowText.font = font;
        arrowText.text = "▼";
        arrowText.fontSize = 22;
        arrowText.color = muted;
        arrowText.alignment = TextAlignmentOptions.Center;
        arrowText.raycastTarget = false;

        // 펼친 목록 배경도 종이 스프라이트
        var templateImage = dropdown.template.GetComponent<Image>();
        templateImage.sprite = paper;
        templateImage.type = Image.Type.Sliced;

        var view = go.AddComponent<SortDropdownView>();
        var so = new SerializedObject(view);
        so.FindProperty("_dropdown").objectReferenceValue = dropdown;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return rect;
    }

    #endregion
}
