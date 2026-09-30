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
}
