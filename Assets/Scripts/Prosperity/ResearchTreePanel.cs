using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 왕국 발전 [연구] 탭의 기술 트리 (예전: 기술 카드 24장 세로 목록). 시안 Docs/왕국화면_개편_시안.md 8장.
/// - 위 고정: 연구 포인트 · [연구 가능 n개] (누르면 그 기술로) · 분야 탭 4개 (진행 막대 · 연구 가능하면 빨간 점)
/// - 트리: 단계 띠(레벨대 · 완료 / 지금 / Lv.N에 열려요) 아래 노드와 선. 고른 기술의 선행 길은 주황, 다른 분야 선행은 흐린 칸
/// - 아래 고정 시트: 고른 기술 (접힘 = 한 줄 + 버튼, 누르면 펼침 = 효과 · 조건 체크리스트 · 다음에 열리는 것 · 버튼)
/// 배치는 ResearchTreeLayout, 상태는 ProsperityResearch. ProsperityScreen이 그릴 때마다 부른다.
/// </summary>
public sealed class ResearchTreePanel
{
    public const float HeaderHeight = 236f;
    private const float CollapsedSheet = 214f;
    private const float ExpandedSheet = 830f;
    private const float NodeSize = 100f;
    private const float RowHeight = 200f;
    private const float BandHeight = 64f;
    private const float BandGap = 26f;
    private const float LabelWidth = 230f;

    private static readonly Color DoneLine = new Color32(0x7F, 0xA8, 0x6C, 0xFF);
    private static readonly Color OpenLine = new Color32(0x4B, 0x2E, 0x22, 0xFF);
    private static readonly Color LockLine = new Color32(0xD6, 0xC6, 0xB4, 0xFF);
    private static readonly Color PathLine = new Color32(0xE8, 0xA8, 0x3A, 0xFF);
    private static readonly Color NextLine = new Color32(0xE9, 0xC4, 0x8C, 0xFF);
    private static readonly Color Positive = new Color32(0x5E, 0x8C, 0x55, 0xFF);
    private static readonly Color Glow = new Color(0.95f, 0.79f, 0.3f, 0.45f);

    private enum NodeState { Done, Open, NeedRp, NeedLevel, NeedPrerequisite }

    private readonly Func<string, Sprite> _icon;
    private TechBranch _branch;
    private string _selected;
    private bool _expanded;
    private bool _initialized;
    private string _scrollTo;

    public float FooterHeight => _expanded ? ExpandedSheet : CollapsedSheet;

    /// <summary>지금 보고 있는 분야 (경제면 트리 아래 장터 주문)</summary>
    public TechBranch Branch => _branch;

    /// <summary>다시 그린 뒤 이 기술이 보이게 스크롤해야 하면 그 노드 (한 번만)</summary>
    public RectTransform ScrollTarget { get; private set; }

    public ResearchTreePanel(Func<string, Sprite> icon)
    {
        _icon = icon ?? throw new ArgumentNullException(nameof(icon));
    }

    #region 상태

    private static NodeState StateOf(ProsperityResearch research, ProsperityTech tech, int level)
    {
        if (research.HasTech(tech.Id))
            return NodeState.Done;
        foreach (var id in tech.Prerequisites)
            if (!research.HasTech(id))
                return NodeState.NeedPrerequisite;
        if (level < tech.Level)
            return NodeState.NeedLevel;
        return research.Data.rp < tech.Cost ? NodeState.NeedRp : NodeState.Open;
    }

    // 처음 열 때: 연구할 수 있는 기술이 있는 분야 · 그 기술. 없으면 산업의 첫 미완료 기술
    private void EnsureSelection(ProsperityResearch research, int level)
    {
        if (_initialized && ProsperityResearch.Find(_selected) != null)
            return;
        _initialized = true;
        string open = FirstOpen(research, level, null);
        _selected = open ?? FirstUnfinished(research, TechBranch.Industry) ?? TechIds.Sawmill;
        _branch = ResearchTreeLayout.BranchOf(_selected);
        _scrollTo = _selected;
    }

    private static string FirstOpen(ProsperityResearch research, int level, TechBranch? branch)
    {
        foreach (var b in ResearchTreeLayout.Branches)
        {
            if (branch.HasValue && b != branch.Value)
                continue;
            foreach (var id in ResearchTreeLayout.TechsOf(b))
                if (research.Check(id, level) == ResearchResult.Success)
                    return id;
        }
        return null;
    }

    private static string FirstUnfinished(ProsperityResearch research, TechBranch branch)
    {
        foreach (var id in ResearchTreeLayout.TechsOf(branch))
            if (!research.HasTech(id))
                return id;
        return null;
    }

    private static int CountOpen(ProsperityResearch research, int level, TechBranch? branch)
    {
        int count = 0;
        foreach (var b in ResearchTreeLayout.Branches)
        {
            if (branch.HasValue && b != branch.Value)
                continue;
            foreach (var id in ResearchTreeLayout.TechsOf(b))
                if (research.Check(id, level) == ResearchResult.Success)
                    count++;
        }
        return count;
    }

    private void Select(string techId, bool scroll)
    {
        _selected = techId;
        _branch = ResearchTreeLayout.BranchOf(techId);
        if (scroll)
            _scrollTo = techId;
    }

    #endregion

    #region 위 (연구 포인트 · 분야 탭)

    public void DrawHeader(RectTransform header, ProsperityResearch research, int level, Action redraw)
    {
        EnsureSelection(research, level);
        var layout = header.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var top = RuntimeUICards.Card(header, false);
        RuntimeUICards.Text(top, "연구 포인트", 32, true);
        RuntimeUICards.Text(top, $"{research.Data.rp} RP", 34, true).color = Positive;
        RuntimeUICards.Flexible(top);
        int open = CountOpen(research, level, null);
        if (open > 0)
        {
            RuntimeUICards.Button(top, $"연구 가능 {open}개", () =>
            {
                var target = FirstOpen(research, level, _branch) ?? FirstOpen(research, level, null);
                if (target == null) return;
                Select(target, true);
                _expanded = false;
                redraw();
            }, RuntimeUIKit.ButtonKind.Feature, 230f, 70f, 26);
        }
        else
        {
            RuntimeUICards.Text(top, "연구 가능한 기술 없음", 24, muted: true);
        }

        var tabs = new GameObject("BranchTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)).GetComponent<HorizontalLayoutGroup>();
        tabs.transform.SetParent(header, false);
        tabs.GetComponent<LayoutElement>().preferredHeight = 108f;
        tabs.spacing = 12f;
        tabs.childControlWidth = tabs.childControlHeight = true;
        tabs.childForceExpandWidth = tabs.childForceExpandHeight = true;
        foreach (var branch in ResearchTreeLayout.Branches)
            BranchTab(tabs.transform, branch, research, level, redraw);
    }

    private void BranchTab(Transform parent, TechBranch branch, ProsperityResearch research, int level, Action redraw)
    {
        var style = RuntimeUIKit.Style;
        var techs = ResearchTreeLayout.TechsOf(branch);
        int done = 0;
        foreach (var id in techs)
            if (research.HasTech(id))
                done++;
        bool selected = branch == _branch;
        var go = new GameObject("Branch", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RuntimeUIKit.ApplySprite(go.GetComponent<Image>(), selected ? style.TabSelected : style.TabNormal, Color.white);
        go.GetComponent<Button>().onClick.AddListener(() =>
        {
            PlazaTapInput.BlockCurrentGesture();
            if (_branch == branch) return;
            var target = FirstOpen(research, level, branch) ?? FirstUnfinished(research, branch) ?? techs[techs.Count - 1];
            Select(target, true);
            _expanded = false;
            redraw();
        });

        var icon = Child("Icon", go.transform, new Vector2(0f, 0.5f), new Vector2(36f, 8f), new Vector2(46f, 46f));
        var iconImage = icon.gameObject.AddComponent<Image>();
        iconImage.sprite = _icon(techs[0]);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        var name = RuntimeUICards.Text(go.transform, ResearchTreeLayout.Name(branch), 28, true);
        Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(62f, 10f), new Vector2(90f, 40f), new Vector2(0f, 0.5f));
        name.color = selected ? style.TextColor : RuntimeUICards.Muted;
        var count = RuntimeUICards.Text(go.transform, $"{done}/{techs.Count}", 20, muted: true, alignment: TextAlignmentOptions.MidlineRight);
        Place(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, 10f), new Vector2(60f, 30f), new Vector2(1f, 0.5f));

        var barGo = new GameObject("Bar", typeof(RectTransform));
        barGo.transform.SetParent(go.transform, false);
        var barRect = (RectTransform)barGo.transform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.offsetMin = new Vector2(20f, 22f);
        barRect.offsetMax = new Vector2(-20f, 34f);
        var track = barGo.AddComponent<Image>();
        RuntimeUIKit.ApplySprite(track, style.BarTrack, new Color(0.92f, 0.86f, 0.78f));
        track.pixelsPerUnitMultiplier = 3f;
        track.raycastTarget = false;
        if (done > 0)
        {
            var fill = Child("Fill", barGo.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Max(0.12f, done / (float)techs.Count), 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();
            RuntimeUIKit.ApplySprite(fillImage, style.BarFill, Positive);
            fillImage.pixelsPerUnitMultiplier = 3f;
            fillImage.raycastTarget = false;
        }

        if (CountOpen(research, level, branch) > 0)
        {
            var dot = Child("Dot", go.transform, new Vector2(1f, 1f), new Vector2(-6f, -2f), new Vector2(26f, 26f));
            var dotImage = dot.gameObject.AddComponent<Image>();
            var red = new Color(0.9f, 0.32f, 0.27f);
            RuntimeUIKit.ApplySprite(dotImage, style.Knob, red);
            dotImage.color = red;
            dotImage.raycastTarget = false;
        }
    }

    #endregion

    #region 트리

    public void DrawTree(Transform content, ProsperityResearch research, int level, Action redraw)
    {
        EnsureSelection(research, level);
        ScrollTarget = null;
        var bands = ResearchTreeLayout.For(_branch);

        // 칸 자리 (열은 폭 비율, 줄은 위에서부터 px)
        var positions = new Dictionary<string, (float x, float y)>();
        var bandTops = new List<float>();
        float y = 10f;
        foreach (var band in bands)
        {
            bandTops.Add(y);
            y += BandHeight + BandGap;
            foreach (var row in band.Rows)
            {
                float cy = y + NodeSize * 0.5f;
                foreach (var slot in row)
                    positions[Key(slot)] = (ColumnX(slot.Column), cy);
                y += RowHeight;
            }
            y += 10f;
        }

        var treeGo = new GameObject("Tree", typeof(RectTransform), typeof(LayoutElement));
        treeGo.transform.SetParent(content, false);
        treeGo.GetComponent<LayoutElement>().preferredHeight = y + 20f;
        var tree = (RectTransform)treeGo.transform;

        // 선: 고른 기술까지 거슬러 올라가는 선행 길은 주황, 고른 기술에서 나가는 길은 옅은 주황
        var path = new HashSet<string>();
        CollectPath(_selected, positions, path);
        foreach (var band in bands)
            foreach (var row in band.Rows)
                foreach (var slot in row)
                {
                    if (slot.Ghost)
                        continue;
                    var tech = ProsperityResearch.Find(slot.TechId);
                    if (tech == null)
                        continue;
                    foreach (var pre in tech.Prerequisites)
                    {
                        if (!TryFind(positions, pre, out var from, out bool ghost))
                            continue;
                        var to = positions[slot.TechId];
                        Color color;
                        float width = 7f;
                        if (path.Contains(pre + ">" + slot.TechId)) { color = PathLine; width = 10f; }
                        else if (pre == _selected) color = NextLine;
                        else if (research.HasTech(pre) && research.HasTech(slot.TechId)) color = DoneLine;
                        else color = research.HasTech(pre) ? OpenLine : LockLine;
                        Edge(tree, from, to, color, width, ghost && Mathf.Approximately(from.y, to.y));
                    }
                }

        // 단계 띠 (선 위에)
        for (int i = 0; i < bands.Count; i++)
            Band(tree, bandTops[i], bands[i], research, level, i);

        // 노드
        for (int i = 0; i < bands.Count; i++)
        {
            var (min, _) = ResearchTreeLayout.Levels(bands[i]);
            bool lockedBand = level < min;
            foreach (var row in bands[i].Rows)
                foreach (var slot in row)
                {
                    var tech = ProsperityResearch.Find(slot.TechId);
                    if (tech == null)
                        continue;
                    var node = Node(tree, positions[Key(slot)], slot, tech, research, level, lockedBand, redraw);
                    if (!slot.Ghost && slot.TechId == _scrollTo)
                        ScrollTarget = node;
                }
        }
        _scrollTo = null;
    }

    private static string Key(TreeSlot slot) => slot.Ghost ? "~" + slot.TechId : slot.TechId;

    private static bool TryFind(Dictionary<string, (float x, float y)> positions, string id, out (float x, float y) position, out bool ghost)
    {
        if (positions.TryGetValue(id, out position)) { ghost = false; return true; }
        ghost = true;
        return positions.TryGetValue("~" + id, out position);
    }

    private static void CollectPath(string id, Dictionary<string, (float x, float y)> positions, HashSet<string> path)
    {
        var tech = ProsperityResearch.Find(id);
        if (tech == null)
            return;
        foreach (var pre in tech.Prerequisites)
        {
            if (!TryFind(positions, pre, out _, out bool ghost) || !path.Add(pre + ">" + id))
                continue;
            if (!ghost)
                CollectPath(pre, positions, path);
        }
    }

    private static float ColumnX(TreeColumn column) =>
        column == TreeColumn.Left ? 0.2f : column == TreeColumn.Right ? 0.8f : 0.5f;

    // 위 칸 → 아래 칸: 내려가다 아래 칸 바로 위에서 옆으로 꺾어 내려감. 같은 줄의 다른 분야 칸 → 옆으로
    private static void Edge(RectTransform tree, (float x, float y) from, (float x, float y) to, Color color, float width, bool sideways)
    {
        float half = NodeSize * 0.5f;
        if (sideways)
        {
            HLine(tree, from.x, to.x, from.y, color, width, half + 6f);
            return;
        }
        float y0 = from.y + half + 46f; // 이름 아래부터
        float y1 = to.y - half - 8f;
        float turn = y1 - 26f;
        if (Mathf.Approximately(from.x, to.x))
        {
            VLine(tree, from.x, y0, y1, color, width);
            return;
        }
        VLine(tree, from.x, y0, turn + width * 0.5f, color, width);
        HLine(tree, from.x, to.x, turn, color, width, 0f);
        VLine(tree, to.x, turn - width * 0.5f, y1, color, width);
    }

    private static void VLine(RectTransform tree, float fx, float top, float bottom, Color color, float width)
    {
        if (bottom <= top)
            return;
        var line = Child("Line", tree, new Vector2(fx, 1f), new Vector2(0f, -top), new Vector2(width, bottom - top), new Vector2(0.5f, 1f));
        Solid(line, color);
    }

    // 두 열 사이 가로선 (inset = 양 끝에서 비울 px, 칸과 겹치지 않게)
    private static void HLine(RectTransform tree, float fa, float fb, float y, Color color, float width, float inset)
    {
        var line = Child("Line", tree, Vector2.zero, Vector2.zero, Vector2.zero);
        line.anchorMin = new Vector2(Mathf.Min(fa, fb), 1f);
        line.anchorMax = new Vector2(Mathf.Max(fa, fb), 1f);
        line.offsetMin = new Vector2(inset - width * 0.5f, -y - width * 0.5f);
        line.offsetMax = new Vector2(-inset + width * 0.5f, -y + width * 0.5f);
        Solid(line, color);
    }

    private static void Solid(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static void Band(RectTransform tree, float top, TreeBand band, ProsperityResearch research, int level, int index)
    {
        var style = RuntimeUIKit.Style;
        var (min, max) = ResearchTreeLayout.Levels(band);
        bool allDone = true;
        foreach (var row in band.Rows)
            foreach (var slot in row)
                if (!slot.Ghost && !research.HasTech(slot.TechId))
                    allDone = false;
        bool locked = level < min;
        var go = new GameObject("Band", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(tree, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(4f, -top - BandHeight);
        rect.offsetMax = new Vector2(-4f, -top);
        var image = go.GetComponent<Image>();
        image.pixelsPerUnitMultiplier = 1.4f; // 띠 높이(64)가 그림 테두리보다 낮음
        var face = allDone ? new Color(0.88f, 0.95f, 0.84f) : locked ? new Color(0.93f, 0.9f, 0.86f) : new Color(1f, 0.94f, 0.8f);
        RuntimeUIKit.ApplySprite(image, style.Inset, face);
        image.color = face;
        image.raycastTarget = false;

        var title = RuntimeUICards.Text(go.transform, $"{index + 1}단계", 28, true);
        Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(110f, 44f), new Vector2(0f, 0.5f));
        if (locked) title.color = RuntimeUICards.Muted;
        var levels = RuntimeUICards.Text(go.transform, min == max ? $"Lv.{min}" : $"Lv.{min}~{max}", 22, muted: true);
        Place(levels.rectTransform, new Vector2(0f, 0.5f), new Vector2(124f, 0f), new Vector2(200f, 40f), new Vector2(0f, 0.5f));

        string status = allDone ? "완료" : locked ? $"Lv.{min}에 열려요" : "지금";
        var tag = RuntimeUICards.Tag(go.transform, status, allDone ? RuntimeUICards.TagKind.On : locked ? RuntimeUICards.TagKind.Plain : RuntimeUICards.TagKind.Gold, 22);
        var tagRect = (RectTransform)tag.Background.transform;
        tagRect.anchorMin = tagRect.anchorMax = tagRect.pivot = new Vector2(1f, 0.5f);
        tagRect.anchoredPosition = new Vector2(-14f, 2f);
        var fitter = tag.Background.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private RectTransform Node(RectTransform tree, (float x, float y) at, TreeSlot slot, ProsperityTech tech, ProsperityResearch research,
        int level, bool lockedBand, Action redraw)
    {
        var style = RuntimeUIKit.Style;
        var state = StateOf(research, tech, level);
        bool selected = !slot.Ghost && tech.Id == _selected;
        float size = slot.Ghost ? 80f : NodeSize;
        var root = Child("Node_" + tech.Id, tree, new Vector2(at.x, 1f), new Vector2(0f, -at.y), new Vector2(size, size));
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = slot.Ghost ? 0.7f : lockedBand && !selected ? 0.55f : 1f;

        if (selected)
        {
            var frame = Child("Selected", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size + 22f, size + 22f));
            var frameImage = frame.gameObject.AddComponent<Image>();
            RuntimeUIKit.ApplySprite(frameImage, style.TagGold, new Color(0.95f, 0.79f, 0.3f));
            frameImage.raycastTarget = false;
        }
        else if (state == NodeState.Open && !slot.Ghost)
        {
            var glow = Child("Glow", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size + 46f, size + 46f));
            var glowImage = glow.gameObject.AddComponent<Image>();
            RuntimeUIKit.ApplySprite(glowImage, style.Knob, Glow);
            glowImage.color = Glow;
            glowImage.raycastTarget = false;
        }

        bool filled = state == NodeState.Done || state == NodeState.Open || state == NodeState.NeedRp;
        var tile = RuntimeUICards.IconTile(root, _icon(tech.Id), size, state == NodeState.NeedPrerequisite && !slot.Ghost, filled);
        tile.anchorMin = tile.anchorMax = tile.pivot = new Vector2(0.5f, 0.5f);
        tile.anchoredPosition = Vector2.zero;
        tile.sizeDelta = new Vector2(size, size);

        // 오른쪽 위 꼬리표: 완료 / 2 RP / Lv.30
        string badge = slot.Ghost ? (state == NodeState.Done ? "완료" : null)
            : state == NodeState.Done ? "완료"
            : state == NodeState.Open ? $"{tech.Cost} RP"
            : state == NodeState.NeedRp ? $"{tech.Cost} RP"
            : state == NodeState.NeedLevel ? $"Lv.{tech.Level}" : null;
        if (badge != null)
        {
            var kind = state == NodeState.Done ? RuntimeUICards.TagKind.On : state == NodeState.Open ? RuntimeUICards.TagKind.Gold : RuntimeUICards.TagKind.Plain;
            var tag = RuntimeUICards.Tag(root, badge, kind, 19);
            var rect = (RectTransform)tag.Background.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.75f, 0.6f);
            rect.anchoredPosition = Vector2.zero;
            var fitter = tag.Background.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        string text = slot.Ghost ? $"{ResearchTreeLayout.Name(ResearchTreeLayout.BranchOf(tech.Id))} · {tech.Name}" : tech.Name;
        var label = RuntimeUICards.Text(root, text, slot.Ghost ? 20 : 24, !slot.Ghost, alignment: TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(LabelWidth, 34f), new Vector2(0.5f, 1f));
        label.color = state == NodeState.Done ? (slot.Ghost ? Positive : style.TextColor)
            : state == NodeState.Open || state == NodeState.NeedRp ? style.TextColor : RuntimeUICards.Muted;

        // 누르면: 이 분야 기술은 고르기, 다른 분야 칸은 그 분야로 가서 고르기
        var hit = root.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        var button = root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            PlazaTapInput.BlockCurrentGesture();
            Select(tech.Id, slot.Ghost);
            redraw();
        });
        return root;
    }

    #endregion

    #region 아래 시트

    public void DrawSheet(RectTransform footer, ProsperityResearch research, int level, Action redraw, Action<string> learned)
    {
        EnsureSelection(research, level);
        var style = RuntimeUIKit.Style;
        var tech = ProsperityResearch.Find(_selected);
        var background = footer.gameObject.AddComponent<Image>();
        RuntimeUIKit.ApplySprite(background, style.Card, new Color(0.99f, 0.96f, 0.9f)); // 9-slice 카드 (패널 그림은 늘이면 모서리가 찌그러짐)
        var toggle = footer.gameObject.AddComponent<Button>();
        toggle.transition = Selectable.Transition.None;
        toggle.onClick.AddListener(() =>
        {
            PlazaTapInput.BlockCurrentGesture();
            _expanded = !_expanded;
            redraw();
        });
        var layout = footer.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(30, 30, 30, 26);
        layout.spacing = 12f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var handle = Child("Handle", footer, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(100f, 10f));
        handle.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Solid(handle, new Color32(0xD9, 0xC4, 0xAA, 0xFF));

        if (tech == null)
            return;
        var state = StateOf(research, tech, level);
        var check = research.Check(tech.Id, level);
        string action = state == NodeState.Done ? "연구 완료"
            : check == ResearchResult.Success ? $"연구하기 · {tech.Cost} RP"
            : check == ResearchResult.NeedsPrerequisite ? "선행 연구 필요"
            : check == ResearchResult.NeedsLevel ? $"Lv.{tech.Level}에 열려요"
            : check == ResearchResult.NotEnoughRp ? "연구 포인트 부족"
            : check == ResearchResult.NotImplemented ? "준비 중" : "연구할 수 없어요";
        Action learn = () =>
        {
            PlazaTapInput.BlockCurrentGesture();
            if (research.TryLearn(tech.Id, GameManager.KingdomLevel) != ResearchResult.Success)
                return;
            learned?.Invoke(tech.Id);
            // 다음에 연구할 수 있는 기술로 (이 분야 먼저)
            var next = FirstOpen(research, GameManager.KingdomLevel, _branch) ?? FirstOpen(research, GameManager.KingdomLevel, null);
            if (next != null)
                Select(next, true);
        };

        var conditions = Conditions(research, tech, level);
        int met = 0;
        foreach (var condition in conditions)
            if (condition.ok) met++;

        if (!_expanded)
        {
            var row = RuntimeUICards.Row(footer, 18f);
            RuntimeUICards.IconTile(row, _icon(tech.Id), 120f, filled: true);
            var column = RuntimeUICards.Column(row, 4f);
            RuntimeUICards.Text(column, tech.Name, 34, true);
            var summary = RuntimeUICards.Text(column, state == NodeState.Done ? "연구 완료" : $"조건 {met}/{conditions.Count} · 눌러서 자세히", 23);
            summary.color = state == NodeState.Done || met == conditions.Count ? Positive : style.WarningColor;
            RuntimeUICards.Button(row, action, learn, check == ResearchResult.Success ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Secondary,
                250f, 96f, 30).interactable = check == ResearchResult.Success;
            return;
        }

        var head = RuntimeUICards.Row(footer, 18f);
        RuntimeUICards.IconTile(head, _icon(tech.Id), 128f, filled: true);
        var text = RuntimeUICards.Column(head, 6f);
        var titleRow = RuntimeUICards.Row(text, 12f);
        RuntimeUICards.Text(titleRow, tech.Name, 38, true);
        RuntimeUICards.Tag(titleRow, ResearchTreeLayout.Name(ResearchTreeLayout.BranchOf(tech.Id)), RuntimeUICards.TagKind.Plain, 22);
        RuntimeUICards.Flexible(titleRow);
        RuntimeUICards.Text(text, "연구하면 " + tech.Effect, 25, muted: true);

        RuntimeUICards.Text(footer, "조건", 28, true);
        foreach (var (ok, label, jump) in conditions)
        {
            var row = RuntimeUICards.Row(footer, 14f);
            RuntimeUICards.Tag(row, ok ? "완료" : "필요", ok ? RuntimeUICards.TagKind.On : RuntimeUICards.TagKind.Warn, 22);
            var line = RuntimeUICards.Text(row, label, 25);
            if (!ok) line.color = style.WarningColor;
            RuntimeUICards.Flexible(row);
            if (jump != null)
            {
                var target = jump;
                RuntimeUICards.Button(row, $"{ResearchTreeLayout.Name(ResearchTreeLayout.BranchOf(target))}으로", () =>
                {
                    PlazaTapInput.BlockCurrentGesture();
                    Select(target, true);
                    redraw();
                }, RuntimeUIKit.ButtonKind.Option, 170f, 60f, 24);
            }
        }

        var opens = new List<string>();
        foreach (var other in ProsperityResearch.Techs)
            foreach (var pre in other.Prerequisites)
                if (pre == tech.Id) opens.Add(other.Name);
        if (opens.Count > 0)
            RuntimeUICards.Text(footer, "다음에 열려요 · " + string.Join(" · ", opens), 23, muted: true);

        var space = new GameObject("Space", typeof(RectTransform), typeof(LayoutElement));
        space.transform.SetParent(footer, false);
        space.GetComponent<LayoutElement>().flexibleHeight = 1f;
        var button = RuntimeUICards.Button(footer, action, learn, check == ResearchResult.Success ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Secondary,
            -1f, 100f, 36);
        button.interactable = check == ResearchResult.Success;
    }

    // 조건 체크리스트: 선행 (다른 분야면 [그 분야로]) · 레벨 · 연구 포인트
    private static List<(bool ok, string label, string jump)> Conditions(ProsperityResearch research, ProsperityTech tech, int level)
    {
        var list = new List<(bool, string, string)>();
        var branch = ResearchTreeLayout.BranchOf(tech.Id);
        foreach (var id in tech.Prerequisites)
        {
            var pre = ProsperityResearch.Find(id);
            if (pre == null)
                continue;
            bool other = ResearchTreeLayout.BranchOf(id) != branch;
            bool ok = research.HasTech(id);
            string label = other ? $"{ResearchTreeLayout.Name(ResearchTreeLayout.BranchOf(id))} · {pre.Name} 연구" : $"{pre.Name} 연구";
            list.Add((ok, label, other && !ok ? id : null));
        }
        list.Add((level >= tech.Level, $"왕국 Lv.{tech.Level} (지금 Lv.{level})", null));
        if (!research.HasTech(tech.Id))
            list.Add((research.Data.rp >= tech.Cost, $"연구 포인트 {tech.Cost} (가진 것 {research.Data.rp})", null));
        return list;
    }

    #endregion

    #region 칸 만들기

    private static RectTransform Child(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    #endregion
}
