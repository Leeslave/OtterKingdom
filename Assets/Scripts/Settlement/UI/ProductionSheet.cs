using TMPro;
using UnityEngine;

/// <summary>시설 생산과 농장 설비 확장. 한가한 특성 주민의 자동 생산을 그대로 활용한다.</summary>
public class ProductionSheet : MonoBehaviour
{
    private Canvas _canvas;
    private SettlementManager _manager;
    private TextMeshProUGUI _production;
    private float _refresh;
    public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

    public void Open(SettlementManager manager)
    {
        _manager = manager;
        if (_canvas == null)
        {
            _canvas = RuntimeUIKit.CreateCanvas("일터와 설비", 235, false);
            _canvas.transform.SetParent(transform, false);
        }
        _canvas.gameObject.SetActive(true);
        Draw();
    }

    public void Close() { if (_canvas != null) _canvas.gameObject.SetActive(false); }

    private void Update()
    {
        if (!IsOpen) return;
        _refresh -= Time.unscaledDeltaTime;
        if (_refresh > 0) return;
        _refresh = 1;
        RefreshProduction();
    }

    private void RefreshProduction()
    {
        if (_production == null) return;
        string Text(string id)
        {
            var building = _manager.FindBuilding(id);
            if (building == null || !_manager.HasBuilt(id)) return $"{(id == "bld_workshop" ? "목공소" : "채석 작업소")} · 건설하면 생산을 시작해요";
            int workers = ProductionRules.Workers(_manager.Config, _manager.Settlement, building);
            return $"{building.DisplayName} · 일하는 주민 {workers}명\n시간당 {_manager.ProductionPerHour(building):N0}개 → 가방으로 자동 수거";
        }
        _production.text = Text("bld_workshop") + "\n\n" + Text("bld_quarry");
    }

    private void Draw(string hint = "")
    {
        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _canvas.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        RuntimeUIKit.PrepareGlyphs("일터 농장 설비 확장 스프링클러 돌길 목공소 채석 작업소 생산 한가한 특성 주민 정찰 개간 수거 보관 조건 비용 확인 완료 시간당 분 골드 목재 돌 " + hint);
        var panel = RuntimeUIScrollSheet.Create(_canvas.transform, Close);
        RuntimeUIKit.CreateLabel(panel, "일터와 농장 설비", 42, true);
        _production = RuntimeUIKit.CreateLabel(panel, "", 30);
        RefreshProduction();
        RuntimeUIKit.CreateLabel(panel, "정찰·개간·주민 작업에 나간 해달은 생산을 쉬어요. 가방이 차면 최대 8시간치까지 일터에 보관해요.", 26);
        Equipment(panel, false);
        Equipment(panel, true);
        if (!string.IsNullOrEmpty(hint)) RuntimeUIKit.CreateLabel(panel, hint, 26);
        RuntimeUIKit.CreateButton(panel, "닫기", Close, RuntimeUIKit.ButtonKind.Secondary);
    }

    private void Equipment(Transform panel, bool stonePath)
    {
        string name = stonePath ? "돌길 설비" : "스프링클러 설비";
        string locked = _manager.EquipmentHint(stonePath);
        RuntimeUIKit.CreateLabel(panel, $"{name} · {(stonePath ? "골드 700 · 목재 10 · 돌 15" : "골드 500 · 목재 15 · 돌 5")}\n{locked ?? (stonePath ? "농부가 더 빨리 걷도록 흙길을 개선해요" : "밭에 스프링클러를 놓을 수 있어요")}", 27);
        if (locked == null)
            RuntimeUIKit.CreateButton(panel, name + " 확장", () => Draw(_manager.TryUpgradeEquipment(stonePath)
                ? name + "를 확장했어요!" : "조건과 비용을 확인해 주세요."));
    }
}
