using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Places the offline-farming NPC in Farm.unity: a sprite + trigger collider +
// OfflineFarmNpcView. Until real NPC art exists it borrows the farmer otter's
// first walk frame, tinted so the two aren't confused — swap the Sprite on the
// SpriteRenderer (and reset its color to white) once the art lands. Re-running
// keeps an existing NPC where it was moved to and only fills in what's missing.
// Run via: OtterKingdom > Tools > Setup Offline Farm NPC (with Farm.unity open).
public static class OfflineFarmNpcSetup
{
    private const string ObjectName = "OfflineFarmNpc";
    private const string PlaceholderSheetPath = "Assets/Sprites/Characters/FarmerOtter/FarmerOtter_Sheet.png";
    private const string PlaceholderSpriteName = "FarmerOtter_Sheet_Walk_0";
    private static readonly Color PlaceholderTint = new Color(0.7f, 0.8f, 1f, 1f);
    // Upper left, clear of the furrows (x ≈ 0, y 1.9 .. -2.6) and the bottom nav bar.
    private static readonly Vector3 DefaultPosition = new Vector3(-3f, 4.6f, 0f);
    // Above furrows (0) and slots (1), same band as the farmer otter.
    private const int SortingOrder = 2;

    [MenuItem("OtterKingdom/Tools/Setup Offline Farm NPC")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Farm")
        {
            Debug.LogError("[OfflineFarmNpcSetup] Open Farm.unity first.");
            return;
        }

        var npc = GameObject.Find(ObjectName);
        bool created = npc == null;
        if (created)
        {
            npc = new GameObject(ObjectName);
            npc.transform.position = DefaultPosition;
        }

        var renderer = npc.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = npc.AddComponent<SpriteRenderer>();
        if (renderer.sprite == null)
        {
            renderer.sprite = LoadPlaceholderSprite();
            renderer.color = PlaceholderTint;
        }
        renderer.sortingOrder = SortingOrder;

        // Added after the sprite so it sizes itself to it.
        var collider = npc.GetComponent<BoxCollider2D>();
        if (collider == null) collider = npc.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        if (npc.GetComponent<OfflineFarmNpcView>() == null) npc.AddComponent<OfflineFarmNpcView>();

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = npc;
        Debug.Log(created
            ? $"[OfflineFarmNpcSetup] Added '{ObjectName}' at {DefaultPosition}. Move it where you like, then save the scene."
            : $"[OfflineFarmNpcSetup] '{ObjectName}' already existed — filled in missing parts. Save the scene.");
    }

    private static Sprite LoadPlaceholderSprite()
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(PlaceholderSheetPath))
        {
            if (asset is Sprite sprite && sprite.name == PlaceholderSpriteName) return sprite;
        }
        Debug.LogWarning($"[OfflineFarmNpcSetup] '{PlaceholderSpriteName}' not found in {PlaceholderSheetPath} — assign a sprite by hand.");
        return null;
    }
}
