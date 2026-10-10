using UnityEngine;

[CreateAssetMenu(menuName = "OtterKingdom/Farm Balance", fileName = "FarmBalanceData")]
public class FarmBalanceData : ScriptableObject
{
    public int storageCapacityCycles = 6;

    // Cost to go from level (index+1) to (index+2). Index 0 = 1->2. 20레벨까지 (낚싯대 강화와 같은 표)
    public int[] upgradeCostByLevel =
    {
        100, 200, 400, 800, 1200, 1700, 2400, 3300, 4500, 6000,
        8000, 10500, 14000, 18500, 24000, 31000, 40000, 52000, 67000,
    };

    // Duration multiplier at each level. Index 0 = level 1 (1.00, no reduction). Lv.5 이후는 한 레벨에 2%씩
    public float[] durationMultiplierByLevel =
    {
        1.00f, 0.90f, 0.80f, 0.70f, 0.60f, 0.58f, 0.56f, 0.54f, 0.52f, 0.50f,
        0.48f, 0.46f, 0.44f, 0.42f, 0.40f, 0.38f, 0.36f, 0.34f, 0.32f, 0.30f,
    };

    public int maxFarmLevel = 20;

    [System.Serializable]
    public struct FurrowUnlock
    {
        [Tooltip("Kingdom level needed to open this furrow.")]
        public int requiredLevel;
        [Tooltip("Gold to open it.")]
        public int cost;

        public FurrowUnlock(int requiredLevel, int cost)
        {
            this.requiredLevel = requiredLevel;
            this.cost = cost;
        }
    }

    [Header("Furrows")]
    [Tooltip("Plot 1's three furrows start open. The other six open one at a time, in order " +
             "(plot 2 left to right, then plot 3): kingdom level and gold for each (growth curve: one furrow at a time). Placeholder numbers.")]
    public FurrowUnlock[] furrowUnlocks =
    {
        new FurrowUnlock(5, 300),
        new FurrowUnlock(6, 600),
        new FurrowUnlock(8, 1000),
        new FurrowUnlock(9, 1600),
        new FurrowUnlock(11, 2500),
        new FurrowUnlock(13, 4000),
    };

    // The price of the n-th furrow beyond plot 1 (0-based). Past the table: the last entry.
    public FurrowUnlock FurrowUnlockAt(int index)
    {
        if (index < 0 || furrowUnlocks == null || furrowUnlocks.Length == 0) return new FurrowUnlock(1, 0);
        return furrowUnlocks[Mathf.Min(index, furrowUnlocks.Length - 1)];
    }

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
