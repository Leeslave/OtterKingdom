using UnityEngine;

/// <summary>The receptionist's board quest, shared with the town hall.</summary>
public class MineCommunitySheet : MonoBehaviour
{
    private Canvas _canvas;
    private SettlementManager _manager;
    public void Open(SettlementManager manager)
    {
        _manager = manager;
        if (_canvas == null)
        {
            _canvas = RuntimeUIKit.CreateCanvas("주민 건강 부탁", 230, false);
            _canvas.transform.SetParent(transform, false);
        }
        _canvas.gameObject.SetActive(true);
        Draw();
    }
    public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;
    public void Close() { if (_canvas != null) _canvas.gameObject.SetActive(false); }

    private void Draw(string hint = "")
    {
        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _canvas.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        var panel = RuntimeUIKit.CreatePanel(RuntimeUIKit.CreateDim(_canvas.transform), 850);
        RuntimeUIKit.CreateLabel(panel, _manager.MineCommunityTitle, 42, true);
        RuntimeUIKit.CreateLabel(panel, _manager.MineCommunityStatus, 26);
        var state = _manager.MineCommunity;
        if (!state.concernAccepted)
        {
            RuntimeUIKit.CreateLabel(panel, "또박이: 깊은 갱도가 열린 뒤부터 쉬는 해달들이 늘었어요.\n상인에게 약초를 부탁하고, 의사 선생님을 모셔 와요.", 28);
            RuntimeUIKit.CreateButton(panel, "주민들을 돕겠다고 약속하기", () => { _manager.AcceptMineConcern(); Draw(); });
        }
        else if (!state.treated)
        {
            RuntimeUIKit.CreateLabel(panel, "요정 상인이 첫 진료에 쓸 약초 10개를 지원했어요.\n가방에 받은 약초를 납품하면 의사 다온이가 임시 진료를 시작해요.", 28);
            if (!state.supportReceived)
                RuntimeUIKit.CreateButton(panel, "지원 약초 10개 받기", () => Draw(_manager.ReceiveMineHerbs() ? "가방에 약초를 담았어요." : "가방에 약초 10개가 들어갈 공간을 마련해 주세요."));
            RuntimeUIKit.CreateButton(panel, "추가 약초 · 요정 상점", () =>
            {
                var shop = FindAnyObjectByType<FairyShopPresenter>();
                if (shop == null) { Draw("광장에서 요정 상인을 만나 주세요."); return; }
                Close();
                shop.Open();
            });
            RuntimeUIKit.CreateButton(panel, "약초 10개 납품 · 임시 진료 시작", () => Draw(_manager.TreatMineResidents() ? "다온이: 증상은 덜해질 거예요. 이제 갱도의 원인을 찾아주세요." : "가방에 약초 10개가 필요해요."));
        }
        else if (!state.sourceRemoved)
            RuntimeUIKit.CreateLabel(panel, "다온이: 진료로 증상을 덜었지만 원인을 없애야 해요.\n5층에서 재료를 모아 별빛 다이아 곡괭이를 만들고, 가장 안쪽 발생 돌을 부숴 주세요.\n5층 입장에는 해님 금 곡괭이만 필요해요.", 28);
        else if (!_manager.HasBuilt(MineCommunityState.HospitalId))
            RuntimeUIKit.CreateLabel(panel, "또박이: 이제 정식 병원을 지어요.\n광장 꾸미기 → 건물 → 마을 병원\n골드 2,000 · 돌 30개 · 공사 2분\n다온이가 마을에 남아 주민들을 돌볼 거예요.", 28);
        else if (!_manager.HasBuilt(MineCommunityState.GuildId))
            RuntimeUIKit.CreateLabel(panel, "또박이: 힘을 모아 이겨 낸 일을 앞으로도 이어가요.\n광장 꾸미기 → 건물 → 마을 길드\n골드 3,000 · 돌 40개 · 공사 3분\n마을회관의 의뢰를 도울 길드 공간을 마련해요.", 28);
        else if (!state.exported)
        {
            RuntimeUIKit.CreateLabel(panel, "요정 상인: 발생 돌의 잔여 조각은 밀봉했어요.\n이 조각을 요정 나라로 보내 약초 지원에 답례해요.\n가스 발생원은 다시 생기지 않아요.", 28);
            RuntimeUIKit.CreateButton(panel, "밀봉된 조각 인도하기", () => { _manager.CompleteMineExport(); Draw(); });
        }
        else RuntimeUIKit.CreateLabel(panel, "또박이: 모두가 함께 마을을 지켰어요.\n병원과 마을 길드가 남았고, 요정 나라에 답례도 전했어요.\n1~5층의 광맥은 계속 탐험하고 자동 채굴할 수 있어요.", 28);
        if (!string.IsNullOrEmpty(hint)) RuntimeUIKit.CreateLabel(panel, hint, 25);
        RuntimeUIKit.CreateButton(panel, "닫기", Close, RuntimeUIKit.ButtonKind.Secondary);
    }
}
