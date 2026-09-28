using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 테스트 씬에 가방 열기 버튼 + "N" 뱃지를 배치한다 (메인 HUD가 생기기 전 임시). 여러 번 실행해도 결과가 같음.
/// 배치 모드: Unity.exe -batchmode -projectPath . -executeMethod InventoryHudSetup.Run -quit
/// </summary>
public static class InventoryHudSetup
{
    private const string ScenePath = "Assets/Scenes/InventoryTestScene.unity";
    private const string TabPrefabPath = "Assets/Prefab/Inventory/UI/CategoryButton.prefab";
    private const string CommonSpriteFolder = "Assets/Art/UI/Common";
    private const string HudName = "TestHud";

    // 1080×1920 기준: 오른쪽 아래, 시안 하단 바의 가방 위치 근처
    private const float ButtonSize = 150f;
    private const float Margin = 56f;
    private const float BadgeSize = 52f;

    [MenuItem("Tools/Inventory/Setup Test Bag Button")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[InventoryHudSetup] Play 모드를 끄고 실행하세요.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToList();

        var presenter = all.Select(t => t.GetComponent<InventoryPresenter>()).First(p => p != null);
        var screen = all.First(t => t.name == "InventoryScreen");
        var canvas = screen.parent;
        var font = AssetDatabase.LoadAssetAtPath<GameObject>(TabPrefabPath).GetComponentInChildren<TextMeshProUGUI>(true).font;

        var old = canvas.Find(HudName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        // 화면 전체 + 안전 영역. 가방 화면보다 먼저 그려지도록 앞 순서에 둠
        var hud = CreateRect(HudName, canvas);
        hud.anchorMin = Vector2.zero;
        hud.anchorMax = Vector2.one;
        hud.offsetMin = Vector2.zero;
        hud.offsetMax = Vector2.zero;
        hud.gameObject.AddComponent<SafeAreaFltter>();
        hud.SetSiblingIndex(screen.GetSiblingIndex());

        var button = CreateRect("BagButton", hud);
        button.anchorMin = new Vector2(1, 0);
        button.anchorMax = new Vector2(1, 0);
        button.pivot = new Vector2(1, 0);
        button.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        button.anchoredPosition = new Vector2(-Margin, Margin);
        var buttonImage = button.gameObject.AddComponent<Image>();
        buttonImage.sprite = LoadSprite("UI_RoundButton_Peach");
        var buttonComponent = button.gameObject.AddComponent<Button>();
        buttonComponent.targetGraphic = buttonImage;
        UnityEventTools.AddVoidPersistentListener(buttonComponent.onClick, presenter.Open);

        var label = CreateText("Label", button, font, "가방", 34, new Color32(0x4B, 0x2E, 0x22, 0xFF));
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(0, 8); // 버튼 아래쪽 입체 턱만큼 위로
        label.rectTransform.offsetMax = Vector2.zero;

        // 뱃지: 버튼 오른쪽 위 모서리에 걸침
        var badge = CreateRect("NewBadge", button);
        badge.anchorMin = Vector2.one;
        badge.anchorMax = Vector2.one;
        badge.pivot = new Vector2(0.5f, 0.5f);
        badge.sizeDelta = new Vector2(BadgeSize, BadgeSize);
        badge.anchoredPosition = new Vector2(-BadgeSize * 0.35f, -BadgeSize * 0.35f);
        var badgeImage = badge.gameObject.AddComponent<Image>();
        badgeImage.sprite = LoadSprite("UI_Badge");
        badgeImage.raycastTarget = false;

        var badgeText = CreateText("Text", badge, font, "N", 30, Color.white);
        badgeText.rectTransform.anchorMin = Vector2.zero;
        badgeText.rectTransform.anchorMax = Vector2.one;
        badgeText.rectTransform.offsetMin = Vector2.zero;
        badgeText.rectTransform.offsetMax = Vector2.zero;

        var view = button.gameObject.AddComponent<InventoryNewBadgeView>();
        var so = new SerializedObject(view);
        so.FindProperty("_badge").objectReferenceValue = badge.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[InventoryHudSetup] 완료");
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
    {
        var rect = CreateRect(name, parent);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Sprite LoadSprite(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{CommonSpriteFolder}/{name}.png");
        if (sprite == null)
            throw new FileNotFoundException($"스프라이트를 찾을 수 없습니다: {name}");
        return sprite;
    }
}
