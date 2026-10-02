/// <summary>
/// 아이템의 획득/소비 출처
/// </summary>
public enum ItemChangeReason
{
    Test,
    Harvest,
    Fishing,
    Sell,
    Load, // 세이브 복원
    Plant, // 모종을 심어서 소모
    Grant, // 시작 모종 지급, 옛 세이브의 모종 재고를 가방으로 옮김
    Purchase, // 요정 상점에서 삼
    Mining, // 광산에서 캔 광석 (오프라인 채굴 포함)
    Gather, // 광장에서 주운 재료 (나뭇가지 → 목재)
    Construction, // 건설 재료로 씀
}
