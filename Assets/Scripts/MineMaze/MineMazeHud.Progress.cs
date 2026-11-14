using System;
using UnityEngine;
using UnityEngine.UI;

public partial class MineMazeHud
{
    private Action _progressRefresh;

    private void BuildWorkshopButton()
    {
        var button = RuntimeUIKit.CreateButton(_safe, "작업대", () =>
        {
            if (!_game.Busy) ShowWorkshop();
        });
        Place((RectTransform)button.transform, new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-24, 0), new Vector2(190, 60));
    }

    public void ShowWorkshop()
    {
        var panel = OpenSheet(880);
        Line(panel, "곡괭이 작업대", 48, true, Cocoa);
        var balance = Line(panel, "", 30, true, Cocoa);
        Line(panel, $"현재: {_model.Pickaxe.DisplayName} · 힘 {_model.Pickaxe.Power}", 28, false, Cocoa);
        var next = _model.NextPickaxe;
        if (next != null)
        {
            Line(panel, $"다음: {next.DisplayName} · 힘 {next.Power}", 32, true, Cocoa);
            Line(panel, $"제작 비용 {next.GoldCost} 골드", 28, false, Cocoa);
            var ingredients = new System.Collections.Generic.List<Action>();
            foreach (var cost in next.Recipe)
                if (cost != null && cost.Ore != null)
                {
                    var label = Line(panel, "", 28, false, Cocoa);
                    ingredients.Add(() => label.text = $"{cost.Ore.DisplayName}: {_model.Count(cost.Ore)} / {cost.Amount}");
                }
            var craft = RuntimeUIKit.CreateButton(panel, "제작하기", () =>
            {
                if (_game.CraftPickaxe()) ShowWorkshop();
            }, RuntimeUIKit.ButtonKind.Primary);
            var hint = Line(panel, "", 26, false, Cocoa);
            _progressRefresh = () =>
            {
                balance.text = $"골드 {_model.Gold}";
                foreach (var update in ingredients) update();
                craft.interactable = _model.CanCraft(out string reason);
                hint.text = reason;
            };
        }
        else
        {
            Line(panel, "이번 탐험의 곡괭이를 모두 준비했어요!", 28, false, Good);
            _progressRefresh = () => balance.text = $"골드 {_model.Gold}";
        }
        if (_game.InGame && _model.WaitingForBag > 0)
            Line(panel, $"가방이 가득 차서 광석 {_model.WaitingForBag}개가 기다리는 중이에요. 가방에 자리가 나면 저절로 들어가요.", 24, false, Cocoa);
        if (_game.InGame && InventoryManager.Instance != null)
            RuntimeUIKit.CreateButton(panel, "가방 칸 늘리기", InventoryExpansionSheet.Open);
        RuntimeUIKit.CreateButton(panel, "남는 광석 판매", ShowOreSales);
        RuntimeUIKit.CreateButton(panel, "돌아가기", ReturnToPlay, RuntimeUIKit.ButtonKind.Secondary);
        _progressRefresh?.Invoke();
    }

    private void ShowOreSales()
    {
        var panel = OpenSheet(880);
        Line(panel, "광석 판매", 46, true, Cocoa);
        Line(panel, $"골드 {_model.Gold} · 제작 재료는 남겨 두세요", 28, false, Cocoa);
        // One row per sellable ore in this floor, including rare chest rewards.
        foreach (var ore in _model.Config.Rework.Ores)
        {
            if (ore == null || !ore.CanSell || _model.Count(ore) == 0) continue;
            var captured = ore;
            RuntimeUIKit.CreateButton(panel, $"{ore.DisplayName} {_model.Count(ore):N0}개 · 수량 고르기", () => ShowOreQuantity(captured), height: 90);
        }
        RuntimeUIKit.CreateButton(panel, "작업대로", ShowWorkshop, RuntimeUIKit.ButtonKind.Secondary);
    }

    private void ShowOreQuantity(MineOreDefinition ore)
    {
        var panel = OpenSheet(880);
        Line(panel, $"{ore.DisplayName} 판매", 44, true, Cocoa);
        Line(panel, "곡괭이 제작에 쓸 재료는 남겨 주세요.", 28, false, Cocoa);
        int amount = 1;
        var count = Line(panel, "", 32, true, Cocoa);
        var row = RuntimeUIKit.CreateRow(panel, RuntimeUIKit.ButtonHeight);
        RuntimeUIKit.CreateButton(row, "-1", () => { amount = Math.Max(1, amount - 1); _progressRefresh?.Invoke(); }, flexible: true);
        RuntimeUIKit.CreateButton(row, "+1", () => { amount = Math.Min(_model.Count(ore), amount + 1); _progressRefresh?.Invoke(); }, flexible: true);
        RuntimeUIKit.CreateButton(row, "+10", () => { amount = Math.Min(_model.Count(ore), amount + 10); _progressRefresh?.Invoke(); }, flexible: true);
        RuntimeUIKit.CreateButton(row, "최대", () => { amount = _model.Count(ore); _progressRefresh?.Invoke(); }, flexible: true);
        var result = Line(panel, "", 28, false, Good);
        var sell = RuntimeUIKit.CreateButton(panel, "판매", () =>
        {
            int quantity = amount;
            if (_game.SellOre(ore, quantity))
                result.text = $"{ore.DisplayName} {quantity:N0}개를 팔았어요. +{(long)ore.SellPrice * quantity:N0}골드";
            else result.text = "지금은 팔 수 없어요. 보유 수량과 골드를 확인해 주세요.";
            // 선택한 광석 화면을 유지한다. 연타해도 다른 종류로 바뀌지 않는다.
            _progressRefresh?.Invoke();
        }, RuntimeUIKit.ButtonKind.Primary);
        _progressRefresh = () =>
        {
            amount = Math.Min(Math.Max(1, amount), _model.Count(ore));
            count.text = $"보유 {_model.Count(ore):N0}개 · 선택 {amount:N0}개";
            sell.interactable = amount > 0;
            RuntimeUIKit.SetButtonLabel(sell, $"{amount:N0}개 판매 · +{(long)ore.SellPrice * amount:N0}G");
        };
        RuntimeUIKit.CreateButton(panel, "다른 광석 고르기", ShowOreSales, RuntimeUIKit.ButtonKind.Secondary);
        _progressRefresh();
    }

    private void ReturnToPlay()
    {
        if (_model.Inside) CloseSheet();
        else ShowEntrance();
    }

    public void ShowGate()
    {
        if (_model.AtContentEnd && !_model.IsSourceFloor)
        {
            var end = OpenSheet(880);
            Line(end, "더 깊은 갱도", 46, true, Cocoa);
            Line(end, "3층 끝까지 도착했어요!\n4층은 아직 닫혀 있어요. 남은 광맥과 상자를 찾아보세요.", 30, false, Cocoa);
            RuntimeUIKit.CreateButton(end, "3층으로", ReturnToPlay);
            return;
        }
        if (_model.FloorCleared) { ShowNextEntrance(); return; }
        var panel = OpenSheet(880);
        Line(panel, _model.IsSourceFloor ? "가스 발생 돌" : $"지하 {_model.CurrentFloor + 2}층으로 가는 길", 46, true, Cocoa);
        var rock = RuntimeUIKit.CreateIcon(panel, _art.RockStage(0), 200);
        var progress = Line(panel, "", 36, true, Cocoa);
        var hint = Line(panel, "", 27, false, Cocoa);
        var hit = RuntimeUIKit.CreateButton(panel, "돌 두드리기", () =>
        {
            _game.HitGate();
            if (_model.FloorCleared) ShowNextEntrance();
        }, RuntimeUIKit.ButtonKind.Primary);
        _progressRefresh = () =>
        {
            progress.text = $"돌파 {_model.GateDamage} / {_model.Config.GateHits}";
            rock.sprite = _art.RockStage(Mathf.Min(2, _model.GateDamage * 3 / _model.Config.GateHits));
            hit.interactable = _model.CanHitGate(out string reason);
            hint.text = hit.interactable ? $"친 만큼 저장돼요. 완성하면 {_model.CurrentFloor + 1}층 자동 채굴이 열려요." : reason;
        };
        RuntimeUIKit.CreateButton(panel, "작업대", ShowWorkshop);
        RuntimeUIKit.CreateButton(panel, "돌아가기", ReturnToPlay, RuntimeUIKit.ButtonKind.Secondary);
        _progressRefresh();
    }

    public void ShowNextEntrance()
    {
        var panel = OpenSheet(880);
        Line(panel, _model.IsSourceFloor ? "발생원을 제거했어요!" : $"지하 {_model.CurrentFloor + 2}층 입구", 48, true, Cocoa);
        RuntimeUIKit.CreateIcon(panel, _mazeArt.Stairs, 180);
        string reward = _model.IsSourceFloor ? "발생원을 없애 마을 주민들이 회복해요."
            : _model.CurrentFloor == 2 ? "심층 갱도가 열렸어요. 지하 4층에서 마을에 영향을 주는 단서를 찾을 수 있어요."
            : "새 광맥이 열렸어요. 깡깡이의 자동 채굴 광석도 늘어났어요.";
        Line(panel, reward, 30, true, Good);
        var timer = Line(panel, "", 28, false, Cocoa);
        _progressRefresh = () => timer.text = $"자동 채굴: {_model.MinerInterval:0.#}초마다 1개\n다음 채굴까지 {_model.MinerRemaining:0}초 · 작업대에서 보유량 확인";
        _progressRefresh();
        if (_model.CurrentFloor + 1 < _model.FloorCount)
            RuntimeUIKit.CreateButton(panel, "다음 층 들어가기", () =>
            {
                _game.SelectFloor(_model.CurrentFloor + 1);
                CloseSheet();
                if (!_model.Inside) _game.Enter();
                else ShowFloorTitle();
            }, RuntimeUIKit.ButtonKind.Primary);
        else if (_game.InGame && _model.IsSourceFloor)
            RuntimeUIKit.CreateButton(panel, "또박이에게 소식 전하기", _game.ReturnToPlazaForMineStory, RuntimeUIKit.ButtonKind.Primary);
        RuntimeUIKit.CreateButton(panel, "작업대 · 보유 광석", ShowWorkshop);
        RuntimeUIKit.CreateButton(panel, "돌아가기", ReturnToPlay, RuntimeUIKit.ButtonKind.Secondary);
    }

    public void RebuildFloor()
    {
        foreach (var flyer in _flyers) if (flyer.Image != null) Destroy(flyer.Image.gameObject);
        _flyers.Clear();
        _pending.Clear();
        if (_mapPanel != null) { _mapPanel.gameObject.SetActive(false); Destroy(_mapPanel.gameObject); }
        BuildMap();
        RebuildStrip();
        _floorTag.text = _floorTitleNumber.text = _model.Config.FloorName;
        _floorName.text = _titleText.text = _model.Layer.DisplayName;
        _titleSub.text = _model.Layer.Subtitle;
        LayoutBelowView();
        Refresh();
        RefreshMap();
    }

    private void ShowFloorSelection()
    {
        var panel = OpenSheet(880);
        Line(panel, "탐험할 층", 46, true, Cocoa);
        for (int i = 0; i < _model.FloorCount; i++)
        {
            int floor = i;
            var config = _model.FloorConfig(i);
            var button = RuntimeUIKit.CreateButton(panel, $"{config.FloorName} · {config.Layer.DisplayName}" + (i > _model.UnlockedFloor ? " (잠김)" : ""), () =>
            {
                _game.SelectFloor(floor);
                ShowEntrance();
            });
            button.interactable = i <= _model.UnlockedFloor;
        }
        RuntimeUIKit.CreateButton(panel, "돌아가기", ShowEntrance, RuntimeUIKit.ButtonKind.Secondary);
    }
}
