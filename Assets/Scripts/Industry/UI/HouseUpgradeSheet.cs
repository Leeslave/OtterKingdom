using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주택 개량 창 (번영의 시대 C02): 광장의 작은 집을 누르면 열림. 지금 집 → 개량 주택 그림, 입주민, 비용(창고 판재 · 골드), [개량하기].
/// 같은 집 · 같은 입주민 그대로 그림만 바뀜 (SettlementManager.TryImproveHouse)
/// </summary>
public class HouseUpgradeSheet : MonoBehaviour
{
    private const float TileSize = 220f;

    private Canvas _canvas;
    private SettlementManager _manager;
    private PlacedBuildingView _house;
    private Action _improved;

    public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

    public void Open(SettlementManager manager, PlacedBuildingView house, Action improved)
    {
        _manager = manager;
        _house = house;
        _improved = improved;
        if (_canvas == null)
            _canvas = RuntimeUIKit.CreateCanvas("주택 개량", 235, false);
        _canvas.gameObject.SetActive(true);
        manager.Settlement.UnlockDevelopment(SettlementManager.HouseViewedDevelopment);
        Draw().PopIn(this);
    }

    public void Close()
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_canvas != null)
            Destroy(_canvas.gameObject);
    }

    private RuntimeUIWindow Draw()
    {
        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _canvas.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        var config = _manager.IndustryConfig;
        int id = _house.Placed.InstanceId;
        bool improved = _manager.IsImprovedHouse(id);
        bool town = _manager.IsTownHouse(id);
        // 단계: 작은 집 → 개량 주택(C02) → 마을 주택(C05)
        var small = _house.Building.FinishedSprite(_house.Placed.Rotation);
        var before = improved ? config.ImprovedHouseSprite(id) : small;
        var after = improved ? config.TownHouseIcon(id) : config.ImprovedHouseSprite(id);
        var window = RuntimeUIWindow.Create(_canvas.transform, "주택 개량", town ? after : before, null, 0, null, Close);
        var content = window.Content;

        // 지금 집 → 다음 단계
        var card = RuntimeUICards.Card(content, true);
        var row = RuntimeUICards.Row(card, 18f);
        row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        RuntimeUICards.Flexible(row);
        RuntimeUICards.IconTile(row, before, TileSize, filled: !town);
        var arrow = RuntimeUICards.Text(row, "→", 64, true, alignment: TextAlignmentOptions.Center);
        arrow.textWrappingMode = TextWrappingModes.NoWrap;
        RuntimeUICards.IconTile(row, after, TileSize, filled: town);
        RuntimeUICards.Flexible(row);

        string resident = ResidentName(id);
        string stays = string.IsNullOrEmpty(resident) ? "" : $"{EunNeun(resident)} 그대로 살아요 · ";
        string title = town ? "마을 주택이에요" : improved ? "마을 주택으로 바꿔요" : "개량 주택으로 바꿔요";
        string what = improved ? "현관에 등불과 꽃 화분을 놓아요" : "지붕 · 창문 · 마당이 새로 바뀌어요";
        RuntimeUICards.Text(card, title, 40, true, alignment: TextAlignmentOptions.Center);
        RuntimeUICards.Text(card, stays + what, 26, muted: true, alignment: TextAlignmentOptions.Center);
        var steps = RuntimeUICards.Row(card, 10f);
        RuntimeUICards.Flexible(steps);
        RuntimeUICards.Tag(steps, "1단계 개량 주택", improved ? RuntimeUICards.TagKind.On : RuntimeUICards.TagKind.Plain, 24);
        RuntimeUICards.Tag(steps, "2단계 마을 주택", town ? RuntimeUICards.TagKind.On : RuntimeUICards.TagKind.Plain, 24);
        RuntimeUICards.Flexible(steps);
        if (town)
            return window;

        // 비용 (창고 자재 · 골드)
        var costCard = RuntimeUICards.Card(content, false);
        RuntimeUICards.Text(costCard, "드는 것", 32, true).gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;
        var costs = RuntimeUICards.Row(costCard, 26f);
        int gold;
        string hint;
        if (improved)
        {
            Cost(costs, config, SettlementManager.StoneBlockItemId, config.TownHouseStoneBlocks);
            Cost(costs, config, SettlementManager.IngotItemId, config.TownHouseIngots);
            gold = config.TownHouseGold;
            hint = _manager.TownHouseHint(id);
        }
        else
        {
            Cost(costs, config, SettlementManager.PlankItemId, config.HousePlanks);
            gold = config.HouseGold;
            hint = _manager.HouseUpgradeHint(id);
        }
        var goldCurrency = _manager.Config.GoldCurrency;
        RuntimeUICards.Cost(costs, goldCurrency != null ? goldCurrency.Icon : null, gold.ToString("N0"), _manager.HasEnoughGold(gold));
        RuntimeUICards.Flexible(costCard);

        if (hint != null)
        {
            var note = RuntimeUICards.Text(content, hint, 28, true, alignment: TextAlignmentOptions.Center);
            note.color = RuntimeUIKit.Style.WarningColor;
        }
        var buttonRow = RuntimeUICards.Row(content, 0f);
        RuntimeUICards.Flexible(buttonRow);
        var button = RuntimeUICards.Button(buttonRow, improved ? "마을 주택으로" : "개량하기", improved ? (System.Action)MakeTownHouse : Improve,
            RuntimeUIKit.ButtonKind.Primary, 420f, 110f, 40);
        button.interactable = hint == null;
        RuntimeUICards.Flexible(buttonRow);
        return window;
    }

    // 창고 자재 한 칸: 아이콘 · 드는 수 (창고에 있는 수)
    private void Cost(Transform row, IndustryConfig config, string itemId, int amount)
    {
        if (amount <= 0)
            return;
        var item = config.FindOutput(itemId);
        int have = _manager.Warehouse != null ? _manager.Warehouse.Available(itemId) : 0;
        RuntimeUICards.Cost(row, item != null ? item.Icon : null, $"{amount} (창고 {have})", have >= amount);
    }

    private void Improve()
    {
        if (!_manager.TryImproveHouse(_house.Placed.InstanceId))
        {
            Draw();
            return;
        }
        Close();
        _improved?.Invoke();
    }

    private void MakeTownHouse()
    {
        if (!_manager.TryMakeTownHouse(_house.Placed.InstanceId))
        {
            Draw();
            return;
        }
        Close();
        _improved?.Invoke();
    }

    // 이름 + 은/는 (받침 있으면 은)
    private static string EunNeun(string word)
    {
        char last = word[word.Length - 1];
        bool batchim = last >= '가' && last <= '힣' && (last - '가') % 28 != 0;
        return word + (batchim ? "은" : "는");
    }

    private string ResidentName(int instanceId)
    {
        var house = _manager.Settlement.FindHouseBySlot(BuildingRules.HomeSlotId(instanceId));
        var otter = house != null && !string.IsNullOrEmpty(house.ResidentId) ? _manager.Config.FindOtter(house.ResidentId) : null;
        return otter != null ? otter.DisplayName : null;
    }
}
