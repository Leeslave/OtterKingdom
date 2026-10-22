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

    // 뽑기 화면이 열린 동안 전역 UI 캔버스 순서: 알림 띠(150)·장소 팝업(200) 위, 튜토리얼(500) 아래
    private const int CoverSortingOrder = 250;

    private readonly GachaScreenState _state = new GachaScreenState();
    private Canvas _canvas;
    private int _baseSortingOrder;
    private bool _covering;
    private GachaBannerDefinition _selected;
    private int _lastCount = 1;
    private bool _dirty;
    private bool _glyphsReady;
    private CurrencyManager _boundCurrency;
    private GachaManager _boundManager;

    public bool IsOpen => _screen.IsOpen;

    /// <summary>뽑기 화면이 게임 화면을 다 가리는 중 (보상 날아가기·알림 띠는 쉬거나 기다림)</summary>
    public static bool IsCovering => Instance != null && (Instance.IsOpen || GachaRevealView.IsPlaying);

    private void Awake()
    {
        // 중복은 GlobalUIRoot가 먼저 꺼서 여기까지 오지 않지만, 혹시 모를 경우를 대비
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        _canvas = GetComponentInChildren<Canvas>().rootCanvas;
        _baseSortingOrder = _canvas.sortingOrder;
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
        _screen.OnRosterClicked += HandleRosterClicked;
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
        _screen.OnRosterClicked -= HandleRosterClicked;
        _screen.OnExchangeClicked -= HandleExchangeClicked;
        _screen.OnPointsClicked -= HandlePointsClicked;
        _reveal.OnClosed -= HandleRevealClosed;
        _reveal.OnAgain -= HandleAgain;
        _exchange.OnExchange -= HandleShardExchange;
        Bind(null, null);
        SetCovering(false);
    }

    // 재화 · 뽑기 기록이 바뀌면 열린 화면을 다시 그림. 매니저들은 늦게 생길 수 있어 바뀌었는지만 보고 다시 붙음
    private void Update()
    {
        SetCovering(IsCovering);
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

    // 열린 동안 전역 UI를 장소 팝업·알림 띠 위로 올려, 뒤에서 뜬 다른 화면(수확 팝업 등)이 뽑기 위에 보이지 않게
    private void SetCovering(bool covering)
    {
        if (_covering == covering || _canvas == null)
            return;
        _covering = covering;
        _canvas.sortingOrder = covering ? CoverSortingOrder : _baseSortingOrder;
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
        var starter = manager.TryGiveStarterOtter();
        if (starter != null)
            lines.Add($"요정의 선물! {starter.DisplayName}{KoreanParticle.SubjectParticle(starter.DisplayName)} 명부에 들어와 응원을 맡았어요 ({GachaRewardText.Bonus(starter.Bonus, starter.PercentAt(1))})");
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

    // 결과 카드 안내: 해달 조각 = 해금 · 별 승급으로 받는 편성 버프, 장신구 = 낀 해달 한 마리의 효과, 재료 = 쓰는 곳
    private (string hint, Sprite portrait) Describe(GachaPull pull)
    {
        var manager = GachaManager.Instance;
        switch (pull.Kind)
        {
            case GachaRewardKind.Fragments:
            {
                var otter = pull.Otter;
                if (otter == null)
                    return ("조각을 모으면 특수 해달을 해금해요", null);
                int stars = manager.Companions.Stars(otter);
                return stars <= 0
                    ? ($"조각을 모아 해금하면 편성해서 {GachaRewardText.Bonus(otter.Bonus, otter.PercentAt(1))}", null)
                    : stars >= SpecialOtterDefinition.MaxStars
                        ? ($"5성 {otter.DisplayName}: {GachaRewardText.Bonus(otter.Bonus, otter.PercentAt(stars))}", null)
                        : ($"별을 올리면 {GachaRewardText.Bonus(otter.Bonus, otter.PercentAt(stars + 1))}", null);
            }
            case GachaRewardKind.Accessory:
                return ("주민 해달 한 마리에게 끼우면 그 해달만 효과를 받아요", null);
            default:
                return ("가방에 넣었어요. 집 · 시설 공사에 써요", null);
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
        string name = reward != null ? $"{GachaRewardText.FragmentName(reward)} {manager.PointsFragments}개" : "픽업 해달 조각";
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

    private static string ExchangeNote() =>
        $"반짝 조각으로 원하는 특수 해달 조각을 골라요. 5성까지 쓰고 남는 조각 · 최대 강화 장신구가 반짝 조각이 돼요";

    private void HandleShardExchange(SpecialOtterDefinition otter)
    {
        var manager = GachaManager.Instance;
        string name = GachaRewardText.FragmentName(otter);
        string note;
        switch (manager.TryExchangeShards(otter))
        {
            case GachaExchangeOutcome.Exchanged:
                AudioManager.Play(SfxKind.RevealRare);
                int stars = manager.Companions.Stars(otter);
                int need = CompanionCollection.NextStarCost(otter, stars);
                note = need > 0
                    ? $"{name}{KoreanParticle.ObjectParticle(name)} 받았어요! ({(stars == 0 ? "해금" : $"{stars + 1}성")}까지 {manager.Companions.Fragments(otter)}/{need})"
                    : $"{name}{KoreanParticle.ObjectParticle(name)} 받았어요!";
                break;
            case GachaExchangeOutcome.NotEnough:
                _exchange.Shake();
                note = "반짝 조각이 모자라요";
                break;
            case GachaExchangeOutcome.Full:
                note = $"{otter.DisplayName}{KoreanParticle.SubjectParticle(otter.DisplayName)} 이미 5성까지 쓸 조각이 있어요";
                break;
            default:
                note = "지금은 바꿀 수 없어요";
                break;
        }
        _exchange.Render(manager.ExchangeEntries(), Balance(manager.Shard), note);
        Refresh();
    }

    // 해달 명부 (특수 해달 · 편성 · 장신구): 런타임에 그리는 화면, 처음 열 때 만듦
    private void HandleRosterClicked()
    {
        if (_roster == null)
            _roster = gameObject.AddComponent<CompanionScreen>();
        _roster.Open();
    }

    private CompanionScreen _roster;

    private void HandleRatesClicked()
    {
        _rates.Show($"{_selected.Title} 확률", RatesText(_selected));
    }

    /// <summary>확률 공개: 한 번 뽑을 때 등급 · 보상별 확률(천장 제외) + 확정 규칙</summary>
    private static string RatesText(GachaBannerDefinition banner)
    {
        var manager = GachaManager.Instance;
        var table = manager.Table(banner);
        if (table == null)
            return "";
        var rates = manager.Rates(banner);
        var text = new StringBuilder();
        text.Append("<size=26><color=#A08672>한 번 뽑을 때의 확률이에요. 확정 규칙은 아래에 있어요.</color></size>\n\n");
        for (int tier = GachaTable.Epic; tier >= GachaTable.Common; tier--)
        {
            double chance = GachaRewardRules.TierChance(table, tier);
            if (chance <= 0)
                continue;
            text.Append($"<size=38><b>{TierNames[tier]}  {Percent(chance)}</b></size>\n");
            foreach (var rate in rates)
            {
                if (rate.Tier != tier)
                    continue;
                text.Append(rate.Featured ? "<color=#E5484D>★ 픽업</color>  " : "·  ");
                text.Append(rate.Name).Append("   ").Append(Percent(rate.Chance)).Append('\n');
            }
            text.Append('\n');
        }

        text.Append("<size=38><b>확정 규칙</b></size>\n");
        text.Append($"·  {banner.EpicPity}회 안에 에픽 해달 조각 {table.PityFragments}개 확정 (지금 {manager.EpicPityLeft(banner)}회 남음)\n");
        text.Append($"·  {banner.RarePity}회 안에 레어 이상 확정\n");
        text.Append($"·  해달 조각은 레어 {table.RareFragments.min}~{table.RareFragments.max}개 · 에픽 {table.EpicFragments.min}~{table.EpicFragments.max}개씩 나와요\n");
        if (banner.IsPickup)
        {
            text.Append($"·  해달 조각이 나오면 {Percent(banner.FeaturedShare)} 확률로 픽업 해달, 에픽 픽업을 놓치면 다음 에픽 조각은 픽업 확정\n");
            text.Append("·  확정까지 남은 횟수는 다음 픽업 뽑기로 이어져요\n");
            var reward = manager.PointsReward(banner);
            if (banner.ExchangePoints > 0 && reward != null)
            {
                string name = $"{GachaRewardText.FragmentName(reward)} {manager.PointsFragments}개";
                text.Append($"·  한 번 뽑을 때마다 별빛 포인트 1점, {banner.ExchangePoints}점이면 {name}{KoreanParticle.ObjectParticle(name)} 받아요\n");
            }
        }
        else
        {
            text.Append("·  확정까지 남은 횟수는 상시 뽑기끼리 이어져요\n");
            if (banner.HasDailyGoldPull)
                text.Append("·  하루 한 번 골드로 1회 뽑을 수 있어요 (새벽 4시에 다시)\n");
        }
        text.Append("·  같은 장신구는 강화(최대 +4), 그 뒤로는 반짝 조각이 돼요\n");
        text.Append("·  5성까지 쓰고 남는 해달 조각은 반짝 조각이 돼요");
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
            for (int tier = GachaTable.Epic; tier >= GachaTable.Rare; tier--)
            {
                foreach (var otter in table.Featured(tier))
                    state.Highlights.Add((manager.IconOf(otter), tier, true));
            }
        }
        else
        {
            state.HighlightLabel = "에픽 해달";
            foreach (var otter in table.Otters(GachaTable.Epic))
                state.Highlights.Add((manager.IconOf(otter), GachaTable.Epic, false));
        }

        state.PityLabel = "에픽 조각 확정까지";
        state.PityTotal = selected.EpicPity;
        state.PityLeft = manager.EpicPityLeft(selected);
        state.Guaranteed = manager.IsFeaturedGuaranteed(selected);

        state.ShowPoints = selected.IsPickup && selected.ExchangePoints > 0;
        state.Points = manager.Points(selected);
        state.PointsGoal = selected.ExchangePoints;
        var pointsReward = manager.PointsReward(selected);
        state.PointsRewardIcon = pointsReward != null ? manager.IconOf(pointsReward) : null;

        state.Note = null;
        state.NoteIcon = manager.Gold != null ? manager.Gold.Icon : null;

        state.ShowGold = selected.HasDailyGoldPull;
        state.GoldAvailable = manager.CanGoldPullToday(selected);
        state.GoldCost = manager.GoldPullCost(selected);
        state.GoldAffordable = Balance(manager.Gold) >= state.GoldCost;

        state.Shards = Balance(manager.Shard);
        var held = selected.Ticket;
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
        }
        text.Append("재료꾸러미목공소집공사에쓰는목재와돌가방이가득해해금성까지조각강화최대라장신구주민이동속도작업시간얻는양작물성장판매가")
            .Append("편성해서별을올리면효과를받아요넣었어요시설써요원하는특수고르남는쓰고받았어요조각을모으면한마리에게끼우면그만수확량물고기생산속도");
        var config = manager.RewardConfig;
        if (config != null)
        {
            foreach (var otter in config.Otters)
            {
                if (otter != null)
                    text.Append(otter.DisplayName).Append(otter.Description);
            }
            foreach (var accessory in config.Accessories)
            {
                if (accessory != null)
                    text.Append(accessory.DisplayName).Append(accessory.Description);
            }
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
}
