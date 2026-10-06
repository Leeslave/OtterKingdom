/// <summary>
/// 게임 효과음 종류 (상세기획서 10.2: 클릭·수확·판매·강화·발견 + 큰 순간).
/// 실제 음원은 AudioManager 인스펙터의 효과음 목록에 종류별로 넣는다. 비어 있으면 코드로 만든 임시 소리.
/// 새 값은 맨 뒤에 추가 (인스펙터에 저장된 번호가 바뀌지 않게).
/// </summary>
public enum SfxKind
{
    Click,     // 버튼 누름
    Harvest,   // 밭 수확
    Fish,      // 낚시 성공
    Find,      // 광산 발견
    Coin,      // 판매·구매·보상 (재화가 오감)
    Upgrade,   // 강화·확장
    LevelUp,   // 왕국 레벨 업
    Complete,  // 건설·주민 작업 완료
}
