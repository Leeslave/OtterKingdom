using System;
using System.Collections.Generic;
using UnityEngine;

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

/// <summary>뽑아서 넣은 것 하나 (재료 · 장신구 · 해달 조각) — 연출 · 결과 카드가 그대로 그림</summary>
public readonly struct GachaPull
{
    public readonly GachaRewardKind Kind;
    public readonly int Tier;
    public readonly bool Featured;
    public readonly bool ByPity;

    /// <summary>처음 얻음 (새 장신구 · 처음 만난 특수 해달의 조각)</summary>
    public readonly bool IsNew;

    /// <summary>반짝 조각으로 바뀐 몫 (최대 강화 장신구 · 5성 뒤 조각 · 가방에 못 넣은 재료)</summary>
    public readonly int Shards;

    public readonly Sprite Icon;
    /// <summary>"음유시인 해달 조각" · "레어 깃털 모자" · "재료 꾸러미"</summary>
    public readonly string Name;
    public readonly string Description;
    /// <summary>"조각 ×2 · 해금까지 12/20" · "강화 +2! 이동속도 +15%" · "목재 50 · 돌 40"</summary>
    public readonly string Detail;

    /// <summary>해달 조각이면 그 해달, 장신구면 그 장신구 (재료는 둘 다 null)</summary>
    public readonly SpecialOtterDefinition Otter;
    public readonly AccessoryDefinition Accessory;

    public GachaPull(GachaRewardKind kind, int tier, bool featured, bool byPity, bool isNew, int shards,
        Sprite icon, string name, string description, string detail,
        SpecialOtterDefinition otter = null, AccessoryDefinition accessory = null)
    {
        Kind = kind;
        Tier = tier;
        Featured = featured;
        ByPity = byPity;
        IsNew = isNew;
        Shards = shards;
        Icon = icon;
        Name = name;
        Description = description;
        Detail = detail;
        Otter = otter;
        Accessory = accessory;
    }

    public string TierName => GachaRewardText.TierName(Kind, Tier);
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
    // 처음 선물 특수 해달 (가챠 개편 5단계, 옛 세이브도 한 번 받음)
    public bool starterOtterGiven;
    public int totalPulls;

    // 한 번 열어 본 배너 (탭의 새 배너 표시용)
    public List<string> seenBanners = new List<string>();

    // 특수 해달 · 편성 · 장신구 (CompanionCollection, Docs/가챠개편_특수해달조각_장신구_기획.md 7장)
    public List<SpecialOtterSaveData> specialOtters = new List<SpecialOtterSaveData>();
    // 편성 칸 순서대로 해달 ID (빈 칸은 빈 문자열)
    public List<string> formation = new List<string>();
    public List<AccessorySaveData> accessories = new List<AccessorySaveData>();
}

/// <summary>세이브: 특수 해달 하나 (아직 안 쓴 조각 · 별, 0성 = 잠김)</summary>
[Serializable]
public class SpecialOtterSaveData
{
    public string otterId;
    public int fragments;
    public int stars;
}

/// <summary>세이브: 가진 장신구 하나 (강화 단계 · 낀 주민 해달 ID, 비면 안 낌)</summary>
[Serializable]
public class AccessorySaveData
{
    public string accessoryId;
    public int plus;
    public string otterId;
}
