using UnityEditor;
using UnityEngine;

// One-off tool: turns the furrow's single big clickable sprite into 3
// individually-clickable slot children (FurrowSlotView), each starting with
// the empty/locked placeholder sprite. Slot art is pre-trimmed/pre-scaled to
// its intended on-screen size (see Assets/Art/Farm/Crop), so slots are always
// created at localScale 1 — no per-sprite scale math here. Re-running this
// replaces any slots it previously created, so it's safe to re-run after
// swapping art. Positions are still derived from the furrow's sprite bounds
// so they land inside it regardless of art changes — nudge them by hand
// afterwards if they don't look right.
// Run via: OtterKingdom > Tools > Setup Furrow Slots (with Farm.unity open).
public static class FurrowSlotSetup
{
    private const string FurrowObjectName = "Plot_1_Active";
    private const string EmptySlotSpritePath = "Assets/Art/Farm/Crop/slot_empty.png";
    private const int SlotCount = 3;
    private const float SlotSpreadFraction = 0.62f; // total span the 3 slot centers are spread across

    [MenuItem("OtterKingdom/Tools/Setup Furrow Slots")]
    public static void Run()
    {
        var furrow = GameObject.Find(FurrowObjectName);
        if (furrow == null)
        {
            Debug.LogError($"[FurrowSlotSetup] Could not find '{FurrowObjectName}' in the open scene.");
            return;
        }

        var furrowRenderer = furrow.GetComponent<SpriteRenderer>();
        if (furrowRenderer == null || furrowRenderer.sprite == null)
        {
            Debug.LogError($"[FurrowSlotSetup] '{FurrowObjectName}' has no SpriteRenderer/sprite.");
            return;
        }

        var emptySprite = AssetDatabase.LoadAssetAtPath<Sprite>(EmptySlotSpritePath);
        if (emptySprite == null)
        {
            Debug.LogError($"[FurrowSlotSetup] Could not load sprite at '{EmptySlotSpritePath}'.");
            return;
        }

        CleanUpFurrowRoot(furrow);

        float furrowWidth = furrowRenderer.sprite.bounds.size.x;
        float spread = furrowWidth * SlotSpreadFraction;

        for (int i = 0; i < SlotCount; i++)
        {
            float t = SlotCount == 1 ? 0.5f : i / (float)(SlotCount - 1);
            float localX = Mathf.Lerp(-spread / 2f, spread / 2f, t);
            CreateSlot(furrow, i, localX, emptySprite, furrowRenderer.sortingOrder + 1);
        }

        EditorUtility.SetDirty(furrow);
        Debug.Log($"[FurrowSlotSetup] Created {SlotCount} slots under '{FurrowObjectName}'. " +
                   "Open Play mode or eyeball the Scene view and nudge position/scale if needed.");
    }

    private static void CleanUpFurrowRoot(GameObject furrow)
    {
        // The old whole-furrow collider/click handler is superseded by
        // per-slot colliders/FurrowSlotView. PlotClickToggle.cs no longer
        // exists in the project, so any lingering reference on this object
        // shows up as a missing script — strip that too.
        var collider = furrow.GetComponent<BoxCollider2D>();
        if (collider != null) Object.DestroyImmediate(collider);

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(furrow);

        // Remove slots from a previous run so this tool is safe to re-run.
        for (int i = furrow.transform.childCount - 1; i >= 0; i--)
        {
            var child = furrow.transform.GetChild(i);
            if (child.name.StartsWith("Slot_"))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void CreateSlot(GameObject furrow, int slotIndex, float localX, Sprite emptySprite, int sortingOrder)
    {
        var slot = new GameObject($"Slot_{slotIndex}");
        slot.transform.SetParent(furrow.transform, false);
        slot.transform.localPosition = new Vector3(localX, 0f, 0f);
        slot.transform.localScale = Vector3.one;

        var renderer = slot.AddComponent<SpriteRenderer>();
        renderer.sprite = emptySprite;
        renderer.sortingOrder = sortingOrder;

        var collider = slot.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        var view = slot.AddComponent<FurrowSlotView>();
        var so = new SerializedObject(view);
        so.FindProperty("plotIndex").intValue = 0;
        so.FindProperty("slotIndex").intValue = slotIndex;
        so.FindProperty("emptySlotSprite").objectReferenceValue = emptySprite;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
