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
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void Build()
    {
        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
            Destroy(_canvas.transform.GetChild(i).gameObject);
        var dim = RuntimeUIKit.CreateDim(_canvas.transform);
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.onClick.AddListener(Close);

        var field = _manager.Farm.Field(_fieldId);
        if (field == null)
        {
            Close();
            return;
        }
        var soil = _manager.SoilOf(field);
        _panel = RuntimeUIKit.CreatePanel(_canvas.transform, 960f);
        RuntimeUIKit.CreateLabel(_panel, soil != null ? $"밭 칸 · {soil.Name}" : "밭 칸", RuntimeUIKit.TitleFontSize, true);
        _status = RuntimeUIKit.CreateLabel(_panel, "", 34);

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
                    if (_manager.TryUpgradeSoil(_fieldId, out string reason))
                        Build();
                    else
                        _status.text = reason;
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
            RuntimeUIKit.CreateButton(buttons, "뽑기", () => { _manager.ClearField(_fieldId); Close(); }, RuntimeUIKit.ButtonKind.Option, 220f);
        RuntimeUIKit.CreateButton(buttons, "닫기", Close, RuntimeUIKit.ButtonKind.Option, 220f);
        UpdateStatus();
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
            _status.text = $"{crop.displayName} 다 자랐어요! 농부가 거둬 가요";
        else
            _status.text = $"{crop.displayName} 자라는 중 · {TimeText(field.remainingSec / Mathf.Max(0.01f, _manager.FieldSpeed(field)) / 60f)} 남음";
    }

    private void Update()
    {
        if (IsOpen && Time.frameCount % 15 == 0)
            UpdateStatus();
    }
}
