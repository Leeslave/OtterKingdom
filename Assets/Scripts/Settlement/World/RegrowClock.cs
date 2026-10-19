/// <summary>
/// 다시 생기기 시계 (나무 · 바위): 그림 n장 중 i번은 (i+1)/n 만큼 찬 시계. 다 찬 그림은 다 됐을 때만 (그때는 시계가 사라짐) —
/// 마지막 몇 초 동안 시계가 꽉 찼는데 아직 안 생기는 일이 없게, 지나간 만큼을 넘지 않는 그림을 고른다
/// </summary>
public static class RegrowClock
{
    /// <param name="done">지나간 비율 (0~1)</param>
    /// <param name="frames">그림 수</param>
    public static int Frame(float done, int frames)
    {
        if (frames <= 1)
            return 0;
        int filled = (int)(done * frames) - 1;
        return filled < 0 ? 0 : filled > frames - 2 ? frames - 2 : filled;
    }
}
