using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낚시 개편 테스트 씬 화면 (코드로 만듦, 광산 개편과 같은 스티커 UI 부품):
/// 골드 · 의뢰 카드 · 바늘 거리/깊이 · 안내 글 · 강화 · 도감 · 낚은 물고기 알림 · 새 물고기 발견 · 테스트
/// </summary>
public class FishingHud : MonoBehaviour
{
    private static readonly Color Cocoa = new Color32(75, 46, 34, 255);
    private static readonly Color Gold = new Color32(255, 214, 92, 255);
    private static readonly Color Muted = new Color32(140, 112, 96, 255);

    private FishingReworkGame _game;
    private FishingReworkModel _model;
    private FishingReworkArt _art;
    private MineReworkArt _ui;
    private MineSfx _sfx;
    private Canvas _canvas;
    private RectTransform _root, _safe;

    private RectTransform _goldPill;
    private TextMeshProUGUI _gold;
    private float _goldShown;
    private float _goldPunch;

    private RectTransform _request;
    private Image _requestIcon, _requestFill;
    private TextMeshProUGUI _requestTitle, _requestCount, _requestReward, _requestButtonLabel;
    private Button _requestButton;
    private Image _requestButtonImage;
    private float _requestPulse;

    private RectTransform _info;
    private TextMeshProUGUI _infoText;
    private TextMeshProUGUI _hint;
    private string _hintOverride;
    private float _hintTimer;

    private GameObject _upgradeBadge, _bookBadge, _bagBadge;
    private TextMeshProUGUI _bagCount;
    private RectTransform _bagButton;
    private float _bagPunch;
    private bool _bookNew;

    private RectTransform _toast;
    private Image _toastIcon;
    private TextMeshProUGUI _toastName, _toastDetail, _toastGold, _toastTag;
    private float _toastTimer;

    private GameObject _sheet;
    private GameObject _reveal;
    private RectTransform _revealRays;

    public bool Blocking => _sheet != null || _reveal != null;

    public void Setup(FishingReworkGame game, FishingReworkModel model, FishingReworkArt art, MineSfx sfx)
    {
        _game = game;
        _model = model;
        _art = art;
        _ui = art.Ui;
        _sfx = sfx;
        // 본 게임: 전역 UI(위 막대 · 아래 이동 막대, 순서 100) 아래에, 그 높이만큼 비우고 (기준 1080 × 1920)
        _canvas = RuntimeUIKit.CreateCanvas("FishingHud", game.Hosted ? HostedSortingOrder : 100, false);
        _canvas.transform.SetParent(transform, false);
        _root = (RectTransform)_canvas.transform;
        _safe = NewRect(_root, "SafeArea");
        RuntimeUIKit.Stretch(_safe);
        _safe.gameObject.AddComponent<SafeAreaFltter>();
        if (game.Hosted)
        {
            var inset = NewRect(_safe, "HostedInset");
            RuntimeUIKit.Stretch(inset);
            inset.offsetMin = new Vector2(0f, HostedBottomInset);
            inset.offsetMax = new Vector2(0f, -HostedTopInset);
            _safe = inset;
        }

        BuildTop();
        BuildRequest();
        BuildMiddle();
        BuildBottom();
        if (game.Hosted)
            ApplyHostedLayout();

        _goldShown = model.Gold;
        _model.Changed += Refresh;
        _model.RequestChanged += RefreshRequest;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_model == null)
            return;
        _model.Changed -= Refresh;
        _model.RequestChanged -= RefreshRequest;
    }

    #region 만들기 도우미

    private static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(Transform parent, string name, Sprite sprite, Color color, bool preserve = false)
    {
        var rect = NewRect(parent, name);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero)
            image.type = Image.Type.Sliced;
        image.preserveAspect = preserve;
        return image;
    }

    private static TextMeshProUGUI NewText(Transform parent, string text, float size, bool title, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center, float outline = 0f)
    {
        var label = RuntimeUIKit.CreateLabel(parent, text, Mathf.RoundToInt(size), title, align);
        label.color = color;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        if (outline > 0f)
        {
            label.outlineWidth = outline;
            label.outlineColor = Cocoa;
        }
        return label;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Fill(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private Button MakeButton(RectTransform rect, Action onClick, bool sound = true)
    {
        var graphic = rect.GetComponent<Graphic>();
        if (graphic == null)
        {
            graphic = rect.gameObject.AddComponent<Image>();
            graphic.color = new Color(1f, 1f, 1f, 0f);
        }
        graphic.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = graphic;
        rect.gameObject.AddComponent<MinePress>();
        button.onClick.AddListener(() =>
        {
            if (sound)
                _sfx.Play(MineSfx.Kind.Tap);
            onClick?.Invoke();
        });
        return button;
    }

    private GameObject NewBadge(RectTransform parent)
    {
        var badge = NewImage(parent, "Badge", _ui.Badge, Color.white);
        Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-24f, -24f), new Vector2(60f, 60f));
        var mark = NewText(badge.transform, "!", 40, true, Color.white);
        Fill(mark.rectTransform, 0f, 4f);
        badge.gameObject.SetActive(false);
        return badge.gameObject;
    }

    private static string Money(int amount) => amount.ToString("N0");

    #endregion

    #region 위 · 의뢰

    private void BuildTop()
    {
        var pill = NewImage(_safe, "Gold", RuntimeUIKit.Style.OptionButton, Color.white);
        _goldPill = pill.rectTransform;
        Place(_goldPill, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(340f, 104f));
        var coin = NewImage(pill.transform, "Coin", _ui.Gold, Color.white, true);
        Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50f, 6f), new Vector2(92f, 92f));
        _gold = NewText(pill.transform, "0", 44, true, Cocoa, TextAlignmentOptions.Right);
        Place(_gold.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 6f), new Vector2(230f, 60f));
    }

    private void BuildRequest()
    {
        var card = NewImage(_safe, "Request", _ui.Paper, new Color(1f, 1f, 1f, 0.97f));
        _request = card.rectTransform;
        Place(_request, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -144f), new Vector2(1032f, 214f));

        var tag = NewImage(card.transform, "Tag", _ui.RibbonLav, Color.white);
        Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(24f, -4f), new Vector2(150f, 50f));
        var tagText = NewText(tag.transform, "의뢰", 28, true, Color.white, TextAlignmentOptions.Center, 0.15f);
        Fill(tagText.rectTransform, 0f, 4f);

        var iconBack = NewImage(card.transform, "IconBack", _ui.RoundCream, Color.white, true);
        Place(iconBack.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, -6f), new Vector2(150f, 150f));
        _requestIcon = NewImage(iconBack.transform, "Icon", null, Color.white, true);
        Fill(_requestIcon.rectTransform, 14f, 14f, 14f, 14f);

        _requestTitle = NewText(card.transform, "", 40, true, Cocoa, TextAlignmentOptions.Left);
        Place(_requestTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(196f, -34f), new Vector2(560f, 56f));
        _requestTitle.enableAutoSizing = true;
        _requestTitle.fontSizeMin = 26f;
        _requestTitle.fontSizeMax = 40f;

        var track = NewImage(card.transform, "Track", _ui.BoxInset, new Color(0.88f, 0.82f, 0.74f, 1f));
        Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(196f, -106f), new Vector2(420f, 44f));
        _requestFill = NewImage(track.transform, "Fill", _ui.BarFill, new Color(0.45f, 0.8f, 0.95f));
        _requestFill.type = Image.Type.Filled;
        _requestFill.fillMethod = Image.FillMethod.Horizontal;
        Fill(_requestFill.rectTransform, 4f, 4f, 4f, 4f);
        _requestCount = NewText(track.transform, "", 28, true, Cocoa);
        Fill(_requestCount.rectTransform, 0f, 2f);

        var coin = NewImage(card.transform, "Coin", _ui.Gold, Color.white, true);
        Place(coin.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(632f, -128f), new Vector2(56f, 56f));
        _requestReward = NewText(card.transform, "", 34, true, Cocoa, TextAlignmentOptions.Left);
        Place(_requestReward.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(692f, -126f), new Vector2(150f, 50f));

        _requestButtonImage = NewImage(card.transform, "Button", RuntimeUIKit.Style.OptionButton, Color.white);
        Place(_requestButtonImage.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, -6f), new Vector2(196f, 108f));
        _requestButton = MakeButton(_requestButtonImage.rectTransform, OnRequestButton, false);
        _requestButtonLabel = NewText(_requestButtonImage.transform, "", 32, true, Cocoa);
        Fill(_requestButtonLabel.rectTransform, 6f, 12f, 6f);
        _requestButtonLabel.enableAutoSizing = true;
        _requestButtonLabel.fontSizeMin = 22f;
        _requestButtonLabel.fontSizeMax = 34f;
    }

    private void OnRequestButton()
    {
        var request = _model.Request;
        if (request != null && request.Done)
        {
            int reward = request.Reward;
            if (_model.ClaimRequest())
            {
                _sfx.Play(MineSfx.Kind.Sell);
                _goldPunch = 1f;
                ShowMessage($"의뢰 완료! +{Money(reward)} 골드");
            }
            return;
        }
        if (_model.SkipRequest())
            _sfx.Play(MineSfx.Kind.Open);
        else
            _sfx.Play(MineSfx.Kind.Tap, 0.7f);
    }

    private string RequestTitle(FishingRequest request)
    {
        switch (request.Kind)
        {
            case FishingRequestKind.Fish: return $"{request.Fish.DisplayName} {request.Needed}마리 낚기";
            case FishingRequestKind.Any: return $"아무 물고기나 {request.Needed}마리 낚기";
            default: return $"{request.Target:0.#}kg 넘는 물고기 낚기";
        }
    }

    private void RefreshRequest()
    {
        var request = _model.Request;
        if (request == null)
        {
            _request.gameObject.SetActive(false);
            return;
        }
        _request.gameObject.SetActive(true);
        _requestIcon.sprite = request.Kind == FishingRequestKind.Fish ? request.Fish.Icon
            : request.Kind == FishingRequestKind.Big ? _art.IconBig : _art.IconAnyFish;
        _requestTitle.text = RequestTitle(request);
        _requestFill.fillAmount = request.Ratio;
        _requestFill.color = request.Done ? new Color(0.5f, 0.85f, 0.45f) : new Color(0.45f, 0.8f, 0.95f);
        _requestCount.text = $"{request.Count}/{request.Needed}";
        _requestReward.text = Money(request.Reward);
        RefreshRequestButton();
    }

    private void RefreshRequestButton()
    {
        var request = _model.Request;
        if (request == null)
            return;
        if (request.Done)
        {
            RuntimeUIKit.ApplySprite(_requestButtonImage, RuntimeUIKit.Style.PrimaryButton, Gold);
            _requestButtonLabel.text = "받기";
            _requestButtonLabel.color = RuntimeUIKit.Style.PrimaryButton != null ? RuntimeUIKit.Style.PrimaryLabelColor : Cocoa;
            return;
        }
        RuntimeUIKit.ApplySprite(_requestButtonImage, RuntimeUIKit.Style.OptionButton, Color.white);
        _requestButtonLabel.color = _model.CanSkipRequest ? Cocoa : Muted;
        _requestButtonLabel.text = _model.CanSkipRequest ? "바꾸기" : $"0:{Mathf.CeilToInt(_model.SkipCooldown):00}";
    }

    /// <summary>의뢰를 다 했을 때 카드가 통통</summary>
    public void PulseRequest()
    {
        _requestPulse = 1f;
        RefreshRequest();
    }

    #endregion

    #region 가운데 · 아래

    private void BuildMiddle()
    {
        var chip = NewImage(_safe, "Info", _ui.ChipNormal, new Color(1f, 1f, 1f, 0.92f));
        _info = chip.rectTransform;
        Place(_info, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -378f), new Vector2(470f, 80f));
        _infoText = NewText(chip.transform, "", 32, true, Cocoa);
        Fill(_infoText.rectTransform, 12f, 6f, 12f);
        _infoText.enableAutoSizing = true;
        _infoText.fontSizeMin = 22f;
        _infoText.fontSizeMax = 32f;

        var toast = NewImage(_safe, "Toast", _ui.Paper, Color.white);
        _toast = toast.rectTransform;
        Place(_toast, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -480f), new Vector2(780f, 200f));
        _toastIcon = NewImage(toast.transform, "Icon", null, Color.white, true);
        Place(_toastIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -4f), new Vector2(210f, 150f));
        _toastName = NewText(toast.transform, "", 44, true, Cocoa, TextAlignmentOptions.Left);
        Place(_toastName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(246f, 6f), new Vector2(330f, 56f));
        _toastDetail = NewText(toast.transform, "", 30, false, RuntimeUIKit.Style.BodyColor, TextAlignmentOptions.Left);
        Place(_toastDetail.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(246f, -6f), new Vector2(330f, 44f));
        _toastGold = NewText(toast.transform, "", 44, true, new Color32(214, 150, 40, 255), TextAlignmentOptions.Right);
        Place(_toastGold.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, -2f), new Vector2(170f, 60f));
        var tag = NewImage(toast.transform, "Tag", _ui.RibbonPeach, Color.white);
        Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(260f, 60f));
        _toastTag = NewText(tag.transform, "", 30, true, Color.white, TextAlignmentOptions.Center, 0.15f);
        Fill(_toastTag.rectTransform, 0f, 6f);
        _toast.gameObject.SetActive(false);
    }

    private void BuildBottom()
    {
        _hint = NewText(_safe, "", 38, true, Color.white, TextAlignmentOptions.Center, 0.22f);
        Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1000f, 70f));

        _upgradeBadge = RoundButton("Upgrade", _art.IconUpgrade, "강화", new Vector2(-270f, 70f), OpenUpgrades);
        _bagBadge = RoundButton("Bag", _art.IconBag, "가방", new Vector2(0f, 70f), OpenBag);
        _bagButton = (RectTransform)_bagBadge.transform.parent;
        _bagCount = _bagBadge.GetComponentInChildren<TextMeshProUGUI>(true);
        _bagCount.fontSize = 30f;
        _bookBadge = RoundButton("Book", _art.IconBook, "도감", new Vector2(270f, 70f), OpenBook);

        var test = NewImage(_safe, "Test", _ui.ChipNormal, new Color(1f, 1f, 1f, 0.75f));
        Place(test.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(150f, 64f));
        MakeButton(test.rectTransform, OpenTest);
        var label = NewText(test.transform, "테스트", 26, true, Muted);
        Fill(label.rectTransform, 0f, 4f);
    }

    // 본 게임: 골드 · 가방 · 도감은 전역 UI 것 → 숨기고, 강화 옆에 [나가기] (선착장으로)
    private const int HostedSortingOrder = 50;
    private const float HostedTopInset = 300f;
    private const float HostedBottomInset = 300f;

    private void ApplyHostedLayout()
    {
        _goldPill.gameObject.SetActive(false);
        _bagButton.gameObject.SetActive(false);
        _bookBadge.transform.parent.gameObject.SetActive(false);
        var test = _safe.Find("Test");
        if (test != null)
            test.gameObject.SetActive(false);
        // 의뢰 카드: 오른쪽 뽑기 버튼 자리(190)를 비우고 왼쪽에 붙임 → 안쪽도 좁게
        const float cardWidth = 1080f - 24f - 190f;
        Place(_request, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(cardWidth, 214f));
        _requestTitle.rectTransform.sizeDelta = new Vector2(460f, 56f);
        _requestFill.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(290f, 44f);
        var coin = _request.Find("Coin") as RectTransform;
        if (coin != null)
            coin.anchoredPosition = new Vector2(500f, -128f);
        _requestReward.rectTransform.anchoredPosition = new Vector2(556f, -126f);
        _requestReward.rectTransform.sizeDelta = new Vector2(110f, 50f);
        _requestButtonImage.rectTransform.sizeDelta = new Vector2(170f, 108f);
        _info.anchoredPosition = new Vector2(24f, -244f);
        _toast.anchoredPosition = new Vector2(0f, -350f);
        var upgrade = (RectTransform)_upgradeBadge.transform.parent;
        upgrade.anchoredPosition = new Vector2(-170f, 30f);
        var exit = RoundButton("Exit", _art.IconPier, "나가기", new Vector2(170f, 30f), () => _game.Exit());
        exit.SetActive(false);
        _hint.rectTransform.anchoredPosition = new Vector2(0f, 270f);
    }

    private GameObject RoundButton(string name, Sprite icon, string text, Vector2 position, Action onClick)
    {
        var back = NewImage(_safe, name, _ui.RoundFeatured, Color.white, true);
        Place(back.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, new Vector2(190f, 190f));
        MakeButton(back.rectTransform, onClick);
        var image = NewImage(back.transform, "Icon", icon, Color.white, true);
        Fill(image.rectTransform, 34f, 40f, 34f, 28f);
        var label = NewText(back.transform, text, 36, true, Color.white, TextAlignmentOptions.Center, 0.25f);
        Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(200f, 50f));
        return NewBadge(back.rectTransform);
    }

    #endregion

    #region 갱신

    public void Refresh()
    {
        RefreshRequest();
        _upgradeBadge.SetActive(_model.AnyUpgradeAffordable);
        _bagBadge.SetActive(_model.Bag.Count > 0);
        _bagCount.text = _model.Bag.Count.ToString();
        _bookBadge.SetActive(_bookNew);
    }

    public void ShowHint()
    {
        _hintOverride = null;
        _hintTimer = 0f;
    }

    private void ShowMessage(string text, float seconds = 1.8f)
    {
        _hintOverride = text;
        _hintTimer = seconds;
    }

    public void ShowHeavyTip(FishingFish fish) =>
        ShowMessage($"{fish.Weight:0.#}kg은 무거워요! 릴을 강화하면 낚을 수 있어요 (지금 {_model.ReelCapacity:0.#}kg)", 2.4f);

    private string PhaseHint()
    {
        switch (_model.Phase)
        {
            case FishingPhase.Ready: return _game.AutoFishing ? "해달이 알아서 낚는 중… 누르면 직접 낚아요" : "화면을 눌러 던지기";
            case FishingPhase.Casting: return "";
            case FishingPhase.InWater:
                if (_game.AutoFishing) return "해달이 알아서 낚는 중… 누르면 직접 낚아요";
                return _model.Holding ? "올라오는 중… 떼면 가라앉아요" : "누르고 있으면 올라와요";
            default: return "걸렸다! 누르면 더 빨리 올라와요";
        }
    }

    private void Update()
    {
        if (_model == null)
            return;
        float dt = Time.unscaledDeltaTime;

        // 골드 숫자가 따라 올라감
        _goldShown = Mathf.MoveTowards(_goldShown, _model.Gold, Mathf.Max(30f, Mathf.Abs(_model.Gold - _goldShown) * 6f) * dt);
        _gold.text = Money(Mathf.RoundToInt(_goldShown));
        _goldPunch = Mathf.MoveTowards(_goldPunch, 0f, dt * 3f);
        _goldPill.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(_goldPunch * Mathf.PI));
        _bagPunch = Mathf.MoveTowards(_bagPunch, 0f, dt * 3f);
        _bagButton.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(_bagPunch * Mathf.PI));

        if (_hintTimer > 0f)
            _hintTimer -= dt;
        _hint.text = _hintTimer > 0f && _hintOverride != null ? _hintOverride : PhaseHint();
        _hint.alpha = _model.Phase == FishingPhase.Ready && _hintTimer <= 0f ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f) : 1f;

        bool out_ = _model.Phase == FishingPhase.InWater || _model.Phase == FishingPhase.Hooked;
        _info.gameObject.SetActive(out_);
        if (out_)
            _infoText.text = $"거리 {_model.HookDistance:0}m · 깊이 {_model.HookDepth:0.0}m";

        var request = _model.Request;
        if (request != null && !request.Done)
            RefreshRequestButton();
        _requestPulse = Mathf.MoveTowards(_requestPulse, 0f, dt * 1.6f);
        float wobble = request != null && request.Done ? 0.03f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
        _request.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(_requestPulse * Mathf.PI * 2f) * _requestPulse + wobble);

        if (_toastTimer > 0f)
        {
            _toastTimer -= dt;
            float t = _toastTimer;
            float scale = t > 1.9f ? Mathf.Lerp(1f, 0.6f, (t - 1.9f) / 0.3f) * (1f + 0.1f * Mathf.Sin((2.2f - t) / 0.3f * Mathf.PI)) : t < 0.3f ? t / 0.3f : 1f;
            _toast.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            if (_toastTimer <= 0f)
                _toast.gameObject.SetActive(false);
        }

        if (_revealRays != null)
            _revealRays.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 20f);
    }

    /// <summary>낚은 물고기 알림 (처음이면 발견 화면)</summary>
    public void ShowCatch(FishingCatch result)
    {
        _goldPunch = 1f;
        _toastIcon.sprite = result.Def.Icon;
        _toastName.text = result.Def.DisplayName;
        _toastName.color = Cocoa;
        _toastDetail.text = $"{result.Weight:0.##}kg · {FishingReworkArt.RarityName(result.Def.Rarity)}";
        _toastGold.text = $"값 {result.Price}";
        _bagPunch = 1f;
        string tag = result.IsNew ? "새 물고기!" : result.IsRecord ? "기록!" : result.IsBig ? "대물!" : null;
        _toastTag.transform.parent.gameObject.SetActive(tag != null);
        if (tag != null)
            _toastTag.text = tag;
        _toast.gameObject.SetActive(true);
        _toastTimer = 2.2f;
        if (result.IsNew)
        {
            _bookNew = true;
            _bookBadge.SetActive(true);
            OpenReveal(result);
        }
    }

    #endregion

    #region 창

    private RectTransform OpenSheet(string title, float width = 940f)
    {
        CloseSheet();
        var dim = RuntimeUIKit.CreateDim(_root, "Sheet");
        _sheet = dim.gameObject;
        MakeButton(dim, CloseSheet, false);
        var panel = RuntimeUIKit.CreatePanel(dim, width);
        panel.gameObject.AddComponent<Button>();  // 패널을 눌러도 닫히지 않게 (위로 안 감)
        var heading = RuntimeUIKit.CreateLabel(panel, title, RuntimeUIKit.TitleFontSize, true);
        heading.color = Cocoa;
        _sfx.Play(MineSfx.Kind.Open);
        return panel;
    }

    private void CloseSheet()
    {
        if (_sheet != null)
            Destroy(_sheet);
        _sheet = null;
    }

    private void AddCloseButton(RectTransform panel)
    {
        RuntimeUIKit.CreateSpace(panel, 4f);
        RuntimeUIKit.CreateButton(panel, "닫기", CloseSheet, RuntimeUIKit.ButtonKind.Secondary);
    }

    private void OpenUpgrades()
    {
        var panel = OpenSheet("낚시 도구 강화");
        foreach (FishingUpgrade upgrade in Enum.GetValues(typeof(FishingUpgrade)))
            UpgradeRow(panel, upgrade);
        var note = RuntimeUIKit.CreateLabel(panel, "줄이 길면 더 멀리 · 찌가 무거우면 더 깊이 · 릴이 튼튼하면 무거운 물고기", 28);
        note.color = Muted;
        note.textWrappingMode = TextWrappingModes.Normal;
        AddCloseButton(panel);
    }

    private void UpgradeRow(RectTransform panel, FishingUpgrade upgrade)
    {
        var track = _model.Config.Track(upgrade);
        int level = _model.Level(upgrade);
        bool max = _model.IsMax(upgrade);

        var row = RuntimeUIKit.CreateRow(panel, 170f, 18f);
        var icon = RuntimeUIKit.CreateIcon(row, track.Icon, 130f);
        icon.GetComponent<LayoutElement>().minHeight = 130f;

        var texts = NewRect(row, "Texts");
        texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var name = NewText(texts, $"{track.DisplayName}  Lv.{level + 1}", 38, true, Cocoa, TextAlignmentOptions.Left);
        Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 4f), new Vector2(420f, 52f));
        string now = $"{track.ValueAt(level):0.#}{track.Unit}";
        string effect = max ? $"{track.EffectName} {now}" : $"{track.EffectName} {now} → {track.ValueAt(level + 1):0.#}{track.Unit}";
        var line = NewText(texts, effect, 28, false, RuntimeUIKit.Style.BodyColor, TextAlignmentOptions.Left);
        Place(line.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(0f, -4f), new Vector2(420f, 44f));
        line.enableAutoSizing = true;
        line.fontSizeMin = 20f;
        line.fontSizeMax = 28f;

        int cost = _model.NextCost(upgrade);
        bool can = _model.CanUpgrade(upgrade);
        var button = RuntimeUIKit.CreateButton(row, max ? "최대" : Money(cost), () =>
        {
            if (_model.TryUpgrade(upgrade))
            {
                _goldPunch = 1f;
                OpenUpgrades();
            }
            else
                _sfx.Play(MineSfx.Kind.Tap, 0.6f);
        }, can ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Option, 250f);
        button.interactable = !max;
        if (!max)
        {
            var coin = NewImage(button.transform, "Coin", _ui.Gold, Color.white, true);
            Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 6f), new Vector2(50f, 50f));
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            ((RectTransform)label.transform).offsetMin = new Vector2(60f, 12f);
            if (!can)
                label.color = Muted;
        }
    }

    private void OpenBook()
    {
        _bookNew = false;
        _bookBadge.SetActive(false);
        var panel = OpenSheet($"물고기 도감  {_model.SpeciesCaught}/{_model.Config.Fish.Count}", 980f);

        var scroll = NewRect(panel, "Scroll");
        var element = scroll.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 1150f;
        element.minHeight = 1150f;
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        var viewport = NewRect(scroll, "Viewport");
        Fill(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
        var content = NewRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport;
        scrollRect.content = content;

        foreach (var def in _model.Config.Fish)
        {
            if (def != null)
                BookRow(content, def);
        }
        AddCloseButton(panel);
    }

    private void BookRow(RectTransform content, FishDefinition def)
    {
        int count = _model.CaughtCount(def);
        bool known = count > 0;
        var back = NewImage(content, "Row", _ui.BoxInset, new Color(1f, 0.98f, 0.94f, 1f));
        var element = back.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 150f;
        element.minHeight = 150f;
        var rt = back.rectTransform;

        var icon = NewImage(rt, "Icon", def.Icon, known ? Color.white : new Color(0.1f, 0.12f, 0.2f, 0.55f), true);
        Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(180f, 124f));

        var name = NewText(rt, known ? def.DisplayName : "???", 36, true, Cocoa, TextAlignmentOptions.Left);
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(208f, -14f), new Vector2(330f, 50f));
        var rarity = NewText(rt, FishingReworkArt.RarityName(def.Rarity), 26, true, FishingReworkArt.RarityColor(def.Rarity), TextAlignmentOptions.Left, 0.18f);
        Place(rarity.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(530f, -20f), new Vector2(120f, 40f));

        string where = def.NearBottom
            ? $"거리 {def.MinDistance:0}~{def.MaxDistance:0}m · 바닥"
            : $"거리 {def.MinDistance:0}~{def.MaxDistance:0}m · 깊이 {def.MinDepth:0}~{def.MaxDepth:0}m";
        var info = NewText(rt, where, 26, false, RuntimeUIKit.Style.BodyColor, TextAlignmentOptions.Left);
        Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(208f, -68f), new Vector2(480f, 36f));
        var weight = NewText(rt, $"{def.MinWeight:0.##}~{def.MaxWeight:0.#}kg · 값 {def.Price}", 26, false, Muted, TextAlignmentOptions.Left);
        Place(weight.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(208f, -104f), new Vector2(480f, 36f));

        var right = NewText(rt, known ? $"{count}마리\n가장 큰 것 {_model.BestWeight(def):0.##}kg" : "아직 못 낚았어요", 26, true,
            known ? Cocoa : Muted, TextAlignmentOptions.Right);
        Place(right.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(250f, 100f));
        if (!_model.CanReach(def))
        {
            var lockText = NewText(rt, def.MinWeight > _model.ReelCapacity ? "릴이 모자라요" : "줄이 모자라요", 24, true,
                new Color32(220, 110, 100, 255), TextAlignmentOptions.Right);
            Place(lockText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 10f), new Vector2(250f, 34f));
        }
    }

    private void OpenBag()
    {
        var panel = OpenSheet($"가방  {_model.Bag.Count}마리", 980f);
        if (_model.Bag.Count == 0)
        {
            var empty = RuntimeUIKit.CreateLabel(panel, "아직 비었어요. 물고기를 낚아 봐요!", 32);
            empty.color = Muted;
            AddCloseButton(panel);
            return;
        }

        var scroll = NewRect(panel, "Scroll");
        var element = scroll.gameObject.AddComponent<LayoutElement>();
        float height = Mathf.Min(1050f, Mathf.Ceil(_model.Bag.Count / 4f) * 250f);
        element.preferredHeight = height;
        element.minHeight = height;
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        var viewport = NewRect(scroll, "Viewport");
        Fill(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.001f);
        var content = NewRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200f, 238f);
        grid.spacing = new Vector2(12f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperCenter;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport;
        scrollRect.content = content;

        // 비싼 것부터
        var bag = new System.Collections.Generic.List<FishingBagSave>(_model.Bag);
        bag.Sort((a, b) => b.price.CompareTo(a.price));
        foreach (var fish in bag)
            BagCell(content, fish);

        var hint = RuntimeUIKit.CreateLabel(panel, "물고기를 누르면 한 마리씩 팔아요", 28);
        hint.color = Muted;
        RuntimeUIKit.CreateButton(panel, $"전부 팔기  {Money(_model.BagValue)}골드", () =>
        {
            int earned = _model.SellAll();
            if (earned <= 0)
                return;
            _sfx.Play(MineSfx.Kind.Sell);
            _goldPunch = 1f;
            ShowMessage($"+{Money(earned)} 골드");
            CloseSheet();
        }, RuntimeUIKit.ButtonKind.Primary);
        AddCloseButton(panel);
    }

    private void BagCell(RectTransform content, FishingBagSave fish)
    {
        var def = _model.Config.FindFish(fish.fishId);
        var cell = NewImage(content, "Fish", _ui.BoxInset, new Color(1f, 0.98f, 0.94f, 1f));
        if (def != null && def.Rarity >= FishRarity.Rare)
            cell.color = Color.Lerp(Color.white, FishingReworkArt.RarityColor(def.Rarity), 0.25f);
        var rt = cell.rectTransform;
        MakeButton(rt, () =>
        {
            int earned = _model.Sell(fish);
            if (earned <= 0)
                return;
            _sfx.Play(MineSfx.Kind.Sell);
            _goldPunch = 1f;
            OpenBag();
        }, false);
        var icon = NewImage(rt, "Icon", def != null ? def.Icon : null, Color.white, true);
        Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(180f, 120f));
        var weight = NewText(rt, $"{fish.weight:0.##}kg", 26, false, RuntimeUIKit.Style.BodyColor);
        Place(weight.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(190f, 36f));
        var coin = NewImage(rt, "Coin", _ui.Gold, Color.white, true);
        Place(coin.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 14f), new Vector2(44f, 44f));
        var price = NewText(rt, Money(fish.price), 32, true, Cocoa, TextAlignmentOptions.Left);
        Place(price.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(-30f, 16f), new Vector2(110f, 44f));
    }

    private void OpenTest()
    {
        var panel = OpenSheet("테스트");
        RuntimeUIKit.CreateButton(panel, "골드 +5,000", () => { _model.CheatGold(5000); _goldPunch = 1f; });
        RuntimeUIKit.CreateButton(panel, "강화 모두 최대", () => { _model.CheatMaxUpgrades(); CloseSheet(); });
        RuntimeUIKit.CreateButton(panel, "의뢰 새로", () => { _model.NewRequest(_model.Request); CloseSheet(); });
        RuntimeUIKit.CreateButton(panel, "처음부터", () => { CloseSheet(); _game.ResetAll(); }, RuntimeUIKit.ButtonKind.Secondary);
        AddCloseButton(panel);
    }

    private void OpenReveal(FishingCatch result)
    {
        if (_reveal != null)
            Destroy(_reveal);
        var dim = RuntimeUIKit.CreateDim(_root, "Reveal");
        _reveal = dim.gameObject;
        MakeButton(dim, () =>
        {
            Destroy(_reveal);
            _reveal = null;
            _revealRays = null;
        });
        var rays = NewImage(dim, "Rays", _ui.Rays, new Color(1f, 0.95f, 0.7f, 0.55f), true);
        _revealRays = rays.rectTransform;
        Place(_revealRays, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(900f, 900f));
        var title = NewText(dim, "새 물고기 발견!", 64, true, Color.white, TextAlignmentOptions.Center, 0.25f);
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 560f), new Vector2(1000f, 90f));
        var icon = NewImage(dim, "Icon", result.Def.Icon, Color.white, true);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(620f, 420f));
        var card = NewImage(dim, "Card", _ui.Paper, Color.white);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(860f, 330f));
        var name = NewText(card.transform, result.Def.DisplayName, 56, true, Cocoa);
        Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(800f, 70f));
        var rarity = NewText(card.transform, $"{FishingReworkArt.RarityName(result.Def.Rarity)} · {result.Weight:0.##}kg", 32, true,
            FishingReworkArt.RarityColor(result.Def.Rarity), TextAlignmentOptions.Center, 0.18f);
        Place(rarity.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(800f, 44f));
        var desc = NewText(card.transform, result.Def.Description, 30, false, RuntimeUIKit.Style.BodyColor);
        Place(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -164f), new Vector2(760f, 130f));
        desc.textWrappingMode = TextWrappingModes.Normal;
        var tap = NewText(dim, "눌러서 닫기", 30, true, new Color(1f, 1f, 1f, 0.8f));
        Place(tap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(600f, 50f));
    }

    #endregion
}
