using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 일터 창(ProductionSheet)의 가공 탭 · 창고 탭 (번영의 시대 A).
/// 가공: 창고 요약 줄 + 시설마다 카드 (스위치로 켜고 끔 · 재료 줄 [+] · 완성 줄 [받기] · 다음 하나 · 수레 스위치).
/// 창고: 칸 요약 + 가방 모양 칸 격자. 버튼을 누르면 창을 다시 그리고(redraw), 숫자는 돌려준 refresh로 1초마다 고침
/// </summary>
public static class IndustrySection
{
    private const float TileSize = 120f;
    private const int GridColumns = 4;
    private const float SlotSize = 176f;

    // 레시피 기술 이름 (연구 데이터는 개발자 B. 없으면 ID 그대로)
    private static readonly Dictionary<string, string> TechNames = new Dictionary<string, string>
    {
        { TechIds.Sawmill, "제재 기술" },
        { TechIds.StoneCutting, "석재 규격화" },
        { TechIds.Warehouse, "공동 창고" },
        { TechIds.Cart, "운송 수레" },
    };

    public const string Glyphs = "가공 판재 석재 블록 제재 기술 규격화 공동 창고 운송 수레 연구 켜짐 꺼짐 받기 재료 완성 다음 까지 분 초 꽉 찼어요 " +
                                 "처음 켜면 바로 줘요 배우면 열려요 누르면 넣어요 있어요 칸 임시 보관 왕국 가방 대신 해달이 모은 목재 돌 로 와요 개가 모이면 만들어요 " +
                                 "창고로 저절로 옮겨요 대 비어 있어요 가공품은 이 아니라 여기에 모여요 집 개량 길 공공시설에 쓰여요 예약 " +
                                 "만들기 만들면 가방으로 꾸미기에서 광장에 놓아요 놓아 보세요 만들었어요 자재로 왕국 표지판 가로등 필요 " +
                                 "0123456789/()·%'→:!";

    public static string TechName(string techId) => TechNames.TryGetValue(techId, out var name) ? name : techId;

    /// <summary>가공 · 창고 탭을 보일까 (가공을 하나라도 배웠거나 창고에 무언가 있음)</summary>
    public static bool IsVisible(SettlementManager manager)
    {
        var config = manager.IndustryConfig;
        var warehouse = manager.Warehouse;
        if (config == null || warehouse == null)
            return false;
        if (warehouse.Total > 0)
            return true;
        foreach (var recipe in config.Recipes)
        {
            if (recipe != null && recipe.IsValid && TechGate.Has(recipe.TechId))
                return true;
        }
        return false;
    }

    #region 가공 탭

    /// <returns>숫자 고침</returns>
    public static Action DrawProcessing(Transform content, SettlementManager manager, Action redraw, Action openWarehouse)
    {
        var refreshes = new List<Action> { WarehouseSummary(content, manager, openWarehouse) };
        foreach (var recipe in manager.IndustryConfig.Recipes)
        {
            if (recipe != null && recipe.IsValid && manager.HasBuilt(recipe.Building.BuildingId))
                refreshes.Add(Recipe(content, manager, recipe, redraw));
        }
        return () =>
        {
            foreach (var refresh in refreshes)
                refresh?.Invoke();
        };
    }

    // 창고 한 줄: [창고 그림] 이름 · 막대 n/칸 · [창고]
    private static Action WarehouseSummary(Transform content, SettlementManager manager, Action openWarehouse)
    {
        var warehouse = manager.Warehouse;
        var card = RuntimeUICards.Card(content, false);
        RuntimeUICards.IconTile(card, WarehouseIcon(manager), 96f, filled: true);
        var text = RuntimeUICards.Column(card, 8f);
        RuntimeUICards.Text(text, WarehouseName(), 32, true);
        var row = RuntimeUICards.Row(text, 14f);
        var fill = RuntimeUICards.Bar(row, 24f);
        var count = RuntimeUICards.Text(row, "", 28, true);
        count.textWrappingMode = TextWrappingModes.NoWrap;
        RuntimeUICards.Button(card, "창고", openWarehouse, RuntimeUIKit.ButtonKind.Option, 150f, 84f);
        return () =>
        {
            count.text = $"{warehouse.Total}/{warehouse.Capacity}";
            count.color = warehouse.Free <= 0 ? RuntimeUIKit.Style.WarningColor : RuntimeUIKit.Style.TextColor;
            RuntimeUICards.SetBar(fill, warehouse.Capacity > 0 ? warehouse.Total / (float)warehouse.Capacity : 0f);
        };
    }

    private static Action Recipe(Transform content, SettlementManager manager, ProcessingRecipeDefinition recipe, Action redraw)
    {
        var building = recipe.Building;
        string output = recipe.Output.DisplayName;
        string input = recipe.Input.DisplayName;
        bool learned = TechGate.Has(recipe.TechId);
        var processor = manager.ProcessorOf(building);
        bool on = learned && processor.on;

        var card = RuntimeUICards.Card(content, true, !learned);
        // 머리: [판재] 판재 가공 / 목공소 · 목재 4 → 판재 1 · 6분 / 스위치
        var head = RuntimeUICards.Row(card, 20f);
        RuntimeUICards.IconTile(head, recipe.Output.Icon, TileSize, !learned, on);
        var text = RuntimeUICards.Column(head, 6f);
        RuntimeUICards.Text(text, $"{output} 가공", 38, true);
        RuntimeUICards.Text(text, $"{building.DisplayName} · {input} {recipe.InputCount}개 → {output} 1개", 25, muted: true);
        RuntimeUICards.Text(text, $"{Duration(recipe.CycleSeconds)}마다 1개", 25, muted: true);
        var right = RuntimeUICards.Column(head, 6f, 150f, TextAnchor.MiddleCenter);
        if (!learned)
        {
            RuntimeUICards.Tag(right, "연구 필요", RuntimeUICards.TagKind.Plain, 26);
            Note(card, $"연구에서 '{TechName(recipe.TechId)}'을 배우면 열려요");
            return null;
        }
        RuntimeUICards.Switch(right, on, () =>
        {
            manager.SetProcessing(building, !on);
            redraw();
        });
        RuntimeUICards.Text(right, on ? "켜짐" : "꺼짐", 24, true, !on, TextAlignmentOptions.Center);

        var rule = recipe.ToRecipe();
        if (!on)
        {
            string trial = processor.trialGiven ? "" : $"\n처음 켜면 {output} 1개를 바로 줘요!";
            Note(card, $"켜면 해달이 모은 {input}가 가방 대신 여기로 와요{trial}");
            if (processor.tray <= 0)
                return null;
        }

        var inset = RuntimeUICards.Inset(card, 14f);
        Action inputLine = null;
        if (on)
        {
            Meter(inset, recipe.Input.Icon, "재료", out var inputFill, out var inputCount, row =>
            {
                RuntimeUICards.Plus(row, () =>
                {
                    manager.LoadProcessorFromBag(building, int.MaxValue);
                    redraw();
                });
            });
            var bagNote = RuntimeUICards.Text(inset, "", 23, muted: true, alignment: TextAlignmentOptions.MidlineRight);
            inputLine = () =>
            {
                inputCount.text = $"{processor.input}/{rule.InputCapacity}";
                RuntimeUICards.SetBar(inputFill, processor.input / (float)rule.InputCapacity);
                int bag = BagCount(recipe.Input);
                bagNote.text = $"가방에 {input} {bag}개 · +를 누르면 넣어요";
            };
        }

        UnityEngine.UI.Button collect = null;
        Meter(inset, recipe.Output.Icon, "완성", out var outputFill, out var outputCount, row =>
        {
            collect = RuntimeUICards.Button(row, "받기", () =>
            {
                manager.CollectProcessed(building);
                redraw();
            }, RuntimeUIKit.ButtonKind.Primary, 132f, 80f, 30);
        });

        TextMeshProUGUI next = null;
        if (on)
        {
            var foot = RuntimeUICards.Row(card, 14f);
            next = RuntimeUICards.Text(foot, "", 25, muted: true);
            next.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            Cart(foot, manager, building, redraw);
        }

        return () =>
        {
            inputLine?.Invoke();
            bool full = processor.tray >= rule.TrayCapacity;
            outputCount.text = $"{processor.tray}/{rule.TrayCapacity}";
            outputCount.color = full ? RuntimeUIKit.Style.WarningColor : RuntimeUIKit.Style.TextColor;
            RuntimeUICards.SetBar(outputFill, processor.tray / (float)rule.TrayCapacity);
            collect.interactable = processor.tray > 0;
            if (next != null)
            {
                next.text = full ? $"{output}이 꽉 찼어요 · 받기를 눌러 창고로"
                    : processor.input < recipe.InputCount ? $"{input} {recipe.InputCount}개가 모이면 만들어요"
                    : $"다음 {output}까지 {Duration(Mathf.Max(0f, recipe.CycleSeconds - processor.progress))}";
            }
        };
    }

    // [아이콘] 이름 [막대] n/칸 [버튼]
    private static void Meter(Transform inset, Sprite icon, string caption, out RectTransform fill, out TextMeshProUGUI count,
        Action<Transform> action)
    {
        var row = RuntimeUICards.Row(inset, 14f, 80f);
        RuntimeUICards.Icon(row, icon, 56f);
        var label = RuntimeUICards.Text(row, caption, 26, true, true);
        label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 66f;
        fill = RuntimeUICards.Bar(row, 26f);
        count = RuntimeUICards.Text(row, "", 30, true, alignment: TextAlignmentOptions.MidlineRight);
        count.textWrappingMode = TextWrappingModes.NoWrap;
        count.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 110f;
        action(row);
    }

    // 수레 스위치 (운송 수레를 배운 뒤)
    private static void Cart(Transform row, SettlementManager manager, BuildingDefinition building, Action redraw)
    {
        int limit = manager.CartRouteLimit;
        if (limit <= 0)
            return;
        bool on = manager.HasCartRoute(building);
        int used = manager.Settlement.Industry.cartRoutes.Count;
        RuntimeUICards.Text(row, on ? "수레" : $"수레 {used}/{limit}", 25, true, !on);
        var toggle = RuntimeUICards.Switch(row, on, () =>
        {
            manager.SetCartRoute(building, !on);
            redraw();
        });
        toggle.interactable = on || used < limit;
    }

    private static void Note(Transform card, string text)
    {
        RuntimeUICards.Text(card, text, 25, muted: true, alignment: TextAlignmentOptions.Center);
    }

    #endregion

    #region 창고 탭

    /// <param name="notify">만들기 결과 알림 (글, 성공) — 창이 다시 그리며 hint로 보여 줌</param>
    /// <returns>숫자 고침</returns>
    public static Action DrawWarehouse(Transform content, SettlementManager manager, Action<string, bool> notify, string hint, bool hintFailed)
    {
        var warehouse = manager.Warehouse;
        var config = manager.IndustryConfig;
        if (warehouse == null || config == null)
            return null;

        // 칸 요약
        var card = RuntimeUICards.Card(content, false);
        RuntimeUICards.IconTile(card, WarehouseIcon(manager), TileSize, filled: true);
        var text = RuntimeUICards.Column(card, 8f);
        RuntimeUICards.Text(text, WarehouseName(), 38, true);
        var row = RuntimeUICards.Row(text, 14f);
        var fill = RuntimeUICards.Bar(row, 28f);
        var count = RuntimeUICards.Text(row, "", 32, true);
        count.textWrappingMode = TextWrappingModes.NoWrap;
        if (!TechGate.Has(TechIds.Warehouse))
            RuntimeUICards.Text(text, $"'{TechName(TechIds.Warehouse)}'을 배우면 {config.WarehouseCapacity}칸이 돼요", 24, muted: true);

        // 칸 격자 (가방 모양): 가진 것 + 빈 칸으로 줄을 채움
        var gridCard = RuntimeUICards.Card(content, true);
        var grid = RuntimeUICards.Grid(gridCard, GridColumns, SlotSize, 40f);
        int shown = 0;
        foreach (var (itemId, amount) in warehouse.Items())
        {
            var item = config.FindOutput(itemId);
            int reserved = warehouse.Reserved(itemId);
            string name = item != null ? item.DisplayName : itemId;
            RuntimeUICards.Slot(grid, item != null ? item.Icon : null, amount.ToString("N0"), reserved > 0 ? $"{name} (예약 {reserved})" : name, SlotSize);
            shown++;
        }
        int slots = Mathf.Max(GridColumns * 2, (shown + GridColumns - 1) / GridColumns * GridColumns);
        for (int i = shown; i < slots; i++)
            RuntimeUICards.Slot(grid, null, "", "", SlotSize);
        RuntimeUICards.Text(gridCard, shown > 0 ? "가공품은 가방이 아니라 여기에 모여요 · 집 개량 · 길 · 공공시설에 쓰여요" : "비어 있어요 · 가공 탭에서 만든 가공품이 여기에 모여요",
            24, muted: true, alignment: TextAlignmentOptions.Center);

        // 창고 자재로 만들기 (마을 정비 C01)
        if (config.Crafts.Count > 0)
        {
            RuntimeUICards.Text(content, "창고 자재로 만들기", 34, true);
            if (!string.IsNullOrEmpty(hint))
            {
                var note = RuntimeUICards.Text(content, hint, 27, true, alignment: TextAlignmentOptions.Center);
                note.color = hintFailed ? RuntimeUIKit.Style.WarningColor : new Color32(0x5E, 0x8C, 0x55, 0xFF);
            }
            foreach (var craft in config.Crafts)
            {
                if (craft != null && craft.Item != null)
                    Craft(content, manager, craft, notify);
            }
        }

        return () =>
        {
            count.text = $"{warehouse.Total}/{warehouse.Capacity}";
            count.color = warehouse.Free <= 0 ? RuntimeUIKit.Style.WarningColor : RuntimeUIKit.Style.TextColor;
            RuntimeUICards.SetBar(fill, warehouse.Capacity > 0 ? warehouse.Total / (float)warehouse.Capacity : 0f);
        };
    }

    // [아이콘] 이름 · 설명 · 비용 / [만들기]
    private static void Craft(Transform content, SettlementManager manager, CraftRecipe craft, Action<string, bool> notify)
    {
        bool learned = string.IsNullOrEmpty(craft.TechId) || TechGate.Has(craft.TechId);
        var card = RuntimeUICards.Card(content, false, !learned);
        RuntimeUICards.IconTile(card, craft.Item.Icon, TileSize, !learned);
        var text = RuntimeUICards.Column(card, 8f);
        RuntimeUICards.Text(text, craft.Item.DisplayName, 36, true);
        RuntimeUICards.Text(text, learned ? "만들면 가방으로 · 꾸미기에서 광장에 놓아요" : $"연구에서 '{TechName(craft.TechId)}'을 배우면 열려요", 24, muted: true);
        var costs = RuntimeUICards.Row(text, 18f);
        var warehouse = manager.Warehouse;
        var config = manager.IndustryConfig;
        if (craft.Planks > 0)
        {
            var plank = config.FindOutput(SettlementManager.PlankItemId);
            RuntimeUICards.Cost(costs, plank != null ? plank.Icon : null, craft.Planks.ToString(), warehouse.Available(SettlementManager.PlankItemId) >= craft.Planks);
        }
        if (craft.StoneBlocks > 0)
        {
            var stone = config.FindOutput(SettlementManager.StoneBlockItemId);
            RuntimeUICards.Cost(costs, stone != null ? stone.Icon : null, craft.StoneBlocks.ToString(), warehouse.Available(SettlementManager.StoneBlockItemId) >= craft.StoneBlocks);
        }
        if (craft.Gold > 0)
        {
            var gold = manager.Config.GoldCurrency;
            RuntimeUICards.Cost(costs, gold != null ? gold.Icon : null, craft.Gold.ToString("N0"), manager.HasEnoughGold(craft.Gold));
        }
        RuntimeUICards.Flexible(costs);

        var right = RuntimeUICards.Column(card, 6f, 170f, TextAnchor.MiddleCenter);
        if (!learned)
        {
            RuntimeUICards.Tag(right, "연구 필요", RuntimeUICards.TagKind.Plain, 26);
            return;
        }
        var button = RuntimeUICards.Button(right, "만들기", () =>
        {
            string why = manager.CraftHint(craft);
            bool ok = manager.TryCraft(craft);
            notify(ok ? $"{SettlementManager.EulReul(craft.Item.DisplayName)} 만들었어요! 꾸미기에서 놓아 보세요" : why ?? "만들 수 없어요", ok);
        }, RuntimeUIKit.ButtonKind.Primary, 160f, 92f, 32);
        button.interactable = manager.CraftHint(craft) == null;
    }

    #endregion

    private static string WarehouseName() => TechGate.Has(TechIds.Warehouse) ? "왕국 창고" : "임시 보관 칸";

    // 창고 그림: 농업 창고 건물 (없으면 첫 가공품)
    private static Sprite WarehouseIcon(SettlementManager manager)
    {
        var granary = manager.FindBuilding("bld_granary");
        if (granary != null && granary.Icon != null)
            return granary.Icon;
        var recipes = manager.IndustryConfig.Recipes;
        return recipes.Count > 0 && recipes[0] != null && recipes[0].Output != null ? recipes[0].Output.Icon : null;
    }

    private static int BagCount(ItemDefinition item)
    {
        var inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        return inventory != null ? inventory.GetCount(item) : 0;
    }

    private static string Duration(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        if (total < 60)
            return $"{total}초";
        return total % 60 == 0 ? $"{total / 60}분" : $"{total / 60}분 {total % 60:00}초";
    }
}
