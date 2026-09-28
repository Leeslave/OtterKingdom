using UnityEngine;

// Single depth rule shared by otters (OtterVisualController) and placed
// props (PlazaProp): whatever touches the ground lower on screen draws in
// front. Keeping both on one formula is what lets an otter walk behind a
// tree canopy or house and in front of it a moment later.
//
// Orders stay well inside the 16-bit sorting range for any plaza within
// +-300 world units of the origin.
public static class PlazaDepth
{
    // Sorting-order steps per world unit of ground Y (100 = 1cm resolution).
    public const float OrdersPerUnit = 100f;

    // Ground image, then flat decorations lying on it (picnic mat), then
    // everything Y-sorted.
    public const int BackgroundOrder = -32000;
    public const int FlatPropOrder = -31000;

    public static int SortingOrderFor(float groundY)
    {
        int order = -Mathf.RoundToInt(groundY * OrdersPerUnit);
        return Mathf.Clamp(order, FlatPropOrder + 1, short.MaxValue);
    }
}
