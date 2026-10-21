/// <summary>
/// 본 게임 쪽 연결 (FishingReworkModel): 골드는 본 게임 골드, 낚은 물고기는 본 게임 가방.
/// 없으면(테스트 씬) 규칙이 세이브 안의 골드 · 가방을 씀
/// </summary>
public interface IFishingHost
{
    int Gold { get; }

    /// <returns>모자라면 바꾸지 않고 false</returns>
    bool TrySpendGold(int amount);

    /// <summary>의뢰 보상</summary>
    void EarnGold(int amount);

    /// <summary>낚은 물고기를 가방에 (가득이면 놓아줌)</summary>
    void AddCatch(FishDefinition fish);
}
