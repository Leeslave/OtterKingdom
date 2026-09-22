using UnityEngine;

[CreateAssetMenu(menuName = "OtterKingdom/Crop Definition", fileName = "CropDefinition")]
public class CropDefinition : ScriptableObject
{
    public string cropId;
    public string displayName;
    public float baseDurationSec;
    public int yieldCount;
    public int sellPrice;
    public SeedType seedType;
}
