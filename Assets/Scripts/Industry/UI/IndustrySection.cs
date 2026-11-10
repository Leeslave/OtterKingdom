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
        { TechIds.Dispatch, "운송 배차" },
        { TechIds.Smelting, "금속 제련" },
        { TechIds.StandardTools, "표준 공구" },
        { TechIds.WaterPower, "수력 기계" },
        { TechIds.PrecisionWork, "정밀 공방" },
        { TechIds.BetterCart, "개량 수레" },
        { TechIds.IndustrialLogistics, "산업 물류 체계" },
        { TechIds.AutoSupply, "원료 자동 공급" },
        { TechIds.Depot, "집산소" },
        { TechIds.TownPlanning, "도시 정비" },
        { TechIds.BetterHouses, "주거 개량" },
        { TechIds.Paving, "도로 포장" },
        { TechIds.PublicSpaces, "공공 시설" },
        { TechIds.Administration, "왕국 행정" },
    };

    public const string Glyphs = "가공 판재 석재 블록 제재 기술 규격화 공동 창고 운송 수레 연구 켜짐 꺼짐 받기 재료 완성 다음 까지 분 초 꽉 찼어요 " +
                                 "처음 켜면 바로 줘요 배우면 열려요 누르면 넣어요 있어요 칸 임시 보관 왕국 가방 대신 해달이 모은 목재 돌 로 와요 개가 모이면 만들어요 " +
                                 "창고로 저절로 옮겨요 대 비어 있어요 가공품은 이 아니라 여기에 모여요 집 개량 길 공공시설에 쓰여요 예약 " +
                                 "깔기 공사 완료 마쳤어요 닫으면 보여요 중앙 돌길 흙 마당 돌을 깔아요 산업 현황 꺼짐 꽉 참 가공 중 재료 대기 쌓인 창고 재료 바로 꺼내요 왕국 정밀 부품 모이면 자동 넣기 남겨요 속도 가방의 으로 로 이 금속 주괴 조개 구리 정밀 부품 만들기 만들면 가방으로 꾸미기에서 광장에 놓아요 놓아 보세요 만들었어요 자재로 왕국 표지판 가로등 필요 " +
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
        if (TechGate.Has(TechIds.IndustrialLogistics))
            refreshes.Add(Overview(content, manager));
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

    // 산업 현황 (X01 산업 물류 체계): 가공 줄마다 상태 · 시간당 · 쌓인 수를 한눈에
    private static Action Overview(Transform content, SettlementManager manager)
    {
        var card = RuntimeUICards.Card(content, true);
        RuntimeUICards.Text(card, "산업 현황", 34, true);
        var rows = new List<Action>();
        foreach (var recipe in manager.IndustryConfig.Recipes)
        {
            if (recipe == null || !recipe.IsValid || !manager.HasBuilt(recipe.Building.BuildingId) || !TechGate.Has(recipe.TechId))
                continue;
            var row = RuntimeUICards.Row(card, 14f, 64f);
            RuntimeUICards.Icon(row, recipe.Output.Icon, 52f);
            var name = RuntimeUICards.Text(row, recipe.Output.DisplayName, 28, true);
            name.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 190f;
            var state = RuntimeUICards.Tag(row, "", RuntimeUICards.TagKind.Plain, 22);
            var numbers = RuntimeUICards.Text(row, "", 25, muted: true, alignment: TextAlignmentOptions.MidlineRight);
            numbers.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            var processor = manager.ProcessorOf(recipe);
            var rule = recipe.ToRecipe();
            rows.Add(() =>
            {
                var (label, kind) = StateOf(manager, recipe, processor, rule);
                state.Set(label, kind);
                numbers.text = $"시간당 {PerHour(manager, recipe):0.#} · 쌓인 {processor.tray}";
            });
        }
        if (rows.Count == 0)
            Note(card, "가공을 켠 시설이 여기에 모여요");
        return () =>
        {
            foreach (var row in rows)
                row();
        };
    }

    private static (string label, RuntimeUICards.TagKind kind) StateOf(SettlementManager manager, ProcessingRecipeDefinition recipe,
        ProcessorSaveData processor, ProcessingRecipe rule)
    {
        if (!processor.on)
            return ("꺼짐", RuntimeUICards.TagKind.Plain);
        if (processor.tray >= rule.TrayCapacity)
            return ("꽉 참", RuntimeUICards.TagKind.Warn);
        return HasMaterials(manager, recipe, processor) ? ("가공 중", RuntimeUICards.TagKind.On) : ("재료 대기", RuntimeUICards.TagKind.Gold);
    }

    // 다음 하나를 만들 재료가 있나 (창고 재료 줄은 창고에, 나머지는 넣어 둔 재료)
    private static bool HasMaterials(SettlementManager manager, ProcessingRecipeDefinition recipe, ProcessorSaveData processor)
    {
        if (!recipe.FromWarehouse)
            return processor.input >= recipe.InputCount;
        var warehouse = manager.Warehouse;
        return warehouse != null && warehouse.Available(recipe.Input.ItemId) >= recipe.InputCount
            && (recipe.ExtraInput == null || warehouse.Available(recipe.ExtraInput.ItemId) >= recipe.ExtraInputCount);
    }

    // 지금 1시간에 만드는 양 (시설이 모으는 원재료가 한도, 창고 재료 · 가방 재료 줄은 레시피 최대 × 속도)
    private static float PerHour(SettlementManager manager, ProcessingRecipeDefinition recipe)
    {
        var rule = recipe.ToRecipe();
        float speed = manager.ProcessingSpeed;
        var building = recipe.Building;
        if (recipe.FromWarehouse || !building.Produces || recipe.Input != building.ProductionItem)
            return rule.OutputPerHour * speed;
        return ProcessingRules.ActualPerHour(rule, manager.ProductionPerHour(building), speed);
    }

    private static Action Recipe(Transform content, SettlementManager manager, ProcessingRecipeDefinition recipe, Action redraw)
    {
        var building = recipe.Building;
        string output = recipe.Output.DisplayName;
        string input = recipe.Input.DisplayName;
        bool learned = TechGate.Has(recipe.TechId);
        var processor = manager.ProcessorOf(recipe);
        bool on = learned && processor.on;
        bool fromWarehouse = recipe.FromWarehouse;

        var card = RuntimeUICards.Card(content, true, !learned);
        // 머리: [판재] 판재 가공 / 목공소 · 목재 4 → 판재 1 · 6분 / 스위치
        var head = RuntimeUICards.Row(card, 20f);
        RuntimeUICards.IconTile(head, recipe.Output.Icon, TileSize, !learned, on);
        var text = RuntimeUICards.Column(head, 6f);
        RuntimeUICards.Text(text, $"{output} 가공", 38, true);
        string materials = recipe.ExtraInput != null
            ? $"{input} {recipe.InputCount} + {recipe.ExtraInput.DisplayName} {recipe.ExtraInputCount}"
            : $"{input} {recipe.InputCount}개";
        RuntimeUICards.Text(text, $"{building.DisplayName} · {materials} → {output} 1개", 25, muted: true);
        float speed = manager.ProcessingSpeed;
        RuntimeUICards.Text(text, speed > 1.001f
            ? $"{Duration(recipe.CycleSeconds / speed)}마다 1개 · 속도 ×{speed:0.##}"
            : $"{Duration(recipe.CycleSeconds)}마다 1개", 25, muted: true);
        var right = RuntimeUICards.Column(head, 6f, 150f, TextAnchor.MiddleCenter);
        if (!learned)
        {
            RuntimeUICards.Tag(right, "연구 필요", RuntimeUICards.TagKind.Plain, 26);
            Note(card, $"연구에서 '{TechName(recipe.TechId)}'을 배우면 열려요");
            return null;
        }
        RuntimeUICards.Switch(right, on, () =>
        {
            manager.SetProcessing(recipe, !on);
            redraw();
        });
        RuntimeUICards.Text(right, on ? "켜짐" : "꺼짐", 24, true, !on, TextAlignmentOptions.Center);

        var rule = recipe.ToRecipe();
        if (!on)
        {
            string trial = processor.trialGiven ? "" : $"\n처음 켜면 {output} 1개를 바로 줘요!";
            Note(card, fromWarehouse ? $"켜면 왕국 창고의 {EuroRo(materials)} 만들어요{trial}"
                : building.Produces && recipe.Input == building.ProductionItem ? $"켜면 해달이 모은 {IGa(input)} 가방 대신 여기로 와요{trial}"
                : $"켜면 가방의 {EuroRo(input)} 만들어요{trial}");
            if (processor.tray <= 0)
                return null;
        }

        var inset = RuntimeUICards.Inset(card, 14f);
        Action inputLine = null;
        if (on && fromWarehouse)
        {
            // 창고 재료: 만들 때마다 창고에서 꺼냄 (넣기 · 자동 넣기 없음)
            var row = RuntimeUICards.Row(inset, 14f, 72f);
            var label = RuntimeUICards.Text(row, "창고 재료", 26, true, true);
            label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 140f;
            var first = Stock(row, recipe.Input);
            var second = recipe.ExtraInput != null ? Stock(row, recipe.ExtraInput) : null;
            RuntimeUICards.Flexible(row);
            var stockNote = RuntimeUICards.Text(inset, "만들 때마다 왕국 창고에서 바로 꺼내요", 23, muted: true, alignment: TextAlignmentOptions.MidlineRight);
            inputLine = () =>
            {
                var warehouse = manager.Warehouse;
                int a1 = warehouse != null ? warehouse.Available(recipe.Input.ItemId) : 0;
                first.text = $"{a1}/{recipe.InputCount}";
                first.color = a1 >= recipe.InputCount ? RuntimeUIKit.Style.TextColor : RuntimeUIKit.Style.WarningColor;
                if (second != null)
                {
                    int a2 = warehouse != null ? warehouse.Available(recipe.ExtraInput.ItemId) : 0;
                    second.text = $"{a2}/{recipe.ExtraInputCount}";
                    second.color = a2 >= recipe.ExtraInputCount ? RuntimeUIKit.Style.TextColor : RuntimeUIKit.Style.WarningColor;
                }
            };
        }
        else if (on)
        {
            Meter(inset, recipe.Input.Icon, "재료", out var inputFill, out var inputCount, row =>
            {
                RuntimeUICards.Plus(row, () =>
                {
                    manager.LoadProcessorFromBag(recipe, int.MaxValue);
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
            // 원료 자동 공급 (L04)
            if (TechGate.Has(TechIds.AutoSupply))
            {
                var auto = RuntimeUICards.Row(inset, 14f);
                var autoLabel = RuntimeUICards.Text(auto, $"자동 넣기 · 가방에 {processor.keep}개는 남겨요", 25, true, !processor.autoSupply);
                autoLabel.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
                bool autoOn = processor.autoSupply;
                RuntimeUICards.Switch(auto, autoOn, () =>
                {
                    manager.SetAutoSupply(recipe, !autoOn);
                    redraw();
                });
            }
        }

        UnityEngine.UI.Button collect = null;
        Meter(inset, recipe.Output.Icon, "완성", out var outputFill, out var outputCount, row =>
        {
            collect = RuntimeUICards.Button(row, "받기", () =>
            {
                manager.CollectProcessed(recipe);
                redraw();
            }, RuntimeUIKit.ButtonKind.Primary, 132f, 80f, 30);
        });

        TextMeshProUGUI next = null;
        if (on)
        {
            var foot = RuntimeUICards.Row(card, 14f);
            next = RuntimeUICards.Text(foot, "", 25, muted: true);
            next.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            Cart(foot, manager, recipe, redraw);
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
                next.text = full ? $"{IGa(output)} 꽉 찼어요 · 받기를 눌러 창고로"
                    : !HasMaterials(manager, recipe, processor) ? (fromWarehouse ? "창고 재료가 모이면 만들어요" : $"{input} {recipe.InputCount}개가 모이면 만들어요")
                    : $"다음 {output}까지 {Duration(Mathf.Max(0f, recipe.CycleSeconds - processor.progress) / speed)}";
            }
        };
    }

    // 창고 재료 한 칸: 아이콘 + 있는 수/드는 수
    private static TextMeshProUGUI Stock(Transform row, ItemDefinition item)
    {
        RuntimeUICards.Icon(row, item.Icon, 52f);
        var count = RuntimeUICards.Text(row, "", 28, true);
        count.textWrappingMode = TextWrappingModes.NoWrap;
        return count;
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
    private static void Cart(Transform row, SettlementManager manager, ProcessingRecipeDefinition recipe, Action redraw)
    {
        int limit = manager.CartRouteLimit;
        if (limit <= 0)
            return;
        bool on = manager.HasCartRoute(recipe);
        int used = manager.Settlement.Industry.cartRoutes.Count;
        RuntimeUICards.Text(row, on ? "수레" : $"수레 {used}/{limit}", 25, true, !on);
        var toggle = RuntimeUICards.Switch(row, on, () =>
        {
            manager.SetCartRoute(recipe, !on);
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

        // 창고 자재로 만들기 (마을 정비 C01): 광장 공사 → 꾸미기
        if (config.Crafts.Count > 0 || config.Pavings.Count > 0)
        {
            RuntimeUICards.Text(content, "창고 자재로 만들기", 34, true);
            if (!string.IsNullOrEmpty(hint))
            {
                var note = RuntimeUICards.Text(content, hint, 27, true, alignment: TextAlignmentOptions.Center);
                note.color = hintFailed ? RuntimeUIKit.Style.WarningColor : new Color32(0x5E, 0x8C, 0x55, 0xFF);
            }
            foreach (var paving in config.Pavings)
            {
                if (paving != null && !string.IsNullOrEmpty(paving.Id))
                    Paving(content, manager, paving, notify);
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

    // [돌길] 이름 · 설명 · 비용 / [깔기] (한 번만)
    private static void Paving(Transform content, SettlementManager manager, PavingProject paving, Action<string, bool> notify)
    {
        bool learned = string.IsNullOrEmpty(paving.TechId) || TechGate.Has(paving.TechId);
        bool done = manager.IsPaved(paving);
        var card = RuntimeUICards.Card(content, false, !learned);
        RuntimeUICards.IconTile(card, paving.Icon, TileSize, !learned, done);
        var text = RuntimeUICards.Column(card, 8f);
        RuntimeUICards.Text(text, paving.DisplayName, 36, true);
        bool waiting = !string.IsNullOrEmpty(paving.Requires) && !manager.HasDevelopment(paving.Requires);
        string subtitle = !learned ? $"연구에서 '{TechName(paving.TechId)}'을 배우면 열려요"
            : waiting && !done ? "먼저 중앙 광장 돌길을 깔아요" : paving.Description;
        RuntimeUICards.Text(text, subtitle, 24, muted: !waiting || done).color = waiting && !done && learned ? RuntimeUIKit.Style.WarningColor : RuntimeUICards.Muted;
        var right = RuntimeUICards.Column(card, 6f, 170f, TextAnchor.MiddleCenter);
        if (done)
        {
            RuntimeUICards.Tag(right, "공사 완료", RuntimeUICards.TagKind.On, 26);
            return;
        }
        var costs = RuntimeUICards.Row(text, 18f);
        var config = manager.IndustryConfig;
        var warehouse = manager.Warehouse;
        if (paving.StoneBlocks > 0)
        {
            var stone = config.FindOutput(SettlementManager.StoneBlockItemId);
            RuntimeUICards.Cost(costs, stone != null ? stone.Icon : null, paving.StoneBlocks.ToString(), warehouse.Available(SettlementManager.StoneBlockItemId) >= paving.StoneBlocks);
        }
        if (paving.Planks > 0)
        {
            var plank = config.FindOutput(SettlementManager.PlankItemId);
            RuntimeUICards.Cost(costs, plank != null ? plank.Icon : null, paving.Planks.ToString(), warehouse.Available(SettlementManager.PlankItemId) >= paving.Planks);
        }
        if (paving.Gold > 0)
        {
            var gold = manager.Config.GoldCurrency;
            RuntimeUICards.Cost(costs, gold != null ? gold.Icon : null, paving.Gold.ToString("N0"), manager.HasEnoughGold(paving.Gold));
        }
        RuntimeUICards.Flexible(costs);
        if (!learned)
        {
            RuntimeUICards.Tag(right, "연구 필요", RuntimeUICards.TagKind.Plain, 26);
            return;
        }
        var button = RuntimeUICards.Button(right, "깔기", () =>
        {
            string why = manager.PavingHint(paving);
            bool ok = manager.TryPave(paving);
            notify(ok ? $"{paving.DisplayName} 공사를 마쳤어요! 창을 닫으면 광장에서 보여요" : why ?? "깔 수 없어요", ok);
        }, RuntimeUIKit.ButtonKind.Feature, 160f, 92f, 32);
        button.interactable = manager.PavingHint(paving) == null;
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
        if (craft.Ingots > 0)
        {
            var ingot = config.FindOutput(SettlementManager.IngotItemId);
            RuntimeUICards.Cost(costs, ingot != null ? ingot.Icon : null, craft.Ingots.ToString(), warehouse.Available(SettlementManager.IngotItemId) >= craft.Ingots);
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

    // 받침 있으면 이 · 없으면 가 / 받침(ㄹ 빼고) 있으면 으로 · 없으면 로
    private static string IGa(string word) => word + (HasBatchim(word, false) ? "이" : "가");

    private static string EuroRo(string word) => word + (HasBatchim(word, true) ? "으로" : "로");

    private static bool HasBatchim(string word, bool rieulIsNone)
    {
        if (string.IsNullOrEmpty(word))
            return false;
        char last = word[word.Length - 1];
        if (last < '가' || last > '힣')
            return false;
        int final = (last - '가') % 28;
        return final != 0 && !(rieulIsNone && final == 8);
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
