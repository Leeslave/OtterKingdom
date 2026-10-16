using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 광산 화면의 창: 아래에서 올라오는 판(작업대 · 층 이동 · 도감 · 테스트)과 가운데 축하 창(새 광석 · 새 곡괭이 · 새 층 · 꺼 둔 동안 · 광석 자세히).
/// 축하 창은 줄을 서서 하나씩
/// </summary>
public partial class MineHud
{
    private enum Sheet { None, Bench, Layers, Book, Debug }

    private const float SlideSeconds = 0.22f;

    private Sheet _sheetOpen = Sheet.None;
    private Image _sheetDim;
    private RectTransform _sheet;
    private RectTransform _sheetBody;
    private TextMeshProUGUI _sheetTitle;
    private float _sheetAge;
    private bool _sheetClosing;

    private Image _popupDim;
    private RectTransform _popup;
    private RectTransform _popupBody;
    private RectTransform _rays;
    private float _popupAge;
    private readonly Queue<Action> _popupQueue = new Queue<Action>();
    private bool _popupOpen;

    /// <summary>창이 열려 있음 (바위를 못 침)</summary>
    public bool Blocking => _sheetOpen != Sheet.None || _popupOpen;

    private void BuildSheets()
    {
        _sheetDim = NewImage(_root, "SheetDim", null, new Color(0f, 0f, 0f, 0.55f));
        RuntimeUIKit.Stretch(_sheetDim.rectTransform);
        MakeButton(_sheetDim.rectTransform, CloseSheet, false);
        _sheetDim.gameObject.SetActive(false);

        var panel = NewImage(_sheetDim.transform, "Sheet", RuntimeUIKit.Style.Panel, Color.white);
        panel.raycastTarget = true;
        _sheet = panel.rectTransform;
        Place(_sheet, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -20f), new Vector2(1060f, 1200f));
        var ribbon = NewImage(_sheet, "TitleRibbon", _art.RibbonPeach, Color.white);
        Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(540f, 110f));
        _sheetTitle = NewText(ribbon.transform, "", 50, true, Color.white, TextAlignmentOptions.Center, 0.2f);
        Fill(_sheetTitle.rectTransform, 0f, 10f);
        var close = NewImage(_sheet, "Close", _art.RoundPeach, Color.white, true);
        Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-46f, -14f), new Vector2(100f, 100f));
        var x = NewText(close.transform, "✕", 46, true, Color.white, TextAlignmentOptions.Center, 0.2f);
        Fill(x.rectTransform, 0f, 6f);
        MakeButton(close.rectTransform, CloseSheet);
        _sheetBody = NewRect(_sheet, "Body");
        Fill(_sheetBody, 50f, 60f, 50f, 90f);

        _popupDim = NewImage(_root, "PopupDim", null, new Color(0f, 0f, 0f, 0.6f));
        RuntimeUIKit.Stretch(_popupDim.rectTransform);
        _popupDim.raycastTarget = true;
        _popupDim.gameObject.SetActive(false);
        var rays = NewImage(_popupDim.transform, "Rays", _art.Rays, new Color(1f, 0.93f, 0.7f, 0.55f), true);
        _rays = rays.rectTransform;
        Place(_rays, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1100f, 1100f));
        var popup = NewImage(_popupDim.transform, "Popup", RuntimeUIKit.Style.Panel, Color.white);
        popup.raycastTarget = true;
        _popup = popup.rectTransform;
        Place(_popup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(900f, 1000f));
        _popupBody = NewRect(_popup, "Body");
        Fill(_popupBody, 50f, 50f, 50f, 50f);
    }

    #region 판 열기 · 닫기

    private void OpenSheet(Sheet sheet, string title, float height)
    {
        _sheetOpen = sheet;
        _sheetTitle.text = title;
        _sheet.sizeDelta = new Vector2(1060f, height);
        _sheetDim.gameObject.SetActive(true);
        _sheetDim.transform.SetAsLastSibling();
        if (_popupOpen)
            _popupDim.transform.SetAsLastSibling();
        _sheetAge = 0f;
        _sheetClosing = false;
        _sfx.Play(MineSfx.Kind.Open);
    }

    public void CloseSheet()
    {
        if (_sheetOpen == Sheet.None || _sheetClosing)
            return;
        _sheetClosing = true;
        _sheetAge = 0f;
    }

    private void ClearBody()
    {
        for (int i = _sheetBody.childCount - 1; i >= 0; i--)
            Destroy(_sheetBody.GetChild(i).gameObject);
    }

    private void UpdateSheets(float dt)
    {
        if (_sheetOpen != Sheet.None)
        {
            _sheetAge += dt;
            float k = Mathf.Clamp01(_sheetAge / SlideSeconds);
            float e = _sheetClosing ? k * k : 1f - (1f - k) * (1f - k);
            float hidden = -_sheet.sizeDelta.y - 40f;
            _sheet.anchoredPosition = new Vector2(0f, _sheetClosing ? Mathf.Lerp(-20f, hidden, e) : Mathf.Lerp(hidden, -20f, e));
            _sheetDim.color = new Color(0f, 0f, 0f, 0.55f * (_sheetClosing ? 1f - e : e));
            if (_sheetClosing && k >= 1f)
            {
                _sheetOpen = Sheet.None;
                _sheetClosing = false;
                _sheetDim.gameObject.SetActive(false);
                ClearBody();
            }
        }
        if (_popupOpen)
        {
            _popupAge += dt;
            float k = Mathf.Clamp01(_popupAge / 0.3f);
            float s = k < 0.7f ? Mathf.Lerp(0.6f, 1.06f, k / 0.7f) : Mathf.Lerp(1.06f, 1f, (k - 0.7f) / 0.3f);
            _popup.localScale = Vector3.one * s;
            _rays.localRotation = Quaternion.Euler(0f, 0f, -_popupAge * 18f);
            _rays.localScale = Vector3.one * (0.95f + 0.05f * Mathf.Sin(_popupAge * 2f));
        }
    }

    #endregion

    #region 작업대

    private void OpenBench()
    {
        OpenSheet(Sheet.Bench, "곡괭이 작업대", 1180f);
        FillBench();
    }

    private void FillBench()
    {
        ClearBody();
        var pickaxe = _model.Pickaxe;
        var next = _model.NextPickaxe;

        // 지금 곡괭이
        var now = NewImage(_sheetBody, "Now", _art.BoxInset, new Color(1f, 0.97f, 0.9f));
        Place(now.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(940f, 220f));
        var nowIcon = NewImage(now.transform, "Icon", pickaxe.Icon, Color.white, true);
        Place(nowIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(190f, 190f));
        var nowName = NewText(now.transform, _model.Sharpen > 0 ? $"{pickaxe.DisplayName} +{_model.Sharpen}" : pickaxe.DisplayName, 46, true, Cocoa, TextAlignmentOptions.Left);
        Place(nowName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, -28f), new Vector2(680f, 60f));
        var nowStats = NewText(now.transform,
            $"힘 {pickaxe.Power}  ·  치명타 {Mathf.RoundToInt(_model.CritChance * 100f)}%\n광부 +{Mathf.RoundToInt(pickaxe.MinerSpeedBonus * 100f)}%  ·  희귀 +{Mathf.RoundToInt(pickaxe.RareBonus * 100f)}%",
            32, false, RuntimeUIKit.Style.BodyColor, TextAlignmentOptions.Left);
        nowStats.textWrappingMode = TextWrappingModes.Normal;
        Place(nowStats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, -96f), new Vector2(680f, 100f));

        // 날 갈기
        int sharpenCost = _model.SharpenCost;
        var sharpen = NewImage(_sheetBody, "Sharpen", _art.Paper, Color.white);
        Place(sharpen.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -248f), new Vector2(940f, 130f));
        var sharpenText = NewText(sharpen.transform,
            sharpenCost >= 0 ? $"날 갈기 +{_model.Sharpen + 1}   <size=80%>치명타 +{Mathf.RoundToInt(_model.Config.CritPerSharpen * 100f)}%</size>" : "날을 끝까지 갈았어요",
            36, true, Cocoa, TextAlignmentOptions.Left);
        Place(sharpenText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 6f), new Vector2(560f, 60f));
        if (sharpenCost >= 0)
        {
            bool afford = _model.Gold >= sharpenCost;
            var buy = CostButton(sharpen.rectTransform, sharpenCost, afford, () =>
            {
                if (_model.TrySharpen())
                {
                    _sfx.Play(MineSfx.Kind.Craft, 1.3f);
                    ShowToast("날이 더 날카로워졌어요!");
                    _game.PlayPickaxeSpark();
                }
                else
                {
                    ShowToast("골드가 모자라요");
                }
            });
            Place(buy, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 4f), new Vector2(260f, 100f));
        }

        // 다음 곡괭이
        if (next == null)
        {
            var done = NewText(_sheetBody, "최고의 곡괭이예요!\n용궁 보석방까지 어디든 캘 수 있어요", 40, true, Cocoa);
            done.textWrappingMode = TextWrappingModes.Normal;
            Place(done.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(900f, 140f));
            return;
        }

        var head = NewText(_sheetBody, "다음 곡괭이", 34, true, RuntimeUIKit.Style.BodyColor);
        Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -398f), new Vector2(900f, 48f));
        var card = NewImage(_sheetBody, "Next", _art.BoxInset, new Color(1f, 0.95f, 0.88f));
        Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -450f), new Vector2(940f, 480f));
        var glow = NewImage(card.transform, "Glow", _art.Glow, new Color(1f, 0.9f, 0.6f, 0.8f), true);
        Place(glow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(130f, -120f), new Vector2(260f, 260f));
        var nextIcon = NewImage(card.transform, "Icon", next.Icon, Color.white, true);
        Place(nextIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(130f, -120f), new Vector2(190f, 190f));
        var nextName = NewText(card.transform, next.DisplayName, 46, true, Cocoa, TextAlignmentOptions.Left);
        Place(nextName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, -40f), new Vector2(660f, 60f));
        int unlock = UnlockedLayerBy(_model.PickaxeTier + 1);
        string opens = unlock >= 0 ? $"\n<color=#C0582E>{unlock + 1}층 {_model.Config.Layers[unlock].DisplayName}이 열려요!</color>" : "";
        var nextDesc = NewText(card.transform, $"힘 {next.Power}  ·  {next.Description}{opens}", 30, false, RuntimeUIKit.Style.BodyColor, TextAlignmentOptions.TopLeft);
        nextDesc.textWrappingMode = TextWrappingModes.Normal;
        Place(nextDesc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, -104f), new Vector2(660f, 130f));

        // 재료 칸
        var row = NewRect(card.transform, "Recipe");
        Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(900f, 190f));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        foreach (var cost in next.Recipe)
        {
            if (cost == null || cost.Ore == null)
                continue;
            int have = _model.Count(cost.Ore);
            RecipeSlot(row, cost.Ore.Icon, $"{have}/{cost.Amount}", have >= cost.Amount, () => OpenDetail(cost.Ore));
        }
        RecipeSlot(row, _art.Gold, next.GoldCost.ToString("N0"), _model.Gold >= next.GoldCost, null);

        bool can = _model.CanCraftNext();
        var make = RuntimeUIKit.CreateButton(_sheetBody, can ? "만들기!" : "재료를 모아요", () =>
        {
            if (_model.TryCraftNext())
            {
                CloseSheet();
                _game.OnPickaxeCrafted();
            }
            else
            {
                ShowToast("재료가 모자라요 — 바위를 깨서 모아요");
            }
        }, can ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Option, 520f);
        var mr = (RectTransform)make.transform;
        Place(mr, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(520f, 120f));
        if (can)
            mr.gameObject.AddComponent<MinePulse>();
    }

    private int UnlockedLayerBy(int tier)
    {
        var layers = _model.Config.Layers;
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].RequiredPickaxeTier == tier)
                return i;
        }
        return -1;
    }

    private void RecipeSlot(RectTransform row, Sprite icon, string text, bool ok, Action onClick)
    {
        var slot = NewImage(row, "Slot", _art.ChipNormal, ok ? new Color(0.9f, 1f, 0.88f) : Color.white);
        slot.rectTransform.sizeDelta = new Vector2(170f, 180f);
        if (onClick != null)
            MakeButton(slot.rectTransform, onClick);
        var image = NewImage(slot.transform, "Icon", icon, Color.white, true);
        Place(image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(104f, 104f));
        var label = NewText(slot.transform, text, 34, true, ok ? Good : Bad);
        Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(160f, 46f));
        label.enableAutoSizing = true;
        label.fontSizeMin = 20f;
        label.fontSizeMax = 34f;
        if (ok)
        {
            var check = NewText(slot.transform, "✔", 34, true, Good, TextAlignmentOptions.Center, 0.15f);
            Place(check.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-18f, -18f), new Vector2(46f, 46f));
        }
    }

    private RectTransform CostButton(RectTransform parent, int cost, bool afford, Action onClick)
    {
        var button = NewImage(parent, "Buy", afford ? RuntimeUIKit.Style.PrimaryButton : RuntimeUIKit.Style.OptionButton, Color.white);
        MakeButton(button.rectTransform, onClick, false);
        var coin = NewImage(button.transform, "Coin", _art.Gold, Color.white, true);
        Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 6f), new Vector2(60f, 60f));
        var text = NewText(button.transform, cost.ToString("N0"), 36, true, afford ? Color.white : Bad, TextAlignmentOptions.Right, afford ? 0.18f : 0f);
        Place(text.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 6f), new Vector2(160f, 60f));
        return button.rectTransform;
    }

    #endregion

    #region 층 이동

    private void OpenLayers()
    {
        OpenSheet(Sheet.Layers, "광산 층", 1320f);
        FillLayers();
    }

    private void FillLayers()
    {
        ClearBody();
        var layers = _model.Config.Layers;
        for (int i = 0; i < layers.Count; i++)
        {
            int index = i;
            var layer = layers[i];
            bool open = _model.IsLayerOpen(i);
            bool blocked = _model.BlockedLayer == i;
            bool here = open && _model.LayerIndex == i && !_game.InBoulder;
            var card = NewImage(_sheetBody, "Layer" + i, _art.BoxInset, here ? new Color(1f, 0.93f, 0.8f) : open || blocked ? Color.white : new Color(0.85f, 0.82f, 0.8f));
            Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f - i * 232f), new Vector2(960f, 216f));
            var medal = NewImage(card.transform, "Medal", open ? _art.RoundFeatured : _art.RoundCream, Color.white, true);
            Place(medal.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(130f, 130f));
            var num = NewText(medal.transform, open || blocked ? $"{i + 1}" : "?", 56, true, Cocoa);
            Fill(num.rectTransform, 0f, 8f);
            var name = NewText(card.transform, layer.DisplayName, 42, true, open || blocked ? Cocoa : new Color(0.45f, 0.38f, 0.35f), TextAlignmentOptions.Left);
            Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(166f, -22f), new Vector2(520f, 56f));
            var sub = NewText(card.transform, layer.Subtitle, 26, false, RuntimeUIKit.Style.BodyColor, TextAlignmentOptions.Left);
            Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(168f, -78f), new Vector2(520f, 40f));
            // 나오는 광석
            var ores = NewRect(card.transform, "Ores");
            Place(ores, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(166f, 16f), new Vector2(520f, 70f));
            var lay = ores.gameObject.AddComponent<HorizontalLayoutGroup>();
            lay.spacing = 6f;
            lay.childAlignment = TextAnchor.MiddleLeft;
            lay.childControlWidth = lay.childControlHeight = false;
            lay.childForceExpandWidth = lay.childForceExpandHeight = false;
            var shown = new HashSet<MineOreDefinition>();
            foreach (var drop in layer.Veins)
                AddLayerOre(ores, drop?.Ore, shown);
            foreach (var drop in layer.RareDrops)
                AddLayerOre(ores, drop?.Ore, shown);
            AddLayerOre(ores, layer.Relic, shown);

            RectTransform action;
            float width = 220f;
            if (here)
            {
                action = Tag(card.rectTransform, "지금 여기", _art.RibbonLav);
            }
            else if (open)
            {
                action = (RectTransform)RuntimeUIKit.CreateButton(card.transform, "가기", () =>
                {
                    CloseSheet();
                    _game.Travel(index);
                }, RuntimeUIKit.ButtonKind.Option, 200f).transform;
                width = 200f;
            }
            else if (blocked)
            {
                action = (RectTransform)RuntimeUIKit.CreateButton(card.transform, "길 뚫기!", () =>
                {
                    CloseSheet();
                    _game.EnterBoulder();
                }, RuntimeUIKit.ButtonKind.Primary, 230f).transform;
                width = 230f;
                action.gameObject.AddComponent<MinePulse>();
            }
            else
            {
                var need = _model.Config.Pickaxes[Mathf.Min(layer.RequiredPickaxeTier, _model.Config.Pickaxes.Count - 1)];
                var lockIcon = NewImage(card.transform, "Need", need.Icon, new Color(1f, 1f, 1f, 0.7f), true);
                Place(lockIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, 14f), new Vector2(84f, 84f));
                var lockText = NewText(card.transform, $"{need.DisplayName}\n필요", 24, true, Bad, TextAlignmentOptions.Center);
                lockText.textWrappingMode = TextWrappingModes.Normal;
                Place(lockText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(150f, 110f));
                continue;
            }
            Place(action, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(width, here ? 80f : 104f));
        }
    }

    private void AddLayerOre(RectTransform row, MineOreDefinition ore, HashSet<MineOreDefinition> shown)
    {
        if (ore == null || !shown.Add(ore))
            return;
        bool known = _model.IsDiscovered(ore);
        var icon = NewImage(row, "Ore", ore.Icon, known ? Color.white : new Color(0.2f, 0.12f, 0.1f, 0.35f), true);
        icon.rectTransform.sizeDelta = new Vector2(64f, 64f);
    }

    private RectTransform Tag(RectTransform parent, string text, Sprite sprite)
    {
        var tag = NewImage(parent, "Tag", sprite, Color.white);
        tag.rectTransform.sizeDelta = new Vector2(220f, 80f);
        var label = NewText(tag.transform, text, 34, true, Color.white, TextAlignmentOptions.Center, 0.18f);
        Fill(label.rectTransform, 0f, 6f);
        return tag.rectTransform;
    }

    #endregion

    #region 도감

    private void OpenBook()
    {
        _newInBook = false;
        _bookBadge.SetActive(false);
        OpenSheet(Sheet.Book, "광석 도감", 1320f);
        FillBook();
    }

    private void FillBook()
    {
        ClearBody();
        var ores = _model.Config.Ores;
        int known = 0;
        foreach (var ore in ores)
            known += _model.IsDiscovered(ore) ? 1 : 0;
        var count = NewText(_sheetBody, $"모은 광석 {known} / {ores.Count}", 34, true, RuntimeUIKit.Style.BodyColor);
        Place(count.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(900f, 50f));
        var grid = NewRect(_sheetBody, "Grid");
        Place(grid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(960f, 1060f));
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(176f, 200f);
        layout.spacing = new Vector2(10f, 12f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 5;
        layout.childAlignment = TextAnchor.UpperCenter;
        foreach (var ore in ores)
        {
            if (ore == null)
                continue;
            bool has = _model.IsDiscovered(ore);
            bool relic = ore.Rarity == MineOreRarity.Relic;
            var tile = NewImage(grid, "Tile", _art.ChipNormal, relic ? new Color(0.92f, 0.88f, 1f) : Color.white);
            var rt = tile.rectTransform;
            if (has)
                MakeButton(rt, () => OpenDetail(ore));
            var icon = NewImage(rt, "Icon", ore.Icon, has ? Color.white : new Color(0.2f, 0.12f, 0.1f, 0.3f), true);
            Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(124f, 124f));
            var label = NewText(rt, has ? (relic ? "유물" : $"x{_model.Count(ore)}") : "???", 28, true, has ? Cocoa : new Color(0.5f, 0.42f, 0.38f));
            Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(170f, 44f));
        }
    }

    #endregion

    #region 테스트

    private void OpenDebug()
    {
        OpenSheet(Sheet.Debug, "테스트", 1080f);
        ClearBody();
        var col = NewRect(_sheetBody, "Buttons");
        Fill(col, 0f, 0f, 0f, 10f);
        var layout = col.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(460f, 120f);
        layout.spacing = new Vector2(20f, 20f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;
        layout.childAlignment = TextAnchor.UpperCenter;
        DebugButton(col, "지금 층 광석 +20", () => _game.CheatOres(20));
        DebugButton(col, "골드 +5,000", () => _model.CheatGold(5000));
        DebugButton(col, "바위 가득", () => _model.CheatFillRocks());
        DebugButton(col, "반짝 광맥", () => _model.CheatShiny());
        DebugButton(col, "광맥 대박", () => _model.CheatJackpot());
        DebugButton(col, "다음 곡괭이 (공짜)", () => { _model.CheatNextPickaxe(); _game.OnPickaxeCrafted(); });
        DebugButton(col, "1시간 꺼 둔 셈", () => _game.CheatOffline(3600));
        DebugButton(col, "처음부터", () => _game.ResetAll());
    }

    private void DebugButton(RectTransform parent, string text, Action action)
    {
        var button = RuntimeUIKit.CreateButton(parent, text, () =>
        {
            action();
            ShowToast(text);
        }, RuntimeUIKit.ButtonKind.Option);
        button.GetComponent<LayoutElement>().ignoreLayout = false;
    }

    #endregion

    #region 가운데 창 (줄 서서 하나씩)

    private void EnqueuePopup(Action build)
    {
        _popupQueue.Enqueue(build);
        if (!_popupOpen)
            ShowNextPopup();
    }

    private void ShowNextPopup()
    {
        if (_popupQueue.Count == 0)
        {
            _popupOpen = false;
            _popupDim.gameObject.SetActive(false);
            return;
        }
        for (int i = _popupBody.childCount - 1; i >= 0; i--)
            Destroy(_popupBody.GetChild(i).gameObject);
        _popupOpen = true;
        _popupAge = 0f;
        _popupDim.gameObject.SetActive(true);
        _popupDim.transform.SetAsLastSibling();
        _popupQueue.Dequeue()();
    }

    private void ClosePopup() => ShowNextPopup();

    // 가운데 창 기본 모양: 리본 제목 · 빛 · 큰 아이콘 · 이름 · 등급 · 글 · 버튼
    private void PopupLayout(string ribbon, Sprite icon, string name, MineOreRarity? rarity, string body, float height,
        out RectTransform buttons, Sprite ribbonSprite = null)
    {
        _popup.sizeDelta = new Vector2(900f, height);
        var rib = NewImage(_popupBody, "Ribbon", ribbonSprite != null ? ribbonSprite : _art.RibbonPeach, Color.white);
        Place(rib.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 46f), new Vector2(620f, 120f));
        var ribText = NewText(rib.transform, ribbon, 52, true, Color.white, TextAlignmentOptions.Center, 0.2f);
        Fill(ribText.rectTransform, 0f, 12f);
        var glow = NewImage(_popupBody, "Glow", _art.Glow, new Color(1f, 0.92f, 0.62f, 0.9f), true);
        Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), new Vector2(420f, 420f));
        var image = NewImage(_popupBody, "Icon", icon, Color.white, true);
        Place(image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), new Vector2(300f, 300f));
        image.gameObject.AddComponent<MineBob>();
        var title = NewText(_popupBody, name, 60, true, Cocoa);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -380f), new Vector2(800f, 80f));
        float y = -462f;
        if (rarity.HasValue)
        {
            var tag = NewImage(_popupBody, "Rarity", _art.RarityTag(rarity.Value), Color.white);
            Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(200f, 60f));
            bool light = rarity.Value <= MineOreRarity.Uncommon;
            var tagText = NewText(tag.transform, RarityName(rarity.Value), 32, true, light ? Cocoa : Color.white,
                TextAlignmentOptions.Center, light ? 0f : 0.18f);
            Fill(tagText.rectTransform, 0f, 4f);
            y -= 76f;
        }
        var text = NewText(_popupBody, body, 34, false, RuntimeUIKit.Style.BodyColor);
        text.textWrappingMode = TextWrappingModes.Normal;
        Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(780f, 220f));
        buttons = NewRect(_popupBody, "Buttons");
        Place(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(800f, 120f));
        var layout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
    }

    public static string RarityName(MineOreRarity rarity)
    {
        switch (rarity)
        {
            case MineOreRarity.Common: return "흔함";
            case MineOreRarity.Uncommon: return "보통";
            case MineOreRarity.Rare: return "드묾";
            case MineOreRarity.Epic: return "귀함";
            case MineOreRarity.Legendary: return "전설";
            default: return "유물";
        }
    }

    /// <summary>처음 캔 광석 · 화석</summary>
    public void ShowDiscovery(MineOreDefinition ore)
    {
        MarkNewInBook();
        EnqueuePopup(() =>
        {
            bool relic = ore.Rarity == MineOreRarity.Relic;
            _sfx.Play(relic || ore.IsRare ? MineSfx.Kind.Jackpot : MineSfx.Kind.Discover);
            PopupLayout(relic ? "옛 왕국의 흔적!" : "새 광석 발견!", ore.Icon, ore.DisplayName, ore.Rarity,
                ore.Description, 1060f, out var buttons, relic ? _art.RibbonLav : null);
            RuntimeUIKit.CreateButton(buttons, "좋아요!", ClosePopup, RuntimeUIKit.ButtonKind.Primary, 360f);
        });
    }

    /// <summary>광석 자세히 (팔기)</summary>
    public void OpenDetail(MineOreDefinition ore)
    {
        if (!_model.IsDiscovered(ore))
        {
            ShowToast("아직 못 찾은 광석이에요");
            return;
        }
        EnqueuePopup(() => BuildDetail(ore));
    }

    private void BuildDetail(MineOreDefinition ore)
    {
        int have = _model.Count(ore);
        string price = ore.CanSell ? $"판매가 {ore.SellPrice:N0}골드" : "팔 수 없어요";
        string use = string.IsNullOrEmpty(ore.UseText) ? "" : $"\n<color=#8A5A3C>쓰임: {ore.UseText}</color>";
        PopupLayout("광석 도감", ore.Icon, ore.DisplayName, ore.Rarity, $"{ore.Description}{use}\n<b>가진 것 {have:N0}개</b> · {price}", 1180f, out var buttons);
        if (ore.CanSell && have > 0)
        {
            SellButton(buttons, ore, 1, "1개 팔기");
            if (have >= 10)
                SellButton(buttons, ore, 10, "10개 팔기");
            SellButton(buttons, ore, have, "모두 팔기");
        }
        RuntimeUIKit.CreateButton(buttons, "닫기", ClosePopup, RuntimeUIKit.ButtonKind.Option, 200f);
    }

    private void SellButton(RectTransform parent, MineOreDefinition ore, int amount, string label)
    {
        Button button = null;
        button = RuntimeUIKit.CreateButton(parent, label, () =>
        {
            int gold = _model.Sell(ore, amount);
            if (gold <= 0)
                return;
            _sfx.Play(MineSfx.Kind.Sell);
            var screen = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            FlyGold(screen, Mathf.Clamp(gold / 20, 3, 10));
            ShowToast($"+{gold:N0} 골드");
            for (int i = _popupBody.childCount - 1; i >= 0; i--)
                Destroy(_popupBody.GetChild(i).gameObject);
            BuildDetail(ore);
        }, RuntimeUIKit.ButtonKind.Secondary, 200f);
    }

    /// <summary>새 곡괭이 완성</summary>
    public void ShowCrafted(MinePickaxeDefinition pickaxe, int layerIndex)
    {
        EnqueuePopup(() =>
        {
            _sfx.Play(MineSfx.Kind.Craft);
            string body = layerIndex >= 0
                ? $"{pickaxe.Description}\n<color=#C0582E><b>{layerIndex + 1}층 {_model.Config.Layers[layerIndex].DisplayName}</b>으로 가는 길을 뚫을 수 있어요!</color>"
                : pickaxe.Description;
            PopupLayout("새 곡괭이!", pickaxe.Icon, pickaxe.DisplayName, null, body, 1000f, out var buttons);
            if (layerIndex >= 0 && _model.BlockedLayer == layerIndex)
            {
                var go = RuntimeUIKit.CreateButton(buttons, "길 뚫으러 가기!", () =>
                {
                    ClosePopup();
                    _game.EnterBoulder();
                }, RuntimeUIKit.ButtonKind.Primary, 420f);
                go.gameObject.AddComponent<MinePulse>();
                RuntimeUIKit.CreateButton(buttons, "나중에", ClosePopup, RuntimeUIKit.ButtonKind.Option, 220f);
            }
            else
            {
                RuntimeUIKit.CreateButton(buttons, "좋아요!", ClosePopup, RuntimeUIKit.ButtonKind.Primary, 360f);
            }
        });
    }

    /// <summary>막힌 길을 깨 새 층이 열림</summary>
    public void ShowLayerOpened(int index, MineLayerDefinition layer)
    {
        EnqueuePopup(() =>
        {
            _sfx.Play(MineSfx.Kind.Jackpot);
            var newest = layer.Veins.Count > 0 ? layer.Veins[layer.Veins.Count - 1]?.Ore : null;
            var icon = newest != null ? newest.Icon : _art.IconLayers;
            PopupLayout("새 층이 열렸어요!", icon, $"{index + 1}층 {layer.DisplayName}", null,
                $"{layer.Subtitle}\n새 광석을 찾아 바위를 깨 봐요!", 960f, out var buttons, _art.RibbonLav);
            RuntimeUIKit.CreateButton(buttons, "캐러 가기!", ClosePopup, RuntimeUIKit.ButtonKind.Primary, 380f);
        });
    }

    /// <summary>꺼 둔 동안 광부가 캔 것</summary>
    public void ShowOffline(List<MineGain> gains, double seconds)
    {
        if (gains.Count == 0)
            return;
        EnqueuePopup(() =>
        {
            _sfx.Play(MineSfx.Kind.Rare);
            int minutes = (int)(seconds / 60);
            string time = minutes >= 60 ? $"{minutes / 60}시간 {minutes % 60}분" : $"{Mathf.Max(1, minutes)}분";
            PopupLayout("광부가 캐 뒀어요", _model.Pickaxe.Icon, $"{time} 동안", null, "", 900f, out var buttons);
            var row = NewRect(_popupBody, "Gains");
            Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(800f, 180f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            foreach (var gain in gains)
                RecipeSlot(row, gain.Ore.Icon, $"+{gain.Amount}", true, null);
            RuntimeUIKit.CreateButton(buttons, "받기!", ClosePopup, RuntimeUIKit.ButtonKind.Primary, 360f);
        });
    }

    #endregion
}

/// <summary>살짝 커졌다 작아졌다 (누르라는 버튼)</summary>
public class MinePulse : MonoBehaviour
{
    private void Update() => transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 5f));
}

/// <summary>둥실둥실 (창 속 큰 아이콘)</summary>
public class MineBob : MonoBehaviour
{
    private Vector2 _base;
    private bool _ready;

    private void Update()
    {
        var rect = (RectTransform)transform;
        if (!_ready)
        {
            _base = rect.anchoredPosition;
            _ready = true;
        }
        rect.anchoredPosition = _base + Vector2.up * (Mathf.Sin(Time.unscaledTime * 2.4f) * 10f);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 1.7f) * 4f);
    }
}
