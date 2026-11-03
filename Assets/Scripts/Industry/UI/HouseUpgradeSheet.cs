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
        var before = _house.Building.FinishedSprite(_house.Placed.Rotation);
        var after = config.ImprovedHouseSprite(id);
        var window = RuntimeUIWindow.Create(_canvas.transform, "주택 개량", improved ? after : before, null, 0, null, Close);
        var content = window.Content;

        // 지금 집 → 개량 주택
        var card = RuntimeUICards.Card(content, true);
        var row = RuntimeUICards.Row(card, 18f);
        row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        RuntimeUICards.Flexible(row);
        RuntimeUICards.IconTile(row, before, TileSize, filled: !improved);
        var arrow = RuntimeUICards.Text(row, "→", 64, true, alignment: TextAlignmentOptions.Center);
        arrow.textWrappingMode = TextWrappingModes.NoWrap;
        RuntimeUICards.IconTile(row, after, TileSize, filled: improved);
        RuntimeUICards.Flexible(row);

        string resident = ResidentName(id);
        RuntimeUICards.Text(card, improved ? "개량 주택이에요" : "개량 주택으로 바꿔요", 40, true, alignment: TextAlignmentOptions.Center);
        RuntimeUICards.Text(card, string.IsNullOrEmpty(resident)
                ? "지붕 · 창문 · 마당이 새로 바뀌어요"
                : $"{EunNeun(resident)} 그대로 살아요 · 지붕 · 창문 · 마당이 새로 바뀌어요",
            26, muted: true, alignment: TextAlignmentOptions.Center);

        if (improved)
        {
            var done = RuntimeUICards.Row(card, 0f);
            RuntimeUICards.Flexible(done);
            RuntimeUICards.Tag(done, "개량 완료", RuntimeUICards.TagKind.On, 30);
            RuntimeUICards.Flexible(done);
            return window;
        }

        // 비용
        var costCard = RuntimeUICards.Card(content, false);
        RuntimeUICards.Text(costCard, "드는 것", 32, true).gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;
        var costs = RuntimeUICards.Row(costCard, 26f);
        var plank = config.FindOutput(SettlementManager.PlankItemId);
        int planks = _manager.Warehouse != null ? _manager.Warehouse.Available(SettlementManager.PlankItemId) : 0;
        RuntimeUICards.Cost(costs, plank != null ? plank.Icon : null, $"{config.HousePlanks} (창고 {planks})", planks >= config.HousePlanks);
        var gold = _manager.Config.GoldCurrency;
        RuntimeUICards.Cost(costs, gold != null ? gold.Icon : null, config.HouseGold.ToString("N0"), _manager.HasEnoughGold(config.HouseGold));
        RuntimeUICards.Flexible(costCard);

        string hint = _manager.HouseUpgradeHint(id);
        if (hint != null)
        {
            var note = RuntimeUICards.Text(content, hint, 28, true, alignment: TextAlignmentOptions.Center);
            note.color = RuntimeUIKit.Style.WarningColor;
        }
        var buttonRow = RuntimeUICards.Row(content, 0f);
        RuntimeUICards.Flexible(buttonRow);
        var button = RuntimeUICards.Button(buttonRow, "개량하기", Improve, RuntimeUIKit.ButtonKind.Primary, 420f, 110f, 40);
        button.interactable = hint == null;
        RuntimeUICards.Flexible(buttonRow);
        return window;
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
