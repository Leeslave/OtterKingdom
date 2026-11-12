using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 왕국 화면 "우리 왕국": 예전 마을회관 발전 현황(한 장에 세로로 다 쌓임)을 탭 4개로 나눈 창 (RuntimeUIWindow).
/// [발전] 레벨업 조건 · 다음 목표 · 왕국 발전/연구 · 땅 넓히기 · 완료한 발전 / [주민] 현황 · 지금 하는 일 · 주민
/// [구역] 질서도 · 땅마다 용도 · 구역 효과 · 이사 지시서 / [일터] 생산 · 가공 · 창고 · 설비 (ProductionSheet)
/// 여는 곳: 상단바 레벨 · 왕국 단계 칩 · 광장 마을회관 · 게시판 "왕국 화면" (SettlementManager.OpenKingdom). 값은 SettlementManager.Kingdom이 계산.
/// 다른 화면으로 가는 버튼은 이 창을 먼저 닫는다 (구역 고르기 등 전역 UI 화면이 이 창보다 아래에 그려지므로)
/// </summary>
public class KingdomSheet : MonoBehaviour
{
    public enum Tab { Progress, Residents, Districts, Work }

    private static readonly string[] TabNames = { "발전", "주민", "구역", "일터" };
    private const float TileSize = 128f;
    private const float ButtonWidth = 172f;
    private const float ButtonHeight = 84f;
    private const float RefreshSeconds = 0.5f;
    // 기록이 자주 바뀌어도 창을 다시 만드는 건 이 간격에 한 번
    private const float RedrawSeconds = 1f;
    private static readonly Color Positive = new Color32(0x5E, 0x8C, 0x55, 0xFF);

    private Canvas _canvas;
    private SettlementManager _manager;
    private RuntimeUIWindow _window;
    private Tab _tab;
    private ProductionSheet _work;
    private readonly List<Action> _refreshes = new List<Action>();
    private readonly List<KingdomWork> _works = new List<KingdomWork>();
    private readonly List<(string name, Sprite icon)> _completed = new List<(string, Sprite)>();
    private readonly List<SettlementOtterDefinition> _residents = new List<SettlementOtterDefinition>();
    private float _refresh;
    private float _lastDraw;
    private bool _redrawPending;
    private bool _subscribed;

    public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

    public void Open(SettlementManager manager, Tab tab, ProductionSheet.Tab work)
    {
        _manager = manager;
        if (_canvas == null)
        {
            _canvas = RuntimeUIKit.CreateCanvas("우리 왕국", 232, false);
            RuntimeUIKit.AttachCanvas(_canvas, transform);
        }
        if (_work == null)
            _work = new ProductionSheet(manager, keep => Draw(keep));
        if (!_subscribed)
        {
            _manager.OnChanged += MarkRedraw;
            _subscribed = true;
        }
        _canvas.gameObject.SetActive(true);
        _tab = tab;
        if (tab == Tab.Work)
            _work.Select(work);
        Draw(false);
        _window.PopIn(this);
    }

    public void Close()
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
        Unsubscribe();
    }

    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        if (_subscribed && _manager != null)
            _manager.OnChanged -= MarkRedraw;
        _subscribed = false;
    }

    private void MarkRedraw() => _redrawPending = true;

    private void Update()
    {
        if (!IsOpen)
            return;
        if (_redrawPending && _tab != Tab.Work && Time.unscaledTime - _lastDraw >= RedrawSeconds)
        {
            _redrawPending = false;
            Draw(true);
            return;
        }
        _refresh -= Time.unscaledDeltaTime;
        if (_refresh > 0f)
            return;
        _refresh = RefreshSeconds;
        Refresh();
    }

    private void Refresh()
    {
        foreach (var refresh in _refreshes)
            refresh?.Invoke();
    }

    /// <param name="keepScroll">같은 탭을 다시 그릴 때 스크롤 자리 유지</param>
    private void Draw(bool keepScroll)
    {
        float scroll = keepScroll && _window != null ? _window.Scroll.verticalNormalizedPosition : 1f;
        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _canvas.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        _refreshes.Clear();
        _lastDraw = Time.unscaledTime;
        _redrawPending = false;

        _window = RuntimeUIWindow.Create(_canvas.transform, "우리 왕국", _manager.TownHallIcon, TabNames, (int)_tab, index =>
        {
            _tab = (Tab)index;
            Draw(false);
        }, Close);
        _window.SetTabDot((int)Tab.Districts, _manager.NeedsDistrictAttention);

        var content = _window.Content;
        switch (_tab)
        {
            case Tab.Residents: DrawResidents(content); break;
            case Tab.Districts: DrawDistricts(content); break;
            case Tab.Work: DrawWork(content); break;
            default: DrawProgress(content); break;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        _window.Scroll.verticalNormalizedPosition = scroll;
        Refresh();
    }

    // 다른 화면으로 갈 때: 이 창을 닫고 연다
    private void Leave(Action open)
    {
        PlazaTapInput.BlockCurrentGesture();
        Close();
        open?.Invoke();
    }

    #region 발전

    private void DrawProgress(Transform content)
    {
        // 왕국 단계 · 마을 발전
        var (done, total) = _manager.MainProgress();
        var head = RuntimeUICards.Card(content, true);
        var headRow = RuntimeUICards.Row(head, 12f);
        RuntimeUICards.Text(headRow, $"{_manager.Settlement.Stage + 1:00} · {_manager.StageName}", 38, true);
        RuntimeUICards.Flexible(headRow);
        RuntimeUICards.Text(headRow, $"마을 발전 {done} / {total}", 27, muted: true);
        var headBar = RuntimeUICards.Bar(head, 24f);
        _refreshes.Add(() => RuntimeUICards.SetBar(headBar, total > 0 ? (float)done / total : 0f));

        var goal = _manager.NextGoal();
        if (_manager.TryGetLevelGate(out var gate))
            LevelGateCard(content, gate, goal);

        Section(content, "다음 목표");
        GoalCard(content, goal);

        if (ProsperityManager.Instance != null)
        {
            var card = RuntimeUICards.Card(content, false);
            RuntimeUICards.IconTile(card, _manager.TownHallIcon, TileSize, filled: true);
            var text = RuntimeUICards.Column(card, 6f);
            RuntimeUICards.Text(text, "왕국 발전 · 연구", 34, true);
            RuntimeUICards.Text(text, "연구 · 공동사업 · 물류 · 기록", 25, muted: true);
            RuntimeUICards.Button(card, "열기", () => Leave(() =>
            {
                var prosperity = ProsperityManager.Instance;
                if (prosperity != null) prosperity.Open();
            }), RuntimeUIKit.ButtonKind.Feature, ButtonWidth, ButtonHeight);
        }

        bool territory = _manager.TryGetTerritoryCard(out var territoryTitle, out var territoryText, out var mission);
        if (territory || _manager.HasIslandMap)
            Section(content, "땅 넓히기");
        if (territory)
        {
            var card = RuntimeUICards.Card(content, false);
            RuntimeUICards.IconTile(card, mission != null ? mission.Icon : null, TileSize);
            var text = RuntimeUICards.Column(card, 6f);
            RuntimeUICards.Text(text, territoryTitle, 34, true);
            RuntimeUICards.Text(text, territoryText, 25, muted: true);
            if (mission != null)
                RuntimeUICards.Button(card, "가 보기", () => Leave(() => _manager.RequestProject(mission)), RuntimeUIKit.ButtonKind.Primary, ButtonWidth, ButtonHeight);
        }
        if (_manager.HasIslandMap)
        {
            var card = RuntimeUICards.Card(content, false);
            var text = RuntimeUICards.Column(card, 6f);
            RuntimeUICards.Text(text, "섬 지도 · 정찰", 34, true);
            RuntimeUICards.Text(text, "정찰대를 보내 새 땅을 찾아요", 25, muted: true);
            RuntimeUICards.Button(card, "열기", () => Leave(_manager.OpenIslandMap), RuntimeUIKit.ButtonKind.Secondary, ButtonWidth, ButtonHeight);
        }

        _manager.CollectCompletedDevelopments(_completed);
        Section(content, "완료한 발전", $"{_completed.Count}개");
        if (_completed.Count == 0)
        {
            RuntimeUICards.Text(content, "아직 완료한 발전이 없어요.", 26, muted: true, alignment: TextAlignmentOptions.Center);
            return;
        }
        var grid = RuntimeUICards.Grid(content, 5, 150f, 44f);
        var fallback = _manager.TownHallIcon;
        foreach (var (name, icon) in _completed)
            RuntimeUICards.Slot(grid, icon != null ? icon : fallback, "", name, 150f);
    }

    // 다음 레벨이 큰 부탁에 묶여 있으면 맨 위에: 경험치 · 큰 부탁 두 줄 (경험치가 다 찼으면 "레벨 멈춤")
    private void LevelGateCard(Transform content, LevelGate gate, KingdomGoal goal)
    {
        var card = RuntimeUICards.Card(content, true);
        var title = RuntimeUICards.Row(card, 12f);
        RuntimeUICards.Text(title, $"Lv.{gate.NextLevel} 레벨업 조건", 38, true);
        RuntimeUICards.Flexible(title);
        RuntimeUICards.Tag(title, gate.Blocked ? "1개 남음" : "2개 남음", gate.Blocked ? RuntimeUICards.TagKind.Warn : RuntimeUICards.TagKind.Plain, 26);
        RuntimeUICards.Text(card, "두 가지를 다 채우면 바로 올라요. 경험치는 넘쳐도 모아 둬요.", 25, muted: true);

        var inset = RuntimeUICards.Inset(card, 14f);
        var expRow = RuntimeUICards.Row(inset, 14f);
        RuntimeUICards.Tag(expRow, gate.Blocked ? "완료" : "진행", gate.Blocked ? RuntimeUICards.TagKind.On : RuntimeUICards.TagKind.Plain, 24);
        RuntimeUICards.Text(expRow, "경험치", 30, true);
        var bar = RuntimeUICards.Bar(expRow, 24f);
        RuntimeUICards.Text(expRow, $"{Mathf.Min(gate.Exp, gate.Need):N0} / {gate.Need:N0}", 26);
        _refreshes.Add(() => RuntimeUICards.SetBar(bar, gate.Need > 0 ? (float)gate.Exp / gate.Need : 1f));

        var requestRow = RuntimeUICards.Row(inset, 14f);
        RuntimeUICards.Tag(requestRow, "남음", RuntimeUICards.TagKind.Warn, 24);
        var column = RuntimeUICards.Column(requestRow, 4f);
        RuntimeUICards.Text(column, $"큰 부탁 · {gate.Request.Title}", 30, true);
        bool sameGoal = goal.Request == gate.Request;
        if (!sameGoal)
            RuntimeUICards.Text(column, $"먼저: {goal.Title}", 24, muted: true);
        if (goal.CanGo)
            RuntimeUICards.Button(requestRow, "가 보기", () => Leave(() => _manager.GoTo(goal)), RuntimeUIKit.ButtonKind.Primary, 150f, 76f, 30);
    }

    private void GoalCard(Transform content, KingdomGoal goal)
    {
        var card = RuntimeUICards.Card(content, false);
        if (goal.Kind == KingdomGoalKind.AllDone)
        {
            var column = RuntimeUICards.Column(card, 14f);
            RuntimeUICards.Text(column, goal.Text, 27);
            var row = RuntimeUICards.Row(column, 14f);
            var global = FindAnyObjectByType<GlobalUIPresenter>();
            RuntimeUICards.Button(row, "생산하러", () => Leave(() => { if (global != null) global.OpenTravel(); }), RuntimeUIKit.ButtonKind.Secondary, 200f, 80f, 30);
            RuntimeUICards.Button(row, "꾸미기", () => Leave(() => { if (global != null) global.OpenDecor(); }), RuntimeUIKit.ButtonKind.Secondary, 200f, 80f, 30);
            RuntimeUICards.Button(row, "도감", () => Leave(() => { if (global != null) global.OpenCodex(); }), RuntimeUIKit.ButtonKind.Secondary, 200f, 80f, 30);
            return;
        }
        RuntimeUICards.IconTile(card, goal.Icon != null ? goal.Icon : _manager.TownHallIcon, TileSize, filled: true);
        var text = RuntimeUICards.Column(card, 6f);
        RuntimeUICards.Text(text, goal.Title, 34, true);
        RuntimeUICards.Text(text, goal.Text, 25, muted: true);
        if (goal.CanGo)
            RuntimeUICards.Button(card, "가 보기", () => Leave(() => _manager.GoTo(goal)), RuntimeUIKit.ButtonKind.Primary, ButtonWidth, ButtonHeight);
    }

    #endregion

    #region 주민

    private void DrawResidents(Transform content)
    {
        var labor = _manager.GetLaborSummary();
        var stats = RuntimeUICards.Card(content, false);
        Stat(stats, "정착 주민", $"{labor.Settled}명");
        Stat(stats, "보낼 수 있음", $"{labor.Available}명");
        Stat(stats, "일하는 중", $"{labor.Working}명");

        _manager.CollectKingdomWork(_works);
        Section(content, "지금 하는 일", _works.Count > 0 ? $"{_works.Count}건" : null);
        if (_works.Count == 0)
            RuntimeUICards.Text(content, "지금 진행 중인 일이 없어요.", 26, muted: true, alignment: TextAlignmentOptions.Center);
        int drawn = _works.Count;
        for (int i = 0; i < _works.Count; i++)
            WorkRow(content, i);
        // 일이 끝나거나 새로 생기면 줄 수가 바뀜 → 다시 그림
        _refreshes.Add(() =>
        {
            _manager.CollectKingdomWork(_works);
            if (_works.Count != drawn)
                _redrawPending = true;
        });

        _manager.CollectResidents(_residents);
        Section(content, $"주민 {_residents.Count}명");
        if (_residents.Count == 0)
            return;
        var grid = RuntimeUICards.Grid(content, 5, 150f, 44f);
        var scouts = _manager.Settlement.Scouting.Workers;
        foreach (var otter in _residents)
        {
            string state = Contains(scouts, otter.OtterId) ? "정찰" : _manager.CanAssign(otter) ? "쉼" : "작업";
            RuntimeUICards.Slot(grid, otter.Portrait, state, otter.DisplayName, 150f);
        }
    }

    private static bool Contains(IReadOnlyList<string> list, string id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == id)
                return true;
        }
        return false;
    }

    private static void Stat(Transform card, string label, string value)
    {
        var inset = RuntimeUICards.Inset(card, 4f);
        inset.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        RuntimeUICards.Text(inset, label, 25, muted: true);
        RuntimeUICards.Text(inset, value, 44, true);
    }

    private void WorkRow(Transform content, int index)
    {
        var work = _works[index];
        var card = RuntimeUICards.Card(content, false);
        RuntimeUICards.IconTile(card, work.Icon != null ? work.Icon : _manager.TownHallIcon, 112f, filled: true);
        var text = RuntimeUICards.Column(card, 6f);
        RuntimeUICards.Text(text, work.Title, 32, true);
        RuntimeUICards.Text(text, work.Detail, 24, muted: true);
        var bar = work.Progress >= 0f ? RuntimeUICards.Bar(text, 22f) : null;
        var right = RuntimeUICards.Column(card, 0f, 170f, TextAnchor.MiddleCenter);
        var remaining = RuntimeUICards.Text(right, work.Remaining, 28, true, alignment: TextAlignmentOptions.Center);
        _refreshes.Add(() =>
        {
            if (index >= _works.Count)
                return;
            var now = _works[index];
            remaining.text = now.Remaining;
            if (bar != null)
                RuntimeUICards.SetBar(bar, Mathf.Max(0f, now.Progress));
        });
    }

    #endregion

    #region 구역

    private void DrawDistricts(Transform content)
    {
        var config = _manager.Config;
        if (!_manager.IsAdministrator)
        {
            var info = RuntimeUICards.Card(content, true);
            RuntimeUICards.Text(info, "아직 구역이 없어요", 34, true);
            RuntimeUICards.Text(info, "행정 해달이 생기면 땅마다 용도(주거 · 상업 · 생산 · 농업)를 정할 수 있어요.\n맞는 구역에 건물이 들어설수록 보너스가 커지고 질서도가 올라요.", 26, muted: true);
            return;
        }

        // 질서도
        var order = _manager.DistrictOrder;
        int score = order != null ? order.Score : 0;
        var orderCard = RuntimeUICards.Card(content, true);
        var orderTitle = RuntimeUICards.Row(orderCard, 12f);
        RuntimeUICards.Text(orderTitle, "왕국 질서도", 36, true);
        RuntimeUICards.Flexible(orderTitle);
        RuntimeUICards.Text(orderTitle, $"{score} / {DistrictRules.MaxOrder}", 40, true);
        var orderBar = RuntimeUICards.Bar(orderCard, 26f);
        _refreshes.Add(() => RuntimeUICards.SetBar(orderBar, score / (float)DistrictRules.MaxOrder));
        var marks = RuntimeUICards.Row(orderCard, 12f);
        RuntimeUICards.Tag(marks, $"이사 지시서 {config.RelocationOrder}", score >= config.RelocationOrder ? RuntimeUICards.TagKind.On : RuntimeUICards.TagKind.Plain, 24);
        RuntimeUICards.Tag(marks, $"성 {config.CastleOrder}", score >= config.CastleOrder ? RuntimeUICards.TagKind.On : RuntimeUICards.TagKind.Plain, 24);
        RuntimeUICards.Flexible(marks);
        RuntimeUICards.Text(orderCard, DistrictGuideRules.OrderGoal(config, score, _manager.PlayerLevel, _manager.HasDevelopment(CastleRules.GuildBuilt)), 25, muted: true);

        // 땅마다 용도
        int pending = _manager.PendingDistrictBlocks().Count;
        Section(content, "구역", pending > 0 ? $"빈 땅 {pending}곳" : null);
        var editable = _manager.EditableDistrictBlocks();
        foreach (var block in _manager.DistrictBlocks)
        {
            if (block == null || !DistrictRules.IsOpen(block, _manager.Settlement))
                continue;
            var type = DistrictRules.TypeOf(block, _manager.Settlement);
            var card = RuntimeUICards.Card(content, false);
            Swatch(card, type);
            var text = RuntimeUICards.Column(card, 4f);
            RuntimeUICards.Text(text, block.DisplayName, 32, true);
            RuntimeUICards.Text(text, type == DistrictType.None ? "아직 용도가 없어요" : Districts.DisplayName(type), 25, muted: type == DistrictType.None);
            if (editable.Contains(block))
            {
                bool empty = type == DistrictType.None;
                RuntimeUICards.Button(card, empty ? "정하기" : "바꾸기", () => Leave(() => _manager.RequestDistrict(block)),
                    empty ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Secondary, 160f, 76f, 30);
            }
        }

        // 구역 효과
        Section(content, "구역 효과", "맞는 구역에 들어선 건물만 세요");
        var effects = RuntimeUICards.Card(content, true);
        foreach (var line in DistrictEffects.Lines(config, _manager.Settlement, order))
            EffectRow(effects, line);

        // 이사 지시서
        string relocation = _manager.RelocationSummary(out var reward);
        if (relocation != null)
        {
            Section(content, "이사 지시서");
            var card = RuntimeUICards.Card(content, false);
            var text = RuntimeUICards.Column(card, 6f);
            RuntimeUICards.Text(text, relocation, 28, true);
            RuntimeUICards.Text(text, reward, 24, muted: true);
            RuntimeUICards.Button(card, "이사하기", () => Leave(() =>
            {
                if (!RelocationModePresenter.Open() && GameManager.Instance != null)
                    GameManager.Instance.ShowAlert("광장에서 이사할 수 있어요.");
            }), RuntimeUIKit.ButtonKind.Feature, ButtonWidth, ButtonHeight);
        }
        RuntimeUICards.Text(content, "꾸미기 모드에서도 구역 색과 이름표를 볼 수 있어요.", 24, muted: true, alignment: TextAlignmentOptions.Center);
    }

    private static void EffectRow(Transform card, DistrictEffectLine line)
    {
        var row = RuntimeUICards.Row(card, 16f);
        Swatch(row, line.Type);
        var text = RuntimeUICards.Column(row, 4f);
        RuntimeUICards.Text(text, Districts.DisplayName(line.Type), 30, true);
        RuntimeUICards.Text(text, !line.HasDistrict ? "아직 이 구역이 없어요"
            : line.PerBuilding > 0 ? $"맞는 건물 {line.Matching}채 · 건물마다 +{line.PerBuilding}%" : "구역이 있으면 바로", 23, muted: true);
        var right = RuntimeUICards.Column(row, 4f, 300f, TextAnchor.MiddleRight);
        var effect = RuntimeUICards.Text(right, DistrictEffects.Label(line.Kind, line.Percent), 28, true, alignment: TextAlignmentOptions.MidlineRight);
        effect.color = line.Percent > 0 ? Positive : RuntimeUICards.Muted;
        if (line.MaxPercent > 0)
            RuntimeUICards.Text(right, $"최대 {line.MaxPercent}%", 22, muted: true, alignment: TextAlignmentOptions.MidlineRight);
    }

    // 용도 색 칸 (안 정했으면 옅은 회색)
    private static void Swatch(Transform parent, DistrictType type)
    {
        var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var element = go.GetComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = 64f;
        element.preferredHeight = element.minHeight = 64f;
        var color = type == DistrictType.None ? new Color(0.86f, 0.82f, 0.78f) : DistrictGuideRules.ColorOf(type);
        color.a = 1f;
        var image = go.GetComponent<Image>();
        RuntimeUIKit.ApplySprite(image, RuntimeUIKit.Style.Tag, color);
        image.color = color; // ApplySprite는 그림이 있으면 흰색으로 둠 → 용도 색으로 물들임
        image.raycastTarget = false;
    }

    #endregion

    #region 일터

    private void DrawWork(Transform content)
    {
        var tabs = _work.VisibleTabs();
        if (!tabs.Contains(_work.Current))
            _work.Select(ProductionSheet.Tab.Production);
        var row = RuntimeUICards.Row(content, 12f);
        foreach (var tab in tabs)
        {
            var target = tab;
            RuntimeUICards.Button(row, ProductionSheet.TabName(tab), () =>
            {
                _work.Select(target);
                Draw(false);
            }, tab == _work.Current ? RuntimeUIKit.ButtonKind.Feature : RuntimeUIKit.ButtonKind.Option, 150f, 76f, 30);
        }
        RuntimeUICards.Flexible(row);
        _work.Draw(content, _refreshes);
    }

    #endregion

    private static void Section(Transform content, string title, string right = null)
    {
        var row = RuntimeUICards.Row(content, 12f);
        RuntimeUICards.Text(row, title, 32, true);
        RuntimeUICards.Flexible(row);
        if (!string.IsNullOrEmpty(right))
            RuntimeUICards.Text(row, right, 25, muted: true);
    }
}
