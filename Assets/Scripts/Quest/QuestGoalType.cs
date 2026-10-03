/// <summary>
/// 퀘스트가 무엇을 세는지. 에셋에 숫자로 저장되므로 순서를 바꾸지 말고 뒤에만 추가한다.
/// </summary>
public enum QuestGoalType
{
    Harvest,            // 수확으로 가방에 들어온 개수 (오프라인 수확 포함)
    Catch,              // 낚시로 가방에 들어온 개수 (오프라인 낚시 포함)
    EarnFromSales,      // 판매로 받은 골드 합계
    Upgrade,            // 생산 업그레이드 횟수 (밭 강화, 낚싯대 강화, 곡괭이 강화)
    CollectionRegister, // 도감에 새로 등록한 항목 수 (해달 탭이면 해달 등록)
    PlaceDecor,         // 장난감을 놓은 횟수 (어느 장소든)
    ShopPurchase,       // 요정 상점에서 산 횟수
    Mine,               // 광산에서 캐서 가방에 들어온 개수 (오프라인 채굴 포함)
    Gather,             // 광장에서 주운 재료 개수 (나뭇가지, 돌무더기)
    CompleteConstruction, // 다 지은 건물 수 (집·의자. 개간 제외). 저장된 기록으로 다시 세므로 퀘스트를 늦게 받아도 앞서 지은 것이 들어감
    MeetOtter,          // 광장에서 처음 만난 해달 수 (처음부터 함께한 첫 해달 제외). 도감 등록과 별개, 기록으로 다시 셈
}

/// <summary>퀘스트 종류</summary>
public enum QuestKind
{
    Main,  // 성장 퀘스트: 레벨과 앞 단계로 열리고, 한 번 받으면 끝
    Daily, // 일일 퀘스트: 매일 새벽 4시에 진행·수령이 초기화됨
}
