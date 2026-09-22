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
}
