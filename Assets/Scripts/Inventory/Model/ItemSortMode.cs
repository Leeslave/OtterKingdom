/// <summary>
/// 가방 목록 정렬 방식. 값은 드롭다운 순서이기도 하므로 새 값은 맨 뒤에 추가.
/// </summary>
public enum ItemSortMode
{
    Recent, // 최근 획득한 것 먼저
    Rarity, // 희귀한 것 먼저
    Name,   // 이름 가나다순
}

public static class ItemSortModeExtensions
{
    public static string ToDisplayName(this ItemSortMode mode)
    {
        switch (mode)
        {
            case ItemSortMode.Recent: return "최신순";
            case ItemSortMode.Rarity: return "등급순";
            case ItemSortMode.Name: return "이름순";
            default: return mode.ToString();
        }
    }
}
