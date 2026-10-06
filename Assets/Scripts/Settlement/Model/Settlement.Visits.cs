/// <summary>
/// 장난감 방문 (P4): 다음 장난감 해달이 찾아올 시각과 회차. 찾아온 해달 자체는 주민 목록(Visitor)이 원본이다
/// </summary>
public partial class Settlement
{
    /// <summary>다음 장난감 해달이 찾아오는 시각 (UTC ticks). 0 = 시계가 돌지 않음</summary>
    public long ToyVisitDueUtcTicks { get; private set; }

    /// <summary>찾아온 장난감 해달 수 = 다음 방문의 회차 (해달 고르기의 난수 씨앗)</summary>
    public int ToyVisitSerial { get; private set; }

    /// <summary>방문 시계를 맞춤 (0 = 멈춤). 저장만 바뀌고 광장은 다시 맞추지 않음</summary>
    public void SetToyVisitDue(long dueUtcTicks) => ToyVisitDueUtcTicks = dueUtcTicks < 0 ? 0 : dueUtcTicks;

    /// <summary>장난감 해달 한 마리가 찾아옴: 회차를 넘기고 시계를 멈춤 (다음 시계는 다음 판정에서)</summary>
    public void CompleteToyVisit()
    {
        ToyVisitSerial++;
        ToyVisitDueUtcTicks = 0;
    }

    private void LoadVisits(SettlementSaveData saved)
    {
        ToyVisitDueUtcTicks = saved.toyVisitDueUtcTicks < 0 ? 0 : saved.toyVisitDueUtcTicks;
        ToyVisitSerial = saved.toyVisitSerial < 0 ? 0 : saved.toyVisitSerial;
    }

    private void WriteVisits(SettlementSaveData result)
    {
        result.toyVisitDueUtcTicks = ToyVisitDueUtcTicks;
        result.toyVisitSerial = ToyVisitSerial;
    }
}
