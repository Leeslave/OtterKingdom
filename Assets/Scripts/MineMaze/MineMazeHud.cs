using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 광산 미로 테스트 씬의 화면 (코드로 만듦, 광산 개편 · 게임 스티커 UI 부품).
/// 위: 층 이름 · 나가기 · 이 층 광석 띠 · 탐험 시간. 갱도 아래: 앞 칸 안내, 지도(지도 해달), 앞 · 뒤 · 왼쪽 · 오른쪽 버튼.
/// 창: 갱도 입구(들어가기) · 탐험 결과(나가기) · 테스트. 광석은 띠로 날아와 숫자가 오른다.
/// </summary>
public partial class MineMazeHud : MonoBehaviour
{
    private static readonly Color Cocoa = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    private static readonly Color Cream = new Color32(0xFF, 0xF8, 0xEC, 0xFF);
    private static readonly Color Good = new Color32(0x5E, 0x9E, 0x4A, 0xFF);
    private static readonly Color GoldColor = new Color32(0xF2, 0xB8, 0x3A, 0xFF);

    // 지도 색
    private static readonly Color MapFloor = new Color32(0xF3, 0xE6, 0xCC, 0xFF);
    private static readonly Color MapWall = new Color32(0x5A, 0x3E, 0x30, 0xFF);
    private static readonly Color MapRock = new Color32(0x8C, 0x96, 0x9C, 0xFF);
    private static readonly Color MapRockOpen = new Color32(0xC6, 0xCC, 0xCF, 0xFF);
    private static readonly Color MapChest = GoldColor;
    private static readonly Color MapChestOpen = new Color32(0xB8, 0x9C, 0x7A, 0xFF);
    private static readonly Color MapStairs = new Color32(0x4F, 0x86, 0xC6, 0xFF);
    private static readonly Color MapStart = new Color32(0x6F, 0xB0, 0x5A, 0xFF);

    private const float PadSize = 540f;
    private const float MapSize = 470f;

    private MineMazeGame _game;
    private MineMazeModel _model;
    private MineReworkArt _art;
    private MineMazeArt _mazeArt;
    private MineSfx _sfx;
    private Canvas _canvas;
    private RectTransform _root;
    private RectTransform _safe;

    // 위
    private TextMeshProUGUI _floorName;
    private TextMeshProUGUI _floorTag;
    private TextMeshProUGUI _floorTitleNumber;
    private TextMeshProUGUI _runTime;
    private TextMeshProUGUI _pickaxe;
    private RectTransform _strip;
    private readonly List<OreChip> _chips = new List<OreChip>();
    // 갱도 아래
    private RectTransform _below;
    private TextMeshProUGUI _prompt;
    private RectTransform _pad;
    private readonly Dictionary<MazeTurn, Button> _turnButtons = new Dictionary<MazeTurn, Button>();
    private RectTransform _action;
    private TextMeshProUGUI _actionLabel;
    private Action _actionClick;
    private RectTransform _mapPanel;
    private RectTransform _mapGrid;
    private Image[,] _mapCells;
    private RectTransform _mapPlayer;
    private RectTransform _mapEmpty;
    private TextMeshProUGUI _mapEmptyText;
    private TextMeshProUGUI _mapTitle;
    // 알림
    private TextMeshProUGUI _combo;
    private float _comboAge = 99f;
    private RectTransform _toast;
    private TextMeshProUGUI _toastText;
    private float _toastAge = 99f;
    private RectTransform _title;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _titleSub;
    private float _titleAge = 99f;
    private Image _flash;
    private Color _flashColor;
    private float _flashAge = 99f;
    // 창
    private RectTransform _sheet;
    private RectTransform _exitButton;

    private Vector2 _laidOutFor;
    private readonly Dictionary<MineOreDefinition, int> _pending = new Dictionary<MineOreDefinition, int>();
    private readonly List<Flyer> _flyers = new List<Flyer>();
    private readonly Stack<Image> _flyerPool = new Stack<Image>();

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

    /// <summary>창이 떠 있음 (갱도 탭을 안 받음)</summary>
    public bool Blocking => _sheet != null;

    public void Setup(MineMazeGame game, MineMazeModel model, MineReworkArt art, MineMazeArt mazeArt, MineSfx sfx)
    {
        _game = game;
        _model = model;
        _art = art;
        _mazeArt = mazeArt;
        _sfx = sfx;
        _canvas = RuntimeUIKit.CreateCanvas("MineMazeHud", 100, false);
        _canvas.transform.SetParent(transform, false);
        _root = (RectTransform)_canvas.transform;
        _safe = NewRect(_root, "SafeArea");
        RuntimeUIKit.Stretch(_safe);
        _safe.gameObject.AddComponent<SafeAreaFltter>();

        BuildTop();
        BuildBelow();
        BuildNotices();
        // 테스트 창(저장 지우기 등)은 테스트 씬 · 개발 빌드에서만
        if (!_game.InGame || Debug.isDebugBuild)
            BuildTestButton();
        BuildWorkshopButton();
        _flash = NewImage(_root, "Flash", null, new Color(1f, 1f, 1f, 0f));
        RuntimeUIKit.Stretch(_flash.rectTransform);
        _flash.raycastTarget = false;

        RebuildStrip();
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

    // ▼를 돌려 화살표로 (폰트에 있는 글자)
    private static TextMeshProUGUI Arrow(Transform parent, float size, Color color, float angle)
    {
        var arrow = NewText(parent, "▼", size, true, color);
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        return arrow;
    }

    #endregion

    #region 위

    private void BuildTop()
    {
        // 층 이름 (왼쪽 위)
        var floor = NewImage(_safe, "Floor", RuntimeUIKit.Style.OptionButton, Color.white);
        Place(floor.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(600f, 120f));
        var tag = NewImage(floor.transform, "Tag", _art.RibbonLav, Color.white);
        Place(tag.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 6f), new Vector2(200f, 60f));
        var floorTag = NewText(tag.transform, _model.Config.FloorName, 30, true, Color.white, TextAlignmentOptions.Center, 0.15f);
        _floorTag = floorTag;
        Fill(floorTag.rectTransform, 0f, 2f);
        _floorName = NewText(floor.transform, _model.Layer.DisplayName, 40, true, Cocoa, TextAlignmentOptions.Left);
        Place(_floorName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(234f, 8f), new Vector2(350f, 56f));

        // 나가기 (오른쪽 위, 어디서든 바로 밖으로)
        var exit = NewImage(_safe, "Exit", RuntimeUIKit.Style.PrimaryButton != null ? RuntimeUIKit.Style.PrimaryButton : RuntimeUIKit.Style.OptionButton, Color.white);
        _exitButton = exit.rectTransform;
        Place(_exitButton, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(250f, 120f));
        var exitLabel = NewText(exit.transform, "나가기", 42, true, RuntimeUIKit.Style.PrimaryButton != null ? RuntimeUIKit.Style.PrimaryLabelColor : Cocoa);
        Fill(exitLabel.rectTransform, 0f, 12f);
        MakeButton(_exitButton, () => _game.Exit());

        // 광석 띠
        var stripBack = NewImage(_safe, "OreStrip", _art.BoxInset, new Color(1f, 0.98f, 0.94f, 0.92f));
        Place(stripBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(1032f, 112f));
        _strip = NewRect(stripBack.transform, "Chips");
        Fill(_strip, 14f, 6f, 14f, 6f);
        var layout = _strip.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // 탐험 시간 · 곡괭이
        var info = NewImage(_safe, "Info", _art.Paper, new Color(1f, 1f, 1f, 0.92f));
        Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(1032f, 70f));
        _runTime = NewText(info.transform, "", 30, true, Cocoa, TextAlignmentOptions.Left);
        Place(_runTime.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 2f), new Vector2(520f, 50f));
        _pickaxe = NewText(info.transform, "", 28, false, RuntimeUIKit.Style.BodyColor, TextAlignmentOptions.Right);
        Place(_pickaxe.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 2f), new Vector2(480f, 50f));
    }

    private void RebuildStrip()
    {
        foreach (var chip in _chips)
            Destroy(chip.Root.gameObject);
        _chips.Clear();
        var ores = new List<MineOreDefinition>();
        var layer = _model.Layer;
        foreach (var ore in _model.Config.Rework.Ores)
        {
            if (ore == null)
                continue;
            bool here = ore == layer.Relic;
            foreach (var drop in layer.Veins)
                here |= drop != null && drop.Ore == ore;
            foreach (var drop in layer.RareDrops)
                here |= drop != null && drop.Ore == ore;
            foreach (var loot in _model.Config.ChestLoot)
                here |= loot != null && loot.Ore == ore;
            if (here)
                ores.Add(ore);
        }
        float width = Mathf.Min(220f, (1000f - 8f * (ores.Count - 1)) / Mathf.Max(1, ores.Count));
        foreach (var ore in ores)
        {
            var root = NewImage(_strip, "Chip_" + ore.OreId, _art.ChipNormal, new Color(1f, 1f, 1f, 0.9f));
            var rt = root.rectTransform;
            rt.sizeDelta = new Vector2(width, 96f);
            var icon = NewImage(rt, "Icon", ore.Icon, Color.white, true);
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-width * 0.2f, 4f), new Vector2(72f, 72f));
            var count = NewText(rt, "0", 32, true, Cocoa, TextAlignmentOptions.Left);
            Place(count.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(width * 0.05f, 2f), new Vector2(width * 0.5f, 46f));
            count.enableAutoSizing = true;
            count.fontSizeMin = 20f;
            count.fontSizeMax = 32f;
            _chips.Add(new OreChip { Ore = ore, Root = rt, Icon = icon, Count = count });
        }
    }

    #endregion

    #region 갱도 아래 (안내 · 지도 · 방향)

    private void BuildBelow()
    {
        // 갱도 그림 아래 끝 ~ 화면 아래 (LayoutBelowView가 맞춤)
        _below = NewRect(_root, "Below");
        _below.anchorMin = Vector2.zero;
        _below.anchorMax = new Vector2(1f, 0.45f);
        _below.offsetMin = _below.offsetMax = Vector2.zero;

        // 앞 칸 안내
        var prompt = NewImage(_below, "Prompt", _art.Paper, new Color(1f, 1f, 1f, 0.95f));
        Place(prompt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(980f, 112f));
        _prompt = NewText(prompt.transform, "", 34, true, Cocoa);
        Fill(_prompt.rectTransform, 24f, 16f, 24f, 6f);
        _prompt.textWrappingMode = TextWrappingModes.Normal;
        _prompt.enableAutoSizing = true;
        _prompt.fontSizeMin = 22f;
        _prompt.fontSizeMax = 34f;

        BuildPad();
        BuildMap();
        LayoutBelowView();
    }

    private void BuildPad()
    {
        _pad = NewRect(_below, "Pad");
        Place(_pad, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 36f), new Vector2(PadSize, PadSize));
        float step = PadSize / 3f;
        TurnButton(MazeTurn.Forward, "앞", new Vector2(0f, step), 180f);
        TurnButton(MazeTurn.Back, "뒤", new Vector2(0f, -step), 0f);
        TurnButton(MazeTurn.Left, "왼쪽", new Vector2(-step, 0f), -90f);
        TurnButton(MazeTurn.Right, "오른쪽", new Vector2(step, 0f), 90f);

        // 가운데: 계단에서 내려가기 등 (쓸 일 없으면 숨김)
        var action = NewImage(_pad, "Action", _art.RoundPeach, Color.white, true);
        _action = action.rectTransform;
        Place(_action, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(step * 0.92f, step * 0.92f));
        _actionLabel = NewText(_action, "", 30, true, Cocoa);
        Fill(_actionLabel.rectTransform, 6f, 10f, 6f, 0f);
        _actionLabel.textWrappingMode = TextWrappingModes.Normal;
        MakeButton(_action, () => _actionClick?.Invoke());
        _action.gameObject.SetActive(false);
    }

    private void TurnButton(MazeTurn turn, string label, Vector2 position, float arrowAngle)
    {
        float size = PadSize / 3f * 0.98f;
        var circle = NewImage(_pad, "Go_" + turn, _art.RoundFeatured, Color.white, true);
        var rect = circle.rectTransform;
        Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
        var arrow = Arrow(rect, 64f, Cocoa, arrowAngle);
        Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(80f, 80f));
        var text = NewText(rect, label, 28, true, Cocoa);
        Place(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(size, 40f));
        _turnButtons[turn] = MakeButton(rect, () => _game.Go(turn));
    }

    private void BuildMap()
    {
        var panel = NewImage(_below, "Map", _art.Paper, new Color(1f, 1f, 1f, 0.95f));
        _mapPanel = panel.rectTransform;
        Place(_mapPanel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 66f), new Vector2(MapSize, MapSize));
        _mapTitle = NewText(_mapPanel, "지도", 28, true, Cocoa, TextAlignmentOptions.Left);
        Place(_mapTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -10f), new Vector2(420f, 44f));

        var map = _model.Map;
        _mapGrid = NewRect(_mapPanel, "Grid");
        float cell = Mathf.Floor(Mathf.Min((MapSize - 44f) / map.Width, (MapSize - 84f) / map.Height));
        Place(_mapGrid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(cell * map.Width, cell * map.Height));
        _mapCells = new Image[map.Width, map.Height];
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var image = NewImage(_mapGrid, $"C{x}_{y}", null, Color.clear);
                Place(image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x * cell, -y * cell), new Vector2(cell, cell));
                _mapCells[x, y] = image;
            }
        }
        var player = Arrow(_mapGrid, cell * 1.25f, new Color32(0xD9, 0x4F, 0x45, 0xFF), 0f);
        _mapPlayer = player.rectTransform;
        _mapPlayer.anchorMin = _mapPlayer.anchorMax = new Vector2(0f, 1f);
        _mapPlayer.pivot = new Vector2(0.5f, 0.5f);
        _mapPlayer.sizeDelta = new Vector2(cell, cell);
        player.outlineWidth = 0.25f;
        player.outlineColor = Color.white;

        // 지도 해달이 오기 전
        _mapEmpty = NewRect(_mapPanel, "NoMap");
        Fill(_mapEmpty, 20f, 20f, 20f, 50f);
        var otter = NewImage(_mapEmpty, "Otter", _mazeArt.MapOtter, new Color(0.35f, 0.25f, 0.2f, 0.35f), true);
        Place(otter.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(170f, 170f));
        otter.enabled = _mazeArt.MapOtter != null;
        _mapEmptyText = NewText(_mapEmpty, "", 27, false, RuntimeUIKit.Style.BodyColor);
        Place(_mapEmptyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(400f, 190f));
        _mapEmptyText.textWrappingMode = TextWrappingModes.Normal;
    }

    /// <summary>갱도 그림 아래 남은 자리에 안내 · 지도 · 방향 버튼을 맞춤 (좁으면 줄임)</summary>
    public void LayoutBelowView()
    {
        if (_below == null)
            return;
        float bottom = Mathf.Clamp01(_game.ViewBottomScreen);
        _below.anchorMax = new Vector2(1f, bottom);
        _below.offsetMin = _below.offsetMax = Vector2.zero;
        float height = _root.rect.height * bottom;
        float room = height - 140f;
        // 세로: 남은 높이에 맞추고, 가로: 지도 + 버튼이 한 줄에 들어가게
        float fitWidth = (_root.rect.width - 90f) / (PadSize + MapSize);
        float scale = Mathf.Clamp(Mathf.Min(room / (PadSize + 50f), fitWidth), 0.5f, 1.15f);
        _pad.localScale = Vector3.one * scale;
        _mapPanel.localScale = Vector3.one * Mathf.Clamp((room - 30f) / MapSize, 0.45f, scale);
        // 가로 화면은 폭이 넓으니 지도 · 버튼을 가운데 쪽으로
        float extra = Mathf.Max(0f, (_root.rect.width - 1080f) * 0.5f);
        _pad.anchoredPosition = new Vector2(-30f - extra, 36f);
        _mapPanel.anchoredPosition = new Vector2(30f + extra, 76f);
    }

    #endregion

    #region 알림

    private void BuildNotices()
    {
        _combo = NewText(_root, "", 64, true, Color.white, TextAlignmentOptions.Right, 0.22f);
        Place(_combo.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 300f), new Vector2(420f, 130f));

        var toast = NewImage(_root, "Toast", RuntimeUIKit.Style.OptionButton, Color.white);
        _toast = toast.rectTransform;
        Place(_toast, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(760f, 100f));
        _toastText = NewText(_toast, "", 34, true, Cocoa);
        Fill(_toastText.rectTransform, 20f, 12f, 20f, 0f);
        _toast.gameObject.SetActive(false);

        _title = NewRect(_root, "FloorTitle");
        Place(_title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(900f, 260f));
        var ribbon = NewImage(_title, "Ribbon", _art.RibbonLav, Color.white);
        Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(300f, 84f));
        var number = NewText(ribbon.transform, _model.Config.FloorName, 42, true, Color.white, TextAlignmentOptions.Center, 0.18f);
        _floorTitleNumber = number;
        Fill(number.rectTransform, 0f, 4f);
        _titleText = NewText(_title, _model.Layer.DisplayName, 92, true, Color.white, TextAlignmentOptions.Center, 0.22f);
        Place(_titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(900f, 120f));
        _titleSub = NewText(_title, _model.Layer.Subtitle, 36, true, Color.white, TextAlignmentOptions.Center, 0.22f);
        Place(_titleSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(900f, 56f));
        _title.gameObject.SetActive(false);
    }

    public void ShowToast(string text)
    {
        _toastText.text = text;
        _toast.gameObject.SetActive(true);
        _toast.SetAsLastSibling();
        _toastAge = 0f;
    }

    public void ShowCombo(int combo, bool crit)
    {
        if (combo < 3)
            return;
        _comboAge = 0f;
        _combo.text = crit ? $"<size=70%>치명타!</size>\n콤보 {combo}" : $"콤보 {combo}";
        _combo.color = combo >= 20 ? new Color(1f, 0.55f, 0.3f) : combo >= 10 ? new Color(1f, 0.82f, 0.3f) : Color.white;
    }

    public void Flash(Color color)
    {
        _flashColor = color;
        _flashAge = 0f;
    }

    public void ShowFloorTitle()
    {
        _title.gameObject.SetActive(true);
        _titleAge = 0f;
    }

    /// <summary>처음 캔 광석 (도감 등록)</summary>
    public void ShowDiscovery(MineOreDefinition ore)
    {
        ShowToast(ore.Rarity == MineOreRarity.Relic ? $"옛 왕국의 흔적! {ore.DisplayName}" : $"새 광석 발견! {ore.DisplayName}");
        _sfx.Play(MineSfx.Kind.Discover);
    }

    #endregion

    #region 갱신

    /// <summary>광석 숫자 · 곡괭이 · 버튼</summary>
    public void Refresh()
    {
        foreach (var chip in _chips)
        {
            bool known = _model.IsDiscovered(chip.Ore);
            chip.Icon.color = known ? Color.white : new Color(0.2f, 0.12f, 0.1f, 0.35f);
            _pending.TryGetValue(chip.Ore, out int pending);
            chip.Count.text = known ? Mathf.Max(0, _model.Count(chip.Ore) - pending).ToString() : "?";
        }
        var pickaxe = _model.Pickaxe;
        _pickaxe.text = $"{pickaxe.DisplayName} · 힘 {pickaxe.Power}";
        RefreshPrompt();
    }

    /// <summary>앞 칸 안내 · 방향 버튼 · 가운데 버튼</summary>
    public void RefreshPrompt()
    {
        if (_prompt == null)
            return;
        bool inside = _model.Inside;
        _below.gameObject.SetActive(inside);
        _exitButton.gameObject.SetActive(inside);
        if (!inside)
            return;

        foreach (var pair in _turnButtons)
        {
            bool open = !_model.Map.IsWall(_model.CellToward(pair.Key));
            pair.Value.interactable = open;
            var image = (Image)pair.Value.targetGraphic;
            image.color = open ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            foreach (var label in pair.Value.GetComponentsInChildren<TextMeshProUGUI>())
                label.alpha = open ? 1f : 0.3f;
        }

        _actionClick = null;
        if (_model.OnStairs)
        {
            _actionLabel.text = "내려\n가기";
            _actionClick = _game.Descend;
        }
        _action.gameObject.SetActive(_actionClick != null);

        _prompt.text = PromptText();
    }

    private string PromptText()
    {
        if (_model.OnStairs)
            return "아래층으로 내려가는 계단이에요";
        var rock = _model.RockAhead;
        switch (_model.Look(MazeTurn.Forward))
        {
            case MazeAhead.Rock:
                if (rock != null && rock.Blocking)
                    return rock.Shiny ? "반짝 광맥이 길을 막고 있어요! 톡톡 쳐서 부숴요" : "돌이 길을 막고 있어요! 톡톡 쳐서 부숴요";
                return "다시 생긴 돌이에요. 캐거나 그냥 지나갈 수 있어요";
            case MazeAhead.Chest:
                return "보물 상자예요! 눌러서 열어요";
            case MazeAhead.OpenedChest:
                return "비어 있는 보물 상자예요";
            case MazeAhead.Stairs:
                return "앞에 아래층 계단이 보여요";
            case MazeAhead.Entrance:
                return "갱도 입구 쪽이에요";
            case MazeAhead.Wall:
                return _model.OnEntrance ? "갱도 입구예요. 나가기로 언제든 나갈 수 있어요" : "앞이 막혔어요. 다른 길을 찾아요";
            default:
                return _model.OnEntrance ? "갱도 입구예요. 나가기로 언제든 나갈 수 있어요" : "어디로 갈까요?";
        }
    }

    /// <summary>지도 (지도 해달이 있으면 밟은 칸 · 그 둘레 벽 · 돌 · 상자 · 계단 · 나)</summary>
    public void RefreshMap()
    {
        if (_mapCells == null)
            return;
        bool has = _model.MapOtter;
        _mapGrid.gameObject.SetActive(has);
        _mapEmpty.gameObject.SetActive(!has);
        _mapTitle.text = has ? "지도 · 지도 해달이 그리는 중" : "지도";
        if (!has)
        {
            int left = Mathf.Max(0, _model.Config.MapOtterRuns - _model.Runs);
            _mapEmptyText.text = $"지도가 없어요\n지도 해달이 오면 지나온 길을\n그려 줘요 (앞으로 {left}번 다녀오면)";
            return;
        }

        var map = _model.Map;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                _mapCells[x, y].color = MapColor(cell);
            }
        }
        var rect = _mapCells[_model.Position.x, _model.Position.y].rectTransform;
        _mapPlayer.anchoredPosition = rect.anchoredPosition + new Vector2(rect.sizeDelta.x * 0.5f, -rect.sizeDelta.y * 0.5f);
        // ▼ = 남쪽: 북 180 · 동 90 · 남 0 · 서 -90
        float angle = _model.Facing == MazeDir.North ? 180f : _model.Facing == MazeDir.East ? 90f : _model.Facing == MazeDir.South ? 0f : -90f;
        _mapPlayer.localRotation = Quaternion.Euler(0f, 0f, angle);
        _mapPlayer.SetAsLastSibling();
    }

    private Color MapColor(Vector2Int cell)
    {
        var map = _model.Map;
        if (map.IsWall(cell))
        {
            // 밟은 칸 옆의 벽만
            for (int d = 0; d < 4; d++)
            {
                if (_model.IsVisited(cell + MazeDirs.Step((MazeDir)d)))
                    return MapWall;
            }
            return Color.clear;
        }
        bool seen = _model.IsVisited(cell);
        if (!seen)
        {
            // 밟은 칸 바로 옆 (돌 · 상자 앞에서 멈춘 칸)
            bool next = false;
            for (int d = 0; d < 4; d++)
                next |= _model.IsVisited(cell + MazeDirs.Step((MazeDir)d));
            if (!next)
                return Color.clear;
            var what = _model.What(cell);
            if (what == MazeAhead.Rock)
                return MapRock;
            if (what == MazeAhead.Chest)
                return MapChest;
            return new Color(MapFloor.r, MapFloor.g, MapFloor.b, 0.45f);
        }
        switch (map.Cell(cell))
        {
            case MazeCell.Start: return MapStart;
            case MazeCell.Stairs: return MapStairs;
            case MazeCell.Chest: return _model.IsChestOpened(cell) ? MapChestOpen : MapChest;
            case MazeCell.Rock:
                var rock = _model.RockAt(cell);
                return rock != null && rock.Present ? MapRockOpen : MapFloor;
            default: return MapFloor;
        }
    }

    #endregion

    #region 광석이 띠로 날아옴

    public void FlyOre(Vector3 world, MineOreDefinition ore, int amount)
    {
        _pending.TryGetValue(ore, out int pending);
        _pending[ore] = pending + amount;
        Refresh();
        var chip = _chips.Find(c => c.Ore == ore);
        var target = chip != null ? chip.Root : (RectTransform)_strip.parent;
        var camera = Camera.main;
        Vector2 screen = camera != null ? (Vector2)camera.WorldToScreenPoint(world) : Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var from);
        var image = _flyerPool.Count > 0 ? _flyerPool.Pop() : NewImage(_root, "Flyer", null, Color.white, true);
        image.gameObject.SetActive(true);
        image.sprite = ore.Icon;
        image.transform.SetAsLastSibling();
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(92f, 92f);
        rect.anchoredPosition = from;
        var to = LocalOf(target);
        var control = Vector2.Lerp(from, to, 0.35f) + new Vector2(UnityEngine.Random.Range(-220f, 220f), 260f);
        _flyers.Add(new Flyer
        {
            Image = image, From = from, Control = control, Target = target, Life = 0.55f, Size = 92f,
            Arrived = () =>
            {
                _pending.TryGetValue(ore, out int left);
                left -= amount;
                if (left <= 0)
                    _pending.Remove(ore);
                else
                    _pending[ore] = left;
                if (chip != null)
                    chip.Punch = 0f;
                _sfx.Play(MineSfx.Kind.Collect, 1f + UnityEngine.Random.Range(-0.05f, 0.12f), 0.6f, 0.04f);
                Refresh();
            },
        });
    }

    private Vector2 LocalOf(RectTransform target)
    {
        var world = target.TransformPoint(target.rect.center);
        var screen = RectTransformUtility.WorldToScreenPoint(null, world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
        return local;
    }

    #endregion

    #region 창 (입구 · 결과 · 테스트)

    private RectTransform OpenSheet(float width)
    {
        CloseSheet();
        _sheet = RuntimeUIKit.CreateDim(_root, "Sheet");
        _sheet.SetAsLastSibling();
        var panel = RuntimeUIKit.CreatePanel(_sheet, width);
        return panel;
    }

    private void CloseSheet()
    {
        if (_sheet != null)
            Destroy(_sheet.gameObject);
        _sheet = null;
        _progressRefresh = null;
    }

    private static TextMeshProUGUI Line(RectTransform panel, string text, float size, bool title, Color color)
    {
        var label = RuntimeUIKit.CreateLabel(panel, text, Mathf.RoundToInt(size), title);
        label.color = color;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    /// <summary>갱도 입구 (들어가기). 나갔다 오면 늘 여기서 층 입구로</summary>
    public void ShowEntrance()
    {
        RefreshPrompt();
        RefreshMap();
        var panel = OpenSheet(860f);
        Line(panel, "오래된 갱도", 60, true, Cocoa);
        Line(panel, $"{_model.Config.FloorName} · {_model.Layer.DisplayName}", 38, true, RuntimeUIKit.Style.BodyColor);
        RuntimeUIKit.CreateSpace(panel, 6f);
        Line(panel, "누군가 먼저 쓰던 광산 카트와 채굴 흔적이 남아 있어요.\n앞 · 뒤 · 왼쪽 · 오른쪽을 골라 길을 찾고, 길을 막은 돌은 톡톡 쳐서 부숴요.", 30, false, RuntimeUIKit.Style.BodyColor);
        RuntimeUIKit.CreateSpace(panel, 6f);
        string otter = _model.MapOtter ? "지도 해달과 함께 가요 (지도를 그려 줘요)"
            : $"지도 해달은 아직 없어요 (앞으로 {Mathf.Max(0, _model.Config.MapOtterRuns - _model.Runs)}번 다녀오면 와요)";
        Line(panel, $"다녀온 횟수 {_model.Runs}번\n{otter}\n{_model.Pickaxe.DisplayName} · 힘 {_model.Pickaxe.Power}", 30, true, Cocoa);
        RuntimeUIKit.CreateSpace(panel, 10f);
        Line(panel, _game.InGame ? $"갱도 골드 {_model.Gold} · 왕국 Lv.{_model.KingdomLevel}"
            : $"골드 {_model.Gold} · 테스트 왕국 Lv.{_model.KingdomLevel}", 28, true, Cocoa);
        RuntimeUIKit.CreateButton(panel, "곡괭이 작업대 · 광석 판매", ShowWorkshop);
        if (_model.FloorCount > 1)
            RuntimeUIKit.CreateButton(panel, "열린 층 선택", ShowFloorSelection);
        else if (_model.FloorCleared)
            RuntimeUIKit.CreateButton(panel, "지하 2층 입구 · 자동 채굴", ShowNextEntrance);
        RuntimeUIKit.CreateButton(panel, "들어가기", () =>
        {
            _sfx.Play(MineSfx.Kind.Tap);
            CloseSheet();
            _game.Enter();
        }, RuntimeUIKit.ButtonKind.Primary);
        if (_game.InGame)
        {
            RuntimeUIKit.CreateButton(panel, "광산으로 돌아가기", () =>
            {
                _sfx.Play(MineSfx.Kind.Tap);
                CloseSheet();
                _game.ReturnToMine();
            }, RuntimeUIKit.ButtonKind.Secondary);
            Line(panel, "광석은 작업대에서 가방으로 옮길 수 있어요. 골드 · 곡괭이는 갱도 전용이에요.", 24, false, new Color(0.45f, 0.38f, 0.33f));
        }
        else
        {
            Line(panel, "테스트 씬: 본 게임 세이브와 따로 돌아요", 24, false, new Color(0.45f, 0.38f, 0.33f));
        }
    }

    /// <summary>나가기 결과 (본 게임에서는 이 시간만큼 마을을 오프라인으로 정산해 기존 보고 화면을 띄움)</summary>
    public void ShowResult(MazeRunResult result)
    {
        RefreshPrompt();
        var panel = OpenSheet(880f);
        Line(panel, "탐험을 마쳤어요", 58, true, Cocoa);
        int minutes = (int)(result.Seconds / 60);
        int seconds = (int)(result.Seconds % 60);
        Line(panel, $"탐험 시간 {minutes}분 {seconds}초", 34, true, RuntimeUIKit.Style.BodyColor);
        if (result.Gains.Count > 0)
        {
            var row = RuntimeUIKit.CreateRow(panel, 96f, 18f);
            foreach (var gain in result.Gains)
            {
                RuntimeUIKit.CreateIcon(row, gain.Ore.Icon, 80f);
                var count = RuntimeUIKit.CreateLabel(row, $"×{gain.Amount}", 34, true);
                count.color = Cocoa;
            }
        }
        else
        {
            Line(panel, "이번에는 캔 것이 없어요", 30, false, RuntimeUIKit.Style.BodyColor);
        }
        Line(panel, $"부순 돌 {result.RocksBroken} · 연 상자 {result.ChestsOpened} · 새로 밟은 칸 {result.NewCells}", 28, false, RuntimeUIKit.Style.BodyColor);
        if (result.MapOtterArrived)
        {
            RuntimeUIKit.CreateSpace(panel, 4f);
            var row = RuntimeUIKit.CreateRow(panel, 150f, 20f);
            RuntimeUIKit.CreateIcon(row, _mazeArt.MapOtter, 140f);
            var text = RuntimeUIKit.CreateLabel(row, "지도 해달이 마을에 왔어요!\n다음 탐험부터 따라와 지도를 그려 줘요.\n지나온 길도 기억해 뒀어요.", 28, true, TextAlignmentOptions.Left);
            text.color = Good;
            _sfx.Play(MineSfx.Kind.Discover);
        }
        RuntimeUIKit.CreateSpace(panel, 4f);
        Line(panel, _game.InGame ? "갱도에서 캔 광석은 갱도 작업대에 쌓여요. 깡깡이의 갱도 자동 채굴도 작업대 쪽으로 들어가요."
            : "본 게임 연결 전 테스트예요. 자동 채굴 광석은 작업대 가방에 쌓이며, 탐험 중에도 같은 속도로 생산해요.", 24, false, new Color(0.45f, 0.38f, 0.33f));
        // 본 게임: 나가기 = 바로 밖(광산)으로. 테스트 씬: 갱도 입구 창으로
        RuntimeUIKit.CreateButton(panel, _game.InGame ? "광산으로" : "확인", () =>
        {
            _sfx.Play(MineSfx.Kind.Tap);
            if (_game.InGame)
            {
                CloseSheet();
                _game.ReturnToMine();
            }
            else
            {
                ShowEntrance();
            }
        }, RuntimeUIKit.ButtonKind.Primary);
    }

    private void BuildTestButton()
    {
        var test = NewImage(_root, "Test", RuntimeUIKit.Style.OptionButton, new Color(1f, 1f, 1f, 0.75f));
        Place(test.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 0f), new Vector2(130f, 60f));
        var label = NewText(test.transform, "테스트", 24, true, Cocoa);
        Fill(label.rectTransform, 0f, 8f);
        MakeButton(test.rectTransform, OpenTest);
    }

    private void OpenTest()
    {
        bool wasInside = _model.Inside;
        var panel = OpenSheet(820f);
        Line(panel, "테스트", 52, true, Cocoa);
        void Item(string text, Action action)
        {
            RuntimeUIKit.CreateButton(panel, text, () =>
            {
                _sfx.Play(MineSfx.Kind.Tap);
                action();
                if (_sheet != null)
                    Back();
            });
        }
        void Back()
        {
            if (wasInside && _model.Inside)
                CloseSheet();
            else
                ShowEntrance();
        }
        Item("지도 해달 바로 오기", _game.CheatMapOtter);
        Item("부순 돌 바로 다시 생기기", _game.CheatRespawn);
        Item("앞 돌을 반짝 광맥으로", _game.CheatShiny);
        Item("곡괭이 한 단계 위", () => _game.CheatPickaxe(1));
        Item("곡괭이 한 단계 아래", () => _game.CheatPickaxe(-1));
        Item("처음부터 (저장 지우기)", _game.ResetAll);
        RuntimeUIKit.CreateButton(panel, "닫기", () =>
        {
            _sfx.Play(MineSfx.Kind.Tap);
            Back();
        }, RuntimeUIKit.ButtonKind.Secondary);
    }

    #endregion

    private void Update()
    {
        _progressRefresh?.Invoke();
        float dt = Time.unscaledDeltaTime;
        // 캔버스 크기가 처음 정해지거나 바뀌면 다시 맞춤
        if (_root.rect.size != _laidOutFor)
        {
            _laidOutFor = _root.rect.size;
            LayoutBelowView();
        }
        if (_model.Inside)
        {
            var now = DateTime.UtcNow;
            double run = _model.RunSeconds(now);
            double next = _model.NextRespawnIn(now);
            string respawn = next >= 0 ? $"   ·   다음 돌 {Mathf.CeilToInt((float)next)}초" : "";
            _runTime.text = $"탐험 {(int)(run / 60)}:{(int)(run % 60):00}{respawn}";
        }
        else
        {
            _runTime.text = "갱도 밖";
        }

        for (int i = _flyers.Count - 1; i >= 0; i--)
        {
            var f = _flyers[i];
            f.Age += dt;
            float k = Mathf.Clamp01(f.Age / f.Life);
            float e = k * k * (3f - 2f * k);
            var to = f.Target != null ? LocalOf(f.Target) : f.Control;
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
        foreach (var chip in _chips)
        {
            if (chip.Punch > 1f)
                continue;
            chip.Punch += dt * 4f;
            chip.Icon.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Mathf.Clamp01(chip.Punch) * Mathf.PI) * 0.18f);
        }

        _comboAge += dt;
        _combo.rectTransform.localScale = Vector3.one * (_comboAge < 0.12f ? Mathf.Lerp(1.4f, 1f, _comboAge / 0.12f) : 1f);
        var cc = _combo.color;
        cc.a = 1f - Mathf.Clamp01((_comboAge - 0.7f) / 0.4f);
        _combo.color = cc;

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
            float alpha = k < 0.25f ? k / 0.25f : 1f - Mathf.Clamp01((k - 1.6f) / 0.5f);
            _title.localScale = Vector3.one * (k < 0.25f ? Mathf.Lerp(1.3f, 1f, k / 0.25f) : 1f);
            foreach (var g in _title.GetComponentsInChildren<Graphic>())
            {
                var c = g.color;
                c.a = alpha;
                g.color = c;
            }
            if (k > 2.1f)
                _title.gameObject.SetActive(false);
        }
        _flashAge += dt;
        var fc = _flashColor;
        fc.a = _flashColor.a * (1f - Mathf.Clamp01(_flashAge / 0.35f));
        _flash.color = fc;
    }
}

/// <summary>광산 미로 화면에 쓰는 한글 (폰트에 미리 넣음)</summary>
public static class MazeGlyphs
{
    public const string Ui = "!%()+,./0123467:?·×▼가간갈갔같개갱거건것게겨견결겹경계고곡골과광괭구국군굴귀규그글금기긴길김깊까깡께끝나남내냥넓녀녔누눌는니닙닛다단닫달대더던데도돌동돼된두둘뒀뒤드든들듦디따때뚫라래량러려력렸로료률르른를름리린림마막만맛매맥먼며면명모못물미밑바밖반발밟방배번벽보본볼봐부분브비빛산상새생서석선설성세셀소속손수순숨숴쉈쉬스습시쌓써쓰씬아안앞야양어억언얼업없었에여연열예옛오온올옴와완왔왕왼요용우운워원위유으은을음의이인읽임입있자작잠장재저적전점정제졌족준중줘지직짐짝째쪽착찾채처첫체쳐쳤초최층치칙친침카칸캐캔콤쾅크큼타탐택터테톡트파판편품픽필하한할함해했험혀현혔화확환횟효후흔희힌힘";
}
