using UnityEngine;

// Fishing tuning shared by every scene's GameManager (the farm scene needs the
// catch items' names/prices for the sale UI too). Values are placeholders.
[CreateAssetMenu(menuName = "OtterKingdom/Fishing Balance", fileName = "FishingBalanceData")]
public class FishingBalanceData : ScriptableObject
{
    [Header("Catch items")]
    public string fishItemId = "fish_basic";
    public string fishDisplayName = "물고기";
    public int fishSellPrice = 20;

    public string trashItemId = "trash_basic";
    public string trashDisplayName = "쓰레기";
    public int trashSellPrice = 1;

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
