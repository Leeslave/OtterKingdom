/// <summary>
/// 본 게임의 가방. 갱도에서 캔 광석은 여기로 바로 들어가고, 못 들어간 것만 갱도 작업대에 쌓인다.
/// 곡괭이 · 문 · 판매는 작업대 것을 먼저 쓰고 모자라면 가방에서 꺼낸다 (MineMazeModel)
/// </summary>
public interface IMineOreBag
{
    int Count(MineOreDefinition ore);

    /// <returns>가방에 들어간 수 (자리가 없으면 0)</returns>
    int Add(MineOreDefinition ore, int amount);

    /// <returns>가방에서 뺀 수</returns>
    int Remove(MineOreDefinition ore, int amount);
}
