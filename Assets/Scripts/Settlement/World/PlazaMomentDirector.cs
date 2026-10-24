using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 광장 순간포착 (PlazaMomentRules). 사진관을 다 지은 뒤, 주인공 해달(꾸벅이 · 재잘이 · 끄적이 · 두리번)이
/// 좋아하는 자리(벤치 · 식탁 · 밤 가로등)에 머무는 동안 확률로 장면이 시작된다:
/// 가까운 한가한 해달이 같은 자리로 와서(실제 광장 해달) 차례로 말풍선 → 마지막 말과 함께 머리 위에 카메라 말풍선.
/// 그걸 누르면 찰칵(플래시) → 사진 카드 → 도감 순간 탭에 등록 (처음이면 함께한 해달 · 날짜). 안 누르면 그냥 지나감.
/// 주인공이 한가하면 가끔 좋아하는 자리로 가 본다 (조건을 너무 기다리지 않게).
/// SettlementPlazaView가 시작할 때 붙인다.
/// </summary>
public class PlazaMomentDirector : MonoBehaviour
{
    private const int OverlayOrder = 32000;
    private const int UiSortingOrder = 125;
    private const string CameraSpritePath = "UI/Celebrate/MomentCamera";
    // 카메라 말풍선을 누른 것으로 치는 거리 (월드 단위)
    private const float CaptureRadius = 0.9f;
    private const float FlashSeconds = 0.45f;

    private static PlazaMomentDirector _current;

    private SettlementPlazaView _plaza;
    private SettlementManager _manager;
    private Sprite _speechBubble;
    private TMP_FontAsset _font;
    private float _otterHeight;
    private Sprite _cameraSprite;

    private float _nextCheck;
    private float _cooldownUntil;
    private Coroutine _scene;
    private SpriteRenderer _camera;
    private bool _captured;
    private readonly List<GameObject> _bubbles = new List<GameObject>();

    /// <summary>지금 카메라 말풍선 위를 누른 탭이면 true (해달 한마디보다 먼저)</summary>
    public static bool OwnsTap(Vector2 world)
    {
        var camera = _current != null ? _current._camera : null;
        return camera != null && camera.gameObject.activeSelf
            && Vector2.Distance(world, camera.transform.position + Vector3.up * 0.45f) <= CaptureRadius;
    }

    public void Init(SettlementPlazaView plaza, SettlementManager manager, Sprite speechBubble, TMP_FontAsset font, float otterHeight)
    {
        _plaza = plaza;
        _manager = manager;
        _speechBubble = speechBubble;
        _font = font;
        _otterHeight = otterHeight;
        _cameraSprite = Resources.Load<Sprite>(CameraSpritePath);
        _current = this;
        _nextCheck = Time.time + PlazaMomentRules.CheckSeconds;
    }

    private void OnDestroy()
    {
        EndScene();
        if (_current == this)
            _current = null;
    }

    private void Update()
    {
        if (_manager == null || !_manager.IsLoaded || _scene != null || Time.time < _nextCheck)
            return;
        _nextCheck = Time.time + PlazaMomentRules.CheckSeconds;
        if (Time.time < _cooldownUntil || !PlazaMomentRules.IsOpen(_manager.Settlement.HasDevelopment))
            return;
        if (PresentationGate.IsBusy || GatheringDirector.IsPlaying || DecorModePresenter.IsActive)
            return;

        foreach (var moment in PlazaMomentRules.All)
        {
            var hero = _plaza.FindAgent(moment.OtterId);
            if (hero == null || !hero.isActiveAndEnabled)
                continue;

            var spot = hero.CurrentSpot;
            if (spot == null)
            {
                // 한가하면 가끔 좋아하는 자리로
                if (Random.value < PlazaMomentRules.NudgeChance)
                    hero.TryVisitSpot(moment.Spot);
                continue;
            }

            var companions = PickCompanions(moment, hero, spot);
            if (!PlazaMomentRules.CanStart(moment, spot.Kind, spot.FreeSeats, companions.Count)
                || Random.value >= PlazaMomentRules.StartChance)
                continue;

            _scene = StartCoroutine(PlayScene(moment, hero, spot, companions));
            return;
        }
    }

    // 자리에서 가까운 한가한 해달부터 (주인공 · 다른 장면 해달 제외)
    private List<OtterWanderAgent> PickCompanions(PlazaMoment moment, OtterWanderAgent hero, PlazaSpotView spot)
    {
        var result = new List<OtterWanderAgent>();
        foreach (var pair in _plaza.SpawnedOtters)
        {
            var agent = pair.Value;
            if (agent == null || agent == hero || !agent.isActiveAndEnabled || !agent.IsFree)
                continue;
            result.Add(agent);
        }
        Vector2 at = spot.transform.position;
        result.Sort((a, b) => Vector2.Distance(a.Position, at).CompareTo(Vector2.Distance(b.Position, at)));
        if (result.Count > moment.Companions)
            result.RemoveRange(moment.Companions, result.Count - moment.Companions);
        return result;
    }

    private IEnumerator PlayScene(PlazaMoment moment, OtterWanderAgent hero, PlazaSpotView spot, List<OtterWanderAgent> companions)
    {
        _captured = false;
        float sceneSeconds = PlazaMomentRules.ArriveTimeoutSeconds + PlazaMomentRules.BeatSeconds * moment.Beats.Count
            + PlazaMomentRules.CaptureSeconds;
        hero.KeepStaying(sceneSeconds);

        // 동료를 같은 자리로 부름 (못 오면 장면 취소)
        var cast = new List<OtterWanderAgent> { hero };
        foreach (var companion in companions)
        {
            if (!companion.TryVisitSpot(spot, sceneSeconds))
            {
                Finish(false);
                yield break;
            }
            cast.Add(companion);
        }

        float waited = 0f;
        while (!AllArrived(cast, spot))
        {
            waited += Time.deltaTime;
            if (waited > PlazaMomentRules.ArriveTimeoutSeconds || hero.CurrentSpot != spot)
            {
                Finish(false);
                yield break;
            }
            yield return null;
        }
        foreach (var otter in cast)
            otter.KeepStaying(PlazaMomentRules.BeatSeconds * moment.Beats.Count + PlazaMomentRules.CaptureSeconds + 1f);

        // 차례로 말풍선. 마지막 말과 함께 카메라 말풍선 (그때부터 누를 수 있음)
        for (int i = 0; i < moment.Beats.Count; i++)
        {
            var (speaker, line) = moment.Beats[i];
            if (speaker < cast.Count)
                Say(cast[speaker], line);
            if (i == moment.Beats.Count - 1)
            {
                ShowCamera(cast);
                break;
            }
            yield return new WaitForSeconds(PlazaMomentRules.BeatSeconds);
        }

        float left = PlazaMomentRules.CaptureSeconds;
        while (left > 0f && !_captured)
        {
            left -= Time.deltaTime;
            BobCamera();
            if (PlazaTapInput.TryGetTap(out Vector2 world) && OwnsTap(world))
            {
                _captured = true;
                Capture(moment, cast);
            }
            yield return null;
        }
        Finish(true);
    }

    private static bool AllArrived(List<OtterWanderAgent> cast, PlazaSpotView spot)
    {
        foreach (var otter in cast)
        {
            if (otter == null || otter.CurrentSpot != spot)
                return false;
        }
        return true;
    }

    private void Finish(bool played)
    {
        EndScene();
        _scene = null;
        _cooldownUntil = Time.time + (played ? PlazaMomentRules.CooldownSeconds : PlazaMomentRules.CooldownSeconds * 0.3f);
    }

    private void EndScene()
    {
        foreach (var bubble in _bubbles)
        {
            if (bubble != null)
                Destroy(bubble);
        }
        _bubbles.Clear();
        if (_camera != null)
            Destroy(_camera.gameObject);
        _camera = null;
    }

    #region 보이는 것

    // 그 해달 머리 위 말풍선 (한 해달에 하나: 새 말이 오면 앞 말은 닫음)
    private void Say(OtterWanderAgent otter, string line)
    {
        if (otter == null || _speechBubble == null)
            return;
        string name = "MomentSpeech";
        var old = otter.transform.Find(name);
        if (old != null)
        {
            _bubbles.Remove(old.gameObject);
            Destroy(old.gameObject);
        }

        float inverse = 1f / Mathf.Max(0.01f, otter.transform.lossyScale.y);
        var bubble = new GameObject(name);
        bubble.transform.SetParent(otter.transform, false);
        bubble.transform.localPosition = new Vector3(0f, (_otterHeight + 0.05f) * inverse, 0f);
        bubble.transform.localScale = Vector3.one * inverse;
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
        text.color = WorldSticker.Cocoa;
        text.rectTransform.sizeDelta = new Vector2(3.2f, 1.1f);
        text.sortingOrder = OverlayOrder + 1;
        _bubbles.Add(bubble);
        Destroy(bubble, PlazaMomentRules.BeatSeconds * 1.6f);
    }

    // 모인 해달들 가운데 위로 카메라 말풍선
    private void ShowCamera(List<OtterWanderAgent> cast)
    {
        if (_cameraSprite == null)
            return;
        Vector2 center = Vector2.zero;
        float top = float.MinValue;
        foreach (var otter in cast)
        {
            center += otter.Position;
            top = Mathf.Max(top, otter.Position.y);
        }
        center /= cast.Count;
        var go = new GameObject("MomentCamera");
        go.transform.position = new Vector3(center.x, top + _otterHeight + 1.9f, 0f);
        go.transform.localScale = Vector3.one * 1.2f;
        _camera = go.AddComponent<SpriteRenderer>();
        _camera.sprite = _cameraSprite;
        _camera.sortingOrder = OverlayOrder + 2;
        _cameraBase = go.transform.position;
    }

    private Vector3 _cameraBase;

    private void BobCamera()
    {
        if (_camera == null)
            return;
        float t = Time.time;
        _camera.transform.position = _cameraBase + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 3.5f)) * 0.15f);
        _camera.transform.localScale = Vector3.one * (1.2f + Mathf.Sin(t * 7f) * 0.05f);
    }

    #endregion

    #region 포착

    private void Capture(PlazaMoment moment, List<OtterWanderAgent> cast)
    {
        var collection = CollectionManager.Instance;
        if (collection == null || !collection.Database.TryGet(moment.EntryId, out var entry))
        {
            Debug.LogWarning($"[PlazaMomentDirector] 도감에 순간 항목이 없습니다: {moment.EntryId}");
            return;
        }

        var names = new List<string>();
        for (int i = 1; i < cast.Count; i++)
        {
            var otter = _manager.Config.FindOtter(cast[i].OtterId);
            if (otter != null)
                names.Add(otter.DisplayName);
        }
        string companions = string.Join(" · ", names);
        bool first = collection.CaptureMoment(moment.EntryId, companions, PlazaMomentRules.FormatDate(System.DateTime.Now));

        if (_camera != null)
            _camera.gameObject.SetActive(false);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(SfxKind.Find);
        StartCoroutine(FlashThenCard(entry, companions, first));
    }

    private IEnumerator FlashThenCard(CollectionEntry entry, string companions, bool first)
    {
        var canvas = RuntimeUIKit.CreateCanvas("MomentFlash", UiSortingOrder + 1, false);
        var flash = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        flash.transform.SetParent(canvas.transform, false);
        RuntimeUIKit.Stretch((RectTransform)flash.transform);
        var image = flash.GetComponent<Image>();
        image.raycastTarget = false;
        for (float t = 0f; t < FlashSeconds; t += Time.unscaledDeltaTime)
        {
            image.color = new Color(1f, 1f, 1f, 1f - t / FlashSeconds);
            yield return null;
        }
        Destroy(canvas.gameObject);
        ShowCard(entry, companions, first);
    }

    // 사진 카드: 미리 그린 순간 그림 + 제목 + 함께한 해달
    private void ShowCard(CollectionEntry entry, string companions, bool first)
    {
        var canvas = RuntimeUIKit.CreateCanvas("MomentCard", UiSortingOrder, false);
        var dim = RuntimeUIKit.CreateDim(canvas.transform);
        var panel = RuntimeUIKit.CreatePanel(dim, 900f);
        string heading = first ? "순간포착!" : "또 찰칵!";
        string body = string.IsNullOrEmpty(companions) ? entry.Tagline : $"{entry.Tagline}\n함께한 해달: {companions}";
        string footer = first ? "도감 순간 탭에 남겼어요." : "처음 만난 날의 기록은 도감에 그대로 있어요.";
        RuntimeUIKit.PrepareGlyphs(heading + entry.DisplayName + body + footer + "닫기");
        RuntimeUIKit.CreateLabel(panel, heading, RuntimeUIKit.TitleFontSize, true);
        var photo = RuntimeUIKit.CreateIcon(panel, entry.Portrait, 760f);
        photo.GetComponent<LayoutElement>().preferredHeight = 760f * 2f / 3f;
        RuntimeUIKit.CreateLabel(panel, entry.DisplayName, RuntimeUIKit.TitleFontSize, true);
        RuntimeUIKit.CreateLabel(panel, body);
        RuntimeUIKit.CreateLabel(panel, footer);
        RuntimeUIKit.CreateButton(panel, "닫기", () => Destroy(canvas.gameObject), RuntimeUIKit.ButtonKind.Primary);
    }

    #endregion
}
