using UnityEngine;

public partial class SettlementManager
{
    public MineCommunityState MineCommunity => InventoryManager.Instance?.MineMazeState?.community;
    public float MineHealthMultiplier => MineCommunity?.ProductionMultiplier ?? 1f;
    public bool HasMineCommunityStory => MineCommunity != null && MineCommunity.started;
    public string MineCommunityTitle => MineCommunity == null || !MineCommunity.concernAccepted ? "또박이의 걱정"
        : !MineCommunity.treated ? "주민들을 위한 약초"
        : !MineCommunity.sourceRemoved ? "깊은 갱도의 원인"
        : !HasBuilt(MineCommunityState.HospitalId) ? "마을에 남을 병원"
        : !HasBuilt(MineCommunityState.GuildId) ? "함께 세우는 마을 길드"
        : !MineCommunity.exported ? "요정 나라에 보내는 답례" : "다시 평온해진 마을";
    public string MineCommunityStatus => !HasMineCommunityStory ? ""
        : MineCommunity.sourceRemoved ? "주민들이 회복했어요. 농장 성장 속도가 정상으로 돌아왔어요."
        : $"주민 건강: {(MineCommunity.treated ? "임시 진료 중" : "휴식 필요")} · 농장 성장 속도 -{(MineCommunity.treated ? 5 : 10)}%\n접속하지 않아도 더 나빠지지 않아요.";

    private MineCommunitySheet _mineCommunitySheet;
    private bool _mineConcernNotified;
    private bool _mineRecoveryNotified;

    public void OpenMineCommunity()
    {
        if (!HasMineCommunityStory) return;
        if (_mineCommunitySheet == null)
            _mineCommunitySheet = gameObject.AddComponent<MineCommunitySheet>();
        _mineCommunitySheet.Open(this);
    }

    private void TickMineCommunity()
    {
        var state = MineCommunity;
        if (state == null || !state.started) return;
        if (state.sourceRemoved && !HasDevelopment("mine_source_removed"))
        {
            Settlement.UnlockDevelopment("mine_source_removed");
            SaveRequested?.Invoke();
            OnChanged?.Invoke();
        }
        if (HasBuilt(MineCommunityState.HospitalId) && !HasDevelopment("mine_hospital"))
        {
            Settlement.UnlockDevelopment("mine_hospital");
            SaveRequested?.Invoke();
            OnChanged?.Invoke();
        }
        // Notices are deferred until outside the mine, where the board can be used.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MineMaze") return;
        if (!state.concernAccepted && !_mineConcernNotified)
        {
            _mineConcernNotified = true;
            GameNotices.Post(new GameNotice("또박이: 요즘 기침하고 쉬는 해달들이 많아졌어요. 게시판에서 이야기해요.", "부탁 보기", OpenMineCommunity));
        }
        if (state.sourceRemoved && !state.exported && !_mineRecoveryNotified)
        {
            _mineRecoveryNotified = true;
            GameNotices.Post(new GameNotice("또박이: 해달들이 다시 기운을 냈어요! 이제 마을에 병원을 마련해요.", "부탁 보기", OpenMineCommunity));
        }
    }

    public bool AcceptMineConcern()
    {
        if (MineCommunity == null || !MineCommunity.AcceptConcern()) return false;
        Settlement.UnlockDevelopment("mine_herbs_open");
        SaveRequested?.Invoke();
        OnChanged?.Invoke();
        return true;
    }

    public bool ReceiveMineHerbs()
    {
        var state = MineCommunity;
        var manager = InventoryManager.Instance;
        if (state == null || !state.concernAccepted || state.supportReceived || manager == null
            || !manager.Config.ItemDatabase.TryGet(MineCommunityState.HerbId, out var herb)
            || manager.Inventory.GetAddableAmount(herb) < MineCommunityState.HerbsRequired) return false;
        manager.Inventory.Add(herb, MineCommunityState.HerbsRequired, ItemChangeReason.Grant);
        state.supportReceived = true;
        SaveRequested?.Invoke();
        OnChanged?.Invoke();
        return true;
    }

    public bool TreatMineResidents()
    {
        var manager = InventoryManager.Instance;
        if (MineCommunity == null || manager == null
            || !manager.Config.ItemDatabase.TryGet(MineCommunityState.HerbId, out var herb)
            || !MineCommunity.Treat(manager.Inventory, herb)) return false;
        Settlement.UnlockDevelopment("mine_temporary_clinic");
        Settlement.SetResident("otter_mine_doctor", ResidentState.Resident);
        SaveRequested?.Invoke();
        OnChanged?.Invoke();
        return true;
    }

    public bool CompleteMineExport()
    {
        if (MineCommunity == null || !MineCommunity.Export(HasBuilt(MineCommunityState.HospitalId), HasBuilt(MineCommunityState.GuildId)))
            return false;
        // Sealed fragments are a unique story delivery, not another gas-producing resource.
        Settlement.UnlockDevelopment("mine_fairy_trade");
        SaveRequested?.Invoke();
        OnChanged?.Invoke();
        return true;
    }
}
