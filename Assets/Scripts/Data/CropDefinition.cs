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

    [Tooltip("Consumable seeds only: stock granted the first time this crop is seen in a save.")]
    public int initialSeedCount;

    [Tooltip("Multiplied into the slot's SpriteRenderer — lets a crop borrow another crop's " +
             "sprites as a placeholder until its own art exists. Leave white for real art.")]
    public Color spriteTint = Color.white;

    [Header("Growth stage sprites (one per furrow slot index, 0-2)")]
    public Sprite[] seedSprites;
    public Sprite[] sproutSprites;
    public Sprite[] grownSprites;
}
