using System.Collections.Generic;

/// <summary>
/// 왕국 구역의 용도 (구역·이사 기획, 2026-10-13). 에셋·세이브에 숫자로 저장되므로 순서를 바꾸지 않는다 (뒤에만 붙임).
/// 이름이 "Zone"이 아닌 이유: 장소(씬)를 뜻하는 ZoneDefinition과 헷갈리지 않게
/// </summary>
public enum DistrictType
{
    None = 0,          // 구역 없음 (가로등·식탁·장난감처럼 어디에 놓아도 되는 것, 아직 용도를 정하지 않은 블록)
    Central = 1,       // 중앙 (행정): 게시판 → 마을회관 → 성
    Residential = 2,   // 주거: 해달 집, 쉬는 자리
    Commercial = 3,    // 상업: 요정 상점, 가게, 길드 접수소
    Production = 4,    // 생산: 공방, 채석장, 곡물창고
    Agricultural = 5,  // 농업: 밭
}

public static class Districts
{
    /// <summary>플레이어가 블록에 고를 수 있는 용도 (중앙은 고르지 않음)</summary>
    public static readonly IReadOnlyList<DistrictType> Assignable = new[]
    {
        DistrictType.Residential, DistrictType.Commercial, DistrictType.Production, DistrictType.Agricultural,
    };

    public static bool IsAssignable(DistrictType type) =>
        type == DistrictType.Residential || type == DistrictType.Commercial || type == DistrictType.Production || type == DistrictType.Agricultural;

    public static string DisplayName(DistrictType type)
    {
        switch (type)
        {
            case DistrictType.Central: return "중앙";
            case DistrictType.Residential: return "주거 지역";
            case DistrictType.Commercial: return "상업 지역";
            case DistrictType.Production: return "생산 지역";
            case DistrictType.Agricultural: return "농업 지역";
            default: return "빈 땅";
        }
    }
}

/// <summary>구역에 속하는 건물 (자리를 골라 짓는 건물 · 부탁 건물 자리). 용도가 None이면 어디에 놓아도 됨</summary>
public interface IDistrictBuilding
{
    DistrictType District { get; }
}
