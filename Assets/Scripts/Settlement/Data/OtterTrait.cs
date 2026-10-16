/// <summary>
/// 해달의 특성 (직업이 아님). 여러 해달이 같은 특성을 가질 수 있고, 한 해달은 특성 하나만 가지며 바뀌지 않는다.
/// 왕국에 사는 해달의 특성 수가 특성 건물의 해금 조건이 된다. 에셋에 숫자로 저장되므로 순서를 바꾸지 않는다
/// </summary>
public enum OtterTrait
{
    None = 0,
    Woodcutting = 1, // 나무캐기
    Mining = 2,      // 채굴
    Farming = 3,     // 농사
    Fishing = 4,     // 낚시
    Building = 5,    // 건설
    Crafting = 6,    // 손재주
    Hauling = 7,     // 운반
    Trading = 8,     // 장사
    Exploring = 9,   // 탐험
    Recording = 10,  // 기록
}

public static class OtterTraits
{
    /// <summary>특성 종류 수 (None 제외). 특성은 1부터 이 값까지</summary>
    public const int Count = 10;

    public static string DisplayName(OtterTrait trait)
    {
        switch (trait)
        {
            case OtterTrait.Woodcutting: return "나무캐기";
            case OtterTrait.Mining: return "채굴";
            case OtterTrait.Farming: return "농사";
            case OtterTrait.Fishing: return "낚시";
            case OtterTrait.Building: return "건설";
            case OtterTrait.Crafting: return "손재주";
            case OtterTrait.Hauling: return "운반";
            case OtterTrait.Trading: return "장사";
            case OtterTrait.Exploring: return "탐험";
            case OtterTrait.Recording: return "기록";
            default: return "";
        }
    }
}
