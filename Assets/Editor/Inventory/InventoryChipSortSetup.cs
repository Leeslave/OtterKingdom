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
    // 탭 아래쪽이 목록 패널에 덮이는 높이 (시안 약 10~12)
    private const float TabTuck = 12f;
    // 칩과 같은 높이 → 같은 9-slice 배율 → 테두리 두께 통일
    private const float ListItemHeight = ToolbarHeight;
    private const float ListPadding = 10f;
    private const float ListGap = 8f;
    // 칩 스프라이트 아래쪽 입체 턱 높이 (테두리 B40 - T34). 글자를 이만큼 올려야 가운데로 보임
    private const float LipOffset = 6f;

    [MenuItem("Tools/Inventory/Setup Sub-Category Chips and Sort")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[InventoryChipSortSetup] Play 모드를 끄고 실행하세요.");
            return;
        }

        // 에디터에서 실행할 때 열린 씬의 저장 안 한 변경이 날아가지 않도록
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

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
        background.pixelsPerUnitMultiplier = SlicedScaleFor(normal, ToolbarHeight);

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

        ArrangeTabsBehindPanel(frame);

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

    // 시안: 탭이 목록 패널 "뒤"에 있고 아래쪽이 패널에 살짝 덮임 (폴더 탭처럼).
    // 부모는 항상 자식보다 먼저 그려지므로, 배경이 부모(ListPanel)에 있으면 탭이 늘 위로 올라옴
    // → 배경을 ItemListFrame으로 옮기고, 탭을 그보다 앞 순서에 둠
    private static void ArrangeTabsBehindPanel(RectTransform frame)
    {
        var listPanel = (RectTransform)frame.parent;
        var tabs = (RectTransform)listPanel.Find("Tabs");

        var panelImage = listPanel.GetComponent<Image>();
        if (panelImage != null && panelImage.sprite != null)
        {
            var frameImage = frame.GetComponent<Image>();
            if (frameImage == null)
                frameImage = frame.gameObject.AddComponent<Image>();

            frameImage.sprite = panelImage.sprite;
            frameImage.type = panelImage.type;
            frameImage.color = panelImage.color;
            frameImage.fillCenter = panelImage.fillCenter;
            frameImage.pixelsPerUnitMultiplier = panelImage.pixelsPerUnitMultiplier;
            frameImage.raycastTarget = true;

            // 탭 사이 빈틈을 눌렀을 때 뒤의 딤까지 클릭이 새지 않도록 투명한 영역만 남김
            panelImage.sprite = null;
            panelImage.color = Color.clear;
        }

        if (tabs.GetSiblingIndex() > frame.GetSiblingIndex())
            tabs.SetSiblingIndex(frame.GetSiblingIndex());

        // 탭 아래쪽 TabTuck만큼이 패널 윗변에 덮이도록 (탭은 위쪽 앵커, 높이 고정)
        float frameTop = -frame.offsetMax.y;
        tabs.anchoredPosition = new Vector2(tabs.anchoredPosition.x, -(frameTop + TabTuck - tabs.sizeDelta.y));
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

        // 칩과 같은 스프라이트/같은 배율 → 테두리 두께가 옆 칩들과 똑같아 보임
        var chipNormal = LoadCommonSprite("UI_Chip_Normal");
        var chipSelected = LoadCommonSprite("UI_Chip_Selected");
        float chipScale = SlicedScaleFor(chipNormal, ToolbarHeight);
        var muted = new Color32(0xA0, 0x86, 0x72, 0xFF);
        var cocoa = new Color32(0x4B, 0x2E, 0x22, 0xFF);

        var dropdown = go.GetComponent<TMP_Dropdown>();
        var background = go.GetComponent<Image>();
        background.sprite = chipNormal;
        background.type = Image.Type.Sliced;
        background.pixelsPerUnitMultiplier = chipScale;

        // 칩 라벨과 같은 크기/색, 아래 입체 턱(lip)만큼 살짝 위로
        dropdown.captionText.font = font;
        dropdown.captionText.fontSize = 28;
        dropdown.captionText.color = muted;
        dropdown.captionText.alignment = TextAlignmentOptions.Center;
        var captionRect = dropdown.captionText.rectTransform;
        captionRect.offsetMin = new Vector2(20, LipOffset);
        captionRect.offsetMax = new Vector2(-48, 0);

        // 기본 화살표 이미지(흰 사각형) 대신 글자 ▼ (제목 폰트에 없는 기호라 본문 폰트)
        var arrow = (RectTransform)go.transform.Find("Arrow");
        Object.DestroyImmediate(arrow.GetComponent<Image>());
        arrow.anchorMin = new Vector2(1, 0);
        arrow.anchorMax = new Vector2(1, 1);
        arrow.pivot = new Vector2(1, 0.5f);
        arrow.sizeDelta = new Vector2(40, -LipOffset);
        arrow.anchoredPosition = new Vector2(-16, LipOffset / 2);
        var arrowText = arrow.gameObject.AddComponent<TextMeshProUGUI>();
        arrowText.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NanumSquareRoundOTFR SDF.asset") ?? font;
        arrowText.text = "▼";
        arrowText.fontSize = 20;
        arrowText.color = muted;
        arrowText.alignment = TextAlignmentOptions.Center;
        arrowText.raycastTarget = false;

        StyleDropdownList(dropdown, font, chipNormal, chipSelected, chipScale, cocoa);

        var view = go.AddComponent<SortDropdownView>();
        var so = new SerializedObject(view);
        so.FindProperty("_dropdown").objectReferenceValue = dropdown;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // TMP 기본 템플릿(항목 20px, 스크롤바, 체크 아이콘)을 칩 스타일로 교체:
    // 크림 칩 패널 + 선택된 항목은 소분류 칩이 선택됐을 때와 같은 초록 칩
    private static void StyleDropdownList(TMP_Dropdown dropdown, TMP_FontAsset font,
        Sprite panelSprite, Sprite selectedSprite, float chipScale, Color cocoa)
    {
        int optionCount = System.Enum.GetValues(typeof(ItemSortMode)).Length;

        var template = dropdown.template;
        template.anchoredPosition = new Vector2(0, -ListGap);
        template.sizeDelta = new Vector2(0, optionCount * ListItemHeight + ListPadding * 2 + LipOffset);

        var templateImage = template.GetComponent<Image>();
        templateImage.sprite = panelSprite;
        templateImage.type = Image.Type.Sliced;
        templateImage.pixelsPerUnitMultiplier = chipScale;
        templateImage.color = Color.white;

        // 항목이 몇 개 안 되므로 스크롤바 없이 전부 보여줌
        var scrollRect = template.GetComponent<ScrollRect>();
        var scrollbar = template.Find("Scrollbar");
        scrollRect.verticalScrollbar = null;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        if (scrollbar != null)
            Object.DestroyImmediate(scrollbar.gameObject);

        var viewport = (RectTransform)template.Find("Viewport");
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        // TMP는 펼칠 때 목록 높이를 "Content 안 항목 배치"로 다시 계산하므로, 여백은 Viewport가 아니라
        // Content와 항목 사이에 둬야 함 (Viewport에 두면 여백만큼 마지막 항목이 잘림)
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        float padTop = ListPadding;
        float padBottom = ListPadding + LipOffset;

        var content = (RectTransform)viewport.Find("Content");
        content.sizeDelta = new Vector2(0, ListItemHeight + padTop + padBottom);

        var item = (RectTransform)content.Find("Item");
        item.anchorMin = new Vector2(0, 0.5f);
        item.anchorMax = new Vector2(1, 0.5f);
        item.sizeDelta = new Vector2(-ListPadding * 2, ListItemHeight);
        item.anchoredPosition = new Vector2(0, (padBottom - padTop) / 2);

        // 누르는 동안만 살짝 물드는 배경 (평소/선택 상태는 투명)
        var toggle = item.GetComponent<Toggle>();
        var itemBackground = item.Find("Item Background").GetComponent<Image>();
        itemBackground.sprite = null;
        itemBackground.color = Color.white;
        var colors = toggle.colors;
        colors.normalColor = new Color(1, 1, 1, 0);
        colors.highlightedColor = new Color32(0xF6, 0xD5, 0xBE, 0xFF);
        colors.pressedColor = new Color32(0xEE, 0xC3, 0xA5, 0xFF);
        colors.selectedColor = new Color(1, 1, 1, 0);
        toggle.colors = colors;

        // 체크 아이콘 대신 현재 정렬을 채우는 초록 칩 (Toggle이 켜진 항목에만 보임)
        var checkmark = (RectTransform)item.Find("Item Checkmark");
        checkmark.anchorMin = Vector2.zero;
        checkmark.anchorMax = Vector2.one;
        checkmark.offsetMin = Vector2.zero;
        checkmark.offsetMax = Vector2.zero;
        var checkImage = checkmark.GetComponent<Image>();
        checkImage.sprite = selectedSprite;
        checkImage.type = Image.Type.Sliced;
        checkImage.color = Color.white;
        checkImage.pixelsPerUnitMultiplier = chipScale;

        var label = dropdown.itemText;
        label.font = font;
        label.fontSize = 28;
        label.color = cocoa;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(0, LipOffset);
        label.rectTransform.offsetMax = Vector2.zero;
    }

    // 9-slice 테두리(위+아래)가 높이보다 크면 모서리가 찌그러지므로 테두리를 줄이는 배율
    private static float SlicedScaleFor(Sprite sprite, float height)
    {
        float borders = sprite.border.y + sprite.border.w;
        return borders > height ? borders / (height * 0.9f) : 1f;
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
