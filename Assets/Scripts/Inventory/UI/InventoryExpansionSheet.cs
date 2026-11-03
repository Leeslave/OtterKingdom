using UnityEngine;

/// <summary>가방이 막힌 현장에서 비용을 확인하고 칸을 늘린다.</summary>
public class InventoryExpansionSheet : MonoBehaviour
{
    private static InventoryExpansionSheet _instance;
    private Canvas _canvas;

    public static void Open()
    {
        if (InventoryManager.Instance == null) return;
        if (_instance == null)
        {
            var canvas = RuntimeUIKit.CreateCanvas("가방 칸 늘리기", 260, true);
            _instance = canvas.gameObject.AddComponent<InventoryExpansionSheet>();
            _instance._canvas = canvas;
        }
        _instance.gameObject.SetActive(true);
        _instance.Draw();
    }

    private void Draw(string hint = "")
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            child.SetActive(false); Destroy(child);
        }
        var manager = InventoryManager.Instance;
        var config = manager.Config;
        var panel = RuntimeUIScrollSheet.Create(_canvas.transform, Close);
        RuntimeUIKit.CreateLabel(panel, "가방 칸 늘리기", 44, true);
        int next = Mathf.Min(manager.Inventory.Capacity + config.ExpandStep, manager.Inventory.MaxCapacity);
        RuntimeUIKit.CreateLabel(panel, $"사용 {manager.Inventory.UsedSlots} / {manager.Inventory.Capacity}칸\n{manager.Inventory.Capacity}칸 → {next}칸", 34);
        if (!string.IsNullOrEmpty(hint)) RuntimeUIKit.CreateLabel(panel, hint, 30);
        if (manager.CanExpand)
        {
            RuntimeUIKit.CreateLabel(panel, $"비용: {config.ExpandCurrency.DisplayName} {config.ExpandCost:N0}개", 32);
            RuntimeUIKit.CreateButton(panel, "이 비용으로 늘리기", () =>
            {
                bool bought = manager.TryPurchaseExpansion();
                UITransitionShield.Block();
                Draw(bought ? "가방 칸을 늘렸어요!" : "조개가 모자라요. 비용과 보유량을 확인해 주세요.");
            }, RuntimeUIKit.ButtonKind.Primary);
        }
        else RuntimeUIKit.CreateLabel(panel, "최대 용량이에요. 남는 작물·물고기를 판매하면 빈 칸이 생겨요.", 30);
    }

    private void Close() { UITransitionShield.Block(); gameObject.SetActive(false); }
    private void OnDestroy() { if (_instance == this) _instance = null; }
}
