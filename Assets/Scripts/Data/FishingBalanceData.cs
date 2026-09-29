using UnityEngine;

// Fishing tuning shared by every scene's GameManager. Values are placeholders.
// The catch items' names and sale prices live in their ItemDefinitions.
[CreateAssetMenu(menuName = "OtterKingdom/Fishing Balance", fileName = "FishingBalanceData")]
public class FishingBalanceData : ScriptableObject
{
    [Header("Catch items")]
    public string fishItemId = "fish_mackerel";
    public string trashItemId = "trash_basic";

    [Header("Timing")]
    [Tooltip("Seconds from the bobber landing until something bites, rolled per cast.")]
    public Vector2 biteDelaySec = new Vector2(10f, 30f);

    [Header("Rod")]
    [Tooltip("Fish chance at rod level 1; the rest is trash.")]
    [Range(0f, 1f)] public float baseFishChance = 0.5f;
    [Tooltip("Added to the fish chance per rod level above 1.")]
    [Range(0f, 1f)] public float fishChancePerRodLevel = 0.05f;
    // Cost to go from level (index+1) to (index+2). Max level = length + 1.
    public int[] rodUpgradeCosts = { 100, 200, 400, 800 };
}
