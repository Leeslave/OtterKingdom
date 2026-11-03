/// <summary>번영의 시대 기술 ID (기획서 5장). 연구 데이터 · 효과 · 퀘스트가 이 문자열로 맞춤 (이름 문자열 매칭 금지)</summary>
public static class TechIds
{
    // 산업
    public const string Sawmill = "I01";          // 제재 기술: 목공소 판재 가공
    public const string StoneCutting = "I02";     // 석재 규격화: 채석 작업소 석재 블록
    public const string Smelting = "I03";         // 금속 제련
    public const string StandardTools = "I04";    // 표준 공구: 가공 슬롯 +1
    public const string WaterPower = "I05";       // 수력 기계
    public const string PrecisionWork = "I06";    // 정밀 공방
    public const string CoProduction = "I07";     // 협업 생산
    public const string RoyalIndustry = "I08";    // 왕립 산업단지

    // 물류
    public const string Warehouse = "L01";        // 공동 창고
    public const string Cart = "L02";             // 운송 수레 (1경로)
    public const string Dispatch = "L03";         // 운송 배차 (3경로)
    public const string AutoSupply = "L04";       // 원료 자동 공급
    public const string Depot = "L05";            // 집산소
    public const string BetterCart = "L06";       // 개량 수레
    public const string AutoDelivery = "L07";     // 계약 자동 배달
    public const string Network = "L08";          // 통합 유통망

    // 경제
    public const string Market = "E01";
    public const string StandingOrders = "E02";
    public const string GoodsContracts = "E03";
    public const string TradeShip = "E04";
    public const string Guild = "E05";
    public const string TradeNetwork = "E06";
    public const string TradeAgreement = "E07";
    public const string WorldTrade = "E08";

    // 도시
    public const string TownPlanning = "C01";     // 도시 정비
    public const string BetterHouses = "C02";     // 주거 개량 1차
    public const string Paving = "C03";           // 도로 포장
    public const string PublicSpaces = "C04";     // 공공 시설
    public const string Administration = "C05";
    public const string Academy = "C06";
    public const string Capital = "C07";
    public const string Palace = "C08";

    // 융합
    public const string IndustrialLogistics = "X01";
    public const string OceanTrade = "X02";
    public const string CultureExchange = "X03";
    public const string Prosperity = "X04";
}
