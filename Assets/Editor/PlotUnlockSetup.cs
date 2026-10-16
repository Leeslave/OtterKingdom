using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off tool: wires the 3 furrow plots in Farm.unity up for coin unlocking.
// Adds a trigger BoxCollider2D + PlotView to each plot root, and gives the
// locked plots (Plot_2/Plot_3) their own 3 FurrowSlotView children, laid out
// by mirroring Plot_1's slots in world space (so any hand-nudging done on
// Plot_1 carries over and the slots come out the same on-screen size despite
// the plots' different scales). Slots that already exist are left untouched,
// so this is safe to re-run.
// Run via: OtterKingdom > Tools > Setup Plot Unlock (with Farm.unity open,
// after Setup Furrow Slots has been run on Plot_1).
public static class PlotUnlockSetup
{
    private static readonly string[] PlotObjectNames = { "Plot_1_Active", "Plot_2_Locked", "Plot_3_Locked" };
    private const string ActivePlotSpritePath = "Assets/Art/Farm/plot_active.png";
    private const string LockedPlotSpritePath = "Assets/Art/Farm/plot_locked.png";
    private const string EmptySlotSpritePath = "Assets/Art/Farm/Crop/slot_empty.png";
    private const int SlotCount = 3;

    [MenuItem("OtterKingdom/Tools/Setup Plot Unlock")]
    public static void Run()
    {
        var activeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ActivePlotSpritePath);
        var lockedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LockedPlotSpritePath);
        var emptySprite = AssetDatabase.LoadAssetAtPath<Sprite>(EmptySlotSpritePath);
        if (activeSprite == null || lockedSprite == null || emptySprite == null)
        {
            Debug.LogError("[PlotUnlockSetup] Could not load plot/slot sprites.");
            return;
        }

        var referencePlot = GameObject.Find(PlotObjectNames[0]);
        if (referencePlot == null || referencePlot.transform.Find("Slot_0") == null)
        {
            Debug.LogError($"[PlotUnlockSetup] '{PlotObjectNames[0]}' or its slots are missing — run Setup Furrow Slots first.");
            return;
        }

        for (int plotIndex = 0; plotIndex < PlotObjectNames.Length; plotIndex++)
        {
            var plot = GameObject.Find(PlotObjectNames[plotIndex]);
            if (plot == null)
            {
                Debug.LogError($"[PlotUnlockSetup] Could not find '{PlotObjectNames[plotIndex]}' in the open scene.");
                continue;
            }

            SetupPlotView(plot, plotIndex, activeSprite, lockedSprite);
            if (plotIndex > 0) MirrorSlots(referencePlot, plot, plotIndex, emptySprite);
        }

        EditorSceneManager.MarkSceneDirty(referencePlot.scene);
        Debug.Log("[PlotUnlockSetup] Done. Save the scene, then nudge Plot_2/Plot_3 slots by hand if needed.");
    }

    private static void SetupPlotView(GameObject plot, int plotIndex, Sprite activeSprite, Sprite lockedSprite)
    {
        var collider = plot.GetComponent<BoxCollider2D>();
        if (collider == null) collider = plot.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        var view = plot.GetComponent<PlotView>();
        if (view == null) view = plot.AddComponent<PlotView>();

        var so = new SerializedObject(view);
        so.FindProperty("plotIndex").intValue = plotIndex;
        so.FindProperty("activeSprite").objectReferenceValue = activeSprite;
        so.FindProperty("lockedSprite").objectReferenceValue = lockedSprite;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void MirrorSlots(GameObject referencePlot, GameObject plot, int plotIndex, Sprite emptySprite)
    {
        for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            if (plot.transform.Find($"Slot_{slotIndex}") != null) continue;

            var referenceSlot = referencePlot.transform.Find($"Slot_{slotIndex}");
            if (referenceSlot == null) continue;

            int sortingOrder = referenceSlot.GetComponent<SpriteRenderer>().sortingOrder;
            var slot = FurrowSlotSetup.CreateSlot(plot, plotIndex, slotIndex, 0f, emptySprite, sortingOrder);

            slot.transform.position = plot.transform.position + (referenceSlot.position - referencePlot.transform.position);

            Vector3 targetScale = referenceSlot.lossyScale;
            Vector3 parentScale = plot.transform.lossyScale;
            slot.transform.localScale = new Vector3(
                targetScale.x / parentScale.x,
                targetScale.y / parentScale.y,
                1f);
        }
    }
}
