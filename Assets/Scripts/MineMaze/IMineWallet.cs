/// <summary>본 게임의 골드. 갱도 작업대(곡괭이)와 광석 판매가 갱도 골드 대신 이 골드를 쓴다 (MineMazeModel.UseWallet)</summary>
public interface IMineWallet
{
    int Balance { get; }

    /// <returns>모자라면 바꾸지 않고 false</returns>
    bool TrySpend(int amount);

    void Earn(int amount);
}
