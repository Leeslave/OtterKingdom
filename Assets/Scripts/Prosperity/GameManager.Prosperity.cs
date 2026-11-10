using System;

public partial class GameManager
{
    private readonly ProsperitySaveGate _prosperitySaves = new ProsperitySaveGate();

    /// <summary>납품 도중 저장 요청이 와도 재료·보상·완료 기록이 모두 반영될 때까지 쓰기를 미룬다.</summary>
    public IDisposable DeferProsperitySave() => _prosperitySaves.Begin(SaveNow);
}
