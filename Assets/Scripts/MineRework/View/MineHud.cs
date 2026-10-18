using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 광산 개편 테스트 씬의 화면 (코드로 만듦, 게임의 스티커 UI 부품을 그대로 씀).
/// 위: 층 버튼 · 골드 · 이 층 광석 띠 · 곡괭이. 가운데: 광맥 대박 띠 · 콤보 · 막힌 길 진행. 아래: 작업대 · 층 이동 · 도감.
/// 광석이 띠로 날아와 숫자가 오르고, 창(작업대 · 층 · 도감 · 발견 · 완성 · 꺼 둔 동안 · 테스트)은 MineHud.Sheets.cs
/// </summary>
public partial class MineHud : MonoBehaviour
{
    private static readonly Color Cocoa = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    private static readonly Color Cream = new Color32(0xFF, 0xF8, 0xEC, 0xFF);
    private static readonly Color Good = new Color32(0x5E, 0x9E, 0x4A, 0xFF);
    private static readonly Color Bad = new Color32(0xD9, 0x4F, 0x45, 0xFF);
    private static readonly Color Gold = new Color32(0xF2, 0xB8, 0x3A, 0xFF);

    private MineReworkGame _game;
    private MineReworkModel _model;
    private MineReworkArt _art;
    private MineSfx _sfx;
    private Canvas _canvas;
    private RectTransform _root;
    private RectTransform _safe;

    // 위
    private TextMeshProUGUI _layerNumber;
    private TextMeshProUGUI _layerName;
    private TextMeshProUGUI _gold;
    private RectTransform _goldPill;
    private RectTransform _strip;
    private Image _pickIcon;
    private TextMeshProUGUI _pickName;
    private TextMeshProUGUI _pickStats;
    private readonly List<OreChip> _chips = new List<OreChip>();
    // 가운데
    private RectTransform _jackpot;
    private Image _jackpotFill;
    private TextMeshProUGUI _jackpotText;
    private TextMeshProUGUI _combo;
    private float _comboAge = 99f;
    private int _comboShown;
    private RectTransform _boulderPanel;
    private RectTransform _boulderBack;
    private Image _boulderFill;
    private TextMeshProUGUI _boulderText;
    private TextMeshProUGUI _respawn;
    private RectTransform _minerBubble;
    private TextMeshProUGUI _minerBubbleText;
    private RectTransform _toast;
    private TextMeshProUGUI _toastText;
    private float _toastAge = 99f;
    private RectTransform _title;
    private TextMeshProUGUI _titleNumber;
    private TextMeshProUGUI _titleName;
    private TextMeshProUGUI _titleSub;
    private float _titleAge = 99f;
    private Image _flash;
    private float _flashAge = 99f;
    private Color _flashColor;
    // 아래
    private RectTransform _benchButton;
    private RectTransform _layersButton;
    private RectTransform _bookButton;
    private GameObject _benchBadge;
    private GameObject _bookBadge;

    // 날아가는 중인 광석 (띠 숫자는 도착할 때 오름)
    private readonly Dictionary<MineOreDefinition, int> _pending = new Dictionary<MineOreDefinition, int>();
    private readonly List<Flyer> _flyers = new List<Flyer>();
    private readonly Stack<Image> _flyerPool = new Stack<Image>();
    private int _goldShown;
    private int _goldTarget;
    private bool _newInBook;

    private class OreChip
    {
        public MineOreDefinition Ore;
        public RectTransform Root;
        public Image Icon;
        public TextMeshProUGUI Count;
        public float Punch = 99f;
    }

    private class Flyer
    {
        public Image Image;
        public Vector2 From;
        public Vector2 Control;
        public RectTransform Target;
        public float Age;
        public float Life;
        public float Size;
        public Action Arrived;
    }

    public RectTransform Root => _root;

    public void Setup(MineReworkGame game, MineReworkModel model, MineReworkArt art, MineSfx sfx)
    {
        _game = game;
        _model = model;
        _art = art;
        _sfx = sfx;
        _canvas = RuntimeUIKit.CreateCanvas("MineHud", 100, false);
        _canvas.transform.SetParent(transform, false);
        _root = (RectTransform)_canvas.transform;
        _safe = NewRect(_root, "SafeArea");
        RuntimeUIKit.Stretch(_safe);
        _safe.gameObject.AddComponent<SafeAreaFltter>();

        BuildTop();
        BuildMiddle();
        BuildBottom();
        BuildSheets();
        _flash = NewImage(_root, "Flash", null, new Color(1f, 1f, 1f, 0f));
        RuntimeUIKit.Stretch(_flash.rectTransform);
        _flash.raycastTarget = false;

        _goldShown = _goldTarget = model.Gold;
        RefreshLayer();
        Refresh();
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

    /// <summary>누르면 쏙 들어갔다 나오는 버튼</summary>
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

    private GameObject NewBadge(RectTransform parent, Vector2 position)
    {
        var badge = NewImage(parent, "Badge", _art.Badge, Color.white);
        Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), position, new Vector2(56f, 56f));
        var mark = NewText(badge.transform, "!", 38, true, Color.white);
        Fill(mark.rectTransform, 0f, 4f);
        badge.gameObject.SetActive(false);
        return badge.gameObject;
    }

    #endregion

    #region 위

    private void BuildTop()
    {
        // 층 버튼 (왼쪽 위)
        var layer = NewImage(_safe, "LayerButton", RuntimeUIKit.Style.OptionButton, Color.white);
        Place(layer.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(560f, 128f));
        MakeButton(layer.rectTransform, OpenLayers);
        var icon = NewImage(layer.transform, "Icon", _art.IconLayers, Color.white, true);
        Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 6f), new Vector2(100f, 100f));
        var tag = NewImage(layer.transform, "Tag", _art.RibbonLav, Color.white);
        Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(122f, -14f), new Vector2(96f, 44f));
        _layerNumber = NewText(tag.transform, "1층", 26, true, Color.white, TextAlignmentOptions.Center, 0.15f);
        Fill(_layerNumber.rectTransform, 0f, 2f);
        _layerName = NewText(layer.transform, "조약돌 굴", 42, true, Cocoa, TextAlignmentOptions.Left);
        Place(_layerName.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(124f, 24f), new Vector2(380f, 56f));
        var arrow = NewText(layer.transform, "▼", 26, true, new Color(Cocoa.r, Cocoa.g, Cocoa.b, 0.6f));
        Place(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 4f), new Vector2(40f, 40f));

        // 골드 (오른쪽 위)
        var pill = NewImage(_safe, "Gold", RuntimeUIKit.Style.OptionButton, Color.white);
        _goldPill = pill.rectTransform;
        Place(_goldPill, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(330f, 104f));
        var coin = NewImage(pill.transform, "Coin", _art.Gold, Color.white, true);
        Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46f, 6f), new Vector2(92f, 92f));
        _gold = NewText(pill.transform, "0", 44, true, Cocoa, TextAlignmentOptions.Right);
        Place(_gold.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 6f), new Vector2(220f, 60f));

        // 광석 띠
        var stripBack = NewImage(_safe, "OreStrip", _art.BoxInset, new Color(1f, 0.98f, 0.94f, 0.92f));
        Place(stripBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -164f), new Vector2(1032f, 132f));
        _strip = NewRect(stripBack.transform, "Chips");
        Fill(_strip, 14f, 8f, 14f, 8f);
        var layout = _strip.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // 곡괭이 (띠 아래 왼쪽, 누르면 작업대)
        var pick = NewImage(_safe, "Pickaxe", _art.Paper, new Color(1f, 1f, 1f, 0.95f));
        Place(pick.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -312f), new Vector2(420f, 116f));
        MakeButton(pick.rectTransform, OpenBench);
        _pickIcon = NewImage(pick.transform, "Icon", null, Color.white, true);
        Place(_pickIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 4f), new Vector2(100f, 100f));
        _pickName = NewText(pick.transform, "", 32, true, Cocoa, TextAlignmentOptions.Left);
        Place(_pickName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(116f, 4f), new Vector2(290f, 44f));
        _pickStats = NewText(pick.transform, "", 24, false, RuntimeUIKit.Style.BodyColor, TextAlignmentOptions.Left);
        Place(_pickStats.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(116f, 2f), new Vector2(290f, 36f));
    }

    private void RebuildStrip()
    {
        foreach (var chip in _chips)
            Destroy(chip.Root.gameObject);
        _chips.Clear();

        var ores = new List<MineOreDefinition>();
        var layer = _model.Layer;
        foreach (var ore in _model.Config.Ores)
        {
            if (ore == null || ore.Rarity == MineOreRarity.Relic)
                continue;
            bool inLayer = false;
            foreach (var drop in layer.Veins)
                inLayer |= drop != null && drop.Ore == ore;
            foreach (var drop in layer.RareDrops)
                inLayer |= drop != null && drop.Ore == ore;
            if (inLayer)
                ores.Add(ore);
        }
        float width = Mathf.Min(170f, (1000f - 8f * (ores.Count - 1)) / Mathf.Max(1, ores.Count));
        foreach (var ore in ores)
        {
            var root = NewImage(_strip, "Chip_" + ore.OreId, _art.ChipNormal, new Color(1f, 1f, 1f, 0.9f));
            root.rectTransform.sizeDelta = new Vector2(width, 112f);
            var rt = root.rectTransform;
            MakeButton(rt, () => OpenDetail(ore), true);
            var icon = NewImage(rt, "Icon", ore.Icon, Color.white, true);
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-width * 0.2f, 6f), new Vector2(80f, 80f));
            var count = NewText(rt, "0", 34, true, Cocoa, TextAlignmentOptions.Left);
            Place(count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(width * 0.05f, 4f), new Vector2(width * 0.5f, 50f));
            count.enableAutoSizing = true;
            count.fontSizeMin = 20f;
            count.fontSizeMax = 34f;
            _chips.Add(new OreChip { Ore = ore, Root = rt, Icon = icon, Count = count });
        }
    }

    #endregion

    #region 가운데

    private void BuildMiddle()
    {
        // 광맥 대박 띠
        var jackpot = NewImage(_safe, "Jackpot", _art.RibbonPeach, Color.white);
        _jackpot = jackpot.rectTransform;
        Place(_jackpot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -446f), new Vector2(560f, 110f));
        _jackpotText = NewText(_jackpot, "광맥 대박!", 46, true, Color.white, TextAlignmentOptions.Center, 0.2f);
        Fill(_jackpotText.rectTransform, 0f, 26f);
        var track = NewImage(_jackpot, "Track", _art.BoxInset, new Color(0.3f, 0.18f, 0.12f, 0.6f));
        Place(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(400f, 24f));
        _jackpotFill = NewImage(track.transform, "Fill", _art.BarFill, Gold);
        _jackpotFill.type = Image.Type.Filled;
        _jackpotFill.fillMethod = Image.FillMethod.Horizontal;
        Fill(_jackpotFill.rectTransform, 3f, 3f, 3f, 3f);
        _jackpot.gameObject.SetActive(false);

        // 콤보 (오른쪽)
        _combo = NewText(_safe, "", 60, true, Color.white, TextAlignmentOptions.Right, 0.22f);
        Place(_combo.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-34f, -600f), new Vector2(400f, 120f));

        // 막힌 길 진행
        var boulder = NewImage(_safe, "Boulder", RuntimeUIKit.Style.Panel, Color.white);
        _boulderPanel = boulder.rectTransform;
        Place(_boulderPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(760f, 190f));
        _boulderText = NewText(_boulderPanel, "", 40, true, Cocoa);
        Place(_boulderText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(700f, 56f));
        var btrack = NewImage(_boulderPanel, "Track", _art.BoxInset, new Color(0.3f, 0.18f, 0.12f, 0.35f));
        Place(btrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(620f, 40f));
        _boulderFill = NewImage(btrack.transform, "Fill", _art.BarFill, new Color32(0xE8, 0x8E, 0x5A, 0xFF));
        _boulderFill.type = Image.Type.Filled;
        _boulderFill.fillMethod = Image.FillMethod.Horizontal;
        Fill(_boulderFill.rectTransform, 4f, 4f, 4f, 4f);
        var back = NewImage(_safe, "BoulderBack", RuntimeUIKit.Style.OptionButton, Color.white);
        _boulderBack = back.rectTransform;
        Place(_boulderBack, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 290f), new Vector2(320f, 104f));
        var backText = NewText(back.transform, "돌아가기", 38, true, Cocoa);
        Fill(backText.rectTransform, 0f, 10f);
        MakeButton(_boulderBack, () => _game.LeaveBoulder());
        _boulderBack.gameObject.SetActive(false);
        _boulderPanel.gameObject.SetActive(false);

        // 다음 바위까지 (아래 버튼 위 작은 칸)
        var pill = NewImage(_safe, "Respawn", _art.Paper, new Color(1f, 1f, 1f, 0.92f));
        Place(pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 276f), new Vector2(520f, 70f));
        _respawn = NewText(pill.transform, "", 30, true, Cocoa);
        Fill(_respawn.rectTransform, 12f, 8f, 12f, 0f);

        // 광부 말풍선 (월드 위치를 따라감)
        var bubble = NewImage(_root, "MinerBubble", RuntimeUIKit.Style.Bubble != null ? RuntimeUIKit.Style.Bubble : RuntimeUIKit.Style.OptionButton, Color.white);
        _minerBubble = bubble.rectTransform;
        Place(_minerBubble, new Vector2(0f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(230f, 84f));
        _minerBubbleText = NewText(_minerBubble, "", 30, true, Cocoa);
        Fill(_minerBubbleText.rectTransform, 6f, 14f, 6f, 0f);

        // 알림 한 줄
        var toast = NewImage(_safe, "Toast", RuntimeUIKit.Style.OptionButton, Color.white);
        _toast = toast.rectTransform;
        Place(_toast, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -560f), new Vector2(760f, 100f));
        _toastText = NewText(_toast, "", 34, true, Cocoa);
        Fill(_toastText.rectTransform, 20f, 12f, 20f, 0f);
        _toast.gameObject.SetActive(false);

        // 층 제목 카드
        _title = NewRect(_safe, "LayerTitle");
        Place(_title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(900f, 300f));
        var ribbon = NewImage(_title, "Ribbon", _art.RibbonLav, Color.white);
        Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(220f, 80f));
        _titleNumber = NewText(ribbon.transform, "", 40, true, Color.white, TextAlignmentOptions.Center, 0.18f);
        Fill(_titleNumber.rectTransform, 0f, 4f);
        _titleName = NewText(_title, "", 92, true, Color.white, TextAlignmentOptions.Center, 0.22f);
        Place(_titleName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(900f, 120f));
        _titleSub = NewText(_title, "", 38, true, Color.white, TextAlignmentOptions.Center, 0.22f);
        Place(_titleSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -206f), new Vector2(900f, 60f));
        _title.gameObject.SetActive(false);
    }

    #endregion

    #region 아래

    private void BuildBottom()
    {
        _benchButton = BottomButton("작업대", _art.IconWorkbench, -330f, OpenBench, out _benchBadge);
        _layersButton = BottomButton("층 이동", _art.IconLayers, 0f, OpenLayers, out _);
        _bookButton = BottomButton("광석 도감", _art.IconCollection, 330f, OpenBook, out _bookBadge);

        var test = NewImage(_safe, "Test", RuntimeUIKit.Style.OptionButton, new Color(1f, 1f, 1f, 0.75f));
        Place(test.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(130f, 70f));
        var testLabel = NewText(test.transform, "테스트", 26, true, Cocoa);
        Fill(testLabel.rectTransform, 0f, 8f);
        MakeButton(test.rectTransform, OpenDebug);
    }

    private RectTransform BottomButton(string label, Sprite icon, float x, Action onClick, out GameObject badge)
    {
        var root = NewRect(_safe, "Button_" + label);
        Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 40f), new Vector2(230f, 220f));
        var circle = NewImage(root, "Circle", _art.RoundFeatured, Color.white, true);
        Place(circle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(176f, 176f));
        var image = NewImage(circle.transform, "Icon", icon, Color.white, true);
        Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(118f, 118f));
        var text = NewText(root, label, 36, true, Color.white, TextAlignmentOptions.Center, 0.24f);
        Place(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(240f, 52f));
        MakeButton(root, onClick);
        badge = NewBadge(circle.rectTransform, new Vector2(-18f, -18f));
        return root;
    }

    #endregion

    #region 갱신

    /// <summary>층이 바뀜 (이름 · 광석 띠)</summary>
    public void RefreshLayer()
    {
        int index = _model.LayerIndex;
        var layer = _model.Layer;
        _layerNumber.text = $"{index + 1}층";
        _layerName.text = layer.DisplayName;
        RebuildStrip();
        Refresh();
    }

    /// <summary>골드 · 광석 · 곡괭이 · 배지</summary>
    public void Refresh()
    {
        _goldTarget = _model.Gold;
        foreach (var chip in _chips)
        {
            bool known = _model.IsDiscovered(chip.Ore);
            chip.Icon.color = known ? Color.white : new Color(0.2f, 0.12f, 0.1f, 0.35f);
            _pending.TryGetValue(chip.Ore, out int pending);
            chip.Count.text = known ? Mathf.Max(0, _model.Count(chip.Ore) - pending).ToString() : "?";
        }
        var pickaxe = _model.Pickaxe;
        _pickIcon.sprite = pickaxe.Icon;
        _pickName.text = _model.Sharpen > 0 ? $"{pickaxe.DisplayName} +{_model.Sharpen}" : pickaxe.DisplayName;
        _pickStats.text = $"힘 {pickaxe.Power} · 치명타 {Mathf.RoundToInt(_model.CritChance * 100f)}%";
        _benchBadge.SetActive(_model.CanCraftNext());
        _bookBadge.SetActive(_newInBook);
        if (_sheetOpen == Sheet.Bench)
            FillBench();
        else if (_sheetOpen == Sheet.Book)
            FillBook();
        else if (_sheetOpen == Sheet.Layers)
            FillLayers();
    }

    public void MarkNewInBook()
    {
        _newInBook = true;
        _bookBadge.SetActive(true);
    }

    #endregion

    #region 광석이 띠로 날아옴

    /// <summary>월드 위치에서 그 광석 칸(없으면 도감 버튼)으로 날아감. 도착하면 숫자가 오름</summary>
    public void FlyOre(Vector3 world, MineOreDefinition ore, int amount)
    {
        _pending.TryGetValue(ore, out int pending);
        _pending[ore] = pending + amount;
        Refresh();
        var chip = _chips.Find(c => c.Ore == ore);
        var target = chip != null ? chip.Root : _bookButton;
        Fly(world, ore.Icon, target, 92f, () =>
        {
            chip = _chips.Find(c => c.Ore == ore);
            _pending.TryGetValue(ore, out int left);
            left -= amount;
            if (left <= 0)
                _pending.Remove(ore);
            else
                _pending[ore] = left;
            if (chip != null)
                chip.Punch = 0f;
            else
                PunchRect(_bookButton);
            _sfx.Play(MineSfx.Kind.Collect, 1f + UnityEngine.Random.Range(-0.05f, 0.12f), 0.6f, 0.04f);
            Refresh();
        });
    }

    /// <summary>골드가 골드 칸으로 날아감 (팔기)</summary>
    public void FlyGold(Vector2 screen, int coins)
    {
        for (int i = 0; i < coins; i++)
        {
            var from = screen + UnityEngine.Random.insideUnitCircle * 60f;
            FlyScreen(from, _art.Gold, _goldPill, 70f, 0.5f + i * 0.05f, () =>
            {
                PunchRect(_goldPill);
                _sfx.Play(MineSfx.Kind.Collect, 1.2f, 0.5f, 0.04f);
            });
        }
    }

    private void Fly(Vector3 world, Sprite sprite, RectTransform target, float size, Action arrived)
    {
        var camera = Camera.main;
        Vector2 screen = camera != null ? (Vector2)camera.WorldToScreenPoint(world) : Vector2.zero;
        FlyScreen(screen, sprite, target, size, 0.55f, arrived);
    }

    private void FlyScreen(Vector2 screen, Sprite sprite, RectTransform target, float size, float life, Action arrived)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var from);
        var image = _flyerPool.Count > 0 ? _flyerPool.Pop() : NewImage(_root, "Flyer", null, Color.white, true);
        image.gameObject.SetActive(true);
        image.sprite = sprite;
        image.transform.SetAsLastSibling();
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = from;
        var to = LocalOf(target);
        var control = Vector2.Lerp(from, to, 0.35f) + new Vector2(UnityEngine.Random.Range(-220f, 220f), 260f);
        _flyers.Add(new Flyer { Image = image, From = from, Control = control, Target = target, Life = life, Size = size, Arrived = arrived });
    }

    private Vector2 LocalOf(RectTransform target)
    {
        var world = target.TransformPoint(target.rect.center);
        var screen = RectTransformUtility.WorldToScreenPoint(null, world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
        return local;
    }

    private readonly List<(RectTransform rect, float age)> _punches = new List<(RectTransform, float)>();

    private void PunchRect(RectTransform rect)
    {
        for (int i = 0; i < _punches.Count; i++)
        {
            if (_punches[i].rect == rect)
            {
                _punches[i] = (rect, 0f);
                return;
            }
        }
        _punches.Add((rect, 0f));
    }

    #endregion

    #region 가운데 알림

    /// <summary>콤보 숫자 (3부터)</summary>
    public void ShowCombo(int combo, bool crit)
    {
        if (combo < 3)
            return;
        _comboShown = combo;
        _comboAge = 0f;
        _combo.text = crit ? $"<size=70%>치명타!</size>\n콤보 {combo}" : $"콤보 {combo}";
        _combo.color = combo >= 20 ? new Color(1f, 0.55f, 0.3f) : combo >= 10 ? new Color(1f, 0.82f, 0.3f) : Color.white;
    }

    public void ShowToast(string text)
    {
        _toastText.text = text;
        _toast.gameObject.SetActive(true);
        _toastAge = 0f;
    }

    /// <summary>층 이름 카드 (가운데 위에 잠깐)</summary>
    public void ShowLayerTitle(int index, MineLayerDefinition layer)
    {
        _titleNumber.text = $"{index + 1}층";
        _titleName.text = layer.DisplayName;
        _titleSub.text = layer.Subtitle;
        _title.gameObject.SetActive(true);
        _titleAge = 0f;
    }

    /// <summary>화면 전체가 잠깐 번쩍</summary>
    public void Flash(Color color)
    {
        _flashColor = color;
        _flashAge = 0f;
    }

    public void SetJackpot(bool on)
    {
        _jackpot.gameObject.SetActive(on);
        if (on)
            PunchRect(_jackpot);
    }

    /// <summary>막힌 길 화면 (null이면 숨김)</summary>
    public void SetBoulder(MineLayerDefinition layer, int index, int left, int total)
    {
        bool on = layer != null;
        _boulderPanel.gameObject.SetActive(on);
        _boulderBack.gameObject.SetActive(on);
        _respawn.transform.parent.gameObject.SetActive(!on);
        if (!on)
            return;
        _boulderText.text = left > 0 ? $"{index + 1}층 {layer.DisplayName} 가는 길을 깨요!  {total - left}/{total}" : "길이 열렸어요!";
        _boulderFill.fillAmount = 1f - left / (float)total;
    }

    #endregion

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // 골드 숫자 굴리기
        if (_goldShown != _goldTarget)
        {
            int step = Mathf.Max(1, Mathf.Abs(_goldTarget - _goldShown) / 8);
            _goldShown = (int)Mathf.MoveTowards(_goldShown, _goldTarget, step);
        }
        _gold.text = _goldShown.ToString("N0");

        // 날아가는 것
        for (int i = _flyers.Count - 1; i >= 0; i--)
        {
            var f = _flyers[i];
            f.Age += dt;
            float k = Mathf.Clamp01(f.Age / f.Life);
            float e = k * k * (3f - 2f * k);
            // 날아가는 사이 층을 옮겨 광석 칸이 새로 만들어졌으면 그 광석 칸(없으면 도감 버튼)으로
            if (f.Target == null)
                f.Target = _bookButton;
            var to = LocalOf(f.Target);
            var a = Vector2.Lerp(f.From, f.Control, e);
            var b = Vector2.Lerp(f.Control, to, e);
            var rect = f.Image.rectTransform;
            rect.anchoredPosition = Vector2.Lerp(a, b, e);
            float s = k < 0.15f ? Mathf.Lerp(0.6f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 0.7f, (k - 0.15f) / 0.85f);
            rect.sizeDelta = Vector2.one * f.Size * s;
            if (k >= 1f)
            {
                f.Image.gameObject.SetActive(false);
                _flyerPool.Push(f.Image);
                _flyers.RemoveAt(i);
                f.Arrived?.Invoke();
            }
        }

        // 칸 · 버튼 통통
        foreach (var chip in _chips)
        {
            if (chip.Punch > 1f)
                continue;
            chip.Punch += dt * 4f;
            float s = 1f + Mathf.Sin(Mathf.Clamp01(chip.Punch) * Mathf.PI) * 0.18f;
            chip.Icon.rectTransform.localScale = Vector3.one * s;
        }
        for (int i = _punches.Count - 1; i >= 0; i--)
        {
            var (rect, age) = _punches[i];
            age += dt * 4f;
            float s = 1f + Mathf.Sin(Mathf.Clamp01(age) * Mathf.PI) * 0.12f;
            if (rect != null)
                rect.localScale = Vector3.one * s;
            if (age >= 1f)
            {
                if (rect != null)
                    rect.localScale = Vector3.one;
                _punches.RemoveAt(i);
            }
            else
            {
                _punches[i] = (rect, age);
            }
        }

        // 콤보
        _comboAge += dt;
        float comboPop = _comboAge < 0.12f ? Mathf.Lerp(1.4f, 1f, _comboAge / 0.12f) : 1f;
        _combo.rectTransform.localScale = Vector3.one * comboPop;
        var cc = _combo.color;
        cc.a = 1f - Mathf.Clamp01((_comboAge - 0.7f) / 0.4f);
        _combo.color = cc;

        // 대박
        if (_model.JackpotActive)
        {
            _jackpotText.text = $"광맥 대박!  {Mathf.CeilToInt(_model.JackpotLeft)}";
            _jackpotFill.fillAmount = _model.JackpotLeft / _model.Config.JackpotSeconds;
            float wob = Mathf.Sin(Time.unscaledTime * 10f) * 2.5f;
            _jackpot.localRotation = Quaternion.Euler(0f, 0f, wob);
        }

        // 다음 바위
        if (_respawn.transform.parent.gameObject.activeSelf)
        {
            int present = 0;
            foreach (var rock in _model.Rocks)
                present += rock.Present ? 1 : 0;
            if (present >= _model.Rocks.Count)
            {
                _respawn.text = "바위가 가득해요!";
            }
            else
            {
                float left = (1f - _model.RespawnProgress) * _model.Config.RockRespawnSeconds;
                _respawn.text = $"다음 바위  {Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}   ({present}/{_model.Rocks.Count})";
            }
        }

        // 알림 · 제목 · 번쩍
        if (_toast.gameObject.activeSelf)
        {
            _toastAge += dt;
            _toast.localScale = Vector3.one * (_toastAge < 0.1f ? Mathf.Lerp(0.8f, 1f, _toastAge / 0.1f) : 1f);
            if (_toastAge > 1.8f)
                _toast.gameObject.SetActive(false);
        }
        if (_title.gameObject.activeSelf)
        {
            _titleAge += dt;
            float k = _titleAge;
            float alpha = k < 0.25f ? k / 0.25f : 1f - Mathf.Clamp01((k - 1.9f) / 0.5f);
            _title.localScale = Vector3.one * (k < 0.25f ? Mathf.Lerp(1.3f, 1f, k / 0.25f) : 1f);
            foreach (var g in _title.GetComponentsInChildren<Graphic>())
            {
                var c = g.color;
                c.a = alpha;
                g.color = c;
            }
            if (k > 2.4f)
                _title.gameObject.SetActive(false);
        }
        _flashAge += dt;
        var fc = _flashColor;
        fc.a = _flashColor.a * (1f - Mathf.Clamp01(_flashAge / 0.35f));
        _flash.color = fc;

        UpdateMinerBubble();
        UpdateSheets(dt);
    }

    private void UpdateMinerBubble()
    {
        var camera = Camera.main;
        bool show = camera != null && _game.MinerVisible && _sheetOpen == Sheet.None;
        _minerBubble.gameObject.SetActive(show);
        if (!show)
            return;
        Vector2 screen = camera.WorldToScreenPoint(_game.MinerBubbleWorld);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
        float half = _root.rect.width * 0.5f - _minerBubble.sizeDelta.x * 0.5f - 12f;
        local.x = Mathf.Clamp(local.x, -half, half);
        _minerBubble.anchorMin = _minerBubble.anchorMax = new Vector2(0.5f, 0.5f);
        _minerBubble.anchoredPosition = local;
        if (_model.Cheering)
            _minerBubbleText.text = $"x2  {Mathf.CeilToInt(_model.CheerLeft)}초";
        else if (_model.CheerCooldownLeft > 0f)
            _minerBubbleText.text = $"쉬는 중 {Mathf.CeilToInt(_model.CheerCooldownLeft)}";
        else
            _minerBubbleText.text = "응원하기!";
        float bob = _model.CheerCooldownLeft <= 0f ? Mathf.Sin(Time.unscaledTime * 4f) * 6f : 0f;
        _minerBubble.anchoredPosition += new Vector2(0f, bob);
    }
}

/// <summary>누르면 쏙 들어가는 작은 손맛 (버튼)</summary>
public class MinePress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private float _target = 1f;
    private float _scale = 1f;

    public void OnPointerDown(PointerEventData eventData) => _target = 0.9f;
    public void OnPointerUp(PointerEventData eventData) => _target = 1f;
    public void OnPointerExit(PointerEventData eventData) => _target = 1f;

    private void Update()
    {
        if (Mathf.Approximately(_scale, _target))
            return;
        _scale = Mathf.MoveTowards(_scale, _target, Time.unscaledDeltaTime * 3f);
        transform.localScale = Vector3.one * _scale;
    }
}
