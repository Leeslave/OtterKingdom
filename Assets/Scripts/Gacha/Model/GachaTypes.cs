using System;
using System.Collections.Generic;

/// <summary>뽑기 값을 치르는 방법</summary>
public enum GachaPayment
{
    Gem,    // 조개 (1회 30)
    Ticket, // 뽑기권 1회 1장
    Gold,   // 상시 배너는 하루 한 번 1회, 골드 배너는 언제든 1회 · 10회
}

public enum GachaPullOutcome
{
    Pulled,
    NotEnough,     // 조개·뽑기권·골드가 모자람
    Closed,        // 뽑기가 아직 안 열렸거나 배너 기간이 아님
    GoldUsedToday, // 오늘 골드 뽑기를 이미 함
}

public enum GachaExchangeOutcome
{
    Exchanged,
    NotEnough,    // 포인트·조각이 모자람
    NotAvailable, // 교환할 수 없는 장난감 · 끝난 배너
    Full,         // 가방에 더 넣을 수 없음 (최대 보유 수)
}

/// <summary>뽑아서 가방에 넣은 장난감 하나</summary>
public readonly struct GachaPull
{
    public readonly ItemDefinition Item;
    public readonly int Tier;
    public readonly bool Featured;
    public readonly bool ByPity;

    /// <summary>처음 얻음 (가방에 없던 장난감, 같은 10회 안에서 두 번째부터는 아님)</summary>
    public readonly bool IsNew;

    /// <summary>겹쳐서 덤으로 받은 반짝 조각 (가방이 가득 차 못 넣었으면 그 몫도 조각으로)</summary>
    public readonly int Shards;

    public GachaPull(GachaRoll roll, bool isNew, int shards)
    {
        Item = roll.Item;
        Tier = roll.Tier;
        Featured = roll.Featured;
        ByPity = roll.ByPity;
        IsNew = isNew;
        Shards = shards;
    }

    public bool IsEpic => Tier >= GachaTable.Epic;
    public bool IsRareOrBetter => Tier >= GachaTable.Rare;
}

/// <summary>뽑기 한 번(1회·10회)의 기록 — 연출과 분석 로그가 쓴다</summary>
public sealed class GachaPullReport
{
    public GachaBannerDefinition Banner;
    public GachaPayment Payment;
    public int Cost;
    public readonly List<GachaPull> Pulls = new List<GachaPull>();

    /// <summary>가장 높은 등급 (픽업이 같은 등급보다 위)</summary>
    public int BestIndex
    {
        get
        {
            int best = 0;
            for (int i = 1; i < Pulls.Count; i++)
            {
                var a = Pulls[i];
                var b = Pulls[best];
                if (a.Tier > b.Tier || (a.Tier == b.Tier && a.Featured && !b.Featured))
                    best = i;
            }
            return best;
        }
    }
}

/// <summary>뽑기 세이브 (GachaManager가 채움). 옛 세이브에 없으면 JsonUtility가 기본값으로 채움</summary>
[Serializable]
public class GachaSaveData
{
    public int standardSinceEpic;
    public int standardSinceRare;
    public int pickupSinceEpic;
    public int pickupSinceRare;
    public bool pickupGuaranteed;

    // 별빛 포인트는 픽업 배너마다 따로 (배너가 바뀌면 남은 포인트는 반짝 조각으로)
    public string pointsBannerId;
    public int points;

    // 마지막으로 골드 뽑기를 한 날 (QuestManager.TodayNumber, 새벽 4시 기준). 0 = 한 적 없음
    public int goldPullDay;

    // 골드 배너 천장 (골드 뽑기끼리)
    public int goldSinceEpic;
    public int goldSinceRare;

    public bool welcomeGiftGiven;
    public int totalPulls;

    // 한 번 열어 본 배너 (탭의 새 배너 표시용)
    public List<string> seenBanners = new List<string>();
}
