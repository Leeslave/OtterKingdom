using UnityEngine;

/// <summary>회관에서 여는 왕국 발전과 연구 화면.</summary>
public sealed class ProsperityScreen : MonoBehaviour
{
    private Canvas _canvas;
    private ProsperityManager _manager;
    private int _tab;
    private bool _redrawPending;
    private Inventory _observedBag;
    private CurrencyManager _observedCurrency;
    private string _marketMessage;
    public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

    public void Open(ProsperityManager manager)
    {
        if (_manager != null) _manager.Research.Changed -= Refresh;
        UnobserveMarket();
        _manager = manager;
        _tab = 0;
        _manager.Research.Changed += Refresh;
        _observedBag = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory : null;
        _observedCurrency = CurrencyManager.Instance;
        if (_observedBag != null) _observedBag.OnItemChanged += HandleMarketInventory;
        if (_observedCurrency != null) _observedCurrency.OnCurrencyChanged += HandleMarketCurrency;
        if (_canvas == null) _canvas = RuntimeUIKit.CreateCanvas("왕국 발전", 240, false);
        _canvas.gameObject.SetActive(true);
        _redrawPending = false;
        Draw();
    }

    private void Close()
    {
        _canvas.gameObject.SetActive(false);
        _redrawPending = false;
        _manager.Research.Changed -= Refresh;
        UnobserveMarket();
    }

    private void Refresh()
    {
        if (_canvas != null && _canvas.gameObject.activeSelf) _redrawPending = true;
    }

    private void LateUpdate()
    {
        if (!_redrawPending) return;
        _redrawPending = false;
        if (_canvas != null && _canvas.gameObject.activeSelf) Draw();
    }

    private void Draw()
    {
        RuntimeUIKit.PrepareGlyphs("왕국 발전 연구 공동사업 물류 기록 새 시대 시작 2 RP 연구 완료 연구하기 준비 중 선행 연구가 필요해요 연구 포인트가 부족해요 Lv.0123456789에 열려요 공동사업 열기 일터 열기 창고 열기 선지급 받기 의뢰 취소 진행 초기화 수확 의뢰 받기 판매 의뢰 받기 주문 받기 납품하고 보상 받기 주문 취소 주문 완료");
        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _canvas.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        var window = RuntimeUIWindow.Create(_canvas.transform, "왕국 발전", null,
            new[] { "연구", "공동사업", "물류", "기록" }, _tab, tab => { _tab = tab; Draw(); }, Close);
        var content = window.Content;
        var research = _manager.Research;
        int level = GameManager.KingdomLevel;
        RuntimeUICards.Text(content, $"연구 포인트 {research.Data.rp} RP", 38, true);
        if (!research.Data.introCompleted)
        {
            if (_manager.NextStep == ProsperityFlowStep.Castle)
            {
                RuntimeUICards.Text(content, ProsperityManager.StepMessage(ProsperityFlowStep.Castle), 30);
                RuntimeUIKit.CreateButton(content, "성 준비 보기", () => { Close(); _manager.OpenNextStep(); });
            }
            RuntimeUICards.Text(content, "또박이: 함께 세운 왕국에 새로운 기술을 더해 봐요. 연구를 통해 일터와 마을을 발전시킬 수 있어요.", 30);
            var settlement = SettlementManager.Instance;
            bool gas = settlement != null && (settlement.HasDevelopment("mine_source_removed") || settlement.MineCommunity?.sourceRemoved == true);
            bool castle = settlement != null && (settlement.IsCastleBuilt || settlement.Settlement.IsCompleted(CastleRules.RequestId));
            bool canEnter = ProsperityResearch.CanEnter(level, gas, castle);
            RuntimeUICards.Text(content, $"Lv.20 이상 {(level >= 20 ? "완료" : "필요")} · 가스 사건 해결 {(gas ? "완료" : "필요")} · 성 완공 {(castle ? "완료" : "필요")}", 26);
            RuntimeUICards.Text(content, $"시작하면 진입 2 RP와 이미 도달한 Lv.21~{ProsperityResearch.LastResearchLevel}의 RP를 한 번씩 받아요.", 26);
            RuntimeUIKit.CreateButton(content, "새 시대 시작", () => _manager.BeginEra(), RuntimeUIKit.ButtonKind.Primary).interactable = canEnter;
            return;
        }
        DrawNextStep(content, research, level);
        if (_tab == 1) { DrawProjects(content, research, level); return; }
        if (_tab == 2)
        {
            RuntimeUICards.Text(content, "가공품과 왕국 자재를 확인해요", 34, true);
            RuntimeUICards.Text(content, "공동 창고를 연구하면 보관 공간이 늘고, 운송 수레를 연구하면 가공품을 자동으로 모을 수 있어요.", 28);
            var owner = _manager.SettlementOwner;
            RuntimeUIKit.CreateButton(content, "일터 열기", () => { Close(); owner.OpenProduction(); }).interactable = owner != null;
            RuntimeUIKit.CreateButton(content, "창고 열기", () => { Close(); owner.OpenWarehouse(); }).interactable = owner != null;
            return;
        }
        if (_tab == 3)
        {
            RuntimeUICards.Text(content, "왕국의 발전 기록", 36, true);
            RuntimeUICards.Text(content, "새 시대 시작 · 완료", 28);
            foreach (var tech in ProsperityResearch.Techs)
                if (research.HasTech(tech.Id)) RuntimeUICards.Text(content, tech.Name + " · 연구 완료", 28);
            if (research.Data.rpGrants.Contains("prosperity:project:" + ProsperityResearch.PlanProjectId))
                RuntimeUICards.Text(content, "번영 계획 수립 · 완료 · 3 RP 지급", 28);
            foreach (var id in research.Data.advanceCompleted)
                RuntimeUICards.Text(content, (id == ProsperityResearch.HarvestAdvance ? "수확" : "판매") + " 선지급 의뢰 · 완료", 28);
            foreach (var order in ProsperityResearch.MarketOrders)
                if (research.Data.marketCompleted.Contains(order.Id)) RuntimeUICards.Text(content, order.Title + " · 납품 완료", 28);
            return;
        }
        RuntimeUICards.Text(content, "연구를 마치면 일터와 마을의 새 기능을 사용할 수 있어요.", 28);
        DrawMarket(content, research, level);
        RuntimeUICards.Text(content, "왕국 기술 연구", 36, true);
        foreach (var tech in ProsperityResearch.Techs)
        {
            var card = RuntimeUICards.Card(content, true);
            RuntimeUICards.Text(card, tech.Name, 34, true);
            RuntimeUICards.Text(card, tech.Effect, 28);
            string prerequisites = "";
            foreach (string id in tech.Prerequisites)
            {
                if (prerequisites.Length > 0) prerequisites += ", ";
                prerequisites += ProsperityResearch.Find(id).Name;
            }
            RuntimeUICards.Text(card, $"Lv.{tech.Level} · {tech.Cost} RP" + (prerequisites.Length > 0 ? $" · 선행: {prerequisites}" : ""), 24);
            var result = research.Check(tech.Id, level);
            string label = result == ResearchResult.AlreadyLearned ? "연구 완료" :
                result == ResearchResult.NeedsLevel ? $"Lv.{tech.Level}에 열려요" :
                result == ResearchResult.NeedsPrerequisite ? "선행 연구가 필요해요" :
                result == ResearchResult.NotEnoughRp ? "연구 포인트가 부족해요" :
                result == ResearchResult.NotImplemented ? "준비 중" : "연구하기";
            RuntimeUIKit.CreateButton(card, label, () => research.TryLearn(tech.Id, GameManager.KingdomLevel),
                RuntimeUIKit.ButtonKind.Primary).interactable = result == ResearchResult.Success;
        }
    }

    private void DrawNextStep(Transform content, ProsperityResearch research, int level)
    {
        var step = _manager.NextStep;
        if (step == ProsperityFlowStep.None || step == ProsperityFlowStep.Intro) return;
        var card = RuntimeUICards.Card(content, true);
        RuntimeUICards.Text(card, "다음 발전", 34, true);
        RuntimeUICards.Text(card, ProsperityManager.StepMessage(step), 28);
        if (step == ProsperityFlowStep.Sawmill || step == ProsperityFlowStep.Warehouse)
        {
            string id = step == ProsperityFlowStep.Sawmill ? TechIds.Sawmill : TechIds.Warehouse;
            var tech = ProsperityResearch.Find(id);
            var label = tech.Name + $" 연구 · {tech.Cost} RP";
            RuntimeUIKit.PrepareGlyphs(label);
            RuntimeUIKit.CreateButton(card, label, () => research.TryLearn(id, GameManager.KingdomLevel),
                RuntimeUIKit.ButtonKind.Primary).interactable = research.Check(id, level) == ResearchResult.Success;
        }
        else
        {
            string label = ProsperityManager.StepAction(step);
            RuntimeUIKit.PrepareGlyphs(label);
            RuntimeUIKit.CreateButton(card, label, () => { Close(); _manager.OpenNextStep(); }, RuntimeUIKit.ButtonKind.Primary);
        }
    }

    private void DrawMarket(Transform content, ProsperityResearch research, int level)
    {
        RuntimeUICards.Text(content, "장터 주문", 36, true);
        if (!research.HasTech(TechIds.Market))
        {
            RuntimeUICards.Text(content, "E01 장터 주문을 연구하면 가방 재료를 납품할 수 있어요.", 28);
            return;
        }
        RuntimeUICards.Text(content, "각 주문은 한 번씩 완료할 수 있어요. 만료 없이 기다려 주며, 주문을 취소해도 재료나 골드가 줄지 않아요. 납품하면 표시된 재료를 전부 건네고 보상을 받아요.", 26);
        if (!string.IsNullOrEmpty(_marketMessage)) RuntimeUICards.Text(content, _marketMessage, 28, true);
        foreach (var order in ProsperityResearch.MarketOrders)
        {
            var card = RuntimeUICards.Card(content, true);
            RuntimeUICards.Text(card, order.Title, 32, true);
            int owned = _manager.MarketItemCount(order.ItemId);
            RuntimeUICards.Text(card, $"{_manager.MarketItemName(order.ItemId)} {order.Amount}개 · 보유 {System.Math.Max(0, owned)}개\n보상: {order.Gold}골드", 28);
            if (research.Data.marketCompleted.Contains(order.Id))
            {
                RuntimeUICards.Text(card, "주문 완료 · 보상 수령 완료", 28, true);
                continue;
            }
            if (!research.Data.marketAccepted.Contains(order.Id))
            {
                RuntimeUIKit.CreateButton(card, "주문 받기", () =>
                {
                    _marketMessage = MarketResultText(research.AcceptMarketOrder(order.Id, GameManager.KingdomLevel));
                    Refresh();
                }).interactable = level >= 21;
                continue;
            }
            var check = _manager.CheckMarketDelivery(order.Id);
            if (check != MarketOrderResult.Success) RuntimeUICards.Text(card, MarketResultText(check), 26, muted: true);
            RuntimeUIKit.CreateButton(card, "납품하고 보상 받기", () =>
            {
                var result = _manager.DeliverMarketOrder(order.Id);
                _marketMessage = result == MarketOrderResult.Success ? $"납품 완료! {order.Gold}골드를 받았어요." : MarketResultText(result);
                Refresh();
            }, RuntimeUIKit.ButtonKind.Primary).interactable = check == MarketOrderResult.Success;
            RuntimeUIKit.CreateButton(card, "주문 취소", () =>
            {
                _marketMessage = MarketResultText(research.CancelMarketOrder(order.Id, GameManager.KingdomLevel));
                Refresh();
            });
        }
    }

    private static string MarketResultText(MarketOrderResult result) => result switch
    {
        MarketOrderResult.Success => null,
        MarketOrderResult.NeedsMarket => "장터 주문 연구가 필요해요.",
        MarketOrderResult.NeedsLevel => "Lv.21부터 주문을 받을 수 있어요.",
        MarketOrderResult.NotEnoughItems => "납품할 재료가 부족해요.",
        MarketOrderResult.GoldCapacity => "골드 보상을 전부 받을 공간이 필요해요.",
        MarketOrderResult.AlreadyCompleted => "이미 완료한 주문이에요.",
        MarketOrderResult.AlreadyAccepted => "이미 받은 주문이에요.",
        MarketOrderResult.NotAccepted => "먼저 주문을 받아 주세요.",
        MarketOrderResult.Busy => "납품을 처리하고 있어요.",
        _ => "주문을 준비하고 있어요. 잠시 후 다시 시도해 주세요.",
    };

    private void HandleMarketInventory(ItemChangedEvent change) => Refresh();
    private void HandleMarketCurrency(Currency currency, int amount) => Refresh();

    private void UnobserveMarket()
    {
        if (_observedBag != null) _observedBag.OnItemChanged -= HandleMarketInventory;
        if (_observedCurrency != null) _observedCurrency.OnCurrencyChanged -= HandleMarketCurrency;
        _observedBag = null;
        _observedCurrency = null;
    }

    private void DrawProjects(Transform content, ProsperityResearch research, int level)
    {
        var card = RuntimeUICards.Card(content, true);
        RuntimeUICards.Text(card, "번영 계획 수립", 36, true);
        RuntimeUICards.Text(card, "목재 12개 · 돌 12개를 모으고 왕국의 다음 계획을 확정해요.\n완료 보상: 3 RP · 납품한 재료는 돌려받을 수 없어요.", 28);
        var owner = _manager.SettlementOwner;
        var plan = _manager.Plan;
        bool done = plan != null && owner != null && CommunityProjectRules.IsCompleted(plan, owner.Settlement);
        RuntimeUIKit.CreateButton(card, done ? "번영 계획 완료" : "공동사업 열기", () =>
        {
            Close();
            _manager.OpenPlan();
        }, RuntimeUIKit.ButtonKind.Primary).interactable = !done && plan != null && owner != null;

        var advanceCard = RuntimeUICards.Card(content, true);
        RuntimeUICards.Text(advanceCard, "연구 포인트 선지급 의뢰", 34, true);
        RuntimeUICards.Text(advanceCard, "미래의 레벨 보상 1 RP를 먼저 받아요. Lv.26·27에서 당겨 받은 RP는 다시 지급되지 않아요. 수락한 뒤의 행동만 세어요.", 28);
        var active = research.Data.advance;
        if (active != null)
        {
            int goal = ProsperityResearch.AdvanceGoal(active.id);
            string objective = active.id == ProsperityResearch.HarvestAdvance ? "작물 수확" : "판매 금액";
            RuntimeUICards.Text(advanceCard, $"{objective} {active.progress:N0} / {goal:N0}\n보상: Lv.{active.futureLevel}의 1 RP 선지급", 28);
            RuntimeUICards.Text(advanceCard, "수령 전에 해당 레벨에 도달하면 정상 레벨 보상을 받고 의뢰는 종료돼요. 취소하면 진행 수량이 초기화돼요.", 24, muted: true);
            RuntimeUIKit.CreateButton(advanceCard, "선지급 받기", () => research.ClaimAdvance(GameManager.KingdomLevel),
                RuntimeUIKit.ButtonKind.Primary).interactable = active.progress >= goal && level < active.futureLevel;
            RuntimeUIKit.CreateButton(advanceCard, "의뢰 취소 · 진행 초기화", () => research.CancelAdvance());
        }
        else
        {
            RuntimeUICards.Text(advanceCard, "수확 의뢰: 작물 30개 수확 · 판매 의뢰: 판매로 1,000골드 벌기\n한 번에 한 의뢰만 진행하며 각 의뢰의 선지급은 한 번만 받을 수 있어요.", 28);
            RuntimeUIKit.CreateButton(advanceCard, "수확 의뢰 받기", () => research.AcceptAdvance(ProsperityResearch.HarvestAdvance, GameManager.KingdomLevel))
                .interactable = research.CanAcceptAdvance(ProsperityResearch.HarvestAdvance, level);
            RuntimeUIKit.CreateButton(advanceCard, "판매 의뢰 받기", () => research.AcceptAdvance(ProsperityResearch.SalesAdvance, GameManager.KingdomLevel))
                .interactable = research.CanAcceptAdvance(ProsperityResearch.SalesAdvance, level);
            if (research.AvailableAdvanceLevel(level) == 0)
                RuntimeUICards.Text(advanceCard, "Lv.26·27의 RP는 이미 받았어요. 더 받을 선지급분이 없어요.", 26, muted: true);
        }
    }

    private void OnDestroy()
    {
        UnobserveMarket();
        if (_manager != null && _manager.Research != null) _manager.Research.Changed -= Refresh;
        if (_canvas != null) Destroy(_canvas.gameObject);
    }
}
