/// <summary>개발용 되돌리기 (치트 창 OtterKingdom/Dev/구역 · 성 테스트). 게임 진행에서는 부르지 않는다</summary>
public partial class Settlement
{
    /// <summary>발전과 그 부탁 완료 기록을 지움 (같은 기능을 다시 시험하려고)</summary>
    public void DevForget(string developmentId, string requestId)
    {
        bool changed = false;
        if (!string.IsNullOrEmpty(developmentId))
            changed |= _developments.Remove(developmentId);
        if (!string.IsNullOrEmpty(requestId))
            changed |= _completedRequests.Remove(requestId);
        if (changed)
            OnChanged?.Invoke();
    }
}
