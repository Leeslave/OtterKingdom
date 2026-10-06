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
    Milestone,          // 정착 진행 한 단계 (Target = 끝낸 부탁 ID 또는 열린 발전 ID). 이루면 1, 기록으로 다시 셈
    VisitZone,          // 장소에 가 보기 (Target = 씬 이름). 도착하거나 이미 그곳에 있으면 1
    UnlockFurrow,       // 밭 고랑 한 칸 열기 (연 횟수)
    ExpandBag,          // 가방 칸 늘리기 (늘린 횟수)
    CompleteDaily,      // 일일 퀘스트 보상 받기 (받은 수)
}

/// <summary>퀘스트 종류</summary>
public enum QuestKind
{
    Main,  // 메인 퀘스트: 레벨마다 몇 개를 하나씩 순서대로. 경험치 합 = 그 레벨의 필요 경험치 (Docs/레벨별_메인퀘스트.md)
    Daily, // 일일 퀘스트: 매일 새벽 4시에 진행·수령이 초기화됨
    Challenge, // 도전 퀘스트: 누적 목표 체인. 골드 + 경험치 (메인 밖의 덤이라 하면 레벨이 조금 빨라짐)
}
