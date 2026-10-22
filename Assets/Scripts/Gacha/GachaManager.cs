using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>교환소 한 줄 (반짝 조각으로 고르는 특수 해달 조각 묶음)</summary>
public readonly struct GachaExchangeEntry
{
    public readonly SpecialOtterDefinition Otter;
    public readonly Sprite Icon;
    public readonly int Count;
    public readonly int Cost;

    public GachaExchangeEntry(SpecialOtterDefinition otter, Sprite icon, int count, int cost)
    {
        Otter = otter;
        Icon = icon;
        Count = count;
        Cost = cost;
    }

    public int Tier => Otter != null ? Otter.Tier : GachaTable.Rare;
}

/// <summary>
/// 보물 조개 뽑기의 주인. 전역 UI(GlobalUI) 루트에 붙어 씬을 넘어 유지된다.
/// 값 치르기 → 뽑기(GachaRewardRules) → 재료는 가방, 장신구 · 해달 조각은 명부(CompanionCollection) → 넘친 몫은 반짝 조각 → 바로 저장 순서로,
/// 연출보다 먼저 결과를 확정한다
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

    [Header("특수 해달 · 장신구 (가챠 개편)")]
    [Tooltip("새 뽑기 보상 · 특수 해달 · 장신구 목록 (Docs/가챠개편_특수해달조각_장신구_기획.md). 비어 있어도 명부 세이브는 그대로 지킴")]
    [SerializeField] private GachaRewardConfig _rewardConfig;

    /// <summary>바로 저장해 달라는 부탁 (뽑기·교환 직후). GameManager가 듣는다</summary>
    public static event Action SaveRequested;

    /// <summary>뽑았을 때 (분석 로그)</summary>
    public static event Action<GachaPullReport> Pulled;

    /// <summary>천장·포인트·골드 뽑기처럼 화면에 보이는 기록이 바뀌었을 때</summary>
    public event Action OnChanged;

    private readonly GachaPity _standardPity = new GachaPity();
    private readonly GachaPity _pickupPity = new GachaPity();
    private readonly GachaPity _goldPity = new GachaPity();
    private readonly Dictionary<GachaBannerDefinition, GachaRewardTable> _tables = new Dictionary<GachaBannerDefinition, GachaRewardTable>();
    private readonly HashSet<string> _seenBanners = new HashSet<string>();
    private readonly System.Random _random = new System.Random();

    private string _pointsBannerId;
    private int _points;
    private int _goldPullDay;
    private bool _welcomeGiven;
    private bool _starterGiven;
    private int _totalPulls;
    private CompanionCollection _companions;

    public IReadOnlyList<GachaBannerDefinition> Banners => _banners;
    public GachaRewardConfig RewardConfig => _rewardConfig;

    /// <summary>특수 해달 명부 · 편성 · 장신구 (세이브는 이 매니저가 함께 읽고 씀)</summary>
    public CompanionCollection Companions =>
        _companions ??= new CompanionCollection(id => _rewardConfig != null ? _rewardConfig.FindAccessory(id) : null);
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

    /// <summary>지금 열린 배너 (탭 순서). 골드 배너는 가챠 개편으로 정리 (상시 배너의 하루 1회 골드 뽑기만 남김)</summary>
    public List<GachaBannerDefinition> OpenBanners()
    {
        var result = new List<GachaBannerDefinition>();
        foreach (var banner in _banners)
        {
            if (banner == null || banner.IsGoldBanner || !banner.IsOpenAt(Now))
                continue;
            var table = Table(banner);
            if (table != null && !table.IsEmpty)
                result.Add(banner);
        }
        result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return result;
    }

    public bool IsOpen(GachaBannerDefinition banner) => banner != null && IsUnlocked && banner.IsOpenAt(Now);

    /// <summary>이 배너의 보상 표 (보상 설정이 없으면 null)</summary>
    public GachaRewardTable Table(GachaBannerDefinition banner)
    {
        if (banner == null)
            throw new ArgumentNullException(nameof(banner));
        if (_rewardConfig == null)
            return null;
        if (!_tables.TryGetValue(banner, out var table))
        {
            table = _rewardConfig.BuildTable(banner.Kind, banner.EpicPity, banner.RarePity, banner.FeaturedShare, banner.FeaturedSpecialOtters);
            _tables.Add(banner, table);
        }
        return table;
    }

    public List<GachaRewardRate> Rates(GachaBannerDefinition banner)
    {
        var table = Table(banner);
        return table != null ? GachaRewardRules.Rates(table) : new List<GachaRewardRate>();
    }

    private GachaPity Pity(GachaBannerKind kind) =>
        kind == GachaBannerKind.Pickup ? _pickupPity : kind == GachaBannerKind.Gold ? _goldPity : _standardPity;

    /// <summary>레어 이상 확정까지 남은 수 (이번이 1번째)</summary>
    public int RarePityLeft(GachaBannerDefinition banner) => Math.Max(1, banner.RarePity - Pity(banner.Kind).SinceRare);

    /// <summary>에픽 해달 조각 확정까지 남은 수 (이번이 1번째)</summary>
    public int EpicPityLeft(GachaBannerDefinition banner) => Math.Max(1, banner.EpicPity - Pity(banner.Kind).SinceEpic);

    /// <summary>다음 에픽 해달 조각은 픽업 확정인지 (픽업을 한 번 놓친 뒤)</summary>
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

    /// <summary>이 방법으로 count회 뽑을 때의 재화와 값 (상시 배너의 골드 뽑기는 1회만, 골드 배너는 1회 · 10회)</summary>
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
                return (_gold, banner.IsGoldBanner ? GoldBannerCost(banner, count) : GoldPullCost(banner));
            default:
                throw new ArgumentOutOfRangeException(nameof(payment));
        }
    }

    /// <summary>골드 배너는 골드, 아니면 뽑기권이 count장 있으면 뽑기권, 없으면 조개</summary>
    public GachaPayment PreferredPayment(GachaBannerDefinition banner, int count) =>
        banner.IsGoldBanner ? GachaPayment.Gold
        : banner.Ticket != null && Balance(banner.Ticket) >= count ? GachaPayment.Ticket : GachaPayment.Gem;

    /// <summary>골드 배너 값 (count = 1 또는 10, 왕국 레벨에 따라)</summary>
    public int GoldBannerCost(GachaBannerDefinition banner, int count) =>
        GachaRules.GoldBannerCost(banner.GoldCostBase, banner.GoldCostPerLevel, KingdomLevel, count, banner.GoldTenPulls);

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
        bool dailyGold = payment == GachaPayment.Gold && !banner.IsGoldBanner;
        if (dailyGold && (count != 1 || !banner.HasDailyGoldPull))
            throw new ArgumentException("골드 뽑기는 골드 뽑기가 있는 배너에서 1회만 합니다.", nameof(payment));
        if (banner.IsGoldBanner && payment != GachaPayment.Gold)
            throw new ArgumentException("골드 배너는 골드로만 뽑습니다.", nameof(payment));

        report = null;
        var table = Table(banner);
        if (!IsOpen(banner) || table == null || table.IsEmpty)
            return GachaPullOutcome.Closed;
        if (dailyGold && !CanGoldPullToday(banner))
            return GachaPullOutcome.GoldUsedToday;

        var (currency, cost) = Price(banner, count, payment);
        if (currency == null)
        {
            Debug.LogWarning($"[GachaManager] {banner.name}에 {payment} 재화가 연결되지 않았습니다.");
            return GachaPullOutcome.NotEnough;
        }
        if (!CurrencyManager.Instance.TrySpend(currency, cost, TransactionSource.GotchaUse))
            return GachaPullOutcome.NotEnough;

        if (dailyGold)
            _goldPullDay = Today;
        if (banner.IsPickup)
        {
            MovePointsTo(banner);
            _points += count;
        }

        report = new GachaPullReport { Banner = banner, Payment = payment, Cost = cost };
        var pity = Pity(banner.Kind);
        int shards = 0;
        for (int i = 0; i < count; i++)
        {
            var pull = Grant(GachaRewardRules.Roll(table, pity, _random));
            shards += pull.Shards;
            report.Pulls.Add(pull);
        }
        GiveShards(shards);

        _totalPulls += count;
        Changed();
        Pulled?.Invoke(report);
        return GachaPullOutcome.Pulled;
    }

    /// <summary>뽑은 것을 넣고 결과 카드 모양으로: 재료는 가방(못 넣은 몫 10개당 반짝 조각 1, 최소 1), 장신구 · 해달 조각은 명부</summary>
    private GachaPull Grant(GachaReward reward)
    {
        switch (reward.Kind)
        {
            case GachaRewardKind.Materials:
            {
                var inventory = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
                int missing = reward.Wood - AddItem(inventory, _rewardConfig.Wood, reward.Wood)
                    + reward.Stone - AddItem(inventory, _rewardConfig.Stone, reward.Stone);
                int shards = missing > 0 ? Math.Max(1, missing / 10) : 0;
                string text = GachaRewardText.Materials(reward.Wood, reward.Stone);
                return new GachaPull(reward.Kind, reward.Tier, false, reward.ByPity, false, shards,
                    _rewardConfig.Wood != null ? _rewardConfig.Wood.Icon : null, "재료 꾸러미", "목공소 · 집 공사에 쓰는 목재와 돌이에요.",
                    shards > 0 ? $"{text} · 가방이 가득해 반짝 조각 +{shards}" : text);
            }
            case GachaRewardKind.Accessory:
            {
                var accessory = reward.Accessory;
                var gain = Companions.AddAccessory(accessory, out int shards);
                int plus = Companions.Plus(accessory);
                string effect = GachaRewardText.Effect(accessory.Effect, accessory.PercentAt(plus));
                string detail = gain == AccessoryGain.New ? $"새 장신구! {effect}"
                    : gain == AccessoryGain.Enhanced ? $"강화 +{plus}! {effect}"
                    : $"최대 강화라 반짝 조각 +{shards}";
                return new GachaPull(reward.Kind, reward.Tier, false, reward.ByPity, gain == AccessoryGain.New, shards,
                    _rewardConfig.IconOf(accessory), accessory.DisplayName, accessory.Description, detail, accessory: accessory);
            }
            default:
                return GrantFragments(reward.Otter, reward.Fragments, reward.Featured, reward.ByPity);
        }
    }

    // 해달 조각을 명부에 (5성까지 쓰고 남는 몫은 반짝 조각 — 넘겨주는 쪽이 GiveShards)
    private GachaPull GrantFragments(SpecialOtterDefinition otter, int count, bool featured, bool byPity)
    {
        bool isNew = !Companions.HasMet(otter);
        int shards = Companions.AddFragments(otter, count);
        int stars = Companions.Stars(otter);
        int need = CompanionCollection.NextStarCost(otter, stars);
        int have = Companions.Fragments(otter);
        string detail = need <= 0 ? $"조각 ×{count} · 5성이라 반짝 조각 +{shards}"
            : stars == 0 ? $"조각 ×{count} · 해금까지 {have}/{need}"
            : $"조각 ×{count} · {stars + 1}성까지 {have}/{need}";
        if (need > 0 && shards > 0)
            detail += $" · 반짝 조각 +{shards}";
        return new GachaPull(GachaRewardKind.Fragments, otter.Tier, featured, byPity, isNew, shards,
            _rewardConfig != null ? _rewardConfig.IconOf(otter) : otter.Portrait, GachaRewardText.FragmentName(otter), otter.Description, detail, otter);
    }

    private static int AddItem(Inventory inventory, ItemDefinition item, int amount) =>
        amount <= 0 || inventory == null || item == null ? 0 : inventory.Add(item, amount, ItemChangeReason.Gacha);

    private void GiveShards(int shards)
    {
        if (shards > 0 && _shard != null && CurrencyManager.Instance != null)
            CurrencyManager.Instance.ProcessTransaction(new CurrencyTransaction(_shard, shards, TransactionSource.GachaReward));
    }

    #endregion

    #region 별빛 포인트 (픽업)

    /// <summary>이 픽업 배너에 모은 별빛 포인트</summary>
    public int Points(GachaBannerDefinition banner) =>
        banner != null && banner.IsPickup && _pointsBannerId == banner.BannerId ? _points : 0;

    /// <summary>별빛 포인트로 받는 픽업 해달 (픽업 에픽, 없으면 첫 픽업 해달)</summary>
    public SpecialOtterDefinition PointsReward(GachaBannerDefinition banner)
    {
        if (banner == null || banner.FeaturedSpecialOtters.Count == 0)
            return null;
        foreach (var otter in banner.FeaturedSpecialOtters)
        {
            if (otter != null && otter.IsEpic)
                return otter;
        }
        return banner.FeaturedSpecialOtters[0];
    }

    /// <summary>별빛 포인트로 받는 조각 수</summary>
    public int PointsFragments => _rewardConfig != null ? _rewardConfig.PointsFragments : 0;

    /// <summary>그 해달의 그림 (그림이 아직 없으면 대신 쓰는 그림)</summary>
    public Sprite IconOf(SpecialOtterDefinition otter) => _rewardConfig != null ? _rewardConfig.IconOf(otter) : otter != null ? otter.Portrait : null;

    /// <summary>별빛 포인트로 픽업 해달 조각을 받는다 (연출에 쓰도록 뽑기 결과 모양으로 돌려줌)</summary>
    public GachaExchangeOutcome TryExchangePoints(GachaBannerDefinition banner, out GachaPull pull)
    {
        if (banner == null)
            throw new ArgumentNullException(nameof(banner));

        pull = default;
        var otter = PointsReward(banner);
        if (!IsOpen(banner) || banner.ExchangePoints <= 0 || otter == null || _rewardConfig == null)
            return GachaExchangeOutcome.NotAvailable;
        if (Points(banner) < banner.ExchangePoints)
            return GachaExchangeOutcome.NotEnough;

        _points -= banner.ExchangePoints;
        pull = GrantFragments(otter, PointsFragments, true, false);
        GiveShards(pull.Shards);
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

    /// <summary>반짝 조각으로 고를 수 있는 특수 해달 조각 묶음 (5성까지 조각이 더 필요한 해달만, 에픽 먼저)</summary>
    public List<GachaExchangeEntry> ExchangeEntries()
    {
        var result = new List<GachaExchangeEntry>();
        if (_rewardConfig == null)
            return result;
        int count = _rewardConfig.ExchangeFragments;
        for (int tier = GachaTable.Epic; tier >= GachaTable.Rare; tier--)
        {
            foreach (var otter in _rewardConfig.Otters)
            {
                if (otter == null || otter.Tier != tier || RoomFor(otter) <= 0)
                    continue;
                result.Add(new GachaExchangeEntry(otter, _rewardConfig.IconOf(otter), count, CompanionCollection.ExchangeCost(otter) * count));
            }
        }
        return result;
    }

    // 5성까지 더 받을 수 있는 조각 수
    private int RoomFor(SpecialOtterDefinition otter) =>
        CompanionCollection.CostToMax(otter, Companions.Stars(otter)) - Companions.Fragments(otter);

    public GachaExchangeOutcome TryExchangeShards(SpecialOtterDefinition otter)
    {
        if (otter == null)
            throw new ArgumentNullException(nameof(otter));
        if (_shard == null || !IsUnlocked || _rewardConfig == null || _rewardConfig.FindOtter(otter.OtterId) == null)
            return GachaExchangeOutcome.NotAvailable;

        GachaExchangeEntry? found = null;
        foreach (var entry in ExchangeEntries())
        {
            if (entry.Otter == otter)
                found = entry;
        }
        if (found == null)
            return GachaExchangeOutcome.Full;
        if (!CurrencyManager.Instance.TrySpend(_shard, found.Value.Cost, TransactionSource.GotchaUse))
            return GachaExchangeOutcome.NotEnough;

        GiveShards(Companions.AddFragments(otter, found.Value.Count));
        Changed();
        return GachaExchangeOutcome.Exchanged;
    }

    #endregion

    #region 명부 · 편성 · 장신구 (명부 화면이 부름, 바뀌면 바로 저장)

    /// <summary>열린 편성 칸: 처음 1칸, 첫 개간 뒤 2칸, 밭 농사를 시작하면 3칸</summary>
    public int FormationSlots
    {
        get
        {
            var settlement = SettlementManager.Instance;
            if (settlement == null || !settlement.IsLoaded)
                return 1;
            int slots = 1;
            if (settlement.HasDevelopment(TerritoryRules.TutorialDone) || settlement.HasDevelopment(TerritoryRules.FirstDevelopment))
                slots++;
            if (settlement.HasDevelopment(SettlementQuestGate.FarmProductionDevelopment))
                slots++;
            return slots;
        }
    }

    /// <summary>편성된 특수 해달들이 주는 이 종류의 왕국 버프 % (SettlementManager가 건물 · 구역과 합산)</summary>
    public int FormationPercent(KingdomBonusKind kind) =>
        IsLoaded && _rewardConfig != null ? Companions.FormationPercent(kind, FormationSlots, _rewardConfig.FindOtter) : 0;

    /// <summary>해금하거나 별을 하나 올림</summary>
    public bool TryStarUp(SpecialOtterDefinition otter)
    {
        if (otter == null || !Companions.TryStarUp(otter))
            return false;
        Changed();
        return true;
    }

    public FormationOutcome AssignFormation(int slot, SpecialOtterDefinition otter)
    {
        var outcome = _rewardConfig != null ? Companions.Assign(slot, otter, FormationSlots, _rewardConfig.FindOtter) : FormationOutcome.Locked;
        if (outcome == FormationOutcome.Assigned)
            Changed();
        return outcome;
    }

    public void ClearFormation(int slot)
    {
        Companions.ClearSlot(slot);
        Changed();
    }

    public bool EquipAccessory(AccessoryDefinition accessory, string otterId)
    {
        if (!Companions.Equip(accessory, otterId))
            return false;
        Changed();
        return true;
    }

    public void UnequipAccessory(AccessoryDefinition accessory)
    {
        Companions.Unequip(accessory);
        Changed();
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

    /// <summary>
    /// 처음 선물 특수 해달 (음유시인): 뽑기가 열려 있고 아직 안 받았으면 해금해 첫 응원 칸에 넣는다 (칸이 차 있으면 명부에만).
    /// 이미 조각 · 별이 있는 세이브는 해금까지 모자란 만큼만 채움. 받은 해달 (없으면 null)
    /// </summary>
    public SpecialOtterDefinition TryGiveStarterOtter()
    {
        var otter = _rewardConfig != null ? _rewardConfig.StarterOtter : null;
        if (!IsLoaded || !IsUnlocked || _starterGiven || otter == null)
            return null;
        _starterGiven = true;
        if (!Companions.IsUnlocked(otter))
        {
            Companions.AddFragments(otter, Math.Max(0, CompanionCollection.NextStarCost(otter, 0) - Companions.Fragments(otter)));
            Companions.TryStarUp(otter);
        }
        if (string.IsNullOrEmpty(Companions.Formation[0]))
            Companions.Assign(0, otter, FormationSlots, _rewardConfig.FindOtter);
        Changed();
        return otter;
    }

    #endregion

    #region 세이브

    public void LoadFromSave(GachaSaveData saved)
    {
        if (saved == null)
            throw new ArgumentNullException(nameof(saved));

        _standardPity.Set(saved.standardSinceEpic, saved.standardSinceRare, false);
        _pickupPity.Set(saved.pickupSinceEpic, saved.pickupSinceRare, saved.pickupGuaranteed);
        _goldPity.Set(saved.goldSinceEpic, saved.goldSinceRare, false);
        _pointsBannerId = string.IsNullOrEmpty(saved.pointsBannerId) ? null : saved.pointsBannerId;
        _points = Math.Max(0, saved.points);
        _goldPullDay = saved.goldPullDay;
        _welcomeGiven = saved.welcomeGiftGiven;
        _starterGiven = saved.starterOtterGiven;
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
        Companions.Load(saved);
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
        result.goldSinceEpic = _goldPity.SinceEpic;
        result.goldSinceRare = _goldPity.SinceRare;
        result.pointsBannerId = _pointsBannerId;
        result.points = _points;
        result.goldPullDay = _goldPullDay;
        result.welcomeGiftGiven = _welcomeGiven;
        result.starterOtterGiven = _starterGiven;
        result.totalPulls = _totalPulls;
        result.seenBanners ??= new List<string>();
        result.seenBanners.Clear();
        result.seenBanners.AddRange(_seenBanners);
        Companions.Write(result);
    }

    #endregion

    // 기록이 바뀜: 화면 갱신 + 바로 저장
    private void Changed()
    {
        OnChanged?.Invoke();
        SaveRequested?.Invoke();
    }
}
