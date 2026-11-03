using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 광장 밭 칸을 누르면 뜨는 창 (Docs/농장_설계형_기획.md): 지금 작물 · 남은 시간, 심을 작물 고르기(씨앗 수),
/// 흙 업그레이드(다음 흙 · 효과 · 비용, 잠겼으면 무엇을 지으면 열리는지), 뽑기. 코드로 만든 화면
/// </summary>
public class PlazaFarmSheet : MonoBehaviour
{
    private const int SortingOrder = 210;

    private Canvas _canvas;
    private RectTransform _panel;
    private SettlementManager _manager;
    private int _fieldId;
    private TextMeshProUGUI _status;

    public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

    public void Open(SettlementManager manager, int fieldId)
    {
        PlazaTapInput.BlockCurrentGesture();
        _manager = manager;
        _fieldId = fieldId;
        if (_canvas == null)
        {
            _canvas = RuntimeUIKit.CreateCanvas(nameof(PlazaFarmSheet), SortingOrder, false);
            _canvas.transform.SetParent(transform, false);
        }
        _canvas.gameObject.SetActive(true);
        Build();
    }

    public void Close()
    {
        PlazaTapInput.BlockCurrentGesture();
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void Build()
    {
        ClearContent();

        var field = _manager.Farm.Field(_fieldId);
        if (field == null)
        {
            Close();
            return;
        }
        var soil = _manager.SoilOf(field);
        _panel = RuntimeUIScrollSheet.Create(_canvas.transform, Close);
        RuntimeUIKit.CreateLabel(_panel, soil != null ? $"밭 칸 · {soil.Name}" : "밭 칸", RuntimeUIKit.TitleFontSize, true);
        _status = RuntimeUIKit.CreateLabel(_panel, "", 34);
        if (_manager.Farm.Crates.Count == 0)
        {
            var crate = _manager.FarmPartDefinition(FarmPartKind.Crate);
            if (crate != null)
                RuntimeUIKit.CreateButton(_panel, "수확 상자 짓기", () => { Close(); DecorModePresenter.RequestBuild(crate); });
        }
        if (InventoryManager.Instance != null && InventoryManager.Instance.Inventory.FreeSlots <= 0)
            RuntimeUIKit.CreateButton(_panel, "가방 칸 늘리기", InventoryExpansionSheet.Open);
        if (_manager.MineHealthMultiplier < 1f)
            RuntimeUIKit.CreateLabel(_panel, _manager.MineCommunityStatus, 24);

        // 심을 작물: 지금 심을 수 있는 것만 (씨앗 없는 작물까지 늘어놓으면 창이 화면을 넘음)
        RuntimeUIKit.CreateLabel(_panel, "심을 작물", 32, true);
        int hidden = 0;
        foreach (var crop in _manager.FarmCrops)
        {
            if (crop == null)
                continue;
            bool free = SettlementManager.FreeSeed(crop);
            int seeds = _manager.SeedCount(crop);
            if (!free && seeds <= 0 && field.cropId != crop.cropId)
            {
                hidden++;
                continue;
            }
            var row = RuntimeUIKit.CreateRow(_panel, 110f);
            var icon = RuntimeUIKit.CreateIcon(row, crop.grownSprites != null && crop.grownSprites.Length > 0 ? crop.grownSprites[0] : null, 90f);
            icon.color = crop.spriteTint;
            float minutes = crop.baseDurationSec / Mathf.Max(0.01f, _manager.FieldSpeed(field)) / 60f;
            var label = RuntimeUIKit.CreateLabel(row, $"{crop.displayName}  <size=75%>{TimeText(minutes)} · {(free ? "씨앗 무제한" : $"씨앗 {seeds}개")}</size>",
                34, false, TextAlignmentOptions.Left);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            bool can = free || seeds > 0;
            bool current = field.cropId == crop.cropId;
            RuntimeUIKit.CreateButton(row, current ? "심는 중" : can ? "심기" : "씨앗 없음", () =>
            {
                if (_manager.PlantField(_fieldId, crop))
                    Close();
                else
                    _status.text = GameManager.NoSeedMessage(crop);
            }, can && !current ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Option, 220f);
        }

        if (hidden > 0)
        {
            var more = RuntimeUIKit.CreateLabel(_panel, $"씨앗이 없는 작물 {hidden}종은 씨앗을 얻으면 나와요", 28);
            more.color = RuntimeUIKit.Style.BodyColor;
        }

        // 흙
        var next = _manager.NextSoil(field, out bool unlocked);
        if (next != null)
        {
            RuntimeUIKit.CreateSpace(_panel, 10f);
            float faster = (1f - next.DurationMultiplier) * 100f;
            string bonus = next.BonusChance > 0f ? $" · 가끔 하나 더 ({Mathf.RoundToInt(next.BonusChance * 100f)}%)" : "";
            RuntimeUIKit.CreateLabel(_panel, $"<b>{next.Name}</b>으로 바꾸기  <size=80%>자람 {Mathf.RoundToInt(faster)}% 빨리{bonus}</size>", 32);
            if (unlocked)
            {
                RuntimeUIKit.CreateButton(_panel, $"흙 바꾸기 ({CostText(next)})", () =>
                {
                    int fieldId = _fieldId;
                    int soil = field.soil;
                    ShowConfirm("흙 바꾸기", $"{CostText(next)}\n{next.Name}으로 바꿀까요?", () =>
                    {
                        if (_manager.Farm.Field(fieldId)?.soil != soil) { Build(); return; }
                        if (_manager.TryUpgradeSoil(fieldId, out string reason))
                            Build();
                        else
                        {
                            Build();
                            _status.text = reason;
                        }
                    });
                }, RuntimeUIKit.ButtonKind.Secondary);
            }
            else
            {
                var locked = RuntimeUIKit.CreateLabel(_panel, _manager.SoilLockText(next), 30);
                locked.color = RuntimeUIKit.Style.WarningColor;
            }
        }

        var buttons = RuntimeUIKit.CreateRow(_panel, RuntimeUIKit.ButtonHeight);
        if (!string.IsNullOrEmpty(field.cropId))
            RuntimeUIKit.CreateButton(buttons, "뽑기", ConfirmClear, RuntimeUIKit.ButtonKind.Option, 220f);
        RuntimeUIKit.CreateButton(buttons, "닫기", Close, RuntimeUIKit.ButtonKind.Option, 220f);
        UpdateStatus();
    }

    private void ConfirmClear()
    {
        int fieldId = _fieldId;
        var field = _manager.Farm.Field(fieldId);
        var crop = field != null ? _manager.FindCrop(field.cropId) : null;
        if (crop == null) return;
        bool wasReady = field.ready;
        float remaining = field.remainingSec;
        string warning = field.ready ? "다 자란 작물이에요. 수확하지 않고 없애요.\n" : "";
        ShowConfirm("작물 뽑기", $"{warning}{crop.displayName}을 뽑을까요? 쓴 씨앗은 돌려받을 수 없어요.", () =>
        {
            // 확인 중 농부가 거두고 다시 심었다면 새 작물은 없애지 않는다.
            if (_manager.Farm.Field(fieldId) != field || field.cropId != crop.cropId
                || field.ready != wasReady || field.remainingSec > remaining) { Build(); return; }
            _manager.ClearField(fieldId);
            if (_fieldId == fieldId) Close();
        });
    }

    private void ClearContent()
    {
        _status = null;
        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _canvas.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    // 밭 시트(210)보다 아래인 GameUI 확인 창(200)에 가려지지 않도록 같은 캔버스에서 확인한다.
    private void ShowConfirm(string title, string message, Action confirm)
    {
        ClearContent();
        var panel = RuntimeUIScrollSheet.Create(_canvas.transform, Build);
        RuntimeUIKit.CreateLabel(panel, title, RuntimeUIKit.TitleFontSize, true);
        RuntimeUIKit.CreateLabel(panel, message, 34);
        var row = RuntimeUIKit.CreateRow(panel, RuntimeUIKit.ButtonHeight);
        RuntimeUIKit.CreateButton(row, "예", confirm, RuntimeUIKit.ButtonKind.Primary, flexible: true);
        RuntimeUIKit.CreateButton(row, "아니오", Build, RuntimeUIKit.ButtonKind.Option, flexible: true);
    }

    private string CostText(FarmSoilTier soil)
    {
        string text = $"골드 {soil.Gold:N0}";
        foreach (var cost in soil.Items)
        {
            if (cost != null && cost.Item != null)
                text += $" · {cost.Item.DisplayName} {cost.Amount}";
        }
        return text;
    }

    private static string TimeText(float minutes) =>
        minutes >= 60f ? $"{minutes / 60f:0.#}시간" : minutes >= 1f ? $"{Mathf.CeilToInt(minutes)}분" : $"{Mathf.CeilToInt(minutes * 60f)}초";

    private void UpdateStatus()
    {
        var field = _manager.Farm.Field(_fieldId);
        if (field == null || _status == null)
            return;
        var crop = _manager.FindCrop(field.cropId);
        if (crop == null)
            _status.text = string.IsNullOrEmpty(field.waitingCropId) ? "비어 있어요. 작물을 심어요!" : "씨앗이 없어 기다리는 중이에요";
        else if (field.ready)
            _status.text = _manager.Farm.Crates.Count == 0
                ? $"{crop.displayName} 다 자랐어요! 꾸미기 → 건물에서 수확 상자를 먼저 지어 주세요."
                : _manager.Farm.FreeSpace(_manager.CrateCapacity) <= 0
                    ? $"{crop.displayName} 다 자랐어요! 수확 상자를 눌러 비우면 농부가 다시 거둬요."
                    : $"{crop.displayName} 다 자랐어요! 농부가 거둬 가요";
        else
            _status.text = $"{crop.displayName} 자라는 중 · {TimeText(field.remainingSec / Mathf.Max(0.01f, _manager.FieldSpeed(field)) / 60f)} 남음";
    }

    private void Update()
    {
        if (IsOpen && Time.frameCount % 15 == 0)
            UpdateStatus();
    }
}
