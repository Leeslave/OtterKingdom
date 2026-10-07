using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>교환소 한 줄 (반짝 조각으로 고르는 장난감)</summary>
public readonly struct GachaExchangeEntry
{
    public readonly ItemDefinition Item;
    public readonly int Tier;
    public readonly int Cost;
    public readonly bool PastFeatured;

    public GachaExchangeEntry(ItemDefinition item, int tier, int cost, bool pastFeatured)
    {
        Item = item;
        Tier = tier;
        Cost = cost;
        PastFeatured = pastFeatured;
    }
}

/// <summary>
/// 보물 조개 뽑기의 주인. 전역 UI(GlobalUI) 루트에 붙어 씬을 넘어 유지된다.
/// 값 치르기 → 뽑기(GachaRules) → 가방에 넣기 → 겹친 장난감은 반짝 조각 덤 → 바로 저장 순서로, 연출보다 먼저 결과를 확정한다
/// (연출 도중 앱을 꺼도 다시 뽑을 수 없음). 천장·별빛 포인트·골드 뽑기 날짜를 가진다.
/// 뽑기는 요정이 광장에 온 뒤(요정 상점이 열린 뒤) 열린다.
/// - 세이브: 게임 쪽이 LoadFromSave / WriteToSave를 호출
/// </summary>
// 매니저들(-100)이 준비된 뒤, 게임 쪽(GameManager)이 세이브를 불러오기 전에 준비
[DefaultExecutionOrder(-80)]
public class GachaManager : MonoBehaviour
{
    public static GachaManager Instance { get; private set; }

    [Header("배너")]
    [Tooltip("상시 + 픽업 배너 (기간이 끝난 픽업도 남겨 두면 교환소에서 그 픽업 장난감을 고를 수 있음)")]
    [SerializeField] private List<GachaBannerDefinition> _banners = new List<GachaBannerDefinition>();

    [Header("재화")]
    [Tooltip("골드 뽑기에 쓰는 골드")]
    [SerializeField] private Currency _gold;

    [Tooltip("겹친 장난감이 덤으로 주는 반짝 조각 (교환소 재화)")]
    [SerializeField] private Currency _shard;

    [Header("처음 선물")]
    [Tooltip("뽑기가 처음 열릴 때 각 배너의 뽑기권을 이만큼 줌")]
    [Min(0)]
    [SerializeField] private int _welcomeTickets = 1;

    /// <summary>바로 저장해 달라는 부탁 (뽑기·교환 직후). GameManager가 듣는다</summary>
    public static event Action SaveRequested;

    /// <summary>뽑았을 때 (분석 로그)</summary>
    public static event Action<GachaPullReport> Pulled;

    /// <summary>천장·포인트·골드 뽑기처럼 화면에 보이는 기록이 바뀌었을 때</summary>
    public event Action OnChanged;

    private readonly GachaPity _standardPity = new GachaPity();
    private readonly GachaPity _pickupPity = new GachaPity();
    private readonly Dictionary<GachaBannerDefinition, GachaTable> _tables = new Dictionary<GachaBannerDefinition, GachaTable>();
    private readonly HashSet<string> _seenBanners = new HashSet<string>();
    private readonly System.Random _random = new System.Random();

    private string _pointsBannerId;
    private int _points;
    private int _goldPullDay;
    private bool _welcomeGiven;
    private int _totalPulls;

    public IReadOnlyList<GachaBannerDefinition> Banners => _banners;
    public Currency Shard => _shard;
    public Currency Gold => _gold;
    public bool IsLoaded { get; private set; }
    public int TotalPulls => _totalPulls;

    /// <summary>뽑기가 열렸는지 (요정이 광장에 온 뒤)</summary>
    public bool IsUnlocked => FairyAccess.IsShopOpen;

    private static DateTime Now => GameClock.Now;
    private static int Today => QuestManager.TodayNumber(GameClock.Now);

    private void Awake()
    {
        // 중복은 GlobalUIRoot가 먼저 꺼서 여기까지 오지 않지만, 혹시 모를 경우를 대비
        if (Instance != null && Instance != this)
            return;

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #region 배너

    /// <summary>지금 열린 배너 (탭 순서)</summary>
    public List<GachaBannerDefinition> OpenBanners()
    {
        var result = new List<GachaBannerDefinition>();
        foreach (var banner in _banners)
        {
            if (banner != null && banner.IsOpenAt(Now) && !Table(banner).IsEmpty)
                result.Add(banner);
        }
        result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return result;
    }

    public bool IsOpen(GachaBannerDefinition banner) => banner != null && IsUnlocked && banner.IsOpenAt(Now);

    public GachaTable Table(GachaBannerDefinition banner)
    {
        if (banner == null)
            throw new ArgumentNullException(nameof(banner));
        if (!_tables.TryGetValue(banner, out var table))
        {
            table = GachaTable.From(banner);
            _tables.Add(banner, table);
        }
        return table;
    }

    public List<GachaRate> Rates(GachaBannerDefinition banner) => GachaRules.Rates(Table(banner));

    private GachaPity Pity(GachaBannerKind kind) => kind == GachaBannerKind.Pickup ? _pickupPity : _standardPity;

    /// <summary>에픽 확정까지 남은 수 (이번이 1번째)</summary>
    public int EpicPityLeft(GachaBannerDefinition banner) => GachaRules.EpicPityLeft(Table(banner), Pity(banner.Kind));

    /// <summary>다음 에픽은 픽업 확정인지 (픽업 에픽을 한 번 놓친 뒤)</summary>
    public bool IsFeaturedGuaranteed(GachaBannerDefinition banner) => banner.IsPickup && _pickupPity.FeaturedGuaranteed;

    /// <summary>처음 보는 배너 (탭에 새 배너 표시)</summary>
    public bool IsNewBanner(GachaBannerDefinition banner) => banner != null && !_seenBanners.Contains(banner.BannerId);

    public void MarkSeen(GachaBannerDefinition banner)
    {
        if (banner != null && !string.IsNullOrEmpty(banner.BannerId) && _seenBanners.Add(banner.BannerId))
            OnChanged?.Invoke();
    }

    #endregion

    #region 값

    /// <summary>이 방법으로 count회 뽑을 때의 재화와 값 (골드 뽑기는 1회만)</summary>
    public (Currency currency, int cost) Price(GachaBannerDefinition banner, int count, GachaPayment payment)
    {
        if (banner == null)
            throw new ArgumentNullException(nameof(banner));
        switch (payment)
        {
            case GachaPayment.Gem:
                return (banner.Gem, banner.GemCost * count);
            case GachaPayment.Ticket:
                return (banner.Ticket, count);
            case GachaPayment.Gold:
                return (_gold, GoldPullCost(banner));
            default:
                throw new ArgumentOutOfRangeException(nameof(payment));
        }
    }

    /// <summary>뽑기권이 count장 있으면 뽑기권, 아니면 조개</summary>
    public GachaPayment PreferredPayment(GachaBannerDefinition banner, int count) =>
        banner.Ticket != null && Balance(banner.Ticket) >= count ? GachaPayment.Ticket : GachaPayment.Gem;

    public int GoldPullCost(GachaBannerDefinition banner) =>
        GachaRules.GoldPullCost(banner.GoldPullBase, banner.GoldPullPerLevel, KingdomLevel);

    /// <summary>오늘 골드 뽑기를 할 수 있는지 (값이 모자라도 true)</summary>
    public bool CanGoldPullToday(GachaBannerDefinition banner) => banner != null && banner.HasDailyGoldPull && _goldPullDay != Today;

    private static int KingdomLevel => ProfileManager.Instance != null ? ProfileManager.Instance.Level : 1;

    private static int Balance(Currency currency) =>
        currency != null && CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCurrency(currency) : 0;

    /// <summary>지금 공짜로 뽑을 수 있는지 (뽑기권 · 오늘의 골드 뽑기 · 처음 선물) — HUD 빨간 점</summary>
    public bool HasFreePull
    {
        get
        {
            if (!IsLoaded || !IsUnlocked)
                return false;
            if (!_welcomeGiven && _welcomeTickets > 0)
                return true;
            foreach (var banner in OpenBanners())
            {
                if (banner.Ticket != null && Balance(banner.Ticket) > 0)
                    return true;
                if (CanGoldPullToday(banner) && Balance(_gold) >= GoldPullCost(banner))
                    return true;
            }
            return false;
        }
    }

    #endregion

    #region 뽑기

    /// <summary>
    /// 뽑는다 (1회 또는 10회). 값을 치른 뒤 결과를 가방에 넣고 저장한다. 실패하면 아무것도 바꾸지 않는다.
    /// </summary>
    public GachaPullOutcome TryPull(GachaBannerDefinition banner, int count, GachaPayment payment, out GachaPullReport report)
    {
        if (banner == null)
            throw new ArgumentNullException(nameof(banner));
        if (count != 1 && count != 10)
            throw new ArgumentOutOfRangeException(nameof(count), "1회 또는 10회만 뽑습니다.");
        if (payment == GachaPayment.Gold && (count != 1 || !banner.HasDailyGoldPull))
            throw new ArgumentException("골드 뽑기는 골드 뽑기가 있는 배너에서 1회만 합니다.", nameof(payment));

        report = null;
        if (!IsOpen(banner))
            return GachaPullOutcome.Closed;
        if (payment == GachaPayment.Gold && !CanGoldPullToday(banner))
            return GachaPullOutcome.GoldUsedToday;

        var (currency, cost) = Price(banner, count, payment);
        if (currency == null)
        {
            Debug.LogWarning($"[GachaManager] {banner.name}에 {payment} 재화가 연결되지 않았습니다.");
            return GachaPullOutcome.NotEnough;
        }
        if (!CurrencyManager.Instance.TrySpend(currency, cost, TransactionSource.GotchaUse))
            return GachaPullOutcome.NotEnough;

        if (payment == GachaPayment.Gold)
            _goldPullDay = Today;
        if (banner.IsPickup)
        {
            MovePointsTo(banner);
            _points += count;
        }

        report = new GachaPullReport { Banner = banner, Payment = payment, Cost = cost };
        var table = Table(banner);
        var pity = Pity(banner.Kind);
        var inventory = InventoryManager.Instance.Inventory;
        int shards = 0;
        for (int i = 0; i < count; i++)
        {
            var roll = GachaRules.Roll(table, pity, _random);
            bool isNew = inventory.GetCount(roll.Item) == 0;
            int added = inventory.Add(roll.Item, 1, ItemChangeReason.Gacha);
            int bonus = isNew ? 0 : GachaRules.ShardsFor(roll.Tier);
            if (added == 0) // 최대 보유 수에 걸림: 그 몫까지 조각으로
                bonus += GachaRules.ShardsFor(roll.Tier);
            shards += bonus;
            report.Pulls.Add(new GachaPull(roll, isNew && added > 0, bonus));
        }
        if (shards > 0 && _shard != null)
            CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(_shard, shards, TransactionSource.GachaReward));

        _totalPulls += count;
        Changed();
        Pulled?.Invoke(report);
        return GachaPullOutcome.Pulled;
    }

    #endregion

    #region 별빛 포인트 (픽업)

    /// <summary>이 픽업 배너에 모은 별빛 포인트</summary>
    public int Points(GachaBannerDefinition banner) =>
        banner != null && banner.IsPickup && _pointsBannerId == banner.BannerId ? _points : 0;

    /// <summary>별빛 포인트로 받는 장난감 (픽업 에픽, 없으면 첫 픽업 장난감)</summary>
    public ItemDefinition PointsReward(GachaBannerDefinition banner)
    {
        if (banner == null || banner.Featured.Count == 0)
            return null;
        var table = Table(banner);
        var epics = table.Featured(GachaTable.Epic);
        return epics.Count > 0 ? epics[0] : banner.Featured[0];
    }

    /// <summary>별빛 포인트로 픽업 에픽을 받는다 (연출에 쓰도록 뽑기 결과 모양으로 돌려줌)</summary>
    public GachaExchangeOutcome TryExchangePoints(GachaBannerDefinition banner, out GachaPull pull)
    {
        if (banner == null)
            throw new ArgumentNullException(nameof(banner));

        pull = default;
        var item = PointsReward(banner);
        if (!IsOpen(banner) || banner.ExchangePoints <= 0 || item == null)
            return GachaExchangeOutcome.NotAvailable;
        if (Points(banner) < banner.ExchangePoints)
            return GachaExchangeOutcome.NotEnough;

        var inventory = InventoryManager.Instance.Inventory;
        if (inventory.GetAddableAmount(item) < 1)
            return GachaExchangeOutcome.Full;

        bool isNew = inventory.GetCount(item) == 0;
        inventory.Add(item, 1, ItemChangeReason.Gacha);
        _points -= banner.ExchangePoints;
        pull = new GachaPull(new GachaRoll(item, GachaTable.TierOf(item), true, false), isNew, 0);
        Changed();
        return GachaExchangeOutcome.Exchanged;
    }

    /// <summary>
    /// 끝난 픽업의 남은 포인트를 반짝 조각으로 바꾼다 (화면을 열 때). 바꾼 포인트 수 (없으면 0)
    /// </summary>
    public int ConvertExpiredPoints()
    {
        if (string.IsNullOrEmpty(_pointsBannerId))
            return 0;
        var banner = FindBanner(_pointsBannerId);
        if (banner != null && banner.IsOpenAt(Now))
            return 0;
        return ConvertPoints();
    }

    // 다른 픽업 배너를 뽑기 시작하면 앞 배너의 포인트를 조각으로 바꾸고 새로 모음
    private void MovePointsTo(GachaBannerDefinition banner)
    {
        if (_pointsBannerId == banner.BannerId)
            return;
        ConvertPoints();
        _pointsBannerId = banner.BannerId;
    }

    private int ConvertPoints()
    {
        int converted = _points;
        if (converted > 0 && _shard != null)
            CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(_shard, converted, TransactionSource.GachaReward));
        _points = 0;
        _pointsBannerId = null;
        if (converted > 0)
            Changed();
        return converted;
    }

    private GachaBannerDefinition FindBanner(string bannerId)
    {
        foreach (var banner in _banners)
        {
            if (banner != null && banner.BannerId == bannerId)
                return banner;
        }
        return null;
    }

    #endregion

    #region 교환소 (반짝 조각)

    /// <summary>
    /// 반짝 조각으로 고를 수 있는 장난감: 열린 상시 배너의 레어·에픽 + 기간이 끝난 픽업 배너의 픽업 장난감 (지금 픽업은 포인트로만)
    /// </summary>
    public List<GachaExchangeEntry> ExchangeEntries()
    {
        var result = new List<GachaExchangeEntry>();
        var added = new HashSet<ItemDefinition>();
        var now = Now;
        foreach (var banner in _banners)
        {
            if (banner == null || banner.IsPickup || !banner.IsOpenAt(now))
                continue;
            var table = Table(banner);
            for (int tier = GachaTable.Epic; tier >= GachaTable.Rare; tier--)
            {
                foreach (var item in table.Standard(tier))
                {
                    if (added.Add(item))
                        result.Add(new GachaExchangeEntry(item, tier, GachaRules.ExchangeCost(tier, false), false));
                }
            }
        }
        foreach (var banner in _banners)
        {
            if (banner == null || !banner.IsPickup || banner.IsOpenAt(now) || banner.EndsAt == null || banner.EndsAt > now)
                continue;
            foreach (var item in banner.Featured)
            {
                if (item != null && added.Add(item))
                    result.Add(new GachaExchangeEntry(item, GachaTable.TierOf(item), GachaRules.ExchangeCost(GachaTable.TierOf(item), true), true));
            }
        }
        return result;
    }

    public GachaExchangeOutcome TryExchangeShards(ItemDefinition item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        GachaExchangeEntry? found = null;
        foreach (var entry in ExchangeEntries())
        {
            if (entry.Item == item)
                found = entry;
        }
        if (found == null || _shard == null || !IsUnlocked)
            return GachaExchangeOutcome.NotAvailable;

        var inventory = InventoryManager.Instance.Inventory;
        if (inventory.GetAddableAmount(item) < 1)
            return GachaExchangeOutcome.Full;
        if (!CurrencyManager.Instance.TrySpend(_shard, found.Value.Cost, TransactionSource.GotchaUse))
            return GachaExchangeOutcome.NotEnough;

        inventory.Add(item, 1, ItemChangeReason.Gacha);
        Changed();
        return GachaExchangeOutcome.Exchanged;
    }

    #endregion

    #region 처음 선물

    /// <summary>처음 선물을 아직 안 받았는지</summary>
    public bool HasWelcomeGift => IsLoaded && IsUnlocked && !_welcomeGiven && _welcomeTickets > 0;

    /// <summary>뽑기가 처음 열리면 열린 배너마다 그 뽑기권을 준다 (한 번). 받은 뽑기권 목록 (없으면 빈 목록)</summary>
    public List<Currency> TryGiveWelcomeGift()
    {
        var given = new List<Currency>();
        if (!HasWelcomeGift)
            return given;

        foreach (var banner in OpenBanners())
        {
            if (banner.Ticket == null || given.Contains(banner.Ticket))
                continue;
            CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(banner.Ticket, _welcomeTickets, TransactionSource.GachaReward));
            given.Add(banner.Ticket);
        }
        _welcomeGiven = true;
        Changed();
        return given;
    }

    public int WelcomeTickets => _welcomeTickets;

    #endregion

    #region 세이브

    public void LoadFromSave(GachaSaveData saved)
    {
        if (saved == null)
            throw new ArgumentNullException(nameof(saved));

        _standardPity.Set(saved.standardSinceEpic, saved.standardSinceRare, false);
        _pickupPity.Set(saved.pickupSinceEpic, saved.pickupSinceRare, saved.pickupGuaranteed);
        _pointsBannerId = string.IsNullOrEmpty(saved.pointsBannerId) ? null : saved.pointsBannerId;
        _points = Math.Max(0, saved.points);
        _goldPullDay = saved.goldPullDay;
        _welcomeGiven = saved.welcomeGiftGiven;
        _totalPulls = Math.Max(0, saved.totalPulls);
        _seenBanners.Clear();
        if (saved.seenBanners != null)
        {
            foreach (var id in saved.seenBanners)
            {
                if (!string.IsNullOrEmpty(id))
                    _seenBanners.Add(id);
            }
        }
        IsLoaded = true;
        OnChanged?.Invoke();
    }

    public void WriteToSave(GachaSaveData result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        result.standardSinceEpic = _standardPity.SinceEpic;
        result.standardSinceRare = _standardPity.SinceRare;
        result.pickupSinceEpic = _pickupPity.SinceEpic;
        result.pickupSinceRare = _pickupPity.SinceRare;
        result.pickupGuaranteed = _pickupPity.FeaturedGuaranteed;
        result.pointsBannerId = _pointsBannerId;
        result.points = _points;
        result.goldPullDay = _goldPullDay;
        result.welcomeGiftGiven = _welcomeGiven;
        result.totalPulls = _totalPulls;
        result.seenBanners ??= new List<string>();
        result.seenBanners.Clear();
        result.seenBanners.AddRange(_seenBanners);
    }

    #endregion

    // 기록이 바뀜: 화면 갱신 + 바로 저장
    private void Changed()
    {
        OnChanged?.Invoke();
        SaveRequested?.Invoke();
    }
}
