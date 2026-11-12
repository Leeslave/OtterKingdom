using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일터 내용: 시설 생산 · 가공 · 왕국 창고 · 농장 설비 확장 (왕국 화면 [일터] 탭이 칩 4개 아래에 그린다).
/// 한가한 특성 주민의 자동 생산을 그대로 활용한다. 가공 · 창고는 가공을 배웠거나 창고에 무언가 있을 때만 보임
/// </summary>
public class ProductionSheet
{
    public enum Tab { Production, Processing, Warehouse, Equipment }

    private static readonly string[] Workplaces = { "bld_workshop", "bld_quarry" };
    private const float TileSize = 140f;
    private const float RightWidth = 190f;

    private readonly SettlementManager _manager;
    // 다시 그리기 (true = 스크롤 자리 유지)
    private readonly Action<bool> _redraw;
    private string _hint = "";
    private bool _hintFailed;
    private List<Action> _refreshes;

    public Tab Current { get; private set; }

    public ProductionSheet(SettlementManager manager, Action<bool> redraw)
    {
        _manager = manager;
        _redraw = redraw;
    }

    public List<Tab> VisibleTabs()
    {
        var tabs = new List<Tab> { Tab.Production };
        if (IndustrySection.IsVisible(_manager))
        {
            tabs.Add(Tab.Processing);
            tabs.Add(Tab.Warehouse);
        }
        tabs.Add(Tab.Equipment);
        return tabs;
    }

    public static string TabName(Tab tab)
    {
        switch (tab)
        {
            case Tab.Processing: return "가공";
            case Tab.Warehouse: return "창고";
            case Tab.Equipment: return "설비";
            default: return "생산";
        }
    }

    public void Select(Tab tab)
    {
        Current = tab;
        _hint = "";
    }

    /// <summary>고른 칸의 내용을 content에 그린다. 매초 부를 갱신은 refreshes에 넣음</summary>
    public void Draw(Transform content, List<Action> refreshes)
    {
        _refreshes = refreshes;
        RuntimeUIKit.PrepareGlyphs("일터와 설비 생산 가공 창고 설비 목공소 채석 작업소 주민 명이 일하는 중 쉬는 건설하면 시작해요 가방으로 먼저 1시간에 개 " +
                                   "정찰 개간 작업에 나간 해달은 생산을 쉬어요 가방이 차면 최대 8시간치까지 일터에 보관해요 " +
                                   "스프링클러 돌길 확장 완료 농부가 더 빨리 걷도록 흙길을 개선해요 밭에 놓을 수 있어요 했어요 조건과 비용을 확인해 주세요 " +
                                   IndustrySection.Glyphs);
        if (!VisibleTabs().Contains(Current))
            Current = Tab.Production;
        switch (Current)
        {
            case Tab.Processing:
                _refreshes.Add(IndustrySection.DrawProcessing(content, _manager, () => _redraw(true), () => SwitchTo(Tab.Warehouse)));
                break;
            case Tab.Warehouse:
                _manager.MarkWarehouseViewed();
                _refreshes.Add(IndustrySection.DrawWarehouse(content, _manager, (message, ok) =>
                {
                    _hint = message;
                    _hintFailed = !ok;
                    _redraw(true);
                }, _hint, _hintFailed));
                break;
            case Tab.Equipment:
                DrawEquipment(content);
                break;
            default:
                DrawProduction(content);
                break;
        }
    }

    private void SwitchTo(Tab tab)
    {
        Select(tab);
        _redraw(false);
    }

    #region 생산

    private void DrawProduction(Transform content)
    {
        foreach (var id in Workplaces)
            _refreshes.Add(Workplace(content, id));
        RuntimeUICards.Text(content, "정찰·개간·주민 작업에 나간 해달은 생산을 쉬어요.\n가방이 차면 최대 8시간치까지 일터에 보관해요.", 24, muted: true,
            alignment: TMPro.TextAlignmentOptions.Center);
    }

    private Action Workplace(Transform content, string id)
    {
        var building = _manager.FindBuilding(id);
        bool built = building != null && _manager.HasBuilt(id);
        string name = building != null ? building.DisplayName : id == "bld_workshop" ? "목공소" : "채석 작업소";
        var card = RuntimeUICards.Card(content, false, !built);
        RuntimeUICards.IconTile(card, building != null ? building.Icon : null, TileSize, !built);
        var text = RuntimeUICards.Column(card, 8f);
        RuntimeUICards.Text(text, name, 38, true);
        var status = RuntimeUICards.Text(text, "", 27, muted: true);
        if (!built)
        {
            status.text = "건설하면 생산을 시작해요";
            return null;
        }
        var flow = RuntimeUICards.Row(text, 0f);
        var destination = RuntimeUICards.Tag(flow, "", RuntimeUICards.TagKind.Plain);
        RuntimeUICards.Flexible(flow);

        // 오른쪽: 모으는 것 · 시간당 개수
        var item = building.ProductionItem;
        var right = RuntimeUICards.Column(card, 2f, RightWidth, TextAnchor.MiddleCenter);
        var amountRow = RuntimeUICards.Row(right, 8f);
        amountRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        if (item != null)
            RuntimeUICards.Icon(amountRow, item.Icon, 58f);
        var amount = RuntimeUICards.Text(amountRow, "", 46, true);
        var unit = RuntimeUICards.Text(right, "1시간에", 23, muted: true, alignment: TMPro.TextAlignmentOptions.Center);

        return () =>
        {
            int workers = ProductionRules.Workers(_manager.Config, _manager.Settlement, building);
            int perHour = _manager.ProductionPerHour(building);
            status.text = workers > 0 ? $"주민 {workers}명이 일하는 중" : "일하는 주민이 없어요";
            amount.text = workers > 0 ? perHour.ToString("N0") : "0";
            unit.text = workers > 0 ? "1시간에" : "쉬는 중";
            bool processing = _manager.IsProcessing(building);
            destination.Set(processing ? "가공 먼저 → 남으면 가방" : "가방으로 자동 수거",
                processing ? RuntimeUICards.TagKind.On : RuntimeUICards.TagKind.Plain);
        };
    }

    #endregion

    #region 설비

    private void DrawEquipment(Transform content)
    {
        if (!string.IsNullOrEmpty(_hint))
        {
            var note = RuntimeUICards.Text(content, _hint, 28, true, alignment: TMPro.TextAlignmentOptions.Center);
            note.color = _hintFailed ? RuntimeUIKit.Style.WarningColor : new Color32(0x5E, 0x8C, 0x55, 0xFF);
        }
        var sprinkler = _manager.FindBuilding("bld_farm_sprinkler");
        Equipment(content, false, sprinkler != null ? sprinkler.Icon : null);
        Equipment(content, true, _manager.FarmConfig != null ? _manager.FarmConfig.StonePathSprite : null);
    }

    private void Equipment(Transform content, bool stonePath, Sprite icon)
    {
        string name = stonePath ? "돌길 설비" : "스프링클러 설비";
        string locked = _manager.EquipmentHint(stonePath);
        bool done = _manager.HasDevelopment(stonePath ? SettlementManager.StonePathEquipment : SettlementManager.SprinklerEquipment);
        var card = RuntimeUICards.Card(content, false);
        RuntimeUICards.IconTile(card, icon, TileSize, locked != null && !done);
        var text = RuntimeUICards.Column(card, 8f);
        RuntimeUICards.Text(text, name, 38, true);
        RuntimeUICards.Text(text, stonePath ? "농부가 더 빨리 걷도록 흙길을 개선해요" : "밭에 스프링클러를 놓을 수 있어요", 25, muted: true);
        var right = RuntimeUICards.Column(card, 6f, RightWidth, TextAnchor.MiddleCenter);
        if (done)
        {
            RuntimeUICards.Tag(right, "확장 완료", RuntimeUICards.TagKind.On, 28);
            return;
        }

        int gold = stonePath ? 700 : 500;
        int woodCost = stonePath ? 10 : 15;
        int stoneCost = stonePath ? 15 : 5;
        var wood = _manager.FindBuilding("bld_workshop")?.ProductionItem;
        var stone = _manager.FindBuilding("bld_quarry")?.ProductionItem;
        var goldCurrency = _manager.Config.GoldCurrency;
        var costs = RuntimeUICards.Row(text, 18f);
        RuntimeUICards.Cost(costs, goldCurrency != null ? goldCurrency.Icon : null, $"{gold:N0}", _manager.HasEnoughGold(gold));
        RuntimeUICards.Cost(costs, wood != null ? wood.Icon : null, woodCost.ToString(), wood == null || _manager.ItemCount(wood) >= woodCost);
        RuntimeUICards.Cost(costs, stone != null ? stone.Icon : null, stoneCost.ToString(), stone == null || _manager.ItemCount(stone) >= stoneCost);
        RuntimeUICards.Flexible(costs);

        if (locked != null)
        {
            var hint = RuntimeUICards.Text(text, locked, 24);
            hint.color = RuntimeUIKit.Style.WarningColor;
            RuntimeUICards.Tag(right, "잠김", RuntimeUICards.TagKind.Plain, 26);
            return;
        }
        RuntimeUICards.Button(right, "확장", () =>
        {
            bool ok = _manager.TryUpgradeEquipment(stonePath);
            _hint = ok ? name + "를 확장했어요!" : "조건과 비용을 확인해 주세요.";
            _hintFailed = !ok;
            _redraw(true);
        }, RuntimeUIKit.ButtonKind.Feature, 170f, 96f, 36);
    }

    #endregion
}
