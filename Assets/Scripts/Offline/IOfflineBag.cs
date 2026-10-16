// What the offline simulation needs from the bag, by item id. GameManager
// maps it onto the real Inventory; tests use an in-memory fake.
public interface IOfflineBag
{
    int GetCount(string itemId);

    bool TryRemove(string itemId, int amount);

    // Returns how many actually went in — the rest didn't fit (or the item
    // has no definition) and is lost.
    int Add(string itemId, int amount);
}
