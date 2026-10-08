using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 보물 조개 뽑기 흐름: HUD 뽑기 버튼 → 화면(배너 고르기) → 뽑기(값 치르기 · 결과 확정은 GachaManager) → 연출 → 결과 → [한 번 더].
/// 조개가 모자라면 재화 부족 팝업 → [충전하기]는 CurrencyShopPresenter가 처리. 처음 열면 요정의 선물(뽑기권),
/// 끝난 픽업의 남은 별빛 포인트는 반짝 조각으로. 전역 UI 루트에 붙어 늘 켜져 있으므로 Instance로 어디서든 연다.
/// </summary>
public class GachaPresenter : MonoBehaviour
{
    private static readonly string[] TierNames = { "흔함", "레어", "에픽" };

    public static GachaPresenter Instance { get; private set; }

    [Header("화면")]
    [SerializeField] private GachaScreenView _screen;

    [SerializeField] private GachaRevealView _reveal;

    [SerializeField] private GachaRatesPopupView _rates;

    [SerializeField] private GachaExchangePopupView _exchange;

    [Tooltip("재화 부족 팝업 (재화 충전 화면과 같은 것)")]
    [SerializeField] private CurrencyShortagePopupView _shortage;

    [Tooltip("HUD 오른쪽의 뽑기 버튼")]
    [SerializeField] private GachaHudButtonView _hudButton;

    [Header("버튼 그림 (값 치르는 방법)")]
    [Tooltip("조개로 뽑기 (연보라)")]
    [SerializeField] private Sprite _gemButton;

    [Tooltip("뽑기권으로 뽑기 (초록)")]
    [SerializeField] private Sprite _ticketButton;

    [Tooltip("골드 배너 뽑기 버튼 (겨자)")]
    [SerializeField] private Sprite _goldButton;

    private readonly GachaScreenState _state = new GachaScreenState();
    private GachaBannerDefinition _selected;
    private int _lastCount = 1;
    private bool _dirty;
    private bool _glyphsReady;
    private CurrencyManager _boundCurrency;
    private GachaManager _boundManager;

    public bool IsOpen => _screen.IsOpen;

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

    private void OnEnable()
    {
        _hudButton.OnClicked += Open;
        _screen.OnCloseClicked += HandleCloseClicked;
        _screen.OnBannerSelected += HandleBannerSelected;
        _screen.OnPullClicked += HandlePullClicked;
        _screen.OnGoldPullClicked += HandleGoldPullClicked;
        _screen.OnRatesClicked += HandleRatesClicked;
        _screen.OnExchangeClicked += HandleExchangeClicked;
        _screen.OnPointsClicked += HandlePointsClicked;
        _reveal.OnClosed += HandleRevealClosed;
        _reveal.OnAgain += HandleAgain;
        _exchange.OnExchange += HandleShardExchange;
    }

    private void OnDisable()
    {
        _hudButton.OnClicked -= Open;
        _screen.OnCloseClicked -= HandleCloseClicked;
        _screen.OnBannerSelected -= HandleBannerSelected;
        _screen.OnPullClicked -= HandlePullClicked;
        _screen.OnGoldPullClicked -= HandleGoldPullClicked;
        _screen.OnRatesClicked -= HandleRatesClicked;
        _screen.OnExchangeClicked -= HandleExchangeClicked;
        _screen.OnPointsClicked -= HandlePointsClicked;
        _reveal.OnClosed -= HandleRevealClosed;
        _reveal.OnAgain -= HandleAgain;
        _exchange.OnExchange -= HandleShardExchange;
        Bind(null, null);
    }

    // 재화 · 뽑기 기록이 바뀌면 열린 화면을 다시 그림. 매니저들은 늦게 생길 수 있어 바뀌었는지만 보고 다시 붙음
    private void Update()
    {
        Bind(CurrencyManager.Instance, GachaManager.Instance);
        if (_dirty && _screen.IsOpen)
        {
            _dirty = false;
            Refresh();
        }
    }

    private void Bind(CurrencyManager currency, GachaManager manager)
    {
        if (_boundCurrency != currency)
        {
            if (_boundCurrency != null)
                _boundCurrency.OnCurrencyChanged -= HandleCurrencyChanged;
            _boundCurrency = currency;
            if (_boundCurrency != null)
                _boundCurrency.OnCurrencyChanged += HandleCurrencyChanged;
        }
        if (_boundManager != manager)
        {
            if (_boundManager != null)
                _boundManager.OnChanged -= MarkDirty;
            _boundManager = manager;
            if (_boundManager != null)
                _boundManager.OnChanged += MarkDirty;
        }
    }

    private void HandleCurrencyChanged(Currency currency, int balance) => MarkDirty();

    private void MarkDirty() => _dirty = true;

    #region 열기 · 닫기

    /// <summary>뽑기 화면을 연다 (뽑기가 아직 안 열렸으면 아무것도 안 함)</summary>
    public void Open()
    {
        var manager = GachaManager.Instance;
        if (manager == null || !manager.IsLoaded || !manager.IsUnlocked)
            return;
        var banners = manager.OpenBanners();
        if (banners.Count == 0)
            return;

        if (!_glyphsReady)
            PrepareGlyphs(manager);
        if (_selected == null || !banners.Contains(_selected))
            _selected = banners[0];
        manager.MarkSeen(_selected);
        Refresh();
        _screen.Show();

        // 요정의 선물(처음 한 번) · 끝난 픽업의 남은 별빛 포인트 → 반짝 조각
        var lines = new List<string>();
        var gifts = manager.TryGiveWelcomeGift();
        if (gifts.Count > 0)
        {
            var names = new List<string>();
            foreach (var ticket in gifts)
                names.Add($"{ticket.DisplayName} {manager.WelcomeTickets}장");
            lines.Add($"요정의 선물! {string.Join(" · ", names)}을 받았어요");
        }
        int converted = manager.ConvertExpiredPoints();
        if (converted > 0)
            lines.Add($"지난 픽업의 별빛 포인트 {converted}점이 반짝 조각이 됐어요");
        if (lines.Count > 0)
            _screen.Notify(string.Join("\n", lines));
        Refresh();
    }

    private void HandleCloseClicked() => _screen.Hide();

    private void HandleBannerSelected(GachaBannerDefinition banner)
    {
        if (banner == null || banner == _selected)
            return;
        _selected = banner;
        GachaManager.Instance.MarkSeen(banner);
        Refresh();
    }

    private void HandleRevealClosed() => Refresh();

    #endregion

    #region 뽑기

    private void HandlePullClicked(int count)
    {
        var manager = GachaManager.Instance;
        Pull(count, manager.PreferredPayment(_selected, count));
    }

    private void HandleGoldPullClicked()
    {
        var manager = GachaManager.Instance;
        if (!manager.CanGoldPullToday(_selected))
        {
            _screen.Notify("골드 뽑기는 하루에 한 번이에요.\n내일 다시 와요!");
            return;
        }
        Pull(1, GachaPayment.Gold);
    }

    // 결과 화면의 [한 번 더]: 같은 횟수로, 그때 쓸 수 있는 값으로
    private void HandleAgain()
    {
        var manager = GachaManager.Instance;
        Pull(_lastCount, manager.PreferredPayment(_selected, _lastCount));
    }

    private void Pull(int count, GachaPayment payment)
    {
        var manager = GachaManager.Instance;
        var outcome = manager.TryPull(_selected, count, payment, out var report);
        switch (outcome)
        {
            case GachaPullOutcome.Pulled:
                _lastCount = count;
                if (_rates.IsOpen)
                    _rates.Hide();
                _reveal.Play(report, AgainLook(count), Describe);
                break;

            case GachaPullOutcome.NotEnough:
                var (currency, cost) = manager.Price(_selected, count, payment);
                if (payment == GachaPayment.Gold)
                    _screen.Notify($"골드가 {NumberFormatter.Short(cost - Balance(currency))} 모자라요");
                else
                    _shortage.Show(currency, cost, Balance(currency));
                break;

            case GachaPullOutcome.GoldUsedToday:
                _screen.Notify("골드 뽑기는 하루에 한 번이에요.\n내일 다시 와요!");
                break;

            case GachaPullOutcome.Closed:
                _screen.Notify("이 뽑기는 기간이 끝났어요");
                break;
        }
        Refresh();
    }

    // 꾸미기 가구 (의자 · 가로등 · 피크닉 매트 등, 장난감이 아님)
    private static bool IsFurniture(ItemDefinition item)
    {
        var decor = DecorManager.Instance != null && DecorManager.Instance.Catalog != null ? DecorManager.Instance.Catalog.FindByItem(item) : null;
        return decor != null && decor.IsFurniture;
    }

    // 결과 카드 안내: 픽업 에픽 = 그 장난감을 좋아하는 한정 해달, 가구 = 해달이 쓰는 자리 · 아늑함, 나머지는 등급별 한 줄
    private (string hint, Sprite portrait) Describe(GachaPull pull)
    {
        var otter = _selected != null ? _selected.FeaturedOtter : null;
        if (otter != null && otter.FavoriteToy == pull.Item)
        {
            string name = otter.DisplayName;
            return ($"광장에 두면 한정 해달 {name}{KoreanParticle.SubjectParticle(name)} 놀러 와요!", otter.Portrait);
        }
        var furniture = DecorManager.Instance != null && DecorManager.Instance.Catalog != null ? DecorManager.Instance.Catalog.FindByItem(pull.Item) : null;
        if (furniture != null && furniture.IsFurniture)
        {
            string use = !furniture.HasSpot ? "광장이 푸릇해져요"
                : furniture.SpotKind == PlazaSpotKind.Sit ? "해달이 앉아 쉬어 가요"
                : furniture.SpotKind == PlazaSpotKind.Eat ? "해달이 둘러앉아 간식을 먹어요"
                : "밤이면 해달이 불빛 아래 모여요";
            return ($"광장에 두면 {use} (아늑함 +{furniture.Coziness})", null);
        }
        switch (pull.Tier)
        {
            case GachaTable.Epic:
                return ("광장에 두면 어떤 해달이든 놀러 올 수 있어요", null);
            case GachaTable.Rare:
                return ("광장에 두면 레어 해달도 놀러 와요", null);
            default:
                return ("광장에 두면 해달 친구가 놀러 와요", null);
        }
    }

    private GachaAgainInfo AgainLook(int count)
    {
        var look = PriceLook(_selected, count);
        return new GachaAgainInfo(count == 1 ? "한 번 더" : "10회 더", look.Button, look.Icon, look.Cost);
    }

    private GachaPriceLook PriceLook(GachaBannerDefinition banner, int count)
    {
        var manager = GachaManager.Instance;
        var payment = manager.PreferredPayment(banner, count);
        var (currency, cost) = manager.Price(banner, count, payment);
        var button = payment == GachaPayment.Ticket ? _ticketButton
            : payment == GachaPayment.Gold && _goldButton != null ? _goldButton
            : _gemButton;
        return new GachaPriceLook(button, currency != null ? currency.Icon : null, cost.ToString("N0"));
    }

    private static int Balance(Currency currency) =>
        currency != null && CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCurrency(currency) : 0;

    #endregion

    #region 별빛 포인트 · 교환소 · 확률

    private void HandlePointsClicked()
    {
        var manager = GachaManager.Instance;
        var outcome = manager.TryExchangePoints(_selected, out var pull);
        var reward = manager.PointsReward(_selected);
        string name = reward != null ? reward.DisplayName : "픽업 장난감";
        switch (outcome)
        {
            case GachaExchangeOutcome.Exchanged:
                var report = new GachaPullReport { Banner = _selected, Payment = GachaPayment.Ticket };
                report.Pulls.Add(pull);
                _lastCount = 1;
                _reveal.Play(report, AgainLook(1), Describe);
                break;
            case GachaExchangeOutcome.NotEnough:
                _screen.Notify($"별빛 포인트 {_selected.ExchangePoints}점을 모으면\n{name}{KoreanParticle.ObjectParticle(name)} 받을 수 있어요");
                break;
            case GachaExchangeOutcome.Full:
                _screen.Notify($"{name}{KoreanParticle.ObjectParticle(name)} 더 가질 수 없어요");
                break;
            default:
                _screen.Notify("지금은 바꿀 수 없어요");
                break;
        }
        Refresh();
    }

    private void HandleExchangeClicked()
    {
        var manager = GachaManager.Instance;
        _exchange.Show(manager.ExchangeEntries(), Balance(manager.Shard), ExchangeNote());
    }

    private static string ExchangeNote()
    {
        var s = GachaRules.DuplicateShards;
        return $"이미 있는 장난감이 나오면 반짝 조각을 덤으로 줘요 (흔함 {s[0]} · 레어 {s[1]} · 에픽 {s[2]})";
    }

    private void HandleShardExchange(ItemDefinition item)
    {
        var manager = GachaManager.Instance;
        string name = item.DisplayName;
        string note;
        switch (manager.TryExchangeShards(item))
        {
            case GachaExchangeOutcome.Exchanged:
                AudioManager.Play(SfxKind.RevealRare);
                note = $"{name}{KoreanParticle.ObjectParticle(name)} 받았어요! 꾸미기에서 광장에 놓아 보세요";
                break;
            case GachaExchangeOutcome.NotEnough:
                _exchange.Shake();
                note = "반짝 조각이 모자라요";
                break;
            case GachaExchangeOutcome.Full:
                note = $"{name}{KoreanParticle.ObjectParticle(name)} 더 가질 수 없어요";
                break;
            default:
                note = "지금은 바꿀 수 없어요";
                break;
        }
        _exchange.Render(manager.ExchangeEntries(), Balance(manager.Shard), note);
        Refresh();
    }

    private void HandleRatesClicked()
    {
        _rates.Show($"{_selected.Title} 확률", RatesText(_selected));
    }

    /// <summary>확률 공개: 한 번 뽑을 때 장난감별 확률(천장 제외) + 확정 규칙</summary>
    private static string RatesText(GachaBannerDefinition banner)
    {
        var manager = GachaManager.Instance;
        var table = manager.Table(banner);
        var rates = manager.Rates(banner);
        var text = new StringBuilder();
        text.Append("<size=26><color=#A08672>한 번 뽑을 때의 확률이에요. 확정 규칙은 아래에 있어요.</color></size>\n\n");
        for (int tier = GachaTable.Epic; tier >= GachaTable.Common; tier--)
        {
            double chance = table.TierChance(tier);
            if (chance <= 0)
                continue;
            text.Append($"<size=38><b>{TierNames[tier]}  {Percent(chance)}</b></size>\n");
            foreach (var rate in rates)
            {
                if (rate.Tier != tier)
                    continue;
                text.Append(rate.Featured ? "<color=#E5484D>★ 픽업</color>  " : "·  ");
                text.Append(rate.Item.DisplayName).Append("   ").Append(Percent(rate.Chance)).Append('\n');
            }
            text.Append('\n');
        }

        text.Append("<size=38><b>확정 규칙</b></size>\n");
        if (banner.IsGoldBanner)
            text.Append($"·  {banner.RarePity}회 안에 레어 이상 확정 (지금 {manager.RarePityLeft(banner)}회 남음)\n");
        else
        {
            text.Append($"·  {banner.EpicPity}회 안에 에픽 확정 (지금 {manager.EpicPityLeft(banner)}회 남음)\n");
            text.Append($"·  {banner.RarePity}회 안에 레어 이상 확정\n");
        }
        if (banner.IsPickup)
        {
            text.Append($"·  에픽이 나오면 {Percent(banner.FeaturedShare)} 확률로 픽업 장난감, 놓치면 다음 에픽은 픽업 확정\n");
            text.Append("·  확정까지 남은 횟수는 다음 픽업 뽑기로 이어져요\n");
            var reward = manager.PointsReward(banner);
            if (banner.ExchangePoints > 0 && reward != null)
                text.Append($"·  한 번 뽑을 때마다 별빛 포인트 1점, {banner.ExchangePoints}점이면 {reward.DisplayName}{KoreanParticle.ObjectParticle(reward.DisplayName)} 받아요\n");
        }
        else if (banner.IsGoldBanner)
        {
            text.Append("·  골드로 언제든 뽑아요. 값은 왕국 레벨에 따라 올라요 ")
                .Append($"(지금 1회 {manager.GoldBannerCost(banner, 1):N0} · 10회 {manager.GoldBannerCost(banner, 10):N0})\n");
            text.Append("·  에픽 장난감은 나오지 않아요 (에픽은 보물 조개에서)\n");
            text.Append("·  확정까지 남은 횟수는 골드 뽑기끼리 이어져요\n");
        }
        else
        {
            text.Append("·  확정까지 남은 횟수는 상시 뽑기끼리 이어져요\n");
            if (banner.HasDailyGoldPull)
                text.Append("·  하루 한 번 골드로 1회 뽑을 수 있어요 (새벽 4시에 다시)\n");
        }
        var s = GachaRules.DuplicateShards;
        text.Append($"·  이미 있는 장난감은 반짝 조각을 덤으로 줘요 (흔함 {s[0]} · 레어 {s[1]} · 에픽 {s[2]})");
        return text.ToString();
    }

    private static string Percent(double value) =>
        (value * 100.0).ToString("0.###", CultureInfo.InvariantCulture) + "%";

    #endregion

    #region 화면 그리기

    private void Refresh()
    {
        var manager = GachaManager.Instance;
        if (manager == null)
            return;

        var state = _state;
        state.Banners.Clear();
        state.Banners.AddRange(manager.OpenBanners());
        if (state.Banners.Count == 0)
            return;
        if (_selected == null || !state.Banners.Contains(_selected))
            _selected = state.Banners[0];
        state.NewBanners.Clear();
        foreach (var banner in state.Banners)
        {
            if (manager.IsNewBanner(banner))
                state.NewBanners.Add(banner);
        }

        var selected = _selected;
        state.Selected = selected;
        var ends = selected.EndsAt;
        state.Limited = ends != null;
        state.Period = ends != null ? PeriodText(ends.Value - GameClock.Now) : "상시";

        var table = manager.Table(selected);
        state.Highlights.Clear();
        if (selected.IsPickup)
        {
            state.HighlightLabel = "확률 UP!";
            for (int tier = GachaTable.Epic; tier >= GachaTable.Common; tier--)
            {
                foreach (var item in table.Featured(tier))
                    state.Highlights.Add((item, tier, true));
            }
        }
        else if (selected.IsGoldBanner)
        {
            // 골드 배너: 레어 장난감 먼저, 그다음 가구
            state.HighlightLabel = "레어 장난감 · 가구";
            foreach (var item in table.Standard(GachaTable.Rare))
                state.Highlights.Add((item, GachaTable.Rare, false));
            foreach (var item in table.Standard(GachaTable.Common))
            {
                if (IsFurniture(item))
                    state.Highlights.Add((item, GachaTable.Common, false));
            }
        }
        else
        {
            state.HighlightLabel = "에픽 장난감";
            foreach (var item in table.Standard(GachaTable.Epic))
                state.Highlights.Add((item, GachaTable.Epic, false));
        }

        // 골드 배너는 에픽이 없어서 레어 이상 확정까지
        state.PityLabel = selected.IsGoldBanner ? "레어 확정까지" : "에픽 확정까지";
        state.PityTotal = selected.IsGoldBanner ? selected.RarePity : selected.EpicPity;
        state.PityLeft = selected.IsGoldBanner ? manager.RarePityLeft(selected) : manager.EpicPityLeft(selected);
        state.Guaranteed = manager.IsFeaturedGuaranteed(selected);

        state.ShowPoints = selected.IsPickup && selected.ExchangePoints > 0;
        state.Points = manager.Points(selected);
        state.PointsGoal = selected.ExchangePoints;
        state.PointsReward = manager.PointsReward(selected);

        state.ShowGold = selected.HasDailyGoldPull;
        state.GoldAvailable = manager.CanGoldPullToday(selected);
        state.GoldCost = manager.GoldPullCost(selected);
        state.GoldAffordable = Balance(manager.Gold) >= state.GoldCost;

        state.Shards = Balance(manager.Shard);
        // 골드 배너는 뽑기권 대신 가진 골드
        var held = selected.IsGoldBanner ? manager.Gold : selected.Ticket;
        state.TicketIcon = held != null ? held.Icon : null;
        state.Tickets = Balance(held);
        state.Single = PriceLook(selected, 1);
        state.Ten = PriceLook(selected, 10);

        _screen.Render(state);
        _dirty = false;
    }

    private static string PeriodText(TimeSpan left)
    {
        if (left.TotalDays >= 1)
            return $"{(int)left.TotalDays}일 남음";
        if (left.TotalHours >= 1)
            return $"{(int)left.TotalHours}시간 남음";
        return "곧 끝나요";
    }

    #endregion

    // 처음 보는 한글을 폰트 아틀라스에 미리 넣는다 (요정 상점과 같은 이유: 새 글자가 한꺼번에 추가되면 D3D12 에디터에서 멈춤)
    private void PrepareGlyphs(GachaManager manager)
    {
        var text = new StringBuilder("0123456789,.%/×+-!?·★()에픽레어흔함확정까지회남음일시간곧끝나요상시확률UP장난감별빛포인트교환골드뽑기오늘의내일다시")
            .Append("한번더10회톡두드려보세요마지막으로눌러서조개를한꺼번에열어요건너뛰기확인새NEW픽업반짝조각이미있는광장에두면해달친구가놀러와요")
            .Append("레어도어떤든올수있어요한정요정의선물을받았어요지난이됐어요모자라요하루에번이에요기간끝났어요규칙아래뽑을때받아요새벽4시")
            .Append("교환소꾸미기에서놓아가질없어요지금은바꿀이어져요끼리덤으로줘요보물바닷가봄바람정보장")
            .Append("가구언제든값은왕국레벨에따라올라요나오지않아요에픽은앉아쉬어가요둘러앉아간식을먹어요밤이면불빛아래모여요푸릇해져요아늑함");
        foreach (var banner in manager.Banners)
        {
            if (banner == null)
                continue;
            text.Append(banner.Title).Append(banner.Subtitle);
            if (banner.Ticket != null)
                text.Append(banner.Ticket.DisplayName);
            if (banner.FeaturedOtter != null)
                text.Append(banner.FeaturedOtter.DisplayName);
            foreach (var item in banner.Pool)
                AppendItem(text, item);
            foreach (var item in banner.Featured)
                AppendItem(text, item);
        }

        var fonts = new HashSet<TMP_FontAsset>();
        foreach (var root in new Component[] { _screen, _reveal, _rates, _exchange })
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                fonts.Add(label.font);
        }
        string characters = text.ToString();
        foreach (var font in fonts)
        {
            if (font != null)
                font.TryAddCharacters(characters, out _);
        }
        _glyphsReady = true;
    }

    private static void AppendItem(StringBuilder text, ItemDefinition item)
    {
        if (item == null)
            return;
        text.Append(item.DisplayName).Append(item.Description);
        if (item.Rarity != null)
            text.Append(item.Rarity.DisplayName);
    }
}
