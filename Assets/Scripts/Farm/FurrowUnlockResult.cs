// Result of trying to open the next furrow (FarmService.TryUnlockNextFurrow).
public enum FurrowUnlockResult
{
    Opened,         // paid and opened
    AllOpen,        // every furrow is already open
    NeedLevel,      // the kingdom level isn't high enough yet
    NotEnoughGold,  // couldn't pay
}
