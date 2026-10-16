using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameNotices의 알림을 화면 위쪽 띠로 보여 준다 (코드로 만드는 화면, 씬을 넘어 유지).
/// 화면을 막지 않고, 띠 위만 누를 수 있다. 몇 초 뒤 저절로 닫히고 [닫기]로 바로 닫을 수 있다.
/// </summary>
public class GameNoticeToast : MonoBehaviour
{
    // 전역 UI(100) 위, 장소 팝업(200) 아래
    private const int SortingOrder = 150;
    private const float ShowSeconds = 6f;
    private const float FadeSeconds = 0.25f;
    private const float Width = 980f;
    private const float TopOffset = 300f;

    private static GameNoticeToast _instance;

    private RectTransform _panel;
    private CanvasGroup _group;
    private TextMeshProUGUI _message;
    private Button _action;
    private GameNotice _current;
    private float _timer;
    private bool _showing;

    public static bool IsShowing => _instance != null && _instance._showing;

    public static void EnsureExists()
    {
        if (_instance != null)
            return;
        var canvas = RuntimeUIKit.CreateCanvas(nameof(GameNoticeToast), SortingOrder, true);
        _instance = canvas.gameObject.AddComponent<GameNoticeToast>();
        _instance.Build(canvas.transform);
    }

    private void Build(Transform root)
    {
        _panel = RuntimeUIKit.CreatePanel(root, Width, "Notice");
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 1f);
        _panel.anchoredPosition = new Vector2(0f, -TopOffset);
        _group = _panel.gameObject.AddComponent<CanvasGroup>();

        _message = RuntimeUIKit.CreateLabel(_panel, string.Empty);
        var row = RuntimeUIKit.CreateRow(_panel, 96f);
        _action = RuntimeUIKit.CreateButton(row, string.Empty, HandleAction, RuntimeUIKit.ButtonKind.Primary, 300f, height: 96f);
        RuntimeUIKit.CreateButton(row, "닫기", Close, RuntimeUIKit.ButtonKind.Secondary, 220f, height: 96f);
        _panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (_showing)
        {
            _timer -= Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(Mathf.Min(ShowSeconds - _timer, _timer) / FadeSeconds);
            if (_timer <= 0f)
                Close();
            return;
        }
        if (GameNotices.PendingCount == 0 || PresentationGate.IsBusy)
            return;
        if (GameNotices.TryTake(out var notice))
            Show(notice);
    }

    private void Show(GameNotice notice)
    {
        _current = notice;
        RuntimeUIKit.PrepareGlyphs(notice.Message + notice.ActionLabel + "닫기");
        _message.text = notice.Message;
        _action.gameObject.SetActive(notice.HasAction);
        if (notice.HasAction)
            RuntimeUIKit.SetButtonLabel(_action, notice.ActionLabel);
        _timer = ShowSeconds;
        _group.alpha = 0f;
        _panel.gameObject.SetActive(true);
        _showing = true;
    }

    private void HandleAction()
    {
        var action = _current.Action;
        Close();
        action?.Invoke();
    }

    private void Close()
    {
        _showing = false;
        _panel.gameObject.SetActive(false);
    }
}
