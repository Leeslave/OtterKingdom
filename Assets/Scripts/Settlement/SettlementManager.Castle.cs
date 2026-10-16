using System;

/// <summary>
/// 성 승격 (구역·이사 기획안 5막): 질서도가 성 기준에 닿으면 성 준비(castle_ready)를 열어 게시판에 "왕국의 성" 부탁이 붙게 한다.
/// 짓기 · 완성 · 축하는 다른 큰 부탁과 같은 건설 흐름 (req_build_castle → castle_built)
/// </summary>
public partial class SettlementManager
{
    /// <summary>성을 세울 수 있게 됐을 때 (한 번)</summary>
    public event Action OnCastleReady;

    /// <summary>개발용: 0 이상이면 왕국 질서도를 이 값으로 (치트 창). -1 = 끔</summary>
    public static int DevOrderOverride { get; set; } = -1;

    public bool IsCastleReady => IsLoaded && Settlement.HasDevelopment(CastleRules.Ready);
    public bool IsCastleBuilt => IsLoaded && Settlement.HasDevelopment(CastleRules.Built);

    // Update에서 (이사 틱과 같은 간격)
    private void TickCastle()
    {
        if (!CastleRules.ShouldOpen(_config, Settlement, DistrictOrder != null ? DistrictOrder.Score : 0, PlayerLevel))
            return;
        Settlement.UnlockDevelopment(CastleRules.Ready);
        GameNotices.Post(new GameNotice("또박이: 왕국 질서도 80 달성! 게시판에서 왕국의 성을 세울 수 있어요"));
        OnCastleReady?.Invoke();
        SaveRequested?.Invoke();
    }
}
