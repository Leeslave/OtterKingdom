using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 첫 마을 모임 연출 (광장, 공동 식탁 옆). [모임 열기]를 누르면(이미 완료·보상은 기록됨) 안전한 때에 짧게 보여 준다:
/// 카메라가 식탁을 비추고, 주민 해달 3~4마리의 모습(복사본 — 실제 주민은 하던 일을 그대로 함)이 식탁 둘레에 앉아 차례로 말풍선.
/// 10~15초, [건너뛰기] 가능. 끝나거나 건너뛰면 기념 카드 → 본 것으로 기록. 기념 기록에서 언제든 다시 볼 수 있다.
/// 새 씬·새 애니메이션 없이 기존 해달 그림·말풍선만 쓴다.
/// </summary>
public class GatheringDirector : MonoBehaviour
{
    private const int OverlayOrder = 32000;
    private const int UiSortingOrder = 125;
    private const int MaxGuests = 4;

    public static bool IsPlaying { get; private set; }

    [Header("자리")]
    [Tooltip("공동 식탁 (카메라가 비추는 곳)")]
    [SerializeField] private Transform _table;

    [Tooltip("해달이 앉을 자리 (식탁 둘레)")]
    [SerializeField] private List<Transform> _seats = new List<Transform>();

    [SerializeField] private PlazaCameraController _camera;

    [Header("연출")]
    [Min(4f)]
    [SerializeField] private float _seconds = 12f;

    [Tooltip("차례로 하는 말 (해달마다 하나씩, 모자라면 처음부터)")]
    [TextArea]
    [SerializeField] private List<string> _lines = new List<string>();

    [SerializeField] private Sprite _speechBubble;
    [SerializeField] private TMP_FontAsset _font;

    [Tooltip("기념 카드 제목")]
    [SerializeField] private string _memoryTitle = "첫 마을 모임";

    private SettlementManager _manager;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<string> _guestNames = new List<string>();
    private Coroutine _routine;
    private bool _skip;
    private GameObject _ui;

    private void Start()
    {
        _manager = SettlementManager.Instance;
        if (_manager == null)
            return;
        _manager.OnGatheringHeld += HandleHeld;
        _manager.OnGatheringReplayRequested += Play;
    }

    private void OnDestroy()
    {
        if (_manager != null)
        {
            _manager.OnGatheringHeld -= HandleHeld;
            _manager.OnGatheringReplayRequested -= Play;
        }
        Cleanup();
        IsPlaying = false;
    }

    private void HandleHeld(CommunityProjectDefinition project) => Play();

    public void Play()
    {
        if (_routine != null)
            return;
        _routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        // 사업 완료 알림·레벨업·튜토리얼 등이 먼저 (모임은 그 뒤에)
        yield return null;
        while (PresentationGate.IsBusy || CommunityProjectPresenter.IsOpen || SettlementPresenter.IsPopupOpen)
            yield return null;

        IsPlaying = true;
        _skip = false;
        if (_camera != null && _table != null)
            _camera.PanTo((Vector2)_table.position + Vector2.up * 0.8f);
        SpawnGuests();
        ShowSkipButton();

        float lineEvery = _spawned.Count > 0 ? Mathf.Max(1.6f, (_seconds - 2f) / _spawned.Count) : _seconds;
        float elapsed = 0f;
        int nextLine = 0;
        float nextLineAt = 1f;
        while (elapsed < _seconds && !_skip)
        {
            elapsed += Time.deltaTime;
            Bob(elapsed);
            if (nextLine < _spawned.Count && elapsed >= nextLineAt)
            {
                Speak(nextLine, _lines.Count > 0 ? _lines[nextLine % _lines.Count] : "반가워요!");
                nextLine++;
                nextLineAt += lineEvery;
            }
            yield return null;
        }

        Cleanup();
        _manager.MarkGatheringViewed();
        ShowMemory();
        IsPlaying = false;
        _routine = null;
    }

    // 주민(입주한 해달)의 광장 모습을 복사해 자리에 앉힘. 실제 주민 오브젝트는 건드리지 않음
    private void SpawnGuests()
    {
        _guestNames.Clear();
        var config = _manager.Config;
        var settlement = _manager.Settlement;
        foreach (var otterId in settlement.ResidentOrder)
        {
            if (_spawned.Count >= Mathf.Min(MaxGuests, _seats.Count))
                break;
            var otter = config.FindOtter(otterId);
            if (otter == null || otter.PlazaPrefab == null || otter.IsBuilder
                || !settlement.TryGetResidentState(otterId, out var state) || state != ResidentState.Resident)
                continue;
            var source = otter.PlazaPrefab.GetComponentInChildren<SpriteRenderer>(true);
            if (source == null)
                continue;

            var seat = _seats[_spawned.Count];
            var guest = new GameObject($"Guest_{otterId}");
            guest.transform.position = seat.position;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(guest.transform, false);
            visual.transform.localPosition = source.transform.localPosition;
            visual.transform.localScale = source.transform.lossyScale;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.sortingOrder = PlazaDepth.SortingOrderFor(seat.position.y);
            // 식탁을 바라봄
            renderer.flipX = _table != null && _table.position.x < seat.position.x;
            _spawned.Add(guest);
            _guestNames.Add(otter.DisplayName);
        }
    }

    private void Bob(float time)
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            var visual = _spawned[i].transform.GetChild(0);
            visual.localScale = new Vector3(visual.localScale.x, Mathf.Abs(visual.localScale.x) * (1f + Mathf.Sin(time * 5f + i) * 0.02f), 1f);
        }
    }

    private void Speak(int index, string line)
    {
        var guest = _spawned[index].transform;
        var bubble = new GameObject("Speech");
        bubble.transform.SetParent(guest, false);
        bubble.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        var renderer = bubble.AddComponent<SpriteRenderer>();
        renderer.sprite = _speechBubble;
        renderer.sortingOrder = OverlayOrder;
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(bubble.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 1.02f, 0f);
        var text = textGo.AddComponent<TextMeshPro>();
        if (_font != null)
        {
            _font.TryAddCharacters(line, out _);
            text.font = _font;
        }
        text.text = line;
        text.enableAutoSizing = true;
        text.fontSizeMin = 2f;
        text.fontSizeMax = 3f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color32(0x4B, 0x2E, 0x22, 0xFF);
        text.rectTransform.sizeDelta = new Vector2(3.2f, 1.1f);
        text.sortingOrder = OverlayOrder + 1;
    }

    private void ShowSkipButton()
    {
        var canvas = RuntimeUIKit.CreateCanvas("GatheringUI", UiSortingOrder, false);
        _ui = canvas.gameObject;
        RuntimeUIKit.PrepareGlyphs("건너뛰기" + _memoryTitle);
        var caption = RuntimeUIKit.CreateLabel(canvas.transform, _memoryTitle, RuntimeUIKit.TitleFontSize, true);
        var captionRect = caption.rectTransform;
        captionRect.anchorMin = captionRect.anchorMax = captionRect.pivot = new Vector2(0.5f, 0f);
        captionRect.anchoredPosition = new Vector2(0f, 360f);
        captionRect.sizeDelta = new Vector2(900f, 90f);
        caption.color = Color.white;
        var skip = RuntimeUIKit.CreateButton(canvas.transform, "건너뛰기", () => _skip = true, RuntimeUIKit.ButtonKind.Secondary, 280f);
        var rect = (RectTransform)skip.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(280f, RuntimeUIKit.ButtonHeight);
        rect.anchoredPosition = new Vector2(-40f, 300f);
    }

    // 기념 카드: 고정 구도의 기록 (누가 함께했는지). 다시 보기는 공동사업 화면·게시판 카드
    private void ShowMemory()
    {
        var canvas = RuntimeUIKit.CreateCanvas("GatheringMemory", UiSortingOrder, false);
        var dim = RuntimeUIKit.CreateDim(canvas.transform);
        var panel = RuntimeUIKit.CreatePanel(dim, 900f);
        string guests = _guestNames.Count > 0 ? string.Join(" · ", _guestNames) : "마을 주민들";
        string body = $"마을회관 앞 식탁에 모두 모였어요.\n함께한 해달: {guests}\n\n이 기록은 공동사업 화면에서 다시 볼 수 있어요.";
        RuntimeUIKit.PrepareGlyphs(_memoryTitle + body + "기념닫기");
        RuntimeUIKit.CreateLabel(panel, _memoryTitle + " 기념", RuntimeUIKit.TitleFontSize, true);
        RuntimeUIKit.CreateLabel(panel, body);
        RuntimeUIKit.CreateButton(panel, "닫기", () => Destroy(canvas.gameObject), RuntimeUIKit.ButtonKind.Primary);
    }

    private void Cleanup()
    {
        foreach (var go in _spawned)
        {
            if (go != null)
                Destroy(go);
        }
        _spawned.Clear();
        if (_ui != null)
            Destroy(_ui);
        _ui = null;
    }
}
