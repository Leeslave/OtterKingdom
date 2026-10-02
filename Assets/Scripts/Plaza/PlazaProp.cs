using UnityEngine;

// A separately drawn plaza object (tree, house, fence...). The transform
// sits where the object meets the ground (the sprite pivot is set there by
// PlazaSceneSetup), and its sorting order follows PlazaDepth so otters are
// hidden behind it or drawn over it depending on who is lower on screen.
//
// Movement blocking is not handled here: each prop prefab carries a child
// Blocked PlazaAreaPolygon ("Footprint") that PlazaWalkableArea picks up.
//
// Runs in edit mode so a prop dragged around the Scene view re-sorts live.
// Props don't move at runtime, so play mode only sorts once on enable.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class PlazaProp : MonoBehaviour
{
    [Tooltip("Lies flat on the ground (e.g. picnic mat): always drawn under otters instead of Y-sorted.")]
    [SerializeField] private bool flat;
    [Tooltip("Sort as if the ground contact were this far up (world units). For big slanted props (houses) whose pivot is the front-bottom corner: an otter standing in front of a side wall is above that corner, so it would be hidden. Set to about the middle of the footprint.")]
    [SerializeField] private float depthOffset;

    public float DepthOffset => depthOffset;

    // Runtime copies (a house being built) sort like the real prop.
    public void SetDepthOffset(float offset)
    {
        depthOffset = offset;
        ApplySorting();
    }

    private SpriteRenderer spriteRenderer;
    private float sortedY = float.NaN;

    private void OnEnable() => ApplySorting();

    private void OnValidate() => ApplySorting();

    private void Update()
    {
        if (Application.isPlaying) return;
        if (transform.position.y != sortedY) ApplySorting();
    }

    private void ApplySorting()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        sortedY = transform.position.y;
        spriteRenderer.sortingOrder = flat ? PlazaDepth.FlatPropOrder : PlazaDepth.SortingOrderFor(sortedY + depthOffset);
    }
}
