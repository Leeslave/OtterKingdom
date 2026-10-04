using UnityEngine;

[CreateAssetMenu(menuName = "OtterKingdom/Farm Balance", fileName = "FarmBalanceData")]
public class FarmBalanceData : ScriptableObject
{
    public int storageCapacityCycles = 6;

    // Cost to go from level (index+1) to (index+2). Index 0 = 1->2.
    public int[] upgradeCostByLevel = { 30, 150, 500, 1200 };

    // Duration multiplier at each level. Index 0 = level 1 (1.00, no reduction).
    public float[] durationMultiplierByLevel = { 1.00f, 0.90f, 0.80f, 0.70f, 0.60f };

    public int maxFarmLevel = 5;

    // Coin cost to unlock each locked plot (plot 1 starts unlocked).
    public int plotUnlockCost = 100;

    [Header("Farmer")]
    [Tooltip("Seconds the farmer spends on one ready slot (walk + harvest) before the crop is in the bag. " +
             "One slot at a time, wherever the player is — the farm scene's otter only shows it.")]
    [Min(0.1f)]
    public float farmerHarvestSec = 10f;

    [Header("Offline")]
    [Tooltip("Farm level at which the crops registered with the farm NPC keep growing while the game is closed.")]
    public int offlineUnlockLevel = 2;
    [Tooltip("Offline grow time = online grow time x this.")]
    public float offlineSlowdown = 4f;
    [Tooltip("Vegetables per offline harvest (one seed each for consumable crops), instead of the crop's online yield.")]
    public int offlineYieldPerHarvest = 1;

    public float DurationMultiplierAt(int farmLevel)
    {
        int index = Mathf.Clamp(farmLevel - 1, 0, durationMultiplierByLevel.Length - 1);
        return durationMultiplierByLevel[index];
    }

    // Seconds for one online grow cycle of this crop at this farm level.
    public float GrowDurationSec(CropDefinition crop, int farmLevel)
    {
        return Mathf.Ceil(crop.baseDurationSec * DurationMultiplierAt(farmLevel));
    }
}
