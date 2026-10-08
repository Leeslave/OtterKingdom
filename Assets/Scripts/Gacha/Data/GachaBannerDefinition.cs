using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>뽑기 배너 종류. 천장(에픽·레어 확정까지 남은 수)은 같은 종류끼리 이어진다</summary>
public enum GachaBannerKind
{
    Standard, // 상시: 늘 열림
    Pickup,   // 픽업: 기간 한정, 픽업 장난감 확률 UP · 별빛 포인트
    Gold,     // 골드: 늘 열림, 골드로 언제든 1회 · 10회 (값은 왕국 레벨에 따라)
}

/// <summary>
/// 보물 조개 뽑기 배너 하나 (상시 "바닷가 보물 조개", 픽업 "봄바람 픽업").
/// 나오는 장난감의 등급은 아이템 희귀도(ItemRarity.Tier: 0 흔함 · 1 레어 · 2 에픽)로 나눈다.
/// 확률·천장 규칙은 GachaRules, 남은 횟수·포인트 같은 기록은 GachaManager가 가진다.
/// </summary>
[CreateAssetMenu(fileName = "GachaBanner", menuName = "Game Data/Gacha/Banner")]
public class GachaBannerDefinition : ScriptableObject
{
    private const string DateFormat = "yyyy-MM-dd";

    [Header("식별")]
    [Tooltip("세이브·기록에 남는 ID (예: pickup_spring_breeze)")]
    [SerializeField] private string _bannerId;

    [Tooltip("상시 / 픽업 (천장은 같은 종류끼리 이어짐)")]
    [SerializeField] private GachaBannerKind _kind;

    [Tooltip("탭·배너에 보일 이름")]
    [SerializeField] private string _title;

    [Tooltip("배너 아래 한 줄 소개")]
    [TextArea]
    [SerializeField] private string _subtitle;

    [Tooltip("탭 순서 (작을수록 앞)")]
    [SerializeField] private int _sortOrder;

    [Header("기간 (픽업만, 기기 날짜 yyyy-MM-dd. 비우면 늘 열림)")]
    [Tooltip("이 날 0시부터")]
    [SerializeField] private string _startDate;

    [Tooltip("이 날 24시까지")]
    [SerializeField] private string _endDate;

    [Header("그림")]
    [Tooltip("배너 그림 (3:2, 왼쪽 아래는 글자 자리)")]
    [SerializeField] private Sprite _bannerArt;

    [Tooltip("탭·연출에 쓰는 이 배너의 조개")]
    [SerializeField] private Sprite _shellIcon;

    [Tooltip("탭 · 이름표 색")]
    [SerializeField] private Color _themeColor = Color.white;

    [Header("나오는 장난감")]
    [Tooltip("상시 장난감 (등급은 아이템 희귀도)")]
    [SerializeField] private List<ItemDefinition> _pool = new List<ItemDefinition>();

    [Tooltip("픽업 장난감 (확률 UP). 상시 배너는 비움")]
    [SerializeField] private List<ItemDefinition> _featured = new List<ItemDefinition>();

    [Tooltip("픽업 에픽을 좋아하는 한정 해달 (결과 카드에 얼굴을 보여 줌, 비워도 됨)")]
    [SerializeField] private SettlementOtterDefinition _featuredOtter;

    [Header("확률 (0~1)")]
    [Tooltip("한 번 뽑을 때 에픽이 나올 확률")]
    [Range(0f, 1f)]
    [SerializeField] private float _epicRate = 0.03f;

    [Tooltip("한 번 뽑을 때 레어가 나올 확률")]
    [Range(0f, 1f)]
    [SerializeField] private float _rareRate = 0.15f;

    [Tooltip("에픽·레어가 나올 때 그것이 픽업 장난감일 확률 (픽업만)")]
    [Range(0f, 1f)]
    [SerializeField] private float _featuredShare = 0.5f;

    [Header("천장")]
    [Tooltip("이 횟수 안에 에픽 확정 (이 횟수째가 에픽)")]
    [Min(1)]
    [SerializeField] private int _epicPity = 60;

    [Tooltip("이 횟수 안에 레어 이상 확정 (10회 뽑기에 레어 이상 하나)")]
    [Min(1)]
    [SerializeField] private int _rarePity = 10;

    [Header("값")]
    [Tooltip("조개(유료 재화)")]
    [SerializeField] private Currency _gem;

    [Tooltip("1회 조개 값 (10회 = ×10)")]
    [Min(1)]
    [SerializeField] private int _gemCost = 30;

    [Tooltip("1회에 1장 쓰는 뽑기권 (있으면 조개보다 먼저 씀)")]
    [SerializeField] private Currency _ticket;

    [Header("별빛 포인트 (픽업만)")]
    [Tooltip("이만큼 모이면 픽업 에픽과 바꿈 (0 = 없음). 1회 뽑을 때마다 1점")]
    [Min(0)]
    [SerializeField] private int _exchangePoints = 100;

    [Header("골드 뽑기 (상시만)")]
    [Tooltip("하루 한 번 골드로 1회 뽑기")]
    [SerializeField] private bool _dailyGoldPull;

    [Tooltip("골드 뽑기 값 = 기본 + 왕국 레벨 × 레벨당")]
    [Min(0)]
    [SerializeField] private int _goldPullBase = 1000;

    [Min(0)]
    [SerializeField] private int _goldPullPerLevel = 800;

    [Header("골드 배너 (골드만)")]
    [Tooltip("1회 값 = 기본 + 왕국 레벨 × 레벨당")]
    [Min(0)]
    [SerializeField] private int _goldCostBase = 500;

    [Min(0)]
    [SerializeField] private int _goldCostPerLevel = 250;

    [Tooltip("10회 값 = 1회 값 × 이 수 (9 = 한 번 공짜)")]
    [Range(1, 10)]
    [SerializeField] private int _goldTenPulls = 9;

    public string BannerId => _bannerId;
    public GachaBannerKind Kind => _kind;
    public bool IsPickup => _kind == GachaBannerKind.Pickup;
    public string Title => _title;
    public string Subtitle => _subtitle;
    public int SortOrder => _sortOrder;
    public Sprite BannerArt => _bannerArt;
    public Sprite ShellIcon => _shellIcon;
    public Color ThemeColor => _themeColor;
    public IReadOnlyList<ItemDefinition> Pool => _pool;
    public IReadOnlyList<ItemDefinition> Featured => _featured;
    public SettlementOtterDefinition FeaturedOtter => _featuredOtter;
    public float EpicRate => _epicRate;
    public float RareRate => _rareRate;
    public float FeaturedShare => IsPickup ? _featuredShare : 0f;
    public int EpicPity => Mathf.Max(1, _epicPity);
    public int RarePity => Mathf.Max(1, _rarePity);
    public Currency Gem => _gem;
    public int GemCost => Mathf.Max(1, _gemCost);
    public Currency Ticket => _ticket;
    public int ExchangePoints => IsPickup ? Mathf.Max(0, _exchangePoints) : 0;
    public bool HasDailyGoldPull => _kind == GachaBannerKind.Standard && _dailyGoldPull;
    public int GoldPullBase => _goldPullBase;
    public int GoldPullPerLevel => _goldPullPerLevel;

    /// <summary>골드로 뽑는 배너 (조개 · 뽑기권 없이 골드만, 언제든 1회 · 10회)</summary>
    public bool IsGoldBanner => _kind == GachaBannerKind.Gold;
    public int GoldCostBase => _goldCostBase;
    public int GoldCostPerLevel => _goldCostPerLevel;
    public int GoldTenPulls => Mathf.Clamp(_goldTenPulls, 1, 10);

    /// <summary>이 장난감이 이 배너의 픽업 장난감인지</summary>
    public bool IsFeatured(ItemDefinition item) => item != null && _featured.Contains(item);

    /// <summary>지금 열려 있는지 (기간이 없으면 늘 열림). now = 기기 시간</summary>
    public bool IsOpenAt(DateTime now)
    {
        if (TryParse(_startDate, out var start) && now < start)
            return false;
        return !TryParse(_endDate, out var end) || now < end.AddDays(1);
    }

    /// <summary>끝나는 순간 (기간이 없으면 null)</summary>
    public DateTime? EndsAt => TryParse(_endDate, out var end) ? end.AddDays(1) : (DateTime?)null;

    private static bool TryParse(string text, out DateTime date) =>
        DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_bannerId))
            Debug.LogWarning($"[{name}] BannerId가 비어 있습니다.", this);
        if (!string.IsNullOrEmpty(_startDate) && !TryParse(_startDate, out _))
            Debug.LogWarning($"[{name}] 시작 날짜 형식이 {DateFormat}가 아닙니다: {_startDate}", this);
        if (!string.IsNullOrEmpty(_endDate) && !TryParse(_endDate, out _))
            Debug.LogWarning($"[{name}] 끝 날짜 형식이 {DateFormat}가 아닙니다: {_endDate}", this);
        if (_epicRate + _rareRate > 1f)
            Debug.LogWarning($"[{name}] 에픽 + 레어 확률이 1을 넘습니다.", this);
    }
}
