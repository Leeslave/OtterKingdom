using UnityEngine;

// Mining tuning shared by every scene's GameManager. Values are placeholders.
// The ore items' names and sale prices live in their ItemDefinitions.
[CreateAssetMenu(menuName = "OtterKingdom/Mining Balance", fileName = "MiningBalanceData")]
public class MiningBalanceData : ScriptableObject
{
    [Header("Ore items")]
    public string diamondItemId = "ore_diamond";
    public string stoneItemId = "ore_stone";

    [Header("Timing")]
    [Tooltip("Seconds between finds while the miner otter is inside, rolled per find.")]
    public Vector2 findIntervalSec = new Vector2(15f, 30f);

    [Header("Pickaxe")]
    [Tooltip("Diamond chance at pickaxe level 1; the rest is stone.")]
    [Range(0f, 1f)] public float baseDiamondChance = 0.3f;
    [Tooltip("Added to the diamond chance per pickaxe level above 1.")]
    [Range(0f, 1f)] public float diamondChancePerPickaxeLevel = 0.05f;
    // Cost to go from level (index+1) to (index+2). Max level = length + 1.
    public int[] pickaxeUpgradeCosts = { 100, 200, 400, 800 };
    public int MaxPickaxeLevel => pickaxeUpgradeCosts.Length + 1;

    [Header("Offline")]
    [Tooltip("Pickaxe level at which mining keeps going while the game is closed (no mining duty needed).")]
    public int offlineUnlockPickaxeLevel = 2;
    [Tooltip("Offline time per find = average online interval x this.")]
    public float offlineSlowdown = 4f;

    public float DiamondChanceAt(int pickaxeLevel)
    {
        return Mathf.Clamp01(baseDiamondChance + (pickaxeLevel - 1) * diamondChancePerPickaxeLevel);
    }

    // Average find interval, slowed down. 90s with the defaults.
    public float OfflineSecPerFind => (findIntervalSec.x + findIntervalSec.y) * 0.5f * offlineSlowdown;
}
