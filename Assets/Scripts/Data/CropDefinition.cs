using UnityEngine;

[CreateAssetMenu(menuName = "OtterKingdom/Crop Definition", fileName = "CropDefinition")]
public class CropDefinition : ScriptableObject
{
    public const string SeedItemPrefix = "seed_";
    private const string CropIdPrefix = "crop_";

    public string cropId;
    public string displayName;
    public float baseDurationSec;
    public int yieldCount;
    [Tooltip("Not used for selling — the sale price comes from the crop's ItemDefinition.")]
    public int sellPrice;
    public SeedType seedType;

    [Tooltip("Consumable seeds only: stock put in the bag the first time this crop is seen in a save.")]
    public int initialSeedCount;

    // Consumable seeds live in the bag as their own item: crop_potato -> seed_potato.
    public string SeedItemId => SeedItemPrefix +
        (cropId.StartsWith(CropIdPrefix) ? cropId.Substring(CropIdPrefix.Length) : cropId);

    [Tooltip("Multiplied into the slot's SpriteRenderer — lets a crop borrow another crop's " +
             "sprites as a placeholder until its own art exists. Leave white for real art.")]
    public Color spriteTint = Color.white;

    [Header("Growth stage sprites (one per furrow slot index, 0-2)")]
    public Sprite[] seedSprites;
    public Sprite[] sproutSprites;
    public Sprite[] grownSprites;
}
