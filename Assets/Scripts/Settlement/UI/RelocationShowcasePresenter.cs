using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 정리 전 · 후 비교 컷 (구역·이사 기획안 4막): 첫 이사 지시서가 나올 때 광장(중앙 공터)을 사진으로 남겨 두고,
/// 옮길 건물이 하나도 남지 않으면(지시서를 하나 이상 끝냄) 같은 구도로 한 장 더 찍어 두 장을 폴라로이드처럼 나란히 보여 준다. 한 번만.
/// 사진은 세이브 폴더의 PNG (relocation_before.png). 기록은 발전으로 (relocation_before_taken · relocation_showcase_done)
/// 광장에서만 찍고 보여 준다 (다른 장소에 있으면 광장에 왔을 때). 코드로 만든 화면, 스스로 만들어짐
/// </summary>
public class RelocationShowcasePresenter : MonoBehaviour
{
    public const string BeforeTakenDevelopment = "relocation_before_taken";
    public const string ShowcaseDoneDevelopment = "relocation_showcase_done";
    private const string BeforeFile = "relocation_before.png";
    private const string PlazaScene = "Plaza";
    private const int SortingOrder = 120;
    private const float CheckSeconds = 1f;
    private const int PhotoWidth = 540;
    private const int PhotoHeight = 720;
    private const float PhotoOrtho = 11f;

    private static RelocationShowcasePresenter _instance;

    private GameObject _root;
    private RawImage _before;
    private RawImage _after;
    private TextMeshProUGUI _note;
    private float _timer;
    private Texture2D _beforeTexture;
    private Texture2D _afterTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureExists();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureExists();

    private static void EnsureExists()
    {
        if (_instance != null)
            return;
        var canvas = RuntimeUIKit.CreateCanvas(nameof(RelocationShowcasePresenter), SortingOrder, true);
        _instance = canvas.gameObject.AddComponent<RelocationShowcasePresenter>();
        _instance.Build(canvas.transform);
    }

    private static string BeforePath => Path.Combine(Application.persistentDataPath, BeforeFile);

    private void Build(Transform canvas)
    {
        _root = new GameObject("Showcase", typeof(RectTransform), typeof(Image));
        _root.transform.SetParent(canvas, false);
        RuntimeUIKit.Stretch((RectTransform)_root.transform);
        _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var panel = RuntimeUIKit.CreatePanel(_root.transform, 1000f, "Panel");
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        RuntimeUIKit.CreateLabel(panel, "광장이 반듯해졌어요!", RuntimeUIKit.TitleFontSize, true);
        var row = RuntimeUIKit.CreateRow(panel, 640f, 30f);
        _before = Polaroid(row, "정리 전", -3f);
        _after = Polaroid(row, "정리 후", 3f);
        _note = RuntimeUIKit.CreateLabel(panel, "", 30);
        var buttons = RuntimeUIKit.CreateRow(panel, RuntimeUIKit.ButtonHeight);
        RuntimeUIKit.CreateButton(buttons, "좋아요!", Close, RuntimeUIKit.ButtonKind.Primary, 360f);
        RuntimeUIKit.PrepareGlyphs("광장이 반듯해졌어요!정리 전후좋아요또박이: 이사 지시서를 모두 끝냈어요 · 왕국 질서도0123456789/");
        _root.SetActive(false);
    }

    // 하얀 폴라로이드 틀 + 사진 + 아래 글씨, 살짝 기울임
    private static RawImage Polaroid(Transform row, string caption, float tilt)
    {
        var frame = new GameObject("Polaroid", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        frame.transform.SetParent(row, false);
        var frameImage = frame.GetComponent<Image>();
        RuntimeUIKit.ApplySprite(frameImage, RuntimeUIKit.Style.OptionButton, new Color(1f, 0.99f, 0.95f));
        var element = frame.GetComponent<LayoutElement>();
        element.preferredWidth = 420f;
        element.preferredHeight = 620f;
        frame.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        var photo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage));
        photo.transform.SetParent(frame.transform, false);
        var rect = (RectTransform)photo.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(26f, 110f);
        rect.offsetMax = new Vector2(-26f, -26f);
        var raw = photo.GetComponent<RawImage>();
        raw.color = Color.white;

        var label = RuntimeUIKit.CreateLabel(frame.transform, caption, 38, true);
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.sizeDelta = new Vector2(0f, 90f);
        labelRect.anchoredPosition = new Vector2(0f, 14f);
        return raw;
    }

    private void Update()
    {
        if (_root.activeSelf)
            return;
        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f)
            return;
        _timer = CheckSeconds;

        var manager = SettlementManager.Instance;
        if (manager == null || !manager.IsLoaded || SceneManager.GetActiveScene().name != PlazaScene || DecorBoardView.Active == null)
            return;
        var settlement = manager.Settlement;
        if (settlement.HasDevelopment(ShowcaseDoneDevelopment))
            return;

        // 첫 지시서가 나오면 "정리 전" (꾸미기 · 구역 화면이 아닐 때)
        if (!settlement.HasDevelopment(BeforeTakenDevelopment))
        {
            if (settlement.RelocationOrder != null && !DecorModePresenter.IsActive && !RelocationModePresenter.IsOpen)
            {
                var photo = Capture(manager);
                if (photo != null)
                {
                    File.WriteAllBytes(BeforePath, photo.EncodeToPNG());
                    Destroy(photo);
                    settlement.UnlockDevelopment(BeforeTakenDevelopment);
                }
            }
            return;
        }

        // 지시서를 끝냈고 옮길 건물 · 이사 중인 건물이 없으면 "정리 후" + 비교 컷
        if (settlement.RelocationOrdersDone == 0 || settlement.RelocationOrder != null || settlement.RelocationMoves.Count > 0
            || PresentationGate.IsBusy || GameNotices.PendingCount > 0 || GameNoticeToast.IsShowing
            || DecorModePresenter.IsActive || RelocationModePresenter.IsOpen
            || (GameManager.Instance != null && GameManager.Instance.IsModalOpen))
            return;
        Show(manager);
    }

    private void Show(SettlementManager manager)
    {
        _beforeTexture = LoadBefore();
        _afterTexture = Capture(manager);
        _before.texture = _beforeTexture;
        _after.texture = _afterTexture;
        var order = manager.DistrictOrder;
        _note.text = $"또박이: 이사 지시서를 모두 끝냈어요 · 왕국 질서도 {(order != null ? order.Score : 0)}";
        manager.Settlement.UnlockDevelopment(ShowcaseDoneDevelopment);
        _root.SetActive(true);
    }

    private void Close()
    {
        _root.SetActive(false);
        if (_beforeTexture != null) Destroy(_beforeTexture);
        if (_afterTexture != null) Destroy(_afterTexture);
        _beforeTexture = _afterTexture = null;
    }

    private static Texture2D LoadBefore()
    {
        if (!File.Exists(BeforePath))
            return null;
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        return texture.LoadImage(File.ReadAllBytes(BeforePath)) ? texture : null;
    }

    // 중앙 공터를 같은 구도로 (카메라를 잠깐 옮겨 월드만 그림 — 화면 UI는 오버레이라 안 찍힘)
    private static Texture2D Capture(SettlementManager manager)
    {
        var camera = Camera.main;
        var board = DecorBoardView.Active;
        var center = manager.Config.FindDistrictBlock(DistrictRules.CenterBlockId);
        if (camera == null || board == null || center == null || center.Stages.Count == 0 || center.Stages[0].Areas.Count == 0)
            return null;
        var area = center.Stages[0].Areas[0];
        var focus = board.AreaWorldRect(new RectInt(area.position + board.Board.SaveOrigin, area.size)).center;

        var oldPosition = camera.transform.position;
        float oldSize = camera.orthographicSize;
        float oldAspect = camera.aspect;
        var oldTarget = camera.targetTexture;
        var rt = RenderTexture.GetTemporary(PhotoWidth, PhotoHeight, 24);
        camera.transform.position = new Vector3(focus.x, focus.y, oldPosition.z);
        camera.orthographicSize = PhotoOrtho;
        camera.aspect = (float)PhotoWidth / PhotoHeight;
        camera.targetTexture = rt;
        camera.Render();
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var photo = new Texture2D(PhotoWidth, PhotoHeight, TextureFormat.RGB24, false);
        photo.ReadPixels(new Rect(0, 0, PhotoWidth, PhotoHeight), 0, 0);
        photo.Apply();
        RenderTexture.active = active;
        camera.targetTexture = oldTarget;
        camera.aspect = oldAspect;
        camera.orthographicSize = oldSize;
        camera.transform.position = oldPosition;
        camera.ResetAspect();
        RenderTexture.ReleaseTemporary(rt);
        return photo;
    }
}
