using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 씬(Assets/Scenes/Title.unity)을 새로 만들고 Build Settings 맨 앞(앱 시작 씬)에 등록한다.
/// - 배경 / 로고 / 해달 세 장은 같은 940×1672 캔버스에 그려진 레이어라서 겹쳐 놓기만 하면 맞는다.
/// - 배경은 화면을 꽉 채우고(비율이 다르면 잘림), 로고·해달은 잘리지 않게 화면 안에 맞춘다.
/// - 해달 아래 빈 곳에 "Touch To Start", 화면을 누르면 TitleScreen이 광장으로 넘긴다.
/// 다시 실행하면 씬을 통째로 새로 만든다 (씬에서 손본 것은 사라짐).
/// </summary>
public static class TitleSceneSetup
{
    public const string ScenePath = "Assets/Scenes/Title.unity";

    private const string ArtDir = "Assets/Art/Title";
    private const string BackgroundPath = ArtDir + "/Title_Background.png";
    private const string LogoPath = ArtDir + "/Title_Logo.png";
    private const string OttersPath = ArtDir + "/Title_Otters.png";
    private const string TouchTextMaterialPath = ArtDir + "/TouchToStart_Outline.mat";
    private const string FontPath = "Assets/Fonts/Cafe24Ssurround-v2.0 SDF.asset";

    // 원본 그림 크기 (세 장 공통)
    private const float ArtWidth = 940f;
    private const float ArtHeight = 1672f;

    // 해달 발끝이 그림 위에서 ~64%, 그 아래 빈 곳 가운데쯤 (그림 아래에서부터의 비율)
    private const float TouchTextCenterFromBottom = 0.24f;
    private const float TouchTextSize = 76f;
    private static readonly Color TouchTextColor = new Color32(0xFF, 0xF6, 0xE0, 0xFF);
    // 로고 글자 테두리와 같은 짙은 갈색
    private static readonly Color TouchTextOutline = new Color32(0x4A, 0x2A, 0x12, 0xFF);

    [MenuItem("OtterKingdom/Tools/Setup Title Scene")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[TitleSceneSetup] Play 모드를 끄고 실행하세요.");
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var background = ImportSprite(BackgroundPath);
        var logo = ImportSprite(LogoPath);
        var otters = ImportSprite(OttersPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (background == null || logo == null || otters == null || font == null)
        {
            Debug.LogError($"[TitleSceneSetup] {ArtDir}의 그림 세 장과 {FontPath}이 필요합니다.");
            return;
        }

        BuildScene(background, logo, otters, font, LoadOrCreateOutlineMaterial(font));
        RegisterFirstInBuild();
        AssetDatabase.SaveAssets();

        Debug.Log($"[TitleSceneSetup] {ScenePath} 생성 완료. Play 후 화면을 누르면 광장으로 넘어갑니다.");
    }

    private static void BuildScene(Sprite background, Sprite logo, Sprite otters, TMP_FontAsset font, Material outline)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 캔버스는 Overlay라 카메라가 안 보이지만, 카메라가 없으면 Game 뷰에 경고가 떠서 둔다
        var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);

        var canvasGo = new GameObject("TitleCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // GlobalUI와 같게
        canvasGo.AddComponent<GraphicRaycaster>();
        var root = (RectTransform)canvasGo.transform;

        // 배경: 화면을 꽉 채움 (남는 쪽은 잘림)
        var bg = CreateImage("Background", root, background, raycast: false);
        Fit(bg.rectTransform, AspectRatioFitter.AspectMode.EnvelopeParent);

        // 로고·해달·문구: 같은 그림 좌표계를 쓰도록 한 판에 모아서 화면 안에 맞춤
        var art = new GameObject("Foreground", typeof(RectTransform)).GetComponent<RectTransform>();
        art.SetParent(root, false);
        Fit(art, AspectRatioFitter.AspectMode.FitInParent);

        Stretch(CreateImage("Otters", art, otters, raycast: false).rectTransform);
        Stretch(CreateImage("Logo", art, logo, raycast: false).rectTransform);

        var text = new GameObject("TouchToStart", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(art, false);
        text.text = "Touch To Start";
        text.font = font;
        if (outline != null) text.fontSharedMaterial = outline;
        text.fontSize = TouchTextSize;
        text.color = TouchTextColor;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        var textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, TouchTextCenterFromBottom);
        textRect.anchorMax = new Vector2(1f, TouchTextCenterFromBottom);
        textRect.sizeDelta = new Vector2(0f, TouchTextSize * 1.6f);
        textRect.anchoredPosition = Vector2.zero;

        // 화면 전체 터치 영역 (투명, 맨 위)
        var touch = CreateImage("TouchArea", root, null, raycast: true);
        touch.color = Color.clear;
        Stretch(touch.rectTransform);
        var title = touch.gameObject.AddComponent<TitleScreen>();

        // 광장으로 넘어가기 전 어두워지는 막 (전역 UI의 것과 같은 ScreenFader)
        var faderImage = CreateImage("ScreenFader", root, null, raycast: true);
        faderImage.color = Color.black;
        Stretch(faderImage.rectTransform);
        var group = faderImage.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        var fader = faderImage.gameObject.AddComponent<ScreenFader>();

        var so = new SerializedObject(title);
        so.FindProperty("_touchText").objectReferenceValue = text;
        so.FindProperty("_fader").objectReferenceValue = fader;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 장소 씬은 전역 UI가 EventSystem을 가지고 오지만 타이틀에는 전역 UI가 없다
        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, bool raycast)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = sprite;
        image.raycastTarget = raycast;
        return image;
    }

    private static void Fit(RectTransform rect, AspectRatioFitter.AspectMode mode)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ArtWidth, ArtHeight);
        var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectRatio = ArtWidth / ArtHeight;
        fitter.aspectMode = mode;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Sprite ImportSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 폰트 기본 머티리얼을 건드리지 않도록 테두리용 복사본을 따로 만든다
    private static Material LoadOrCreateOutlineMaterial(TMP_FontAsset font)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(TouchTextMaterialPath);
        if (material == null)
        {
            material = new Material(font.material);
            AssetDatabase.CreateAsset(material, TouchTextMaterialPath);
        }

        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
        material.SetColor(ShaderUtilities.ID_OutlineColor, TouchTextOutline);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void RegisterFirstInBuild()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
